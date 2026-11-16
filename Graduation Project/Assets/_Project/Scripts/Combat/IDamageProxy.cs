namespace RetroFPS.Combat
{
    /// <summary>Identifies the root damage target owning a specialized damage collider.</summary>
    public interface IDamageProxy
    {
        /// <summary>Gets the owner used for melee damage and per-attack deduplication.</summary>
        IDamageable DamageOwner { get; }
    }
}
