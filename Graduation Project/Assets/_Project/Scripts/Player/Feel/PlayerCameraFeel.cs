using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement))]
    [DefaultExecutionOrder(100)]
    public class PlayerCameraFeel : MonoBehaviour
    {
        #region Tuning
        [Header("View Punch")]
        [SerializeField] private float punchFrequency = 6.5f;
        [SerializeField] private float punchDamping = 0.55f;

        [Header("Landing and Jump")]
        [SerializeField] private float minLandImpact = 4f;
        [SerializeField] private float landFrequency = 4.5f;
        [SerializeField] private float landDamping = 0.6f;
        [SerializeField] private float maxLandDip = 0.22f;
        [SerializeField] private float jumpRise = 0.03f;
        [SerializeField] private float landPitchDown = 2.5f;
        [SerializeField] private float slideDip = 0.04f;

        [Header("Head Bob")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobStrideLength = 2.6f;
        [SerializeField] private float bobVertical = 0.022f;
        [SerializeField] private float bobLateral = 0.012f;
        [SerializeField] private float bobFadeSpeed = 10f;

        [Header("Dash")]
        [SerializeField] private float dashRollKick = 3f;
        #endregion

        private PlayerMovement _movement;
        private PlayerLook _look;
        private Spring _pitch;
        private Spring _yaw;
        private Spring _roll;
        private Spring _vertical;
        private float _bobPhase;
        private float _bobWeight;
        private Vector3 _previousPosition;
        private Vector3 _positionOffset;
        private Vector3 _rotationOffset;
        // Initial velocity per degree of requested PEAK displacement (derived from punch spring params).
        private float _punchImpulseScale = 1f;

        public Vector3 PositionOffset => _positionOffset;
        public Vector3 RotationOffset => _rotationOffset;

        #region Lifecycle
        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _look = GetComponent<PlayerLook>();
            _punchImpulseScale = ComputePeakImpulseScale(punchFrequency, punchDamping);
            // Warm the lazy singleton outside the movement/footstep hot paths.
            _ = CombatAudio.Instance;
        }

        private void OnValidate()
        {
            _punchImpulseScale = ComputePeakImpulseScale(punchFrequency, punchDamping);
        }

        /// <summary>
        /// Initial velocity v0 that makes a spring at rest peak at exactly 1 unit.
        /// Underdamped: x(t) = (v0/wd) e^(-z w t) sin(wd t) peaks at t* = acos(z)/wd with
        /// x(t*) = (v0/w) e^(-z acos(z)/sqrt(1-z^2)); overdamped uses the acosh/sinh analogue,
        /// critical damping is the shared limit (v0/w)/e.
        /// </summary>
        private static float ComputePeakImpulseScale(float frequencyHz, float dampingRatio)
        {
            float omega = 2f * Mathf.PI * Mathf.Max(0.01f, frequencyHz);
            float z = Mathf.Max(0f, dampingRatio);
            float exponent;
            if (z < 0.999f)
            {
                float s = Mathf.Sqrt(1f - z * z);
                exponent = z * Mathf.Acos(z) / s;
            }
            else if (z > 1.001f)
            {
                float s = Mathf.Sqrt(z * z - 1f);
                exponent = z * Mathf.Log(z + s) / s;
            }
            else
            {
                exponent = 1f;
            }
            return omega * Mathf.Exp(exponent);
        }

        private void OnEnable()
        {
            ResetOffsets();
            if (_movement == null) return;
            _movement.OnJumped += HandleJump;
            _movement.OnLanded += HandleLanding;
            _movement.OnDashStarted += HandleDash;
            _movement.OnSlideStarted += HandleSlide;
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.OnJumped -= HandleJump;
                _movement.OnLanded -= HandleLanding;
                _movement.OnDashStarted -= HandleDash;
                _movement.OnSlideStarted -= HandleSlide;
            }
            ResetOffsets();
        }

        internal void ResetOffsets()
        {
            _pitch = default;
            _yaw = default;
            _roll = default;
            _vertical = default;
            _bobPhase = 0f;
            _bobWeight = 0f;
            _positionOffset = Vector3.zero;
            _rotationOffset = Vector3.zero;
            _previousPosition = transform.position;
        }
        #endregion

        #region Pose
        private void Update()
        {
            if (_movement == null || !_movement.isActiveAndEnabled ||
                (_look != null && !_look.isActiveAndEnabled))
            {
                ResetOffsets();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _pitch.Step(0f, punchFrequency, punchDamping, dt);
            _yaw.Step(0f, punchFrequency, punchDamping, dt);
            _roll.Step(0f, punchFrequency, punchDamping, dt);
            _vertical.Step(0f, landFrequency, landDamping, dt);

            Vector3 travelled = transform.position - _previousPosition;
            travelled.y = 0f;
            _previousPosition = transform.position;
            float speed = _movement.CurrentHorizontalSpeed;
            bool bobbing = enableHeadBob && _movement.IsGrounded && !_movement.IsSliding && speed > 1f;
            float weight = bobbing ? Mathf.Clamp01(speed / Mathf.Max(0.01f, _movement.WalkSpeed)) : 0f;
            _bobWeight = Mathf.Lerp(_bobWeight, weight, 1f - Mathf.Exp(-bobFadeSpeed * Time.deltaTime));

            if (bobbing)
            {
                // Ignore teleport-sized discontinuities rather than emit a burst of footsteps.
                float distance = Mathf.Min(travelled.magnitude, speed * Time.deltaTime);
                float nextPhase = _bobPhase + distance / Mathf.Max(0.01f, bobStrideLength);
                int crossings = Mathf.FloorToInt(nextPhase * 2f + 0.5f) - Mathf.FloorToInt(_bobPhase * 2f + 0.5f);
                for (int i = 0; i < crossings; i++)
                {
                    CombatAudio audio = CombatAudio.Instance;
                    if (audio != null) audio.PlayFootstep();
                }
                _bobPhase = Mathf.Repeat(nextPhase, 1f);
            }

            float phase = _bobPhase * Mathf.PI * 2f;
            _positionOffset = new Vector3(Mathf.Cos(phase) * bobLateral * _bobWeight,
                Mathf.Clamp(_vertical.Value, -maxLandDip, maxLandDip) + Mathf.Sin(phase * 2f) * bobVertical * _bobWeight, 0f);
            _rotationOffset = new Vector3(Mathf.Clamp(_pitch.Value, -9f, 9f),
                Mathf.Clamp(_yaw.Value, -5f, 5f), _roll.Value);
        }

        public void AddViewPunch(float pitchDegrees, float yawDegrees)
        {
            // Peak displacement equals the requested degrees (spring at rest; display clamps still apply).
            _pitch.AddVelocity(pitchDegrees * _punchImpulseScale);
            _yaw.AddVelocity(yawDegrees * _punchImpulseScale);
        }

        // At damping 0.6, an impulse of roughly 2 * omega * peak reaches the requested dip.
        private void AddPositionKick(float peak)
        {
            _vertical.AddVelocity(peak * (4f * Mathf.PI * landFrequency));
        }
        #endregion

        #region Movement Events
        private void HandleLanding(float impact)
        {
            if (impact <= minLandImpact) return;
            float t = Mathf.InverseLerp(minLandImpact, 22f, impact);
            AddPositionKick(-maxLandDip * t);
            AddViewPunch(-landPitchDown * t, 0f);
            CombatAudio audio = CombatAudio.Instance;
            if (audio != null) audio.PlayLand(t);
            if (t > 0.55f && _look != null) _look.AddTrauma(0.12f * t);
        }

        private void HandleJump()
        {
            AddPositionKick(jumpRise);
            CombatAudio audio = CombatAudio.Instance;
            if (audio != null) audio.PlayJump();
        }

        private void HandleDash()
        {
            Vector3 direction = _movement.Velocity.normalized;
            float lateral = Vector3.Dot(transform.right, direction);
            _roll.AddVelocity(-lateral * dashRollKick * 4f * Mathf.PI * punchFrequency);
            CombatAudio audio = CombatAudio.Instance;
            if (audio != null) audio.PlayDash();
        }

        private void HandleSlide()
        {
            AddPositionKick(-slideDip);
            CombatAudio audio = CombatAudio.Instance;
            if (audio != null) audio.PlaySlideStart();
        }
        #endregion
    }
}
