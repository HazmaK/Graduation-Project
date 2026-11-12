using System;
using RetroFPS.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroFPS.UI.Menu
{
    /// <summary>Wrapping value selector; arrow buttons deliberately stay outside the navigation loop.</summary>
    public sealed class MenuCycleRow : UnityEngine.UI.Selectable, ISubmitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private MenuTheme theme;
        [SerializeField] private UnityEngine.UI.Button previous;
        [SerializeField] private UnityEngine.UI.Button next;
        private string[] options = Array.Empty<string>();
        private int index;
        private RectTransform previousRect;
        private RectTransform nextRect;
        private Vector2 previousPosition;
        private Vector2 nextPosition;
        private float nudgeRemaining;

        /// <summary>Raised only for an accepted user change.</summary>
        public event Action<int> Changed;
        /// <summary>Currently displayed option.</summary>
        public int Index => index;

        protected override void Awake()
        {
            base.Awake();
            // Selectable runs in edit mode too (AddComponent from the builder) before references are wired.
            if (previous == null || next == null) return;
            previous.onClick.AddListener(Previous);
            next.onClick.AddListener(Next);
            previousRect = (RectTransform)previous.transform;
            nextRect = (RectTransform)next.transform;
            previousPosition = previousRect.anchoredPosition;
            nextPosition = nextRect.anchoredPosition;
        }

        protected override void OnDestroy()
        {
            if (previous != null) previous.onClick.RemoveListener(Previous);
            if (next != null) next.onClick.RemoveListener(Next);
            base.OnDestroy();
        }

        /// <summary>Binds preformatted values without emitting a settings request.</summary>
        public void SetOptions(string[] values) { options = values ?? Array.Empty<string>(); SetIndex(index); }
        /// <summary>Refreshes the displayed selection without notifying listeners.</summary>
        public void SetIndex(int selected)
        {
            index = options.Length == 0 ? 0 : Mathf.Clamp(selected, 0, options.Length - 1);
            valueLabel.text = options.Length == 0 ? string.Empty : options[index];
        }

        /// <summary>Cycles one step backwards.</summary>
        public void Previous() => Step(-1);
        /// <summary>Cycles one step forwards.</summary>
        public void Next() => Step(1);
        /// <inheritdoc/>
        public void OnSubmit(BaseEventData eventData) => Next();
        /// <inheritdoc/>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Next();
        }
        /// <inheritdoc/>
        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left) { Previous(); eventData.Use(); }
            else if (eventData.moveDir == MoveDirection.Right) { Next(); eventData.Use(); }
            else base.OnMove(eventData);
        }

        private void Step(int direction)
        {
            if (!IsActive() || !IsInteractable() || options.Length < 2) return;
            EventSystem.current.SetSelectedGameObject(gameObject);
            SetIndex((index + direction + options.Length) % options.Length);
            if (!GameSettings.Current.ReduceMotion)
            {
                Vector2 left = previousPosition;
                Vector2 right = nextPosition;
                left.x -= theme.PressedOffset;
                right.x += theme.PressedOffset;
                previousRect.anchoredPosition = left;
                nextRect.anchoredPosition = right;
                nudgeRemaining = theme.PressSeconds;
            }
            MenuEvents.RaiseUiAudio(UiAudioEventType.SliderStep);
            Changed?.Invoke(index);
        }

        private void Update()
        {
            if (nudgeRemaining <= 0f) return;
            nudgeRemaining -= Time.unscaledDeltaTime;
            if (nudgeRemaining > 0f && !GameSettings.Current.ReduceMotion) return;
            previousRect.anchoredPosition = previousPosition;
            nextRect.anchoredPosition = nextPosition;
            nudgeRemaining = 0f;
        }
    }
}
