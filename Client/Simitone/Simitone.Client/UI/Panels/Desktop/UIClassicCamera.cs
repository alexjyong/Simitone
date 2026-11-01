using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using FSO.HIT;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic camera mode's strip, in place of the live mode strip: the snapshot sizes, the quality, and the photo
    /// album. The positions, names and explanations are the game's (string table 140). While it's open, the frame
    /// follows the mouse over the lot, and a click takes the photo (UIClassicCameraFrame).
    /// </summary>
    public class UIClassicCameraPanel : UIClassicStrip
    {
        //the frame sizes, small to large, and custom (which starts at medium); Ctrl and the arrow keys change custom
        public static readonly Point[] Sizes = { new Point(128, 96), new Point(256, 192), new Point(512, 384) };
        public static int SizeChoice = 1;
        public static Point CustomSize = new Point(256, 192);
        public static int Quality = 2; //low, medium, high
        private List<UIButton> SizeButtons = new List<UIButton>();
        private List<UIButton> QualityButtons = new List<UIButton>();
        private UIClassicInfoPopup Popup;

        public UIClassicCameraPanel(TS1GameScreen game) : base(game)
        {
            string str(int i) => GameFacade.Strings.GetString("140", i.ToString());
            Popup = new UIClassicInfoPopup(-4) { X = 8 };
            UIButton btn(string art, int place, Texture2D picture, int about)
            {
                //each place is a "(x;y)" string before the name
                var xy = str(place).Trim('(', ')').Split(';');
                var b = new UIButton(Tex("Buttons/" + art)) { Position = new Vector2(int.Parse(xy[0]), int.Parse(xy[1])), Tooltip = str(place + 1) };
                if (picture != null)
                {
                    b.OnButtonHover += (e) => Popup.Show(picture, str(about), str(about + 1));
                    b.OnButtonExit += (e) => Popup.Hide();
                }
                Add(b);
                return b;
            }
            var sizes = Tex("Backgrounds/CamPopupDetail.bmp");
            var quality = Tex("Backgrounds/CamPopupQualDetail.bmp");
            SizeButtons.Add(btn("CamFrameSmall.bmp", 0, sizes, 16));
            SizeButtons.Add(btn("CamFrameMed.bmp", 2, sizes, 16));
            SizeButtons.Add(btn("CamFrameLarge.bmp", 4, sizes, 16));
            SizeButtons.Add(btn("CamFrameCust.bmp", 6, sizes, 16));
            for (int i = 0; i < SizeButtons.Count; i++)
            {
                var size = i;
                SizeButtons[i].OnButtonClick += (b) => { SizeChoice = size; UpdateSelection(); };
            }
            QualityButtons.Add(btn("CamQualLow.bmp", 10, quality, 18));
            QualityButtons.Add(btn("CamQualMed.bmp", 12, quality, 18));
            QualityButtons.Add(btn("CamQualHigh.bmp", 14, quality, 18));
            for (int i = 0; i < QualityButtons.Count; i++)
            {
                var level = i;
                QualityButtons[i].OnButtonClick += (b) => { Quality = level; UpdateSelection(); };
            }
            var album = btn("CamViewAlbum.bmp", 8, null, 0);
            album.OnButtonClick += (b) => UIScreen.GlobalShowDialog(new UIClassicPhotoAlbum(game), true);
            Add(new UIImage(Tex("Backgrounds/CameraToothpkLeft.bmp")) { Position = new Vector2(144, 6) });
            Add(new UIImage(Tex("Backgrounds/CameraToothpkRight.bmp")) { Position = new Vector2(216, 6) });
            UpdateSelection();
            Add(Popup);
            GameResized();
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < SizeButtons.Count; i++) SizeButtons[i].Selected = i == SizeChoice;
            for (int i = 0; i < QualityButtons.Count; i++) QualityButtons[i].Selected = i == Quality;
        }

        /// <summary>
        /// The frame's size now, in screen pixels.
        /// </summary>
        public static Point FrameSize => (SizeChoice < Sizes.Length) ? Sizes[SizeChoice] : CustomSize;

        public override void Update(UpdateState state)
        {
            base.Update(state);
            //Ctrl and the arrow keys size the custom frame
            if (SizeChoice != 3 || !(state.KeyboardState.IsKeyDown(Keys.LeftControl) || state.KeyboardState.IsKeyDown(Keys.RightControl))) return;
            var step = new Point(0, 0);
            if (state.NewKeys.Contains(Keys.Left)) step.X -= 16;
            if (state.NewKeys.Contains(Keys.Right)) step.X += 16;
            if (state.NewKeys.Contains(Keys.Up)) step.Y -= 16;
            if (state.NewKeys.Contains(Keys.Down)) step.Y += 16;
            CustomSize = new Point(Math.Max(32, Math.Min(UIScreen.Current.ScreenWidth, CustomSize.X + step.X)), Math.Max(32, Math.Min(UIScreen.Current.ScreenHeight, CustomSize.Y + step.Y)));
        }
    }

    /// <summary>
    /// The camera's frame over the lot while camera mode is open: it follows the mouse, and a click photographs what's
    /// inside it (the lot, without the interface) into the family's photo album. It covers the lot, so clicks don't
    /// reach it while the camera is out.
    /// </summary>
    public class UIClassicCameraFrame : UIElement
    {
        private TS1GameScreen Game;
        private Point Mouse;
        private bool Over;
        private float Flash;

        public UIClassicCameraFrame(TS1GameScreen game)
        {
            Game = game;
            ListenForMouse(new Rectangle(0, 0, 10000, 10000), (type, state) =>
            {
                switch (type)
                {
                    case UIMouseEventType.MouseOver: Over = true; break;
                    case UIMouseEventType.MouseOut: Over = false; break;
                    case UIMouseEventType.MouseUp: TakePhoto(); break;
                }
            });
        }

        private Rectangle Frame
        {
            get
            {
                var size = UIClassicCameraPanel.FrameSize;
                return new Rectangle(Mouse.X - size.X / 2, Mouse.Y - size.Y / 2, size.X, size.Y);
            }
        }

        private void TakePhoto()
        {
            //low quality is a smaller picture; medium and low are JPEG, high is PNG
            var quality = UIClassicCameraPanel.Quality;
            SpecialEffects.Layer.TakePhoto(Frame, (quality == 0) ? 0.75f : 1f, quality < 2);
            HITVM.Get().PlaySoundEvent(UISounds.CameraPhoto);
            Flash = 1;
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            Mouse = new Point(state.MouseState.X, state.MouseState.Y);
            Flash = Math.Max(0, Flash - 1 / 15f);
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible || !Over) return;
            var frame = Frame;
            var px = TextureGenerator.GetPxWhite(batch.GraphicsDevice);
            var color = Color.White;
            if (Flash > 0) DrawLocalTexture(batch, px, null, new Vector2(frame.X, frame.Y), new Vector2(frame.Width, frame.Height), Color.White * Flash * 0.6f);
            DrawLocalTexture(batch, px, null, new Vector2(frame.X, frame.Y), new Vector2(frame.Width, 2), color);
            DrawLocalTexture(batch, px, null, new Vector2(frame.X, frame.Bottom - 2), new Vector2(frame.Width, 2), color);
            DrawLocalTexture(batch, px, null, new Vector2(frame.X, frame.Y), new Vector2(2, frame.Height), color);
            DrawLocalTexture(batch, px, null, new Vector2(frame.Right - 2, frame.Y), new Vector2(2, frame.Height), color);
        }
    }

    /// <summary>
    /// The classic photo album (the game's scrapbook): the family's photos one at a time, with their names, which can
    /// be edited (a photo's name is its file name), first, previous, next and last, delete, and done. The layout and
    /// text are the game's (string table 144).
    /// </summary>
    public class UIClassicPhotoAlbum : UIContainer
    {
        private static readonly Rectangle PhotoArea = new Rectangle(52, 22, 596, 392);
        private List<string> Photos;
        private int Index;
        private Texture2D Photo;
        private UITextBox Caption;
        private UILabel Empty;
        private UIButton DeleteButton;
        private string Folder;

        public UIClassicPhotoAlbum(TS1GameScreen game)
        {
            string str(int i) => GameFacade.Strings.GetString("144", i.ToString());
            Texture2D tex(string name) => ClassicUIGraphics.Get("cpanel/" + name);
            var back = tex("Backgrounds/ScrapBack.bmp");
            Add(new UIImage(back));
            Folder = SpecialEffects.AlbumPath(game.vm);
            Photos = Directory.Exists(Folder) ? Directory.GetFiles(Folder).Where(IsPhoto).OrderBy(x => x).ToList() : new List<string>();

            UIButton btn(string art, int place, int tooltip, Action click)
            {
                var xy = str(place).Trim('(', ')').Split(';');
                var b = new UIButton(tex("Buttons/" + art)) { Position = new Vector2(int.Parse(xy[0]), int.Parse(xy[1])), Tooltip = str(tooltip) };
                b.OnButtonClick += (e) => click();
                Add(b);
                return b;
            }
            btn("ScrapDone.bmp", 0, 2, Close).Caption = str(1);
            DeleteButton = btn("ScrapDelete.bmp", 3, 5, ConfirmDelete);
            DeleteButton.Caption = str(4);
            btn("ScrapLeft.bmp", 6, 7, () => Show(Index - 1));
            btn("ScrapRight.bmp", 8, 9, () => Show(Index + 1));
            btn("ScrapHome.bmp", 10, 11, () => Show(0));
            btn("ScrapEnd.bmp", 12, 13, () => Show(Photos.Count - 1));

            var style = ClassicFont.Style(12);
            style.Color = Color.White;
            Caption = new UITextBox { Position = new Vector2(100, 430), Tooltip = str(19), MaxChars = 60 };
            var field = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
            field.SetData(new Color[] { new Color(0, 0, 40) });
            Caption.SetBackgroundTexture(field, 0, 0, 0, 0);
            Caption.TextMargin = new Rectangle(6, 3, 6, 3);
            Caption.Alignment = TextAlignment.Center;
            Caption.TextStyle = style;
            Caption.SetSize(500, 22);
            Caption.OnEnterPress += (e) => Rename();
            Add(Caption);
            Add(Empty = new UILabel { Caption = str(22), CaptionStyle = style, Position = new Vector2(PhotoArea.X, PhotoArea.Y), Size = new Vector2(PhotoArea.Width, PhotoArea.Height), Alignment = TextAlignment.Center | TextAlignment.Middle });

            Position = new Vector2((UIScreen.Current.ScreenWidth - back.Width) / 2, (UIScreen.Current.ScreenHeight - back.Height) / 2);
            Show(Photos.Count - 1);
        }

        private static bool IsPhoto(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg";
        }

        private void Show(int index)
        {
            Rename();
            Photo?.Dispose();
            Photo = null;
            Index = Math.Max(0, Math.Min(Photos.Count - 1, index));
            Empty.Visible = Photos.Count == 0;
            Caption.Visible = DeleteButton.Visible = Photos.Count > 0;
            if (Photos.Count == 0) return;
            try
            {
                using (var file = File.OpenRead(Photos[Index])) Photo = Texture2D.FromStream(GameFacade.GraphicsDevice, file);
            }
            catch (Exception) { }
            Caption.CurrentText = Path.GetFileNameWithoutExtension(Photos[Index]);
        }

        /// <summary>
        /// A changed name renames the photo's file.
        /// </summary>
        private void Rename()
        {
            if (Photos.Count == 0 || Index >= Photos.Count) return;
            var path = Photos[Index];
            var bad = Path.GetInvalidFileNameChars();
            var name = new string((Caption.CurrentText ?? "").Where(x => !bad.Contains(x)).ToArray()).Trim();
            if (name == "" || name == Path.GetFileNameWithoutExtension(path)) return;
            var target = Path.Combine(Folder, name + Path.GetExtension(path));
            if (File.Exists(target)) return;
            try
            {
                File.Move(path, target);
                Photos[Index] = target;
            }
            catch (Exception) { }
        }

        private void ConfirmDelete()
        {
            string str(int i) => GameFacade.Strings.GetString("144", i.ToString());
            UIClassicAlert alert = null;
            alert = new UIClassicAlert(new UIAlertOptions
            {
                Title = str(20),
                Message = str(21),
                Buttons = UIAlertButton.YesNo((b) =>
                {
                    alert.Close();
                    try { File.Delete(Photos[Index]); } catch (Exception) { }
                    Photos.RemoveAt(Index);
                    Caption.CurrentText = "";
                    Show(Index);
                }, (b) => alert.Close())
            }, null, false);
            UIScreen.GlobalShowDialog(alert, true);
        }

        private void Close()
        {
            Rename();
            Photo?.Dispose();
            UIScreen.RemoveDialog(this);
        }

        public override void Draw(UISpriteBatch batch)
        {
            base.Draw(batch);
            if (!Visible || Photo == null) return;
            //fitted into the frame, never larger than it was taken
            var scale = Math.Min(1f, Math.Min(PhotoArea.Width / (float)Photo.Width, PhotoArea.Height / (float)Photo.Height));
            var size = new Vector2(Photo.Width, Photo.Height) * scale;
            var pos = new Vector2(PhotoArea.X, PhotoArea.Y) + (new Vector2(PhotoArea.Width, PhotoArea.Height) - size) / 2;
            DrawLocalTexture(batch, Photo, null, pos, new Vector2(scale));
        }
    }
}
