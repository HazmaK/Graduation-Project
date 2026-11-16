namespace RetroFPS.Combat
{
    /// <summary>Deterministic light/light/heavy cadence, with heavy attacks deferred when blocked.</summary>
    public sealed class FlyingEyeAttackPlanner
    {
        private readonly int _lightsBeforeHeavy;

        /// <summary>Creates a cadence requiring at least one light between heavy attacks.</summary>
        public FlyingEyeAttackPlanner(int lightsBeforeHeavy)
        {
            _lightsBeforeHeavy = lightsBeforeHeavy > 0 ? lightsBeforeHeavy : 1;
        }

        /// <summary>Number of light attacks committed since the last heavy attack.</summary>
        public int LightsSinceHeavy { get; private set; }

        /// <summary>Whether the next attack requests a heavy, without committing it.</summary>
        public bool WantsHeavy => LightsSinceHeavy >= _lightsBeforeHeavy;

        /// <summary>Commits a permitted heavy when due, otherwise another light.</summary>
        public FlyingEyeAttack Next(bool heavyAllowed)
        {
            if (WantsHeavy && heavyAllowed)
            {
                LightsSinceHeavy = 0;
                return FlyingEyeAttack.Heavy;
            }

            if (LightsSinceHeavy < int.MaxValue) LightsSinceHeavy++;
            return FlyingEyeAttack.Light;
        }

        /// <summary>Restarts the cadence with no committed light attacks.</summary>
        public void Reset() => LightsSinceHeavy = 0;
    }
}
