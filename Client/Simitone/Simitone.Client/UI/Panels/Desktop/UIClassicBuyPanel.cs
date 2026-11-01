using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Content;
using FSO.Content.Interfaces;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic Buy mode panel. It starts with the catalog's two sorts: by function (top row) and by room (bottom).
    /// Picking one shows its subsorts with a button to go back, and the items beside them. Community lots sort by
    /// the lot's own areas instead of rooms, and only offer the objects made for that kind of lot.
    /// </summary>
    public class UIClassicBuyPanel : UIClassicStrip
    {
        private const int GridX = 340;

        //by function: the art's name and Simitone's catalog category, in the game's order (string table 150 from 8)
        private static readonly (string Name, sbyte Category)[] Functions =
        {
            ("Seating", 0), ("Surfaces", 1), ("Decorative", 5), ("Electronics", 3),
            ("Appliances", 2), ("Plumbing", 4), ("Lighting", 7), ("Misc", 6)
        };
        //the subsort art's folder for each catalog category
        private static readonly string[] SubsortFolders =
        {
            "Seating", "Surfaces", "Appliances", "Electronics", "Plumbing", "Decorative", "Miscellaneous", "Lighting"
        };
        //by room: the art's name and the object's room flag, in the game's order (string table 150 from 0)
        private static readonly (string Name, int Bit)[] Rooms =
        {
            ("Living", 3), ("Dining", 5), ("Bedroom", 1), ("Study", 7),
            ("Kitchen", 0), ("Bathroom", 2), ("Outside", 4), ("Misc", 6)
        };
        //by area, on community lots: the sort and back button art, the object's lot sort flag and the name (string
        //table 150), and the art for the area's subsorts. The game shares some art between lot types.
        private static readonly Dictionary<UICatalogMode, ((string Icon, string Back, int Bit, int String)[] Areas, string Subsorts)> LotAreas =
            new Dictionary<UICatalogMode, ((string, string, int, int)[], string)>()
        {
            { UICatalogMode.Downtown, (new[] { ("DDining", "DDining", 0, 16), ("DShops", "DShops", 1, 17), ("DOutdoor", "DOutdoor", 2, 18), ("DStreet", "DStreet", 3, 19), ("DMisc", "DMisc", 7, 23) }, "Downtown") },
            { UICatalogMode.Vacation, (new[] { ("VOne", "VOne", 0, 24), ("VTwo", "VTwo", 1, 25), ("VThree", "VThree", 2, 26), ("VFour", "VFour", 3, 27), ("VMisc", "VMisc", 7, 31) }, "Vacation") },
            { UICatalogMode.Community, (new[] { ("COne", "COne", 0, 32), ("CTwo", "CTwo", 1, 33), ("CThree", "CThree", 2, 34), ("CFour", "CFour", 3, 35), ("CMisc", "CMisc", 7, 39) }, "Community") },
            { UICatalogMode.Studiotown, (new[] { ("DDining", "DDining", 0, 40), ("DShops", "DShops", 1, 41), ("STStudio", "SStudio", 2, 42), ("STSpa", "SSpa", 3, 43), ("DMisc", "DMisc", 7, 47) }, "Downtown") },
            { UICatalogMode.Magictown, (new[] { ("DDining", "DDining", 0, 16), ("DShops", "DShops", 1, 17), ("MTmagic", "MMagic", 2, 44), ("MOutdoor", "DOutdoor", 3, 18), ("DMisc", "DMisc", 7, 23) }, "Downtown") },
        };

        private UIBuyBrowsePanel Engine;
        private UIClassicCatalog Catalog;
        private UIClassicInfoPopup Popup;
        private UIContainer Sorts;
        private List<UIButton> SubButtons = new List<UIButton>();

        public UIClassicBuyPanel(TS1GameScreen game, UICatalogMode mode) : base(game)
        {
            Engine = new UIBuyBrowsePanel(game, 0, mode) { HandleEyedropper = false };
            Popup = new UIClassicInfoPopup(-4) { X = 8 };
            Catalog = new UIClassicCatalog(Engine, Popup) { Position = new Vector2(GridX, 0) };
            Add(Catalog);
            Add(Popup);
            ShowSorts();
            game.LotControl.ObjectHolder.OnEyedropperPick += OnEyedropperPick;
            game.LotControl.ObjectHolder.OnEyedropperArchitecturePick += OnEyedropperFloorPick;
            if (UIClassicCatalog.PendingGUID != null) OnEyedropperPick(UIClassicCatalog.PendingGUID.Value);
            UIClassicCatalog.PendingGUID = null;
        }

        /// <summary>
        /// The eyedropper picked an object: show its function's items and pick it up, or hand it to build mode.
        /// </summary>
        private void OnEyedropperPick(uint guid)
        {
            Game.LotControl.ObjectHolder.EyedropperMode = false;
            var item = Content.Get().WorldCatalog.GetItemByGUID(guid);
            if (item == null) return;
            var category = item.Value.Category;
            if (category > 7)
            {
                UIClassicCatalog.PendingGUID = guid;
                Game.Frontend.SwitchMode(UIMainPanelMode.BUILD);
                return;
            }
            var f = Functions.FirstOrDefault(x => x.Category == category);
            ShowFunction(f.Name, f.Category);
            if (Catalog.Select(x => x.Item.GUID == guid)) return;
            //"all" leaves out items without a subsort; show it on its own then
            Catalog.SetItems(Elements(new[] { item.Value }));
            Catalog.Select(x => x.Item.GUID == guid);
        }

        private void OnEyedropperFloorPick(ushort pattern, ArchitectureType type)
        {
            Game.LotControl.ObjectHolder.EyedropperMode = false;
            UIClassicCatalog.PendingFloor = pattern;
            Game.Frontend.SwitchMode(UIMainPanelMode.BUILD);
        }

        public override void GameResized()
        {
            base.GameResized();
            Catalog?.SetWidth(AvailableWidth - GridX);
        }

        public override void Kill()
        {
            //the engine was never added, so let go of its lot events directly (its Kill fades it out of a parent)
            Engine.Removed();
            Game.LotControl.ObjectHolder.OnEyedropperPick -= OnEyedropperPick;
            Game.LotControl.ObjectHolder.OnEyedropperArchitecturePick -= OnEyedropperFloorPick;
        }

        private string Str(int table, int index) => GameFacade.Strings.GetString(table.ToString(), index.ToString());

        private void SetLeft(UIContainer left)
        {
            if (Sorts != null) Remove(Sorts);
            Sorts = left;
            AddBefore(Sorts, Popup);
        }

        /// <summary>
        /// The two rows of sorts.
        /// </summary>
        private void ShowSorts()
        {
            var left = new UIContainer();
            for (int i = 0; i < Functions.Length; i++)
            {
                var f = Functions[i];
                var b = new UIButton(Tex("Catalog/BuyF" + f.Name + ".bmp")) { Tooltip = Str(150, 8 + i) };
                b.Position = new Vector2(12 + i * 40 + (36 - b.Size.X) / 2, 6 + (42 - b.Size.Y) / 2);
                b.OnButtonClick += (e) => ShowFunction(f.Name, f.Category);
                left.Add(b);
            }
            if (LotAreas.TryGetValue(Engine.Mode, out var lot))
            {
                for (int i = 0; i < lot.Areas.Length; i++)
                {
                    var a = lot.Areas[i];
                    var b = new UIButton(Tex("Catalog/Buy" + a.Icon + ".bmp")) { Tooltip = Str(150, a.String) };
                    b.Position = new Vector2(12 + i * 40 + (36 - b.Size.X) / 2, 52 + (44 - b.Size.Y) / 2);
                    b.OnButtonClick += (e) => ShowArea(a.Back, x => (Engine.GetSubsort(x) & (1 << a.Bit)) != 0, lot.Subsorts);
                    left.Add(b);
                }
            }
            else
            {
                for (int i = 0; i < Rooms.Length; i++)
                {
                    var r = Rooms[i];
                    var b = new UIButton(Tex("Catalog/BuyR" + r.Name + ".bmp")) { Tooltip = Str(150, i) };
                    b.Position = new Vector2(12 + i * 40 + (36 - b.Size.X) / 2, 52 + (44 - b.Size.Y) / 2);
                    b.OnButtonClick += (e) => ShowArea("R" + r.Name, x => (x.RoomSort & (1 << r.Bit)) != 0, null);
                    left.Add(b);
                }
            }
            SetLeft(left);
            Catalog.SetItems(new List<UICatalogElement>());
        }

        /// <summary>
        /// A sort's subsorts: the back button, then up to four buttons a row. Picking one shows its items.
        /// </summary>
        private void ShowSubsorts(string back, List<(string Icon, string Tooltip, List<UICatalogElement> Items)> subs)
        {
            var left = new UIContainer();
            var backButton = new UIButton(Tex("Catalog/BackButtons/Back" + back + ".bmp")) { Position = new Vector2(12, 37), Tooltip = "Back" };
            backButton.OnButtonClick += (e) => ShowSorts();
            left.Add(backButton);
            SubButtons.Clear();
            var perRow = (subs.Count > 8) ? 5 : 4;
            for (int i = 0; i < subs.Count; i++)
            {
                var sub = subs[i];
                var b = new UIButton(Tex(sub.Icon)) { Tooltip = sub.Tooltip, Disabled = sub.Items.Count == 0 };
                b.Position = new Vector2(56 + (i % perRow) * 40 + (36 - b.Size.X) / 2, 8 + (i / perRow) * 44 + (40 - b.Size.Y) / 2);
                var index = i;
                b.OnButtonClick += (e) =>
                {
                    for (int j = 0; j < SubButtons.Count; j++) SubButtons[j].Selected = j == index;
                    Catalog.SetItems(sub.Items);
                };
                SubButtons.Add(b);
                left.Add(b);
            }
            SetLeft(left);
            //start on "all"
            var all = subs.Count - 1;
            SubButtons[all].Selected = true;
            Catalog.SetItems(subs[all].Items);
        }

        private void ShowFunction(string name, sbyte category)
        {
            var items = Elements(Content.Get().WorldCatalog.GetItemsByCategory(category).Where(ForSale));
            List<UICatalogElement> where(int bit)
            {
                //as Simitone's buy panel: "other" also takes subsort 16, and "all" anything with a subsort
                if (bit == 8) return items.Where(x => FunctionSort(x.Item) > 0).ToList();
                var mask = 1 << bit;
                if (bit == 7) mask |= 16;
                return items.Where(x => (FunctionSort(x.Item) & mask) > 0).ToList();
            }
            var folder = "Catalog/SubSortIcons/" + SubsortFolders[category] + "/BuySubSort";
            var table = 200 + UIBuyBrowsePanel.RemapString[category];
            var subs = new List<(string, string, List<UICatalogElement>)>();
            var names = new[] { "One", "Two", "Three", "Four" };
            for (int i = 0; i < 4; i++) subs.Add((folder + names[i] + ".bmp", Str(table, i), where(i)));
            if (category == 6)
            {
                subs.Add((folder + "Pets.bmp", Str(210, 3), where(5)));
                subs.Add((folder + "Magic.bmp", Str(210, 4), where(6)));
            }
            subs.Add(("Catalog/SubSortIcons/BuySubSortOther.bmp", Str(210, 1), where(7)));
            subs.Add(("Catalog/SubSortIcons/BuySubSortAll.bmp", Str(210, 2), where(8)));
            ShowSubsorts("F" + name, subs);
        }

        /// <summary>
        /// Everything for a room (or a community lot's area), then by function.
        /// </summary>
        /// <param name="back">The back button's art.</param>
        /// <param name="subsorts">Whose subsort art to use (null for the rooms').</param>
        private void ShowArea(string back, Func<ObjectCatalogItem, bool> inArea, string subsorts)
        {
            var items = Elements(Enumerable.Range(0, 8).SelectMany(c => Content.Get().WorldCatalog.GetItemsByCategory((sbyte)c))
                .Where(x => ForSale(x) && inArea(x)));
            var art = (subsorts == null) ? "RoomSubsort/BuySubSortRoom" : subsorts + "Subsort/BuySubSort" + subsorts;
            var subs = new List<(string, string, List<UICatalogElement>)>();
            for (int i = 0; i < Functions.Length; i++)
            {
                var f = Functions[i];
                subs.Add(("Catalog/SubSortIcons/" + art + f.Name + ".bmp", Str(150, 8 + i), items.Where(x => x.Item.Category == f.Category).ToList()));
            }
            subs.Add(("Catalog/SubSortIcons/BuySubSortAll.bmp", Str(210, 2), items));
            ShowSubsorts(back, subs);
        }

        /// <summary>
        /// Community lots only sell the objects made for that kind of lot (the ones it sorts into an area).
        /// </summary>
        private bool ForSale(ObjectCatalogItem item) => !LotAreas.ContainsKey(Engine.Mode) || Engine.GetSubsort(item) != 0;

        /// <summary>
        /// The object's subsort within its function. On community lots Simitone's catalog sorts by area instead, so
        /// this reads the function subsort itself.
        /// </summary>
        private byte FunctionSort(ObjectCatalogItem item) => LotAreas.ContainsKey(Engine.Mode) ? item.Subsort : Engine.GetSubsort(item);

        private static List<UICatalogElement> Elements(IEnumerable<ObjectCatalogItem> items)
        {
            return items.OrderBy(x => x.Price).Select(x => new UICatalogElement() { Item = x, CalcPrice = (int)x.Price }).ToList();
        }
    }
}
