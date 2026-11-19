using RetroFPS.Run.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RetroFPS.UI.HUD
{
    /// <summary>Pointer-only choice surface; all confirmation goes through the offer gate.</summary>
    public sealed class CardOfferChoiceView : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private TMP_Text nameText, boonText, curseText, flavorText, rarityText;
        [SerializeField] private UnityEngine.UI.Image frame, rarityBorder;
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private Color commonColor = new Color(0.63f, 0.59f, 0.52f, 1f);
        [SerializeField] private Color rareColor = new Color(0.58f, 0.30f, 0.78f, 1f);
        [SerializeField] private Color relicColor = new Color(1.00f, 0.80f, 0.32f, 1f);
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
        /// <summary>Localized rarity tier cell.</summary>
        public TMP_Text RarityText => rarityText;
        /// <summary>Always-visible tier-colored border.</summary>
        public UnityEngine.UI.Image RarityBorder => rarityBorder;
        /// <summary>Border and label tint for common cards.</summary>
        public Color CommonColor => commonColor;
        /// <summary>Border and label tint for rare cards.</summary>
        public Color RareColor => rareColor;
        /// <summary>Border and label tint for relic cards.</summary>
        public Color RelicColor => relicColor;
        /// <summary>Binds a pre-created card or refusal surface.</summary>
        public void Bind(CardOfferView view, int selection)
        {
            owner = view; index = selection;
            if (!bound && button != null) { button.onClick.AddListener(Click); bound = true; }
        }
        private void OnDestroy() { if (bound && button != null) button.onClick.RemoveListener(Click); }
        /// <summary>Keeps UGUI submit inactive until the same arm guard permits direct input.</summary>
        public void SetInputReady(bool ready) { if (button != null) button.interactable = ready; }
        /// <summary>Applies the tier label and always-visible tier border color.</summary>
        public void SetRarity(CardRarity rarity, string label)
        {
            Color tint = rarity switch
            {
                CardRarity.Rare => rareColor,
                CardRarity.Relic => relicColor,
                _ => commonColor,
            };
            if (rarityText != null) { rarityText.text = label; rarityText.color = tint; rarityText.enabled = true; }
            if (rarityBorder != null) { rarityBorder.color = tint; rarityBorder.enabled = true; }
        }
        /// <summary>Hides the rarity label and border for surfaces without a tier, such as refusal.</summary>
        public void ClearRarity()
        {
            if (rarityText != null) { rarityText.text = string.Empty; rarityText.enabled = false; }
            if (rarityBorder != null) rarityBorder.enabled = false;
        }
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
