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
        private Vector3 _slashStartPos;
        private Vector3 _slashPeakPos;
        private Quaternion _slashStartRot;
        private Quaternion _slashPeakRot;

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
        public void TriggerMeleeSlash(float duration = 0.20f)
        {
            _isSlashing = true;
            _slashTimer = 0f;
            _slashDuration = duration;

            _slashStartPos = restPosition + new Vector3(0.20f, 0.12f, -0.06f);
            _slashPeakPos = restPosition + new Vector3(-0.35f, -0.16f, 0.10f);

            _slashStartRot = Quaternion.Euler(restRotation + new Vector3(-20f, 45f, -30f));
            _slashPeakRot = Quaternion.Euler(restRotation + new Vector3(35f, -65f, 50f));
        }

        /// <summary>
        /// Kombo 2: Soldan Sağa Geri Dönüş Ters Kesişi (Backhand Slash).
        /// </summary>
        public void TriggerMeleeBackhand(float duration = 0.20f)
        {
            _isSlashing = true;
            _slashTimer = 0f;
            _slashDuration = duration;

            _slashStartPos = restPosition + new Vector3(-0.30f, -0.14f, 0.05f);
            _slashPeakPos = restPosition + new Vector3(0.32f, 0.14f, -0.05f);

            _slashStartRot = Quaternion.Euler(restRotation + new Vector3(30f, -55f, 45f));
            _slashPeakRot = Quaternion.Euler(restRotation + new Vector3(-18f, 55f, -35f));
        }

        /// <summary>
        /// Kombo 3: Yukarıdan Aşağıya Ağır İnfaz Ezici Darbesi (Overhead Heavy Finisher).
        /// </summary>
        public void TriggerMeleeOverhead(float duration = 0.28f)
        {
            _isSlashing = true;
            _slashTimer = 0f;
            _slashDuration = duration;

            _slashStartPos = restPosition + new Vector3(0.04f, 0.32f, -0.12f);
            _slashPeakPos = restPosition + new Vector3(-0.02f, -0.26f, 0.18f);

            _slashStartRot = Quaternion.Euler(restRotation + new Vector3(-55f, 8f, 10f));
            _slashPeakRot = Quaternion.Euler(restRotation + new Vector3(65f, -4f, -8f));
        }

        /// <summary>
        /// Sağ Tık: Cellat İnfaz Hücumu (Executioner's Lunge Thrust).
        /// </summary>
        public void TriggerMeleeLunge(float duration = 0.28f)
        {
            _isSlashing = true;
            _slashTimer = 0f;
            _slashDuration = duration;

            _slashStartPos = restPosition + new Vector3(-0.08f, 0.04f, -0.22f);
            _slashPeakPos = restPosition + new Vector3(0.02f, 0.02f, 0.36f);

            _slashStartRot = Quaternion.Euler(restRotation + new Vector3(-12f, 10f, -14f));
            _slashPeakRot = Quaternion.Euler(restRotation + new Vector3(6f, -3f, 4f));
        }

        /// <summary>
        /// Havada Sağ Tık: Kılıç Yere Dik Çakılma (Meteor Slam).
        /// </summary>
        public void TriggerMeleeSlam(float duration = 0.35f)
        {
            _isSlashing = true;
            _slashTimer = 0f;
            _slashDuration = duration;

            _slashStartPos = restPosition + new Vector3(0f, 0.22f, -0.06f);
            _slashPeakPos = restPosition + new Vector3(0f, -0.35f, 0.22f);

            _slashStartRot = Quaternion.Euler(restRotation + new Vector3(15f, 0f, 0f));
            _slashPeakRot = Quaternion.Euler(restRotation + new Vector3(88f, 0f, 0f));
        }

        private void ExecuteSlashAnimation()
        {
            _slashTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_slashTimer / _slashDuration);

            // İki aşamalı kinetik eğri: Hızlı sert vuruş darbesi (%40) + akıcı toparlanma (%60)
            if (t <= 0.40f)
            {
                float strikeT = Mathf.Sin((t / 0.40f) * Mathf.PI * 0.5f);
                _posePosition = Vector3.Lerp(_slashStartPos, _slashPeakPos, strikeT);
                _poseRotation = Quaternion.Slerp(_slashStartRot, _slashPeakRot, strikeT);
            }
            else
            {
                float returnT = Mathf.SmoothStep(0f, 1f, (t - 0.40f) / 0.60f);
                _posePosition = Vector3.Lerp(_slashPeakPos, restPosition, returnT);
                _poseRotation = Quaternion.Slerp(_slashPeakRot, Quaternion.Euler(restRotation), returnT);
            }

            if (t >= 1.0f)
            {
                _isSlashing = false;
            }
        }
    }
}
