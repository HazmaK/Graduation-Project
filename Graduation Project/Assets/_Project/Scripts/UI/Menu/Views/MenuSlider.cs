using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RetroFPS.UI.Menu
{
    /// <summary>Five-percent slider with accelerated horizontal holds and no wheel handling.</summary>
    public sealed class MenuSlider : UnityEngine.UI.Slider
    {
        [SerializeField] private InputActionReference navigate;
        private readonly HoldAccelerator accelerator = new HoldAccelerator();
        private float lastMove = float.NegativeInfinity;
        private bool draggingTrack;
        private int dragPointerId;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (navigate != null) navigate.action.performed += OnNavigate;
        }
        protected override void OnDisable()
        {
            if (navigate != null) navigate.action.performed -= OnNavigate;
            accelerator.Reset();
            lastMove = float.NegativeInfinity;
            draggingTrack = false;
            base.OnDisable();
        }
        private void OnNavigate(InputAction.CallbackContext context)
        {
            if (Mathf.Abs(context.ReadValue<Vector2>().x) > 0.4f) return;
            accelerator.Reset();
            lastMove = float.NegativeInfinity;
        }

        /// <inheritdoc/>
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            eventData.selectedObject = gameObject;
            RectTransform track = handleRect != null ? handleRect.parent as RectTransform
                : fillRect != null ? fillRect.parent as RectTransform : null;
            draggingTrack = (track != null && RectTransformUtility.RectangleContainsScreenPoint(
                track, eventData.position, eventData.pressEventCamera))
                || (handleRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    handleRect, eventData.position, eventData.pressEventCamera));
            if (!draggingTrack) return;
            dragPointerId = eventData.pointerId;
            base.OnPointerDown(eventData);
        }

        /// <inheritdoc/>
        public override void OnDrag(PointerEventData eventData)
        {
            if (draggingTrack && eventData.pointerId == dragPointerId) base.OnDrag(eventData);
        }

        /// <inheritdoc/>
        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == dragPointerId) draggingTrack = false;
            base.OnPointerUp(eventData);
        }

        /// <inheritdoc/>
        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            int direction = eventData.moveDir == MoveDirection.Left ? -1
                : eventData.moveDir == MoveDirection.Right ? 1 : 0;
            if (direction == 0) { accelerator.Reset(); base.OnMove(eventData); return; }
            float now = Time.unscaledTime;
            int steps = accelerator.Repeat(now - lastMove, direction);
            lastMove = now;
            value = Mathf.Clamp(value + direction * steps, minValue, maxValue);
            eventData.Use();
        }

        /// <inheritdoc/>
        public override void OnDeselect(BaseEventData eventData)
        {
            accelerator.Reset();
            lastMove = float.NegativeInfinity;
            base.OnDeselect(eventData);
        }
    }
}
