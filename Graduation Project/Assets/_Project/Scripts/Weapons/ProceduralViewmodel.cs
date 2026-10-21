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
        [SerializeField] private float recoilRecoverySpeed = 16f;

        private PlayerMovement _movement;
        private Vector3 _currentRecoilPos;
        private Vector3 _currentRecoilRot;
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
            transform.localPosition = restPosition;
            transform.localRotation = Quaternion.Euler(restRotation);
        }

        public void SetRestPose(Vector3 pos, Vector3 rot)
        {
            restPosition = pos;
            restRotation = rot;
        }

        private void Update()
        {
            HandleRecoilRecovery();

            if (_isSlashing)
            {
                ExecuteSlashAnimation();
            }
            else
            {
                ApplySwayAndBob();
            }
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
            Vector3 targetPos = restPosition + targetSwayPos + bobPos + _currentRecoilPos;
            Quaternion targetRot = Quaternion.Euler(restRotation + targetSwayRot + _currentRecoilRot);

            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * swaySmooth);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * swaySmooth);
        }

        private void HandleRecoilRecovery()
        {
            _currentRecoilPos = Vector3.Lerp(_currentRecoilPos, Vector3.zero, Time.deltaTime * recoilRecoverySpeed);
            _currentRecoilRot = Vector3.Lerp(_currentRecoilRot, Vector3.zero, Time.deltaTime * recoilRecoverySpeed);
        }

        /// <summary>
        /// Ateş anında namlunun geriye ve yukarı doğru sert tepmesini sağlar.
        /// </summary>
        public void AddRecoil(float kickZ = 0.08f, float kickPitch = 5f, float kickYaw = 1.5f)
        {
            _currentRecoilPos += new Vector3(Random.Range(-kickYaw * 0.005f, kickYaw * 0.005f), kickPitch * 0.006f, -kickZ);
            _currentRecoilRot += new Vector3(-kickPitch, Random.Range(-kickYaw, kickYaw), Random.Range(-kickYaw, kickYaw));
        }

        /// <summary>
        /// Karakter hasar aldığında silahın fiziksel bir darbe hissederek aşağı-yana sarsılmasını sağlar.
        /// </summary>
        public void AddDamageFlinch(float intensity = 1.0f)
        {
            _currentRecoilPos += new Vector3(Random.Range(-0.025f, 0.025f) * intensity, -0.04f * intensity, -0.035f * intensity);
            _currentRecoilRot += new Vector3(7f * intensity, Random.Range(-5f, 5f) * intensity, Random.Range(-6f, 6f) * intensity);
        }

        /// <summary>
        /// Kılıçla saldırıldığında ekranda sert ve savurgan bir darbe arkı çizer.
        /// </summary>
        public void TriggerMeleeSlash(float duration = 0.22f)
        {
            _isSlashing = true;
            _slashTimer = 0f;
            _slashDuration = duration;

            _slashStartPos = restPosition + new Vector3(0.15f, 0.12f, -0.05f);
            _slashPeakPos = restPosition + new Vector3(-0.35f, -0.18f, 0.10f);

            _slashStartRot = Quaternion.Euler(restRotation + new Vector3(-20f, 45f, -30f));
            _slashPeakRot = Quaternion.Euler(restRotation + new Vector3(35f, -65f, 50f));
        }

        private void ExecuteSlashAnimation()
        {
            _slashTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_slashTimer / _slashDuration);

            // Vurucu Easing: Hızlı ve sert savuruş
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

            transform.localPosition = Vector3.Lerp(_slashStartPos, _slashPeakPos, ease);
            transform.localRotation = Quaternion.Slerp(_slashStartRot, _slashPeakRot, ease);

            if (t >= 1.0f)
            {
                _isSlashing = false;
            }
        }
    }
}
