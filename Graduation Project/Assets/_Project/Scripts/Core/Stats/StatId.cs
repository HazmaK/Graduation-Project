namespace RetroFPS.Stats
{
    /// <summary>Stable identifiers for player statistics.</summary>
    public enum StatId
    {
        /// <summary>Outgoing damage.</summary>
        DamageDealt = 0,
        /// <summary>Melee damage.</summary>
        MeleeDamage = 1,
        /// <summary>Weapon firing rate.</summary>
        FireRate = 2,
        /// <summary>Movement speed.</summary>
        MoveSpeed = 3,
        /// <summary>Dash recharge duration.</summary>
        DashRechargeTime = 4,
        /// <summary>Maximum health.</summary>
        MaxHealth = 5,
        /// <summary>Incoming damage.</summary>
        DamageTaken = 6,
        /// <summary>Incoming healing.</summary>
        HealReceived = 7,
        /// <summary>Harvest healing.</summary>
        HarvestHeal = 8,
        /// <summary>Orb drop probability.</summary>
        OrbDropChance = 9,
        /// <summary>Ammo supplied by pickups.</summary>
        AmmoPickupAmount = 10,
        /// <summary>Overheal limit.</summary>
        OverhealCap = 11
    }

    /// <summary>Bounds for the stat identifiers.</summary>
    public static class StatIds
    {
        /// <summary>Number of supported statistics.</summary>
        public const int Count = 12;
    }
}
