using RetroFPS.Settings;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroFPS.Pause
{
    /// <summary>Pause presentation adapter; gameplay time, cursor and loading belong to PauseRuntime.</summary>
    public sealed class PauseMenuController : MonoBehaviour, IPauseMenuRequestSink, IMenuInputContext, ISettingsPanelHost
    {
        [SerializeField] private MenuTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private CanvasGroup scrimGroup;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private UnityEngine.UI.Selectable[] rootItems;
        [SerializeField] private SettingsPanelView settingsPanel;
        [SerializeField] private ConfirmDialogView mainMenuDialog;
        [SerializeField] private ConfirmDialogView revertDialog;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private TMP_Text loadError;
        [SerializeField] private MenuInputRouter inputRouter;
        private PauseRuntime runtime;
        private HudEventSystemGuard eventSystemGuard;
        private EventSystem eventSystem;
        private BaseEventData focusEvent;
        private PauseMenuStateMachine machine;
        private PauseMenuState shownState = (PauseMenuState)(-1);
        private MenuFocusVisual[] visuals;
        private UnityEngine.UI.Selectable[] settingsItems;
        private CanvasGroup[] panels;
        private float[] alphaFrom, alphaTo;
        private readonly MenuTween panelTween = new MenuTween();
        private bool needsFocus, loadIssued, initialized;
        private int screenWidth, screenHeight;
        private Rect screenSafe;

        /// <summary>Current presentation state.</summary>
        public PauseMenuState State => machine != null ? machine.State : PauseMenuState.Closed;
        /// <inheritdoc/>
        public bool InputLocked => machine == null || machine.InputLocked || fader.IsFading || !panelTween.Complete;
        /// <inheritdoc/>
        public bool CanEditSettings => !InputLocked && State == PauseMenuState.Settings;
        /// <inheritdoc/>
        public bool CanPreviewDisplay => machine != null && State == PauseMenuState.Settings && !fader.IsFading && panelTween.Complete;
        /// <inheritdoc/>
        public UnityEngine.UI.Selectable DefaultFocus
        {
            get
            {
                if (machine == null) return null;
                switch (machine.FocusTarget)
                {
                    case MenuFocusTarget.Start: return rootItems[0];
                    case MenuFocusTarget.Settings: return rootItems[1];
                    case MenuFocusTarget.Quit: return rootItems[2];
                    case MenuFocusTarget.Language: return settingsItems[0];
                    case MenuFocusTarget.WindowMode: return settingsItems[6];
                    case MenuFocusTarget.Resolution: return settingsItems[7];
                    case MenuFocusTarget.No: return mainMenuDialog.Negative;
                    case MenuFocusTarget.Revert: return revertDialog.Negative;
                    default: return null;
                }
            }
        }
        /// <summary>Player injection before the first gameplay input sample.</summary>
        public void Bind(PauseRuntime owner, HudEventSystemGuard guard)
        {
            if (runtime != null) runtime.SceneLeaveFailed -= OnSceneLeaveFailed;
            runtime = owner;
            eventSystemGuard = guard;
            if (!initialized) Initialize();
            if (runtime != null) runtime.SceneLeaveFailed += OnSceneLeaveFailed;
            BindEventSystem();
        }
        private void Initialize()
        {
            initialized = true;
            GameSettings.EnsureInitialized();
            machine = new PauseMenuStateMachine(theme.PressSeconds);
            settingsItems = settingsPanel.OrderedSelectables;
            focusEvent = new BaseEventData(eventSystemGuard != null ? eventSystemGuard.BundledSystem : EventSystem.current);
            settingsPanel.Bind(this);
            visuals = GetComponentsInChildren<MenuFocusVisual>(true);
            for (int i = 0; i < visuals.Length; i++) visuals[i].Bind(this);
            panels = new[] { rootGroup, settingsPanel.Group, mainMenuDialog.Group, revertDialog.Group, dimGroup };
            alphaFrom = new float[panels.Length];
            alphaTo = new float[panels.Length];
            machine.DisplayResolved += settingsPanel.ResolveDisplay;
            machine.ResumeRequested += ResumeRuntime;
            machine.LeaveRequested += BeginLeave;
            ((UnityEngine.UI.Button)rootItems[0]).onClick.AddListener(ResumeButton);
            ((UnityEngine.UI.Button)rootItems[1]).onClick.AddListener(OpenSettings);
            ((UnityEngine.UI.Button)rootItems[2]).onClick.AddListener(OpenMainMenu);
            ((UnityEngine.UI.Button)settingsItems[9]).onClick.AddListener(Back);
            mainMenuDialog.Affirmative.onClick.AddListener(ConfirmMainMenu);
            mainMenuDialog.Negative.onClick.AddListener(Back);
            revertDialog.Affirmative.onClick.AddListener(KeepDisplay);
            revertDialog.Negative.onClick.AddListener(RevertDisplay);
            GameSettings.Changed += RefreshLanguage;
            WarmError();
            RenderState();
        }
        private void OnDisable()
        {
            if (!initialized) return;
            bool leaving = State == PauseMenuState.Leaving;
            CancelForExclusiveFlow();
            if (!leaving && runtime != null) runtime.CancelForExclusiveFlow();
        }
        private void OnDestroy()
        {
            if (runtime != null) runtime.SceneLeaveFailed -= OnSceneLeaveFailed;
            if (!initialized) return;
            GameSettings.Changed -= RefreshLanguage;
            machine.DisplayResolved -= settingsPanel.ResolveDisplay;
            machine.ResumeRequested -= ResumeRuntime;
            machine.LeaveRequested -= BeginLeave;
            ((UnityEngine.UI.Button)rootItems[0]).onClick.RemoveListener(ResumeButton);
            ((UnityEngine.UI.Button)rootItems[1]).onClick.RemoveListener(OpenSettings);
            ((UnityEngine.UI.Button)rootItems[2]).onClick.RemoveListener(OpenMainMenu);
            ((UnityEngine.UI.Button)settingsItems[9]).onClick.RemoveListener(Back);
            mainMenuDialog.Affirmative.onClick.RemoveListener(ConfirmMainMenu);
            mainMenuDialog.Negative.onClick.RemoveListener(Back);
            revertDialog.Affirmative.onClick.RemoveListener(KeepDisplay);
            revertDialog.Negative.onClick.RemoveListener(RevertDisplay);
        }
        private void WarmError()
        {
            loadError.vertexBufferAutoSizeReduction = false;
            loadError.text = strings.Get("pause.main_menu_load_error", LanguageSetting.Turkish);
            loadError.ForceMeshUpdate(true);
            loadError.text = strings.Get("pause.main_menu_load_error", LanguageSetting.English);
            loadError.ForceMeshUpdate(true);
            RefreshLanguage(GameSettings.Current);
        }
        private void RefreshLanguage(GameSettingsData data)
        {
            loadError.text = strings.Get("pause.main_menu_load_error", data.Language);
            if (data.ReduceMotion && fader.IsFading) fader.Fade(State == PauseMenuState.Leaving, 0.1f);
        }
        private void BindEventSystem()
        {
            EventSystem next = eventSystemGuard != null ? eventSystemGuard.ActiveSystem : EventSystem.current;
            if (eventSystem == next) return;
            eventSystem = next;
            inputRouter.BindEventSystem(next);
            if (State != PauseMenuState.Closed) needsFocus = true;
        }
        private void Update()
        {
            if (!initialized) return;
            UpdateSafeArea();
            BindEventSystem();
            machine.Tick(Time.unscaledDeltaTime, runtime != null && (runtime.CanPause || State == PauseMenuState.Leaving));
            if (shownState != State) RenderState();
            if (GameSettings.Current.ReduceMotion && !panelTween.Complete) panelTween.Tick(float.MaxValue);
            panelTween.Tick(Time.unscaledDeltaTime);
            for (int i = 0; i < panels.Length; i++) panels[i].alpha = Mathf.LerpUnclamped(alphaFrom[i], alphaTo[i], panelTween.Value);
            ApplyInputGate();
            if (State == PauseMenuState.RevertPrompt) revertDialog.ShowCountdown(machine.Countdown);
            if (needsFocus) Focus();
            if (State == PauseMenuState.Leaving && !fader.IsFading && !loadIssued)
            {
                loadIssued = true;
                runtime.LoadMainMenu();
            }
        }
        /// <inheritdoc/>
        public void Open()
        {
            if (runtime == null || runtime.Service == null || !runtime.Service.IsPaused || !machine.Open()) return;
            fader.enabled = true;
            if (fader.IsFading) fader.Fade(false, 0f);
            loadIssued = false;
            BindEventSystem();
            RenderState();
        }
        /// <inheritdoc/>
        public void Back() { if (!InputLocked) PressSelected(machine.Back()); }
        /// <inheritdoc/>
        public void Resume() { if (!InputLocked && machine.ToggleResume()) RenderState(); }
        /// <inheritdoc/>
        public void CancelForExclusiveFlow()
        {
            if (!initialized || State == PauseMenuState.Closed) return;
            machine.CancelForExclusiveFlow();
            ResolveSettings();
            RenderState();
        }
        /// <inheritdoc/>
        public bool OpenDisplayPrompt(MenuFocusTarget opener)
        {
            if (!CanPreviewDisplay || !machine.OpenRevertPrompt(opener, 10f)) return false;
            RenderState();
            return true;
        }
        private void ResolveSettings()
        {
            if (GameSettings.Service.HasPendingDisplay) GameSettings.Service.RevertDisplay();
            GameSettings.Save();
        }
        private void ResumeRuntime() { ResolveSettings(); runtime.Resume(); }
        private void BeginLeave()
        {
            ResolveSettings();
            if (!runtime.TryPrepareSceneLeave())
            {
                if (State == PauseMenuState.Leaving && runtime.Service.IsPaused) OnSceneLeaveFailed();
                return;
            }
            loadIssued = false;
            fader.Fade(true, theme.StartFadeSeconds);
            RenderState();
        }
        private void OnSceneLeaveFailed()
        {
            if (State != PauseMenuState.Leaving) return;
            machine.LoadFailed();
            loadIssued = false;
            fader.Fade(false, theme.FirstLoadSeconds);
            RenderState();
        }
        private void ResumeButton() { if (!InputLocked) PressSelected(machine.RequestResume()); }
        private void OpenSettings() { if (!InputLocked) PressSelected(machine.OpenSettings()); }
        private void OpenMainMenu() { if (!InputLocked) PressSelected(machine.OpenMainMenuConfirm()); }
        private void ConfirmMainMenu() { if (!InputLocked) PressSelected(machine.ConfirmMainMenu()); }
        private void KeepDisplay() { if (!InputLocked) PressSelected(machine.KeepDisplay()); }
        private void RevertDisplay() { if (!InputLocked) PressSelected(machine.RevertDisplay()); }
        private void PressSelected(bool accepted)
        {
            if (!accepted) return;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i].gameObject == selected) { visuals[i].Press(); break; }
            ApplyInputGate();
        }
        private void RenderState()
        {
            PauseMenuState previous = shownState;
            shownState = State;
            if (State == PauseMenuState.Settings && previous != PauseMenuState.RevertPrompt) settingsPanel.Open();
            if (previous == PauseMenuState.Settings && State == PauseMenuState.Root) GameSettings.Save();
            bool visible = State != PauseMenuState.Closed;
            scrimGroup.alpha = visible ? 1f : 0f;
            scrimGroup.blocksRaycasts = visible;
            inputRouter.enabled = visible;
            if (!visible)
            {
                // Stop a recovery fade synchronously before an exclusive death/transition view takes focus.
                fader.enabled = false;
                fadeGroup.alpha = 0f;
                fadeGroup.blocksRaycasts = false;
            }
            alphaTo[0] = State == PauseMenuState.Root || State == PauseMenuState.ConfirmMainMenu || State == PauseMenuState.Leaving ? 1f : 0f;
            alphaTo[1] = State == PauseMenuState.Settings || State == PauseMenuState.RevertPrompt ? 1f : 0f;
            alphaTo[2] = State == PauseMenuState.ConfirmMainMenu ? 1f : 0f;
            alphaTo[3] = State == PauseMenuState.RevertPrompt ? 1f : 0f;
            alphaTo[4] = alphaTo[2] + alphaTo[3];
            for (int i = 0; i < panels.Length; i++) alphaFrom[i] = panels[i].alpha;
            float seconds = previous == PauseMenuState.Closed || State == PauseMenuState.Closed ? 0f
                : MenuTween.ResolveDuration(theme.PanelSeconds, GameSettings.Current.ReduceMotion, false);
            panelTween.Start(seconds, MenuEase.EaseInOut);
            for (int i = 0; i < panels.Length; i++) panels[i].alpha = Mathf.LerpUnclamped(alphaFrom[i], alphaTo[i], panelTween.Value);
            loadError.gameObject.SetActive(machine.HasLoadError && visible);
            ApplyInputGate();
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null, focusEvent);
                if (!visible) eventSystem.sendNavigationEvents = true;
            }
            needsFocus = visible && State != PauseMenuState.Leaving;
            Focus();
            if (State == PauseMenuState.ConfirmMainMenu || State == PauseMenuState.RevertPrompt)
                MenuEvents.RaiseUiAudio(UiAudioEventType.DialogOpen);
        }
        private void ApplyInputGate()
        {
            bool open = !InputLocked;
            SetInput(rootGroup, open && State == PauseMenuState.Root);
            SetInput(settingsPanel.Group, open && State == PauseMenuState.Settings);
            SetInput(mainMenuDialog.Group, open && State == PauseMenuState.ConfirmMainMenu);
            SetInput(revertDialog.Group, open && State == PauseMenuState.RevertPrompt);
            dimGroup.blocksRaycasts = State == PauseMenuState.ConfirmMainMenu || State == PauseMenuState.RevertPrompt;
            if (eventSystem != null && State != PauseMenuState.Closed) eventSystem.sendNavigationEvents = open;
            if (State == PauseMenuState.Closed) fadeGroup.blocksRaycasts = false;
            else fader.BlockInput(!open);
        }
        private static void SetInput(CanvasGroup group, bool enabled) { group.interactable = enabled; group.blocksRaycasts = enabled; }
        private void Focus()
        {
            if (!needsFocus || eventSystem == null) return;
            UnityEngine.UI.Selectable target = DefaultFocus;
            if (target == null) return;
            eventSystem.SetSelectedGameObject(target.gameObject, focusEvent);
            needsFocus = eventSystem.currentSelectedGameObject != target.gameObject;
        }
        /// <inheritdoc/>
        public UnityEngine.UI.Selectable FindActive(GameObject candidate)
        {
            if (candidate == null || State == PauseMenuState.Closed || State == PauseMenuState.Leaving) return null;
            if (State == PauseMenuState.ConfirmMainMenu) return Match(mainMenuDialog, candidate);
            if (State == PauseMenuState.RevertPrompt) return Match(revertDialog, candidate);
            UnityEngine.UI.Selectable[] items = State == PauseMenuState.Settings ? settingsItems : rootItems;
            for (int i = 0; i < items.Length; i++)
                if (items[i].gameObject == candidate && items[i].IsInteractable()) return items[i];
            return null;
        }
        private static UnityEngine.UI.Selectable Match(ConfirmDialogView dialog, GameObject candidate)
        {
            if (dialog.Negative.gameObject == candidate) return dialog.Negative;
            return dialog.Affirmative.gameObject == candidate ? dialog.Affirmative : null;
        }
        private void UpdateSafeArea()
        {
            Rect current = Screen.safeArea;
            if (screenWidth == Screen.width && screenHeight == Screen.height && screenSafe == current) return;
            screenWidth = Screen.width; screenHeight = Screen.height; screenSafe = current;
            if (screenWidth <= 0 || screenHeight <= 0 || safeArea == null) return;
            safeArea.anchorMin = new Vector2(Mathf.Max(0.05f, current.xMin / screenWidth), Mathf.Max(0.05f, current.yMin / screenHeight));
            safeArea.anchorMax = new Vector2(Mathf.Min(0.95f, current.xMax / screenWidth), Mathf.Min(0.95f, current.yMax / screenHeight));
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
    }
}
