using System.Globalization;
using RetroFPS.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroFPS.UI.Menu
{
    /// <summary>Adapts the menu state machine to its four prefab instances and input contexts.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MenuTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private CanvasGroup settingsGroup;
        [SerializeField] private CanvasGroup dimGroup;
        [SerializeField] private UnityEngine.UI.Selectable[] rootItems;
        [SerializeField] private UnityEngine.UI.Selectable[] settingsItems;
        [SerializeField] private ConfirmDialogView quitDialog;
        [SerializeField] private ConfirmDialogView revertDialog;
        [SerializeField] private SettingsPanelView settingsPanel;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private TMP_Text loadError;
        private MainMenuStateMachine machine;
        private MainMenuState shownState = (MainMenuState)(-1);
        private MenuFocusVisual[] visuals;
        private readonly MenuTween panelTween = new MenuTween();
        private CanvasGroup[] groups;
        private float[] alphaFrom;
        private float[] alphaTo;
        private bool hasStarted;

        /// <summary>All activation, pointer and navigation input must respect this gate.</summary>
        public bool InputLocked => machine == null || machine.InputLocked || fader.IsFading;
        /// <summary>Whether live settings requests may be accepted.</summary>
        public bool CanEditSettings => !InputLocked && machine.State == MainMenuState.Settings;
        /// <summary>Display confirmation may supersede a pending Settings press, but never a fade.</summary>
        public bool CanPreviewDisplay => machine != null && machine.State == MainMenuState.Settings && !fader.IsFading;
        /// <summary>Safe focus destination in the active context.</summary>
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
                    case MenuFocusTarget.MasterVolume: return settingsItems[0];
                    case MenuFocusTarget.WindowMode: return settingsItems[5];
                    case MenuFocusTarget.Resolution: return settingsItems[6];
                    case MenuFocusTarget.No: return quitDialog.Negative;
                    case MenuFocusTarget.Revert: return revertDialog.Negative;
                    default: return null;
                }
            }
        }

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            GameSettings.EnsureInitialized();
            machine = new MainMenuStateMachine(GameSettings.Current.ReduceMotion, theme.FirstLoadSeconds, theme.PressSeconds);
            groups = new[] { rootGroup, settingsGroup, quitDialog.Group, revertDialog.Group, dimGroup };
            alphaFrom = new float[groups.Length];
            alphaTo = new float[groups.Length];
            visuals = rootGroup.transform.root.GetComponentsInChildren<MenuFocusVisual>(true);
            for (int i = 0; i < visuals.Length; i++) visuals[i].Bind(this);
            settingsPanel.Bind(this);
            rootItems[0].GetComponent<UnityEngine.UI.Button>().onClick.AddListener(StartRun);
            rootItems[1].GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OpenSettings);
            rootItems[2].GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OpenQuit);
            settingsItems[8].GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Back);
            quitDialog.Affirmative.onClick.AddListener(ConfirmQuit);
            quitDialog.Negative.onClick.AddListener(Back);
            revertDialog.Affirmative.onClick.AddListener(KeepDisplay);
            revertDialog.Negative.onClick.AddListener(RevertDisplay);
            machine.DisplayResolved += settingsPanel.ResolveDisplay;
            loadError.text = strings.Get("menu.load_error");
            loadError.color = theme.Danger;
            versionLabel.text = string.IsNullOrEmpty(Application.version) ? string.Empty
                : string.Format(CultureInfo.InvariantCulture, strings.Get("menu.version_format"), Application.version);
        }

        private void OnEnable()
        {
            MenuEvents.PanelChanged += OnPanelChanged;
            MenuEvents.LoadFailed += OnLoadFailed;
        }
        private void OnDisable()
        {
            MenuEvents.PanelChanged -= OnPanelChanged;
            MenuEvents.LoadFailed -= OnLoadFailed;
            if (hasStarted && GameSettings.Service.HasPendingDisplay) GameSettings.Service.RevertDisplay();
        }
        private void Start()
        {
            hasStarted = true;
            fader.Fade(false, theme.FirstLoadSeconds);
            RenderState();
        }
        private void Update()
        {
            machine.Tick(Time.unscaledDeltaTime);
            if (shownState != machine.State) RenderState();
            if (GameSettings.Current.ReduceMotion && !panelTween.Complete) panelTween.Tick(float.MaxValue);
            panelTween.Tick(Time.unscaledDeltaTime);
            for (int i = 0; i < groups.Length; i++)
                groups[i].alpha = Mathf.LerpUnclamped(alphaFrom[i], alphaTo[i], panelTween.Value);
            ApplyInputGate();
            if (machine.State == MainMenuState.RevertPrompt) revertDialog.ShowCountdown(machine.Countdown);
        }

        private void OnPanelChanged(MainMenuState state)
        {
            if (state == MainMenuState.Loading)
            {
                fader.Fade(true, theme.StartFadeSeconds, true);
                MenuEvents.RaiseUiAudio(UiAudioEventType.StartRun);
            }
            // Render after Tick returns: display resolution callbacks must finish first.
        }

        private void RenderState()
        {
            MainMenuState previous = shownState;
            shownState = machine.State;
            if (shownState == MainMenuState.Settings && previous != MainMenuState.RevertPrompt) settingsPanel.Open();
            if (previous == MainMenuState.Settings && shownState == MainMenuState.Root) GameSettings.Save();
            bool root = shownState == MainMenuState.Root || shownState == MainMenuState.FirstLoad
                || shownState == MainMenuState.Loading || shownState == MainMenuState.QuitConfirm;
            alphaTo[0] = root ? 1f : 0f;
            alphaTo[1] = shownState == MainMenuState.Settings || shownState == MainMenuState.RevertPrompt ? 1f : 0f;
            alphaTo[2] = shownState == MainMenuState.QuitConfirm ? 1f : 0f;
            alphaTo[3] = shownState == MainMenuState.RevertPrompt ? 1f : 0f;
            alphaTo[4] = alphaTo[2] + alphaTo[3];
            for (int i = 0; i < groups.Length; i++) alphaFrom[i] = groups[i].alpha;
            panelTween.Start(MenuTween.ResolveDuration(
                shownState == MainMenuState.QuitConfirm || shownState == MainMenuState.RevertPrompt
                    ? theme.DialogSeconds : theme.PanelSeconds, GameSettings.Current.ReduceMotion, false), MenuEase.EaseInOut);
            loadError.gameObject.SetActive(machine.HasLoadError);
            ApplyInputGate();
            eventSystem.SetSelectedGameObject(null);
            UnityEngine.UI.Selectable target = DefaultFocus;
            if (target != null) eventSystem.SetSelectedGameObject(target.gameObject);
            if (shownState == MainMenuState.QuitConfirm || shownState == MainMenuState.RevertPrompt)
                MenuEvents.RaiseUiAudio(UiAudioEventType.DialogOpen);
        }

        private void ApplyInputGate()
        {
            bool open = !InputLocked;
            SetInput(rootGroup, open && machine.State == MainMenuState.Root);
            SetInput(settingsGroup, open && machine.State == MainMenuState.Settings);
            SetInput(quitDialog.Group, open && machine.State == MainMenuState.QuitConfirm);
            SetInput(revertDialog.Group, open && machine.State == MainMenuState.RevertPrompt);
            dimGroup.blocksRaycasts = machine.State == MainMenuState.QuitConfirm || machine.State == MainMenuState.RevertPrompt;
            eventSystem.sendNavigationEvents = open;
            fader.BlockInput(!open);
        }
        private static void SetInput(CanvasGroup group, bool enabled)
        {
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
        private void PressSelected(bool accepted)
        {
            if (!accepted) return;
            GameObject selected = eventSystem.currentSelectedGameObject;
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i].gameObject == selected) { visuals[i].Press(); break; }
            ApplyInputGate();
        }
        private void StartRun() { if (!InputLocked) PressSelected(machine.Start()); }
        private void OpenSettings() { if (!InputLocked) PressSelected(machine.OpenSettings()); }
        private void OpenQuit() { if (!InputLocked) PressSelected(machine.OpenQuitConfirm()); }
        private void ConfirmQuit() { if (!InputLocked) PressSelected(machine.ConfirmQuit()); }
        private void KeepDisplay() { if (!InputLocked) PressSelected(machine.KeepDisplay()); }
        private void RevertDisplay() { if (!InputLocked) PressSelected(machine.RevertDisplay()); }

        /// <summary>Routes Back through the active context; root and locked states are inert.</summary>
        public void Back() { if (!InputLocked) PressSelected(machine.Back()); }
        /// <summary>Starts the ten-second safe-default display confirmation context.</summary>
        public bool OpenDisplayPrompt(MenuFocusTarget opener)
        {
            if (!CanPreviewDisplay || !machine.OpenRevertPrompt(opener, theme.RevertSeconds)) return false;
            RenderState();
            return true;
        }
        /// <summary>Looks up a cached selectable only inside the current focus trap.</summary>
        public UnityEngine.UI.Selectable FindActive(GameObject candidate)
        {
            if (candidate == null) return null;
            if (machine.State == MainMenuState.QuitConfirm)
                return MatchDialog(quitDialog, candidate);
            if (machine.State == MainMenuState.RevertPrompt)
                return MatchDialog(revertDialog, candidate);
            UnityEngine.UI.Selectable[] items = machine.State == MainMenuState.Settings ? settingsItems : rootItems;
            for (int i = 0; i < items.Length; i++)
                if (items[i].gameObject == candidate && items[i].IsInteractable()) return items[i];
            return null;
        }
        private static UnityEngine.UI.Selectable MatchDialog(ConfirmDialogView dialog, GameObject candidate)
        {
            if (dialog.Negative.gameObject == candidate) return dialog.Negative;
            return dialog.Affirmative.gameObject == candidate ? dialog.Affirmative : null;
        }
        private void OnLoadFailed()
        {
            if (machine.State != MainMenuState.Loading) return;
            machine.LoadFailed();
            fader.Fade(false, theme.FirstLoadSeconds);
            RenderState();
        }
    }
}
