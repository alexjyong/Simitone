
using FSO.Client;
using FSO.Client.Debug;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common;
using FSO.Common.Rendering.Framework;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Files.RC;
using FSO.HIT;
using FSO.LotView;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Marshals;
using FSO.SimAntics.Marshals.Threads;
using FSO.SimAntics.Model;
using FSO.SimAntics.Model.TS1Platform;
using FSO.SimAntics.NetPlay;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Panels;
using Simitone.Client.UI.Panels.WorldUI;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Screens
{
    public class TS1GameScreen : FSO.Client.UI.Framework.GameScreen
    {
        public UIContainer WindowContainer;
        public bool Downtown;
        public bool Desktop = !FSOEnvironment.SoftwareKeyboard;

        public UILotControl LotControl { get; set; }
        public UISimitoneFrontend Frontend { get; set; }
        private FSO.LotView.World World;
        public FSO.SimAntics.VM vm { get; set; }
        public VMNetDriver Driver;
        public UISimitoneBg Bg;
        public uint VisualBudget { get; set; }

        //for TS1 hybrid mode
        public UINeighborhoodSelectionPanel TS1NeighPanel;
        public FAMI ActiveFamily;

        /// <summary>
        /// Essential lot infrastructure objects that must be kept on eviction.
        /// VerifyFamily searches for the mailbox by GUID to place newly-spawned sims;
        /// if absent, sims spawn at OUT_OF_WORLD and become invisible/unselectable.
        /// </summary>
        private static readonly HashSet<uint> EssentialLotObjects = new HashSet<uint>
        {
            0xEF121974u, // Mailbox (primary - used by VerifyFamily)
            0x1D95C9B0u, // Mailbox (alternate)
            0x39CCF441u, // Mailbox (2-tile)
            0xA4258067u, // Trash bin
            0x313D2F9Au, // Phone
            0x303CD603u, // Phone (community lots)
            0x865A6812u, // Car portal entrance
            0xD564C66Bu, // Car portal exit
        };

        public bool InLot
        {
            get
            {
                return (vm != null);
            }
        }

        private int m_ZoomLevel;
        public int ZoomLevel
        {
            get
            {
                return m_ZoomLevel;
            }
            set
            {
                value = Math.Max(1, Math.Min(3, value));

                if (value < 4)
                {
                    if (vm == null)
                    {

                    }
                    else
                    {
                        var targ = (WorldZoom)(4 - value); //near is 3 for some reason... will probably revise
                        //HITVM.Get().PlaySoundEvent(UIMusic.None);
                        LotControl.Visible = true;
                        Bg.Visible = false;
                        World.Visible = true;
                        //ucp.SetMode(UIUCP.UCPMode.LotMode);
                        LotControl.SetTargetZoom(targ);
                        if (m_ZoomLevel != value) vm.Context.World.InitiateSmoothZoom(targ);
                        vm.Context.World.State.Zoom = targ;
                        m_ZoomLevel = value;
                    }
                }
                else //cityrenderer! we'll need to recreate this if it doesn't exist...
                {
                    if (m_ZoomLevel < 4)
                    { //coming from lot view... snap zoom % to 0 or 1
                        if (World != null)
                        {
                            LotControl.Visible = false;
                        }
                    }
                    m_ZoomLevel = value;
                }
            }
        }

        private int _Rotation = 0;
        public int Rotation
        {
            get
            {
                return _Rotation;
            }
            set
            {
                _Rotation = value;
                World.State.CenterTile = World.EstTileAtPosWithScroll(new Vector2(ScreenWidth / 2, ScreenHeight / 2));
                if (World != null)
                {
                    switch (_Rotation)
                    {
                        case 0:
                            World.State.Rotation = WorldRotation.TopLeft; break;
                        case 1:
                            World.State.Rotation = WorldRotation.TopRight; break;
                        case 2:
                            World.State.Rotation = WorldRotation.BottomRight; break;
                        case 3:
                            World.State.Rotation = WorldRotation.BottomLeft; break;
                    }
                }
                World.RestoreTerrainToCenterTile();
            }
        }

        public sbyte Level
        {
            get
            {
                if (World == null) return 1;
                else return World.State.Level;
            }
            set
            {
                if (World != null)
                {
                    World.State.Level = value;
                }
            }
        }

        public sbyte Stories
        {
            get
            {
                if (World == null) return 2;
                return World.Stories;
            }
        }

        public VMAvatar SelectedAvatar
        {
            get
            {
                return vm.GetAvatarByPersist(vm.MyUID);
            }
        }

        public TS1GameScreen(NeighSelectionMode mode) : base()
        {
            Bg = new UISimitoneBg();
            Bg.Position = (new Vector2(ScreenWidth, ScreenHeight)) / 2;
            Add(Bg);

            WindowContainer = new UIContainer();
            Add(WindowContainer);

            if (Content.Get().TS1)
            {
                NeighSelection(mode);
            }
        }
        public int? MoveInFamily;

        public void StartMoveIn(int familyID)
        {
            MoveInFamily = familyID;
        }

        public void NeighSelection(NeighSelectionMode mode)
        {
            Content.Get().Neighborhood.PreparePersonDataFromObject = PersonGeneratorHelper.PreparePersonDataFromObject;
            Content.Get().Neighborhood.AddMissingNeighbors();
            var nbd = (ushort)((mode == NeighSelectionMode.MoveInMagic) ? 7 : 4);
            TS1NeighPanel = new UINeighborhoodSelectionPanel(nbd);
            var switcher = new UINeighbourhoodSwitcher(TS1NeighPanel, nbd, mode != NeighSelectionMode.Normal);
            TS1NeighPanel.OnHouseSelect += (house) =>
            {
                if (MoveInFamily != null)
                {
                    //move them in first
                    //confirm it
                    UIMobileAlert confirmDialog = null;
                    confirmDialog = new UIMobileAlert(new UIAlertOptions()
                    {
                        Title = GameFacade.Strings.GetString("132", "0"),
                        Message = GameFacade.Strings.GetString("132", "1"),
                        Buttons = UIAlertButton.YesNo((b) =>
                        {
                            confirmDialog.Close();
                            MoveInAndPlay((short)house, MoveInFamily.Value, switcher);
                        },
                        (b) => confirmDialog.Close())
                    });
                    UIScreen.GlobalShowDialog(confirmDialog, true);
                }
                else
                {
                    PlayHouse((short)house, switcher);
                }
            };
            Add(TS1NeighPanel);
            Add(switcher);
        }

        public void PlayHouse(short house, UIElement switcher)
        {
            ActiveFamily = Content.Get().Neighborhood.GetFamilyForHouse((short)house);
            InitializeLot(Content.Get().Neighborhood.GetHousePath(house), false);// "UserData/Houses/House21.iff"
            Remove(TS1NeighPanel);
            if (switcher != null) Remove(switcher);
        }

        public void MoveInAndPlay(short house, int family, UIElement switcher)
        {
            MoveInFamily = null;
            var neigh = Content.Get().Neighborhood;
            var fami = neigh.GetFamily((ushort)family);
            neigh.SetFamilyForHouse(house, fami, true);
            PlayHouse(house, switcher);
        }

        public void EvictLot(FAMI family, short houseID)
        {
            family.Budget += family.ValueInArch;
            family.ValueInArch = 0;
            Content.Get().Neighborhood.MoveOut(houseID);
            ResetHouse(houseID);
            Content.Get().Neighborhood.SaveNeighbourhood(false);
            TS1NeighPanel.SelectHouse(houseID);
        }

        private static readonly string DebugLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "simitone_evict_debug.txt");

        private static void DbgLog(string msg)
        {
            try { System.IO.File.AppendAllText(DebugLogPath, "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + msg + "\n"); }
            catch { }
        }

        /// <summary>
        /// Resets a house by removing buy-mode objects and avatars while keeping
        /// architecture (walls, floors) and build-mode objects (doors, windows, stairs, etc.).
        /// </summary>
        private void ResetHouse(int houseID)
        {
            System.IO.File.WriteAllText(DebugLogPath, ""); // clear log each eviction
            DbgLog("=== ResetHouse houseID=" + houseID + " ===");
            var neigh = Content.Get().Neighborhood;
            var houseIff = neigh.GetHouse(houseID);
            var fsov = houseIff.Get<FSOV>(1);
            DbgLog("FSOV in IFF: " + (fsov != null ? "YES (" + fsov.Data.Length + " bytes)" : "NO -> Case B"));

            VMMarshal marshal;

            if (fsov != null)
            {
                // Case A: House has FSOV (played in Simitone) - deserialize and filter
                DbgLog("--- Case A ---");
                marshal = new VMMarshal();
                using (var reader = new BinaryReader(new MemoryStream(fsov.Data)))
                {
                    marshal.Deserialize(reader);
                }
                DbgLog("Entities in FSOV: " + marshal.Entities.Length);
                DbgLog("GlobalState BEFORE reset: [3]=" + marshal.GlobalState[3] +
                    " [9]=" + marshal.GlobalState[9] +
                    " [10]=" + marshal.GlobalState[10] +
                    " [17]=" + marshal.GlobalState[17] +
                    " [20]=" + marshal.GlobalState[20] +
                    " [25]=" + marshal.GlobalState[25] +
                    " [32]=" + marshal.GlobalState[32] +
                    " GS_length=" + marshal.GlobalState.Length);

                // Reset critical GlobalState values that LoadFromIff always sets.
                // Without these the game behaves as if no expansion packs are installed,
                // which breaks controller behaviour (magic man, new neighbors, etc.).
                marshal.GlobalState[20] = 255; // Game Edition: all expansion packs enabled
                marshal.GlobalState[25] = 4;   // Needed for idle interactions (from EA-Land Edith)
                marshal.GlobalState[17] = 4;   // Runtime Code Version (checked by controller trees)
                if (marshal.GlobalState.Length > 3) marshal.GlobalState[3] = 0; // Clear selected person
                DbgLog("GlobalState AFTER reset:  [3]=" + marshal.GlobalState[3] +
                    " [9]=" + marshal.GlobalState[9] +
                    " [17]=" + marshal.GlobalState[17] +
                    " [20]=" + marshal.GlobalState[20] +
                    " [25]=" + marshal.GlobalState[25]);

                // Controllers are kept with their original thread state. A blank thread
                // means no active loop, so the magic-man / new-neighbors scripts never run.
                // The original running thread is safe across families because the checks
                // are based on neighbour data (who has visited, who got the gift), not on
                // lot-level flags.
                var controllerGuids = new HashSet<uint>(Content.Get().WorldObjects.ControllerObjects.Select(c => (uint)c.ID));
                DbgLog("ControllerGuids count: " + controllerGuids.Count + " -> " +
                    string.Join(", ", controllerGuids.Select(g => "0x" + g.ToString("X8"))));
                var keptIds = new HashSet<short>();
                var keptEntities = new List<VMEntityMarshal>();
                var keptThreads = new List<VMThreadMarshal>();
                for (int i = 0; i < marshal.Entities.Length; i++)
                {
                    var entity = marshal.Entities[i];
                    if (entity is VMAvatarMarshal)
                    {
                        DbgLog("  REMOVE avatar GUID=0x" + entity.GUID.ToString("X8") + " ID=" + entity.ObjectID);
                        continue;
                    }

                    // Always exclude controllers: VMBlueprintRestoreCmd's spawn loop
                    // re-creates them fresh (clean ObjectData + EP1 on thread), exactly
                    // matching the LoadFromIff path for vanilla lots.  Keeping a stale
                    // controller from the evicted session risks its ObjectData containing
                    // "already visited" state that prevents magic-man / new-neighbor visits.
                    var isController = controllerGuids.Contains(entity.GUID);
                    if (isController)
                    {
                        DbgLog("  REMOVE controller GUID=0x" + entity.GUID.ToString("X8") + " ID=" + entity.ObjectID +
                            " pos=(" + entity.Position.x + "," + entity.Position.y + ")");
                        continue;
                    }

                    // Keep build-mode, essential, and OUT_OF_WORLD non-avatar/non-controller objects.
                    // Buy-mode furniture at real lot positions is excluded.
                    var isOutOfWorld = entity.Position.x == short.MinValue;
                    var objd = Content.Get().WorldObjects.Get(entity.GUID)?.OBJ;
                    var buildMode = objd?.BuildModeType ?? -1;
                    var isEssential = EssentialLotObjects.Contains(entity.GUID);

                    if (isOutOfWorld || (objd != null && (buildMode > 0 || isEssential)))
                    {
                        keptIds.Add(entity.ObjectID);
                        keptEntities.Add(entity);
                        keptThreads.Add(new VMThreadMarshal
                        {
                            Stack = new VMStackFrameMarshal[0],
                            Queue = new VMQueuedActionMarshal[0],
                            ActiveQueueBlock = -1,
                            TempRegisters = new short[20],
                            TempXL = new int[2],
                        });
                        DbgLog("  KEEP GUID=0x" + entity.GUID.ToString("X8") + " ID=" + entity.ObjectID +
                            " oow=" + isOutOfWorld + " bmt=" + buildMode + " ess=" + isEssential +
                            " pos=(" + entity.Position.x + "," + entity.Position.y + ")");
                    }
                    else
                    {
                        DbgLog("  REMOVE GUID=0x" + entity.GUID.ToString("X8") + " ID=" + entity.ObjectID +
                            " oow=" + isOutOfWorld + " bmt=" + buildMode +
                            " pos=(" + entity.Position.x + "," + entity.Position.y + ")");
                    }
                }

                DbgLog("Kept " + keptEntities.Count + " non-controller entities (controllers stripped for fresh spawn)");
                marshal.Entities = keptEntities.ToArray();
                marshal.Threads = keptThreads.ToArray();

                // Filter multitile groups: keep only groups where ALL objects are in the kept set
                var keptGroups = new List<VMMultitileGroupMarshal>();
                foreach (var group in marshal.MultitileGroups)
                {
                    bool allKept = true;
                    foreach (var objId in group.Objects)
                    {
                        if (!keptIds.Contains(objId))
                        {
                            allKept = false;
                            break;
                        }
                    }
                    if (allKept) keptGroups.Add(group);
                }
                marshal.MultitileGroups = keptGroups.ToArray();

                // Update ObjectId counter
                short maxId = 0;
                foreach (var id in keptIds)
                {
                    if (id > maxId) maxId = id;
                }
                marshal.ObjectId = (short)(maxId + 1);
                if (marshal.ObjectId < 1) marshal.ObjectId = 1;

                // Keep walls/floors intact (architecture stays)
                // Clear platform state
                var ts1State = (VMTS1LotState)marshal.PlatformState;
                if (ts1State.SimulationInfo != null)
                {
                    ts1State.SimulationInfo.ObjectsValue = 0; // Furniture removed
                    ts1State.SimulationInfo.Version = 0x3E; // Normalize: SIMI.Write hardcodes 0x3E but uses instance Version for item count; mismatch corrupts the layout for TS1 originals
                    // Recompute ArchitectureValue from actual walls/floors + kept build-mode objects,
                    // matching the same formula VMTS1LotState.UpdatePersistState uses on a live VM.
                    ts1State.SimulationInfo.ArchitectureValue =
                        VMArchitectureStats.GetArchValue(marshal.Context.Architecture) + keptGroups.Sum(g => g.Price);
                    for (int i = 0; i < ts1State.SimulationInfo.BudgetDays.Length; i++)
                    {
                        ts1State.SimulationInfo.BudgetDays[i].Valid = 0;
                    }
                }
                ts1State.CurrentFamily = null;
            }
            else
            {
                DbgLog("--- Case B (ConvertToFilteredMarshal) ---");
                // Case B: No FSOV (lot never played in Simitone) - convert IFF with filtering
                marshal = VMTS1ActivatorNew.ConvertToFilteredMarshal(
                    houseIff,
                    (short)houseID,
                    (guid) =>
                    {
                        if (EssentialLotObjects.Contains(guid)) return true;
                        var objd = Content.Get().WorldObjects.Get(guid)?.OBJ;
                        return objd != null && objd.BuildModeType > 0;
                    });
                DbgLog("Case B result: " + marshal.Entities.Length + " entities, GS[17]=" +
                    marshal.GlobalState[17] + " GS[20]=" + marshal.GlobalState[20] + " GS[25]=" + marshal.GlobalState[25]);
            }

            DbgLog("Saving FSOV to IFF, entities=" + marshal.Entities.Length +
                " threads=" + marshal.Threads.Length + " groups=" + marshal.MultitileGroups.Length);
            // Serialize filtered marshal into new FSOV chunk
            var newFsov = new FSOV();
            newFsov.ChunkLabel = "Simitone Lot Data";
            newFsov.ChunkID = 1;
            newFsov.ChunkProcessed = true;
            newFsov.ChunkType = "FSOV";
            newFsov.AddedByPatch = true;

            using (var stream = new MemoryStream())
            {
                marshal.SerializeInto(new BinaryWriter(stream));
                newFsov.Data = stream.ToArray();
            }

            // Build new IFF with filtered FSOV + SIMI + thumbnails from original
            var newIff = new IffFile();
            newIff.AddChunk(newFsov);

            var platState = (VMTS1LotState)marshal.PlatformState;
            if (platState.SimulationInfo != null)
            {
                platState.SimulationInfo.ChunkProcessed = true;
                platState.SimulationInfo.AddedByPatch = true;
                newIff.AddChunk(platState.SimulationInfo);
            }

            // Copy thumbnail chunks from the original house IFF.
            // Chunks are lazily loaded (ChunkData has raw bytes but OriginalData is null),
            // so we must set OriginalData for WriteChunk to use as fallback.
            CopyChunksRaw<BMP>(houseIff, newIff);
            CopyChunksRaw<PNG>(houseIff, newIff);
            CopyChunksRaw<THMB>(houseIff, newIff);

            neigh.ResetHouse(houseID, newIff);
        }

        /// <summary>
        /// Copies chunks from one IFF to another, preserving raw byte data
        /// so that lazily-loaded chunks can be written without processing.
        /// </summary>
        private static void CopyChunksRaw<T>(IffFile source, IffFile dest) where T : IffChunk
        {
            var chunks = source.List<T>();
            if (chunks == null) return;
            foreach (var chunk in chunks)
                dest.AddChunk(chunk);
        }

        public override void GameResized()
        {
            base.GameResized();
            Bg.Position = (new Vector2(ScreenWidth, ScreenHeight)) / 2;
            World?.GameResized();
        }

        public void Initialize(string propertyName, bool external)
        {
            GameFacade.CurrentCityName = propertyName;
            ZoomLevel = 1; //screen always starts at near zoom
            InitializeLot(propertyName, external);
        }

        private int SwitchLot = -1;

        public void ChangeSpeedTo(int speed)
        {
            //0 speed is 0x
            //1 speed is 1x
            //2 speed is 3x
            //3 speed is 10x

            if (vm == null) return;
            if (vm.SpeedMultiplier == -1) return;

            switch (vm.SpeedMultiplier)
            {
                case 0:
                    switch (speed)
                    {
                        case 1:
                            HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo1); break;
                        case 2:
                            HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo2); break;
                        case 3:
                            HITVM.Get().PlaySoundEvent(UISounds.SpeedPTo3); break;
                    }
                    break;
                case 1:
                    switch (speed)
                    {
                        case 0:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed1ToP); break;
                        case 2:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed1To2); break;
                        case 3:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed1To3); break;
                    }
                    break;
                case 3:
                    switch (speed)
                    {
                        case 0:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed2ToP); break;
                        case 1:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed2To1); break;
                        case 3:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed2To3); break;
                    }
                    break;
                case 10:
                    switch (speed)
                    {
                        case 0:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed3ToP); break;
                        case 1:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed3To1); break;
                        case 2:
                            HITVM.Get().PlaySoundEvent(UISounds.Speed3To2); break;
                    }
                    break;
            }

            switch (speed)
            {
                case 0: vm.SpeedMultiplier = 0; break;
                case 1: vm.SpeedMultiplier = 1; break;
                case 2: vm.SpeedMultiplier = 3; break;
                case 3: vm.SpeedMultiplier = 10; break;
            }
            vm.ResetTickAlign();
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            GameFacade.Game.IsFixedTimeStep = (vm == null || vm.Ready);
            
            Visible = World?.Visible != false && World?.State.Cameras.HideUI != true;
            GameFacade.Game.IsMouseVisible = Visible;

            if (state.NewKeys.Contains(Keys.D1)) ChangeSpeedTo(1);
            if (state.NewKeys.Contains(Keys.D2)) ChangeSpeedTo(2);
            if (state.NewKeys.Contains(Keys.D3)) ChangeSpeedTo(3);
            if (state.NewKeys.Contains(Keys.P)) ChangeSpeedTo(0);
            if (state.NewKeys.Contains(Keys.D0))
            {
                //frame advance
                ChangeSpeedTo(1);
                GameThread.NextUpdate((FSO.Common.Rendering.Framework.Model.UpdateState ustate) => ChangeSpeedTo(0));
            }
            base.Update(state);

            if (state.NewKeys.Contains(Microsoft.Xna.Framework.Input.Keys.F12) && GraphicsModeControl.Mode != GlobalGraphicsMode.Full2D)
            {
                GraphicsModeControl.ChangeMode((GraphicsModeControl.Mode == GlobalGraphicsMode.Full3D) ? GlobalGraphicsMode.Hybrid2D : GlobalGraphicsMode.Full3D);
            }

            /*
            if (state.NewKeys.Contains(Keys.F12))
            {
                ChangeSpeedTo(1);
                //running 10000 ticks
                var timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                for (int i=0; i<10000; i++)
                {
                    vm.Tick();
                }

                timer.Stop();
                UIScreen.GlobalShowDialog(new UIMobileAlert(new UIAlertOptions() {
                    Title = "Benchmark",
                    Message = "10000 ticks took " + timer.ElapsedMilliseconds + "ms."
                }), true);
            }
            */

            if (World != null)
            {
                //stub smooth zoom?
            }

            if (SwitchLot > 0)
            {
                if (!Downtown) SavedLot = vm.Save();
                if (SwitchLot == ActiveFamily.HouseNumber && SavedLot != null)
                {
                    Downtown = false;
                    InitializeLot(SavedLot);
                    SavedLot = null;
                }
                else
                {
                    Downtown = true;
                    InitializeLot(Content.Get().Neighborhood.GetHousePath(SwitchLot), false);
                }
                SwitchLot = -1;
            }
            //vm.Context.Clock.Hours = 12;
            if (vm != null) vm.Update();

            //SaveHouseButton_OnButtonClick(null);
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            base.PreDraw(batch);
            vm?.PreDraw();
        }

        public void CleanupLastWorld()
        {
            if (vm == null) return;

            //clear our cache too, if the setting lets us do that
            TimedReferenceController.Clear();
            TimedReferenceController.Clear();

            if (ZoomLevel < 4) ZoomLevel = 5;
            vm.Context.Ambience.Kill();
            foreach (var ent in vm.Entities)
            { //stop object sounds
                var threads = ent.SoundThreads;
                for (int i = 0; i < threads.Count; i++)
                {
                    threads[i].Sound.RemoveOwner(ent.ObjectID);
                }
                threads.Clear();
            }
            vm.CloseNet(VMCloseNetReason.LeaveLot);
            GameFacade.Scenes.Remove(World);
            World.Dispose();
            //LotControl.Dispose();
            this.Remove(LotControl);
            this.Remove(Frontend);
            vm.SuppressBHAVChanges();
            vm = null;
            World = null;
            Driver = null;
            LotControl = null;
        }

        private VMMarshal SavedLot;

        public void InitializeLot()
        {
            CleanupLastWorld();
            World = new FSO.LotView.World(GameFacade.GraphicsDevice);

            World.Opacity = 1;
            GameFacade.Scenes.Add(World);

            var globalLink = new VMTS1GlobalLinkStub();
            Driver = new VMServerDriver(globalLink);

            vm = new VM(new VMContext(World), Driver, new UIHeadlineRendererProvider());
            vm.ListenBHAVChanges();
            vm.Init();

            LotControl = new UILotControl(vm, World);
            this.AddAt(0, LotControl);

            if (m_ZoomLevel > 3)
            {
                World.Visible = false;
                LotControl.Visible = false;
            }

            ZoomLevel = Math.Max(ZoomLevel, 4);

            if (IDEHook.IDE != null) IDEHook.IDE.StartIDE(vm);

            vm.OnFullRefresh += VMRefreshed;
            //vm.OnEODMessage += LotControl.EODs.OnEODMessage;
            vm.OnRequestLotSwitch += VMLotSwitch;
            vm.OnGenericVMEvent += Vm_OnGenericVMEvent;
        }

        public void InitializeLot(VMMarshal marshal)
        {
            InitializeLot();
            vm.MyUID = 65537;
            vm.Load(marshal);

            vm.TS1State.ActivateFamily(vm, ActiveFamily);

            var settings = GlobalSettings.Default;
            var myClient = new VMNetClient
            {
                PersistID = 1,
                RemoteIP = "local",
                AvatarState = new VMNetAvatarPersistState()
                {
                    Name = settings.LastUser ?? "",
                    DefaultSuits = new VMAvatarDefaultSuits(settings.DebugGender),
                    BodyOutfit = settings.DebugBody,
                    HeadOutfit = settings.DebugHead,
                    PersistID = 1,
                    SkinTone = (byte)settings.DebugSkin,
                    Gender = (short)(settings.DebugGender ? 1 : 0),
                    Permissions = FSO.SimAntics.Model.TSOPlatform.VMTSOAvatarPermissions.Admin,
                    Budget = 1000000
                }

            };

            var server = (VMServerDriver)Driver;
            server.ConnectClient(myClient);

            GameFacade.Cursor.SetCursor(CursorType.Normal);
            ZoomLevel = 1;

            Frontend = new UISimitoneFrontend(this);
            this.Add(Frontend);
        }

        public void ShowLoadErrors(List<VMLoadError> errors, bool verbose)
        {
            var errorMsg = GameFacade.Strings.GetString("153", "16");

            if (verbose)
            {
                errorMsg += "\n";
                foreach (var error in errors)
                {
                    errorMsg += "\n" + error.ToString();
                }
            }

            //signal thru the VM so we can stop time appropriately
            vm.LastSpeedMultiplier = vm.SpeedMultiplier;
            vm.SpeedMultiplier = 0;
            vm.SignalDialog(new VMDialogInfo
            {
                Block = true,
                Caller = null,
                Yes = "OK",
                DialogID = 0,
                Title = GameFacade.Strings.GetString("153", "17"),
                Message = errorMsg,
            });

            /*
            CloseAlert = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
            {
                Title = GameFacade.Strings.GetString("153", "17"), //missing objects!
                Message = errorMsg,
                Buttons = UIAlertButton.Ok(
                        (b) => { CloseAlert.Close(); CloseAlert = null; }
                        )
            });
            */
        }

        public void InitializeLot(string lotName, bool external)
        {
            if (lotName == "" || lotName[0] == '!') return;
            InitializeLot();
            
            if (!external)
            {
                if (!Downtown && ActiveFamily != null)
                {
                    ActiveFamily.SelectWholeFamily();
                    vm.TS1State.ActivateFamily(vm, ActiveFamily);
                }
                BlueprintReset(lotName, null);
                
                if (vm.LoadErrors.Count > 0) GameThread.NextUpdate((state) => ShowLoadErrors(vm.LoadErrors, true));

                vm.MyUID = 65537;
                var settings = GlobalSettings.Default;
                var myClient = new VMNetClient
                {
                    PersistID = 1,
                    RemoteIP = "local",
                    AvatarState = new VMNetAvatarPersistState()
                    {
                        Name = settings.LastUser ?? "",
                        DefaultSuits = new VMAvatarDefaultSuits(settings.DebugGender),
                        BodyOutfit = settings.DebugBody,
                        HeadOutfit = settings.DebugHead,
                        PersistID = 1,
                        SkinTone = (byte)settings.DebugSkin,
                        Gender = (short)(settings.DebugGender ? 1 : 0),
                        Permissions = FSO.SimAntics.Model.TSOPlatform.VMTSOAvatarPermissions.Admin,
                        Budget = 1000000
                    }
                };

                if (Downtown)
                {
                    var ngbh = Content.Get().Neighborhood;
                    var crossData = ngbh.GameState;
                    var neigh = ngbh.GetNeighborIDForGUID(crossData.DowntownSimGUID);
                    if (neigh != null) {
                        var inv = ngbh.GetInventoryByNID(neigh.Value);
                        if (inv != null) {
                            var hr = inv.FirstOrDefault(x => x.Type == 2 && x.GUID == 7)?.Count ?? 0;
                            var min = inv.FirstOrDefault(x => x.Type == 2 && x.GUID == 8)?.Count ?? 0;
                            Driver.SendCommand(new VMNetSetTimeCmd()
                            {
                                Hours = hr,
                                Minutes = min,
                            });
                        }
                    }
                }

                var server = (VMServerDriver)Driver;
                server.ConnectClient(myClient);
                LoadSurrounding(short.Parse(lotName.Substring(lotName.Length - 6, 2)));

                GameFacade.Cursor.SetCursor(CursorType.Normal);
                ZoomLevel = 1;
            }

            Frontend = new UISimitoneFrontend(this);
            this.Add(Frontend);
        }

        public void LoadSurrounding(short houseID)
        {
            return;
            var surrounding = new NBHm(new OBJ(File.OpenRead(@"C:\Users\Rhys\Desktop\fso 2018\nb4.obj")));
            NBHmHouse myH = null;
            var myHeight = vm.Context.Blueprint.InterpAltitude(new Vector3(0, 0, 0));
            if (!surrounding.Houses.TryGetValue(houseID, out myH)) return;
            foreach (var house in surrounding.Houses)
            {
                if (house.Key == houseID) continue;
                var h = house.Value;
                //let's make their lot as a surrounding lot
                var gd = World.State.Device;
                var subworld = World.MakeSubWorld(gd);
                subworld.Initialize(gd);
                var tempVM = new VM(new VMContext(subworld), new VMServerDriver(new VMTSOGlobalLinkStub()), new VMNullHeadlineProvider());
                tempVM.Init();
                BlueprintReset(Content.Get().Neighborhood.GetHousePath(house.Key), tempVM);
                subworld.State.Level = 5;
                var subHeight = tempVM.Context.Blueprint.InterpAltitude(new Vector3(0, 0, 0));
                tempVM.Context.Blueprint.BaseAlt = (int)Math.Round(((subHeight - myHeight) + myH.Position.Y - h.Position.Y) / tempVM.Context.Blueprint.TerrainFactor);
                subworld.UseFade = false;
                subworld.GlobalPosition = new Vector2((myH.Position.X - h.Position.X), (myH.Position.Z - h.Position.Z));

                foreach (var obj in tempVM.Entities)
                {
                    obj.Position = obj.Position;
                }

                vm.Context.Blueprint.SubWorlds.Add(subworld);
            }
            vm.Context.World.InitSubWorlds();
        }

        public void BlueprintReset(string path, VM vm)
        {
            string filename = Path.GetFileName(path);
            bool isSurrounding = true;
            if (vm == null)
            {
                isSurrounding = false;
                vm = this.vm;
            }
            DbgLog("=== BlueprintReset path=" + path + " isSurrounding=" + isSurrounding + " ===");
            try
            {
                var cacheFile = Path.Combine(FSOEnvironment.UserDir, "LocalHouse/") + filename.Substring(0, filename.Length - 4) + ".fsov";
                DbgLog("Trying LocalHouse cache: " + cacheFile + " exists=" + File.Exists(cacheFile));
                using (var file = new BinaryReader(File.OpenRead(cacheFile)))
                {
                    var marshal = new FSO.SimAntics.Marshals.VMMarshal();
                    marshal.Deserialize(file);
                    //vm.SendCommand(new VMStateSyncCmd()
                    //{
                    //    State = marshal
                    //});

                    DbgLog("LocalHouse cache loaded: " + marshal.Entities.Length + " entities, GS[17]=" + (marshal.GlobalState?.Length > 17 ? marshal.GlobalState[17].ToString() : "?") + " GS[20]=" + (marshal.GlobalState?.Length > 20 ? marshal.GlobalState[20].ToString() : "?") + " GS[25]=" + (marshal.GlobalState?.Length > 25 ? marshal.GlobalState[25].ToString() : "?"));
                    vm.Load(marshal);
                    DbgLog("vm.Load done (LocalHouse path)");
                    vm.Reset();
                }
            }
            catch (Exception ex)
            {
                DbgLog("LocalHouse cache failed (" + ex.GetType().Name + ": " + ex.Message + "), falling back to VMBlueprintRestoreCmd");
                var floorClip = Rectangle.Empty;
                var offset = new Point();
                var targetSize = 0;

                var isIff = path.EndsWith(".iff");
                short jobLevel = -1;
                if (isIff) jobLevel = short.Parse(path.Substring(path.Length - 6, 2));
                DbgLog("Sending VMBlueprintRestoreCmd: isIff=" + isIff + " jobLevel=" + jobLevel + " dataLen=" + (File.Exists(path) ? new FileInfo(path).Length.ToString() : "FILE_MISSING"));
                vm.SendCommand(new VMBlueprintRestoreCmd
                {
                    JobLevel = jobLevel,
                    XMLData = File.ReadAllBytes(path),
                    IffData = isIff,

                    FloorClipX = floorClip.X,
                    FloorClipY = floorClip.Y,
                    FloorClipWidth = floorClip.Width,
                    FloorClipHeight = floorClip.Height,
                    OffsetX = offset.X,
                    OffsetY = offset.Y,
                    TargetSize = targetSize
                });
            }

            var isSimless = (ActiveFamily == null && !isSurrounding);
            vm.SpeedMultiplier = -1;
            vm.Tick();
            vm.SpeedMultiplier = 1;

            if (isSimless)
            {
                vm.SpeedMultiplier = -1;
            }
            vm.SetGlobalValue(32, (short)(isSimless ? 1 : 0));
        }


        private void Vm_OnGenericVMEvent(VMEventType type, object data)
        {
            switch (type)
            {
                case VMEventType.TS1BuildBuyChange:
                    Frontend?.ModeSwitcher?.UpdateBuildBuy();
                    Frontend?.DesktopUCP?.UpdateBuildBuy();
                    break;
            }
        }

        private void VMLotSwitch(uint lotId)
        {
            vm.SpeedMultiplier = 0;
            if ((short)lotId == -1)
            {
                lotId = (uint)ActiveFamily.HouseNumber;
            }
            SwitchLot = (int)lotId;
        }

        private void VMRefreshed()
        {
            if (vm == null) return;
            LotControl.ActiveEntity = null;
            LotControl.RefreshCut();
        }

        private void SaveHouseButton_OnButtonClick(UIElement button)
        {
            if (vm == null) return;

            var exporter = new VMWorldExporter();
            Directory.CreateDirectory(Path.Combine(FSOEnvironment.UserDir, "Blueprints/cas.xml"));
            exporter.SaveHouse(vm, Path.Combine(FSOEnvironment.UserDir, "Blueprints/cas.xml"));
            var marshal = vm.Save();
            Directory.CreateDirectory(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/"));
            using (var output = new FileStream(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/cas.fsov"), FileMode.Create))
            {
                marshal.SerializeInto(new BinaryWriter(output));
            }
        }

        private UIMobileAlert CloseAlert;
        public override bool CloseAttempt()
        {
            if (CloseAlert != null) return true;

            GameThread.NextUpdate(x =>
            {
                if (CloseAlert == null)
                {
                    var canSave = vm != null;
                    CloseAlert = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
                    {
                        Title = GameFacade.Strings.GetString("153", "1"), //quit?
                        Message = GameFacade.Strings.GetString("153", canSave?"6":"2"), //are you sure (2), save before quitting (3)
                        Buttons = 
                        canSave?
                        UIAlertButton.YesNoCancel(
                            (b) => { Save(); GameFacade.Game.Exit(); },
                            (b) => { GameFacade.Game.Exit(); },
                            (b) => { CloseAlert.Close(); CloseAlert = null; }
                            )
                        :
                        UIAlertButton.YesNo(
                            (b) => { GameFacade.Game.Exit(); },
                            (b) => { CloseAlert.Close(); CloseAlert = null; }
                            )
                    });
                    GlobalShowDialog(CloseAlert, true);
                }
            });
            return false;
        }

        public void ReturnToNeighbourhood()
        {
            if (CloseAlert == null)
            {
                CloseAlert = new UIMobileAlert(new FSO.Client.UI.Controls.UIAlertOptions
                {
                    Title = GameFacade.Strings.GetString("153", "3"), //save
                    Message = GameFacade.Strings.GetString("153", "4"), //Do you want to save the game?
                    Buttons =
                    UIAlertButton.YesNoCancel(
                        (b) => { Save(); ExitLot(); CloseAlert.Close(); CloseAlert = null; },
                        (b) => { ExitLot(); CloseAlert.Close(); CloseAlert = null; },
                        (b) => { CloseAlert.Close(); CloseAlert = null; }
                        )
                });
                GlobalShowDialog(CloseAlert, true);
            }
        }

        public void Save()
        {
            //save the house first
            var iff = new IffFile();
            vm.TS1State.UpdateSIMI(vm);
            var marshal = vm.Save();
            var fsov = new FSOV();
            fsov.ChunkLabel = "Simitone Lot Data";
            fsov.ChunkID = 1;
            fsov.ChunkProcessed = true;
            fsov.ChunkType = "FSOV";
            fsov.AddedByPatch = true;

            using (var stream = new MemoryStream())
            {
                marshal.SerializeInto(new BinaryWriter(stream));
                fsov.Data = stream.ToArray();
            }

            iff.AddChunk(fsov);

            var simi = vm.TS1State.SimulationInfo;
            simi.ChunkProcessed = true;
            simi.AddedByPatch = true;
            iff.AddChunk(simi);

            Texture2D roofless = null;
            var thumb = World.GetLotThumb(GameFacade.GraphicsDevice, (tex) => roofless = FSO.Common.Utils.TextureUtils.Decimate(tex, GameFacade.GraphicsDevice, 2, false));
            thumb = FSO.Common.Utils.TextureUtils.Decimate(thumb, GameFacade.GraphicsDevice, 2, false);

            var tPNG = GeneratePNG(thumb);
            tPNG.ChunkID = 513;
            iff.AddChunk(tPNG);

            var rPNG = GeneratePNG(roofless);
            rPNG.ChunkID = 512;
            iff.AddChunk(rPNG);

            Content.Get().Neighborhood.SaveHouse(vm.GetGlobalValue(10), iff);
            Content.Get().Neighborhood.SaveNeighbourhood(true);
        }

        public PNG GeneratePNG(Texture2D data)
        {
            var png = new PNG();
            using (var stream = new MemoryStream())
            {
                data.SaveAsPng(stream, data.Width, data.Height);
                png.data = stream.ToArray();
            }

            png.ChunkLabel = "Lot Thumbnail";
            png.ChunkProcessed = true;
            png.ChunkType = "PNG_";
            png.AddedByPatch = true;

            return png;
        }

        public void ExitLot()
        {
            CleanupLastWorld();
            NeighSelection(NeighSelectionMode.Normal);
            Downtown = false;
            SavedLot = null;
        }
    }

    public enum NeighSelectionMode
    {
        Normal,
        MoveIn,
        MoveInMagic
    }
}