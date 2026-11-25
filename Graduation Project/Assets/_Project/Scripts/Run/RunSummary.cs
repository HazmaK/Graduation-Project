using System;
using System.Collections.Generic;
using RetroFPS.Run.Cards;

namespace RetroFPS.Run
{
    /// <summary>Immutable run statistics and card membership captured when a floor is cleared.</summary>
    public readonly struct RunSummary
    {
        private readonly CurseBlessingDefinition[] _cards;

        /// <summary>One-based number of the cleared floor.</summary>
        public int FloorNumber { get; }
        /// <summary>Total unscaled run duration at capture.</summary>
        public float TotalSeconds { get; }
        /// <summary>Unscaled run duration spent in Araf phases.</summary>
        public float ArafSeconds { get; }
        /// <summary>Total enemies killed.</summary>
        public int TotalKills { get; }
        /// <summary>Enemies killed in the tower.</summary>
        public int TowerKills { get; }
        /// <summary>Enemies killed in Araf.</summary>
        public int ArafKills { get; }
        /// <summary>Critical kills recorded during the run.</summary>
        public int CriticalKills { get; }
        /// <summary>Gib kills recorded during the run.</summary>
        public int GibKills { get; }
        /// <summary>Total damage received during the run.</summary>
        public float DamageTaken { get; }
        /// <summary>Whether the run's Araf charge was consumed.</summary>
        public bool ArafChargeUsed { get; }
        /// <summary>Number of cards in the captured inventory.</summary>
        public int CardCount => _cards == null ? 0 : _cards.Length;

        /// <summary>Creates a summary and copies the supplied card membership into private storage.</summary>
        public RunSummary(int floorNumber, float totalSeconds, float arafSeconds, int totalKills,
            int towerKills, int arafKills, int criticalKills, int gibKills, float damageTaken,
            bool arafChargeUsed, IReadOnlyList<CurseBlessingDefinition> cards)
        {
            FloorNumber = floorNumber;
            TotalSeconds = totalSeconds;
            ArafSeconds = arafSeconds;
            TotalKills = totalKills;
            TowerKills = towerKills;
            ArafKills = arafKills;
            CriticalKills = criticalKills;
            GibKills = gibKills;
            DamageTaken = damageTaken;
            ArafChargeUsed = arafChargeUsed;
            int count = cards == null ? 0 : cards.Count;
            _cards = count == 0 ? Array.Empty<CurseBlessingDefinition>() : new CurseBlessingDefinition[count];
            for (int i = 0; i < count; i++) _cards[i] = cards[i];
        }

        /// <summary>Returns a captured card at a zero-based slot.</summary>
        public CurseBlessingDefinition GetCard(int index)
        {
            if ((uint)index >= (uint)CardCount) throw new ArgumentOutOfRangeException(nameof(index));
            return _cards[index];
        }
    }
}
