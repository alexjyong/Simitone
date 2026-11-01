using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.HIT;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic live mode strip along the bottom of the screen, right of the classic control panel: the selected
    /// Sim's portrait and the rest of the family, the mood gauge with the panel buttons, and the selected panel.
    /// Each panel is drawn with the original art.
    /// </summary>
    public class UIClassicLivePanel : UIContainer
    {
        public const int StripHeight = 100;
        //where the parts sit along the strip, as in the Windows game
        private const int GadgetX = 202;
        private const int PanelX = 310;

        private TS1GameScreen Game;
        private UIImage Background;
        private UIImage Gadget;
        private Texture2D GreenBars;
        private Texture2D RedBars;
        private Texture2D Frame;
        private Texture2D ThumbBack;

        //live mode panels, by the UIMainPanel live category IDs (0 motives, 1 job, 2 personality, 3 relationships, 4 inventory),
        //then the classic UI's own (5 interests, 6 house)
        private UIButton[] PanelButtons = new UIButton[7];
        private int PanelID = -1;
        private UIElement Panel;
        private int PanelWidth; //the space the open panel was made for

        private List<UIClassicPortrait> Portraits = new List<UIClassicPortrait>();
        private string LastFamily = "";
        private VMAvatar LastSelected;

        public UIClassicLivePanel(TS1GameScreen game)
        {
            Game = game;
            Texture2D tex(string name) => ClassicUIGraphics.Get("cpanel/" + name);

            Add(Background = new UIImage(tex("Backgrounds/PanelBack.bmp")));
            Add(Gadget = new UIImage(tex("Backgrounds/LiveGadget.bmp")) { Position = new Vector2(GadgetX, 0) });
            GreenBars = tex("Greenbars.BMP");
            RedBars = tex("Redbars.bmp");
            Frame = tex("Buttons/IconFrame.bmp");
            ThumbBack = tex("People/ThumbBack.bmp");

            UIButton btn(string name, int x, int y, int panel, string tooltip)
            {
                var b = new UIButton(tex("Buttons/" + name)) { Position = new Vector2(GadgetX + x, y), Tooltip = tooltip };
                if (panel == -1) b.Disabled = true; //no Simitone panel for this yet
                else
                {
                    PanelButtons[panel] = b;
                    b.OnButtonClick += (e) => ShowPanel(panel);
                }
                Add(b);
                return b;
            }
            btn("Mood.bmp", 7, 30, 0, "Mood");
            btn("Interest.bmp", 43, 3, 5, "Interests");
            btn("Relationship.BMP", 76, 3, 3, "Relationships");
            btn("Personality.bmp", 43, 34, 2, "Personality");
            btn("Job.bmp", 76, 34, 1, "Job");
            btn("Gift.bmp", 43, 65, 4, "Inventory");
            btn("House.bmp", 76, 65, 6, "House");

            GameResized();
            ShowPanel(0);
        }

        public override void GameResized()
        {
            base.GameResized();
            //the art is 804 wide, the strip's width at 1024x768; wider screens stretch its plain right end
            Background.SetSize(Math.Max(Background.Texture.Width, UIScreen.Current.ScreenWidth - X), StripHeight);
        }

        public void ShowPanel(int id)
        {
            if (id == PanelID) return;
            HITVM.Get().PlaySoundEvent(UISounds.Click);
            PanelID = id;
            for (int i = 0; i < PanelButtons.Length; i++) PanelButtons[i].Selected = i == id;
            CreatePanel();
        }

        private void CreatePanel()
        {
            if (Panel != null) Remove(Panel);
            PanelWidth = AvailableWidth();
            switch (PanelID)
            {
                case 0: Panel = new UIClassicNeedsPanel(Game); break;
                case 1: Panel = new UIClassicJobPanel(Game); break;
                case 2: Panel = new UIClassicPersonalityPanel(Game); break;
                case 3: Panel = new UIClassicRelationshipPanel(Game, PanelWidth); break;
                case 5: Panel = new UIClassicInterestsPanel(Game); break;
                case 6: Panel = new UIClassicHousePanel(Game); break;
                default: Panel = new UIClassicInventoryPanel(Game, PanelWidth); break;
            }
            Panel.Position = new Vector2(PanelX, 0);
            Add(Panel);
        }

        private int AvailableWidth() => UIScreen.Current.ScreenWidth - (int)X - PanelX;

        public override void Update(UpdateState state)
        {
            base.Update(state);
            //the strip is placed and resized after it's made; the paged panels fit as many columns as there's room for
            if (PanelWidth != AvailableWidth()) CreatePanel();
            var family = Game.vm.TS1State.CurrentFamily;
            var members = (family == null) ? new List<VMAvatar>() : Game.vm.Context.ObjectQueries.Avatars
                .Where(x => family.RuntimeSubset.Contains(x.Object.OBJ.GUID)).OfType<VMAvatar>().ToList();
            var selected = Game.SelectedAvatar;
            var key = string.Join(",", members.Select(x => x.ObjectID));
            if (key != LastFamily || selected != LastSelected)
            {
                LastFamily = key;
                LastSelected = selected;
                UpdatePortraits(members, selected);
            }
        }

        /// <summary>
        /// The selected Sim in the big frame, and the others in small thumbnails beside it (click one to select them).
        /// </summary>
        private void UpdatePortraits(List<VMAvatar> members, VMAvatar selected)
        {
            foreach (var p in Portraits) Remove(p);
            Portraits.Clear();
            int thumb = 0;
            foreach (var sim in members)
            {
                var big = sim == selected;
                var p = new UIClassicPortrait(UIIconCache.GetObject(sim), big ? Frame : ThumbBack, big)
                {
                    Tooltip = sim.Name
                };
                if (big) p.Position = new Vector2(15, 4);
                else
                {
                    //two rows of thumbnails
                    p.Position = new Vector2(68 + (thumb / 2) * 29, 8 + (thumb % 2) * 47);
                    thumb++;
                    var id = sim.ObjectID;
                    p.OnClick += () =>
                    {
                        HITVM.Get().PlaySoundEvent(UISounds.QueueAdd);
                        Game.vm.SendCommand(new VMNetChangeControlCmd() { TargetID = id });
                    };
                }
                Portraits.Add(p);
                Add(p);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible) return;
            //mood: lit bars from the middle of the gauge, green up for a good mood and red down for a bad one (5 bars each)
            var mood = Game.SelectedAvatar?.GetMotiveData(VMMotive.Mood) ?? 0;
            var bars = (int)Math.Ceiling(Math.Min(100, Math.Abs((int)mood)) / 20f);
            if (bars == 0) return;
            var h = bars * 5;
            if (mood > 0) DrawLocalTexture(batch, GreenBars, new Rectangle(0, 25 - h, 27, h), new Vector2(GadgetX + 9, 3 + 25 - h));
            else DrawLocalTexture(batch, RedBars, new Rectangle(0, 0, 27, h), new Vector2(GadgetX + 9, 72));
        }
    }

    /// <summary>
    /// A family member's head in a frame: the big one for the selected Sim (IconFrame), small ones for the others.
    /// </summary>
    public class UIClassicPortrait : UIElement
    {
        private Texture2D Head;
        private Texture2D Frame;
        private Texture2D Px;
        private bool Big;
        public event Action OnClick;

        public UIClassicPortrait(Texture2D head, Texture2D frame, bool big)
        {
            Head = head;
            Frame = frame;
            Big = big;
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            var size = big ? 45 : frame.Width;
            ListenForMouse(new Rectangle(0, 0, size, size), (type, state) =>
            {
                if (type == UIMouseEventType.MouseUp) OnClick?.Invoke();
            });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            int size, border;
            if (Big)
            {
                //IconFrame's blue middle is a placeholder; the game draws the selected Sim in cyan on navy
                size = 45;
                border = 2;
                DrawLocalTexture(batch, Px, null, Vector2.Zero, new Vector2(size), new Color(40, 224, 248));
                DrawLocalTexture(batch, Px, null, new Vector2(border), new Vector2(size - border * 2), new Color(0, 0, 64));
            }
            else
            {
                size = Frame.Width;
                border = 1;
                DrawLocalTexture(batch, Frame, Vector2.Zero);
            }
            if (Head == null) return;
            //the head icons have room around the head; show the face close up
            var crop = new Rectangle(Head.Width / 8, Head.Height / 10, Head.Width * 3 / 4, Head.Height * 3 / 4);
            var inner = size - border * 2f;
            DrawLocalTexture(batch, Head, crop, new Vector2(border), new Vector2(inner / crop.Width, inner / crop.Height));
        }
    }

    /// <summary>
    /// The classic Needs panel: the eight motives in two columns, each a bar that is green when the need is met and
    /// red when it isn't, filled from the left by how far it is from -100 to 100.
    /// </summary>
    public class UIClassicNeedsPanel : UIContainer
    {
        private static readonly VMMotive[] Motives = new VMMotive[]
        {
            VMMotive.Hunger, VMMotive.Comfort, VMMotive.Hygiene, VMMotive.Bladder,
            VMMotive.Energy, VMMotive.Fun, VMMotive.Social, VMMotive.Room
        };
        private const int BarWidth = 61;
        private const int BarHeight = 4;

        private TS1GameScreen Game;
        private Texture2D Px;
        private Vector2[] BarPos = new Vector2[8];

        public UIClassicNeedsPanel(TS1GameScreen game)
        {
            Game = game;
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);

            var title = ClassicFont.Style(12);
            title.Color = Color.White;
            Add(new UILabel() { Caption = "Needs", CaptionStyle = title, Position = new Vector2(8, 1) });

            var style = ClassicFont.Style(8);
            style.Color = Color.White;
            for (int i = 0; i < 8; i++)
            {
                var x = 21 + (i / 4) * 102;
                var y = 21 + (int)((i % 4) * 19.4f);
                Add(new UILabel()
                {
                    //f102 has the motive names in this order
                    Caption = GameFacade.Strings.GetString("f102", (i + 1).ToString()),
                    CaptionStyle = style,
                    Alignment = TextAlignment.Center | TextAlignment.Middle,
                    Position = new Vector2(x, y),
                    Size = new Vector2(BarWidth, 13)
                });
                BarPos[i] = new Vector2(x, y + 14);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible) return;
            var sim = Game.SelectedAvatar;
            for (int i = 0; i < 8; i++)
            {
                var value = (sim == null) ? 0 : sim.GetMotiveData(Motives[i]);
                var fill = (int)Math.Round(BarWidth * Math.Max(0, Math.Min(200, value + 100)) / 200f);
                var pos = BarPos[i];
                DrawLocalTexture(batch, Px, null, pos, new Vector2(BarWidth, BarHeight), new Color(96, 104, 112));
                DrawLocalTexture(batch, Px, null, pos, new Vector2(fill, BarHeight), (value >= 0) ? new Color(32, 208, 48) : new Color(224, 40, 32));
            }
        }
    }
}
