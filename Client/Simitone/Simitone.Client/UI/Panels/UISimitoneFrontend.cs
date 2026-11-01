using FSO.Client.UI.Framework;
using FSO.Content;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSO.Common.Rendering.Framework.Model;
using FSO.Client;
using Microsoft.Xna.Framework;
using Simitone.Client.UI.Panels.LiveSubpanels;
using FSO.Client.UI.Model;
using Microsoft.Xna.Framework.Input;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.HIT;
using Simitone.Client.UI.Panels.Desktop;

namespace Simitone.Client.UI.Panels
{
    public class UISimitoneFrontend : UIContainer
    {
        public UIClockPanel Clock;
        public UITwoStateButton CutBtn;
        public UICutawayPanel CutPanel;
        public TS1GameScreen Game;
        public UIMoneyPanel Money;
        public UIMainPanel MainPanel;
        public UIStencilButton ExtendPanelBtn;
        public UIModeSwitcher ModeSwitcher;
        public UIDesktopUCP DesktopUCP;
        //classic UI: the live mode strip that takes the main panel's place in live mode
        public UIClassicLivePanel ClassicLive;
        public UIClassicStrip ClassicMode; //the classic Buy, Build or Options panel, in place of the main panel
        public UIClassicCameraPanel ClassicCamera; //classic camera mode's strip, in place of live mode's
        private UIClassicCameraFrame CameraFrame;
        public UICheatTextbox CheatTextbox;

        public bool PanelActive;
        public int LastCut = 0;

        public UISimitoneFrontend(TS1GameScreen screen)
        {
            var ui = Content.Get().CustomUI;
            Game = screen;
            //panels made below use Game.Frontend, and lots without a family switch to buy mode before this returns
            Game.Frontend = this;

            if (!Game.Desktop)
            {
                CutBtn = new UITwoStateButton(ui.Get("cut_btn_down.png").Get(GameFacade.GraphicsDevice));
                CutBtn.X = screen.ScreenWidth - (256 + 15);
                CutBtn.Y = 15;
                CutBtn.OnButtonClick += CutButton;
                Add(CutBtn);

                Clock = new UIClockPanel(screen.vm);
                Clock.X = screen.ScreenWidth - (334 + 15);
                Clock.Y = 15;
                Add(Clock);

                Money = new UIMoneyPanel(screen);
                Money.Position = new Vector2(15, screen.ScreenHeight - 172);
                Add(Money);

                ExtendPanelBtn = new UIStencilButton(ui.Get("panel_expand.png").Get(GameFacade.GraphicsDevice));
                ExtendPanelBtn.OnButtonClick += ExpandClicked;
                Add(ExtendPanelBtn);
            }

            CheatTextbox = new UICheatTextbox(Game.vm);
            CheatTextbox.Position = new Vector2(10, 10);
            CheatTextbox.Visible = false;
            Add(CheatTextbox);

            MainPanel = new UIMainPanel(screen);
            MainPanel.OnEndSelect += OnEndSelect;
            MainPanel.ModeChanged += ModeChanged;
            Add(MainPanel);

            if (Game.Desktop)
            {
                DesktopUCP = new UIDesktopUCP(screen);
                PlaceDesktopUCP();
                DesktopUCP.OnModeClick += LiveButtonClicked;
                DesktopUCP.OnCameraClick += ToggleCamera;
                Add(DesktopUCP);
                UpdateClassicLive();
            }
            else
            {
                var mode = new UIModeSwitcher(screen);
                mode.Position = new Vector2(64 + 15, screen.ScreenHeight - (64 + 15));
                mode.OnModeClick += LiveButtonClicked;
                Add(mode);
                ModeSwitcher = mode;
                ExtendPanelBtn.Position = new Vector2(mode.X + 54, mode.Y - 50);
            }

            MainPanel.X = MainPanelX();
            MainPanel.GameResized();
            MainPanel.Y = screen.ScreenHeight - (128 + 15);
            MainPanel.Visible = false;

            if (Game.vm.GetGlobalValue(32) > 0)
            {
                MainPanel.SetMode(UIMainPanelMode.BUY);
                ModeSwitcher?.EndSwitch(MainPanel.Mode);
                if (ClassicMode == null) MainPanel.Open(); //the classic buy panel replaces it
            } else
            {
                FSO.HIT.HITVM.Get().PlaySoundEvent(UIMusic.None);
            }
        }

        private void ModeChanged(UIMainPanelMode obj)
        {
            Clock?.SetHidden(obj != UIMainPanelMode.LIVE);
            DesktopUCP?.SetMode(obj);
            if (ClassicLive != null)
            {
                ClassicLive.Visible = obj == UIMainPanelMode.LIVE;
                if (ClassicLive.Visible || HasClassicMode(obj))
                {
                    if (MainPanel.PanelActive) MainPanel.Close();
                    //the mode's first category opens a subpanel even with the main panel closed (and buy mode's search
                    //box). Removing it lets go of anything held, so do it before the classic panel picks anything up
                    MainPanel.SetSubpanel(null);
                }
                UpdateClassicMode(obj);
            }
            var lotType = MainPanel.GetLotType(true);
            var hit = FSO.HIT.HITVM.Get();
            switch (obj)
            {
                case UIMainPanelMode.LIVE:
                case UIMainPanelMode.OPTIONS:
                    hit.PlaySoundEvent(UIMusic.None); break;
                case UIMainPanelMode.BUY:
                    switch (lotType)
                    {
                        case UICatalogMode.Downtown:
                            hit.PlaySoundEvent(UIMusic.Downtown); break;
                        case UICatalogMode.Vacation:
                            hit.PlaySoundEvent(UIMusic.Vacation); break;
                        case UICatalogMode.Community:
                            hit.PlaySoundEvent(UIMusic.Unleashed); break;
                        case UICatalogMode.Studiotown:
                            hit.PlaySoundEvent(UIMusic.SuperstarTransition); break;
                        case UICatalogMode.Magictown:
                            hit.PlaySoundEvent(UIMusic.MagictownBuy); break;
                        default:
                            hit.PlaySoundEvent(UIMusic.Buy); break;
                    }
                    break;
                case UIMainPanelMode.BUILD:
                    switch (lotType)
                    {
                        case UICatalogMode.Downtown:
                            hit.PlaySoundEvent(UIMusic.Downtown); break;
                        case UICatalogMode.Vacation:
                            hit.PlaySoundEvent(UIMusic.Vacation); break;
                        case UICatalogMode.Community:
                            hit.PlaySoundEvent(UIMusic.Unleashed); break;
                        case UICatalogMode.Studiotown:
                            hit.PlaySoundEvent(UIMusic.SuperstarTransition); break;
                        case UICatalogMode.Magictown:
                            hit.PlaySoundEvent(UIMusic.MagictownBuild); break;
                        default:
                            hit.PlaySoundEvent(UIMusic.Build); break;
                    }
                    break;
            }
        }

        private void ExpandClicked(UIElement button)
        {
            MainPanel.Open();
            MainPanel.Switcher_OnCategorySelect(MainPanel.Switcher.ActiveCategory);
        }

        /// <summary>
        /// Programmatically switch to a different mode (Buy, Build, Live, Options).
        /// Used by eyedropper to auto-switch modes when clicking cross-mode objects.
        /// </summary>
        public void SwitchMode(UIMainPanelMode mode)
        {
            LiveButtonClicked(mode);
        }

        private bool LiveButtonClicked(UIMainPanelMode mode)
        {
            if (ClassicLive != null && (mode == UIMainPanelMode.LIVE || HasClassicMode(mode)))
            {
                //the classic strips are these modes' panels: leave the main panel closed (closing it if it was open)
                if (MainPanel.Mode != mode) MainPanel.SetMode(mode);
                return false;
            }
            var deskAuto = Game.Desktop && (mode != UIMainPanelMode.LIVE || MainPanel.Mode != UIMainPanelMode.LIVE);
            if (MainPanel.PanelActive || deskAuto)
            {
                if (MainPanel.ShowingSelect || deskAuto)
                {
                    if (!MainPanel.PanelActive) MainPanel.Open();
                    //switch to the target mode
                    MainPanel.SetMode(mode);
                    MainPanel.SwitchAvatar?.Kill();
                    return false;
                }
                else
                {
                    StartSelect();
                    return true;
                }
            } else
            {
                MainPanel.Open();
                StartSelect();
                return true;
            }
        }

        private void StartSelect()
        {
            MainPanel.ShowSelect();
        }

        private void OnEndSelect()
        {
            ModeSwitcher?.EndSwitch(MainPanel.Mode);
        }

        private void CutButton(UIElement button)
        {
            if (CutPanel != null)
            {
                CutBtn.Selected = false;
                CutPanel.Kill();
                CutPanel = null;
            } else
            {
                CutBtn.Selected = true;
                CutPanel = new UICutawayPanel(LastCut);
                CutPanel.X = CutBtn.X-39;
                CutPanel.Y = 15;
                CutPanel.OnSelection += CutPanel_OnSelection;
                AddAt(0, CutPanel);
            }
        }

        private void CutPanel_OnSelection(int obj)
        {
            CutBtn.Selected = false;
            Game.LotControl.World.State.DrawRoofs = (obj == 3);
            Game.LotControl.WallsMode = obj;
            CutPanel?.Kill();
            CutPanel = null;
        }

        public float ClockTween;
        public override void Update(UpdateState state)
        {
            // Only switch Sims with Space if no text input has focus
            if (state.NewKeys.Contains(Keys.Space) && state.InputManager.GetFocus() == null)
            {
                var selected = Game.LotControl.ActiveEntity;
                var familyMembers = Game.vm.Context.ObjectQueries.Avatars.Where(x =>
                    Game.vm.TS1State.CurrentFamily.RuntimeSubset.Contains(x.Object.OBJ.GUID)
                        ).ToList();
                var index = familyMembers.IndexOf(selected);
                if (familyMembers.Count > 0)
                {
                    index = (index + 1) % (familyMembers.Count);
                    HITVM.Get().PlaySoundEvent(UISounds.QueueAdd);
                    Game.vm.SendCommand(new VMNetChangeControlCmd() { TargetID = familyMembers[index].ObjectID });
                }
            }

            base.Update(state);
            if (!Game.Desktop)
            {
                if (LastCut != Game.LotControl.WallsMode)
                {
                    LastCut = Game.LotControl.WallsMode;
                    var ui = Content.Get().CustomUI;
                    string cutImg = "cut_btn_down.png";
                    switch (LastCut)
                    {
                        case 1:
                            cutImg = "cut_btn_away.png"; break;
                        case 2:
                            cutImg = "cut_btn_up.png"; break;
                        case 3:
                            cutImg = "cut_btn_roof.png"; break;
                    }
                    CutBtn.Texture = ui.Get(cutImg).Get(GameFacade.GraphicsDevice);
                }
                if (Clock.TweenHook != ClockTween)
                {
                    ClockTween = Clock.TweenHook;
                    CutBtn.X = Game.ScreenWidth - (256 + (138f * ClockTween) + 15);
                    if (CutPanel != null) CutPanel.X = CutBtn.X - 39;
                }
                ModeSwitcher.LiveButton.Switching = MainPanel.ShowingSelect;
                ExtendPanelBtn.Visible = !MainPanel.PanelActive;
            }
        }

        public override void GameResized()
        {
            base.GameResized();
            if (!Game.Desktop)
            {
                CutBtn.X = Game.ScreenWidth - (256 + (138f * ClockTween) + 15);
                if (CutPanel != null) CutPanel.X = CutBtn.X - 39;
                Clock.X = Game.ScreenWidth - (334 + 15);
                Clock.Y = 15;
                Money.Position = new Vector2(15, Game.ScreenHeight - 172);
                var mode = ModeSwitcher;
                mode.Position = new Vector2(64 + 15, Game.ScreenHeight - (64 + 15));
                ExtendPanelBtn.Position = new Vector2(mode.X + 54, mode.Y - 50);
            }
            else
            {
                PlaceDesktopUCP();
                if (ClassicLive != null) PlaceClassicLive();
                if (ClassicMode != null) PlaceClassicMode();
                if (ClassicCamera != null) PlaceClassicCamera();
            }
            MainPanel.Y = Game.ScreenHeight - (128 + 15);
        }

        /// <summary>
        /// Replaces the desktop control panel, after the classic UI setting changes.
        /// </summary>
        public void RebuildDesktopUCP()
        {
            if (DesktopUCP == null) return;
            Remove(DesktopUCP);
            DesktopUCP = new UIDesktopUCP(Game);
            DesktopUCP.OnModeClick += LiveButtonClicked;
            DesktopUCP.OnCameraClick += ToggleCamera;
            CloseCamera();
            DesktopUCP.SetMode(MainPanel.Mode);
            PlaceDesktopUCP();
            Add(DesktopUCP);
            UpdateClassicLive();
            MainPanel.X = MainPanelX();
            MainPanel.GameResized();
        }

        /// <summary>
        /// Adds or removes the classic live strip to match the control panel.
        /// </summary>
        private void UpdateClassicLive()
        {
            var classic = DesktopUCP.Classic;
            if (classic == (ClassicLive != null)) return;
            if (classic)
            {
                ClassicLive = new UIClassicLivePanel(Game);
                PlaceClassicLive();
                ClassicLive.Visible = MainPanel.Mode == UIMainPanelMode.LIVE;
                //under the control panel, whose mode swoosh reaches over the strip's edge
                AddBefore(ClassicLive, DesktopUCP);
            }
            else
            {
                Remove(ClassicLive);
                ClassicLive = null;
                UpdateClassicMode(UIMainPanelMode.LIVE);
                if (MainPanel.Mode != UIMainPanelMode.LIVE)
                {
                    //back to the main panel for this mode
                    MainPanel.Open();
                    MainPanel.Switcher_OnCategorySelect(MainPanel.Switcher.ActiveCategory);
                }
            }
            if (classic) UpdateClassicMode(MainPanel.Mode);
        }

        /// <summary>
        /// If classic mode has its own panel for this mode (besides live mode's strip).
        /// </summary>
        private bool HasClassicMode(UIMainPanelMode mode)
        {
            return mode == UIMainPanelMode.OPTIONS || mode == UIMainPanelMode.BUY || mode == UIMainPanelMode.BUILD;
        }

        /// <summary>
        /// Shows the classic panel for this mode, replacing the last one.
        /// </summary>
        private void UpdateClassicMode(UIMainPanelMode mode)
        {
            CloseCamera();
            if (ClassicMode != null)
            {
                ClassicMode.Kill();
                Remove(ClassicMode);
                ClassicMode = null;
            }
            if (ClassicLive == null || !HasClassicMode(mode)) return;
            switch (mode)
            {
                case UIMainPanelMode.OPTIONS: ClassicMode = new UIClassicOptionsPanel(Game); break;
                case UIMainPanelMode.BUY: ClassicMode = new UIClassicBuyPanel(Game, MainPanel.GetLotType(false)); break;
                case UIMainPanelMode.BUILD: ClassicMode = new UIClassicBuildPanel(Game); break;
            }
            PlaceClassicMode();
            AddBefore(ClassicMode, DesktopUCP);
            if (MainPanel.PanelActive) MainPanel.Close();
        }

        private void PlaceClassicMode()
        {
            ClassicMode.Position = new Vector2(DesktopUCP.Size.X, Game.ScreenHeight - UIClassicStrip.StripHeight);
            ClassicMode.GameResized();
        }

        /// <summary>
        /// Classic camera mode, from live mode: the camera strip replaces the live strip, and the frame follows the
        /// mouse over the lot. The camera button or another mode closes it.
        /// </summary>
        public void ToggleCamera()
        {
            if (ClassicCamera != null)
            {
                CloseCamera();
                return;
            }
            if (ClassicLive == null || MainPanel.Mode != UIMainPanelMode.LIVE) return;
            HITVM.Get().PlaySoundEvent(UISounds.Click);
            ClassicCamera = new UIClassicCameraPanel(Game);
            PlaceClassicCamera();
            ClassicLive.Visible = false;
            AddBefore(ClassicCamera, DesktopUCP);
            //under the panels, over the lot
            CameraFrame = new UIClassicCameraFrame(Game);
            AddAt(0, CameraFrame);
            DesktopUCP.SetCamera(true);
        }

        private void CloseCamera()
        {
            if (ClassicCamera == null) return;
            Remove(ClassicCamera);
            Remove(CameraFrame);
            ClassicCamera = null;
            CameraFrame = null;
            if (ClassicLive != null) ClassicLive.Visible = MainPanel.Mode == UIMainPanelMode.LIVE;
            DesktopUCP?.SetCamera(false);
        }

        private void PlaceClassicCamera()
        {
            ClassicCamera.Position = new Vector2(DesktopUCP.Size.X, Game.ScreenHeight - UIClassicStrip.StripHeight);
            ClassicCamera.GameResized();
        }

        private void PlaceClassicLive()
        {
            ClassicLive.Position = new Vector2(DesktopUCP.Size.X, Game.ScreenHeight - UIClassicLivePanel.StripHeight);
            ClassicLive.GameResized();
        }

        //the main panel starts right of the control panel
        private float MainPanelX()
        {
            if (!Game.Desktop) return 64 + 15;
            return (DesktopUCP.Classic) ? DesktopUCP.Size.X : 64 + 15 + 100;
        }

        private void PlaceDesktopUCP()
        {
            //the classic panel sits in the corner, like the original game's
            DesktopUCP.Position = (DesktopUCP.Classic) ? new Vector2(0, Game.ScreenHeight - DesktopUCP.Size.Y) : new Vector2(15, Game.ScreenHeight - (278 + 15));
        }
    }
}
