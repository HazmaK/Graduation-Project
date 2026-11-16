using System;

namespace RetroFPS.Weapons
{
    /// <summary>Independent special recoveries and lunge-to-slam airtime state, without scene or clock dependencies.</summary>
    public sealed class BladeAltState
    {
        private float _lungeCooldown;
        private float _slamCooldown;
        private bool _lungedThisAirtime;
        private bool _slamFollowsLunge;

        /// <summary>Seconds until another lunge is available.</summary>
        public float LungeCooldownRemaining => _lungeCooldown;
        /// <summary>Seconds until another slam is available.</summary>
        public float SlamCooldownRemaining => _slamCooldown;

        /// <summary>Uses the same airborne and strict pitch rule for readiness and activation.</summary>
        public static bool WantsSlam(bool isGrounded, float forwardY, float slamThreshold)
            => !isGrounded && forwardY < slamThreshold;

        /// <summary>Reports readiness of the action the current aim and ground state select.</summary>
        public bool CanFire(bool isGrounded, float forwardY, float slamThreshold)
            => (WantsSlam(isGrounded, forwardY, slamThreshold) ? _slamCooldown : _lungeCooldown) <= 0f;

        /// <summary>Advances both recoveries and clears the airtime marker on ground contact.</summary>
        public void Advance(float deltaTime, bool isGrounded)
        {
            float elapsed = Math.Max(0f, deltaTime);
            _lungeCooldown = Math.Max(0f, _lungeCooldown - elapsed);
            _slamCooldown = Math.Max(0f, _slamCooldown - elapsed);
            if (isGrounded) _lungedThisAirtime = false;
        }

        /// <summary>Starts only lunge recovery and records a lunge in this airtime.</summary>
        public void StartLunge(float cooldown)
        {
            ResetLungeCooldown(cooldown);
            _lungedThisAirtime = true;
        }

        /// <summary>Replaces only lunge recovery, including its kill-chain reset.</summary>
        public void ResetLungeCooldown(float cooldown)
            => _lungeCooldown = Math.Max(0f, cooldown);

        /// <summary>Starts only slam recovery and snapshots its height cap before landing clears the airtime marker.</summary>
        public void StartSlam(float cooldown, bool isLunging)
        {
            SetSlamCooldown(cooldown);
            _slamFollowsLunge = isLunging || _lungedThisAirtime;
        }

        /// <summary>Replaces only slam recovery at activation or landing.</summary>
        public void SetSlamCooldown(float cooldown)
            => _slamCooldown = Math.Max(0f, cooldown);

        /// <summary>Returns the committed slam's height credit limit, even after grounding or holstering.</summary>
        public float SlamHeightLimit(float lungeSlamMaxHeight)
            => _slamFollowsLunge ? Math.Max(0f, lungeSlamMaxHeight) : float.PositiveInfinity;
    }
}
