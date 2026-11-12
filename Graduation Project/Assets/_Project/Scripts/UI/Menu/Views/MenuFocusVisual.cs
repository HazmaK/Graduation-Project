using RetroFPS.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroFPS.UI.Menu
{
    /// <summary>One selection drives hover, bone seal, underline, row band and pressed feedback.</summary>
    public sealed class MenuFocusVisual : MonoBehaviour, ISelectHandler, IDeselectHandler,
        IPointerEnterHandler, IPointerDownHandler, ISubmitHandler
    {
        [SerializeField] private MenuTheme theme;
        [SerializeField] private UnityEngine.UI.Selectable selectable;
        [SerializeField] private TMP_Text label;
        [SerializeField] private UnityEngine.UI.Image sigil;
        [SerializeField] private UnityEngine.UI.Image underline;
        [SerializeField] private UnityEngine.UI.Image rowBand;
        [SerializeField] private UnityEngine.UI.Image sliderFill;
        [SerializeField] private Sprite pressedSigil;
        [SerializeField] private UnityEngine.UI.Shadow pressShadow;
        private MainMenuController controller;
        private readonly MenuTween tween = new MenuTween();
        private Vector2 labelPosition;
        private Vector3 scale;
        private float fromScale = 1f;
        private float toScale = 1f;
        private float pressRemaining;
        private bool focused;
        private Sprite normalSigil;
        private Sprite normalUnderline;

        private void Awake()
        {
            labelPosition = label.rectTransform.anchoredPosition;
            scale = Vector3.one;
            normalSigil = sigil.sprite;
            normalUnderline = underline.sprite;
            Paint(false);
        }

        /// <summary>Injects the active menu's input gate once at startup.</summary>
        public void Bind(MainMenuController owner) => controller = owner;

        /// <inheritdoc/>
        public void OnSelect(BaseEventData eventData)
        {
            Paint(true);
            MenuEvents.RaiseUiAudio(UiAudioEventType.Focus);
        }
        /// <inheritdoc/>
        public void OnDeselect(BaseEventData eventData) => Paint(false);
        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (controller != null && !controller.InputLocked && selectable.IsInteractable())
                eventData.selectedObject = gameObject;
        }
        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && selectable.IsInteractable()
                && controller != null && !controller.InputLocked) Press();
        }
        /// <inheritdoc/>
        public void OnSubmit(BaseEventData eventData)
        {
            if (selectable.IsInteractable() && controller != null && !controller.InputLocked) Press();
        }

        /// <summary>Shows the fixed 80 ms confirmation feedback without a scale pulse.</summary>
        public void Press()
        {
            pressRemaining = theme.PressSeconds;
            Vector2 position = labelPosition;
            position.y -= theme.PressedOffset;
            label.rectTransform.anchoredPosition = position;
            sigil.sprite = pressedSigil;
            sigil.color = Color.white;
            underline.sprite = null;
            underline.color = theme.Accent;
            if (pressShadow != null) pressShadow.enabled = true;
        }

        private void Paint(bool selected)
        {
            focused = selected;
            sigil.enabled = selected;
            underline.enabled = selected;
            if (rowBand != null) rowBand.enabled = selected;
            label.color = selected ? theme.Focus : theme.Text;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            if (sliderFill != null) sliderFill.color = selected ? theme.Accent : theme.TextDim;
            fromScale = label.rectTransform.localScale.x;
            toScale = selected && !GameSettings.Current.ReduceMotion ? theme.FocusScale : 1f;
            tween.Start(MenuTween.ResolveDuration(theme.FocusSeconds, GameSettings.Current.ReduceMotion, false));
            if (!selected) ResetPress();
        }

        private void ResetPress()
        {
            pressRemaining = 0f;
            label.rectTransform.anchoredPosition = labelPosition;
            sigil.color = Color.white; // The generated seal already carries its bone outline and crimson core.
            underline.color = Color.white;
            sigil.sprite = normalSigil;
            underline.sprite = normalUnderline;
            if (pressShadow != null) pressShadow.enabled = false;
        }

        private void Update()
        {
            if (GameSettings.Current.ReduceMotion)
            {
                scale.x = scale.y = scale.z = 1f;
                label.rectTransform.localScale = scale;
            }
            else if (!tween.Complete)
            {
                tween.Tick(Time.unscaledDeltaTime);
                scale.x = scale.y = scale.z = Mathf.LerpUnclamped(fromScale, toScale, tween.Value);
                label.rectTransform.localScale = scale;
            }
            if (focused) underline.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, label.preferredWidth);
            if (pressRemaining <= 0f) return;
            pressRemaining -= Time.unscaledDeltaTime;
            if (pressRemaining <= 0f) ResetPress();
        }
    }
}
