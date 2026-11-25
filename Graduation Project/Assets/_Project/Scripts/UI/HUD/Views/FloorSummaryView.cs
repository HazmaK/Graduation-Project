using RetroFPS.Pause;
using RetroFPS.Presentation;
using RetroFPS.Run;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RetroFPS.UI.HUD
{
    /// <summary>Full-screen floor-cleared summary modal reusing the exclusive pause and focus patterns.</summary>
    public sealed class FloorSummaryView : MonoBehaviour, IPauseEnvironment
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private StringTable strings;
        [SerializeField] private GameObject summaryLayer;
        [SerializeField] private CanvasGroup cardGroup, gameplayGroup;
        [SerializeField] private HudEventSystemGuard eventSystemGuard;
        [SerializeField] private TMP_Text[] statLabels;
        [SerializeField] private TMP_Text[] statValues;
        [SerializeField] private TMP_Text cardsLabel, cardsValue;
        [SerializeField] private UnityEngine.UI.Button continueButton, mainMenuButton;
        [SerializeField] private TMP_Text continueLabel, mainMenuLabel;
        [SerializeField] private UnityEngine.UI.Image continueSeal, continueUnderline;
        [SerializeField] private UnityEngine.UI.Image mainMenuSeal, mainMenuUnderline;

        private readonly MenuTween fade = new MenuTween();
        private readonly RunSummaryRow[] rowBuffer = new RunSummaryRow[RunSummaryModel.TotalRowCount];
        private PauseRuntime pauseRuntime;
        private IPauseService previous;
        private CardOfferPauseService exclusive;
        private PauseSnapshot restoration;
        private BaseEventData focusEvent;
        private RunSummary currentSummary;
        private double armAt;
        private int releasedInputs;
        private int selectedIndex;
        private int renderedSelection = -1;
        private bool isOpen, ownsEnvironment, actionConfirmed, reduceMotion, needsFocus, stickHeld, initialized, buttonsBound;
        private bool warnedMenuLeaveFailure;

        /// <summary>Gets whether the floor summary modal is currently open.</summary>
        public bool IsOpen => isOpen;

        private void Awake() => BindButtons();

        private void OnDestroy()
        {
            Close();
            if (buttonsBound)
            {
                if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
                if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(MainMenu);
                buttonsBound = false;
            }
        }

        /// <summary>Initializes button bindings and restores authored hidden/open state.</summary>
        public void Initialize(PauseRuntime runtime = null)
        {
            if (runtime != null) pauseRuntime = runtime;
            BindButtons();
            if (!initialized)
            {
                initialized = true;
                focusEvent = new BaseEventData(eventSystemGuard != null ? eventSystemGuard.BundledSystem : EventSystem.current);
            }
            if (!isOpen && RunEvents.IsFloorSummaryOpen) Open(RunEvents.LastFloorSummary);
            else if (summaryLayer != null) summaryLayer.SetActive(isOpen);
        }

        private void BindButtons()
        {
            if (buttonsBound) return;
            buttonsBound = true;
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(MainMenu);
        }

        private void OnEnable()
        {
            RunEvents.OnFloorCleared += Open;
            RunEvents.OnFloorContinued += HandleFloorContinued;
            RunEvents.OnRunChanged += HandleRunChanged;
            RunEvents.OnTrueDeath += HandleTrueDeath;
            GameSettings.Changed += SettingsChanged;
            if (RunEvents.IsFloorSummaryOpen && !isOpen) Open(RunEvents.LastFloorSummary);
        }

        private void OnDisable()
        {
            RunEvents.OnFloorCleared -= Open;
            RunEvents.OnFloorContinued -= HandleFloorContinued;
            RunEvents.OnRunChanged -= HandleRunChanged;
            RunEvents.OnTrueDeath -= HandleTrueDeath;
            GameSettings.Changed -= SettingsChanged;
            Close();
        }

        private void Open(RunSummary summary)
        {
            if (isOpen || summaryLayer == null || RunEvents.CurrentRun?.Phase == RunPhase.TrueDeath) return;
            if (!initialized)
            {
                Initialize(pauseRuntime);
                if (isOpen) return;
            }
            GamePause.Service?.CancelForExclusiveFlow();
            previous = GamePause.Service;
            if (HitstopManager.Instance != null) HitstopManager.Instance.CancelHitstop();
            restoration = Capture();
            exclusive = new CardOfferPauseService(previous);
            ownsEnvironment = true;
            GamePause.SetService(exclusive);
            Apply(new PauseSnapshot(0f, restoration.FixedDeltaTime, true, 0, true));

            currentSummary = summary;
            isOpen = true;
            actionConfirmed = false;
            warnedMenuLeaveFailure = false;
            releasedInputs = 0;
            selectedIndex = 0;
            renderedSelection = -1;
            needsFocus = true;
            double armSeconds = config != null ? config.DeathArmSeconds : 0.6d;
            armAt = Time.unscaledTimeAsDouble + armSeconds;
            Gamepad pad = Gamepad.current;
            stickHeld = pad != null && Mathf.Abs(pad.leftStick.x.ReadValue()) > 0.5f;

            fade.Start(reduceMotion || config == null ? 0f : config.DeathFadeMs * 0.001f);
            if (cardGroup != null) cardGroup.alpha = reduceMotion ? 1f : fade.Value;
            if (gameplayGroup != null) gameplayGroup.alpha = 0f;

            RefreshSummaryText();
            summaryLayer.SetActive(true);
            UpdateButtonsInteractable(CanConfirm(Time.unscaledTimeAsDouble));
            UpdateFocusVisuals();
            Focus();
        }

        private void HandleFloorContinued() => Close();
        private void HandleRunChanged(RunState run) => Close();

        private void HandleTrueDeath()
        {
            Close();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SettingsChanged(GameSettingsData data)
        {
            if (isOpen) RefreshSummaryText();
        }

        /// <summary>Updates localized summary rows when the active language changes while open.</summary>
        public void RefreshLanguage(LanguageSetting language)
        {
            if (isOpen) RefreshSummaryText(language);
        }

        /// <summary>Snaps the entrance fade immediately under reduced motion without bypassing arming.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value;
            if (!value) return;
            fade.Start(0f);
            if (isOpen && cardGroup != null) cardGroup.alpha = 1f;
        }

        private void RefreshSummaryText() => RefreshSummaryText(GameSettings.Current.Language);

        private void RefreshSummaryText(LanguageSetting language)
        {
            RunSummaryModel.BuildRows(in currentSummary, strings, language, rowBuffer);
            RefreshStatSlots(statLabels, true);
            RefreshStatSlots(statValues, false);
            RunSummaryRow cardsRow = rowBuffer[RunSummaryModel.CardsRowIndex];
            if (cardsLabel != null) cardsLabel.text = cardsRow.Label;
            if (cardsValue != null) cardsValue.text = cardsRow.Value;
        }

        private void RefreshStatSlots(TMP_Text[] slots, bool labels)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                TMP_Text slot = slots[i];
                if (slot == null) continue;
                bool visible = i < RunSummaryModel.StatRowCount;
                slot.gameObject.SetActive(visible);
                slot.text = visible ? (labels ? rowBuffer[i].Label : rowBuffer[i].Value) : string.Empty;
            }
        }

        /// <summary>Continues the run after clearing the floor, closing the summary and relocking the cursor.</summary>
        public void Continue()
        {
            if (!CanConfirm(Time.unscaledTimeAsDouble)) return;
            actionConfirmed = true;
            UpdateButtonsInteractable(false);
            RunEvents.ContinueAfterFloorClear();
        }

        /// <summary>Leaves the run for the main menu via the shared <see cref="PauseRuntime"/> departure flow.</summary>
        public void MainMenu()
        {
            if (!CanConfirm(Time.unscaledTimeAsDouble)) return;
            actionConfirmed = true;
            UpdateButtonsInteractable(false);

            if (pauseRuntime == null) pauseRuntime = FindAnyObjectByType<PauseRuntime>();
            if (pauseRuntime != null)
            {
                pauseRuntime.SceneLeaveFailed -= HandleSceneLeaveFailed;
                pauseRuntime.SceneLeaveFailed += HandleSceneLeaveFailed;
                if (pauseRuntime.TryLeaveToMainMenu()) return;
            }
            HandleSceneLeaveFailed();
        }

        private void HandleSceneLeaveFailed()
        {
            if (!isOpen) return;
            Apply(new PauseSnapshot(0f, restoration.FixedDeltaTime, true, 0, true));
            actionConfirmed = false;
            UpdateButtonsInteractable(CanConfirm(Time.unscaledTimeAsDouble));
            needsFocus = true;
            if (warnedMenuLeaveFailure) return;
            warnedMenuLeaveFailure = true;
            Debug.LogWarning("Floor summary could not leave for the main menu.", this);
        }

        private void Close()
        {
            if (isOpen) RunEvents.CloseFloorSummary();
            if (pauseRuntime != null) pauseRuntime.SceneLeaveFailed -= HandleSceneLeaveFailed;
            isOpen = false;
            actionConfirmed = false;
            needsFocus = false;
            renderedSelection = -1;
            if (summaryLayer != null) summaryLayer.SetActive(false);
            if (gameplayGroup != null) gameplayGroup.alpha = 1f;
            if (!ownsEnvironment) return;
            ownsEnvironment = false;
            if (ReferenceEquals(GamePause.Service, exclusive))
                GamePause.SetService(previous is UnityEngine.Object owner && owner == null ? null : previous);
            if (pauseRuntime == null || !pauseRuntime.IsSceneLoadInProgress) Apply(restoration);
            previous = null;
            exclusive = null;
        }

        private bool CanConfirm(double unscaledSeconds)
        {
            return isOpen && !actionConfirmed && fade.Complete && releasedInputs == 15
                && !double.IsNaN(unscaledSeconds) && !double.IsInfinity(unscaledSeconds) && unscaledSeconds >= armAt;
        }

        private void Update()
        {
            if (!isOpen) return;
            fade.Tick(Time.unscaledDeltaTime);
            if (cardGroup != null) cardGroup.alpha = reduceMotion ? 1f : fade.Value;

            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            Mouse mouse = Mouse.current;
            if (!actionConfirmed)
            {
                if (keyboard == null || !keyboard.enterKey.isPressed) releasedInputs |= 1;
                if (keyboard == null || !keyboard.spaceKey.isPressed) releasedInputs |= 2;
                if (gamepad == null || !gamepad.buttonSouth.isPressed) releasedInputs |= 4;
                if (mouse == null || !mouse.leftButton.isPressed) releasedInputs |= 8;
            }

            bool canConfirm = CanConfirm(Time.unscaledTimeAsDouble);
            UpdateButtonsInteractable(canConfirm);

            if (actionConfirmed) return;

            EventSystem system = eventSystemGuard != null ? eventSystemGuard.ActiveSystem : EventSystem.current;
            if (system != null)
            {
                GameObject current = system.currentSelectedGameObject;
                if (continueButton != null && current == continueButton.gameObject) selectedIndex = 0;
                else if (mainMenuButton != null && current == mainMenuButton.gameObject) selectedIndex = 1;
                else needsFocus = true;
            }

            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) move = -1;
                else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) move = 1;
            }
            float axis = gamepad != null ? gamepad.leftStick.x.ReadValue() : 0f;
            bool held = Mathf.Abs(axis) > 0.5f;
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame || (held && !stickHeld && axis < 0f)) move = -1;
                else if (gamepad.dpad.right.wasPressedThisFrame || (held && !stickHeld && axis > 0f)) move = 1;
            }
            stickHeld = held;

            if (move != 0 && system == null)
            {
                selectedIndex = (selectedIndex + 1) & 1;
                needsFocus = true;
            }

            Focus();
            UpdateFocusVisuals();

            if (canConfirm && ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                if (selectedIndex == 0) Continue();
                else MainMenu();
            }
        }

        private void Focus()
        {
            if (!needsFocus) return;
            UnityEngine.UI.Button target = selectedIndex == 0 ? continueButton : mainMenuButton;
            if (target == null || !target.gameObject.activeInHierarchy) return;
            EventSystem system = eventSystemGuard != null ? eventSystemGuard.ActiveSystem : EventSystem.current;
            if (system == null) return;
            system.SetSelectedGameObject(target.gameObject, focusEvent);
            needsFocus = system.currentSelectedGameObject != target.gameObject;
        }

        private void UpdateButtonsInteractable(bool interactable)
        {
            if (continueButton != null && continueButton.interactable != interactable) continueButton.interactable = interactable;
            if (mainMenuButton != null && mainMenuButton.interactable != interactable) mainMenuButton.interactable = interactable;
        }

        private void UpdateFocusVisuals()
        {
            if (renderedSelection == selectedIndex) return;
            renderedSelection = selectedIndex;
            ApplyButtonVisual(selectedIndex == 0, continueLabel, continueSeal, continueUnderline);
            ApplyButtonVisual(selectedIndex == 1, mainMenuLabel, mainMenuSeal, mainMenuUnderline);
        }

        private void ApplyButtonVisual(bool focused, TMP_Text label, UnityEngine.UI.Image seal, UnityEngine.UI.Image underline)
        {
            if (label != null && theme != null)
                label.color = focused ? theme.MenuTheme.Focus : theme.MenuTheme.Text;
            if (seal != null) seal.enabled = focused;
            if (underline != null) underline.enabled = focused;
        }

        /// <inheritdoc/>
        public PauseSnapshot Capture() => new PauseSnapshot(Time.timeScale, Time.fixedDeltaTime, AudioListener.pause, (int)Cursor.lockState, Cursor.visible);

        /// <inheritdoc/>
        public void Apply(PauseSnapshot snapshot)
        {
            Time.timeScale = snapshot.TimeScale;
            Time.fixedDeltaTime = snapshot.FixedDeltaTime;
            AudioListener.pause = snapshot.AudioPaused;
            Cursor.lockState = (CursorLockMode)snapshot.CursorLockMode;
            Cursor.visible = snapshot.CursorVisible;
        }
    }
}
