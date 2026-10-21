using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroFPS
{
    /// <summary>
    /// Birinci şahıs kamera kontrolü (Gözden geçirilmiş ve düzeltilmiş versiyon).
    /// - Bağımsız Yaw (Gövde) ve Pitch (Kamera) rotasyonu
    /// - Ters eksen (Invert Y) desteği
    /// - Doğal FOV (Varsayılan 65 dikey = ~95 yatay derece)
    /// - Tıklama ile otomatik imleç kilitleme
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Kamerayı taşıyan alt obje (Sadece dikey Pitch yapar).")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private Camera playerCamera;

        [Header("Bakış Ayarları")]
        [Tooltip("Fare hassasiyeti (Standart New Input System için 0.08 - 0.15 arası idealdir)")]
        [SerializeField] private float mouseSensitivity = 0.10f;
        [SerializeField] private bool invertY = false;
        [SerializeField] private float minPitch = -89f;
        [SerializeField] private float maxPitch = 89f;

        [Header("Kamera Yumuşatma (Smooth Look)")]
        [Tooltip("Sert/keskin piksel sıçramalarını yumuşatıp akıcı bakış sağlar")]
        [SerializeField] private bool smoothLook = true;
        [SerializeField] private float smoothSpeed = 35f;

        [Header("Strafe Kamera Eğimi (Tilt)")]
        [SerializeField] private float tiltAngle = 2.0f;
        [SerializeField] private float tiltSpeed = 10f;

        [Header("Dinamik FOV (Hızlandıkça Hafif Genişleme)")]
        [Tooltip("Standart FOV: 65 (16:9 ekranda ~95 derece yatay genişlik verir, balıkgözü yapmaz)")]
        [SerializeField] private float baseFOV = 65f;
        [SerializeField] private float maxFOV = 78f;
        [SerializeField] private float fovLerpSpeed = 8f;
        [SerializeField] private float fovStartSpeed = 11f;
        [SerializeField] private float fovMaxSpeed = 24f;

        [Header("Ekran Sarsıntısı (Screen Shake / Trauma)")]
        [SerializeField] private float maxShakeAngle = 3.5f;
        [SerializeField] private float shakeFrequency = 24f;
        [SerializeField] private float traumaDecay = 3.5f;

        private float _trauma = 0f;
        private Vector3 _directionalFlinch = Vector3.zero;
        private float _flinchRecoverySpeed = 14f;

        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _targetYaw = 0f;
        private float _targetPitch = 0f;
        private float _currentTilt = 0f;
        private PlayerMovement _movement;

        public Camera PlayerCam => playerCamera;
        public Transform CameraHolder => cameraHolder;

        private void Awake()
        {
            if (GetComponent<Camera>() != null)
            {
                Destroy(this);
                return;
            }

            _movement = GetComponent<PlayerMovement>();

            // Mevcut gövde rotasyonunu başlangıç yaw değeri olarak al
            _targetYaw = transform.eulerAngles.y;
            _yaw = _targetYaw;

            // Kamera ve CameraHolder otomatik tespiti / onarımı
            SetupCameraReferences();

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = baseFOV;
                playerCamera.nearClipPlane = 0.05f; // Karakterin kendi içine girmesini engeller
            }

            // Karakterin kendi kapsül mesh'ini birinci şahısta gizle (görüşü kapatmasın)
            MeshRenderer mesh = GetComponent<MeshRenderer>();
            if (mesh != null)
            {
                mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }

            LockCursor();
        }

        private void SetupCameraReferences()
        {
            if (cameraHolder == null)
            {
                // Eğer CameraHolder adında bir alt obje varsa onu kullan
                Transform holder = transform.Find("CameraHolder");
                if (holder != null)
                {
                    cameraHolder = holder;
                }
                else
                {
                    // Yoksa kamera bul ve ebeveynini kontrol et
                    var cam = GetComponentInChildren<Camera>();
                    if (cam != null)
                    {
                        if (cam.transform.parent != null && cam.transform.parent != transform)
                        {
                            cameraHolder = cam.transform.parent;
                        }
                        else
                        {
                            // Kameranın kendisini holder olarak kullan ama göz hizasına ayarla
                            cameraHolder = cam.transform;
                        }
                    }
                }
            }

            if (playerCamera == null && cameraHolder != null)
            {
                playerCamera = cameraHolder.GetComponentInChildren<Camera>();
                if (playerCamera == null)
                    playerCamera = cameraHolder.GetComponent<Camera>();
            }

            // Başlangıç pitch açısını al
            if (cameraHolder != null)
            {
                _targetPitch = cameraHolder.localEulerAngles.x;
                if (_targetPitch > 180f) _targetPitch -= 360f;
                _pitch = _targetPitch;

                // Göz hizasını 1.55m olarak ayarla (1.80m manken ile tam göz hizası)
                if (cameraHolder != transform)
                {
                    Vector3 pos = cameraHolder.localPosition;
                    pos.y = 1.55f;
                    cameraHolder.localPosition = pos;
                }
            }
        }

        private void Update()
        {
            // Ekrana tıklandığında imleci otomatik kilitle
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }

            HandleLook();
            HandleTilt();
            HandleDynamicFOV();

            // ESC ile imleç kilidini aç / kapat
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                    UnlockCursor();
                else
                    LockCursor();
            }
        }

        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Mouse.current == null) return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

            // Hedef açıları güncelle
            _targetYaw += mouseDelta.x;

            float pitchDelta = invertY ? mouseDelta.y : -mouseDelta.y;
            _targetPitch += pitchDelta;
            _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);

            // Yumuşatma (Smooth Look) ile sert piksel sıçramalarını em
            if (smoothLook)
            {
                _yaw = Mathf.Lerp(_yaw, _targetYaw, Time.deltaTime * smoothSpeed);
                _pitch = Mathf.Lerp(_pitch, _targetPitch, Time.deltaTime * smoothSpeed);
            }
            else
            {
                _yaw = _targetYaw;
                _pitch = _targetPitch;
            }

            // 1. Yatay dönüş (Yaw): Karakterin gövdesini sağa/sola döndür
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // 2. Dikey bakış (Pitch) ve Prosedürel Ekran Sarsıntısı (Screen Shake)
            float shakePitch = 0f;
            float shakeYaw = 0f;
            float shakeRoll = 0f;

            if (_trauma > 0.001f)
            {
                _trauma = Mathf.Max(0f, _trauma - Time.deltaTime * traumaDecay);
                float shake = _trauma * _trauma;
                float time = Time.time * shakeFrequency;
                shakePitch = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * maxShakeAngle * shake;
                shakeYaw = (Mathf.PerlinNoise(0f, time) * 2f - 1f) * maxShakeAngle * shake;
                shakeRoll = (Mathf.PerlinNoise(time, time) * 2f - 1f) * (maxShakeAngle * 0.6f) * shake;
            }

            // Hasar yönü yaylanması (Directional Flinch Recovery)
            if (_directionalFlinch.sqrMagnitude > 0.001f)
            {
                _directionalFlinch = Vector3.Lerp(_directionalFlinch, Vector3.zero, Time.deltaTime * _flinchRecoverySpeed);
            }
            else
            {
                _directionalFlinch = Vector3.zero;
            }

            if (cameraHolder != null && cameraHolder != transform)
            {
                cameraHolder.localRotation = Quaternion.Euler(
                    _pitch + shakePitch + _directionalFlinch.x,
                    shakeYaw + _directionalFlinch.y,
                    _currentTilt + shakeRoll + _directionalFlinch.z
                );
            }
        }

        private void HandleTilt()
        {
            if (_movement == null || cameraHolder == null) return;

            float strafeInput = _movement.InputVector.x;
            float targetTilt = -strafeInput * tiltAngle;
            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        }

        private float _dashFovKick = 0f;

        /// <summary>
        /// Dash veya ani patlamalarda kameraya görsel bir FOV şoku / warp hissi verir.
        /// </summary>
        public void DoFovKick(float amount)
        {
            _dashFovKick = amount;
        }

        /// <summary>
        /// Hasar alındığında kamerayı darbenin geldiği yönün tersine doğru fiziksel olarak sarsar.
        /// </summary>
        public void AddDirectionalFlinch(Vector3 localHitDir, float intensity = 1.0f)
        {
            float pitchFlinch = -localHitDir.z * 5.0f * intensity;
            float yawFlinch = localHitDir.x * 4.0f * intensity;
            float rollFlinch = -localHitDir.x * 6.0f * intensity;

            _directionalFlinch += new Vector3(pitchFlinch, yawFlinch, rollFlinch);
            DoFovKick(2.5f * intensity);
            AddTrauma(0.40f * intensity);
        }

        /// <summary>
        /// Silah ateşlendiğinde veya ağır darbe vuruşlarında kameraya tok bir sarsıntı (trauma) ekler.
        /// </summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void HandleDynamicFOV()
        {
            if (playerCamera == null || _movement == null) return;

            float horizontalSpeed = _movement.CurrentHorizontalSpeed;
            float speedRatio = Mathf.InverseLerp(fovStartSpeed, fovMaxSpeed, horizontalSpeed);
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio) + _dashFovKick;

            _dashFovKick = Mathf.Lerp(_dashFovKick, 0f, Time.deltaTime * 6f);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
        }

        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
