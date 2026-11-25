namespace RetroFPS.Run
{
    /// <summary>Mutable statistics accumulated over a single run.</summary>
    public sealed class RunRecord
    {
        /// <summary>Enemies killed in the tower.</summary>
        public int TowerKills { get; private set; }
        /// <summary>Enemies killed during an Araf phase.</summary>
        public int ArafKills { get; private set; }
        /// <summary>Enemies killed across all phases.</summary>
        public int TotalKills => TowerKills + ArafKills;
        /// <summary>Kills caused by critical hits.</summary>
        public int CriticalKills { get; private set; }
        /// <summary>Kills that gibbed the enemy.</summary>
        public int GibKills { get; private set; }
        /// <summary>Total positive, finite damage received.</summary>
        public float DamageTaken { get; private set; }
        /// <summary>Unscaled seconds spent falling into, inside, or escaping Araf.</summary>
        public float ArafSeconds { get; private set; }

        /// <summary>Records one kill and its critical and gib flags.</summary>
        public void RecordKill(bool inAraf, bool critical, bool gibbed)
        {
            if (inAraf) ArafKills++;
            else TowerKills++;
            if (critical) CriticalKills++;
            if (gibbed) GibKills++;
        }

        /// <summary>Accumulates damage when the amount is positive and finite.</summary>
        public void RecordDamage(float amount)
        {
            if (amount > 0f && !float.IsInfinity(amount)) DamageTaken += amount;
        }

        /// <summary>Accumulates positive, finite time only during the three Araf phases.</summary>
        public void AdvanceTime(float unscaledDeltaSeconds, RunPhase phase)
        {
            if (unscaledDeltaSeconds > 0f && !float.IsInfinity(unscaledDeltaSeconds) &&
                (phase == RunPhase.FallingToAraf || phase == RunPhase.InAraf || phase == RunPhase.EscapingAraf))
                ArafSeconds += unscaledDeltaSeconds;
        }
    }
}
