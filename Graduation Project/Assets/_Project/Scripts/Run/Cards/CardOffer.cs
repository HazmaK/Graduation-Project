using System;

namespace RetroFPS.Run.Cards
{
    /// <summary>Up to two offered cards, compacted into zero-based slots.</summary>
    public readonly struct CardOffer
    {
        private readonly CurseBlessingDefinition _first;
        private readonly CurseBlessingDefinition _second;

        /// <summary>Creates an offer from its non-null entries.</summary>
        public CardOffer(CurseBlessingDefinition first, CurseBlessingDefinition second)
        {
            _first = first != null ? first : second;
            _second = first != null ? second : null;
        }

        /// <summary>Number of non-null cards in the offer.</summary>
        public int Count => (_first != null ? 1 : 0) + (_second != null ? 1 : 0);

        /// <summary>Returns the card at a zero-based offer index.</summary>
        public CurseBlessingDefinition Get(int index)
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return index == 0 && _first != null ? _first : _second;
        }

        /// <summary>Checks whether the exact non-null card belongs to this offer.</summary>
        public bool Contains(CurseBlessingDefinition card)
            => card != null && (ReferenceEquals(card, _first) || ReferenceEquals(card, _second));
    }
}
