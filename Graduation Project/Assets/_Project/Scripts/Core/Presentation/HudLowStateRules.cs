namespace RetroFPS.Presentation
{
    /// <summary>Shared low-resource predicates. Zero maxima represent absent resources.</summary>
    public static class HudLowStateRules
    {
        /// <summary>Tests the inclusive health percentage boundary.</summary>
        public static bool IsLowHealth(float healthPercent, float lowHealthPercent)
            => healthPercent <= lowHealthPercent;

        /// <summary>Tests low health with a guarded maximum.</summary>
        public static bool IsLowHealth(float current, float maximum, float lowHealthPercent)
            => maximum > 0f && IsLowHealth(current / maximum, lowHealthPercent);

        /// <summary>Tests health using the shared default when no config is assigned.</summary>
        public static bool IsLowHealth(float current, float maximum, HudConfig config)
            => IsLowHealth(current, maximum, config != null ? config.LowHealthPercent : HudConfig.DefaultLowHealthPercent);

        /// <summary>Tests the strict ammo boundary; views may give empty ammo its own presentation.</summary>
        public static bool IsLowAmmo(int current, int maximum, float lowAmmoRatio)
            => maximum > 0 && (float)current / maximum < lowAmmoRatio;

        /// <summary>Tests empty ammo only for resources with positive capacity.</summary>
        public static bool IsEmptyAmmo(int current, int maximum)
            => maximum > 0 && current <= 0;
    }
}
