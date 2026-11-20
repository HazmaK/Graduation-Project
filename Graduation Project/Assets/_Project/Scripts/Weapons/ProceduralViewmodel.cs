using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroFPS
{
    /// <summary>
    /// Birinci şahıs silahının organik, canlı ve tok hissettirmesini sağlayan Prosedürel Görünüm Modeli (Viewmodel).
    /// - Fare hareketinde gecikmeli salınım (Weapon Sway)
    /// - Yürürken ritmik adımlama salınımı (Viewmodel Bobbing)
    /// - Ateş edildiğinde geri tepme sarsıntısı (Recoil Kick & Spring Recovery)
    /// - Yakın dövüşte kılıç savurma animasyonu (Melee Slash Arc)
    /// Tamamen sıfır GC tahsisiyle çalışır.
    /// </summary>
    public class ProceduralViewmodel : MonoBehaviour
    {
        [Header("Pozisyon ve Dinlenme Duruşu")]
        [SerializeField] private Vector3 restPosition = new Vector3(0.22f, -0.22f, 0.42f);
        [SerializeField] private Vector3 restRotation = Vector3.zero;

        [Header("Weapon Sway (Fare Takip Salınımı)")]
        [SerializeField] private float swayAmount = 0.025f;
        [SerializeField] private float maxSway = 0.06f;
        [SerializeField] private float swaySmooth = 12f;
        [SerializeField] private float swayRotationAmount = 2.5f;

        [Header("Viewmodel Bobbing (Adım Salınımı)")]
        [SerializeField] private float bobFrequency = 9f;
        [SerializeField] private float bobHorizontalAmplitude = 0.015f;
        [SerializeField] private float bobVerticalAmplitude = 0.022f;

        [Header("Recoil (Geri Tepme Yaylanması)")]
        [SerializeField] private float recoilFrequency = 9f;
        [SerializeField] private float recoilDamping = 0.5f;

        [Header("Vertical Inertia and Landing")]
        [SerializeField] private float verticalInertiaScale = 0.006f;
        [SerializeField] private float maxVerticalInertia = 0.05f;
        [SerializeField] private float maxInertiaPitch = 4f;
        [SerializeField] private float inertiaSmooth = 12f;
        [SerializeField] private float landingDip = 0.05f;
        [SerializeField] private float landingPitch = 5f;
        [SerializeField] private float jumpDip = 0.012f;

        [Header("Equip Raise")]
        [SerializeField] private float equipDropDistance = 0.28f;
        [SerializeField] private float equipDuration = 0.16f;
        [SerializeField] private float equipPitchDown = 35f;

        // Melee swing keys are in view space: grip offset from rest, and where the blade points.
        // The arcs pass in front of the camera so the blade crosses the screen; the edge is turned to lead the motion.
        [Header("Melee Swings (blade only)")]
        [SerializeField] private SwingProfile slashSwing = new SwingProfile(   // combo 1: upper-right to lower-left
            new SwingKey(new Vector3(0.08f, 0.21f, -0.15f), new Vector3(0.55f, 0.75f, -0.35f)),
            new SwingKey(new Vector3(-0.16f, 0.17f, 0.0f), new Vector3(-0.50f, 0.45f, 0.75f)),
            new SwingKey(new Vector3(-0.34f, 0.04f, 0.0f), new Vector3(-0.85f, -0.15f, 0.45f)),
            0.15f, 0.25f, 0.10f);
        [SerializeField] private SwingProfile backhandSwing = new SwingProfile( // combo 2: lower-left to upper-right
            new SwingKey(new Vector3(-0.40f, 0.06f, 0.0f), new Vector3(-0.85f, -0.20f, 0.45f)),
            new SwingKey(new Vector3(-0.36f, 0.20f, 0.02f), new Vector3(0.40f, 0.58f, 0.71f)),
            new SwingKey(new Vector3(0.06f, 0.22f, -0.10f), new Vector3(0.60f, 0.75f, -0.20f)),
            0.15f, 0.25f, 0.10f, 45f);
        [SerializeField] private SwingProfile overheadSwing = new SwingProfile( // combo 3: heavy top-down chop
            new SwingKey(new Vector3(0.0f, 0.30f, -0.18f), new Vector3(0.35f, 0.85f, -0.40f)),
            new SwingKey(new Vector3(-0.12f, 0.20f, 0.0f), new Vector3(-0.30f, 0.60f, 0.74f)),
            new SwingKey(new Vector3(-0.24f, 0.04f, 0.0f), new Vector3(-0.55f, -0.45f, 0.70f)),
            0.28f, 0.22f, 0.12f, 25f);
        [SerializeField] private SwingProfile lungeSwing = new SwingProfile(    // alt: forward thrust
            new SwingKey(new Vector3(0.05f, -0.02f, -0.22f), new Vector3(-0.10f, 0.15f, 0.98f)),
            new SwingKey(new Vector3(0.0f, 0.02f, 0.0f), new Vector3(-0.15f, 0.18f, 0.97f)),
            new SwingKey(new Vector3(-0.02f, 0.03f, 0.12f), new Vector3(-0.15f, 0.17f, 0.97f)),
            0.18f, 0.22f, 0.25f);
        [SerializeField] private SwingProfile slamSwing = new SwingProfile(     // air alt: raise then drive down
            new SwingKey(new Vector3(0.0f, 0.32f, -0.15f), new Vector3(0.10f, 0.90f, 0.42f)),
            new SwingKey(new Vector3(-0.10f, 0.22f, 0.05f), new Vector3(-0.08f, 0.30f, 0.95f)),
            new SwingKey(new Vector3(-0.12f, 0.12f, 0.10f), new Vector3(-0.05f, -0.88f, 0.47f)),
            0.25f, 0.20f, 0.20f, 20f);

        private PlayerMovement _movement;
        private Spring _recoilX;
        private Spring _recoilY;
        private Spring _recoilZ;
        private Spring _recoilPitch;
        private Spring _recoilYaw;
        private Spring _recoilRoll;
        private Spring _landingY;
        private Spring _landingPitch;
        private float _inertiaY;
        private float _inertiaPitch;
        private Vector3 _posePosition;
        private Quaternion _poseRotation;
        private bool _isEquipping;
        private float _equipTimer;
        private float _bobTimer = 0f;

        // Yakın dövüş savurma durumu
        private bool _isSlashing = false;
        private float _slashTimer = 0f;
        private float _slashDuration = 0.25f;
        private SwingProfile _activeSwing;
        private Vector3 _swingFromPos;
        private Quaternion _swingFromRot;

        private void Awake()
        {
            _movement = GetComponentInParent<PlayerMovement>();
            _posePosition = restPosition;
            _poseRotation = Quaternion.Euler(restRotation);
            transform.localPosition = restPosition;
            transform.localRotation = Quaternion.Euler(restRotation);
        }

        #region Movement Feedback
        private void OnEnable()
        {
            if (_movement == null) _movement = GetComponentInParent<PlayerMovement>();
            if (_movement != null)
            {
                _movement.OnLanded += HandleLanding;
                _movement.OnJumped += HandleJump;
            }
        }

        private void OnDisable()
        {
            if (_movement != null)
            {
                _movement.OnLanded -= HandleLanding;
                _movement.OnJumped -= HandleJump;
            }
            _recoilX = default;
            _recoilY = default;
            _recoilZ = default;
            _recoilPitch = default;
            _recoilYaw = default;
            _recoilRoll = default;
            _landingY = default;
            _landingPitch = default;
            _inertiaY = 0f;
            _inertiaPitch = 0f;
            _isSlashing = false;
            _isEquipping = false;
            _posePosition = restPosition;
            _poseRotation = Quaternion.Euler(restRotation);
        }

        private void HandleLanding(float impact)
        {
            float t = Mathf.InverseLerp(4f, 22f, impact);
            // An underdamped velocity kick with this scale peaks close to the requested offset.
            float impulseScale = 3.7f * Mathf.PI * recoilFrequency;
            _landingY.AddVelocity(-landingDip * t * impulseScale);
            _landingPitch.AddVelocity(landingPitch * t * impulseScale);
        }

        private void HandleJump()
        {
            _landingY.AddVelocity(-jumpDip * 3.7f * Mathf.PI * recoilFrequency);
        }

        public void PlayEquip()
        {
            _equipTimer = 0f;
            _isEquipping = true;
        }
        #endregion

        public void SetRestPose(Vector3 pos, Vector3 rot)
        {
            restPosition = pos;
            restRotation = rot;
        }

        private void Update()
        {
            if (Pause.GamePause.IsSimulationBlocked) return;
            if (_isSlashing)
            {
                ExecuteSlashAnimation();
            }
            else
            {
                ApplySwayAndBob();
            }
            ComposePose();
            // Display newly displaced recoil before advancing it, including at low frame rates.
            HandleRecoilRecovery();
        }

        private void ApplySwayAndBob()
        {
            // 1. Fare Salınımı (Sway)
            Vector2 mouseDelta = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
            {
                mouseDelta = Mouse.current.delta.ReadValue() * 0.05f;
            }

            float swayX = Mathf.Clamp(-mouseDelta.x * swayAmount, -maxSway, maxSway);
            float swayY = Mathf.Clamp(-mouseDelta.y * swayAmount, -maxSway, maxSway);
            Vector3 targetSwayPos = new Vector3(swayX, swayY, 0f);

            float swayRotX = Mathf.Clamp(mouseDelta.y * swayRotationAmount, -6f, 6f);
            float swayRotY = Mathf.Clamp(-mouseDelta.x * swayRotationAmount, -6f, 6f);
            Vector3 targetSwayRot = new Vector3(swayRotX, swayRotY, -swayRotY * 0.5f);

            // 2. Adımlama Salınımı (Bobbing)
            Vector3 bobPos = Vector3.zero;
            if (_movement != null && _movement.IsGrounded)
            {
                float speed = _movement.CurrentHorizontalSpeed;
                if (speed > 0.5f)
                {
                    _bobTimer += Time.deltaTime * (speed * 0.65f + bobFrequency);
                    bobPos.x = Mathf.Cos(_bobTimer * 0.5f) * bobHorizontalAmplitude;
                    bobPos.y = Mathf.Sin(_bobTimer) * bobVerticalAmplitude;
                }
                else
                {
                    _bobTimer = Mathf.MoveTowards(_bobTimer, 0f, Time.deltaTime * 5f);
                }
            }

            // Pozisyon ve rotasyonu harmanla
            Vector3 targetPos = restPosition + targetSwayPos + bobPos;
            Quaternion targetRot = Quaternion.Euler(restRotation + targetSwayRot);

            _posePosition = Vector3.Lerp(_posePosition, targetPos, Time.deltaTime * swaySmooth);
            _poseRotation = Quaternion.Slerp(_poseRotation, targetRot, Time.deltaTime * swaySmooth);
        }

        private void HandleRecoilRecovery()
        {
            float dt = Time.deltaTime;
            _recoilX.Step(0f, recoilFrequency, recoilDamping, dt);
            _recoilY.Step(0f, recoilFrequency, recoilDamping, dt);
            _recoilZ.Step(0f, recoilFrequency, recoilDamping, dt);
            _recoilPitch.Step(0f, recoilFrequency, recoilDamping, dt);
            _recoilYaw.Step(0f, recoilFrequency, recoilDamping, dt);
            _recoilRoll.Step(0f, recoilFrequency, recoilDamping, dt);
            _landingY.Step(0f, recoilFrequency, recoilDamping, dt);
            _landingPitch.Step(0f, recoilFrequency, recoilDamping, dt);
        }

        private void ComposePose()
        {
            // Yerdeyken yapışma hızı (-2) kalıcı ofset üretmesin.
            float verticalVelocity = _movement != null && !_movement.IsGrounded ? _movement.Velocity.y : 0f;
            float targetY = Mathf.Clamp(-verticalVelocity * verticalInertiaScale, -maxVerticalInertia, maxVerticalInertia);
            float targetPitch = -targetY / Mathf.Max(0.001f, maxVerticalInertia) * maxInertiaPitch;
            float blend = 1f - Mathf.Exp(-inertiaSmooth * Time.deltaTime);
            _inertiaY = Mathf.Lerp(_inertiaY, targetY, blend);
            _inertiaPitch = Mathf.Lerp(_inertiaPitch, targetPitch, blend);

            float equipOffset = 0f;
            if (_isEquipping)
            {
                float t = Mathf.Clamp01(_equipTimer / Mathf.Max(0.001f, equipDuration));
                float u = t - 1f;
                float eased = 1f + 2.70158f * u * u * u + 1.70158f * u * u;
                equipOffset = 1f - eased;
                _equipTimer += Time.deltaTime;
                if (t >= 1f) _isEquipping = false;
            }

            Vector3 positionOffset = new Vector3(_recoilX.Value,
                _recoilY.Value + _inertiaY + Mathf.Clamp(_landingY.Value, -landingDip, landingDip)
                - equipDropDistance * equipOffset, _recoilZ.Value);
            Vector3 rotationOffset = new Vector3(_recoilPitch.Value + _inertiaPitch
                + Mathf.Clamp(_landingPitch.Value, -landingPitch, landingPitch) + equipPitchDown * equipOffset,
                _recoilYaw.Value, _recoilRoll.Value);
            transform.localPosition = _posePosition + positionOffset;
            transform.localRotation = _poseRotation * Quaternion.Euler(rotationOffset);
        }

        /// <summary>
        /// Ateş anında namlunun geriye ve yukarı doğru sert tepmesini sağlar.
        /// </summary>
        public void AddRecoil(float kickZ = 0.08f, float kickPitch = 5f, float kickYaw = 1.5f)
        {
            _recoilX.SetValue(_recoilX.Value + Random.Range(-kickYaw * 0.005f, kickYaw * 0.005f));
            _recoilY.SetValue(_recoilY.Value + kickPitch * 0.006f);
            _recoilZ.SetValue(_recoilZ.Value - kickZ);
            _recoilPitch.SetValue(_recoilPitch.Value - kickPitch);
            _recoilYaw.SetValue(_recoilYaw.Value + Random.Range(-kickYaw, kickYaw));
            _recoilRoll.SetValue(_recoilRoll.Value + Random.Range(-kickYaw, kickYaw));
        }

        /// <summary>
        /// Karakter hasar aldığında silahın fiziksel bir darbe hissederek aşağı-yana sarsılmasını sağlar.
        /// </summary>
        public void AddDamageFlinch(float intensity = 1.0f)
        {
            _recoilX.SetValue(_recoilX.Value + Random.Range(-0.025f, 0.025f) * intensity);
            _recoilY.SetValue(_recoilY.Value - 0.04f * intensity);
            _recoilZ.SetValue(_recoilZ.Value - 0.035f * intensity);
            _recoilPitch.SetValue(_recoilPitch.Value + 7f * intensity);
            _recoilYaw.SetValue(_recoilYaw.Value + Random.Range(-5f, 5f) * intensity);
            _recoilRoll.SetValue(_recoilRoll.Value + Random.Range(-6f, 6f) * intensity);
        }

        /// <summary>
        /// Kılıçla saldırıldığında ekranda sert ve savurgan bir darbe arkı çizer (Kombo 1: Sağdan Sola Yatay Kesiş).
        /// </summary>
        public void TriggerMeleeSlash(float duration = 0.20f) => StartSwing(in slashSwing, duration);

        /// <summary>Kombo 2: lower-left to upper-right backhand.</summary>
        public void TriggerMeleeBackhand(float duration = 0.20f) => StartSwing(in backhandSwing, duration);

        /// <summary>Kombo 3: heavy top-down finisher chop.</summary>
        public void TriggerMeleeOverhead(float duration = 0.28f) => StartSwing(in overheadSwing, duration);

        /// <summary>Right click: executioner's forward lunge thrust.</summary>
        public void TriggerMeleeLunge(float duration = 0.28f) => StartSwing(in lungeSwing, duration);

        /// <summary>Airborne right click: raise the blade and drive it straight down.</summary>
        public void TriggerMeleeSlam(float duration = 0.35f) => StartSwing(in slamSwing, duration);

        // Starts from the current pose, so a chained combo swing flows out of the previous one instead of snapping.
        private void StartSwing(in SwingProfile swing, float duration)
        {
            _activeSwing = swing;
            _swingFromPos = _posePosition;
            _swingFromRot = _poseRotation;
            _slashTimer = 0f;
            _slashDuration = Mathf.Max(0.05f, duration);
            _isSlashing = true;
        }

        private void ExecuteSlashAnimation()
        {
            _slashTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_slashTimer / _slashDuration);
            MeleeSwingCurve.Evaluate(in _activeSwing, t, _swingFromPos, _swingFromRot,
                restPosition, Quaternion.Euler(restRotation), out _posePosition, out _poseRotation);
            if (t >= 1f) _isSlashing = false;
        }
    }
}
