using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Utils;
using FSO.Content;
using FSO.HIT;
using FSO.HIT.Model;
using FSO.LotView;
using FSO.LotView.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.UI.Panels
{
    /// <summary>
    /// Options dialog with two tabs:
    ///  - Graphics: lighting, AA, 3D mode, display mode, window size, refresh rate.
    ///  - Sound &amp; Controls: music/sound effect/voice/ambience volume, edge scrolling.
    /// Changes apply immediately where the engine allows it and are saved to config.ini.
    /// Opened from Options > Settings on a lot, and from the "..." button on the neighbourhood.
    /// </summary>
    public class UIOptionsAlert : UIMobileDialog
    {
        private List<OptionRow> Rows = new List<OptionRow>();
        private List<UIBigButton> TabButtons = new List<UIBigButton>();
        private int ActiveTab;
        private UILabel NoteLabel;
        private UIBigButton DoneButton;

        // windowed size to return to after fullscreen (GraphicsWidth/Height track the current size)
        private Point WindowedSize;

        private static readonly string[] TabNames = new string[] { "Graphics", "Sound & Controls" };
        private const float TabScale = 0.6f;

        private static readonly Point[] Resolutions = new Point[]
        {
            new Point(1024, 768), new Point(1280, 720), new Point(1280, 800), new Point(1366, 768),
            new Point(1440, 900), new Point(1600, 900), new Point(1680, 1050), new Point(1920, 1080),
            new Point(1920, 1200), new Point(2560, 1440)
        };
        private static readonly int[] RefreshRates = new int[] { 30, 60, 75, 90, 120, 144 };

        private class OptionRow
        {
            public int Tab;
            public UILabel Label;
            public Func<List<Tuple<string, int>>> Choices;
            public Func<int> Current;
            public Action<int> Set;
            public Func<bool> Enabled;

            // cycling rows: one button that advances to the next value
            public UIBigButton Button;
            // stepper rows: - and + buttons around a value label
            public UIBigButton Minus;
            public UIBigButton Plus;
            public UILabel Value;

            public IEnumerable<UIElement> Elements
            {
                get
                {
                    return new UIElement[] { Label, Button, Minus, Plus, Value }.Where(x => x != null);
                }
            }
        }

        public UIOptionsAlert()
        {
            Caption = "Options";
            SetHeight(700);

            var settings = GlobalSettings.Default;
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            WindowedSize = new Point(settings.GraphicsWidth, settings.GraphicsHeight);
            if (!settings.Windowed || WindowedSize.X * FSOEnvironment.DPIScaleFactor >= display.Width)
                WindowedSize = new Point(1024, 768);

            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = i;
                var btn = new UIBigButton(false);
                btn.Caption = TabNames[i];
                btn.ScaleX = btn.ScaleY = TabScale;
                btn.OnButtonClick += (b) => SetTab(tab);
                Add(btn);
                TabButtons.Add(btn);
            }

            // --- Graphics ---

            AddRow(0, "Lighting",
                () => Choices("Off", "Advanced", "Advanced + Shadows", "Ultra"),
                () => Math.Max(0, settings.LightingMode),
                (v) => { settings.LightingMode = v; ApplyWorldConfig(); });

            AddRow(0, "Anti-Aliasing",
                () =>
                {
                    var list = new List<Tuple<string, int>>() { Tuple.Create("Off", 0) };
                    if (FSOEnvironment.MSAASupport) list.Add(Tuple.Create("4x MSAA", 1));
                    list.Add(Tuple.Create("2x Supersampling", 2));
                    return list;
                },
                () => settings.AntiAlias,
                (v) => { settings.AntiAlias = v; ApplyWorldConfig(); });

            AddRow(0, "3D Mode",
                () => Choices("Off (2D)", "Hybrid (3D Walls)", "Full 3D"),
                () => settings.GlobalGraphicsMode,
                (v) =>
                {
                    settings.GlobalGraphicsMode = v;
                    // switching between the two 3D modes is live; turning 3D on/off needs a restart
                    // because 3D content is only loaded at startup.
                    if (FSOEnvironment.Enable3D && v > 0) GraphicsModeControl.ChangeMode((GlobalGraphicsMode)v);
                });

            AddRow(0, "Display",
                () => new List<Tuple<string, int>>() { Tuple.Create("Windowed", 1), Tuple.Create("Fullscreen", 0) },
                () => settings.Windowed ? 1 : 0,
                (v) => SetDisplayMode(v == 1, WindowedSize));

            AddRow(0, "Window Size",
                () => WindowSizes().Select((x, i) => Tuple.Create(x.X + "x" + x.Y, i)).ToList(),
                () => WindowSizes().IndexOf(WindowedSize),
                (v) => SetDisplayMode(true, WindowSizes()[v]),
                () => settings.Windowed);

            AddRow(0, "Refresh Rate",
                () =>
                {
                    var rates = RefreshRates.ToList();
                    if (!rates.Contains(settings.TargetRefreshRate)) rates.Add(settings.TargetRefreshRate);
                    rates.Sort();
                    return rates.Select(x => Tuple.Create(x + " Hz", x)).ToList();
                },
                () => settings.TargetRefreshRate,
                (v) => ((SimitoneGame)GameFacade.Game).SetRefreshRate(v));

            // --- Sound & Controls ---

            AddVolumeRow("Music", HITVolumeGroup.MUSIC, () => settings.MusicVolume, (v) => settings.MusicVolume = (byte)v);
            AddVolumeRow("Sound Effects", HITVolumeGroup.FX, () => settings.FXVolume, (v) => settings.FXVolume = (byte)v);
            AddVolumeRow("Voices", HITVolumeGroup.VOX, () => settings.VoxVolume, (v) => settings.VoxVolume = (byte)v);
            AddVolumeRow("Ambience", HITVolumeGroup.AMBIENCE, () => settings.AmbienceVolume, (v) => settings.AmbienceVolume = (byte)v);

            AddRow(1, "Edge Scrolling",
                () => new List<Tuple<string, int>>() { Tuple.Create("On", 1), Tuple.Create("Off", 0) },
                () => settings.EdgeScroll ? 1 : 0,
                (v) => settings.EdgeScroll = v == 1);

            NoteLabel = new UILabel();
            NoteLabel.Alignment = TextAlignment.Center | TextAlignment.Middle;
            NoteLabel.CaptionStyle = NoteLabel.CaptionStyle.Clone();
            NoteLabel.CaptionStyle.Size = 15;
            Add(NoteLabel);

            DoneButton = new UIBigButton(true);
            DoneButton.Caption = "Done";
            DoneButton.Width = 275;
            DoneButton.OnButtonClick += (btn) => Close();
            Add(DoneButton);

            Layout();
            SetTab(0);
        }

        /// <summary>
        /// Preset window sizes that fit on the display, plus the current size if it isn't a preset.
        /// </summary>
        private List<Point> WindowSizes()
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            var sizes = Resolutions.Where(x => x.X * FSOEnvironment.DPIScaleFactor <= display.Width
                && x.Y * FSOEnvironment.DPIScaleFactor <= display.Height).ToList();
            if (!sizes.Contains(WindowedSize)) sizes.Insert(0, WindowedSize);
            return sizes;
        }

        private static List<Tuple<string, int>> Choices(params string[] names)
        {
            return names.Select((x, i) => Tuple.Create(x, i)).ToList();
        }

        private OptionRow NewRow(int tab, string name)
        {
            var row = new OptionRow() { Tab = tab };
            row.Label = new UILabel();
            row.Label.Caption = name;
            row.Label.Alignment = TextAlignment.Center | TextAlignment.Middle;
            row.Label.CaptionStyle = row.Label.CaptionStyle.Clone();
            row.Label.CaptionStyle.Size = 20;
            row.Label.CaptionStyle.Color = UIStyle.Current.DialogText;
            Add(row.Label);
            Rows.Add(row);
            return row;
        }

        private void AddRow(int tab, string name, Func<List<Tuple<string, int>>> choices, Func<int> current, Action<int> set, Func<bool> enabled = null)
        {
            var row = NewRow(tab, name);
            row.Choices = choices;
            row.Current = current;
            row.Set = set;
            row.Enabled = enabled;

            row.Button = new UIBigButton(false);
            row.Button.CaptionStyle.Size = 26;
            row.Button.OnButtonClick += (btn) =>
            {
                // advance to the next value, wrapping around
                var list = row.Choices();
                var index = list.FindIndex(x => x.Item2 == row.Current());
                var next = list[(index + 1) % list.Count];
                row.Set(next.Item2);
                GlobalSettings.Default.Save();
                Refresh();
            };
            Add(row.Button);
        }

        private void AddVolumeRow(string name, HITVolumeGroup group, Func<int> current, Action<int> set)
        {
            var row = NewRow(1, name);
            row.Current = current;
            row.Set = (v) =>
            {
                set(v);
                HITVM.Get().SetMasterVolume(group, v / 10f);
            };

            row.Minus = new UIBigButton(false);
            row.Minus.Caption = "-";
            row.Minus.OnButtonClick += (btn) => StepVolume(row, -1);
            Add(row.Minus);

            row.Plus = new UIBigButton(false);
            row.Plus.Caption = "+";
            row.Plus.OnButtonClick += (btn) => StepVolume(row, 1);
            Add(row.Plus);

            row.Value = new UILabel();
            row.Value.Alignment = TextAlignment.Center | TextAlignment.Middle;
            row.Value.CaptionStyle = row.Value.CaptionStyle.Clone();
            row.Value.CaptionStyle.Size = 26;
            row.Value.CaptionStyle.Color = UIStyle.Current.DialogText;
            Add(row.Value);
        }

        private void StepVolume(OptionRow row, int delta)
        {
            var value = Math.Max(0, Math.Min(10, row.Current() + delta));
            row.Set(value);
            GlobalSettings.Default.Save();
            Refresh();
        }

        private void SetTab(int tab)
        {
            ActiveTab = tab;
            foreach (var row in Rows)
            {
                foreach (var elem in row.Elements) elem.Visible = row.Tab == tab;
            }
            for (int i = 0; i < TabButtons.Count; i++)
            {
                var btn = TabButtons[i];
                var active = i == tab;
                btn.Texture = Content.Get().CustomUI.Get(active ? "greenbutton.png" : "button.png").Get(GameFacade.GraphicsDevice);
                btn.CaptionStyle.Color = active ? UIStyle.Current.GreenBtnTxt : UIStyle.Current.BtnTxt;
                btn.CaptionStyle.HighlightedColor = btn.CaptionStyle.Color;
                btn.CaptionStyle.SelectedColor = btn.CaptionStyle.Color;
                btn.Width = 300 / TabScale;
            }
            Refresh();
        }

        private void Layout()
        {
            var tabWidth = 300;
            for (int i = 0; i < TabButtons.Count; i++)
            {
                TabButtons[i].Position = new Vector2(Width / 2 - tabWidth - 10 + i * (tabWidth + 20), 85);
            }

            // two columns of up to three settings per tab
            var colWidth = Math.Min(460, Width / 2 - 40);
            var tabIndex = new Dictionary<int, int>();
            foreach (var row in Rows)
            {
                int i;
                tabIndex.TryGetValue(row.Tab, out i);
                tabIndex[row.Tab] = i + 1;

                var centerX = (i % 2 == 0) ? Width / 4 : (Width * 3) / 4;
                var left = centerX - colWidth / 2;
                var y = 160 + (i / 2) * 130;
                row.Label.Position = new Vector2(left, y);
                row.Label.Size = new Vector2(colWidth, 30);
                if (row.Button != null)
                {
                    row.Button.Position = new Vector2(left, y + 30);
                    row.Button.Width = colWidth;
                }
                else
                {
                    row.Minus.Position = new Vector2(left, y + 30);
                    row.Minus.Width = 100;
                    row.Plus.Position = new Vector2(left + colWidth - 100, y + 30);
                    row.Plus.Width = 100;
                    row.Value.Position = new Vector2(left + 100, y + 30);
                    row.Value.Size = new Vector2(colWidth - 200, 100);
                }
            }
            NoteLabel.Position = new Vector2(0, 560);
            NoteLabel.Size = new Vector2(Width, 25);
            DoneButton.Position = new Vector2((Width - DoneButton.Width) / 2, 590);
        }

        private void Refresh()
        {
            foreach (var row in Rows)
            {
                if (row.Button != null)
                {
                    var list = row.Choices();
                    var current = list.FirstOrDefault(x => x.Item2 == row.Current());
                    row.Button.Caption = current?.Item1 ?? list[0].Item1;
                    row.Button.Disabled = row.Enabled != null && !row.Enabled();
                    row.Button.Width = Math.Min(460, Width / 2 - 40);
                }
                else
                {
                    var value = row.Current();
                    row.Value.Caption = (value * 10) + "%";
                    row.Minus.Disabled = value <= 0;
                    row.Plus.Disabled = value >= 10;
                    row.Minus.Width = row.Plus.Width = 100;
                }
            }

            var restart = (GlobalSettings.Default.GlobalGraphicsMode > 0) != FSOEnvironment.Enable3D;
            NoteLabel.Caption = restart
                ? "Restart Simitone to turn 3D mode " + (FSOEnvironment.Enable3D ? "off." : "on.")
                : "Changes are saved automatically.";
            NoteLabel.CaptionStyle.Color = restart ? UIStyle.Current.NegMoney : UIStyle.Current.DialogText * 0.7f;
        }

        private void ApplyWorldConfig()
        {
            SimitoneGame.UpdateWorldConfig();
            // only a loaded lot has a world to update; the neighbourhood picks the config up on entry
            var game = GameFacade.Screens.CurrentUIScreen as TS1GameScreen;
            game?.vm?.Context.World.ChangedWorldConfig(GameFacade.GraphicsDevice);
        }

        private void SetDisplayMode(bool windowed, Point size)
        {
            if (windowed) WindowedSize = size;
            // the graphics device can't be reset mid-update, so defer to the next frame
            GameThread.NextUpdate((state) =>
            {
                ((SimitoneGame)GameFacade.Game).SetDisplayMode(windowed, size.X, size.Y);
                GlobalSettings.Default.Save();
                Refresh();
            });
        }

        public override void GameResized()
        {
            base.GameResized();
            Layout();
        }
    }
}
