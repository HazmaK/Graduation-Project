using UnityEngine;

namespace RetroFPS.Run.Cards
{
    /// <summary>Per-tier multipliers applied to catalog offer weights.</summary>
    [System.Serializable]
    public struct CardRarityMultipliers
    {
        [SerializeField] private float common;
        [SerializeField] private float rare;
        [SerializeField] private float relic;

        /// <summary>Creates multipliers for the common, rare and relic tiers.</summary>
        public CardRarityMultipliers(float common, float rare, float relic)
        {
            this.common = common;
            this.rare = rare;
            this.relic = relic;
        }

        /// <summary>Configured common-tier multiplier.</summary>
        public float Common => common;
        /// <summary>Configured rare-tier multiplier.</summary>
        public float Rare => rare;
        /// <summary>Configured relic-tier multiplier.</summary>
        public float Relic => relic;
        /// <summary>Neutral multipliers that preserve catalog weights.</summary>
        public static CardRarityMultipliers One => new CardRarityMultipliers(1f, 1f, 1f);

        /// <summary>Returns a nonnegative multiplier; unknown tiers return zero.</summary>
        public float Get(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common: return Mathf.Max(0f, common);
                case CardRarity.Rare: return Mathf.Max(0f, rare);
                case CardRarity.Relic: return Mathf.Max(0f, relic);
                default: return 0f;
            }
        }
    }
}
