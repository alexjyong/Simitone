using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework;
using FSO.Common.Rendering.Framework.Camera;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.Content;
using FSO.SimAntics.Model;
using FSO.Vitaboy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Panels.CAS;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The game's Create-a-Family and Create-a-Sim screens, over Simitone's Create-a-Sim (TS1CASScreen), which still
    /// keeps the family being made and turns a Sim's choices into heads and bodies. Its own panels are kept as the data
    /// these edit: names, bio, personality, age, sex and skin. The buttons sit where the screens' backgrounds draw them.
    /// </summary>
    public class UIClassicCAS : UIContainer
    {
        private const int Width = 800;
        private const int Height = 600;
        private static readonly Rectangle MembersArea = new Rectangle(78, 106, 504, 300);
        private static readonly Color TextColor = new Color(200, 206, 214);

        private TS1CASScreen Screen;
        private Texture2D Px;
        private UIContainer FamilyPage;
        private UIContainer SimPage;

        //Create-a-Family
        private UITextBox LastName;
        private List<UIClassicSimView> Members = new List<UIClassicSimView>();
        private List<UILabel> MemberNames = new List<UILabel>();
        private int Selected = -1;
        private UIButton AddButton;
        private UIButton DeleteButton;
        private UIButton EditButton;
        private UIButton FamilyDone;

        //Create-a-Sim
        private UITextBox FirstName;
        private UITextEdit Bio;
        private UIClassicPersonality Personality;
        private UIClassicSimView Preview;
        private UIButton AdultButton, ChildButton, MaleButton, FemaleButton, LightButton, MediumButton, DarkButton;
        private UIButton SimDone;
        private string PreviewKey;

        private static string Str(int table, int i) => GameFacade.Strings.GetString(table.ToString(), i.ToString());
        private static Texture2D Tex(string name) => ClassicUIGraphics.Get("Nbhd/" + name);

        public UIClassicCAS(TS1CASScreen screen)
        {
            Screen = screen;
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            Add(FamilyPage = new UIContainer());
            Add(SimPage = new UIContainer() { Visible = false });
            BuildFamilyPage();
            BuildSimPage();
            GameResized();
        }

        private static TextStyle Style(int size, Color? color = null)
        {
            var style = ClassicFont.Style(size);
            style.Color = color ?? Color.White;
            return style;
        }

        private static UIButton Button(UIContainer page, string name, int x, int y, string tooltip, Action click, int states = 4)
        {
            var b = new UIButton(Tex(name)) { Position = new Vector2(x, y), Tooltip = tooltip, ImageStates = states };
            b.OnButtonClick += (e) => click();
            page.Add(b);
            return b;
        }

        /// <summary>
        /// A text field inside one of the backgrounds' sunken bars.
        /// </summary>
        private static UITextBox NameBox(UIContainer page)
        {
            var box = new UITextBox();
            var field = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
            field.SetData(new[] { Color.Transparent });
            box.SetBackgroundTexture(field, 0, 0, 0, 0);
            box.TextMargin = new Rectangle(8, 4, 8, 4);
            box.Alignment = TextAlignment.Center;
            box.TextStyle = Style(14);
            box.MaxChars = 24;
            box.Position = new Vector2(266, 48);
            box.SetSize(270, 30);
            page.Add(box);
            return box;
        }

        private static void Title(UIContainer page, string caption, string prompt)
        {
            page.Add(new UILabel() { Caption = caption, CaptionStyle = Style(14), Position = new Vector2(30, 14), Size = new Vector2(400, 24), Alignment = TextAlignment.Left | TextAlignment.Middle });
            page.Add(new UILabel() { Caption = prompt, CaptionStyle = Style(11, TextColor), Position = new Vector2(30, 48), Size = new Vector2(224, 30), Alignment = TextAlignment.Right | TextAlignment.Middle });
        }

        private void BuildFamilyPage()
        {
            FamilyPage.Add(new UIImage(Tex("DsgnFamBkg.bmp")));
            Title(FamilyPage, Str(129, 9), Str(129, 12));
            LastName = NameBox(FamilyPage);

            AddButton = Button(FamilyPage, "AddPersonBtn.bmp", 593, 104, Str(129, 0), () => Screen.ModifySim(false, -1));
            DeleteButton = Button(FamilyPage, "DeletePersonBtn.bmp", 594, 208, Str(129, 1), DeleteMember);
            EditButton = Button(FamilyPage, "EditPersonBtn.bmp", 596, 313, Str(129, 2), () => { if (Selected != -1) Screen.ModifySim(false, Selected); });
            Button(FamilyPage, "DesignFamCancel.bmp", 470, 532, Str(129, 11), CancelFamily, 6);
            FamilyDone = Button(FamilyPage, "DesignFamDone.bmp", 650, 532, Str(129, 10), FinishFamily, 6);

            //clicking a Sim in the family selects it, for Delete and Edit
            var picker = new UIClassicClickArea(MembersArea);
            picker.OnClick += (pos) =>
            {
                for (int i = 0; i < Members.Count; i++)
                {
                    if (new Rectangle(Members[i].Position.ToPoint(), Members[i].ViewSize).Contains(pos)) Select(i);
                }
            };
            FamilyPage.Add(picker);
        }

        private void BuildSimPage()
        {
            SimPage.Add(new UIImage(Tex("CreateACharBack.bmp")));
            Title(SimPage, Str(130, 15), Str(130, 26));
            FirstName = NameBox(SimPage);

            SimPage.Add(new UILabel() { Caption = Str(130, 16), CaptionStyle = Style(11), Position = new Vector2(92, 100), Size = new Vector2(200, 18), Alignment = TextAlignment.Left | TextAlignment.Middle });
            SimPage.Add(Personality = new UIClassicPersonality(Screen.CASPanel));

            //the semicircle: age on top, skin in the middle, sex below
            AdultButton = Button(SimPage, "DesignCharAdultBtn.bmp", 499, 104, Str(130, 0), () => SetType(1, 'a'));
            ChildButton = Button(SimPage, "DesignCharChildBtn.bmp", 406, 114, Str(130, 1), () => SetType(1, 'c'));
            LightButton = Button(SimPage, "DesignCharLightBtn.bmp", 399, 201, Str(130, 6), () => SetSkin("lgt"));
            MediumButton = Button(SimPage, "DesignCharMediumBtn.bmp", 453, 200, Str(130, 5), () => SetSkin("med"));
            DarkButton = Button(SimPage, "DesignCharDarkBtn.bmp", 500, 200, Str(130, 4), () => SetSkin("drk"));
            MaleButton = Button(SimPage, "DesignCharMaleBtn.bmp", 401, 272, Str(130, 3), () => SetType(0, 'm'));
            FemaleButton = Button(SimPage, "DesignCharFemaleBtn.bmp", 499, 273, Str(130, 2), () => SetType(0, 'f'));

            //the Sim, with arrows beside its head and its body to try the others
            SimPage.Add(Preview = new UIClassicSimView(new Point(176, 258)) { Position = new Vector2(576, 120) });
            Button(SimPage, "DesignCharSkinsLeftBtn.bmp", 580, 150, Str(130, 9), () => Step(true, -1));
            Button(SimPage, "DesignCharSkinsRightBtn.bmp", 734, 150, Str(130, 10), () => Step(true, 1));
            Button(SimPage, "DesignCharSkinsLeftBtn.bmp", 580, 290, Str(130, 7), () => Step(false, -1));
            Button(SimPage, "DesignCharSkinsRightBtn.bmp", 734, 290, Str(130, 8), () => Step(false, 1));

            SimPage.Add(new UILabel() { Caption = Str(130, 24), CaptionStyle = Style(11), Position = new Vector2(28, 398), Size = new Vector2(200, 18), Alignment = TextAlignment.Left | TextAlignment.Middle });
            Bio = new UITextEdit();
            Bio.Position = new Vector2(28, 428);
            Bio.SetSize(744, 84);
            Bio.TextStyle = Style(11, TextColor);
            Bio.MaxLines = 5;
            SimPage.Add(Bio);

            Button(SimPage, "DesignCharCancelBtn.bmp", 490, 546, Str(130, 12), () => Screen.SetMode(UICASMode.FamilyEdit), 6);
            SimDone = Button(SimPage, "DesignCharDoneBtn.bmp", 650, 546, Str(130, 11), FinishSim, 6);
        }

        /// <summary>
        /// Called by the screen when it changes between the family and one of its Sims.
        /// </summary>
        public void ShowMode(UICASMode mode)
        {
            var sim = mode == UICASMode.SimEdit;
            FamilyPage.Visible = !sim;
            SimPage.Visible = sim;
            if (sim)
            {
                //the screen has just loaded the Sim (or a new one) into its panels
                var panel = Screen.CASPanel;
                FirstName.CurrentText = panel.FirstNameTextBox.CurrentText;
                Bio.CurrentText = panel.BioEdit.CurrentText;
                Personality.Refresh();
                PreviewKey = null;
            }
            else RefreshFamily();
        }

        private void RefreshFamily()
        {
            foreach (var view in Members) FamilyPage.Remove(view);
            foreach (var label in MemberNames) FamilyPage.Remove(label);
            Members.Clear();
            MemberNames.Clear();
            var family = Screen.WIPFamily;
            if (family.Count > 0)
            {
                //side by side, as wide as the area allows
                var slot = Math.Min(124, MembersArea.Width / family.Count);
                var x = MembersArea.X + (MembersArea.Width - slot * family.Count) / 2;
                for (int i = 0; i < family.Count; i++)
                {
                    var view = new UIClassicSimView(new Point(slot - 4, 250)) { Position = new Vector2(x + i * slot + 2, MembersArea.Y + 4) };
                    view.Show(family[i]);
                    Members.Add(view);
                    FamilyPage.Add(view);
                    var label = new UILabel() { Caption = family[i].Name, CaptionStyle = Style(10, TextColor), Position = new Vector2(x + i * slot, MembersArea.Bottom - 40), Size = new Vector2(slot, 18), Alignment = TextAlignment.Center | TextAlignment.Middle };
                    MemberNames.Add(label);
                    FamilyPage.Add(label);
                }
            }
            Select(Math.Min(Selected, family.Count - 1));
        }

        private void Select(int index)
        {
            Selected = (index >= 0 && index < Screen.WIPFamily.Count) ? index : -1;
        }

        private void DeleteMember()
        {
            if (Selected == -1) return;
            Confirm(Str(129, 5), Str(129, 6), () =>
            {
                Screen.ModifySim(true, Selected);
                Selected = -1;
                RefreshFamily();
            });
        }

        private void CancelFamily()
        {
            Confirm(Str(129, 7), Str(129, 8), () =>
            {
                Screen.ClearFamily();
                Screen.SetMode(UICASMode.ToNeighborhood);
            });
        }

        private void FinishFamily()
        {
            Screen.FamilyPanel.SecondName.CurrentText = LastName.CurrentText.Trim();
            Confirm(Str(129, 13), Str(129, 14), () =>
            {
                Screen.SaveFamily();
                Screen.SetMode(UICASMode.ToNeighborhood);
            });
        }

        private void FinishSim()
        {
            var panel = Screen.CASPanel;
            panel.FirstNameTextBox.CurrentText = FirstName.CurrentText.Trim();
            panel.BioEdit.CurrentText = Bio.CurrentText;
            panel.UpdateTotalPoints();
            void done()
            {
                Screen.AcceptMember();
                Screen.SetMode(UICASMode.FamilyEdit);
            }
            //the game asks first when there are personality points left over
            if (panel.TotalPoints < panel.AllowedPoints) Confirm(Str(130, 13), Str(130, 14), done);
            else done();
        }

        private void SetType(int index, char value)
        {
            var panel = Screen.CASPanel;
            var type = panel.AType.ToCharArray();
            type[index] = value;
            panel.AType = new string(type);
            panel.UpdateType();
        }

        private void SetSkin(string skin)
        {
            Screen.CASPanel.SkinType = skin;
            Screen.CASPanel.UpdateType();
        }

        private void Step(bool head, int direction)
        {
            if (head) Screen.HeadPosition = (float)Math.Round(Screen.HeadPosition) + direction;
            else Screen.BodyPosition = (float)Math.Round(Screen.BodyPosition) + direction;
            FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
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

        public override void Update(UpdateState state)
        {
            base.Update(state);
            var panel = Screen.CASPanel;
            if (SimPage.Visible)
            {
                AdultButton.Selected = panel.AType[1] == 'a';
                ChildButton.Selected = panel.AType[1] == 'c';
                MaleButton.Selected = panel.AType[0] == 'm';
                FemaleButton.Selected = panel.AType[0] == 'f';
                LightButton.Selected = panel.SkinType == "lgt";
                MediumButton.Selected = panel.SkinType == "med";
                DarkButton.Selected = panel.SkinType == "drk";
                SimDone.Disabled = FirstName.CurrentText.Trim().Length == 0;

                //show the Sim as chosen so far; the heads and bodies are ready once the screen's lot has loaded
                if (Screen.ActiveHeads != null && Screen.ActiveBodies != null && Screen.ActiveHeads.Count > 0 && Screen.ActiveBodies.Count > 0)
                {
                    var member = Screen.BuildMember();
                    var key = member.Head + member.HeadTex + member.Body + member.BodyTex + member.Gender;
                    if (key != PreviewKey)
                    {
                        PreviewKey = key;
                        Preview.Show(member);
                    }
                }
            }
            else
            {
                var count = Screen.WIPFamily.Count;
                AddButton.Disabled = count >= 8;
                DeleteButton.Disabled = EditButton.Disabled = Selected == -1;
                FamilyDone.Disabled = count == 0 || LastName.CurrentText.Trim().Length == 0;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            //the screens are 800x600: black around them on a bigger window
            var screen = UIScreen.Current;
            DrawLocalTexture(batch, Px, null, -Position, new Vector2(screen.ScreenWidth, screen.ScreenHeight), Color.Black);
            base.Draw(batch);
            if (FamilyPage.Visible && Selected != -1 && Selected < Members.Count)
            {
                //the selected Sim's outline, in the game's highlight
                var r = new Rectangle(Members[Selected].Position.ToPoint(), Members[Selected].ViewSize);
                var c = new Color(0, 255, 255);
                DrawLocalTexture(batch, Px, null, new Vector2(r.X, r.Y), new Vector2(r.Width, 2), c);
                DrawLocalTexture(batch, Px, null, new Vector2(r.X, r.Bottom - 2), new Vector2(r.Width, 2), c);
                DrawLocalTexture(batch, Px, null, new Vector2(r.X, r.Y), new Vector2(2, r.Height), c);
                DrawLocalTexture(batch, Px, null, new Vector2(r.Right - 2, r.Y), new Vector2(2, r.Height), c);
            }
        }

        public override void GameResized()
        {
            base.GameResized();
            var screen = UIScreen.Current;
            Position = new Vector2((screen.ScreenWidth - Width) / 2, Math.Max(0, (screen.ScreenHeight - Height) / 2));
        }
    }

    /// <summary>
    /// The personality panel: each trait's name and its ten lights (DesignCharPersLED.bmp over the background's unlit
    /// ones), clicked to set; the star sign closest to the personality, which is clicked to try the next sign's own
    /// personality and explains itself when the mouse is over it; and the points left to give out (CACPointRemainingBars.bmp). The points are kept in Simitone's own
    /// panel.
    /// </summary>
    public class UIClassicPersonality : UIContainer
    {
        private const int LedX = 228;
        private const int LedPitch = 6; //each light and the gap after it
        private static readonly int[] Rows = { 131, 163, 195, 227, 259 };
        private static readonly Rectangle SignArea = new Rectangle(96, 288, 190, 26);
        private UISimCASPanel Panel;
        private Texture2D Lit;
        private Texture2D Remaining;
        private TextStyle LabelStyle;
        private UIClassicTip SignTip;

        public UIClassicPersonality(UISimCASPanel panel)
        {
            Panel = panel;
            Lit = ClassicUIGraphics.Get("Nbhd/DesignCharPersLED.bmp");
            Remaining = ClassicUIGraphics.Get("Nbhd/CACPointRemainingBars.bmp");
            LabelStyle = ClassicFont.Style(11);
            LabelStyle.Color = Color.White;
            ListenForMouse(new Rectangle(LedX - 4, Rows[0], 70, Rows[4] + 25 - Rows[0]), (type, state) =>
            {
                if (type != UIMouseEventType.MouseDown) return;
                var pos = GlobalPoint(new Vector2(state.MouseState.X, state.MouseState.Y));
                var row = Array.FindIndex(Rows, y => pos.Y >= y && pos.Y < y + 25);
                if (row == -1) return;
                var points = (int)Math.Max(0, Math.Min(10, Math.Ceiling((pos.X - LedX) / LedPitch)));
                //clicking the last lit light turns it off
                if (points == Panel.Personalities[row].Points) points--;
                Panel.UpdateTotalPoints();
                var left = Panel.AllowedPoints - (Panel.TotalPoints - Panel.Personalities[row].Points);
                Panel.Personalities[row].Points = Math.Max(0, Math.Min(points, left));
                Panel.UpdateTotalPoints();
                FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
            });
            //the sign explains itself (its own element, as a tooltip shows at the top of its element), and clicking it
            //gives the Sim the next sign's own personality
            Add(SignTip = new UIClassicTip(null, Rectangle.Empty, new Rectangle(0, 0, SignArea.Width, SignArea.Height), null) { Position = SignArea.Location.ToVector2() });
            SignTip.OnClick = () =>
            {
                var next = ZodiacSign.Personalities[(Sign() + 1) % 12];
                for (int i = 0; i < 5; i++) Panel.Personalities[i].Points = next[i];
                Panel.UpdateTotalPoints();
                FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
            };
        }

        public void Refresh() => Panel.UpdateTotalPoints();

        /// <summary>
        /// The star sign closest to the points given so far (the traits are in the game's order: neat, outgoing, active,
        /// playful, nice).
        /// </summary>
        private int Sign()
        {
            var p = Panel.Personalities;
            return ZodiacSign.Of(p[0].Points, p[1].Points, p[2].Points, p[3].Points, p[4].Points);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            for (int i = 0; i < 5; i++)
            {
                DrawLocalString(batch, GameFacade.Strings.GetString("130", (17 + i).ToString()), Vector2.Zero, LabelStyle, new Rectangle(96, Rows[i], 124, 25), TextAlignment.Left | TextAlignment.Middle);
                var points = Panel.Personalities[i].Points;
                if (Lit != null && points > 0) DrawLocalTexture(batch, Lit, new Rectangle(0, 0, Math.Min(Lit.Width, points * LedPitch), Lit.Height), new Vector2(LedX, Rows[i]));
            }
            var sign = Sign();
            DrawLocalString(batch, ZodiacSign.Name(sign), Vector2.Zero, LabelStyle, SignArea, TextAlignment.Center | TextAlignment.Middle);
            SignTip.Tooltip = ZodiacSign.Tooltip(sign);
            var left = Panel.AllowedPoints - Panel.TotalPoints;
            if (Remaining != null && left > 0) DrawLocalTexture(batch, Remaining, new Rectangle(0, 0, Math.Min(Remaining.Width, left * LedPitch), Remaining.Height), new Vector2(117, 342));
        }
    }

    /// <summary>
    /// A Sim drawn on its own, standing and slowly turning: Create-a-Sim's preview, and each Sim in Create-a-Family.
    /// </summary>
    public class UIClassicSimView : UIElement
    {
        public Point ViewSize;
        private _3DTargetScene Scene;
        private BasicCamera Camera;
        private SimAvatar Avatar;
        private Animation Standing;
        private float Frame;
        private float Turn;

        public UIClassicSimView(Point size)
        {
            ViewSize = size;
        }

        public void Show(CASFamilyMember member)
        {
            var gd = GameFacade.GraphicsDevice;
            var child = member.Gender > 1;
            if (Scene == null)
            {
                Camera = new BasicCamera(gd, new Vector3(0, 0, 10), Vector3.Zero, Vector3.Up) { FOV = (float)Math.PI / 4 };
                Scene = new _3DTargetScene(gd, Camera, ViewSize, (GlobalSettings.Default.AntiAlias > 0) ? 8 : 0);
                Scene.ID = "UIClassicSimView";
                Scene.Initialize(GameFacade.Scenes);
            }
            if (Avatar != null) Scene.Remove(Avatar);

            var skeleton = Content.Get().AvatarSkeletons.Get(child ? "child.skel" : "adult.skel").Clone();
            Avatar = new SimAvatar(skeleton);
            var outfits = TS1CASScreen.Outfits(member);
            Avatar.Body = new VMOutfitReference(outfits.Body).GetContent();
            Avatar.Handgroup = Avatar.Body;
            Avatar.Head = new VMOutfitReference(outfits.Head).GetContent();
            Avatar.Scale = Vector3.One;
            Standing = Content.Get().AvatarAnimations.Get((child ? "c2o" : "a2o") + "-standing-loop.anim")
                ?? Content.Get().AvatarAnimations.Get("a2o-standing-loop.anim");
            Pose();

            //frame the whole Sim, from its feet to the top of its head
            var top = Avatar.Skeleton.GetBone("HEAD")?.AbsolutePosition.Y + 0.6f ?? 3.5f;
            var middle = top / 2;
            var distance = (top / 2 * 1.15f) / (float)Math.Tan(Camera.FOV / 2);
            Camera.Target = new Vector3(0, middle, 0);
            Camera.Position = new Vector3(0, middle, distance);
            Scene.Add(Avatar);
        }

        private void Pose()
        {
            if (Standing != null) Animator.RenderFrame(Avatar, Standing, (int)Frame, Frame % 1, 1f);
            Avatar.ReloadSkeleton();
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (Avatar == null || !Visible) return;
            var step = 30f / FSO.Common.FSOEnvironment.RefreshRate; //the animations run at 30 frames a second
            Frame += step;
            if (Standing != null && Frame >= Standing.NumFrames) Frame = 0;
            Turn += step / 30f;
            Avatar.RotationY = (float)Math.Sin(Turn * 0.5) * 0.7f;
            Pose();
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            base.PreDraw(batch);
            if (!Visible || Scene == null || UISpriteBatch.Invalidated || _3DScene.IsInvalidated) return;
            batch.Pause();
            Scene.Draw(GameFacade.GraphicsDevice);
            batch.Resume();
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || Scene?.Target == null) return;
            DrawLocalTexture(batch, Scene.Target, Vector2.Zero);
        }

        public override void Removed()
        {
            base.Removed();
            Scene?.Dispose();
            Scene = null;
        }
    }

    /// <summary>
    /// An invisible clickable area, reporting where it was clicked (in its parent's space).
    /// </summary>
    public class UIClassicClickArea : UIElement
    {
        public event Action<Point> OnClick;

        public UIClassicClickArea(Rectangle area)
        {
            Position = new Vector2(area.X, area.Y);
            ListenForMouse(new Rectangle(0, 0, area.Width, area.Height), (type, state) =>
            {
                if (type != UIMouseEventType.MouseDown) return;
                var local = GlobalPoint(new Vector2(state.MouseState.X, state.MouseState.Y));
                OnClick?.Invoke(new Point((int)(local.X + Position.X), (int)(local.Y + Position.Y)));
            });
        }

        public override void Draw(UISpriteBatch batch) { }
    }
}
