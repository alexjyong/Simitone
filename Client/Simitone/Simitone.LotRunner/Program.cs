using FSO.Common;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Marshals;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Primitives;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Simitone.LotRunner
{
    /// <summary>
    /// Plays TS1 lots headlessly (no window, graphics or sound), as fast as the VM can tick, and reports
    /// SimAntics exceptions, dialogs, objects arriving and leaving, and the household at the end.
    /// It plays like a careful player: it answers No to yes/no questions (such as adopting a baby)
    /// and calls the fire department when a fire starts, so the household stays the same between runs.
    /// Nothing is saved: the neighbourhood and lots are only read.
    ///
    /// Usage: Simitone.LotRunner -path "/path/to/The Sims/" [-user ~/Documents/Simitone/] [-hours 24] [-quiet] house...|all
    ///   house    house numbers to play, one after another (e.g. 5 for the Goths), or "all" for every house a family lives in
    ///   -path    the TS1 install
    ///   -user    Simitone's user folder; saved lot states in its LocalHouse/ are loaded like the game does
    ///   -hours   game hours to play per lot (default 24)
    ///   -quiet   only print exceptions and the summary
    /// Exit code: SimAntics exceptions plus crashed lots, up to 255 (0 = clean).
    /// </summary>
    public class Program
    {
        private const int TicksPerGameMinute = 30;
        private const uint FireFighterGUID = 0x14c180ba;
        //game minutes to wait for the fire fighter before calling again (they usually take under an hour)
        private const int FireCallRetry = 60;

        private static bool Quiet;
        private static int Exceptions;
        private static int Crashes;
        //distinct exceptions (message without the object instance) and how often each happened
        private static Dictionary<string, int> ExceptionCounts = new Dictionary<string, int>();
        private static VM Vm;

        public static int Main(string[] args)
        {
            string path = null;
            var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Simitone/");
            var hours = 24;
            var houses = new List<short>();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-path": path = args[++i]; break;
                    case "-user": userDir = args[++i]; break;
                    case "-hours": hours = int.Parse(args[++i]); break;
                    case "-quiet": Quiet = true; break;
                    case "all": houses.Add(-1); break;
                    default: houses.Add(short.Parse(args[i])); break;
                }
            }
            if (path == null || houses.Count == 0)
            {
                Console.WriteLine("usage: Simitone.LotRunner -path <TS1 install> [-user <Simitone user dir>] [-hours 24] [-quiet] house...|all");
                return -1;
            }

            var timer = Stopwatch.StartNew();
            InitContent(path, userDir);
            Console.WriteLine("content loaded in " + timer.Elapsed.TotalSeconds.ToString("0.0") + "s");
            if (houses.Remove(-1)) houses.AddRange(Content.Get().Neighborhood.FamilyForHouse.Keys.Where(x => x > 0).OrderBy(x => x));

            foreach (var house in houses)
            {
                timer.Restart();
                var finished = RunLot(house, hours);
                var family = Content.Get().Neighborhood.GetFamilyForHouse(house);
                Console.WriteLine("house " + house + " (" + ((family == null) ? "no family" : Content.Get().Neighborhood.GetFamilyString(family.ChunkID)?.GetString(0)) + "): " + (finished ? hours + " game hours" : "crashed") + " in " + timer.Elapsed.TotalSeconds.ToString("0.0") + "s");
            }
            if (Crashes > 0) Console.WriteLine(Crashes + " lot(s) crashed");
            Console.WriteLine(Exceptions + " SimAntics exception(s), " + ExceptionCounts.Count + " distinct");
            foreach (var ex in ExceptionCounts.OrderByDescending(x => x.Value)) Console.WriteLine(ex.Value.ToString().PadLeft(6) + "  " + ex.Key);
            return Math.Min(Exceptions + Crashes, 255); //exit codes wrap at 256
        }

        private static void InitContent(string path, string userDir)
        {
            path = path.Replace('\\', '/');
            if (!path.EndsWith("/")) path += "/";
            FSOEnvironment.UserDir = userDir;
            FSOEnvironment.ContentDir = "Content/";
            FSOEnvironment.NoSound = true;
            VM.UseWorld = false;

            Content.Target = FSOEngineMode.TS1;
            Content.TS1HybridBasePath = path;
            VMContext.InitVMConfig(true);
            Content.Init(path, ContentMode.SERVER);

            var neigh = Content.Get().Neighborhood;
            neigh.PreparePersonDataFromObject = PersonGeneratorHelper.PreparePersonDataFromObject;
            neigh.AddMissingNeighbors();
        }

        /// <summary>
        /// Loads a house the way TS1GameScreen does (saved state from LocalHouse/, else the house file) and plays it.
        /// Returns false if the VM crashed.
        /// </summary>
        private static bool RunLot(short house, int hours)
        {
            var neigh = Content.Get().Neighborhood;
            var family = neigh.GetFamilyForHouse(house);
            var lotPath = neigh.GetHousePath(house);

            var driver = new VMServerDriver(new VMTS1GlobalLinkStub());
            var vm = new VM(new VMContext(null), driver, new VMNullHeadlineProvider());
            Vm = vm;
            vm.Init();
            vm.OnDialog += OnDialog;
            if (family != null)
            {
                family.SelectWholeFamily();
                vm.TS1State.ActivateFamily(vm, family);
            }

            var saved = Path.Combine(FSOEnvironment.UserDir, "LocalHouse/", Path.GetFileNameWithoutExtension(lotPath) + ".fsov");
            if (File.Exists(saved))
            {
                using (var file = new BinaryReader(File.OpenRead(saved)))
                {
                    var marshal = new VMMarshal();
                    marshal.Deserialize(file);
                    vm.Load(marshal);
                    vm.Reset();
                }
                Log("house " + house + ": loaded saved state " + saved);
            }
            else
            {
                vm.SendCommand(new VMBlueprintRestoreCmd
                {
                    JobLevel = short.Parse(lotPath.Substring(lotPath.Length - 6, 2)),
                    XMLData = File.ReadAllBytes(lotPath),
                    IffData = true
                });
                Log("house " + house + ": loaded " + lotPath);
            }

            vm.MyUID = 65537;
            driver.ConnectClient(new VMNetClient
            {
                PersistID = 1,
                RemoteIP = "local",
                AvatarState = new VMNetAvatarPersistState()
                {
                    Name = "",
                    DefaultSuits = new VMAvatarDefaultSuits(false),
                    PersistID = 1,
                    Permissions = FSO.SimAntics.Model.TSOPlatform.VMTSOAvatarPermissions.Admin,
                    Budget = 1000000
                }
            });

            //the first ticks run the restore command and connect the client
            vm.Tick();
            foreach (var error in vm.LoadErrors) Log("load error: " + error);
            Log("family: " + ((family == null) ? "none" : string.Join(", ", Avatars(vm, true).Select(x => x.Name))));

            var lastHour = -1;
            var present = ObjectNames(vm);
            var arrived = new List<string>();
            var left = new List<string>();
            //what each family member on the lot was last doing, to report it when they leave
            var familyOnLot = Avatars(vm, true).ToDictionary(x => x.Name, x => x.Thread.ActiveAction?.Name ?? "idle");
            //game minute of the last fire department call, while a fire burns
            int? fireCalled = null;
            var fireReported = false;
            var fireFighter = false;
            var ticks = hours * 60 * TicksPerGameMinute;
            for (int i = 1; i <= ticks; i++)
            {
                try
                {
                    vm.Tick();
                }
                catch (Exception e)
                {
                    //thrown outside a SimAntics thread (which would be caught and reported as an exception); the game would crash too
                    Crashes++;
                    Console.WriteLine("CRASH at " + vm.Context.Clock.Hours + ":" + vm.Context.Clock.Minutes.ToString("00") + ", stopping this lot: " + e);
                    Vm = null;
                    return false;
                }
                if (i % TicksPerGameMinute != 0) continue;

                //check objects every game minute, so short visits (a car that leaves in ten minutes) show up too
                var now = ObjectNames(vm);
                arrived.AddRange(now.Except(present));
                left.AddRange(present.Except(now));
                present = now;

                var clock = vm.Context.Clock;
                var time = clock.Hours + ":" + clock.Minutes.ToString("00");
                var familyNow = Avatars(vm, true).ToDictionary(x => x.Name, x => x.Thread.ActiveAction?.Name ?? "idle");
                foreach (var sim in familyOnLot.Keys.Except(familyNow.Keys)) Log(time + " " + sim + " left the lot (last doing " + familyOnLot[sim] + ")");
                foreach (var sim in familyNow.Keys.Except(familyOnLot.Keys)) Log(time + " " + sim + " came back");
                familyOnLot = familyNow;

                var fireFighterNow = vm.Context.ObjectQueries.Avatars.Any(x => x.Object.OBJ.GUID == FireFighterGUID);
                if (fireFighterNow != fireFighter) Log(time + " fire fighter " + (fireFighterNow ? "arrived" : "left"));
                fireFighter = fireFighterNow;
                var minute = i / TicksPerGameMinute;
                if (!vm.Entities.Any(x => x.ToString() == "Fire"))
                {
                    if (fireReported) Log(time + " fire is out");
                    fireCalled = null;
                    fireReported = false;
                }
                //call when a fire starts, and again if nobody has come after a while (the caller can burn or not reach the phone);
                //a smoke alarm (if the lot has one) calls them too
                else if (!fireFighter && (fireCalled == null || minute - fireCalled >= FireCallRetry))
                {
                    var call = CallFireDepartment(vm);
                    if (call != null) fireCalled = minute;
                    //nobody home (at work or school) yet: try again every minute, but only say so once
                    if (call != null || !fireReported) Log(time + " fire! " + (call ?? "nobody on the lot can call the fire department yet"));
                    fireReported = true;
                }

                if (clock.Hours != lastHour)
                {
                    lastHour = clock.Hours;
                    var cameAndWent = arrived.Intersect(left).ToList();
                    Log(clock.Hours + ":" + clock.Minutes.ToString("00") + " sims on lot: " + string.Join(", ", Avatars(vm, false).Select(x => x.Name + " (" + (x.Thread.ActiveAction?.Name ?? "idle") + ")"))
                        + List(" | arrived: ", arrived.Except(cameAndWent))
                        + List(" | left: ", left.Except(cameAndWent))
                        + List(" | came and went: ", cameAndWent));
                    arrived.Clear();
                    left.Clear();
                }
            }

            foreach (var sim in Avatars(vm, true))
            {
                var action = sim.Thread.ActiveAction?.Name ?? "nothing";
                Console.WriteLine("  " + sim.Name + ": doing " + action + ", " + sim.Thread.Queue.Count + " queued");
            }
            vm.CloseNet(VMCloseNetReason.LeaveLot);
            Vm = null;
            return true;
        }

        private static IEnumerable<VMAvatar> Avatars(VM vm, bool familyOnly)
        {
            var family = vm.TS1State.CurrentFamily;
            return vm.Context.ObjectQueries.Avatars.OfType<VMAvatar>().Where(x => !familyOnly || (family != null && family.RuntimeSubset.Contains(x.Object.OBJ.GUID)));
        }

        //engine helper objects that come and go constantly; not interesting in the log
        private static string[] HelperObjects = new string[] { "Destination", "ReserveTile", "Social Interactions", "*Social Interactions" };

        /// <summary>
        /// The objects on the lot, one entry per multitile object, to spot cars, visitors and so on coming and going.
        /// </summary>
        private static HashSet<string> ObjectNames(VM vm)
        {
            var names = new HashSet<string>();
            foreach (var ent in vm.Entities)
            {
                if (ent is VMAvatar || ent.MultitileGroup.BaseObject != ent) continue;
                var name = ent.ToString();
                if (HelperObjects.Any(x => name.StartsWith(x))) continue;
                names.Add(name + " #" + ent.ObjectID);
            }
            return names;
        }

        /// <summary>
        /// Has a family member phone the fire department, as a player would. Returns what happened, for the log,
        /// or null if nobody can (everyone is at work or school, or there is no phone).
        /// </summary>
        private static string CallFireDepartment(VM vm)
        {
            //a sim who is awake and not panicking yet gets to the phone sooner
            var family = Avatars(vm, true).OrderBy(x =>
            {
                var action = x.Thread.ActiveAction?.Name;
                return (action == "Sleep" || action == "Nap") ? 2 : (action == "Fire!") ? 1 : 0;
            });
            var phones = vm.Entities.Where(x => x.ToString().Contains("Phone")).ToList();
            foreach (var caller in family)
            {
                //sims away from the lot get no phone interactions
                foreach (var phone in phones)
                {
                    var call = phone.GetPieMenu(vm, caller, false, true).FirstOrDefault(x => x.Name.EndsWith("Fire Dept."));
                    if (call == null) continue;
                    //interactions go to the selected sim, so select the caller first, as a player would
                    vm.SendCommand(new VMNetChangeControlCmd { TargetID = caller.ObjectID });
                    vm.SendCommand(new VMNetInteractionCmd
                    {
                        Interaction = call.ID,
                        ActorUID = caller.PersistID,
                        CalleeID = phone.ObjectID,
                        Param0 = call.Param0,
                        Global = call.Global
                    });
                    return caller.Name + " calls the fire department on the " + phone;
                }
            }
            return null;
        }

        private static string List(string label, IEnumerable<string> items)
        {
            var list = items.ToList();
            return (list.Count == 0) ? "" : label + string.Join(", ", list);
        }

        private static void OnDialog(VMDialogInfo info)
        {
            if (info == null) return;
            var message = (info.Message ?? "").Replace("\r", "").Replace("\n", " ");
            if (info.Title == "SimAntics Exception!")
            {
                Exceptions++;
                //the message names the object as (Callee:Caller), which differs per instance; group without it
                var key = message.Replace("A SimAntics Exception has occurred, and has been suppressed:", "").Replace("The object will be reset. Please report this!", "").Trim();
                ExceptionCounts.TryGetValue(key, out var count);
                ExceptionCounts[key] = count + 1;
                if (count == 0) Console.WriteLine("EXCEPTION " + key);
                return;
            }
            Log("dialog \"" + info.Title + "\": " + message);
            if (!info.Block) return;
            //answer so the VM keeps running: No to questions, so nothing changes the household (adoption, moving in);
            //otherwise the first button (OK)
            var type = (info.Operand == null) ? VMDialogType.Message : info.Operand.Type;
            var no = type == VMDialogType.YesNo || type == VMDialogType.YesNoCancel;
            Log("  answered " + (no ? info.No ?? "No" : info.Yes ?? "OK"));
            Vm.SendCommand(new VMNetDialogResponseCmd
            {
                ActorUID = info.Caller?.PersistID ?? 0,
                ResponseCode = (byte)(no ? 1 : 0),
                ResponseText = ""
            });
        }

        private static void Log(string message)
        {
            if (!Quiet) Console.WriteLine(message);
        }
    }
}
