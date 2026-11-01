using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.NetPlay.Model.Commands;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic Build mode panel: the twelve tools, undo and redo (UndoHistory), and the chosen tool's catalog. The roof tool also
    /// has the pitch buttons, placed as the game's roof string table (147) has them.
    /// </summary>
    public class UIClassicBuildPanel : UIClassicStrip
    {
        private const int ToolsX = 266; //the chosen tool's options and catalog start here, right of the divider
        private const int RoofCatalogX = ToolsX + 48;

        //the tool art, Simitone's catalog category for it (-1 for the hand), in the game's order (string table 139)
        private static readonly (string Name, sbyte Category)[] Tools =
        {
            ("Terrain", 18), ("Water", 14), ("Wall", 13), ("Wallpaper", 15), ("Stairs", 10), ("Fireplace", 12),
            ("Tree", 11), ("Floor", 16), ("Door", 8), ("Window", 9), ("Roof", 17), ("Hand", -1)
        };
        //roof pitch buttons: art, position and pitch, as the game stores the pitch in its lots (HOUS)
        private static readonly (string Name, int Y, int String, float Pitch)[] Pitches =
        {
            ("roofsteep.bmp", 1, 5, 1.0f), ("roofmedium.bmp", 30, 3, 0.6f), ("roofshallow.bmp", 50, 1, 0.4f), ("roofflat.bmp", 71, 12, 0f)
        };

        private UIBuyBrowsePanel Engine;
        private UIClassicCatalog Catalog;
        private UIClassicInfoPopup Popup;
        private List<UIButton> ToolButtons = new List<UIButton>();
        private List<UIButton> PitchButtons = new List<UIButton>();
        private UIButton UndoButton;
        private UIButton RedoButton;
        public UIClassicBuildPanel(TS1GameScreen game) : base(game)
        {
            Engine = new UIBuyBrowsePanel(game, 0, UICatalogMode.Build) { HandleEyedropper = false };
            string str(int table, int i) => GameFacade.Strings.GetString(table.ToString(), i.ToString());

            for (int i = 0; i < Tools.Length; i++)
            {
                var tool = Tools[i];
                var b = new UIButton(Tex("Buttons/toolBtn" + tool.Name + ".bmp")) { Tooltip = str(139, i) };
                b.Position = new Vector2(10 + (i % 6) * 34, 10 + (i / 6) * 45);
                var index = i;
                b.OnButtonClick += (e) => SelectTool(index);
                ToolButtons.Add(b);
                Add(b);
            }
            Add(UndoButton = new UIButton(Tex("Buttons/Undo.BMP")) { Position = new Vector2(222, 20), Tooltip = str(139, 12), Disabled = true });
            Add(RedoButton = new UIButton(Tex("Buttons/Redo.bmp")) { Position = new Vector2(222, 54), Tooltip = str(139, 13), Disabled = true });
            UndoButton.OnButtonClick += (b) => UndoRedo(true);
            RedoButton.OnButtonClick += (b) => UndoRedo(false);
            Add(new UIImage(Tex("Backgrounds/BuildToothpkLeft.bmp")) { Position = new Vector2(258, 6) });

            for (int i = 0; i < Pitches.Length; i++)
            {
                var pitch = Pitches[i];
                var b = new UIButton(Tex("Build/" + pitch.Name)) { Position = new Vector2(ToolsX + 10, pitch.Y), Tooltip = str(147, pitch.String), Visible = false };
                b.OnButtonClick += (e) =>
                {
                    game.vm.SendCommand(new VMNetSetRoofCmd() { Pitch = pitch.Pitch, Style = game.vm.Context.Architecture.RoofStyle });
                };
                PitchButtons.Add(b);
                Add(b);
            }

            Popup = new UIClassicInfoPopup(-4) { X = 8 };
            Catalog = new UIClassicCatalog(Engine, Popup) { Position = new Vector2(ToolsX, 0) };
            Add(Catalog);
            Add(Popup);
            SelectTool(Tools.Length - 1);
            game.LotControl.ObjectHolder.OnEyedropperPick += OnEyedropperPick;
            game.LotControl.ObjectHolder.OnEyedropperArchitecturePick += OnEyedropperFloorPick;
            if (UIClassicCatalog.PendingGUID != null) OnEyedropperPick(UIClassicCatalog.PendingGUID.Value);
            if (UIClassicCatalog.PendingFloor != null) OnEyedropperFloorPick(UIClassicCatalog.PendingFloor.Value, ArchitectureType.Floor);
            UIClassicCatalog.PendingGUID = null;
            UIClassicCatalog.PendingFloor = null;
        }

        /// <summary>
        /// The eyedropper picked an object: choose its tool and pick it up, or hand it to buy mode.
        /// </summary>
        private void OnEyedropperPick(uint guid)
        {
            Game.LotControl.ObjectHolder.EyedropperMode = false;
            var item = Content.Get().WorldCatalog.GetItemByGUID(guid);
            if (item == null) return;
            var category = item.Value.Category;
            if (category <= 7)
            {
                UIClassicCatalog.PendingGUID = guid;
                Game.Frontend.SwitchMode(UIMainPanelMode.BUY);
                return;
            }
            var tool = Array.FindIndex(Tools, x => x.Category == category);
            if (tool == -1) return;
            SelectTool(tool);
            Catalog.Select(x => x.Item.GUID == guid);
        }

        private void OnEyedropperFloorPick(ushort pattern, ArchitectureType type)
        {
            Game.LotControl.ObjectHolder.EyedropperMode = false;
            SelectTool(Array.FindIndex(Tools, x => x.Name == "Floor"));
            Catalog.Select(x => x.Special?.ResID == pattern);
        }

        private void SelectTool(int index)
        {
            for (int i = 0; i < ToolButtons.Count; i++) ToolButtons[i].Selected = i == index;
            var tool = Tools[index];
            var roof = tool.Name == "Roof";
            foreach (var b in PitchButtons) b.Visible = roof;
            Catalog.X = roof ? RoofCatalogX : ToolsX;
            GameResized();
            if (tool.Category == -1)
            {
                //the hand: nothing to place, just pick up and move things. Its name and description are in Build.iff
                var strings = ClassicUIGraphics.GetGameData("Build.iff")?.Get<STR>(132);
                Catalog.SetTool(new UIClassicCatalogCell(Tex("Build/HandIcon.bmp"), Tex("Build/PopupHand.bmp"),
                    strings?.GetString(1) ?? GameFacade.Strings.GetString("139", "11"), strings?.GetString(2) ?? ""));
                return;
            }
            Engine.InitCategory(tool.Category, true);
            Catalog.SetItems(Engine.FullCategory);
        }

        private void UndoRedo(bool undo)
        {
            //drop whatever is being placed first: it belongs to the lot that's about to be replaced
            Engine.Deselect();
            Game.LotControl.ObjectHolder.ClearSelected();
            if (undo) Game.Undo?.Undo();
            else Game.Undo?.Redo();
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            UndoButton.Disabled = !(Game.Undo?.CanUndo ?? false);
            RedoButton.Disabled = !(Game.Undo?.CanRedo ?? false);
        }

        public override void GameResized()
        {
            base.GameResized();
            Catalog?.SetWidth(AvailableWidth - (int)Catalog.X);
        }

        public override void Kill()
        {
            //the engine was never added, so let go of its lot events directly (its Kill fades it out of a parent)
            Engine.Removed();
            Game.LotControl.ObjectHolder.OnEyedropperPick -= OnEyedropperPick;
            Game.LotControl.ObjectHolder.OnEyedropperArchitecturePick -= OnEyedropperFloorPick;
        }
    }
}
