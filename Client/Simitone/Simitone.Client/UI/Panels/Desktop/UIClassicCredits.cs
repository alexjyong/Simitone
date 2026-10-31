using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Utils;
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
    /// The game's credits, from the neighbourhood's Sims logo: the base game's (UIText table 163, where "[F]" is a gap)
    /// then each expansion's (GameData/Credits.iff), rolling up a navy box like the game's dialogs. The wheel scrolls
    /// them too.
    /// </summary>
    public class UIClassicCredits : UIContainer
    {
        private const int Width = 520;
        private const int Height = 440;
        private static readonly Rectangle TextArea = new Rectangle(30, 52, Width - 60, Height - 52 - 70);
        private Texture2D Px;
        private TextStyle Style;
        private TextStyle Heading;
        private List<(string Text, bool Heading)> Lines = new List<(string, bool)>();
        private int LineHeight;
        private float Scroll;
        private int LastWheel = int.MinValue;

        public UIClassicCredits()
        {
            Px = TextureGenerator.GetPxWhite(GameFacade.GraphicsDevice);
            Style = ClassicFont.Style(11);
            Style.Color = new Color(200, 206, 214);
            Heading = ClassicFont.Style(12);
            Heading.Color = Color.White;
            LineHeight = (int)Math.Ceiling(Style.MeasureString("Ay").Y) + 2;

            var title = Heading.Clone();
            title.Size = 13;
            Add(new UILabel() { Caption = GameFacade.Strings.GetString("151", "8"), CaptionStyle = title, Position = new Vector2(20, 18), Size = new Vector2(Width - 40, 24), Alignment = TextAlignment.Center | TextAlignment.Middle });
            var ok = new UIButton(ClassicUIGraphics.Get("cpanel/Buttons/ButtonTileDialog.BMP"))
            {
                Caption = GameFacade.Strings.GetString("142", "0"),
                CaptionStyle = Heading,
                Width = 94,
                Position = new Vector2((Width - 94) / 2, Height - 55)
            };
            ok.OnButtonClick += (b) => UIScreen.RemoveDialog(this);
            Add(ok);

            //the base game's, then the expansions' in the order their tables are numbered
            var paragraphs = new List<string>();
            var game = ClassicUIGraphics.GetGameData("UIText.iff")?.Get<STR>(163);
            for (int i = 0; i < (game?.Length ?? 0); i++)
            {
                var line = game.GetString(i);
                paragraphs.Add((line == "[F]") ? "" : line);
            }
            var expansions = ClassicUIGraphics.GetGameData("Credits.iff")?.List<STR>()?.OrderBy(x => x.ChunkID);
            if (expansions != null)
            {
                foreach (var table in expansions)
                {
                    paragraphs.Add("");
                    for (int i = 0; i < table.Length; i++) paragraphs.Add(table.GetString(i));
                }
            }
            foreach (var paragraph in paragraphs) AddParagraph(paragraph);
            Scroll = -TextArea.Height; //start below the box and roll up
            GameResized();
        }

        /// <summary>
        /// Splits a paragraph into lines that fit, word by word. Short lines in capitals are the headings.
        /// </summary>
        private void AddParagraph(string paragraph)
        {
            foreach (var raw in paragraph.Replace("\r\n", "\n").Split('\n'))
            {
                var text = raw.Trim();
                var heading = text.Length > 0 && text.Length < 48 && text == text.ToUpperInvariant() && text.Any(char.IsLetter);
                var style = heading ? Heading : Style;
                var line = "";
                foreach (var word in text.Split(' '))
                {
                    var attempt = (line == "") ? word : line + " " + word;
                    if (line != "" && style.MeasureString(attempt).X > TextArea.Width)
                    {
                        Lines.Add((line, heading));
                        line = word;
                    }
                    else line = attempt;
                }
                Lines.Add((line, heading));
            }
        }

        public override void Update(UpdateState state)
        {
            base.Update(state);
            Scroll += 30f / FSO.Common.FSOEnvironment.RefreshRate;
            var wheel = state.MouseState.ScrollWheelValue;
            if (LastWheel != int.MinValue && wheel != LastWheel) Scroll -= (wheel - LastWheel) / 120f * LineHeight * 3;
            LastWheel = wheel;
            var end = Lines.Count * LineHeight;
            if (Scroll > end) Scroll = -TextArea.Height; //round again
            if (Scroll < -TextArea.Height) Scroll = -TextArea.Height;
        }

        public override void Draw(UISpriteBatch batch)
        {
            if (!Visible) return;
            void rect(int x, int y, int w, int h, Color c) => DrawLocalTexture(batch, Px, null, new Vector2(x, y), new Vector2(w, h), c);
            //the navy box of the game's dialogs, with a darker well for the text
            rect(0, 0, Width, Height, new Color(0, 1, 82));
            var light = new Color(190, 198, 208);
            for (int i = 0; i < 4; i++) rect(0, i, Width, 1, Color.Lerp(light, new Color(108, 107, 163), i / 3f));
            rect(0, 0, 2, Height, new Color(14, 16, 92));
            rect(Width - 5, 0, 5, Height, new Color(1, 2, 39));
            rect(0, Height - 5, Width, 5, new Color(1, 2, 39));
            rect(TextArea.X - 6, TextArea.Y - 4, TextArea.Width + 12, TextArea.Height + 8, new Color(0, 0, 40));

            //only the whole lines inside the well
            var first = Math.Max(0, (int)Math.Floor(Scroll / LineHeight));
            for (int i = first; i < Lines.Count; i++)
            {
                var y = TextArea.Y + i * LineHeight - Scroll;
                if (y < TextArea.Y) continue;
                if (y + LineHeight > TextArea.Bottom) break;
                var (text, heading) = Lines[i];
                DrawLocalString(batch, text, Vector2.Zero, heading ? Heading : Style, new Rectangle(TextArea.X, (int)y, TextArea.Width, LineHeight), TextAlignment.Center | TextAlignment.Middle);
            }
            base.Draw(batch);
        }

        public override void GameResized()
        {
            base.GameResized();
            var screen = UIScreen.Current;
            Position = new Vector2((screen.ScreenWidth - Width) / 2, (screen.ScreenHeight - Height) / 2);
        }
    }
}
