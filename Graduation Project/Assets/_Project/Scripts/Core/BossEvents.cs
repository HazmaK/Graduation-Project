namespace RetroFPS.Combat
{
    /// <summary>Boss presentation events and the latest phase snapshot for late HUD subscribers.</summary>
    public static class BossEvents
    {
        private static bool engaged;
        private static string nameKey;
        private static float currentHealth, maxHealth;
        private static int phase;

        /// <summary>Whether a boss encounter is currently engaged.</summary>
        public static bool IsEngaged => engaged;
        /// <summary>The engaged boss's localization key.</summary>
        public static string NameLocKey => nameKey;
        /// <summary>The latest current-phase health.</summary>
        public static float CurrentHealth => currentHealth;
        /// <summary>The latest current-phase maximum health.</summary>
        public static float MaxHealth => maxHealth;
        /// <summary>The latest one-based phase number.</summary>
        public static int Phase => phase;

        /// <summary>Announces the boss name key and initial phase maximum health.</summary>
        public static event System.Action<string, float> OnBossEngaged;
        /// <summary>Announces current and maximum health in the current phase.</summary>
        public static event System.Action<float, float> OnBossHealthChanged;
        /// <summary>Announces the one-based phase number.</summary>
        public static event System.Action<int> OnBossPhaseChanged;
        /// <summary>Announces defeat or an encounter reset.</summary>
        public static event System.Action OnBossDisengaged;

        /// <summary>Starts a fresh boss presentation snapshot at phase one and full health.</summary>
        public static void RaiseBossEngaged(string nameLocKey, float maxHealth)
        {
            engaged = true; nameKey = nameLocKey; phase = 1;
            BossEvents.maxHealth = maxHealth; currentHealth = maxHealth;
            OnBossEngaged?.Invoke(nameLocKey, maxHealth);
        }

        /// <summary>Records and forwards the current phase health.</summary>
        public static void RaiseBossHealthChanged(float current, float max)
        {
            if (engaged) { currentHealth = current; maxHealth = max; }
            OnBossHealthChanged?.Invoke(current, max);
        }

        /// <summary>Records a phase change and refills its presentation health.</summary>
        public static void RaiseBossPhaseChanged(int phase)
        {
            if (engaged && BossEvents.phase != phase)
            { BossEvents.phase = phase; currentHealth = maxHealth; }
            OnBossPhaseChanged?.Invoke(phase);
        }

        /// <summary>Clears the snapshot and hides subscribed boss presentations.</summary>
        public static void RaiseBossDisengaged()
        {
            engaged = false; nameKey = null; currentHealth = maxHealth = 0f; phase = 0;
            OnBossDisengaged?.Invoke();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnBossEngaged = null; OnBossHealthChanged = null;
            OnBossPhaseChanged = null; OnBossDisengaged = null;
            engaged = false; nameKey = null; currentHealth = maxHealth = 0f; phase = 0;
        }
    }
}
