using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Scene-independent access to the active difficulty. Follows <see cref="GameSettings"/>;
    /// enemies read it on spawn, the player and attack tokens see changes immediately.
    /// </summary>
    public static class GameDifficulty
    {
        private static DifficultyPresetTable table;
        private static DifficultyModifiers current = DifficultyModifiers.Identity;
        private static DifficultySetting level = DifficultySetting.Normal;
        private static bool initialized;
        private static bool overridden;

        /// <summary>Active modifiers; identity outside Play Mode unless a test override is set.</summary>
        public static DifficultyModifiers Current
        {
            get { EnsureInitialized(); return current; }
        }

        /// <summary>Active difficulty level.</summary>
        public static DifficultySetting Level
        {
            get { EnsureInitialized(); return level; }
        }

        /// <summary>Loads the preset table and subscribes to settings once per Play Mode session.</summary>
        public static void EnsureInitialized()
        {
            if (initialized || overridden || !Application.isPlaying) return;
            initialized = true;
            table = Resources.Load<DifficultyPresetTable>(DifficultyPresetTable.ResourcesPath);
            if (table == null) Debug.LogWarning("[GameDifficulty] Preset table missing; using Normal values.");
            GameSettings.Changed += OnSettingsChanged;
            Apply(GameSettings.Current.Difficulty);
        }

        /// <summary>Injects modifiers for tests; also applies the token offsets.</summary>
        public static void SetForTests(DifficultyModifiers modifiers, DifficultySetting testLevel = DifficultySetting.Normal)
        {
            overridden = true;
            level = testLevel;
            SetCurrent(modifiers);
        }

        /// <summary>Clears test overrides and restores identity values.</summary>
        public static void ResetForTests() => ResetStatics();

        private static void OnSettingsChanged(GameSettingsData data)
        {
            if (data.Difficulty != level) Apply(data.Difficulty);
        }

        private static void Apply(DifficultySetting selected)
        {
            level = selected;
            SetCurrent(table != null ? table.Get(selected) : DifficultyModifiers.Identity);
        }

        private static void SetCurrent(DifficultyModifiers modifiers)
        {
            current = modifiers;
            EnemyAttackTokens.SetLimitOffset(AttackTokenChannel.Melee, modifiers.MeleeTokenOffset);
            EnemyAttackTokens.SetLimitOffset(AttackTokenChannel.Slam, modifiers.SlamTokenOffset);
            EnemyAttackTokens.SetLimitOffset(AttackTokenChannel.RangedHeavy, modifiers.RangedHeavyTokenOffset);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (initialized) GameSettings.Changed -= OnSettingsChanged;
            initialized = false;
            overridden = false;
            table = null;
            level = DifficultySetting.Normal;
            SetCurrent(DifficultyModifiers.Identity);
        }
    }
}
