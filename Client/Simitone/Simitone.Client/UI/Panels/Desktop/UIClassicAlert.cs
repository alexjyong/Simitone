using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// A game dialog in the style of the Windows version: a navy box in the middle of the screen with the title, the
    /// icon on a soft glow, the message, and the original slanted buttons. Used in place of UIMobileAlert in classic mode.
    /// </summary>
    public class UIClassicAlert : UIContainer
    {
        private static readonly Color Body = new Color(0, 1, 82);
        private static readonly Color TextColor = new Color(200, 206, 214);
        private const int ButtonWidth = 94; //at least; longer captions widen it
        private const int ButtonGap = 16;
        private static Texture2D Glow;

        private int Width;
        private int Height;
        private Texture2D Px;
        private Texture2D Icon;
        private bool IconIsHead;
        private TextStyle Style;
        private string Message;
        private TextRendererResult MessageText;
        private bool TextDirty;
        private Rectangle MessageArea;
        private List<UIButton> Buttons = new List<UIButton>();
        private UITextBox TextBox;
        private bool TextBoxFocused;

        public string ResponseText => TextBox?.CurrentText;

        /// <param name="head">If the icon is a Sim's head, which is cropped to the face like the portraits.</param>
        public UIClassicAlert(UIAlertOptions options, Texture2D icon, bool head)
        {
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            Icon = (icon != null && icon.Height >= 4) ? icon : null;
            IconIsHead = head;
            Message = options.Message ?? "";
            Width = Math.Max(412, Math.Min(560, 300 + Message.Length));

            Style = ClassicFont.Style(11);
            Style.Color = TextColor;
            var buttonStyle = Style.Clone();
            buttonStyle.Size = 12;
            var captions = options.Buttons.Select(x => x.Text ?? DefaultCaption(x.Type)).ToList();
            //the slanted ends take about 15px each
            var widths = captions.Select(x => Math.Max(ButtonWidth, (int)buttonStyle.MeasureString(x).X + 36)).ToList();
            Width = Math.Max(Width, widths.Sum() + Math.Max(0, widths.Count - 1) * ButtonGap + 60);

            var title = Style.Clone();
            title.Size = 13;
            Add(new UILabel()
            {
                Caption = options.Title ?? "",
                CaptionStyle = title,
                Alignment = TextAlignment.Center | TextAlignment.Middle,
                Position = new Vector2(20, 24),
                Size = new Vector2(Width - 40, 24)
            });

            //the message sits right of the icon, or across the dialog without one
            var textX = (Icon != null) ? 128 : 30;
            MessageArea = new Rectangle(textX, 56, Width - textX - 30, 0);
            ComputeText();
            var textBottom = MessageArea.Y + Math.Max(MessageText.BoundingBox.Height + 14, 40);
            var y = Math.Max(textBottom, (Icon != null) ? 112 : 0) + 12;

            if (options.TextEntry)
            {
                TextBox = new UITextBox();
                TextBox.MaxChars = options.MaxChars;
                var field = new Texture2D(GameFacade.GraphicsDevice, 1, 1);
                field.SetData(new Color[] { new Color(0, 0, 40) });
                TextBox.SetBackgroundTexture(field, 0, 0, 0, 0);
                TextBox.TextMargin = new Rectangle(6, 3, 6, 3);
                TextBox.Alignment = TextAlignment.Center;
                TextBox.TextStyle = Style.Clone();
                TextBox.TextStyle.Color = Color.White;
                TextBox.Position = new Vector2(40, y);
                TextBox.SetSize(Width - 80, 22);
                Add(TextBox);
                y += 22 + 16;
            }

            for (int i = 0; i < options.Buttons.Length; i++)
            {
                var opt = options.Buttons[i];
                var btn = new UIButton(ClassicUIGraphics.Get("cpanel/Buttons/ButtonTileDialog.BMP"))
                {
                    Caption = captions[i],
                    CaptionStyle = buttonStyle,
                    Width = widths[i]
                };
                ButtonClickDelegate handler = opt.Handler ?? ((b) => Close());
                btn.OnButtonClick += handler;
                if (TextBox != null && Buttons.Count == 0) TextBox.OnEnterPress += (elem) => handler(btn); //enter confirms, like the first button
                Buttons.Add(btn);
                Add(btn);
            }

            Height = Math.Max(206, y + 33 + 22);
            var total = widths.Sum() + Math.Max(0, widths.Count - 1) * ButtonGap;
            var bx = (Width - total) / 2;
            foreach (var btn in Buttons)
            {
                btn.Position = new Vector2(bx, Height - 22 - 33);
                bx += (int)btn.Width + ButtonGap;
            }

            if (Glow == null) Glow = MakeGlow();
            GameResized();
        }

        /// <summary>
        /// Lays out the message. The result is in screen space, so it's redone whenever the dialog moves.
        /// </summary>
        private void ComputeText()
        {
            MessageText = TextRenderer.ComputeText(Message, new TextRendererOptions
            {
                Alignment = TextAlignment.Center | TextAlignment.Top,
                MaxWidth = MessageArea.Width,
                Position = new Vector2(MessageArea.X, MessageArea.Y),
                Scale = Vector2.One,
                TextStyle = Style,
                WordWrap = true
            }, this);
            TextDirty = false;
        }

        public override void CalculateMatrix()
        {
            base.CalculateMatrix();
            TextDirty = true;
        }

        private static string DefaultCaption(UIAlertButtonType type)
        {
            switch (type)
            {
                case UIAlertButtonType.Yes: return GameFacade.Strings.GetString("142", "2");
                case UIAlertButtonType.No: return GameFacade.Strings.GetString("142", "3");
                case UIAlertButtonType.Cancel: return GameFacade.Strings.GetString("142", "1");
                default: return GameFacade.Strings.GetString("142", "0");
            }
        }

        /// <summary>
        /// The light behind the dialog's icon: a blue that fades out from the middle.
        /// </summary>
        private static Texture2D MakeGlow()
        {
            const int size = 96;
            var data = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f)) / (size / 2f);
                    var a = MathHelper.Clamp(1 - d, 0, 1);
                    data[y * size + x] = new Color(24, 72, 196) * (float)Math.Pow(a, 1.5);
                }
            }
            var tex = new Texture2D(GameFacade.GraphicsDevice, size, size);
            tex.SetData(data);
            return tex;
        }

        public void Close()
        {
            UIScreen.RemoveDialog(this);
        }

        public override void GameResized()
        {
            base.GameResized();
            var screen = UIScreen.Current;
            Position = new Vector2((screen.ScreenWidth - Width) / 2, (screen.ScreenHeight - Height) / 2);
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            if (TextBox != null && !TextBoxFocused)
            {
                //type straight away, without clicking the box first
                state.InputManager.SetFocus(TextBox);
                TextBoxFocused = true;
            }
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            void rect(int x, int y, int w, int h, Color c) => DrawLocalTexture(batch, Px, null, new Vector2(x, y), new Vector2(w, h), c);

            //the navy box, lit along the top and in shadow along the bottom and right, as the game draws it
            rect(0, 0, Width, Height, Body);
            var light = new Color(190, 198, 208);
            for (int i = 0; i < 4; i++) rect(0, i, Width, 1, Color.Lerp(light, new Color(108, 107, 163), i / 3f));
            rect(0, 4, Width, 2, new Color(19, 25, 104));
            rect(0, 0, 2, Height, new Color(14, 16, 92));
            rect(Width - 5, 0, 5, Height, new Color(1, 2, 39));
            rect(0, Height - 5, Width, 5, new Color(1, 2, 39));

            if (TextBox != null)
            {
                //a sunken field for the text
                var p = TextBox.Position.ToPoint();
                rect(p.X - 1, p.Y - 1, (int)TextBox.Width + 2, 24, new Color(108, 107, 163));
            }

            if (Icon != null)
            {
                var centre = new Vector2(80, 82);
                DrawLocalTexture(batch, Glow, null, centre - new Vector2(Glow.Width / 2f), Vector2.One);
                //a framed face (Sims) or thumbnail (objects), fitted to 40px
                var src = IconIsHead
                    ? new Rectangle(Icon.Width / 8, Icon.Height / 10, Icon.Width * 3 / 4, Icon.Height * 3 / 4)
                    : new Rectangle(0, 0, Icon.Width, Icon.Height);
                var scale = 40f / Math.Max(src.Width, src.Height);
                var size = new Vector2(src.Width, src.Height) * scale;
                rect((int)(centre.X - 21), (int)(centre.Y - 21), 42, 42, new Color(200, 210, 220));
                rect((int)(centre.X - 20), (int)(centre.Y - 20), 40, 40, new Color(0, 0, 64));
                DrawLocalTexture(batch, Icon, src, centre - size / 2, new Vector2(scale));
            }

            base.Draw(batch);
            if (TextDirty) ComputeText();
            TextRenderer.DrawText(MessageText.DrawingCommands, this, batch);
        }
    }
}
