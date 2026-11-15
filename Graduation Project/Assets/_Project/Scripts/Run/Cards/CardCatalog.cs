using System;
using UnityEngine;

namespace RetroFPS.Run.Cards
{
    /// <summary>Catalog of cards available to an Araf offer.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Araf/Card Catalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        [SerializeField] private CurseBlessingDefinition[] cards = Array.Empty<CurseBlessingDefinition>();

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
        public void Configure(CurseBlessingDefinition[] cards)
            => this.cards = cards != null ? (CurseBlessingDefinition[])cards.Clone() : Array.Empty<CurseBlessingDefinition>();
#endif
    }
}
