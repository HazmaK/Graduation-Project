using RetroFPS.Pause;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroFPS.UI.HUD
{
    /// <summary>Exclusive localized offer modal with unscaled arming and explicit refusal.</summary>
    public sealed class CardOfferView : MonoBehaviour, IPauseEnvironment
    {
        [SerializeField] private StringTable strings;
        [SerializeField] private GameObject offerLayer;
        [SerializeField] private CardOfferChoiceView first, second, refuse;
        private readonly CardOfferGate gate = new CardOfferGate();
        private CardOffer offer;
        private IPauseService previous;
        private CardOfferPauseService exclusive;
        private PauseSnapshot restoration;
        private bool stickHeld, ownsEnvironment, inputReady;
        private double openedAt;
        /// <summary>Whether this modal owns the offer.</summary>
        public bool IsOpen => gate.State != CardOfferGate.GateState.Closed;
        /// <summary>Restores authored visibility after the HUD's inactive-text warmup.</summary>
        public void Initialize()
        {
            if (!IsOpen && RunEvents.IsCardOfferOpen) Open(RunEvents.CurrentCardOffer);
            if (offerLayer != null) offerLayer.SetActive(IsOpen);
            if (!IsOpen) return;
            first.gameObject.SetActive(offer.Count > 0); second.gameObject.SetActive(offer.Count > 1);
            RefreshText(); Highlight();
        }
        private void OnEnable()
        {
            RunEvents.OnCardOfferOpened += Open;
            RunEvents.OnCardOfferResolved += Resolved;
            RunEvents.OnRunChanged += RunChanged;
            RunEvents.OnTrueDeath += TrueDeath;
            GameSettings.Changed += SettingsChanged;
            if (RunEvents.IsCardOfferOpen) Open(RunEvents.CurrentCardOffer);
        }
        private void OnDisable()
        {
            RunEvents.OnCardOfferOpened -= Open;
            RunEvents.OnCardOfferResolved -= Resolved;
            RunEvents.OnRunChanged -= RunChanged;
            RunEvents.OnTrueDeath -= TrueDeath;
            GameSettings.Changed -= SettingsChanged;
            // Handlers are already detached, so the resolve below cannot re-enter this view.
            if (IsOpen && RunEvents.IsCardOfferOpen) RunEvents.ResolveCardOffer(null);
            Close();
        }
        private void Open(CardOffer value)
        {
            if (IsOpen || offerLayer == null || RunEvents.CurrentRun?.Phase == RunPhase.TrueDeath) return;
            GamePause.Service?.CancelForExclusiveFlow();
            previous = GamePause.Service;
            restoration = Capture();
            exclusive = new CardOfferPauseService(previous);
            ownsEnvironment = true;
            GamePause.SetService(exclusive);
            Apply(new PauseSnapshot(0f, restoration.FixedDeltaTime, true, 0, true));
            offer = value; gate.Open(value); openedAt = Time.unscaledTimeAsDouble;
            // A stick already deflected at open must return to centre before it can move the selection.
            Gamepad pad = Gamepad.current;
            stickHeld = pad != null && Mathf.Abs(pad.leftStick.x.ReadValue()) > 0.5f;
            first.Bind(this, 0); second.Bind(this, 1); refuse.Bind(this, value.Count);
            refuse?.ClearRarity();
            SetInputReady(false);
            first.gameObject.SetActive(value.Count > 0); second.gameObject.SetActive(value.Count > 1);
            offerLayer.SetActive(true);
            RefreshText(); Highlight();
        }
        private void Resolved(CurseBlessingDefinition card) => Close();
        private void RunChanged(RunState run) => ReleaseOffer();
        // Treats an interrupted offer as refused exactly once; Resolved re-enters Close only.
        private void ReleaseOffer()
        {
            if (RunEvents.IsCardOfferOpen) RunEvents.ResolveCardOffer(null);
            Close();
        }
        private void TrueDeath()
        {
            ReleaseOffer();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        private void SettingsChanged(GameSettingsData settings) { if (IsOpen) RefreshText(); }
        private void RefreshText()
        {
            if (strings == null) return;
            LanguageSetting language = GameSettings.Current.Language;
            for (int i = 0; i < offer.Count; i++)
            {
                CurseBlessingDefinition card = offer.Get(i);
                CardOfferChoiceView choice = i == 0 ? first : second;
                choice.NameText.text = strings.Get(card.NameKey, language);
                choice.NameText.color = card.AccentColor;
                choice.BoonText.text = strings.Get(card.BoonKey, language);
                choice.CurseText.text = strings.Get(card.CurseKey, language);
                choice.FlavorText.text = strings.Get(card.FlavorKey, language);
                choice.SetRarity(card.Rarity, strings.Get(RarityKey(card.Rarity), language));
            }
            refuse?.ClearRarity();
        }
        private static string RarityKey(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => "hud.card.rarity.rare",
            CardRarity.Relic => "hud.card.rarity.relic",
            _ => "hud.card.rarity.common",
        };
        /// <summary>Changes focus from a pointer hover without confirming.</summary>
        public void Select(int index) { if (!IsOpen) return; gate.Select(index); Highlight(); }
        private void Highlight()
        {
            first.SetSelected(gate.Selection == 0 && gate.CardCount > 0);
            second.SetSelected(gate.Selection == 1 && gate.CardCount > 1);
            refuse.SetSelected(gate.Selection == gate.CardCount);
        }
        /// <summary>Resolves once after the arm delay, including mouse confirmation.</summary>
        public void Confirm()
        {
            if (!gate.TryConfirm(out CurseBlessingDefinition chosen)) return;
            RunEvents.ResolveCardOffer(chosen);
            Close();
        }
        private void Close()
        {
            gate.Close(); offer = default;
            SetInputReady(false);
            if (offerLayer != null) offerLayer.SetActive(false);
            if (!ownsEnvironment) return;
            ownsEnvironment = false;
            // Environment is always restored; the service is handed back only while still ours and the owner lives.
            if (ReferenceEquals(GamePause.Service, exclusive))
                GamePause.SetService(previous is UnityEngine.Object owner && owner == null ? null : previous);
            Apply(restoration);
            previous = null; exclusive = null;
        }
        private void SetInputReady(bool ready)
        {
            inputReady = ready;
            first?.SetInputReady(ready); second?.SetInputReady(ready); refuse?.SetInputReady(ready);
        }
        private void Update()
        {
            if (!IsOpen) return;
            // Absolute elapsed time avoids accepting the opening frame's already elapsed delta.
            gate.Tick(Time.unscaledTimeAsDouble - openedAt); openedAt = Time.unscaledTimeAsDouble;
            if (!inputReady && gate.State == CardOfferGate.GateState.Ready) SetInputReady(true);
            Keyboard keyboard = Keyboard.current; Gamepad gamepad = Gamepad.current;
            int direction = 0;
            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) direction = -1;
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) direction = 1;
            }
            float axis = gamepad != null ? gamepad.leftStick.x.ReadValue() : 0f;
            bool held = Mathf.Abs(axis) > 0.5f;
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame || (held && !stickHeld && axis < 0f)) direction = -1;
                if (gamepad.dpad.right.wasPressedThisFrame || (held && !stickHeld && axis > 0f)) direction = 1;
            }
            stickHeld = held;
            if (direction != 0) { gate.Move(direction); Highlight(); }
            if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)) Confirm();
        }
        /// <inheritdoc/>
        public PauseSnapshot Capture() => new PauseSnapshot(Time.timeScale, Time.fixedDeltaTime, AudioListener.pause, (int)Cursor.lockState, Cursor.visible);
        /// <inheritdoc/>
        public void Apply(PauseSnapshot snapshot)
        {
            Time.timeScale = snapshot.TimeScale; Time.fixedDeltaTime = snapshot.FixedDeltaTime;
            AudioListener.pause = snapshot.AudioPaused; Cursor.lockState = (CursorLockMode)snapshot.CursorLockMode; Cursor.visible = snapshot.CursorVisible;
        }
    }
}
