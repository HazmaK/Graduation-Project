using System;
using UnityEngine;

namespace RetroFPS.Run.Cards
{
    /// <summary>Catalog of cards available to an Araf offer.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Araf/Card Catalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        [SerializeField] private CurseBlessingDefinition[] cards = Array.Empty<CurseBlessingDefinition>();
        [SerializeField] private float commonWeight = 10f;
        [SerializeField] private float rareWeight = 5f;
        [SerializeField] private float relicWeight = 1.5f;

        /// <summary>Returns the nonnegative per-card weight for a tier; unknown tiers have zero weight.</summary>
        public float GetWeight(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return Mathf.Max(0f, commonWeight);
                case CardRarity.Rare: return Mathf.Max(0f, rareWeight);
                case CardRarity.Relic: return Mathf.Max(0f, relicWeight);
                default: return 0f;
            }
        }

        /// <summary>Number of catalog entries, including empty entries.</summary>
        public int Count => cards != null ? cards.Length : 0;

        /// <summary>Returns the card at a zero-based catalog index.</summary>
        public CurseBlessingDefinition Get(int index)
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return cards[index];
        }

#if UNITY_EDITOR
        /// <summary>Initializes catalog entries for editor builders and tests.</summary>
        public void Configure(CurseBlessingDefinition[] cards, float commonWeight = 10f,
            float rareWeight = 5f, float relicWeight = 1.5f)
        {
            this.cards = cards != null ? (CurseBlessingDefinition[])cards.Clone() : Array.Empty<CurseBlessingDefinition>();
            this.commonWeight = commonWeight;
            this.rareWeight = rareWeight;
            this.relicWeight = relicWeight;
        }
#endif
    }
}
