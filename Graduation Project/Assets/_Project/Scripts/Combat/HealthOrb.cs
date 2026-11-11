using UnityEngine;

namespace RetroFPS.Combat
{
    public enum OrbState
    {
        Inactive,
        Launching,
        Hovering,
        Vacuuming
    }

    /// <summary>
    /// Sektör standardı, sıfır GC tahsisli ve manyetik vakum mekanizmalı Can Küresi (Health Orb).
    /// - Düşman öldüğünde hafif bir yay (Arc) hareketiyle fırlayarak yere iner.
    /// - Yerde havada asılı kalır ve ritmik gotik zümrüt ışımasıyla salınır (Hover & Bob).
    /// - Oyuncu menzile girdiğinde ve canı eksikse oyuncunun göğsüne doğru katlanarak hızlanır (Vacuum Magnet).
    /// - Toplandığında kinetik iyileşme sayısı, yeşil ekran parıltısı ve ferahlatıcı ses çalar.
    /// - 16 saniye sonra kaybolmadan önce hızlıca yanıp sönerek oyuncuyu uyarır.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthOrb : MonoBehaviour
    {
        [Header("İyileşme Değerleri")]
        [Tooltip("Bu kürenin kazandırdığı can miktarı")]
        [SerializeField] private float healAmount = 15f;

        [Tooltip("100 HP üzerinde geçici can (Overheal) vermesine izin verilsin mi?")]
        [SerializeField] private bool allowOverheal = false;

        [Tooltip("Overheal açıkken çıkılabilecek maksimum tavan")]
        [SerializeField] private float overhealCap = 125f;

        [Header("Vakum & Manyetik Çekim Parametreleri")]
        [Tooltip("Manyetik çekimin başlayacağı temel algılama yarıçapı (metre)")]
        [SerializeField] private float attractionRadius = 6.0f;

        [Tooltip("Oyuncu Dash atarken artırılmış algılama menzili çarpanı")]
        [SerializeField] private float dashAttractionMultiplier = 1.6f;

        [Tooltip("Kürenin toplandığı kabul edilen temas mesafesi (metre)")]
        [SerializeField] private float pickupRadius = 0.90f;

        [Tooltip("Vakum başlangıç uçuş hızı")]
        [SerializeField] private float initialVacuumSpeed = 5.0f;

        [Tooltip("Küre oyuncuya yaklaştıkça artan çekim ivmesi")]
        [SerializeField] private float vacuumAcceleration = 28f;

        [Tooltip("Kürenin ulaşabileceği maksimum vakum uçuş hızı")]
        [SerializeField] private float maxVacuumSpeed = 32f;

        [Header("Fiziksel Salınım ve Görsel Efektler")]
        [Tooltip("Yerden ideal asılı kalma yüksekliği")]
        [SerializeField] private float hoverHeight = 0.55f;

        [Tooltip("Havada asılı kalma dikey dalgalanma genliği")]
        [SerializeField] private float bobAmplitude = 0.12f;

        [Tooltip("Dikey dalgalanma frekansı")]
        [SerializeField] private float bobFrequency = 3.2f;

        [Tooltip("Kendi etrafında dönme hızı (derece/saniye)")]
        [SerializeField] private float rotationSpeed = 120f;

        [Tooltip("Toplam sahnede kalma süresi (saniye)")]
        [SerializeField] private float lifetime = 10.0f;

        [Tooltip("Kaybolmadan kaç saniye önce yanıp sönme uyarısı başlasın?")]
        [SerializeField] private float blinkWarningDuration = 3.0f;

        [Header("Görsel Renk Paleti (Occult Emerald)")]
        [SerializeField] private Color orbCoreColor = new Color(0.08f, 1.0f, 0.45f, 1f);
        [SerializeField] private Color orbEmissionColor = new Color(0.20f, 2.5f, 0.85f, 1f);

        // Çalışma zamanı durum değişkenleri
        private OrbState _state = OrbState.Inactive;
        private float _currentHealAmount;
        private float _lifeTimer;
        private float _bobTimer;
        private float _currentVacuumSpeed;
        private Vector3 _velocity;
        private float _restingY;

        // Önbelleklenmiş referanslar
        private Transform _playerTransform;
        private PlayerHealth _playerHealth;
        private PlayerMovement _playerMovement;
        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private HealthOrbManager _manager;

        public bool IsActive => _state != OrbState.Inactive;
        public float HealAmount => _currentHealAmount;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            EnsureVisualMeshAndMaterial();
            CachePlayerReferences();
        }

        private void CachePlayerReferences()
        {
            if (_playerTransform != null && _playerHealth != null) return;

            PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>();
            if (player != null)
            {
                _playerHealth = player;
                _playerTransform = player.transform;
                _playerMovement = player.GetComponent<PlayerMovement>();
            }
        }

        /// <summary>
        /// Proje sahnesinde harici prefab yoksa dahi kusursuz çalışan gotik okült zümrüt küre oluşturur.
        /// </summary>
        private void EnsureVisualMeshAndMaterial()
        {
            if (_renderer != null) return;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "OrbVisual";
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one * 0.42f;

            Collider col = visual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _renderer = visual.GetComponent<Renderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader) { name = "Mat_OccultHealthOrb" };
            mat.color = orbCoreColor;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, orbCoreColor);
            if (mat.HasProperty(EmissionColorId))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, orbEmissionColor);
            }
            _renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Havuzdan aktif edildiğinde çağrılır.
        /// </summary>
        public void Spawn(Vector3 spawnPosition, float healValue, HealthOrbManager manager)
        {
            _manager = manager;
            _currentHealAmount = healValue > 0f ? healValue : healAmount;
            _lifeTimer = lifetime;
            _bobTimer = Random.Range(0f, 6.28f);
            _currentVacuumSpeed = initialVacuumSpeed;

            CachePlayerReferences();

            transform.position = spawnPosition;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            gameObject.SetActive(true);

            if (_renderer != null) _renderer.enabled = true;

            // Fırlatma arkı: Hafif yukarı ve rastgele yatay koni (Pop impulse)
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.5f);
            _velocity = new Vector3(randomCircle.x, Random.Range(3.2f, 4.8f), randomCircle.y);

            // Zemin temas yüksekliğini raycast ile bul
            if (Physics.Raycast(spawnPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                _restingY = hit.point.y + hoverHeight;
            }
            else
            {
                _restingY = spawnPosition.y + hoverHeight;
            }

            _state = OrbState.Launching;
        }

        private void Update()
        {
            if (_state == OrbState.Inactive) return;

            float dt = Time.deltaTime;

            // 1. Ömür Takibi ve Yanıp Sönme Uyarısı (Despawn Blink Warning)
            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                Despawn();
                return;
            }

            if (_lifeTimer <= blinkWarningDuration && _renderer != null)
            {
                float blinkProgress = 1f - (_lifeTimer / blinkWarningDuration);
                float blinkSpeed = Mathf.Lerp(8f, 26f, blinkProgress);
                _renderer.enabled = (Mathf.Sin(Time.time * blinkSpeed) > 0f);
            }

            // 2. Kendi ekseni etrafında gotik kristal dönüşü
            transform.Rotate(Vector3.up, rotationSpeed * dt, Space.World);

            // 3. Durum Makinesi
            switch (_state)
            {
                case OrbState.Launching:
                    UpdateLaunching(dt);
                    break;

                case OrbState.Hovering:
                    UpdateHovering(dt);
                    break;

                case OrbState.Vacuuming:
                    UpdateVacuuming(dt);
                    break;
            }
        }

        private void UpdateLaunching(float dt)
        {
            // Yerçekimi ivmesi
            _velocity.y -= 14f * dt;
            transform.position += _velocity * dt;

            // Zemin yüksekliğine ulaştığında yay hareketini bitir ve havada asılı kal
            if (transform.position.y <= _restingY)
            {
                Vector3 pos = transform.position;
                pos.y = _restingY;
                transform.position = pos;
                _velocity = Vector3.zero;
                _state = OrbState.Hovering;
            }
        }

        private void UpdateHovering(float dt)
        {
            // Dikey sinüs dalgalanması (Hover Bob)
            _bobTimer += dt * bobFrequency;
            float targetY = _restingY + Mathf.Sin(_bobTimer) * bobAmplitude;
            Vector3 pos = transform.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY, dt * 1.5f);
            transform.position = pos;

            // Oyuncu kontrolü
            if (_playerTransform == null || _playerHealth == null || _playerHealth.IsDead) return;

            // Sektör Standardı: Oyuncunun canı zaten %100 ise ve overheal kapalıysa küre yerde kalsın!
            // Boşa harcanmaz, taktiksel bir kaynak olarak arenada bekler.
            if (!allowOverheal && _playerHealth.CurrentHealth >= _playerHealth.MaxHealth) return;

            // Mesafe kontrolü (Sıfır GC kare mesafe)
            Vector3 playerCenter = _playerTransform.position + Vector3.up * 1.0f;
            float sqrDist = (playerCenter - transform.position).sqrMagnitude;

            float currentAttractionRadius = attractionRadius;
            if (_playerMovement != null && _playerMovement.IsDashing)
            {
                currentAttractionRadius *= dashAttractionMultiplier;
            }

            if (sqrDist <= currentAttractionRadius * currentAttractionRadius)
            {
                _state = OrbState.Vacuuming;
                _currentVacuumSpeed = initialVacuumSpeed;
            }
        }

        private void UpdateVacuuming(float dt)
        {
            if (_playerTransform == null || _playerHealth == null || _playerHealth.IsDead)
            {
                _state = OrbState.Hovering;
                return;
            }

            Vector3 playerCenter = _playerTransform.position + Vector3.up * 1.0f;
            Vector3 toPlayer = playerCenter - transform.position;
            float dist = toPlayer.magnitude;

            // Toplanma kontrolü
            if (dist <= pickupRadius)
            {
                Collect();
                return;
            }

            // Katlanarak artan manyetik çekim ivmesi
            _currentVacuumSpeed = Mathf.Min(maxVacuumSpeed, _currentVacuumSpeed + vacuumAcceleration * dt);
            Vector3 dir = toPlayer / Mathf.Max(0.001f, dist);
            transform.position += dir * (_currentVacuumSpeed * dt);
        }

        private void Collect()
        {
            if (_playerHealth == null) return;

            // Canı yenile
            bool healed = _playerHealth.Heal(_currentHealAmount, allowOverheal, overhealCap);
            if (!healed && !allowOverheal && _playerHealth.CurrentHealth >= _playerHealth.MaxHealth)
            {
                // Can zaten fullenmişse geri havada asılı kalma moduna geç
                _state = OrbState.Hovering;
                return;
            }

            // Tok ses, görsel sayı ve kamera flaşı tetikle
            if (CombatAudio.Instance != null)
            {
                CombatAudio.Instance.PlayHealthPickup();
            }

            if (DamageNumberSystem.Instance != null)
            {
                DamageNumberSystem.Instance.SpawnHealNumber(transform.position, _currentHealAmount);
            }

            Despawn();
        }

        public void Despawn()
        {
            _state = OrbState.Inactive;
            gameObject.SetActive(false);

            if (_manager != null)
            {
                _manager.ReturnToPool(this);
            }
        }
    }
}
