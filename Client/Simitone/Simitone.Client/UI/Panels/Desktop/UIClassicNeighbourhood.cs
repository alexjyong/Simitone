using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.HIT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic neighbourhood screen, over Simitone's view of the neighbourhood: the game's banner along the top with
    /// its buttons, and a popup about the house under the mouse. Clicking a house plays it; the family button opens the
    /// family bin, which can send a family to move in (the moving van cursor), and the bulldozer evicts a family or
    /// bulldozes an empty house (Bulldozer), as the other towns' bulldozer does their lots. Unleashed's Rezone does both
    /// as needed, then makes an empty lot residential or community (LotZoning), and Import Family brings in the family
    /// files in the Import folder (FamilyImport). The other
    /// towns are reached from the main neighbourhood's banner, and each has a button to return to it.
    /// </summary>
    public class UIClassicNeighbourhood : UIContainer
    {
        private const int BarWidth = 800;
        private enum ClickMode { Normal, MoveIn, Evict, Rezone }

        private TS1GameScreen Game;
        private UINeighborhoodSelectionPanel Panel;
        private UIContainer Bar;
        private UIClassicHousePopup Popup;
        private ushort Mode;
        private ClickMode Clicking;
        private UIButton EvictButton;
        private UIButton RezoneButton;
        private int? Hovered;
        private bool HoveredEmpty; //nothing built there to bulldoze
        private bool RightWasDown;
        private bool EscWasDown;
        public static bool OpenFamilyBinOnStart; //coming back from Create-a-Family

        public UIClassicNeighbourhood(TS1GameScreen game, UINeighborhoodSelectionPanel panel, ushort mode)
        {
            Game = game;
            Panel = panel;
            Panel.ClassicClick = Click;
            Panel.OnHouseHover += Hover;
            Add(Bar = new UIContainer());
            Add(Popup = new UIClassicHousePopup());
            Bulldozer.Forget(); //back from a lot, which may have been bulldozed
            SimitoneCursors.SetClassic(true);
            SetMode(mode);
            if (OpenFamilyBinOnStart)
            {
                OpenFamilyBinOnStart = false;
                GameThread.NextUpdate((state) => OpenFamilyBin());
            }
        }

        private static Texture2D Tex(string name) => ClassicUIGraphics.Get(name);
        private static string Str(int table, int i) => GameFacade.Strings.GetString(table.ToString(), i.ToString());

        /// <summary>
        /// Shows one of the towns: 4 is the main neighbourhood, 2 Downtown, 3 Vacation Island, 5 Studio Town, 7 Magic Town.
        /// </summary>
        private void SetMode(ushort mode)
        {
            if (Mode != 0 && Mode != mode) Panel.PopulateScreen(mode);
            Mode = mode;
            Popup.Hide();
            Hovered = null;
            foreach (var child in new List<UIElement>(Bar.GetChildren())) Bar.Remove(child);

            var main = mode == 4;
            Bar.Add(new UIImage(Tex(main ? "NghUI/banner_neighborhood.bmp" : "NghUI/banner_downtown.bmp")));
            //the logo is the credits button
            var logo = new UIButton(Tex("NghUI/TheSimsLogo.bmp")) { Position = new Vector2((BarWidth - 70) / 2, 0), Tooltip = Str(151, 8), ImageStates = 1 };
            logo.OnButtonClick += (b) => UIScreen.GlobalShowDialog(new UIClassicCredits(), true);
            Bar.Add(logo);
            if (main)
            {
                //the player's other neighbourhoods, left of it
                var hoods = Neighbourhoods();
                var current = CurrentNeighbourhood();
                var index = hoods.IndexOf(current);
                var prev = Button("NghUI/Previous.bmp", 280, Str(151, 4), () => SwitchTo(hoods[index - 1]));
                var next = Button("NghUI/Next.bmp", 339, Str(151, 5), () => SwitchTo(hoods[index + 1]));
                prev.Disabled = index <= 0;
                next.Disabled = index == -1 || index >= hoods.Count - 1;
                Bar.Add(new UIImage(Tex("Nbhd/Current.bmp")) { Position = new Vector2(308, 3) });
                Bar.Add(new UILabel() { Caption = (current + 1).ToString(), CaptionStyle = NumberStyle(), Position = new Vector2(308, 3), Size = new Vector2(29, 46), Alignment = TextAlignment.Center | TextAlignment.Middle });
            }

            //left: the family bin (where families can live), eviction, and the way back from the other towns
            var x = 14;
            UIButton left(string name, string tooltip, Action click)
            {
                var b = Button(name, x, tooltip, click);
                x += (int)b.Size.X + 5;
                return b;
            }
            //the other towns name their buttons in their own tooltip tables
            var tips = (mode == 2) ? 169 : (mode == 3) ? 170 : (mode == 5) ? 173 : 174;
            if (!main) left("NghUI/Return.bmp", Str(tips, 0), () => SetMode(4));
            if (main || mode == 7) left("NghUI/MoveIn.bmp", main ? Str(151, 0) : Str(174, 5), OpenFamilyBin);
            EvictButton = left("NghUI/Bulldoze.bmp", main ? Str(151, 1) : Str(tips, 4), () => SetClicking((Clicking == ClickMode.Evict) ? ClickMode.Normal : ClickMode.Evict));
            //Unleashed's lots are in the main neighbourhood, which it can rezone
            RezoneButton = (main && Tex("NghUI/Rezone.bmp") != null)
                ? left("NghUI/Rezone.bmp", Str(151, 10), () => SetClicking((Clicking == ClickMode.Rezone) ? ClickMode.Normal : ClickMode.Rezone))
                : null;
            if (main) left("NghUI/Import.bmp", Str(151, 6), ImportFamilies);

            //right, from the far end: quit, the exported web pages, and the other towns
            x = BarWidth - 14;
            void right(string name, string tooltip, Action click, bool disabled = false)
            {
                var b = Button(name, 0, tooltip, click);
                x -= (int)b.Size.X;
                b.X = x;
                b.Disabled = disabled;
                x -= 2;
            }
            right("NghUI/Exit.bmp", Str(151, 2), () => Game.CloseAttempt());
            var web = Path.Combine(Content.Get().Neighborhood.UserPath, "Export", "index.html");
            right("NghUI/InetBtn.bmp", Str(151, 3), () => OpenPage(web), !File.Exists(web));
            if (main)
            {
                x -= 12;
                right("NghUI/MagicLand.bmp", Str(151, 12), () => SetMode(7));
                right("NghUI/Studiotown.bmp", Str(151, 11), () => SetMode(5));
                right("NghUI/Vacation.bmp", Str(151, 9), () => SetMode(3));
                right("NghUI/Downtown.bmp", Str(151, 7), () => SetMode(2));
            }
            var keep = (Clicking == ClickMode.MoveIn) ? (mode == 4 || mode == 7) : (Clicking != ClickMode.Rezone || RezoneButton != null);
            SetClicking(keep ? Clicking : ClickMode.Normal);
            GameResized();
        }

        private bool Switching;

        /// <summary>
        /// Loads another neighbourhood after this frame, so the new screen's buttons don't also take this click.
        /// </summary>
        private void SwitchTo(int id)
        {
            if (Switching) return;
            Switching = true;
            GameThread.NextUpdate((state) => Game.SwitchNeighbourhood(id, this));
        }

        private static TextStyle NumberStyle()
        {
            var style = ClassicFont.Style(14);
            style.Color = Color.White;
            return style;
        }

        private static string Folder(int id) => "UserData" + ((id == 0) ? "" : (id + 1).ToString());

        /// <summary>
        /// The neighbourhoods there are: UserData, then UserData2 to UserData8, where the neighbourhood provider copies
        /// them from (the install, or the Legacy Collection's saved games) or already copied to Simitone's.
        /// </summary>
        public static List<int> Neighbourhoods()
        {
            var source = Content.TS1SteamInstall
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games", "Electronic Arts", "The Sims 25")
                : Content.Get().TS1BasePath;
            var result = new List<int>();
            for (int id = 0; id < 8; id++)
            {
                if (Directory.Exists(Path.Combine(FSO.Common.FSOEnvironment.UserDir, Folder(id)))
                    || Directory.Exists(Path.Combine(source, Folder(id)))) result.Add(id);
            }
            return result;
        }

        private static int CurrentNeighbourhood()
        {
            var folder = Path.GetFileName(Content.Get().Neighborhood.UserPath.TrimEnd('/', '\\'));
            for (int id = 0; id < 8; id++) if (string.Equals(folder, Folder(id), StringComparison.OrdinalIgnoreCase)) return id;
            return 0;
        }

        private UIButton Button(string name, int x, string tooltip, Action click)
        {
            var b = new UIButton(Tex(name)) { Position = new Vector2(x, 0), Tooltip = tooltip };
            b.OnButtonClick += (e) => click();
            Bar.Add(b);
            return b;
        }

        private static void OpenPage(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception) { }
        }

        private void OpenFamilyBin()
        {
            SetClicking(ClickMode.Normal);
            UIScreen.GlobalShowDialog(new UIClassicFamilyBin(this), true);
        }

        /// <summary>
        /// The family bin's Move In: the cursor becomes the moving van until a house is picked.
        /// </summary>
        public void StartMoveIn(FAMI family)
        {
            Game.StartMoveIn(family.ChunkID);
            SetClicking(ClickMode.MoveIn);
        }

        private void SetClicking(ClickMode mode)
        {
            Clicking = mode;
            if (mode != ClickMode.MoveIn) Game.MoveInFamily = null;
            if (EvictButton != null) EvictButton.Selected = mode == ClickMode.Evict;
            if (RezoneButton != null) RezoneButton.Selected = mode == ClickMode.Rezone;
            if (Hovered != null) Hover(Hovered.Value, true);
        }

        private FAMI MovingFamily => (Game.MoveInFamily == null) ? null : Content.Get().Neighborhood.GetFamily((ushort)Game.MoveInFamily.Value);

        private void Hover(int house, bool on)
        {
            if (!on)
            {
                if (Hovered == house)
                {
                    Hovered = null;
                    Popup.Hide();
                }
                return;
            }
            Hovered = house;
            var neigh = Content.Get().Neighborhood;
            var family = neigh.GetFamilyForHouse((short)house);
            HoveredEmpty = family == null && Bulldozer.IsEmpty(house);
            var tool = Clicking == ClickMode.Evict || Clicking == ClickMode.Rezone;
            string text;
            if (family != null)
            {
                text = tool
                    ? GameFacade.Strings.GetString("134", "9", new string[] { HouseRollover.FamilyName(family), "§" + (family.ValueInArch + family.Budget).ToString("##,#0"), family.FamilyFriends.ToString() })
                    : HouseRollover.Status(house, MovingFamily, out _);
            }
            else
            {
                //the address and the lot's name, then what it costs or that it's a community lot
                var address = HouseRollover.Address(house);
                var name = neigh.GetHouseNameDesc(house).Item1;
                var heading = (name == "" || name == address) ? address : (address == "") ? name : address + "\r\n" + name;
                //with the bulldozer: that it can be demolished, or not the tutorial house, or that there's nothing
                //there; Rezone demolishes the same way, then rezones an empty lot
                var status = !tool ? HouseRollover.Status(house, MovingFamily, out _)
                    : !Bulldozer.Allowed(house) ? Str(134, 12)
                    : !HoveredEmpty ? Str(134, 10)
                    : (Clicking == ClickMode.Rezone) ? GameFacade.Strings.GetString("134", "19", new string[] { "", "" }).TrimStart()
                    : Str(134, 11);
                text = heading + "\r\n" + status;
            }
            Popup.Show(family, text.TrimEnd(), family == null && !HouseRollover.IsCommunity(house));
            PlacePopup();
        }

        /// <summary>
        /// Beside the house, inside the screen.
        /// </summary>
        private void PlacePopup()
        {
            if (Hovered == null || !Panel.HousePositions.TryGetValue(Hovered.Value, out var pos)) return;
            var screen = UIScreen.Current;
            var at = new Vector2(Panel.X, Panel.Y) + pos * Panel.ScaleX;
            var x = at.X + 40;
            if (x + Popup.Width > screen.ScreenWidth - 8) x = at.X - 40 - Popup.Width;
            var y = MathHelper.Clamp(at.Y - Popup.Height / 2, 60, screen.ScreenHeight - Popup.Height - 8);
            Popup.Position = new Vector2(Math.Max(8, x), y);
        }

        private void Click(int house)
        {
            var neigh = Content.Get().Neighborhood;
            var family = neigh.GetFamilyForHouse((short)house);
            switch (Clicking)
            {
                case ClickMode.Evict:
                    if (family == null)
                    {
                        if (!Bulldozer.Allowed(house))
                        {
                            Alert(Str(131, 12), Str(131, 13));
                            return;
                        }
                        if (Bulldozer.IsEmpty(house))
                        {
                            Alert(Str(131, 4), Str(131, 5));
                            return;
                        }
                        //a house, or a community lot
                        var community = HouseRollover.IsCommunity(house);
                        Confirm(Str(131, community ? 14 : 0), Str(131, community ? 15 : 1), () => Game.BulldozeHouse((short)house, this));
                        return;
                    }
                    Confirm(Str(131, 2), GameFacade.Strings.GetString("131", "3", new string[] { HouseRollover.FamilyName(family), "§" + (family.ValueInArch + family.Budget).ToString("##,#0") }), () =>
                    {
                        Game.EvictLot(family, (short)house);
                        SetClicking(ClickMode.Normal);
                    });
                    return;
                case ClickMode.Rezone:
                    //a lot is rezoned empty: the family is evicted, then the house bulldozed, each asked first
                    if (family != null)
                    {
                        Confirm(Str(131, 6), GameFacade.Strings.GetString("131", "7", new string[] { HouseRollover.FamilyName(family), "§" + (family.ValueInArch + family.Budget).ToString("##,#0") }), () =>
                        {
                            Game.EvictLot(family, (short)house);
                            SetClicking(ClickMode.Normal);
                        });
                    }
                    else if (!Bulldozer.Allowed(house)) Alert(Str(131, 12), Str(131, 13));
                    else if (!Bulldozer.IsEmpty(house)) Confirm(Str(131, 8), Str(131, 9), () => Game.BulldozeHouse((short)house, this));
                    else Rezone(house);
                    return;
                case ClickMode.MoveIn:
                    var moving = MovingFamily;
                    if (moving == null) return;
                    HouseRollover.Status(house, moving, out var valid);
                    if (!valid)
                    {
                        //why not, in the game's words: someone lives there, it's a community lot, or it costs too much
                        var why = (family != null) ? 14 : HouseRollover.IsCommunity(house) ? 12 : 2;
                        Alert(Str(132, why), Str(132, why + 1));
                        return;
                    }
                    Confirm(Str(132, 0), Str(132, 1), () => Game.MoveInAndPlay((short)house, moving.ChunkID, this));
                    return;
                default:
                    Game.PlayHouse((short)house, this);
                    return;
            }
        }

        /// <summary>
        /// The game's choice of residential or community for an empty lot.
        /// </summary>
        private void Rezone(int house)
        {
            var community = HouseRollover.IsCommunity(house);
            UIClassicAlert alert = null;
            void pick(bool toCommunity)
            {
                alert.Close();
                if (toCommunity != community) LotZoning.Set(house, toCommunity);
                SetClicking(ClickMode.Normal);
            }
            alert = new UIClassicAlert(new UIAlertOptions
            {
                Title = Str(250, 0),
                Message = GameFacade.Strings.GetString("250", "1", new string[] { Str(251, community ? 1 : 0) }),
                Buttons = new UIAlertButton[]
                {
                    new UIAlertButton(UIAlertButtonType.Yes, (b) => pick(false), Str(251, 0)),
                    new UIAlertButton(UIAlertButtonType.No, (b) => pick(true), Str(251, 1)),
                    new UIAlertButton(UIAlertButtonType.Cancel, (b) => alert.Close())
                }
            }, null, false);
            UIScreen.GlobalShowDialog(alert, true);
        }

        /// <summary>
        /// Import Family: each family file in the Import folder is offered in turn, in the game's words: where its house
        /// goes (or that it goes to the family bin) and who's in it.
        /// </summary>
        private void ImportFamilies()
        {
            SetClicking(ClickMode.Normal);
            var files = FamilyImport.Find();
            if (files.Count == 0)
            {
                Alert(Str(143, 8), "There are no families to import. Put a family file (.FAM) in the Import folder: " + FamilyImport.Folder());
                return;
            }
            var neigh = Content.Get().Neighborhood;
            var imported = false;
            void offer(int i)
            {
                if (i >= files.Count)
                {
                    if (imported)
                    {
                        //the new houses' thumbnails
                        Bulldozer.Forget();
                        Panel.PopulateScreen(Mode);
                    }
                    return;
                }
                var fam = files[i];
                if (fam.AlreadyHere())
                {
                    AlertThen(Str(143, 8), "The " + fam.Name + " family is already in this neighborhood, so it can't be imported again.", () => offer(i + 1));
                    return;
                }
                var lot = fam.Lot();
                var occupant = (lot == 0) ? null : neigh.GetFamilyForHouse((short)lot);
                string where;
                if (lot == 0) where = Str(143, 1);
                else if (occupant != null) where = Str(143, 2).Replace("$family", HouseRollover.FamilyName(occupant));
                else where = Str(143, Bulldozer.IsEmpty(lot) ? 3 : 4);
                var message = Str(143, 0).Replace("$family", fam.Name) + "\r\n\r\n" + where.Replace("$lot", lot.ToString())
                    + "\r\n\r\n" + Str(143, 5) + "\r\n" + string.Join(", ", fam.Members());
                UIClassicAlert alert = null;
                alert = new UIClassicAlert(new UIAlertOptions
                {
                    Title = Str(143, 8),
                    Message = message,
                    Buttons = new UIAlertButton[]
                    {
                        new UIAlertButton(UIAlertButtonType.Yes, (b) =>
                        {
                            alert.Close();
                            try
                            {
                                fam.Import(lot);
                                imported = true;
                                offer(i + 1);
                            }
                            catch (Exception e)
                            {
                                AlertThen(Str(143, 8), "The " + fam.Name + " family couldn't be imported: " + e.Message, () => offer(i + 1));
                            }
                        }, Str(143, 6)),
                        new UIAlertButton(UIAlertButtonType.No, (b) => { alert.Close(); offer(i + 1); }, Str(143, 7))
                    }
                }, null, false);
                UIScreen.GlobalShowDialog(alert, true);
            }
            offer(0);
        }

        private void Confirm(string title, string message, Action yes)
        {
            UIClassicAlert alert = null;
            alert = new UIClassicAlert(new UIAlertOptions
            {
                Title = title,
                Message = message,
                Buttons = UIAlertButton.YesNo((b) => { alert.Close(); yes(); }, (b) => alert.Close())
            }, null, false);
            UIScreen.GlobalShowDialog(alert, true);
        }

        private void Alert(string title, string message) => AlertThen(title, message, null);

        private void AlertThen(string title, string message, Action then)
        {
            UIClassicAlert alert = null;
            alert = new UIClassicAlert(new UIAlertOptions
            {
                Title = title,
                Message = message,
                Buttons = UIAlertButton.Ok((b) => { alert.Close(); then?.Invoke(); })
            }, null, false);
            UIScreen.GlobalShowDialog(alert, true);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            //a family sent to move in from elsewhere (the screen is told after it's made)
            if (Game.MoveInFamily != null && Clicking == ClickMode.Normal) SetClicking(ClickMode.MoveIn);
            //a right click or escape puts the van or bulldozer away
            var right = state.MouseState.RightButton == ButtonState.Pressed;
            var esc = state.KeyboardState.IsKeyDown(Keys.Escape);
            if (Clicking != ClickMode.Normal && ((right && !RightWasDown) || (esc && !EscWasDown))) SetClicking(ClickMode.Normal);
            RightWasDown = right;
            EscWasDown = esc;

            if (Hovered != null) PlacePopup();
            var cursor = CursorType.Normal;
            if (Clicking != ClickMode.Normal && state.MouseState.Y > Bar.Y + 52)
            {
                var can = true;
                if (Hovered != null)
                {
                    var occupied = Content.Get().Neighborhood.GetFamilyForHouse((short)Hovered.Value) != null;
                    if (Clicking == ClickMode.Evict) can = occupied || (Bulldozer.Allowed(Hovered.Value) && !HoveredEmpty);
                    else if (Clicking == ClickMode.Rezone) can = occupied || Bulldozer.Allowed(Hovered.Value);
                    else HouseRollover.Status(Hovered.Value, MovingFamily, out can);
                }
                cursor = !can ? CursorType.NoDrop : (Clicking == ClickMode.Evict) ? CursorType.Bulldoze : (Clicking == ClickMode.Rezone) ? CursorType.Rezone : CursorType.MoveIn;
            }
            GameFacade.Cursor.SetCursor(cursor);
        }

        public override void GameResized()
        {
            base.GameResized();
            Bar.Position = new Vector2((UIScreen.Current.ScreenWidth - BarWidth) / 2, 0);
            PlacePopup();
        }

        public override void Removed()
        {
            base.Removed();
            Panel.OnHouseHover -= Hover;
            GameFacade.Cursor.SetCursor(CursorType.Normal);
        }
    }

    /// <summary>
    /// The popup about a house: the faces of the family living there, or for an empty house the game's "no one lives
    /// here" picture, over the text, on the game's translucent navy (HotspotPopupTiler.TGA, stretched to fit).
    /// </summary>
    public class UIClassicHousePopup : UIContainer
    {
        private const int Margin = 14;
        private const int HeadSize = 36;
        private UIImage Background;
        private UILabel Text;
        private List<(string Name, Texture2D Head)> Heads = new List<(string, Texture2D)>();
        private Texture2D NoOne;
        private Texture2D Frame;
        public float Width { get; private set; }
        public float Height { get; private set; }

        public UIClassicHousePopup()
        {
            Add(Background = new UIImage(ClassicUIGraphics.Get("Nbhd/HotspotPopupTiler.TGA")).With9Slice(24, 24, 24, 24));
            var style = ClassicFont.Style(11);
            style.Color = new Color(200, 206, 214);
            Add(Text = new UILabel() { CaptionStyle = style, Wrapped = true, Alignment = TextAlignment.Left | TextAlignment.Top });
            NoOne = ClassicUIGraphics.Get("Nbhd/MustPickFam.BMP");
            Frame = ClassicUIGraphics.Get("Nbhd/PersonInfoBkg.bmp");
            Visible = false;
        }

        private bool Empty;

        public void Show(FAMI family, string text, bool empty)
        {
            Heads = (family == null) ? new List<(string, Texture2D)>() : FamilyHeads.Of(family);
            Empty = empty && NoOne != null;
            var top = Margin;
            var width = 220;
            if (Heads.Count > 0)
            {
                width = Math.Max(width, Heads.Count * (HeadSize + 4) + Margin * 2);
                top += HeadSize + 8;
            }
            else if (Empty) top += NoOne.Height + 8;
            Text.Caption = text;
            Text.Position = new Vector2(Margin, top);
            Text.Size = new Vector2(width - Margin * 2, 1000);
            var lines = TextRenderer.ComputeText(text, new TextRendererOptions
            {
                Alignment = TextAlignment.Left | TextAlignment.Top,
                MaxWidth = width - Margin * 2,
                Position = Vector2.Zero,
                Scale = Vector2.One,
                TextStyle = Text.CaptionStyle,
                WordWrap = true
            }, this);
            Width = width;
            Height = top + lines.BoundingBox.Height + Margin;
            Background.SetSize(Width, Height);
            Visible = true;
        }

        public void Hide() => Visible = false;

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            base.Draw(batch);
            if (Empty) DrawLocalTexture(batch, NoOne, new Vector2((Width - NoOne.Width) / 2, Margin));
            var x = Margin;
            foreach (var (name, head) in Heads)
            {
                if (Frame != null) DrawLocalTexture(batch, Frame, new Vector2(x, Margin));
                if (head != null)
                {
                    //the face, cropped like the portraits, inside the frame
                    var src = new Rectangle(head.Width / 8, head.Height / 10, head.Width * 3 / 4, head.Height * 3 / 4);
                    var scale = (HeadSize - 4f) / src.Width;
                    DrawLocalTexture(batch, head, src, new Vector2(x + 2, Margin + 2), new Vector2(scale));
                }
                x += HeadSize + 4;
            }
        }
    }
}
