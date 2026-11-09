using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.Common.Utils;
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
    /// Graphics options (lighting, AA, 3D mode, display mode, resolution, refresh rate).
    /// Each setting is a button that cycles through its values; changes apply immediately
    /// where the engine allows it and are saved to config.ini.
    /// </summary>
    public class UIGraphicsOptionsAlert : UIMobileDialog
    {
        private TS1GameScreen Game;
        private List<OptionRow> Rows = new List<OptionRow>();
        private UILabel NoteLabel;
        private UIBigButton DoneButton;

        // windowed size to return to after fullscreen (GraphicsWidth/Height track the current size)
        private Point WindowedSize;

        private static readonly Point[] Resolutions = new Point[]
        {
            new Point(1024, 768), new Point(1280, 720), new Point(1280, 800), new Point(1366, 768),
            new Point(1440, 900), new Point(1600, 900), new Point(1680, 1050), new Point(1920, 1080),
            new Point(1920, 1200), new Point(2560, 1440)
        };
        private static readonly int[] RefreshRates = new int[] { 30, 60, 75, 90, 120, 144 };

        private class OptionRow
        {
            public UILabel Label;
            public UIBigButton Button;
            public Func<List<Tuple<string, int>>> Choices;
            public Func<int> Current;
            public Action<int> Set;
            public Func<bool> Enabled;
        }

        public UIGraphicsOptionsAlert(TS1GameScreen game)
        {
            Game = game;
            Caption = "Graphics Options";
            SetHeight(640);

            var settings = GlobalSettings.Default;
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            WindowedSize = new Point(settings.GraphicsWidth, settings.GraphicsHeight);
            if (!settings.Windowed || WindowedSize.X * FSOEnvironment.DPIScaleFactor >= display.Width)
                WindowedSize = new Point(1024, 768);

            AddRow("Lighting",
                () => Choices("Off", "Advanced", "Advanced + Shadows", "Ultra"),
                () => Math.Max(0, settings.LightingMode),
                (v) => { settings.LightingMode = v; ApplyWorldConfig(); });

            AddRow("Anti-Aliasing",
                () =>
                {
                    var list = new List<Tuple<string, int>>() { Tuple.Create("Off", 0) };
                    if (FSOEnvironment.MSAASupport) list.Add(Tuple.Create("4x MSAA", 1));
                    list.Add(Tuple.Create("2x Supersampling", 2));
                    return list;
                },
                () => settings.AntiAlias,
                (v) => { settings.AntiAlias = v; ApplyWorldConfig(); });

            AddRow("3D Mode",
                () => Choices("Off (2D)", "Hybrid (3D Walls)", "Full 3D"),
                () => settings.GlobalGraphicsMode,
                (v) =>
                {
                    settings.GlobalGraphicsMode = v;
                    // switching between the two 3D modes is live; turning 3D on/off needs a restart
                    // because 3D content is only loaded at startup.
                    if (FSOEnvironment.Enable3D && v > 0) GraphicsModeControl.ChangeMode((GlobalGraphicsMode)v);
                });

            AddRow("Display",
                () => new List<Tuple<string, int>>() { Tuple.Create("Windowed", 1), Tuple.Create("Fullscreen", 0) },
                () => settings.Windowed ? 1 : 0,
                (v) => SetDisplayMode(v == 1, WindowedSize));

            AddRow("Window Size",
                () => WindowSizes().Select((x, i) => Tuple.Create(x.X + "x" + x.Y, i)).ToList(),
                () => WindowSizes().IndexOf(WindowedSize),
                (v) => SetDisplayMode(true, WindowSizes()[v]),
                () => settings.Windowed);

            AddRow("Refresh Rate",
                () =>
                {
                    var rates = RefreshRates.ToList();
                    if (!rates.Contains(settings.TargetRefreshRate)) rates.Add(settings.TargetRefreshRate);
                    rates.Sort();
                    return rates.Select(x => Tuple.Create(x + " Hz", x)).ToList();
                },
                () => settings.TargetRefreshRate,
                (v) => ((SimitoneGame)GameFacade.Game).SetRefreshRate(v));

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
            Refresh();
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

        private void AddRow(string name, Func<List<Tuple<string, int>>> choices, Func<int> current, Action<int> set, Func<bool> enabled = null)
        {
            var row = new OptionRow() { Choices = choices, Current = current, Set = set, Enabled = enabled };

            row.Label = new UILabel();
            row.Label.Caption = name;
            row.Label.Alignment = TextAlignment.Center | TextAlignment.Middle;
            row.Label.CaptionStyle = row.Label.CaptionStyle.Clone();
            row.Label.CaptionStyle.Size = 20;
            row.Label.CaptionStyle.Color = UIStyle.Current.DialogText;
            Add(row.Label);

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

            Rows.Add(row);
        }

        private void Layout()
        {
            // two columns of three settings
            var colWidth = Math.Min(460, Width / 2 - 40);
            for (int i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                var centerX = (i % 2 == 0) ? Width / 4 : (Width * 3) / 4;
                var y = 95 + (i / 2) * 135;
                row.Label.Position = new Vector2(centerX - colWidth / 2, y);
                row.Label.Size = new Vector2(colWidth, 30);
                row.Button.Position = new Vector2(centerX - colWidth / 2, y + 30);
                row.Button.Width = colWidth;
            }
            NoteLabel.Position = new Vector2(0, 500);
            NoteLabel.Size = new Vector2(Width, 25);
            DoneButton.Position = new Vector2((Width - DoneButton.Width) / 2, 530);
        }

        private void Refresh()
        {
            foreach (var row in Rows)
            {
                var list = row.Choices();
                var current = list.FirstOrDefault(x => x.Item2 == row.Current());
                row.Button.Caption = current?.Item1 ?? list[0].Item1;
                row.Button.Disabled = row.Enabled != null && !row.Enabled();
                row.Button.Width = Math.Min(460, Width / 2 - 40);
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
            Game.vm?.Context.World.ChangedWorldConfig(GameFacade.GraphicsDevice);
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
