using RetroFPS.Weapons;

namespace RetroFPS.Combat
{
    /// <summary>The resolved visual kind used for popup labels, styles and scaling.</summary>
    public enum DamagePopupKind
    {
        /// <summary>Ordinary damage.</summary>
        Damage,
        /// <summary>Critical damage.</summary>
        Critical,
        /// <summary>Reflected damage, including perfect parries.</summary>
        Reflected,
        /// <summary>Health restored.</summary>
        Heal,
        /// <summary>Arbalest ammunition collected.</summary>
        Bolts,
        /// <summary>Shotgun ammunition collected.</summary>
        Shells
    }

    /// <summary>Pure popup classification with reflected damage taking precedence over critical damage.</summary>
    public static class DamagePopupLabelPolicy
    {
        /// <summary>Resolves damage flags into one visual kind.</summary>
        public static DamagePopupKind Resolve(bool isCrit, bool isReflected)
            => isReflected ? DamagePopupKind.Reflected : isCrit ? DamagePopupKind.Critical : DamagePopupKind.Damage;

        /// <summary>Resolves a supported ammo type; None is not an ammo popup.</summary>
        public static DamagePopupKind ResolveAmmo(AmmoType type)
            => type == AmmoType.Shells ? DamagePopupKind.Shells : DamagePopupKind.Bolts;
    }
}
