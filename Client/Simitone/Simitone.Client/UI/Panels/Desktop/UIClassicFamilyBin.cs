using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The family bin ("Select a Family"): the families without a house, each with its faces, net worth and friends,
    /// on the game's own screen (Nbhd/PickBkg.bmp). Its buttons sit in the slots drawn for them: create a family (in
    /// Create-a-Family), delete one, or move one in, which hands the neighbourhood the moving van.
    /// </summary>
    public class UIClassicFamilyBin : UIContainer
    {
        public const int Rows = 6;
        private const int RowY = 75;
        private const int RowPitch = 72;

        public static FAMI Reselect; //a family just made in Create-a-Family, to select when the bin opens again

        private UIClassicNeighbourhood Neighbourhood;
        private List<FAMI> Families;
        private Dictionary<FAMI, List<(string Name, Texture2D Head)>> Heads;
        private List<UIClassicFamilyRow> RowElems = new List<UIClassicFamilyRow>();
        private UIButton DeleteButton;
        private UIButton MoveInButton;
        private UIClassicFrameButton UpButton;
        private UIClassicFrameButton DownButton;
        private UILabel EmptyLabel;
        private int Scroll;
        private int Selection = -1;
        private int LastWheel;

        private static string Str(int i) => GameFacade.Strings.GetString("128", i.ToString());

        public UIClassicFamilyBin(UIClassicNeighbourhood neighbourhood)
        {
            Neighbourhood = neighbourhood;
            Add(new UIImage(ClassicUIGraphics.Get("Nbhd/PickBkg.bmp")));

            var title = ClassicFont.Style(14);
            title.Color = Color.White;
            Add(new UILabel() { Caption = Str(8), CaptionStyle = title, Position = new Vector2(30, 34), Size = new Vector2(400, 24), Alignment = TextAlignment.Left | TextAlignment.Middle });
            //the money and smiley over the columns are part of the background; these explain them
            Add(TipArea(new Rectangle(586, 42, 24, 24), Str(10)));
            Add(TipArea(new Rectangle(663, 42, 24, 24), Str(11)));

            for (int i = 0; i < Rows; i++)
            {
                var index = i;
                var row = new UIClassicFamilyRow() { Position = new Vector2(18, RowY + i * RowPitch) };
                row.OnClick += () => Select(Scroll + index);
                row.OnDoubleClick += () => { Select(Scroll + index); MoveIn(); };
                RowElems.Add(row);
                Add(row);
            }
            var empty = ClassicFont.Style(12);
            empty.Color = new Color(200, 206, 214);
            Add(EmptyLabel = new UILabel()
            {
                Caption = Str(13) + "\r\n\r\n" + Str(14),
                CaptionStyle = empty,
                Wrapped = true,
                Position = new Vector2(120, 200),
                Size = new Vector2(520, 200),
                Alignment = TextAlignment.Center | TextAlignment.Top,
                Visible = false
            });

            var scroll = ClassicUIGraphics.Get("Nbhd/ScrollBar.bmp");
            Add(UpButton = new UIClassicFrameButton(scroll, 12, 0, 1) { Position = new Vector2(744, 78) });
            Add(DownButton = new UIClassicFrameButton(scroll, 12, 4, 5) { Position = new Vector2(744, 470) });
            UpButton.OnClick += () => SetScroll(Scroll - 1);
            DownButton.OnClick += () => SetScroll(Scroll + 1);

            UIButton btn(string name, int x, int y, string tooltip, Action click, int states = 4)
            {
                var b = new UIButton(ClassicUIGraphics.Get("Nbhd/" + name)) { Position = new Vector2(x, y), Tooltip = tooltip, ImageStates = states };
                b.OnButtonClick += (e) => click();
                Add(b);
                return b;
            }
            btn("PAFAddFamily.bmp", 322, 530, Str(2), CreateFamily);
            DeleteButton = btn("PAFDeleteFamily.bmp", 414, 530, Str(4), DeleteFamily);
            MoveInButton = btn("PAFMoveIn.bmp", 506, 530, Str(3), MoveIn);
            btn("PAFCancel.bmp", 40, 538, Str(5), Close, 6);

            Refresh();
            if (Reselect != null) Select(Families.IndexOf(Reselect));
            Reselect = null;
            GameResized();
        }

        private static Texture2D Clear;

        /// <summary>
        /// An invisible area with a tooltip.
        /// </summary>
        private static UIImage TipArea(Rectangle area, string tooltip)
        {
            if (Clear == null)
            {
                Clear = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
                Clear.SetData(new[] { Color.Transparent });
            }
            var image = new UIImage(Clear) { Position = new Vector2(area.X, area.Y), Tooltip = tooltip };
            image.SetSize(area.Width, area.Height);
            image.UseTooltip();
            return image;
        }

        /// <summary>
        /// The families that have no house: not townies (from 1000), nor the empty default family.
        /// </summary>
        public static List<FAMI> Homeless() => Content.Get().Neighborhood.MainResource.List<FAMI>()
            .Where(x => x.HouseNumber == 0 && x.ChunkID > 0 && x.ChunkID < 1000 && x.FamilyGUIDs.Length > 0).ToList();

        private void Refresh()
        {
            Families = Homeless();
            Heads = FamilyHeads.Of(Families);
            EmptyLabel.Visible = Families.Count == 0;
            SetScroll(Scroll);
            Select(Math.Min(Selection, Families.Count - 1));
        }

        private void SetScroll(int scroll)
        {
            Scroll = Math.Max(0, Math.Min(scroll, Families.Count - Rows));
            for (int i = 0; i < Rows; i++)
            {
                var index = Scroll + i;
                var family = (index < Families.Count) ? Families[index] : null;
                RowElems[i].Set(family, (family == null) ? null : Heads[family], index == Selection);
            }
            UpButton.Visible = DownButton.Visible = Families.Count > Rows;
            UpButton.Disabled = Scroll == 0;
            DownButton.Disabled = Scroll >= Families.Count - Rows;
        }

        private void Select(int index)
        {
            Selection = (index >= 0 && index < Families.Count) ? index : -1;
            if (Selection != -1 && (Selection < Scroll || Selection >= Scroll + Rows)) Scroll = Selection - Rows / 2;
            SetScroll(Scroll);
            DeleteButton.Disabled = MoveInButton.Disabled = Selection == -1;
        }

        private void CreateFamily()
        {
            Close();
            GameController.EnterCAS(true);
        }

        private void MoveIn()
        {
            if (Selection == -1) return;
            var family = Families[Selection];
            Close();
            Neighbourhood.StartMoveIn(family);
        }

        private void DeleteFamily()
        {
            if (Selection == -1) return;
            var family = Families[Selection];
            UIClassicAlert alert = null;
            alert = new UIClassicAlert(new UIAlertOptions
            {
                Title = Str(6),
                Message = GameFacade.Strings.GetString("128", "7", new string[] { HouseRollover.FamilyName(family) }),
                Buttons = UIAlertButton.YesNo((b) =>
                {
                    alert.Close();
                    var neigh = Content.Get().Neighborhood;
                    var names = neigh.MainResource.Get<FAMs>(family.ChunkID);
                    family.ChunkParent.FullRemoveChunk(family);
                    if (names != null) names.ChunkParent.FullRemoveChunk(names);
                    neigh.SaveNeighbourhood(true);
                    Refresh();
                }, (b) => alert.Close())
            }, null, false);
            UIScreen.GlobalShowDialog(alert, true);
        }

        private void Close() => UIScreen.RemoveDialog(this);

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var wheel = state.MouseState.ScrollWheelValue;
            if (wheel != LastWheel && Families.Count > Rows) SetScroll(Scroll + ((wheel < LastWheel) ? 1 : -1));
            LastWheel = wheel;
        }

        public override void GameResized()
        {
            base.GameResized();
            var screen = UIScreen.Current;
            Position = new Vector2((screen.ScreenWidth - 800) / 2, Math.Max(0, (screen.ScreenHeight - 600) / 2));
        }
    }

    /// <summary>
    /// A family in the bin: the game's row (FamilyPickItemBkg.bmp: normal, hovered and selected), the family's name and
    /// faces, and under the background's money and smiley, their net worth and friends.
    /// </summary>
    public class UIClassicFamilyRow : UIElement
    {
        private const int HeadSize = 36;
        private static Texture2D Back;
        private static Texture2D Frame;
        private FAMI Family;
        private List<(string Name, Texture2D Head)> Heads;
        private bool Selected;
        private bool Hovered;
        private int LastClick;
        private TextStyle NameStyle;
        private TextStyle ValueStyle;
        private UIMouseEventRef ClickHandler;
        public event Action OnClick;
        public event Action OnDoubleClick;

        public UIClassicFamilyRow()
        {
            if (Back == null) Back = ClassicUIGraphics.Get("Nbhd/FamilyPickItemBkg.bmp");
            if (Frame == null) Frame = ClassicUIGraphics.Get("Nbhd/PersonInfoBkg.bmp");
            NameStyle = ClassicFont.Style(13);
            NameStyle.Color = Color.White;
            ValueStyle = ClassicFont.Style(11);
            ValueStyle.Color = new Color(200, 206, 214);
            ClickHandler = ListenForMouse(new Rectangle(0, 0, 690, 68), (type, state) =>
            {
                if (Family == null) return;
                switch (type)
                {
                    case UIMouseEventType.MouseOver: Hovered = true; break;
                    case UIMouseEventType.MouseOut: Hovered = false; break;
                    case UIMouseEventType.MouseDown:
                        var now = Environment.TickCount;
                        if (now - LastClick < 400) OnDoubleClick?.Invoke();
                        else OnClick?.Invoke();
                        LastClick = now;
                        break;
                }
            });
        }

        public void Set(FAMI family, List<(string Name, Texture2D Head)> heads, bool selected)
        {
            Family = family;
            Heads = heads;
            Selected = selected;
            if (family == null) Hovered = false;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Family == null || Back == null) return;
            var frame = Selected ? 2 : Hovered ? 1 : 0;
            DrawLocalTexture(batch, Back, new Rectangle(0, frame * 68, 690, 68), Vector2.Zero);

            DrawLocalString(batch, HouseRollover.FamilyName(Family), Vector2.Zero, NameStyle, new Rectangle(20, 0, 200, 68), TextAlignment.Left | TextAlignment.Middle);
            var x = 230;
            foreach (var (name, head) in Heads.Take(8))
            {
                if (Frame != null) DrawLocalTexture(batch, Frame, new Vector2(x, 16));
                if (head != null)
                {
                    var src = new Rectangle(head.Width / 8, head.Height / 10, head.Width * 3 / 4, head.Height * 3 / 4);
                    DrawLocalTexture(batch, head, src, new Vector2(x + 2, 18), new Vector2((HeadSize - 4f) / src.Width));
                }
                x += HeadSize + 4;
            }
            var worth = "§" + (Family.ValueInArch + Family.Budget).ToString("##,#0");
            DrawLocalString(batch, worth, Vector2.Zero, ValueStyle, new Rectangle(540, 0, 80, 68), TextAlignment.Center | TextAlignment.Middle);
            DrawLocalString(batch, Family.FamilyFriends.ToString(), Vector2.Zero, ValueStyle, new Rectangle(627, 0, 60, 68), TextAlignment.Center | TextAlignment.Middle);
        }
    }

    /// <summary>
    /// A button drawn from two frames of a strip (normal and hovered), such as the family bin's scroll arrows.
    /// </summary>
    public class UIClassicFrameButton : UIElement
    {
        private Texture2D Strip;
        private int FrameWidth;
        private int Normal;
        private int Over;
        private bool Hovered;
        public bool Disabled;
        public event Action OnClick;

        public UIClassicFrameButton(Texture2D strip, int frames, int normal, int over)
        {
            Strip = strip;
            FrameWidth = (strip?.Width ?? frames) / frames;
            Normal = normal;
            Over = over;
            ListenForMouse(new Rectangle(0, 0, FrameWidth, strip?.Height ?? 1), (type, state) =>
            {
                switch (type)
                {
                    case UIMouseEventType.MouseOver: Hovered = true; break;
                    case UIMouseEventType.MouseOut: Hovered = false; break;
                    case UIMouseEventType.MouseDown: if (!Disabled && Visible) OnClick?.Invoke(); break;
                }
            });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Strip == null) return;
            var frame = (Hovered && !Disabled) ? Over : Normal;
            DrawLocalTexture(batch, Strip, new Rectangle(frame * FrameWidth, 0, FrameWidth, Strip.Height), Vector2.Zero, Vector2.One, Disabled ? Color.White * 0.4f : Color.White);
        }
    }
}
