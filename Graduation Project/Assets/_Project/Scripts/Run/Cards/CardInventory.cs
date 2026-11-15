using System;
using RetroFPS.Stats;

namespace RetroFPS.Run.Cards
{
    /// <summary>Run-owned cards and their contributions to a stat sheet.</summary>
    public sealed class CardInventory
    {
        /// <summary>Maximum number of cards held in a run.</summary>
        public const int MaxCards = 16;

        private readonly StatSheet _sheet;
        private readonly CurseBlessingDefinition[] _cards = new CurseBlessingDefinition[MaxCards];
        private int _count;

        /// <summary>Creates an inventory backed by the supplied stat sheet.</summary>
        public CardInventory(StatSheet sheet)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        }

        /// <summary>Number of acquired cards.</summary>
        public int Count => _count;
        /// <summary>Raised after a card and all its modifiers have been acquired.</summary>
        public event Action<CurseBlessingDefinition> OnCardAcquired;

        /// <summary>Returns the acquired card at a zero-based slot.</summary>
        public CurseBlessingDefinition Get(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _cards[index];
        }

        /// <summary>Checks ownership by ordinal card identity.</summary>
        public bool Has(CurseBlessingDefinition card)
        {
            if (card == null) return false;
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(_cards[i].Id, card.Id, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Acquires a unique card if both the inventory and sheet can hold all its modifiers.</summary>
        public bool TryAcquire(CurseBlessingDefinition card)
        {
            if (card == null || _count == MaxCards || Has(card)) return false;
            int modifierCount = card.ModifierCount;
            if (modifierCount > StatSheet.MaxModifiers - _sheet.Count) return false;
            for (int i = 0; i < modifierCount; i++)
            {
                StatModifier modifier = card.GetModifier(i);
                if (float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value)) return false;
                if (modifier.Stat == StatId.MaxHealth && modifier.Condition != StatCondition.Always) return false;
            }

            int sourceId = _count + 1;
            _cards[_count++] = card;
            _sheet.BeginBatch();
            try
            {
                for (int i = 0; i < modifierCount; i++)
                {
                    StatModifier modifier = card.GetModifier(i);
                    _sheet.Add(sourceId, in modifier);
                }
            }
            finally
            {
                _sheet.EndBatch();
            }
            OnCardAcquired?.Invoke(card);
            return true;
        }
    }
}
