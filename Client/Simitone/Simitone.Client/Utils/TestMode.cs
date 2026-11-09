using FSO.Client;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Opt-in automation for development testing, so a test run needs no clicks, keys or window focus.
    /// Nothing changes unless one of these environment variables is set:
    ///   SIMITONE_TEST_LOT=5          play this house straight away instead of showing the neighbourhood
    ///   SIMITONE_TEST_DIALOGS=auto   answer game dialogs automatically (see AnswerDialog)
    ///   SIMITONE_TEST_SCRIPT=file    run the commands in this file once the lot is ready (see RunCommand)
    ///   SIMITONE_TEST_OUT=dir        where "shot" saves screenshots (default: the working directory)
    /// Dialogs, SimAntics exceptions, the game hour and script steps are logged to the console as "TEST ..." lines.
    /// (To keep the window from taking focus on macOS, also set SDL_MAC_BACKGROUND_APP=1.)
    /// </summary>
    public static class TestMode
    {
        public static readonly bool Enabled;
        public static bool AutoDialogs;
        public static bool Quitting; //"quit": exit without the lot's save prompt

        private static int Lot;
        private static Queue<string> Script = new Queue<string>();
        private static string OutDir;

        private static string NextText = "";
        private static byte NextAnswer = 0;
        private static Func<TS1GameScreen, bool> Waiting;
        private static string PendingShot;
        private static int LastHour = -1;

        static TestMode()
        {
            int.TryParse(Environment.GetEnvironmentVariable("SIMITONE_TEST_LOT"), out Lot);
            AutoDialogs = Environment.GetEnvironmentVariable("SIMITONE_TEST_DIALOGS") == "auto";
            var script = Environment.GetEnvironmentVariable("SIMITONE_TEST_SCRIPT");
            OutDir = Environment.GetEnvironmentVariable("SIMITONE_TEST_OUT") ?? Directory.GetCurrentDirectory();

            if (script != null)
            {
                foreach (var line in File.ReadAllLines(script))
                {
                    var cmd = line.Trim();
                    if (cmd != "" && !cmd.StartsWith("#")) Script.Enqueue(cmd);
                }
                //a script can start with "lot 5" to choose its house
                if (Script.Count > 0 && Script.Peek().StartsWith("lot ")) int.TryParse(Script.Dequeue().Substring(4), out Lot);
            }
            Enabled = Lot > 0 || AutoDialogs || script != null;
        }

        public static void Log(string message)
        {
            Console.WriteLine("TEST " + message);
        }

        /// <summary>
        /// The house to play instead of showing the neighbourhood, once per run.
        /// </summary>
        public static bool TakeLot(out short house)
        {
            house = (short)Lot;
            if (Lot <= 0) return false;
            Lot = 0;
            Log("playing house " + house + " (" + (long)(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds + " ms after the game started)");
            return true;
        }

        /// <summary>
        /// Logs a dialog from the VM. Returns true when test mode answered it, so the UI should not show it.
        /// </summary>
        public static bool HandleDialog(VM vm, VMDialogInfo info)
        {
            if (!Enabled) return false;
            var type = (info.Operand == null) ? VMDialogType.Message : info.Operand.Type;
            var message = (info.Message ?? "").Replace("\r", "").Replace("\n", " ");
            Log("dialog " + type + " \"" + info.Title + "\": " + message);
            if (!AutoDialogs) return false;
            if (info.Block) AnswerDialog(vm, info, type);
            return true;
        }

        /// <summary>
        /// Answers a blocking dialog: the button chosen with "answer" (default the first), the text from "text"
        /// for text entry, the first option for pet and clothes choices, and cancel for map and phone dialogs.
        /// </summary>
        private static void AnswerDialog(VM vm, VMDialogInfo info, VMDialogType type)
        {
            byte code = NextAnswer;
            string text = "";
            switch (type)
            {
                case VMDialogType.TextEntry:
                    code = 0;
                    text = NextText;
                    NextText = "";
                    break;
                case VMDialogType.TS1PetChoice:
                case VMDialogType.TS1Clothes:
                    code = 1;
                    text = "0";
                    break;
                case VMDialogType.NumericEntry:
                case VMDialogType.TS1Vacation:
                case VMDialogType.TS1Neighborhood:
                case VMDialogType.TS1StudioTown:
                case VMDialogType.TS1Magictown:
                case VMDialogType.TS1PhoneBook:
                    code = 0;
                    break;
            }
            Log("answered " + code + ((text == "") ? "" : " \"" + text + "\""));
            vm.SendCommand(new VMNetDialogResponseCmd
            {
                ActorUID = info.Caller?.PersistID ?? 0,
                ResponseCode = code,
                ResponseText = text
            });
        }

        /// <summary>
        /// Called every frame by the game screen: logs the game hour and runs the script.
        /// </summary>
        public static void Update(TS1GameScreen screen)
        {
            var vm = screen.vm;
            if (!Enabled || vm == null || !vm.Ready) return;

            var clock = vm.Context.Clock;
            if (clock.Hours != LastHour)
            {
                LastHour = clock.Hours;
                Log("time " + clock.Hours + ":" + clock.Minutes.ToString("00") + " speed " + vm.SpeedMultiplier);
            }

            while (PendingShot == null)
            {
                if (Waiting != null)
                {
                    if (!Waiting(screen)) return;
                    Waiting = null;
                }
                if (Script.Count == 0) return;
                var cmd = Script.Dequeue();
                Log("> " + cmd);
                try
                {
                    RunCommand(screen, cmd);
                }
                catch (Exception e)
                {
                    Log("command failed: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Script commands, one per line (# starts a comment):
        ///   lot 5                      (first line only) the house to play, like SIMITONE_TEST_LOT
        ///   speed 0-3                  pause, normal, fast, ultra
        ///   wait 5                     wait 5 seconds of real time
        ///   ticks 300                  wait 300 VM ticks
        ///   until 14:30                wait for that time of day in the game
        ///   select Bella               control this Sim (name prefix)
        ///   interact Mailbox | Get Mail   make the controlled Sim use an object (both are name matches)
        ///   dialogs auto|manual        answer dialogs automatically or show them
        ///   answer 1                   button for later automatic answers (0 yes/ok, 1 no, 2 cancel)
        ///   text Biscuit               text for the next text entry dialog
        ///   shot name                  save a screenshot as name.png
        ///   sims                       log the Sims on the lot and what each is doing
        ///   log message                write a line to the log
        ///   mode buy                   switch to live, buy, build or options mode
        ///   category 1                 open that category of the current mode
        ///   subcategory 1 1            open a Build catalog subcategory (category, then its index)
        ///   search lamp                type this into the catalog search
        ///   time category 1            run a step and log how long it took
        ///   namecheck                  compare every catalog item's saved name with the name from loading its object
        ///   scanreport                 log how the startup object scan went (catalog index use) and a fingerprint of its result
        ///   catcount                   log how many catalog items each category has
        ///   frames 30                  log how long the next 30 frames take (slowest and average)
        ///   quit                       exit without saving
        /// </summary>
        private static void RunCommand(TS1GameScreen screen, string cmd)
        {
            var vm = screen.vm;
            var space = cmd.IndexOf(' ');
            var name = (space == -1) ? cmd : cmd.Substring(0, space);
            var arg = (space == -1) ? "" : cmd.Substring(space + 1).Trim();

            switch (name)
            {
                case "speed":
                    screen.ChangeSpeedTo(int.Parse(arg));
                    break;
                case "wait":
                    var end = DateTime.Now.AddSeconds(double.Parse(arg, CultureInfo.InvariantCulture));
                    Waiting = (s) => DateTime.Now >= end;
                    break;
                case "ticks":
                    var endTick = vm.Scheduler.CurrentTickID + uint.Parse(arg);
                    Waiting = (s) => s.vm.Scheduler.CurrentTickID >= endTick;
                    break;
                case "until":
                    var parts = arg.Split(':');
                    var target = int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
                    var last = TimeOfDay(vm);
                    Waiting = (s) =>
                    {
                        //the clock can skip minutes at high speed, so wait until it passes the target
                        var now = TimeOfDay(s.vm);
                        var passed = (last <= now) ? (last < target && target <= now) : (last < target || target <= now);
                        last = now;
                        return passed;
                    };
                    break;
                case "select":
                    var sim = vm.Context.ObjectQueries.Avatars.FirstOrDefault(x => x.Name.StartsWith(arg, StringComparison.OrdinalIgnoreCase));
                    if (sim == null) throw new Exception("no Sim called " + arg);
                    vm.SendCommand(new VMNetChangeControlCmd() { TargetID = sim.ObjectID });
                    //the change happens on a later tick; wait for it so the next command acts as this Sim
                    Waiting = (s) => s.SelectedAvatar == sim;
                    break;
                case "interact":
                    Interact(screen, arg);
                    break;
                case "dialogs":
                    AutoDialogs = arg == "auto";
                    break;
                case "answer":
                    NextAnswer = byte.Parse(arg);
                    break;
                case "text":
                    NextText = arg;
                    break;
                case "shot":
                    PendingShot = Path.Combine(OutDir, arg + ".png");
                    break;
                case "log":
                    Log(arg);
                    break;
                case "sims":
                    Log("sims on lot: " + string.Join(", ", vm.Context.ObjectQueries.Avatars.OfType<VMAvatar>()
                        .Select(x => x.Name + " (" + (x.Thread.ActiveAction?.Name ?? "idle") + ")")));
                    break;
                case "mode":
                    screen.Frontend.SwitchMode((UIMainPanelMode)Enum.Parse(typeof(UIMainPanelMode), arg, true));
                    break;
                case "category":
                    screen.Frontend.MainPanel.Switcher_OnCategorySelect(int.Parse(arg));
                    break;
                case "subcategory":
                    var browse = screen.Frontend.MainPanel.SubPanel as UIBuyBrowsePanel;
                    if (browse == null) throw new Exception("no catalog open");
                    var sub = arg.Split(' ');
                    browse.InitSubcategory(UIBuyBrowsePanel.BuildCategories[int.Parse(sub[0])][int.Parse(sub[1])]);
                    break;
                case "search":
                    var catalog = screen.Frontend.MainPanel.SubPanel as UIBuyBrowsePanel;
                    if (catalog == null) throw new Exception("no catalog open");
                    catalog.ApplyNameFilter(arg);
                    break;
                case "time":
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    RunCommand(screen, arg);
                    Log("took " + watch.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms: " + arg);
                    break;
                case "frames":
                    //log how long the next N frames take (the slowest and the average), e.g. right after a search
                    var count = int.Parse(arg);
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    var times = new List<double>();
                    var lastFrame = 0.0;
                    Waiting = (s) =>
                    {
                        var now = clock.Elapsed.TotalMilliseconds;
                        times.Add(now - lastFrame);
                        lastFrame = now;
                        if (times.Count < count) return false;
                        Log("frames: slowest " + times.Max().ToString("0.0", CultureInfo.InvariantCulture) + " ms, average "
                            + times.Average().ToString("0.0", CultureInfo.InvariantCulture) + " ms over " + count);
                        return true;
                    };
                    break;
                case "catcount":
                    //how many catalog items each category has (custom content shows up here)
                    var cat = FSO.Content.Content.Get().WorldCatalog;
                    Log("catalog: " + string.Join(", ", Enumerable.Range(0, 30).Select(c => c + "=" + cat.GetItemsByCategory((sbyte)c).Count).Where(x => !x.EndsWith("=0"))));
                    break;
                case "scanreport":
                    //how the object scan at startup went, and a fingerprint of what it found (to compare runs)
                    var scanned = FSO.Content.Content.Get().WorldObjects as FSO.Content.TS1.TS1ObjectProvider;
                    Log("scan: " + FSO.Content.TS1.TS1ObjectProvider.LastScanReport + " | fingerprint " + scanned?.ScanFingerprint());
                    break;
                case "namecheck":
                    //compares every catalog item's name from the scan (the catalog index) with the name the old way gave:
                    //loading the whole object and asking its resources
                    var wc = FSO.Content.Content.Get().WorldCatalog;
                    var wo = FSO.Content.Content.Get().WorldObjects;
                    int total = 0, same = 0, noName = 0; var odd = new List<string>();
                    for (sbyte c = 0; c < 30; c++)
                    {
                        foreach (var item in wc.GetItemsByCategory(c))
                        {
                            total++;
                            string oldName = null;
                            var go = wo.Get(item.GUID);
                            if (go != null) oldName = go.Resource.Get<FSO.Files.Formats.IFF.Chunks.CTSS>(go.OBJ.CatalogStringsID)?.GetString(0);
                            var newName = string.IsNullOrEmpty(item.CatalogName) ? null : item.CatalogName;
                            if (string.IsNullOrEmpty(oldName)) oldName = null;
                            if (oldName == null) noName++;
                            if (oldName == newName) same++;
                            else if (odd.Count < 8) odd.Add(item.GUID.ToString("x8") + " old '" + oldName + "' new '" + newName + "'");
                        }
                    }
                    Log("names: " + total + " catalog items, " + same + " the same (" + noName + " have no name), " + (total - same) + " differ" + (odd.Count > 0 ? ": " + string.Join("; ", odd) : ""));
                    break;
                case "type":
                    screen.Frontend.MainPanel.SetSearchText(arg); //typing into the catalog search box (no text clears it)
                    break;
                case "focus":
                    screen.Frontend.MainPanel.TestFocus = arg != "off"; //draw the search box as focused
                    break;
                case "quit":
                    Quitting = true;
                    GameFacade.Game.Exit();
                    break;
                default:
                    throw new Exception("unknown command");
            }
        }

        private static int TimeOfDay(VM vm)
        {
            return vm.Context.Clock.Hours * 60 + vm.Context.Clock.Minutes;
        }

        private static void Interact(TS1GameScreen screen, string arg)
        {
            var vm = screen.vm;
            var split = arg.Split('|');
            if (split.Length != 2) throw new Exception("expected: interact object | interaction");
            var objName = split[0].Trim();
            var actionName = split[1].Trim();

            var caller = screen.SelectedAvatar;
            if (caller == null) throw new Exception("no Sim is selected");
            foreach (var obj in vm.Entities.Where(x => x.ToString().IndexOf(objName, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var action = obj.GetPieMenu(vm, caller, false, true)
                    .FirstOrDefault(x => x.Name.IndexOf(actionName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (action == null) continue;
                Log("queueing \"" + action.Name + "\" on " + obj + " for " + caller.Name);
                vm.SendCommand(new VMNetInteractionCmd
                {
                    Interaction = action.ID,
                    ActorUID = caller.PersistID,
                    CalleeID = obj.ObjectID,
                    Param0 = action.Param0,
                    Global = action.Global
                });
                return;
            }
            throw new Exception("no " + objName + " offers " + actionName);
        }

        /// <summary>
        /// Called after each frame is drawn: saves the screenshot requested by "shot".
        /// </summary>
        public static void AfterDraw(GraphicsDevice gd)
        {
            if (PendingShot == null) return;
            var w = gd.PresentationParameters.BackBufferWidth;
            var h = gd.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            gd.GetBackBufferData(data);
            for (int i = 0; i < data.Length; i++) data[i].A = 255;
            using (var tex = new Texture2D(gd, w, h))
            using (var file = File.Create(PendingShot))
            {
                tex.SetData(data);
                tex.SaveAsPng(file, w, h);
            }
            Log("saved " + PendingShot);
            PendingShot = null;
        }
    }
}
