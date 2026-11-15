using System;
using RetroFPS.UI.Menu;

namespace RetroFPS.Pause
{
    /// <summary>Exclusive pause presentation contexts.</summary>
    public enum PauseMenuState { Closed, Root, Settings, RevertPrompt, ConfirmMainMenu, Leaving }

    /// <summary>Engine-independent pause navigation, delayed commands and display safety deadline.</summary>
    public sealed class PauseMenuStateMachine
    {
        private enum Pending { None, Resume, Settings, MainMenu, Back, Keep, Revert, Leave }
        private readonly float pressSeconds;
        private readonly RevertCountdown countdown = new RevertCountdown();
        private Pending pending;
        private float pressRemaining;
        private MenuFocusTarget displayOpener;
        /// <summary>Current input context.</summary>
        public PauseMenuState State { get; private set; }
        /// <summary>Logical focus; Start maps to DEVAM and Quit maps to ANA MENÜ.</summary>
        public MenuFocusTarget FocusTarget { get; private set; }
        /// <summary>Whether press feedback or a noninteractive context owns input.</summary>
        public bool InputLocked => State == PauseMenuState.Closed || State == PauseMenuState.Leaving || pending != Pending.None;
        /// <summary>Display safety clock uses real time even while gameplay is frozen.</summary>
        public RevertCountdown Countdown => countdown;
        /// <summary>Whether to display localized scene-load recovery copy.</summary>
        public bool HasLoadError { get; private set; }
        /// <summary>True commits the display preview; false restores its committed snapshot.</summary>
        public event Action<bool> DisplayResolved;
        /// <summary>Normal resume, after settings have been resolved.</summary>
        public event Action ResumeRequested;
        /// <summary>Single departure request after confirmation feedback.</summary>
        public event Action LeaveRequested;

        /// <summary>Creates the machine without taking ownership of gameplay.</summary>
        public PauseMenuStateMachine(float pressSeconds = 0.08f) => this.pressSeconds = Math.Max(0f, pressSeconds);

        /// <summary>Shows Root only after PauseRuntime has acquired pause.</summary>
        public bool Open()
        {
            if (State != PauseMenuState.Closed) return false;
            HasLoadError = false;
            Change(PauseMenuState.Root, MenuFocusTarget.Start);
            return true;
        }

        /// <summary>Advances real-time deadlines, rechecking eligibility before any delayed action.</summary>
        public void Tick(float deltaSeconds, bool eligible = true)
        {
            if (State == PauseMenuState.Closed || State == PauseMenuState.Leaving) return;
            if (!eligible) { CancelForExclusiveFlow(); return; }
            float dt = Math.Max(0f, deltaSeconds);
            if (State == PauseMenuState.RevertPrompt)
            {
                countdown.Tick(dt);
                if (countdown.Expired) { ResolveDisplay(false); return; }
            }
            if (pending == Pending.None) return;
            pressRemaining -= dt;
            if (pressRemaining > 0f) return;
            Pending command = pending;
            pending = Pending.None;
            switch (command)
            {
                case Pending.Resume: CloseAndResume(); break;
                case Pending.Settings: Change(PauseMenuState.Settings, MenuFocusTarget.Language); break;
                case Pending.MainMenu: Change(PauseMenuState.ConfirmMainMenu, MenuFocusTarget.No); break;
                case Pending.Back:
                    if (State == PauseMenuState.Settings) Change(PauseMenuState.Root, MenuFocusTarget.Settings);
                    else if (State == PauseMenuState.ConfirmMainMenu) Change(PauseMenuState.Root, MenuFocusTarget.Quit);
                    else ResolveDisplay(false);
                    break;
                case Pending.Keep: ResolveDisplay(true); break;
                case Pending.Revert: ResolveDisplay(false); break;
                case Pending.Leave:
                    HasLoadError = false;
                    Change(PauseMenuState.Leaving, MenuFocusTarget.None);
                    LeaveRequested?.Invoke();
                    break;
            }
        }

        /// <summary>Requests DEVAM with fixed press feedback.</summary>
        public bool RequestResume() => Queue(Pending.Resume, PauseMenuState.Root);
        /// <summary>Requests the shared settings panel.</summary>
        public bool OpenSettings() => Queue(Pending.Settings, PauseMenuState.Root);
        /// <summary>Requests safe-default confirmation.</summary>
        public bool OpenMainMenuConfirm() => Queue(Pending.MainMenu, PauseMenuState.Root);
        /// <summary>Confirms a single scene departure.</summary>
        public bool ConfirmMainMenu() => Queue(Pending.Leave, PauseMenuState.ConfirmMainMenu);
        /// <summary>Back resumes at Root and otherwise ascends one context.</summary>
        public bool Back() => State == PauseMenuState.Root ? RequestResume() : Queue(Pending.Back, State);
        /// <summary>Start resumes from any unlocked open context, reverting an unresolved preview.</summary>
        public bool ToggleResume()
        {
            if (InputLocked) return false;
            CloseAndResume();
            return true;
        }
        /// <summary>Opens the ten-second modal and retains the actual display row opener.</summary>
        public bool OpenRevertPrompt(MenuFocusTarget opener, float seconds = 10f)
        {
            if (State != PauseMenuState.Settings || (opener != MenuFocusTarget.WindowMode && opener != MenuFocusTarget.Resolution)) return false;
            pending = Pending.None;
            displayOpener = opener;
            countdown.Start(seconds);
            Change(PauseMenuState.RevertPrompt, MenuFocusTarget.Revert);
            if (countdown.Expired) ResolveDisplay(false);
            return true;
        }
        /// <summary>Requests keeping the display preview; deadline takes precedence.</summary>
        public bool KeepDisplay() => Queue(Pending.Keep, PauseMenuState.RevertPrompt);
        /// <summary>Requests reverting the display preview.</summary>
        public bool RevertDisplay() => Queue(Pending.Revert, PauseMenuState.RevertPrompt);
        /// <summary>Discards delayed work and closes without requesting normal resume.</summary>
        public void CancelForExclusiveFlow()
        {
            if (State == PauseMenuState.RevertPrompt) DisplayResolved?.Invoke(false);
            countdown.Stop();
            pending = Pending.None;
            Change(PauseMenuState.Closed, MenuFocusTarget.None);
        }
        /// <summary>Restores Root after preflight or load failure; a fresh confirmation is required.</summary>
        public void LoadFailed()
        {
            if (State != PauseMenuState.Leaving) return;
            pending = Pending.None;
            HasLoadError = true;
            Change(PauseMenuState.Root, MenuFocusTarget.Quit);
            MenuEvents.RaiseUiAudio(UiAudioEventType.Error);
        }
        private bool Queue(Pending command, PauseMenuState required)
        {
            if (InputLocked || State != required) return false;
            pending = command;
            pressRemaining = pressSeconds;
            MenuEvents.RaiseUiAudio(command == Pending.Back || command == Pending.Revert ? UiAudioEventType.Back : UiAudioEventType.Confirm);
            return true;
        }
        private void CloseAndResume()
        {
            if (State == PauseMenuState.RevertPrompt) DisplayResolved?.Invoke(false);
            countdown.Stop();
            pending = Pending.None;
            Change(PauseMenuState.Closed, MenuFocusTarget.None);
            ResumeRequested?.Invoke();
        }
        private void ResolveDisplay(bool keep)
        {
            countdown.Stop();
            pending = Pending.None;
            Change(PauseMenuState.Settings, displayOpener);
            DisplayResolved?.Invoke(keep);
        }
        private void Change(PauseMenuState state, MenuFocusTarget focus) { State = state; FocusTarget = focus; }
    }
}
