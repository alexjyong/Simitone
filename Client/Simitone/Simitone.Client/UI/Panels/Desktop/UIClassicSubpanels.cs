using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.Utils;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
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
    /// Shared pieces of the classic live mode panels, which sit in the 100 high strip right of the mood gauge.
    /// </summary>
    public abstract class UIClassicSubpanel : UIContainer
    {
        protected TS1GameScreen Game;
        protected Texture2D Px;

        public UIClassicSubpanel(TS1GameScreen game)
        {
            Game = game;
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
        }

        protected static Texture2D Tex(string name) => ClassicUIGraphics.Get("cpanel/" + name);

        protected static TextStyle Style(int size)
        {
            var style = ClassicFont.Style(size);
            style.Color = Color.White;
            return style;
        }

        protected UILabel AddLabel(string caption, int size, Vector2 pos)
        {
            var label = new UILabel() { Caption = caption, CaptionStyle = Style(size), Position = pos };
            Add(label);
            return label;
        }

        /// <summary>
        /// A 10 point bar (skills and personality): the lit segments over the unlit art, and optionally the
        /// highlight on the segment a promotion needs.
        /// </summary>
        protected void DrawPoints(UISpriteBatch batch, Texture2D off, Texture2D on, Vector2 pos, int points, int needed = 0)
        {
            DrawLocalTexture(batch, off, pos);
            points = Math.Max(0, Math.Min(10, points));
            //each segment is 4px; "on" may be shorter than "off" (personality), so stretch it to match
            var scale = new Vector2(1, off.Height / (float)on.Height);
            if (points > 0) DrawLocalTexture(batch, on, new Rectangle(0, 0, points * 4, on.Height), pos, scale);
            if (needed > 0 && needed <= 10) DrawLocalTexture(batch, Tex("SkillsHilite.bmp"), null, pos + new Vector2((needed - 1) * 4, 0), scale);
        }

        /// <summary>
        /// A -100 to 100 bar like the Needs panel's: green when good, red when bad.
        /// </summary>
        protected void DrawValueBar(UISpriteBatch batch, Vector2 pos, int width, int value)
        {
            var fill = (int)Math.Round(width * Math.Max(0, Math.Min(200, value + 100)) / 200f);
            DrawLocalTexture(batch, Px, null, pos, new Vector2(width, 4), new Color(96, 104, 112));
            DrawLocalTexture(batch, Px, null, pos, new Vector2(fill, 4), (value >= 0) ? new Color(32, 208, 48) : new Color(224, 40, 32));
        }
    }

    /// <summary>
    /// The classic Job panel: the job and its pay and hours, performance, and the skills and friends the next
    /// promotion needs (the highlighted segment is the level needed).
    /// </summary>
    public class UIClassicJobPanel : UIClassicSubpanel
    {
        private static readonly string[] SkillNames = new string[] { "Cooking", "Mechanical", "Charisma", "Body", "Logic", "Creativity" };
        private static readonly VMPersonDataVariable[] SkillVars = new VMPersonDataVariable[]
        {
            VMPersonDataVariable.CookingSkill, VMPersonDataVariable.MechanicalSkill, VMPersonDataVariable.CharismaSkill,
            VMPersonDataVariable.BodySkill, VMPersonDataVariable.LogicSkill, VMPersonDataVariable.CreativitySkill
        };

        private UILabel JobLabel;
        private UILabel CareerLabel;
        private UILabel SalaryLabel;
        private UILabel FriendsLabel;
        private UIImage SalaryIcon;
        private UIImage PerformanceIcon;
        private Texture2D BarsOn;
        private Texture2D BarsOff;
        private JobLevel Level;
        private int Performance;

        public UIClassicJobPanel(TS1GameScreen game) : base(game)
        {
            BarsOn = Tex("JobSubBars.bmp");
            BarsOff = Tex("Backgrounds/JobSubBarsOff.bmp");

            JobLabel = AddLabel("", 11, new Vector2(8, 3));
            CareerLabel = AddLabel("", 8, new Vector2(8, 20));
            //the 4 frame icons; the second (cyan) frame, as the game shows them
            Add(SalaryIcon = new UIImage(Tex("Buttons/SalaryButton.bmp")) { Position = new Vector2(8, 40), SourceRectangle = new Rectangle(13, 0, 13, 11) });
            SalaryLabel = AddLabel("", 9, new Vector2(25, 37));
            Add(PerformanceIcon = new UIImage(Tex("Buttons/PerformanceButton.bmp")) { Position = new Vector2(8, 60), SourceRectangle = new Rectangle(13, 0, 13, 12) });
            AddLabel("Performance", 9, new Vector2(25, 57));
            FriendsLabel = AddLabel("", 8, new Vector2(8, 82));

            for (int i = 0; i < 6; i++)
                AddLabel(SkillNames[i], 8, SkillPos(i) + new Vector2(-70, -3));
        }

        private static Vector2 SkillPos(int i)
        {
            //two columns of three, the bar right of its name
            return new Vector2(272 + (i / 3) * 124, 12 + (i % 3) * 26);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var sim = Game.SelectedAvatar;
            if (sim == null) return;
            var job = Content.Get().Jobs.GetJob((ushort)sim.GetPersonData(VMPersonDataVariable.JobType));
            var level = sim.GetPersonData(VMPersonDataVariable.JobPromotionLevel);
            Level = (job != null && level >= 0 && level < job.JobLevels.Length) ? job.JobLevels[level] : null;
            Performance = sim.GetPersonData(VMPersonDataVariable.JobPerformance);

            SalaryIcon.Visible = PerformanceIcon.Visible = Level != null;
            if (Level == null)
            {
                JobLabel.Caption = "Unemployed";
                CareerLabel.Caption = SalaryLabel.Caption = FriendsLabel.Caption = "";
                return;
            }
            JobLabel.Caption = Level.JobName;
            CareerLabel.Caption = job.Name + ", level " + (level + 1);
            SalaryLabel.Caption = "§" + Level.Salary + "   " + ToTime(Level.StartTime) + " - " + ToTime(Level.EndTime);
            //MinRequired is friends, then the skills (in hundredths of a point). The family's count is the one the game's
            //promotions read, kept with the family
            var friends = Level.MinRequired[0];
            var have = Game.vm.TS1State.CurrentFamily?.FamilyFriends ?? 0;
            FriendsLabel.Caption = (friends > 0) ? "Family friends: " + have + " of " + friends : "";
        }

        private static string ToTime(int hour)
        {
            return ((hour % 12 == 0) ? 12 : hour % 12) + ((hour >= 12 && hour < 24) ? "pm" : "am");
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible) return;
            var sim = Game.SelectedAvatar;
            if (Level != null) DrawValueBar(batch, new Vector2(25, 73), 110, Performance);
            for (int i = 0; i < 6; i++)
            {
                var points = (sim == null) ? 0 : sim.GetPersonData(SkillVars[i]) / 100;
                DrawPoints(batch, BarsOff, BarsOn, SkillPos(i), points, (Level == null) ? 0 : Level.MinRequired[i + 1] / 100);
            }
        }
    }

    /// <summary>
    /// The classic Personality panel: the five traits as 10 point bars, and the Sim's star sign (the one closest to
    /// its personality, as the game shows it), which explains itself when the mouse is over it.
    /// </summary>
    public class UIClassicPersonalityPanel : UIClassicSubpanel
    {
        private static readonly string[] TraitNames = new string[] { "Neat", "Outgoing", "Active", "Playful", "Nice" };
        private static readonly VMPersonDataVariable[] TraitVars = new VMPersonDataVariable[]
        {
            VMPersonDataVariable.NeatPersonality, VMPersonDataVariable.OutgoingPersonality, VMPersonDataVariable.ActivePersonality,
            VMPersonDataVariable.PlayfulPersonality, VMPersonDataVariable.NicePersonality
        };

        private Texture2D BarsOn;
        private Texture2D BarsOff;
        private UILabel SignLabel;
        private UIClassicTip SignTip;
        private int ShownSign = -1;

        public UIClassicPersonalityPanel(TS1GameScreen game) : base(game)
        {
            BarsOn = Tex("JobSubBars.bmp");
            BarsOff = Tex("Backgrounds/PersBkg.bmp");
            AddLabel("Personality", 12, new Vector2(8, 1));
            for (int i = 0; i < 5; i++) AddLabel(TraitNames[i], 9, TraitPos(i) + new Vector2(-70, -2));
            SignLabel = AddLabel("", 10, new Vector2(8, 26));
            Add(SignTip = new UIClassicTip(null, Rectangle.Empty, new Rectangle(0, 0, 130, 18), null) { Position = new Vector2(8, 26) });
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var sim = Game.SelectedAvatar;
            var sign = (sim == null) ? -1 : ZodiacSign.Of(sim.GetPersonData(TraitVars[0]) / 100, sim.GetPersonData(TraitVars[1]) / 100,
                sim.GetPersonData(TraitVars[2]) / 100, sim.GetPersonData(TraitVars[3]) / 100, sim.GetPersonData(TraitVars[4]) / 100);
            if (sign == ShownSign) return;
            ShownSign = sign;
            SignLabel.Caption = (sign == -1) ? "" : ZodiacSign.Name(sign);
            SignTip.Tooltip = (sign == -1) ? null : ZodiacSign.Tooltip(sign);
        }

        private static Vector2 TraitPos(int i)
        {
            return new Vector2(220, 8 + i * 18);
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible) return;
            var sim = Game.SelectedAvatar;
            for (int i = 0; i < 5; i++)
                DrawPoints(batch, BarsOff, BarsOn, TraitPos(i), (sim == null) ? 0 : sim.GetPersonData(TraitVars[i]) / 100);
        }
    }

    /// <summary>
    /// The classic Relationships panel: framed faces of everyone the Sim knows, coloured red to green by their daily
    /// relationship, with a smiley for friends (50 or more) and a heart for love: pink when the Sim loves them, red
    /// when it's mutual. Love is a relationship's second value being 1, as the game's "Do I Love temp 0?" tests. The buttons show friends, family or everyone; the arrows page.
    /// </summary>
    public class UIClassicRelationshipPanel : UIClassicSubpanel
    {
        private const int Rows = 2;
        private const int CellSize = 47;
        private const int GridX = 66;
        //faces of neighbours, which need an object instance to render; kept for the session
        private static Dictionary<uint, Texture2D> Faces = new Dictionary<uint, Texture2D>();

        private int Filter = -1; //-1 all, 0 friends, 1 family
        private int Page;
        private int Columns;
        private List<UIButton> SortButtons = new List<UIButton>();
        private UIButton PrevButton;
        private UIButton NextButton;
        private List<UIClassicRelationship> Cells = new List<UIClassicRelationship>();
        private string LastKey;

        public UIClassicRelationshipPanel(TS1GameScreen game, int width) : base(game)
        {
            UIButton sort(string name, int x, int y, int filter, string tooltip)
            {
                var b = new UIButton(Tex("Buttons/" + name)) { Position = new Vector2(x, y), Tooltip = tooltip };
                b.OnButtonClick += (e) => { Filter = filter; Page = 0; LastKey = null; };
                if (filter == 2) b.Disabled = true; //fame (Superstar) isn't tracked here yet
                SortButtons.Add(b);
                Add(b);
                return b;
            }
            sort("RelFriendSort.bmp", 10, 26, 0, "Friends");
            sort("RelFamilySort.bmp", 32, 26, 1, "Family");
            sort("RelAllSort.bmp", 10, 50, -1, "Everyone");
            sort("RelStarSort.bmp", 32, 50, 2, "Famous");

            Columns = Math.Max(1, (width - GridX - 24) / CellSize);
            Add(PrevButton = new UIButton(Tex("Buttons/ScrollLeft.BMP")) { Position = new Vector2(GridX - 11, 26), Tooltip = "Previous" });
            Add(NextButton = new UIButton(Tex("Buttons/ScrollRight.BMP")) { Position = new Vector2(GridX + Columns * CellSize + 2, 26), Tooltip = "Next" });
            PrevButton.OnButtonClick += (e) => { Page--; LastKey = null; };
            NextButton.OnButtonClick += (e) => { Page++; LastKey = null; };
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            for (int i = 0; i < SortButtons.Count; i++) SortButtons[i].Selected = new int[] { 0, 1, -1, 2 }[i] == Filter;

            var sim = Game.SelectedAvatar;
            var neighbours = Content.Get().Neighborhood;
            var me = (sim == null) ? null : neighbours.GetNeighborByID(sim.GetPersonData(VMPersonDataVariable.NeighborId));
            if (me == null) return;
            var family = Game.vm.TS1State.CurrentFamily?.FamilyGUIDs ?? new uint[0];

            var known = me.Relationships.Keys
                .Select(id => neighbours.GetNeighborByID((short)id))
                .Where(n => n != null)
                .Where(n => Filter == -1
                    || (Filter == 0 && Daily(me, n.NeighbourID) >= 50)
                    || (Filter == 1 && family.Contains(n.GUID)))
                .OrderByDescending(n => Daily(me, n.NeighbourID))
                .ToList();
            var perPage = Columns * Rows;
            var pages = Math.Max(1, (known.Count + perPage - 1) / perPage);
            Page = Math.Max(0, Math.Min(pages - 1, Page));
            PrevButton.Disabled = Page == 0;
            NextButton.Disabled = Page >= pages - 1;

            var shown = known.Skip(Page * perPage).Take(perPage).ToList();
            var key = me.NeighbourID + ":" + string.Join(",", shown.Select(x => x.NeighbourID));
            if (key != LastKey)
            {
                LastKey = key;
                foreach (var c in Cells) Remove(c);
                Cells.Clear();
                for (int i = 0; i < shown.Count; i++)
                {
                    var cell = new UIClassicRelationship(Face(shown[i].GUID), Name(shown[i].GUID))
                    {
                        Position = new Vector2(GridX + (i / Rows) * CellSize, 4 + (i % Rows) * CellSize)
                    };
                    Cells.Add(cell);
                    Add(cell);
                }
            }
            for (int i = 0; i < Cells.Count; i++)
            {
                Cells[i].Value = Daily(me, shown[i].NeighbourID);
                Cells[i].Love = Loves(me, shown[i]) ? (Loves(shown[i], me) ? 2 : 1) : 0;
            }
        }

        private static int Daily(Neighbour me, short id)
        {
            return (me.Relationships.TryGetValue(id, out var values) && values.Count > 0) ? values[0] : 0;
        }

        private static bool Loves(Neighbour from, Neighbour to)
        {
            return from.Relationships.TryGetValue(to.NeighbourID, out var values) && values.Count > 1 && values[1] == 1;
        }

        private Texture2D Face(uint guid)
        {
            if (Faces.TryGetValue(guid, out var face)) return face;
            if (Content.Get().WorldObjects.Get(guid) != null)
            {
                var obj = Game.vm.Context.CreateObjectInstance(guid, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                if (obj != null)
                {
                    face = UIIconCache.GetObject(obj.BaseObject);
                    obj.Delete(Game.vm.Context);
                }
            }
            Faces[guid] = face;
            return face;
        }

        private static string Name(uint guid)
        {
            var obj = Content.Get().WorldObjects.Get(guid);
            if (obj == null) return "";
            return obj.Resource.Get<CTSS>(obj.OBJ.CatalogStringsID)?.GetString(0) ?? obj.OBJ.ChunkLabel;
        }
    }

    /// <summary>
    /// One face in the classic Relationships panel.
    /// </summary>
    public class UIClassicRelationship : UIElement
    {
        private Texture2D Frame;
        private Texture2D Face;
        private Texture2D Smiley;
        private Texture2D Heart;
        private Texture2D DeepHeart;
        private Texture2D Px;
        public int Value;
        public int Love; //0 none, 1 one way, 2 mutual

        public UIClassicRelationship(Texture2D face, string name)
        {
            Face = face;
            Frame = ClassicUIGraphics.Get("cpanel/People/PeopleTemplate.bmp");
            Smiley = ClassicUIGraphics.Get("cpanel/Buttons/Smiley.bmp");
            Heart = ClassicUIGraphics.Get("cpanel/Buttons/Heart.bmp");
            DeepHeart = ClassicUIGraphics.Get("cpanel/Buttons/HeartDeep.bmp");
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            Tooltip = name;
            ListenForMouse(new Rectangle(0, 0, 45, 45), (type, state) => { });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            //red for a bad relationship, through grey, to green for a good one (as the game's UnknownRel art)
            var t = Math.Max(-100, Math.Min(100, Value)) / 100f;
            var grey = new Color(128, 128, 128);
            var bg = (t < 0) ? Color.Lerp(grey, new Color(208, 0, 0), -t) : Color.Lerp(grey, new Color(0, 160, 0), t);
            //PeopleTemplate is 4 frames of 45x45 (the first is the plain one) with a solid middle, so the face goes over it
            DrawLocalTexture(batch, Frame, new Rectangle(0, 0, 45, 45), Vector2.Zero);
            DrawLocalTexture(batch, Px, null, new Vector2(3), new Vector2(39), bg);
            if (Face != null)
            {
                var crop = new Rectangle(Face.Width / 8, Face.Height / 10, Face.Width * 3 / 4, Face.Height * 3 / 4);
                DrawLocalTexture(batch, Face, crop, new Vector2(3), new Vector2(39f / crop.Width, 39f / crop.Height));
            }
            if (Value >= 50) DrawLocalTexture(batch, Smiley, new Vector2(34, 34));
            if (Love > 0) DrawLocalTexture(batch, (Love == 2) ? DeepHeart : Heart, new Vector2(34, 2));
        }
    }

    /// <summary>
    /// The classic Inventory panel: the family's items in the same frames as the Relationships panel, with how many
    /// there are. The buttons show gifts and other items, magic, or ingredients (Makin' Magic); the arrows page.
    /// </summary>
    public class UIClassicInventoryPanel : UIClassicSubpanel
    {
        private const int Rows = 2;
        private const int CellSize = 47;
        private const int GridX = 66;
        //as Simitone's inventory: 2 is hidden, and 7 and 8 (magic and ingredients) only show when picked
        private static readonly HashSet<int> HiddenTypes = new HashSet<int>() { 2, 6, 7, 8 };

        private int Filter = -1; //-1 everything not hidden, otherwise that item type
        private int Page;
        private int Columns;
        private List<UIButton> SortButtons = new List<UIButton>();
        private List<int> SortFilters = new List<int>();
        private UIButton PrevButton;
        private UIButton NextButton;
        private List<UIClassicInventoryItem> Cells = new List<UIClassicInventoryItem>();
        private string LastKey;

        public UIClassicInventoryPanel(TS1GameScreen game, int width) : base(game)
        {
            void sort(string name, int x, int y, int filter, string tooltip)
            {
                var b = new UIButton(Tex("Buttons/" + name)) { Position = new Vector2(x, y), Tooltip = tooltip };
                b.OnButtonClick += (e) => { Filter = filter; Page = 0; LastKey = null; };
                SortButtons.Add(b);
                SortFilters.Add(filter);
                Add(b);
            }
            sort("InventoryGiftSort.bmp", 10, 26, -1, "Gifts");
            sort("InventoryMagicSort.bmp", 32, 26, 7, "Magic");
            sort("InventoryOtherSort.bmp", 10, 50, 8, "Ingredients");

            Columns = Math.Max(1, (width - GridX - 24) / CellSize);
            Add(PrevButton = new UIButton(Tex("Buttons/ScrollLeft.BMP")) { Position = new Vector2(GridX - 11, 26), Tooltip = "Previous" });
            Add(NextButton = new UIButton(Tex("Buttons/ScrollRight.BMP")) { Position = new Vector2(GridX + Columns * CellSize + 2, 26), Tooltip = "Next" });
            PrevButton.OnButtonClick += (e) => { Page--; LastKey = null; };
            NextButton.OnButtonClick += (e) => { Page++; LastKey = null; };
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            for (int i = 0; i < SortButtons.Count; i++) SortButtons[i].Selected = SortFilters[i] == Filter;

            var sim = Game.SelectedAvatar;
            if (sim == null) return;
            var items = Content.Get().Neighborhood.GetInventoryByNID(sim.GetPersonData(VMPersonDataVariable.NeighborId))?
                .Where(x => (Filter == -1) ? !HiddenTypes.Contains(x.Type) : x.Type == Filter)
                .ToList() ?? new List<InventoryItem>();
            var perPage = Columns * Rows;
            var pages = Math.Max(1, (items.Count + perPage - 1) / perPage);
            Page = Math.Max(0, Math.Min(pages - 1, Page));
            PrevButton.Disabled = Page == 0;
            NextButton.Disabled = Page >= pages - 1;

            var shown = items.Skip(Page * perPage).Take(perPage).ToList();
            var key = string.Join(",", shown.Select(x => x.GUID + "x" + x.Count));
            if (key == LastKey) return;
            LastKey = key;
            foreach (var c in Cells) Remove(c);
            Cells.Clear();
            for (int i = 0; i < shown.Count; i++)
            {
                var cell = new UIClassicInventoryItem(shown[i])
                {
                    Position = new Vector2(GridX + (i / Rows) * CellSize, 4 + (i % Rows) * CellSize)
                };
                Cells.Add(cell);
                Add(cell);
            }
        }
    }

    /// <summary>
    /// One item in the classic Inventory panel: its catalog picture in a frame, and the count when there's more than one.
    /// </summary>
    public class UIClassicInventoryItem : UIContainer
    {
        private Texture2D Frame;
        private Texture2D Icon;
        private Texture2D Px;

        public UIClassicInventoryItem(InventoryItem item)
        {
            Frame = ClassicUIGraphics.Get("cpanel/People/PeopleTemplate.bmp");
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            var obj = Content.Get().WorldObjects.Get(item.GUID);
            if (obj != null)
            {
                Icon = obj.Resource.Get<BMP>(obj.OBJ.CatalogStringsID)?.GetTexture(GameFacade.GraphicsDevice);
                Tooltip = obj.Resource.Get<CTSS>(obj.OBJ.CatalogStringsID)?.GetString(0) ?? obj.OBJ.ChunkLabel;
            }
            if (item.Count > 1)
            {
                var style = ClassicFont.Style(8);
                style.Color = Color.White;
                style.Shadow = true;
                Add(new UILabel()
                {
                    Caption = item.Count.ToString(),
                    CaptionStyle = style,
                    Alignment = TextAlignment.Right | TextAlignment.Bottom,
                    Position = new Vector2(3, 3),
                    Size = new Vector2(38, 39)
                });
            }
            ListenForMouse(new Rectangle(0, 0, 45, 45), (type, state) => { });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            DrawLocalTexture(batch, Frame, new Rectangle(0, 0, 45, 45), Vector2.Zero);
            DrawLocalTexture(batch, Px, null, new Vector2(3), new Vector2(39), new Color(0, 0, 64));
            if (Icon != null)
            {
                //catalog pictures are two frames side by side; the first is the normal one
                var src = new Rectangle(0, 0, Icon.Width / 2, Icon.Height);
                var scale = 39f / Math.Max(src.Width, src.Height);
                var pos = new Vector2(3 + (39 - src.Width * scale) / 2, 3 + (39 - src.Height * scale) / 2);
                DrawLocalTexture(batch, Icon, src, pos, new Vector2(scale));
            }
            base.Draw(batch);
        }
    }

    /// <summary>
    /// The classic Interests panel: what the Sim likes to talk about, as each topic's picture and a 10 point bar.
    /// Adults and children share four of the game's interest slots (Travel or Toys, and so on), and Hot Date's
    /// topics use older ones; pets have none. Names and descriptions are the game's (GameData/Live.iff, 140 and 142).
    /// </summary>
    public class UIClassicInterestsPanel : UIClassicSubpanel
    {
        //the topic's string (Live.iff table 140; its art is in the same order), its person data slot, and who has it
        private enum Who { Everyone, Adults, Children }
        private static readonly (int Name, string Art, int Slot, Who Who)[] Interests =
        {
            (1, "Travel", 46, Who.Adults), (2, "Violence", 47, Who.Adults), (3, "Politics", 48, Who.Adults), (4, "The60s", 49, Who.Adults),
            (9, "Toys", 46, Who.Children), (10, "Aliens", 47, Who.Children), (11, "Pets", 48, Who.Children), (12, "School", 49, Who.Children),
            (5, "Weather", 50, Who.Everyone), (6, "Sports", 51, Who.Everyone), (7, "Music", 52, Who.Everyone), (8, "Outdoors", 53, Who.Everyone),
            (13, "Exercise", 13, Who.Everyone), (14, "Food", 14, Who.Everyone), (15, "Parties", 16, Who.Everyone), (16, "Style", 20, Who.Everyone),
            (17, "Charisma", 26, Who.Everyone), (18, "Health", 54, Who.Everyone), (19, "Romance", 55, Who.Everyone)
        };

        private Texture2D BarsOn;
        private Texture2D BarsOff;
        private List<(UIClassicTip Tip, int Slot)> Shown = new List<(UIClassicTip, int)>();
        private UILabel NoneLabel;
        private string ShownFor;

        public UIClassicInterestsPanel(TS1GameScreen game) : base(game)
        {
            BarsOn = Tex("InterestBars.bmp");
            BarsOff = Tex("Backgrounds/InterestBarsBackground.bmp");
            AddLabel(ClassicUIGraphics.GameString("Live.iff", 140, 0, "Interests"), 12, new Vector2(8, 1));
            NoneLabel = AddLabel(ClassicUIGraphics.GameString("Live.iff", 134, 9, "Not Available to Pets"), 10, new Vector2(80, 40));
        }

        private static Vector2 CellPos(int i)
        {
            //five columns of three, the bar right of the picture
            return new Vector2(16 + (i / 3) * 84, 20 + (i % 3) * 26);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var sim = Game.SelectedAvatar;
            var who = (sim == null || sim.IsPet) ? "none" : (sim.IsChild ? "child" : "adult");
            if (who == ShownFor) return;
            ShownFor = who;
            foreach (var cell in Shown) Remove(cell.Tip);
            Shown.Clear();
            NoneLabel.Visible = sim?.IsPet == true;
            if (who == "none") return;
            var mine = Interests.Where(x => x.Who == Who.Everyone || x.Who == ((who == "child") ? Who.Children : Who.Adults)).ToList();
            for (int i = 0; i < mine.Count; i++)
            {
                var interest = mine[i];
                var art = Tex("Buttons/Interest" + interest.Art + ".BMP");
                var tip = new UIClassicTip(art, (art == null) ? Rectangle.Empty : new Rectangle(0, 0, art.Width / 3, art.Height), new Rectangle(0, 0, 70, 25),
                    ClassicUIGraphics.GameString("Live.iff", 140, interest.Name) + ": " + ClassicUIGraphics.GameString("Live.iff", 142, interest.Name)) { Position = CellPos(i) };
                Shown.Add((tip, interest.Slot));
                Add(tip);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible) return;
            var sim = Game.SelectedAvatar;
            if (sim == null) return;
            foreach (var cell in Shown)
                DrawPoints(batch, BarsOff, BarsOn, cell.Tip.Position + new Vector2(29, 5), sim.GetPersonData((VMPersonDataVariable)cell.Slot)); //interests run 0 to 10
        }
    }

    /// <summary>
    /// The classic House panel: the game's five ratings of the house as 10 point bars, and its size, bedrooms,
    /// bathrooms and lot. The names and descriptions are the game's (GameData/Live.iff, 133 and 135); the ratings are
    /// Simitone's, from HouseStats, as the game works them out in its engine.
    /// </summary>
    public class UIClassicHousePanel : UIClassicSubpanel
    {
        private Texture2D BarsOn;
        private Texture2D BarsOff;
        private HouseStats Stats;
        private float NextUpdate;
        private UILabel[] Facts = new UILabel[4];

        public UIClassicHousePanel(TS1GameScreen game) : base(game)
        {
            string str(int table, int i) => ClassicUIGraphics.GameString("Live.iff", (ushort)table, i);
            BarsOn = Tex("HouseSubBars.bmp");
            BarsOff = Tex("Backgrounds/HouseSubBarsOff.BMP");
            AddLabel(str(133, 0), 12, new Vector2(8, 1));
            for (int i = 0; i < 5; i++)
            {
                AddLabel(str(133, i + 1), 9, RatingPos(i) + new Vector2(-80, -2));
                Add(new UIClassicTip(null, Rectangle.Empty, new Rectangle(-80, -2, 120, 16), str(135, i * 2) + ": " + str(135, i * 2 + 1)) { Position = RatingPos(i) });
            }
            for (int i = 0; i < 4; i++)
            {
                Facts[i] = AddLabel("", 9, new Vector2(300, 6 + i * 20));
                Add(new UIClassicTip(null, Rectangle.Empty, new Rectangle(0, 0, 150, 18), str(135, 10 + i * 2) + ": " + str(135, 11 + i * 2)) { Position = Facts[i].Position });
            }
        }

        private static Vector2 RatingPos(int i)
        {
            return new Vector2(230, 8 + i * 18);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            //the ratings look over the whole lot, so not every frame
            NextUpdate -= 1 / 60f;
            if (NextUpdate > 0 && Stats != null) return;
            NextUpdate = 1;
            Stats = HouseStats.Of(Game.vm);
            string str(int i) => ClassicUIGraphics.GameString("Live.iff", 133, i);
            Facts[0].Caption = str(6).Replace("%d", Stats.SquareFeet.ToString());
            Facts[1].Caption = str(7).Replace("%d", Stats.Bedrooms.ToString());
            Facts[2].Caption = str(8).Replace("%d", Stats.Bathrooms.ToString());
            Facts[3].Caption = str(9).Replace("%s", Stats.Lot.Width + " x " + Stats.Lot.Height);
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible || Stats == null) return;
            var ratings = new[] { Stats.Size, Stats.Furnishings, Stats.Yard, Stats.Upkeep, Stats.Layout };
            for (int i = 0; i < 5; i++) DrawPoints(batch, BarsOff, BarsOn, RatingPos(i), ratings[i]);
        }
    }

    /// <summary>
    /// A part of a classic panel that explains itself in a tooltip when the mouse is over it, optionally with a picture.
    /// </summary>
    public class UIClassicTip : UIElement
    {
        private Texture2D Picture;
        private Rectangle Source;
        private Rectangle Area;
        public Action OnClick;

        public UIClassicTip(Texture2D picture, Rectangle source, Rectangle area, string tooltip)
        {
            Picture = picture;
            Source = source;
            Area = area;
            Tooltip = tooltip;
            ListenForMouse(area, (type, state) =>
            {
                if (type == UIMouseEventType.MouseDown) OnClick?.Invoke();
            });
            UIUtils.GiveTooltip(this); //shown while the mouse is inside the bounds
        }

        public override Rectangle GetBounds() => Area;

        public override void Draw(UISpriteBatch batch)
        {
            if (Visible && Picture != null) DrawLocalTexture(batch, Picture, Source, Vector2.Zero);
        }
    }
}
