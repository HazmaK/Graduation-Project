using UnityEngine;

namespace RetroFPS
{
    /// <summary>Allocation-free damped oscillator. Frequencies are in cycles per second.</summary>
    public struct Spring
    {
        private float _value;
        private float _velocity;

        public float Value => _value;
        public float Velocity => _velocity;

        public void Step(float target, float frequencyHz, float dampingRatio, float dt)
        {
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            if (dt <= 0f) return;

            float omega = 2f * Mathf.PI * Mathf.Max(0f, frequencyHz);
            // Fine substeps retain impulse peaks and overshoot across common frame rates.
            // Also tighten the step for unusually high inspector frequencies.
            float maxStep = Mathf.Min(1f / 480f,
                0.5f / Mathf.Max(1f, omega * (1f + Mathf.Max(0f, dampingRatio))));
            int steps = Mathf.CeilToInt(dt / maxStep);
            float step = dt / steps;
            float damping = 2f * Mathf.Max(0f, dampingRatio) * omega;
            for (int i = 0; i < steps; i++)
            {
                _velocity += ((target - _value) * omega * omega - damping * _velocity) * step;
                _value += _velocity * step;
            }
        }

        public void AddVelocity(float v)
        {
            _velocity += v;
        }

        // Keep mutable storage private while allowing same-assembly recoil displacement.
        internal void SetValue(float value)
        {
            _value = value;
        }
    }
}
