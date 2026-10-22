using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RetroFPS.Combat;

namespace RetroFPS
{
    /// <summary>
    /// Sol El İkincil Taktiksel Araç: Çivili Buckler (Spiked Buckler / Parry Kalkanı).
    /// - Q veya Fare Orta Tuşu ile parry savurması yapar.
    /// - 0.35 saniyelik aktif parry penceresi boyunca:
    ///   * Gelen menzilli mermileri (EnemyProjectile) algılar ve baktığınız yöne 2.5x hasarla geri yansıtır.
    ///   * Yakın dövüş darbelerini (Ghul / Melee) %100 bloke eder ve saldıran düşmanı 2 saniye sersemletir (Stun).
    /// - Çift katmanlı koruma: Hem aktif alan taraması (Every Frame Sweep) hem de temas anı reaktif koruma içerir.
    /// - 3D Modelci için hazır soket sistemi (customShieldModel) sunar; model yoksa gotik kukla kalkan oluşturur.
    /// </summary>
    [RequireComponent(typeof(PlayerLook))]
    [DisallowMultipleComponent]
    public class PlayerParryController : MonoBehaviour
    {
        [Header("Parry & Zamanlama Parametreleri")]
        [Tooltip("Kusursuz savuşturma (Parry) aktif penceresi (saniye)")]
        [SerializeField] private float parryActiveWindow = 0.35f;

        [Tooltip("Savurma sonrası toparlanma süresi (saniye)")]
        [SerializeField] private float parryRecoveryDuration = 0.20f;

        [Tooltip("Tekrar parry yapabilmek için gereken bekleme süresi")]
        [SerializeField] private float parryCooldown = 0.50f;

        [Tooltip("Oyuncunun baktığı yöne göre parry kabul açısı (derece)")]
        [SerializeField] private float parryAngle = 130f;

        [Tooltip("Mermileri havada yakalayıp yansıtma tarama menzili (metre)")]
        [SerializeField] private float projectileCatchRadius = 4.2f;

        [Header("Sersemletme ve Yansıtma Değerleri")]
        [SerializeField] private float meleeStunDuration = 2.2f;
        [SerializeField] private float reflectedSpeedMultiplier = 1.7f;
        [SerializeField] private float reflectedDamageMultiplier = 2.5f;

        [Header("İşitsel Geri Bildirim")]
        [Tooltip("İsteğe bağlı özel parry sesi; boşsa dinamik metalik çınlama sentezlenir.")]
        [SerializeField] private AudioClip customParrySound;

        [Header("Görsel & Model Soketi (3D Modelci İçin)")]
        [Tooltip("Blender/Maya'dan aktarılan kalkan 3D modeli buraya bağlanır.")]
        [SerializeField] private GameObject customShieldModel;

        [Tooltip("Kalkanın kameraya bağlı duracağı sol el soketi")]
        [SerializeField] private Transform leftHandSocket;

        private PlayerLook _look;
        private Camera _cam;
        private Transform _cameraHolder;
        private GameObject _proceduralShieldRoot;
        private Transform _shieldVisualTransform;
        private Renderer[] _shieldRenderers;

        private float _stateTimer = 0f;
        private float _cooldownTimer = 0f;
        private bool _isParrying = false;
        private bool _isInRecovery = false;

        private readonly Collider[] _overlapResults = new Collider[16];

        public event Action OnParryTriggered;
        public event Action<bool> OnParrySuccess; // true: Mermi, false: Yakın dövüş

        public bool IsParrying => _isParrying;
        public bool IsOnCooldown => _cooldownTimer > 0f;
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);
        public float CooldownNormalized => Mathf.Clamp01(_cooldownTimer / parryCooldown);

        private Vector3 _defaultLocalPos;
        private Quaternion _defaultLocalRot;
        private Vector3 _parryPunchPos;
        private Quaternion _parryPunchRot;

        private void Awake()
        {
            _look = GetComponent<PlayerLook>();
            if (_look != null)
            {
                _cam = _look.PlayerCam;
                _cameraHolder = _look.CameraHolder;
            }
            if (_cam == null) _cam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (_cameraHolder == null && _cam != null) _cameraHolder = _cam.transform.parent ?? _cam.transform;

            SetupLeftHandSocket();
            SetupShieldVisual();
        }

        private void SetupLeftHandSocket()
        {
            if (leftHandSocket == null && _cameraHolder != null)
            {
                Transform found = _cameraHolder.Find("FP_LeftHand_Socket");
                if (found != null)
                {
                    leftHandSocket = found;
                }
                else
                {
                    GameObject socketGO = new GameObject("FP_LeftHand_Socket");
                    socketGO.transform.SetParent(_cameraHolder, false);
                    socketGO.transform.localPosition = new Vector3(-0.38f, -0.32f, 0.60f);
                    socketGO.transform.localRotation = Quaternion.Euler(12f, 24f, -10f);
                    leftHandSocket = socketGO.transform;
                }
            }

            _defaultLocalPos = new Vector3(-0.38f, -0.32f, 0.60f);
            _defaultLocalRot = Quaternion.Euler(12f, 24f, -10f);
            _parryPunchPos = new Vector3(-0.10f, -0.12f, 0.85f);
            _parryPunchRot = Quaternion.Euler(-18f, -12f, 25f);
        }

        private void SetupShieldVisual()
        {
            if (customShieldModel != null)
            {
                _shieldVisualTransform = customShieldModel.transform;
                _shieldRenderers = customShieldModel.GetComponentsInChildren<Renderer>();
                return;
            }

            if (leftHandSocket == null) return;

            // Modelci kendi modelini koyana kadar ekranda görünen gotik çivili kalkan kuklası
            _proceduralShieldRoot = new GameObject("SpikedBuckler_Procedural");
            _proceduralShieldRoot.transform.SetParent(leftHandSocket, false);
            _proceduralShieldRoot.transform.localPosition = Vector3.zero;
            _proceduralShieldRoot.transform.localRotation = Quaternion.identity;

            // Kalkan Gövdesi (Disk)
            GameObject disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disk.name = "Buckler_Disc";
            disk.transform.SetParent(_proceduralShieldRoot.transform, false);
            disk.transform.localScale = new Vector3(0.36f, 0.025f, 0.36f);
            disk.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Kalkan Göbek Bombesi
            GameObject boss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boss.name = "Buckler_Boss";
            boss.transform.SetParent(_proceduralShieldRoot.transform, false);
            boss.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
            boss.transform.localPosition = new Vector3(0f, 0f, 0.04f);

            // 4 Adet Gotik Çivi (Spikes)
            for (int i = 0; i < 4; i++)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = $"Buckler_Spike_{i}";
                spike.transform.SetParent(_proceduralShieldRoot.transform, false);
                float angle = i * 90f * Mathf.Deg2Rad;
                spike.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.13f, Mathf.Sin(angle) * 0.13f, 0.05f);
                spike.transform.localScale = new Vector3(0.028f, 0.05f, 0.028f);
                spike.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Kamera görünümünde collider'ları temizle
            Collider[] colliders = _proceduralShieldRoot.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);

            Material shieldMat = CreateMaterial("Mat_SpikedBuckler", new Color(0.18f, 0.20f, 0.24f));
            _shieldRenderers = _proceduralShieldRoot.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < _shieldRenderers.Length; i++) _shieldRenderers[i].material = shieldMat;

            _shieldVisualTransform = _proceduralShieldRoot.transform;
        }

        private Material CreateMaterial(string name, Color col)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Standard");

            Material mat = new Material(shader) { name = name };
            mat.color = col;
            return mat;
        }

        private void Update()
        {
            HandleCooldownAndTimers();
            HandleInput();

            // Parry aktif penceresi boyunca her kare yaklaşan mermileri tara
            if (_isParrying)
            {
                CheckAndReflectProjectiles();
            }

            UpdateShieldProceduralAnimation();
        }

        private void HandleCooldownAndTimers()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

            if (_isParrying)
            {
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    _isParrying = false;
                    _isInRecovery = true;
                    _stateTimer = parryRecoveryDuration;
                }
            }
            else if (_isInRecovery)
            {
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    _isInRecovery = false;
                }
            }
        }

        private void HandleInput()
        {
            if (Keyboard.current == null && Mouse.current == null) return;

            // Q Tuşu veya Fare Orta Tuşu ile Savuşturma (Parry)
            bool parryPressed = (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                             || (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame);

            if (parryPressed && !IsOnCooldown && !_isParrying && !_isInRecovery)
            {
                ExecuteParry();
            }
        }

        private void ExecuteParry()
        {
            _isParrying = true;
            _isInRecovery = false;
            _stateTimer = parryActiveWindow;
            _cooldownTimer = parryCooldown;

            OnParryTriggered?.Invoke();

            // İlk karede hemen tara
            CheckAndReflectProjectiles();

            Debug.Log("<color=yellow><b>[PARRY ACTIVE]</b></color> Shield swung! 0.35s active parry window opened.");
        }

        /// <summary>
        /// Sahnedeki aktif düşman mermilerini tarar ve oyuncunun önündekileri nişangaha geri yansıtır.
        /// </summary>
        private void CheckAndReflectProjectiles()
        {
            if (_cam == null) return;

            Vector3 aimDirection = _cam.transform.forward;
            Vector3 camPos = _cam.transform.position;

            for (int i = EnemyProjectile.ActiveProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile proj = EnemyProjectile.ActiveProjectiles[i];
                if (proj == null || proj.IsDeflected) continue;

                Vector3 toProj = proj.transform.position - camPos;
                float dist = toProj.magnitude;

                if (dist <= projectileCatchRadius)
                {
                    float angle = Vector3.Angle(aimDirection, toProj.normalized);
                    if (angle <= parryAngle * 0.5f)
                    {
                        proj.Deflect(aimDirection, reflectedSpeedMultiplier, reflectedDamageMultiplier);
                        TriggerParryFeedback(true);

                        if (Combat.DamageNumberSystem.Instance != null)
                        {
                            Combat.DamageNumberSystem.Instance.SpawnDamageNumber(proj.transform.position, proj.Damage * reflectedDamageMultiplier, true, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Merminin hedefe çarpış anında oyuncunun o yöne bakıp bakmadığını doğrular.
        /// </summary>
        public bool IsFacingTarget(Vector3 targetPos)
        {
            if (_cam == null) return true;
            Vector3 toTarget = (targetPos - _cam.transform.position).normalized;
            return Vector3.Angle(_cam.transform.forward, toTarget) <= parryAngle * 0.5f;
        }

        public Vector3 GetAimDirection()
        {
            return _cam != null ? _cam.transform.forward : transform.forward;
        }

        public void NotifyProjectileParried()
        {
            TriggerParryFeedback(true);
        }

        /// <summary>
        /// PlayerHealth.TakeDamage sırasında çağrılır.
        /// Gelen saldırı parry penceresindeyse hasarı %100 sıfırlar ve düşmanı sersemletir.
        /// </summary>
        public bool TryParryMelee(Vector3 hitPoint, Vector3 hitNormal, float damage, GameObject attacker = null)
        {
            if (!_isParrying) return false;
            if (_cam == null) return false;

            // Saldırganın oyuncunun ön tarafında olup olmadığını yatay açıyla kontrol et
            Vector3 toAttacker = attacker != null
                ? (attacker.transform.position - transform.position)
                : (hitPoint - transform.position);

            toAttacker.y = 0f;

            if (toAttacker.sqrMagnitude > 0.001f)
            {
                Vector3 playerForwardHoriz = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up).normalized;
                float angle = Vector3.Angle(playerForwardHoriz, toAttacker.normalized);

                if (angle > parryAngle * 0.5f)
                {
                    // Arkadan veya kör noktadan gelen saldırılar parrylenemez
                    return false;
                }
            }

            // 1. Doğrudan saldıran düşmanı sersemlet (Stun)
            bool stunned = false;
            if (attacker != null)
            {
                IStunnable stunnable = attacker.GetComponentInParent<IStunnable>();
                if (stunnable != null)
                {
                    stunnable.Stun(meleeStunDuration, toAttacker.normalized);
                    stunned = true;
                }
            }

            // 2. Saldırgan null ise etraftaki en yakın düşmanı bul ve sersemlet
            if (!stunned)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(transform.position, 4.0f, _overlapResults, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hitCount; i++)
                {
                    Collider col = _overlapResults[i];
                    if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

                    IStunnable stunnable = col.GetComponentInParent<IStunnable>();
                    if (stunnable != null)
                    {
                        stunnable.Stun(meleeStunDuration, (col.transform.position - transform.position).normalized);
                        break;
                    }
                }
            }

            TriggerParryFeedback(false);

            if (Combat.DamageNumberSystem.Instance != null && attacker != null)
            {
                Combat.DamageNumberSystem.Instance.SpawnDamageNumber(attacker.transform.position + Vector3.up * 1.5f, 0f, true, true);
            }

            return true;
        }

        private static AudioClip _cachedParryClip;

        private static AudioClip GetOrCreateParryClip()
        {
            if (_cachedParryClip != null) return _cachedParryClip;

            int sampleRate = 44100;
            float duration = 0.24f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 20f); // Keskin sönümlenme

                // Metalik çınlama harmonikleri: 1150 Hz, 2400 Hz, 4600 Hz
                float tone1 = Mathf.Sin(2f * Mathf.PI * 1150f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.65f;
                float tone3 = Mathf.Sin(2f * Mathf.PI * 4600f * t) * 0.35f;

                // Ağır bas gövdesi: 110 Hz
                float bass = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 30f) * 0.9f;

                // Transient metalik vuruş gürültüsü
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 140f) * 0.5f;

                samples[i] = Mathf.Clamp((tone1 + tone2 + tone3 + bass + noise) * envelope, -1f, 1f);
            }

            _cachedParryClip = AudioClip.Create("Synthesized_ParryClang", sampleCount, 1, sampleRate, false);
            _cachedParryClip.SetData(samples, 0);
            return _cachedParryClip;
        }

        private void TriggerParryFeedback(bool isProjectile)
        {
            Vector3 effectPos = _cam != null
                ? _cam.transform.position + _cam.transform.forward * 1.5f
                : transform.position + transform.forward * 1.5f;

            Vector3 effectForward = _cam != null ? _cam.transform.forward : transform.forward;

            // 1. İşitsel Geri Bildirim: Tok Metalik Kalkan Çınlaması (CLANG!)
            AudioClip clipToPlay = customParrySound != null ? customParrySound : GetOrCreateParryClip();
            if (clipToPlay != null)
            {
                AudioSource.PlayClipAtPoint(clipToPlay, effectPos, 1.0f);
            }

            // 2. Görsel Şok Dalgası ve Kıvılcım Patlaması
            ParryVFX.Spawn(effectPos, effectForward, isProjectile);

            // 3. Kamera Travması & FOV Darbesi (Kinetic FOV Punch)
            if (_look != null)
            {
                _look.AddTrauma(0.55f);
                _look.DoFovKick(-5.0f); // Anlık odaklanma zoom'u
            }

            // 4. Mikro Hitstop (Ekran Dondurma - 0.10 saniye)
            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(0.10f, 0.02f);
            }

            OnParrySuccess?.Invoke(isProjectile);

            Debug.Log($"<color=cyan><b>[PERFECT PARRY SUCCESS!]</b></color> {(isProjectile ? "Projectile reflected with electro-blue surge!" : "Melee strike deflected and enemy staggered!")}");
        }

        private void UpdateShieldProceduralAnimation()
        {
            if (leftHandSocket == null) return;

            Vector3 targetPos;
            Quaternion targetRot;

            if (_isParrying)
            {
                targetPos = _parryPunchPos;
                targetRot = _parryPunchRot;
            }
            else
            {
                targetPos = _defaultLocalPos;
                targetRot = _defaultLocalRot;
            }

            float lerpSpeed = _isParrying ? 32f : 10f;
            leftHandSocket.localPosition = Vector3.Lerp(leftHandSocket.localPosition, targetPos, Time.deltaTime * lerpSpeed);
            leftHandSocket.localRotation = Quaternion.Slerp(leftHandSocket.localRotation, targetRot, Time.deltaTime * lerpSpeed);
        }
    }
}
