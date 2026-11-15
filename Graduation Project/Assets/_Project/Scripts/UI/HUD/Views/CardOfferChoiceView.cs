using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroFPS.UI.HUD
{
    /// <summary>Pointer-only choice surface; all confirmation goes through the offer gate.</summary>
    public sealed class CardOfferChoiceView : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private TMP_Text nameText, boonText, curseText, flavorText;
        [SerializeField] private UnityEngine.UI.Image frame;
        [SerializeField] private UnityEngine.UI.Button button;
        private CardOfferView owner;
        private int index;
        private bool bound;
        /// <summary>Localized name cell.</summary>
        public TMP_Text NameText => nameText;
        /// <summary>Localized boon cell.</summary>
        public TMP_Text BoonText => boonText;
        /// <summary>Localized curse cell.</summary>
        public TMP_Text CurseText => curseText;
        /// <summary>Localized flavor cell.</summary>
        public TMP_Text FlavorText => flavorText;
        /// <summary>Binds a pre-created card or refusal surface.</summary>
        public void Bind(CardOfferView view, int selection)
        {
            owner = view; index = selection;
            if (!bound && button != null) { button.onClick.AddListener(Click); bound = true; }
        }
        private void OnDestroy() { if (bound && button != null) button.onClick.RemoveListener(Click); }
        /// <summary>Keeps UGUI submit inactive until the same arm guard permits direct input.</summary>
        public void SetInputReady(bool ready) { if (button != null) button.interactable = ready; }
        /// <summary>Uses both a frame and scale to indicate focus.</summary>
        public void SetSelected(bool selected)
        {
            if (frame != null) frame.enabled = selected;
            transform.localScale = selected ? Vector3.one * 1.025f : Vector3.one;
        }
        /// <inheritdoc/>
        public void OnPointerEnter(PointerEventData data) => owner?.Select(index);
        private void Click()
        {
            if (owner == null) return;
            owner.Select(index); owner.Confirm();
        }
    }
}
