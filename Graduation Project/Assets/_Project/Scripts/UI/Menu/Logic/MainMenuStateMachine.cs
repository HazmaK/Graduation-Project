using System;

namespace RetroFPS.UI.Menu
{
    /// <summary>The menu's mutually exclusive input contexts.</summary>
    public enum MainMenuState { FirstLoad, Root, Settings, RevertPrompt, QuitConfirm, Loading }
    /// <summary>Logical focus destinations; views map them to selectable objects.</summary>
    public enum MenuFocusTarget { None, Start, Settings, Quit, MasterVolume, WindowMode, Resolution, Revert, No }

    /// <summary>Caller-driven menu logic. Accepted buttons act after their 80 ms press feedback.</summary>
    public sealed class MainMenuStateMachine
    {
        private enum Pending { None, Settings, Quit, Start, Back, Keep, Revert, ConfirmQuit }
        private readonly float firstLoadSeconds;
        private readonly float pressSeconds;
        private readonly string sceneName;
        private readonly RevertCountdown countdown = new RevertCountdown();
        private float firstLoadElapsed;
        private float pressRemaining;
        private Pending pending;
        private MenuFocusTarget displayOpener;
        /// <summary>Gets the active context.</summary>
        public MainMenuState State { get; private set; } = MainMenuState.FirstLoad;
        /// <summary>Gets the focus destination after the latest transition.</summary>
        public MenuFocusTarget FocusTarget { get; private set; }
        /// <summary>Gets whether navigation and activation must be ignored.</summary>
        public bool InputLocked => State == MainMenuState.FirstLoad || State == MainMenuState.Loading || pending != Pending.None;
        /// <summary>Gets whether the menu should show its localized load error.</summary>
        public bool HasLoadError { get; private set; }
        /// <summary>Gets the display confirmation countdown.</summary>
        public RevertCountdown Countdown => countdown;
        /// <summary>Reports Keep (true) or Revert (false) to the settings adapter.</summary>
        public event Action<bool> DisplayResolved;
        /// <summary>Creates a machine with injected transition timings and gameplay scene.</summary>
        public MainMenuStateMachine(bool reduceMotion = false, float firstLoadSeconds = 0.6f, float pressSeconds = 0.08f, string sceneName = "TestLevel_Gauntlet")
        {
            this.firstLoadSeconds = MenuTween.ResolveDuration(firstLoadSeconds, reduceMotion, true);
            this.pressSeconds = Math.Max(0f, pressSeconds);
            this.sceneName = sceneName;
        }
        /// <summary>Advances first-load, button feedback, and display expiry without engine time.</summary>
        public void Tick(float deltaSeconds)
        {
            float dt = Math.Max(0f, deltaSeconds);
            if (State == MainMenuState.FirstLoad)
            {
                firstLoadElapsed += dt;
                if (firstLoadElapsed >= firstLoadSeconds) Change(MainMenuState.Root, MenuFocusTarget.Start);
                return;
            }
            // Deadline wins over an unfinished Keep press at the expiration boundary.
            if (State == MainMenuState.RevertPrompt)
            {
                countdown.Tick(dt);
                if (countdown.Expired) { pending = Pending.None; ResolveDisplay(false); return; }
            }
            if (pending == Pending.None) return;
            pressRemaining -= dt;
            if (pressRemaining > 0f) return;
            Pending action = pending;
            pending = Pending.None;
            switch (action)
            {
                case Pending.Settings: Change(MainMenuState.Settings, MenuFocusTarget.MasterVolume); break;
                case Pending.Quit: Change(MainMenuState.QuitConfirm, MenuFocusTarget.No); break;
                case Pending.Start:
                    HasLoadError = false;
                    Change(MainMenuState.Loading, MenuFocusTarget.None);
                    MenuEvents.RaiseStartRequested(sceneName);
                    break;
                case Pending.Back:
                    if (State == MainMenuState.Settings) Change(MainMenuState.Root, MenuFocusTarget.Settings);
                    else if (State == MainMenuState.QuitConfirm) Change(MainMenuState.Root, MenuFocusTarget.Quit);
                    else ResolveDisplay(false);
                    break;
                case Pending.Keep: ResolveDisplay(true); break;
                case Pending.Revert: ResolveDisplay(false); break;
                case Pending.ConfirmQuit: MenuEvents.RaiseQuitConfirmed(); break;
            }
        }
        /// <summary>Requests AYARLAR from the root.</summary>
        public bool OpenSettings() => Queue(Pending.Settings, MainMenuState.Root);
        /// <summary>Requests ÇIKIŞ from the root, defaulting to HAYIR.</summary>
        public bool OpenQuitConfirm() => Queue(Pending.Quit, MainMenuState.Root);
        /// <summary>Requests one gameplay start after the press feedback.</summary>
        public bool Start() => Queue(Pending.Start, MainMenuState.Root);
        /// <summary>Requests quit from the confirmation dialog.</summary>
        public bool ConfirmQuit() => Queue(Pending.ConfirmQuit, MainMenuState.QuitConfirm);
        /// <summary>Requests Back; root and locked contexts are no-ops.</summary>
        public bool Back()
        {
            if (State != MainMenuState.Settings && State != MainMenuState.QuitConfirm && State != MainMenuState.RevertPrompt) return false;
            return Queue(Pending.Back, State);
        }
        /// <summary>Opens a display prompt, superseding a pending Settings press and remembering its row.</summary>
        public bool OpenRevertPrompt(MenuFocusTarget opener, float seconds = 10f)
        {
            if (State != MainMenuState.Settings || (opener != MenuFocusTarget.WindowMode && opener != MenuFocusTarget.Resolution)) return false;
            pending = Pending.None;
            pressRemaining = 0f;
            displayOpener = opener;
            countdown.Start(seconds);
            Change(MainMenuState.RevertPrompt, MenuFocusTarget.Revert);
            if (countdown.Expired) ResolveDisplay(false);
            return true;
        }
        /// <summary>Requests keeping the pending display settings.</summary>
        public bool KeepDisplay() => Queue(Pending.Keep, MainMenuState.RevertPrompt);
        /// <summary>Requests reverting the pending display settings.</summary>
        public bool RevertDisplay() => Queue(Pending.Revert, MainMenuState.RevertPrompt);
        /// <summary>Consumes a load-failure notification, allowing a fresh Start attempt without republishing it.</summary>
        public void LoadFailed()
        {
            if (State != MainMenuState.Loading) return;
            pending = Pending.None;
            HasLoadError = true;
            Change(MainMenuState.Root, MenuFocusTarget.Start);
            MenuEvents.RaiseUiAudio(UiAudioEventType.Error);
        }
        private bool Queue(Pending action, MainMenuState required)
        {
            if (InputLocked || State != required) return false;
            pending = action;
            pressRemaining = pressSeconds;
            MenuEvents.RaiseUiAudio(action == Pending.Back || action == Pending.Revert ? UiAudioEventType.Back : UiAudioEventType.Confirm);
            return true;
        }
        private void ResolveDisplay(bool keep)
        {
            countdown.Stop();
            Change(MainMenuState.Settings, displayOpener);
            DisplayResolved?.Invoke(keep);
        }
        private void Change(MainMenuState state, MenuFocusTarget focus)
        {
            State = state;
            FocusTarget = focus;
            MenuEvents.RaisePanelChanged(state);
        }
    }
}
