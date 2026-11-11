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
    [DefaultExecutionOrder(-100)]
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

        [Header("Strafe Kamera Eğimi (Tilt)")]
        [SerializeField] private float tiltAngle = 2.0f;
        [SerializeField] private float tiltSpeed = 10f;
        [SerializeField] private float slideTilt = 4f;

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
        [SerializeField] private float maxShakeOffset = 0.045f;

        private float _trauma = 0f;
        private Vector3 _directionalFlinch = Vector3.zero;
        private float _flinchRecoverySpeed = 14f;

        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _currentTilt = 0f;
        private PlayerMovement _movement;
        private PlayerCameraFeel _feel;

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
            _feel = GetComponent<PlayerCameraFeel>();
            if (_feel == null) _feel = gameObject.AddComponent<PlayerCameraFeel>();

            // Mevcut gövde rotasyonunu başlangıç yaw değeri olarak al
            _yaw = transform.eulerAngles.y;

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

        /// <summary>
        /// Bakışı verilen yaw ile sıfırlar (pitch, sarsıntı, flinch, ölüm kamera açısı) ve imleci kilitler. Checkpoint retry için.
        /// </summary>
        public void ResetView(float yaw)
        {
            _yaw = yaw;
            _pitch = 0f;
            _currentTilt = 0f;
            _trauma = 0f;
            _shakePitch = 0f;
            _shakeYaw = 0f;
            _shakeRoll = 0f;
            _directionalFlinch = Vector3.zero;
            _dashFovKick = 0f;
            if (_feel != null) _feel.ResetOffsets();
            if (playerCamera != null) playerCamera.fieldOfView = baseFOV;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (cameraHolder != null && cameraHolder != transform)
            {
                cameraHolder.localRotation = Quaternion.identity;
                if (_movement != null)
                    cameraHolder.localPosition = new Vector3(0f, _movement.CameraBaseHeight, 0f);
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

            // Başlangıç rotasyonunu ve sarsıntıları sıfırla
            if (cameraHolder != null)
            {
                cameraHolder.localRotation = Quaternion.identity;
                _pitch = 0f;
                _currentTilt = 0f;
                _trauma = 0f;
                _directionalFlinch = Vector3.zero;

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

            _yaw += mouseDelta.x;
            float pitchDelta = invertY ? mouseDelta.y : -mouseDelta.y;
            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);

            // 1. Yatay dönüş (Yaw): Karakterin gövdesini sağa/sola döndür
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // Pitch'i bu karede hemen uygula: Update'te ateş eden silahlar güncel bakışı kullansın.
            // LateUpdate yine nihai pozu (sarsıntı/feel dahil) yazar.
            ApplyCameraHolderRotation();
        }

        private void LateUpdate()
        {
            HandleTilt();
            HandleDynamicFOV();

            // 2. Dikey bakış (Pitch) ve Prosedürel Ekran Sarsıntısı (Screen Shake)
            float shakePitch = 0f;
            float shakeYaw = 0f;
            float shakeRoll = 0f;
            Vector3 shakePosition = Vector3.zero;

            if (_trauma > 0.001f)
            {
                _trauma = Mathf.Max(0f, _trauma - Time.unscaledDeltaTime * traumaDecay);
                float shake = _trauma * _trauma;
                float time = Time.unscaledTime * shakeFrequency;
                shakePitch = (Mathf.PerlinNoise(time, 0f) * 2f - 1f) * maxShakeAngle * shake;
                shakeYaw = (Mathf.PerlinNoise(0f, time) * 2f - 1f) * maxShakeAngle * shake;
                shakeRoll = (Mathf.PerlinNoise(time, time) * 2f - 1f) * (maxShakeAngle * 0.6f) * shake;
                shakePosition.x = (Mathf.PerlinNoise(time, 31.7f) * 2f - 1f) * maxShakeOffset * shake;
                shakePosition.y = (Mathf.PerlinNoise(73.1f, time) * 2f - 1f) * maxShakeOffset * shake;
            }

            // Hasar yönü yaylanması (Directional Flinch Recovery)
            if (_directionalFlinch.sqrMagnitude > 0.001f)
            {
                _directionalFlinch = Vector3.Lerp(_directionalFlinch, Vector3.zero,
                    1f - Mathf.Exp(-Time.unscaledDeltaTime * _flinchRecoverySpeed));
            }
            else
            {
                _directionalFlinch = Vector3.zero;
            }

            _shakePitch = shakePitch;
            _shakeYaw = shakeYaw;
            _shakeRoll = shakeRoll;

            if (cameraHolder != null && cameraHolder != transform)
            {
                Vector3 feelPosition = _feel != null ? _feel.PositionOffset : Vector3.zero;
                float baseHeight = _movement != null ? _movement.CameraBaseHeight : 1.55f;
                cameraHolder.localPosition = new Vector3(0f, baseHeight, 0f) + feelPosition + shakePosition;
                ApplyCameraHolderRotation();
            }
        }

        // Last frame's shake components, so Update can re-compose the pose with the fresh pitch
        // (weapons fire in Update along cam.forward) without double-applying any offset.
        private float _shakePitch;
        private float _shakeYaw;
        private float _shakeRoll;

        private void ApplyCameraHolderRotation()
        {
            if (cameraHolder == null || cameraHolder == transform) return;

            Vector3 feelRotation = _feel != null ? _feel.RotationOffset : Vector3.zero;
            cameraHolder.localRotation = Quaternion.Euler(
                _pitch + _shakePitch + _directionalFlinch.x - feelRotation.x,
                _shakeYaw + _directionalFlinch.y + feelRotation.y,
                _currentTilt + _shakeRoll + _directionalFlinch.z + feelRotation.z
            );
        }

        private void HandleTilt()
        {
            if (_movement == null || cameraHolder == null) return;

            float lateralVelocity = Vector3.Dot(transform.right, _movement.Velocity);
            float targetTilt = Mathf.Clamp(-lateralVelocity / Mathf.Max(0.01f, _movement.WalkSpeed) * tiltAngle,
                -tiltAngle * 1.5f, tiltAngle * 1.5f);
            if (_movement.IsSliding) targetTilt += slideTilt;
            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * tiltSpeed);
        }

        private float _dashFovKick = 0f;

        /// <summary>
        /// Dash veya ani patlamalarda kameraya görsel bir FOV şoku / warp hissi verir.
        /// </summary>
        public void DoFovKick(float amount)
        {
            if (amount * _dashFovKick > 0f)
            {
                if (Mathf.Abs(amount) > Mathf.Abs(_dashFovKick)) _dashFovKick = amount;
            }
            else
            {
                _dashFovKick = amount;
            }
            _dashFovKick = Mathf.Clamp(_dashFovKick, -12f, 22f);
        }

        public void AddViewPunch(float pitchDegrees, float yawDegrees)
        {
            if (_feel != null) _feel.AddViewPunch(pitchDegrees, yawDegrees);
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
            float kick = _dashFovKick;
            _dashFovKick = Mathf.Lerp(_dashFovKick, 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
            if (playerCamera == null || _movement == null) return;

            float horizontalSpeed = _movement.CurrentHorizontalSpeed;
            float speedRatio = Mathf.InverseLerp(fovStartSpeed, fovMaxSpeed, horizontalSpeed);
            float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio) + kick;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV,
                1f - Mathf.Exp(-Time.unscaledDeltaTime * fovLerpSpeed));
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
