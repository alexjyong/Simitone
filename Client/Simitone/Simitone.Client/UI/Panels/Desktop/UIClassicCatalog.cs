using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Controls.Catalog;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Panels.LiveSubpanels;
using Simitone.Client.UI.Panels.LiveSubpanels.Catalog;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic catalog: two rows of items in the original frames, with page arrows at both ends. Hovering an item
    /// shows its name, price and description above the strip; clicking it picks it up through Simitone's buy/build
    /// panel (Engine), which isn't shown but does the buying, placing and tool handling.
    /// </summary>
    public class UIClassicCatalog : UIContainer
    {
        public const int CellSize = 47;
        //an eyedropper pick for the other mode's panel, which opens after the mode switches (object GUID, or floor pattern)
        public static uint? PendingGUID;
        public static ushort? PendingFloor;
        private const int Rows = 2;
        private UIBuyBrowsePanel Engine;
        private List<UICatalogElement> Items = new List<UICatalogElement>();
        private UIButton PrevButton;
        private UIButton NextButton;
        private List<UIClassicCatalogCell> Cells = new List<UIClassicCatalogCell>();
        private UIClassicInfoPopup Popup;
        private int Columns;
        private int Page;
        private UIClassicCatalogCell Tool;

        public UIClassicCatalog(UIBuyBrowsePanel engine, UIClassicInfoPopup popup)
        {
            Engine = engine;
            Popup = popup;
            Add(PrevButton = new UIButton(ClassicUIGraphics.Get("cpanel/Buttons/ScrollLeftBuy.BMP")) { Position = new Vector2(0, 26), Tooltip = GameFacade.Strings.GetString("139", "14") });
            Add(NextButton = new UIButton(ClassicUIGraphics.Get("cpanel/Buttons/ScrollRight.BMP")) { Tooltip = GameFacade.Strings.GetString("139", "15") });
            PrevButton.OnButtonClick += (b) => { Page--; Refresh(); };
            NextButton.OnButtonClick += (b) => { Page++; Refresh(); };
            SetWidth(CellSize + 24);
        }

        /// <summary>
        /// Fits as many columns as there's room for (the strip is placed, and resized, after it's made).
        /// </summary>
        public void SetWidth(int width)
        {
            var columns = Math.Max(1, (width - 24) / CellSize);
            if (columns == Columns) return;
            Columns = columns;
            NextButton.Position = new Vector2(12 + Columns * CellSize, 26);
            Refresh();
        }

        public void SetItems(List<UICatalogElement> items)
        {
            Items = items;
            Tool = null;
            Page = 0;
            Engine.Deselect();
            Engine.FilterCategory = items;
            Refresh();
        }

        /// <summary>
        /// Shows a tool with nothing to buy (the hand) as a single cell, always chosen, in place of the items.
        /// </summary>
        public void SetTool(UIClassicCatalogCell tool)
        {
            SetItems(new List<UICatalogElement>());
            Tool = tool;
            Refresh();
        }

        /// <summary>
        /// Picks up the first item that matches, turning to its page. False if there's none.
        /// </summary>
        public bool Select(Func<UICatalogElement, bool> match)
        {
            var index = Items.FindIndex(x => match(x));
            if (index == -1) return false;
            Page = index / (Columns * Rows);
            Refresh();
            Engine.Selected(index);
            return true;
        }

        private void Refresh()
        {
            var perPage = Columns * Rows;
            var pages = Math.Max(1, (Items.Count + perPage - 1) / perPage);
            Page = Math.Max(0, Math.Min(pages - 1, Page));
            PrevButton.Disabled = Page == 0;
            NextButton.Disabled = Page >= pages - 1;
            foreach (var c in Cells) Remove(c);
            Cells.Clear();
            if (Tool != null)
            {
                Tool.Position = new Vector2(11, 2);
                Tool.Chosen = true;
                Tool.OnHover -= ShowToolPopup;
                Tool.OnHover += ShowToolPopup;
                Add(Tool);
                Cells.Add(Tool);
                return;
            }
            for (int i = Page * perPage; i < Math.Min(Items.Count, (Page + 1) * perPage); i++)
            {
                var n = i - Page * perPage;
                var index = i;
                var cell = new UIClassicCatalogCell(Items[i]) { Position = new Vector2(11 + (n / Rows) * CellSize, 2 + (n % Rows) * CellSize) };
                cell.OnClick += () =>
                {
                    FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
                    if (Engine.ItemID == index) Engine.Deselect();
                    else Engine.Selected(index);
                };
                cell.OnHover += (hover) =>
                {
                    if (hover)
                    {
                        if (cell.PopupPicture != null) Popup.Show(cell.PopupPicture, cell.Title, cell.Description);
                        else Popup.Show(cell.Icon, cell.Title, cell.Description, cell.IconSource);
                    }
                    else Popup.Hide();
                };
                Cells.Add(cell);
                Add(cell);
            }
        }

        private void ShowToolPopup(bool hover)
        {
            if (hover) Popup.Show(Tool.PopupPicture, Tool.Title, Tool.Description);
            else Popup.Hide();
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (Tool != null) return;
            //the item being placed stays lit
            for (int i = 0; i < Cells.Count; i++) Cells[i].Chosen = Engine.ItemID == Page * Columns * Rows + i;
        }
    }

    /// <summary>
    /// One item in the classic catalog: its picture in ThumbTemplate's frame (normal, chosen, hovered, chosen and hovered).
    /// </summary>
    public class UIClassicCatalogCell : UIElement
    {
        private Texture2D Frame;
        private bool Hover;
        public bool Chosen;
        public Texture2D Icon;
        public Rectangle IconSource;
        public Texture2D PopupPicture; //shown in the popup instead of the icon, where the game has one
        public string Title;
        public string Description;
        public event Action OnClick;
        public event Action<bool> OnHover;

        /// <summary>
        /// A tool's cell: its four-frame button art and the popup's picture and text.
        /// </summary>
        public UIClassicCatalogCell(Texture2D icon, Texture2D popupPicture, string title, string description)
        {
            Frame = ClassicUIGraphics.Get("cpanel/Catalog/ThumbTemplate.BMP");
            Icon = icon;
            if (icon != null) IconSource = new Rectangle(0, 0, icon.Width / 4, icon.Height);
            PopupPicture = popupPicture;
            Title = title;
            Description = description;
            ListenForHover();
        }

        public UIClassicCatalogCell(UICatalogElement elem)
        {
            Frame = ClassicUIGraphics.Get("cpanel/Catalog/ThumbTemplate.BMP");
            if (elem.Special?.Res != null)
            {
                var res = elem.Special.Res;
                Icon = res.GetIcon(elem.Special.ResID);
                Title = res.GetName(elem.Special.ResID);
                Description = res.GetDescription(elem.Special.ResID);
                var price = res.GetPrice(elem.Special.ResID);
                if (price > 0) Title += "   §" + price;
                if (res is UICatalogTerrainResProvider)
                {
                    //Simitone's terrain icons are from The Sims Online; the game has its own (four-frame buttons)
                    var art = new[] { ("Build/TerrainUpIcon.BMP", "Build/PopupTerrainUp.BMP"), ("Build/TerrainLevelIcon.BMP", "Build/PopupTerrainLevel.bmp"), ("HDBuild/TerrainGrassIcon.BMP", "HDBuild/popupGrass.bmp") };
                    var id = (int)elem.Special.ResID;
                    if (id < art.Length)
                    {
                        Icon = ClassicUIGraphics.Get("cpanel/" + art[id].Item1);
                        PopupPicture = ClassicUIGraphics.Get("cpanel/" + art[id].Item2);
                        if (Icon != null) IconSource = new Rectangle(0, 0, Icon.Width / 4, Icon.Height);
                    }
                }
            }
            else
            {
                var obj = Content.Get().WorldObjects.Get(elem.Item.GUID);
                var strings = obj?.Resource.Get<CTSS>(obj.OBJ.CatalogStringsID);
                Icon = new UICatalogItem(elem, null).Icon;
                Title = (strings?.GetString(0) ?? elem.ToString()) + "   §" + elem.Item.Price;
                Description = strings?.GetString(1) ?? "";
            }
            if (Icon != null && IconSource.Width == 0)
            {
                //catalog pictures are usually two frames side by side; the first is the normal one
                var twoFrames = !(Icon.Width / (float)Icon.Height < 1.1f || Icon.Width == 127 || Icon.Width == 128);
                IconSource = new Rectangle(0, 0, twoFrames ? Icon.Width / 2 : Icon.Width, Icon.Height);
            }
            ListenForHover();
        }

        private void ListenForHover()
        {
            Tooltip = null;
            ListenForMouse(new Rectangle(0, 0, 45, 45), (type, state) =>
            {
                switch (type)
                {
                    case UIMouseEventType.MouseOver: Hover = true; OnHover?.Invoke(true); break;
                    case UIMouseEventType.MouseOut: Hover = false; OnHover?.Invoke(false); break;
                    case UIMouseEventType.MouseUp: OnClick?.Invoke(); break;
                }
            });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var frame = (Chosen ? 1 : 0) + (Hover ? 2 : 0);
            DrawLocalTexture(batch, Frame, new Rectangle(frame * 45, 0, 45, 45), Vector2.Zero);
            if (Icon == null) return;
            var scale = 37f / Math.Max(IconSource.Width, IconSource.Height);
            var size = new Vector2(IconSource.Width, IconSource.Height) * scale;
            DrawLocalTexture(batch, Icon, IconSource, (new Vector2(45) - size) / 2, new Vector2(scale));
        }
    }
}
