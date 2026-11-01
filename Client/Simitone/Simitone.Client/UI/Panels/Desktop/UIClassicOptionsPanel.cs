using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Common;
using FSO.HIT;
using FSO.HIT.Model;
using FSO.SimAntics;
using FSO.SimAntics.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using Simitone.Client.UI.Screens;
using Simitone.Client.Utils;
using System;
using System.Collections.Generic;

namespace Simitone.Client.UI.Panels.Desktop
{
    /// <summary>
    /// The classic Options panel: Save, Neighborhood and Quit, and the Play, Sound and Graphics option pages. The
    /// positions and names are the game's own, from its options string table (145). Hovering an option explains it in a
    /// popup above the strip, with the game's picture where it has one.
    /// </summary>
    public class UIClassicOptionsPanel : UIClassicStrip
    {
        private const int SettingsX = 275; //where the game puts the first column of options
        private const int DetailX = 480; //and the graphics page's detail settings
        private UIButton PlayButton;
        private UIButton SoundButton;
        private UIButton GraphicsButton;
        private UIContainer Page;
        private UIClassicInfoPopup Popup;

        public UIClassicOptionsPanel(TS1GameScreen game) : base(game)
        {
            string str(int i) => GameFacade.Strings.GetString("145", i.ToString());
            UIButton btn(string name, int x, int y, string tooltip, Action click)
            {
                var b = new UIButton(Tex("Buttons/" + name)) { Position = new Vector2(x, y), Tooltip = tooltip };
                b.OnButtonClick += (e) => click();
                Add(b);
                return b;
            }
            btn("OptSave.BMP", 29, 6, str(3), () => Game.Save());
            btn("OptNbhd.bmp", 88, 6, str(1), () => Game.ReturnToNeighbourhood());
            btn("OptExit.bmp", 159, 6, str(5), () => Game.CloseAttempt());
            GraphicsButton = btn("optgraphics.bmp", 29, 58, str(7), () => ShowPage(2));
            SoundButton = btn("optsound.bmp", 89, 58, str(9), () => ShowPage(1));
            PlayButton = btn("optplay.bmp", 151, 58, str(11), () => ShowPage(0));

            Add(new UIImage(Tex("Backgrounds/OptionsToothpkHoriz.bmp")) { Position = new Vector2(14, 50) });
            Add(new UIImage(Tex("Backgrounds/OptionsToothpkVert.bmp")) { Position = new Vector2(226, 6) });

            Popup = new UIClassicInfoPopup(-4) { X = 8 };
            ShowPage(0);
            Add(Popup);
            GameResized();
        }

        private void ShowPage(int page)
        {
            PlayButton.Selected = page == 0;
            SoundButton.Selected = page == 1;
            GraphicsButton.Selected = page == 2;
            if (Page != null) Remove(Page);
            Popup?.Hide();
            Page = (page == 0) ? PlayPage() : (page == 1) ? SoundPage() : GraphicsPage();
            Add(Page);
            if (Popup != null) Add(Popup); //keep it on top
        }

        /// <summary>
        /// Shows the popup while the mouse is over an option.
        /// </summary>
        private Action<bool> Explains(string picture, string title, string text)
        {
            return (hover) =>
            {
                if (hover) Popup.Show((picture == null) ? null : Tex(picture), title, text);
                else Popup.Hide();
            };
        }

        private UIContainer PlayPage()
        {
            string str(int i) => GameFacade.Strings.GetString("145", i.ToString());
            var page = new UIContainer();
            var settings = GlobalSettings.Default;
            var count = 0;
            void check(string name, string picture, string title, string text, bool value, Action<bool> set)
            {
                //four to a column, at the game's spacing
                var pos = new Vector2(SettingsX + (count / 4) * 200, 9 + (count % 4) * 20);
                var box = new UIClassicCheckbox(name, Style(10), 190) { Position = pos, Checked = value };
                box.OnToggle += (on) =>
                {
                    set(on);
                    settings.Save();
                };
                box.OnHover += Explains(picture, title, text);
                page.Add(box);
                count++;
            }
            check(str(56), "PopupOptAutoCenter.bmp", str(57), str(58), settings.TS1AutoCenter, (on) =>
            {
                VMSpecialEffect.AutoCenter = on;
                settings.TS1AutoCenter = on;
            });
            check(str(59), "PopupOptFreeWill.bmp", str(60), str(61), VM.FreeWillEnabled, (on) =>
            {
                VM.FreeWillEnabled = on;
                settings.TS1FreeWill = on;
            });
            check(str(62), "PopupOptEdgeScroll.bmp", str(63), str(64), settings.EdgeScroll, (on) => settings.EdgeScroll = on);
            check(str(65), "PopupOptSimInBack.bmp", str(66).Trim(), str(67), settings.TS1SimInBackground, (on) => settings.TS1SimInBackground = on);
            check(str(72), "PopupOptQuickTips.BMP", str(73), str(74), settings.TS1QuickTips, (on) => settings.TS1QuickTips = on);
            check(str(75), "PopupOptAutoSnap.bmp", str(76), str(77), settings.TS1AutoSnapshot, (on) => settings.TS1AutoSnapshot = on);
            check(str(78), "PopupOptLivePIP.bmp", str(79), str(80), settings.TS1LivePIP, (on) => settings.TS1LivePIP = on);
            check(str(81), "PopupExportHTML.bmp", str(82), str(83), settings.TS1ExportHTML, (on) => settings.TS1ExportHTML = on);
            check("Classic UI", null, "About Classic UI",
                "Draws the control panel, panels and dialogs with the original art from your copy of The Sims. Turn it off to use Simitone's own interface.",
                true, (on) =>
                {
                    GlobalSettings.Default.TS1ClassicUI = on;
                    Game.Frontend.RebuildDesktopUCP();
                });

            //Reset Tutorial, below Classic UI
            var reset = new UIButton(Tex("Buttons/opttutreset.BMP")) { Position = new Vector2(SettingsX + 400, 34) };
            reset.OnButtonClick += (b) => TutorialReset.Confirm(Game);
            page.Add(reset);
            page.Add(new UILabel() { Caption = str(69), CaptionStyle = Style(10), Position = new Vector2(SettingsX + 400 + 34, 34), Size = new Vector2(120, 30), Alignment = TextAlignment.Left | TextAlignment.Middle });
            var explainReset = Explains("PopupResetTutorial.bmp", str(70), str(71));
            reset.OnButtonHover += (b) => explainReset(true);
            reset.OnButtonExit += (b) => explainReset(false);
            return page;
        }

        /// <summary>
        /// The game's graphics options. Shadows need lighting, as Simitone's shadows are a lighting mode, and Interface
        /// Effects is the smooth zoom. Terrain Detail thins out the grass; Character Detail is how many nearby lights
        /// shade the Sims (with advanced lighting).
        /// </summary>
        private UIContainer GraphicsPage()
        {
            string str(int i) => GameFacade.Strings.GetString("145", i.ToString());
            var settings = GlobalSettings.Default;
            var page = new UIContainer();
            UIClassicCheckbox check(int name, string picture, int y, bool value, Action<bool> set)
            {
                var box = new UIClassicCheckbox(str(name), Style(10), 190) { Position = new Vector2(SettingsX, y), Checked = value };
                box.OnToggle += (on) =>
                {
                    set(on);
                    ApplyGraphics();
                };
                box.OnHover += Explains(picture, str(name + 1), str(name + 2));
                page.Add(box);
                return box;
            }
            UIClassicCheckbox shadows = null;
            //anti-aliasing is MSAA where the graphics card has it, supersampling otherwise
            check(14, "PopupOptAntiAlias.bmp", 9, settings.AntiAlias > 0, (on) => settings.AntiAlias = on ? (FSOEnvironment.MSAASupport ? 1 : 2) : 0);
            shadows = check(17, "PopupOptShadows.bmp", 29, settings.LightingMode >= 2, (on) => settings.LightingMode = on ? 2 : 1);
            check(20, "PopupOptLighting.bmp", 49, settings.LightingMode > 0, (on) =>
            {
                settings.LightingMode = on ? 1 : 0;
                shadows.Checked = false;
                shadows.Disabled = !on;
            });
            shadows.Disabled = settings.LightingMode == 0;
            check(23, "PopupOptTransUI.bmp", 69, settings.SmoothZoom, (on) => settings.SmoothZoom = on);

            //Low, Med and High, under each detail setting's name (the game's second column)
            void detail(int name, string picture, int y, int value, Action<int> set)
            {
                var explain = Explains(picture, str(name + 1), str(name + 2));
                page.Add(new UILabel() { Caption = str(name), CaptionStyle = Style(10), Position = new Vector2(DetailX, y), Size = new Vector2(190, 18) });
                var radios = new List<UIClassicCheckbox>();
                for (int i = 0; i < 3; i++)
                {
                    var level = i;
                    var radio = new UIClassicCheckbox(str(39 + i), Style(10), 64, true) { Position = new Vector2(DetailX + i * 64, y + 18), Checked = value == i };
                    radio.OnToggle += (on) =>
                    {
                        foreach (var r in radios) r.Checked = r == radio;
                        set(level);
                        ApplyGraphics();
                    };
                    radio.OnHover += explain;
                    radios.Add(radio);
                    page.Add(radio);
                }
            }
            detail(28, "PopupOptTerrainDetail.bmp", 6, settings.TS1TerrainDetail, (level) => settings.TS1TerrainDetail = level);
            detail(31, "PopupOptCharDetail.bmp", 48, settings.TS1CharacterDetail, (level) => settings.TS1CharacterDetail = level);
            return page;
        }

        /// <summary>
        /// Saves the graphics settings and applies them to the lot.
        /// </summary>
        private void ApplyGraphics()
        {
            var settings = GlobalSettings.Default;
            var config = FSO.LotView.WorldConfig.Current;
            config.LightingMode = settings.LightingMode;
            config.AA = settings.AntiAlias;
            config.SmoothZoom = settings.SmoothZoom;
            config.TerrainDetail = settings.TS1TerrainDetail;
            config.CharacterDetail = settings.TS1CharacterDetail;
            Game.vm?.Context.World.ChangedWorldConfig(GameFacade.GraphicsDevice);
            settings.Save();
        }

        private UIContainer SoundPage()
        {
            string str(int i) => GameFacade.Strings.GetString("145", i.ToString());
            var page = new UIContainer();
            var y = 13;
            void slider(int name, string picture, HITVolumeGroup group, Func<byte> get, Action<byte> set)
            {
                page.Add(new UILabel()
                {
                    Caption = str(name),
                    CaptionStyle = Style(10),
                    Alignment = TextAlignment.Right | TextAlignment.Middle,
                    Position = new Vector2(235, y - 3),
                    Size = new Vector2(60, 20)
                });
                var s = new UIClassicSlider(130) { Position = new Vector2(305, y), Value = get() };
                s.OnChange += (v) =>
                {
                    set((byte)v);
                    HITVM.Get().SetMasterVolume(group, v / 10f);
                    GlobalSettings.Default.Save();
                };
                s.OnHover += Explains(picture, str(name + 1), str(name + 2));
                page.Add(s);
                y += 28;
            }
            slider(44, "PopupOptSFX.bmp", HITVolumeGroup.FX, () => GlobalSettings.Default.FXVolume, (v) => GlobalSettings.Default.FXVolume = v);
            slider(47, "PopupOptMusic.bmp", HITVolumeGroup.MUSIC, () => GlobalSettings.Default.MusicVolume, (v) => GlobalSettings.Default.MusicVolume = v);
            slider(50, "PopupOptVox.bmp", HITVolumeGroup.VOX, () => GlobalSettings.Default.VoxVolume, (v) => GlobalSettings.Default.VoxVolume = v);
            return page;
        }
    }
}
