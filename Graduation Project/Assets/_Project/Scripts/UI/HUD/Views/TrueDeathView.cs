using RetroFPS.Presentation;
using RetroFPS.UI.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

namespace RetroFPS.UI.HUD
{
    /// <summary>Owns only death presentation, focus and the single delayed Core restart request.</summary>
    public sealed class TrueDeathView : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private HudTheme theme;
        [SerializeField] private GameObject deathLayer;
        [SerializeField] private CanvasGroup cardGroup, gameplayGroup;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private UnityEngine.UI.Image focusSeal, focusUnderline;
        [SerializeField] private HudEventSystemGuard eventSystemGuard;
        private readonly TrueDeathGate gate = new TrueDeathGate();
        private readonly MenuTween fade = new MenuTween();
        private BaseEventData focusEvent;
        private Vector2 labelPosition;
        private bool reduceMotion, needsFocus, initialized;
        /// <summary>Gets whether this view has received a true-death event.</summary>
        public bool IsOpen => gate.Visible;

        private void Awake()
        {
            // Serialized references can be absent while the editor constructs this component.
            if (buttonLabel != null) labelPosition = buttonLabel.rectTransform.anchoredPosition;
            if (restartButton != null) restartButton.onClick.AddListener(Confirm);
        }
        private void OnDestroy() { if (restartButton != null) restartButton.onClick.RemoveListener(Confirm); }
        private void OnEnable() { if (initialized && gate.Visible) Restore(); }

        /// <summary>Initializes presentation without inferring death from health or run phase.</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            focusEvent = new BaseEventData(eventSystemGuard != null ? eventSystemGuard.BundledSystem : EventSystem.current);
            if (buttonLabel != null) labelPosition = buttonLabel.rectTransform.anchoredPosition;
            // Warm inactive TMP/Selectable components before true death can enable the layer.
            if (deathLayer != null) deathLayer.SetActive(true);
            if (deathLayer != null) deathLayer.SetActive(gate.Visible);
            if (gameplayGroup != null) { gameplayGroup.interactable = false; gameplayGroup.blocksRaycasts = false; }
            if (gate.Visible) Restore();
        }

        /// <summary>Opens once, exclusively from RunEvents.OnTrueDeath.</summary>
        public void Show()
        {
            if (!gate.OnTrueDeath(Time.unscaledTimeAsDouble, config != null ? config.DeathArmSeconds : 0.6d)) return;
            fade.Start(reduceMotion || config == null ? 0f : config.DeathFadeMs * 0.001f);
            Restore();
            if (cardGroup != null) cardGroup.alpha = fade.Value;
        }

        private void Restore()
        {
            if (deathLayer == null || restartButton == null) return;
            if (gameplayGroup != null) gameplayGroup.alpha = 0f;
            deathLayer.SetActive(true); restartButton.interactable = gate.CanConfirm(Time.unscaledTimeAsDouble);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            needsFocus = !gate.Confirmed;
            if (buttonLabel != null && theme != null) buttonLabel.color = theme.MenuTheme.Focus;
            if (focusSeal != null && theme != null) focusSeal.sprite = gate.Confirmed ? theme.DeathSealPressed : theme.DeathSeal;
            if (focusUnderline != null) focusUnderline.enabled = true;
            Focus();
        }

        /// <summary>Accepts UI submit/click and the explicit keyboard/gamepad fallback through the same latch.</summary>
        public void Confirm()
        {
            if (!fade.Complete || !gate.TryConfirm(Time.unscaledTimeAsDouble)) return;
            // Button.OnSubmit checks this immediately after onClick and skips its built-in coroutine.
            restartButton.interactable = false;
            if (focusSeal != null && theme != null) focusSeal.sprite = theme.DeathSealPressed;
            if (!reduceMotion && buttonLabel != null && theme != null)
            {
                Vector2 position = labelPosition; position.y -= theme.MenuTheme.PressedOffset;
                buttonLabel.rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>Immediately finishes the fade and resets displacement, retaining the 80 ms gate.</summary>
        public void SetReduceMotion(bool value)
        {
            reduceMotion = value;
            if (!value) return;
            fade.Start(0f);
            if (gate.Visible && cardGroup != null) cardGroup.alpha = 1f;
            if (buttonLabel != null) buttonLabel.rectTransform.anchoredPosition = labelPosition;
        }

        private void Focus()
        {
            if (!needsFocus || restartButton == null || !restartButton.gameObject.activeInHierarchy) return;
            EventSystem system = eventSystemGuard != null ? eventSystemGuard.ActiveSystem : EventSystem.current;
            if (system == null) return;
            system.SetSelectedGameObject(restartButton.gameObject, focusEvent);
            needsFocus = system.currentSelectedGameObject != restartButton.gameObject;
        }

        private void Update()
        {
            if (!gate.Visible) return;
            fade.Tick(Time.unscaledDeltaTime);
            if (cardGroup != null) cardGroup.alpha = reduceMotion ? 1f : fade.Value;
            Keyboard keyboard = Keyboard.current; Gamepad gamepad = Gamepad.current; Mouse mouse = Mouse.current;
            gate.ObserveInputs(keyboard != null && keyboard.enterKey.isPressed,
                keyboard != null && keyboard.spaceKey.isPressed, gamepad != null && gamepad.buttonSouth.isPressed,
                mouse != null && mouse.leftButton.isPressed, fade.Complete);
            if (restartButton != null) restartButton.interactable = gate.CanConfirm(Time.unscaledTimeAsDouble);
            if (!gate.Confirmed)
            {
                EventSystem system = eventSystemGuard != null ? eventSystemGuard.ActiveSystem : EventSystem.current;
                if (system != null && system.currentSelectedGameObject != restartButton.gameObject) needsFocus = true;
                Focus();
                if (system != null && system.currentSelectedGameObject == restartButton.gameObject)
                {
                    if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
                        (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)) Confirm();
                }
            }
            if (gate.TryConsumeRequest(Time.unscaledTimeAsDouble)) HudRequests.RequestNewRun();
        }
    }
}
