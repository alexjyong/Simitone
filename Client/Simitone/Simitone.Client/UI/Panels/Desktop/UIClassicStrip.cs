using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// A classic mode's panel along the bottom of the screen, right of the control panel (Buy, Build and Options; Live
    /// has UIClassicLivePanel). Draws the strip's background; the frontend calls Kill when the mode changes.
    /// </summary>
    public abstract class UIClassicStrip : UIContainer
    {
        public const int StripHeight = UIClassicLivePanel.StripHeight;
        protected TS1GameScreen Game;
        private UIImage Background;

        public UIClassicStrip(TS1GameScreen game)
        {
            Game = game;
            Add(Background = new UIImage(Tex("Backgrounds/PanelBack.bmp")));
        }

        protected static Texture2D Tex(string name) => ClassicUIGraphics.Get("cpanel/" + name);

        protected static TextStyle Style(int size)
        {
            var style = ClassicFont.Style(size);
            style.Color = Color.White;
            return style;
        }

        /// <summary>
        /// The width right of the control panel.
        /// </summary>
        protected int AvailableWidth => UIScreen.Current.ScreenWidth - (int)X;

        public override void GameResized()
        {
            base.GameResized();
            Background.SetSize(Math.Max(Background.Texture.Width, AvailableWidth), StripHeight);
        }

        /// <summary>
        /// Called when the strip is removed, to let go of anything held in the lot (such as an object being placed).
        /// </summary>
        public virtual void Kill() { }
    }

    /// <summary>
    /// A checkbox from the classic options (optcheckbox.bmp: off, on, then hovered off and on, then disabled off and on),
    /// with its label to the right. The whole row is clickable.
    /// </summary>
    public class UIClassicCheckbox : UIContainer
    {
        private Texture2D Tex;
        private UILabel Label;
        private bool Hover;
        public bool Checked;
        public bool Disabled;
        public event Action<bool> OnToggle;
        public event Action<bool> OnHover;

        /// <summary>
        /// A checkbox, or with radio a radio button (optradio.bmp, the same frames), which clicking only ever turns on.
        /// </summary>
        public UIClassicCheckbox(string caption, TextStyle style, int width, bool radio = false)
        {
            Tex = ClassicUIGraphics.Get(radio ? "cpanel/Buttons/optradio.bmp" : "cpanel/Buttons/optcheckbox.bmp");
            Add(Label = new UILabel() { Caption = caption, CaptionStyle = style, Position = new Vector2(26, 0), Size = new Vector2(width - 26, 21), Alignment = TextAlignment.Left | TextAlignment.Middle });
            ListenForMouse(new Rectangle(0, 0, width, 21), (type, state) =>
            {
                switch (type)
                {
                    case UIMouseEventType.MouseOver: Hover = true; OnHover?.Invoke(true); break;
                    case UIMouseEventType.MouseOut: Hover = false; OnHover?.Invoke(false); break;
                    case UIMouseEventType.MouseUp:
                        if (Disabled || (radio && Checked)) break;
                        Checked = !Checked;
                        FSO.HIT.HITVM.Get().PlaySoundEvent(FSO.Client.UI.Model.UISounds.Click);
                        OnToggle?.Invoke(Checked);
                        break;
                }
            });
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            var frame = (Checked ? 1 : 0) + (Disabled ? 4 : (Hover ? 2 : 0));
            DrawLocalTexture(batch, Tex, new Rectangle(frame * 20, 0, 20, Tex.Height), Vector2.Zero);
            base.Draw(batch);
        }
    }

    /// <summary>
    /// A classic options slider (optslideroff.bmp: the track, then the thumb; optslideron.bmp has the lit thumb).
    /// The track is stretched to the width; the value runs from 0 to Max.
    /// </summary>
    public class UIClassicSlider : UIElement
    {
        private const int TrackWidth = 33;
        private const int ThumbX = 35;
        private const int ThumbWidth = 9;
        private Texture2D Off;
        private Texture2D On;
        private int Width;
        private bool Hover;
        private bool Dragging;
        public int Max = 10;
        public int Value;
        public event Action<int> OnChange;
        public event Action<bool> OnHover;

        public UIClassicSlider(int width)
        {
            Width = width;
            Off = ClassicUIGraphics.Get("cpanel/Buttons/optslideroff.bmp");
            On = ClassicUIGraphics.Get("cpanel/Buttons/optslideron.bmp");
            ListenForMouse(new Rectangle(0, 0, width, 14), (type, state) =>
            {
                switch (type)
                {
                    case UIMouseEventType.MouseOver: Hover = true; OnHover?.Invoke(true); break;
                    case UIMouseEventType.MouseOut: Hover = false; OnHover?.Invoke(false); break;
                    case UIMouseEventType.MouseDown: Dragging = true; break;
                    case UIMouseEventType.MouseUp: Dragging = false; break;
                }
            });
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (!Dragging) return;
            if (state.MouseState.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                Dragging = false;
                return;
            }
            var x = GlobalPoint(new Vector2(state.MouseState.X, state.MouseState.Y)).X;
            var value = (int)Math.Round(MathHelper.Clamp((x - ThumbWidth / 2f) / (Width - ThumbWidth), 0, 1) * Max);
            if (value != Value)
            {
                Value = value;
                OnChange?.Invoke(value);
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            //the track in three slices, so its rounded ends keep their shape
            const int cap = 4;
            DrawLocalTexture(batch, Off, new Rectangle(0, 0, cap, 14), Vector2.Zero);
            DrawLocalTexture(batch, Off, new Rectangle(cap, 0, TrackWidth - cap * 2, 14), new Vector2(cap, 0), new Vector2((Width - cap * 2f) / (TrackWidth - cap * 2), 1));
            DrawLocalTexture(batch, Off, new Rectangle(TrackWidth - cap, 0, cap, 14), new Vector2(Width - cap, 0));
            var thumbX = (Width - ThumbWidth) * Value / (float)Max;
            DrawLocalTexture(batch, (Hover || Dragging) ? On : Off, new Rectangle(ThumbX, 0, ThumbWidth, 14), new Vector2((int)thumbX, 0));
        }
    }

    /// <summary>
    /// The popup above the strip that explains the option under the mouse: its picture, title and description, on the
    /// game's info popup background.
    /// </summary>
    public class UIClassicInfoPopup : UIContainer
    {
        protected UIImage Background;
        private UIImage Picture;
        private UILabel Title;
        private UILabel Text;
        private float Bottom; //the popup grows upwards from here to fit its text

        public UIClassicInfoPopup(float bottom)
        {
            Bottom = bottom;
            var bg = ClassicUIGraphics.Get("cpanel/Backgrounds/PopupInfo.BMP");
            Add(Background = new UIImage(bg));
            Add(Picture = new UIImage() { Position = new Vector2(10, 12) });
            var title = ClassicFont.Style(12);
            title.Color = Color.White;
            Add(Title = new UILabel() { CaptionStyle = title, Position = new Vector2(156, 8), Size = new Vector2(bg.Width - 170, 20), Alignment = TextAlignment.Left | TextAlignment.Top });
            var text = title.Clone();
            text.Size = 9;
            text.Color = new Color(200, 206, 214);
            Add(Text = new UILabel() { CaptionStyle = text, Position = new Vector2(156, 30), Size = new Vector2(bg.Width - 166, bg.Height - 36), Wrapped = true, Alignment = TextAlignment.Left | TextAlignment.Top });
            Visible = false;
        }

        /// <param name="source">The part of the picture to show (catalog pictures have two frames); all of it if null.</param>
        public void Show(Texture2D picture, string title, string text, Rectangle? source = null)
        {
            Picture.Texture = picture;
            Picture.Visible = picture != null;
            if (picture != null)
            {
                //fitted into the space the options' pictures take (133x103), at most twice the size
                var src = source ?? new Rectangle(0, 0, picture.Width, picture.Height);
                var scale = Math.Min(2f, Math.Min(133f / src.Width, 103f / src.Height));
                Picture.SourceRectangle = src;
                Picture.SetSize(src.Width * scale, src.Height * scale);
                Picture.Position = new Vector2(10 + (133 - src.Width * scale) / 2, 12 + (103 - src.Height * scale) / 2);
            }
            //without a picture the text has the whole width
            var left = (picture == null) ? 14 : 156;
            Title.Position = new Vector2(left, 8);
            Title.Size = new Vector2(Background.Texture.Width - left - 14, 20);
            Text.Position = new Vector2(left, 30);
            Text.Size = new Vector2(Background.Texture.Width - left - 10, Text.Size.Y);
            Title.Caption = title;
            Text.Caption = text;
            //long descriptions need more room than the art's height (the measured height falls a line short)
            var measured = TextRenderer.ComputeText(text, new TextRendererOptions
            {
                Alignment = TextAlignment.Left | TextAlignment.Top,
                MaxWidth = (int)Text.Size.X,
                Position = Vector2.Zero,
                Scale = Vector2.One,
                TextStyle = Text.CaptionStyle,
                WordWrap = true
            }, this);
            var height = Math.Max(Background.Texture.Height, measured.BoundingBox.Height + 14 + 42);
            Background.SetSize(Background.Texture.Width, height);
            Text.Size = new Vector2(Text.Size.X, height - 36);
            Y = Bottom - height;
            Visible = true;
        }

        public void Hide() => Visible = false;
    }

    /// <summary>
    /// A help tip shown for a while at the top of the screen, for TS1's Quick Tips option: the help system's
    /// popups appear without pausing the game or needing a click. A new tip replaces the last.
    /// </summary>
    public class UIClassicQuickTip : UIClassicInfoPopup
    {
        private static UIClassicQuickTip Current;
        private float TimeLeft;

        private UIClassicQuickTip() : base(0) { }

        public static void ShowTip(UIContainer screen, string title, string text)
        {
            if (Current?.Parent != null) Current.Parent.Remove(Current);
            Current = new UIClassicQuickTip();
            Current.Show(null, title, text);
            //centred at the top, clear of the action queue at the top left
            Current.Position = new Vector2((UIScreen.Current.ScreenWidth - Current.Background.Texture.Width) / 2, 8);
            //long enough to read: a few seconds, and more for longer tips
            Current.TimeLeft = 6 + text.Length / 40f;
            screen.Add(Current);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            TimeLeft -= 1 / 60f;
            if (TimeLeft <= 0 && Parent != null)
            {
                Parent.Remove(this);
                if (Current == this) Current = null;
            }
        }
    }
}
