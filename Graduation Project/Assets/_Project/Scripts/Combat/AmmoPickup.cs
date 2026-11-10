using UnityEngine;
using RetroFPS.Weapons;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Sektör standardı, fiziksel salınımlı ve manyetik vakum mekanizmalı Cephane Paketi (Ammo Pickup).
    /// - Sahneye yerleştirilebilir veya ölen düşmanlardan saçılabilir.
    /// - Yerde hafifçe havada asılı kalır ve döner (Hover & Rotate).
    /// - Oyuncu menzile girdiğinde ve o cephaneye ihtiyacı varsa oyuncunun göğsüne doğru hızla çekilir.
    /// - Toplandığında kinetik bildirim metni ve tok metalik ses çalar.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmmoPickup : MonoBehaviour
    {
        [Header("Cephane Türü & Miktarı")]
        [SerializeField] private AmmoType ammoType = AmmoType.Shells;
        [SerializeField] private int ammoAmount = 4;

        [Header("Vakum & Manyetik Çekim")]
        [SerializeField] private float attractionRadius = 5.5f;
        [SerializeField] private float pickupRadius = 0.95f;
        [SerializeField] private float vacuumSpeed = 8f;
        [SerializeField] private float vacuumAcceleration = 24f;

        [Header("Salınım ve Görsel Efektler")]
        [SerializeField] private float hoverHeight = 0.5f;
        [SerializeField] private float bobAmplitude = 0.10f;
        [SerializeField] private float bobFrequency = 3.0f;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float lifetime = 25f;

        private Transform _playerTransform;
        private PlayerAmmoInventory _playerInventory;
        private bool _isVacuuming = false;
        private float _currentVacuumSpeed;
        private float _lifeTimer = 0f;
        private float _bobTimer = 0f;
        private float _baseY;

        public AmmoType Type => ammoType;
        public int Amount => ammoAmount;

        public void Initialize(AmmoType type, int amount, Vector3 spawnPosition)
        {
            ammoType = type;
            ammoAmount = amount;
            transform.position = spawnPosition;
            _baseY = spawnPosition.y;
            SetupVisualColors();
        }

        private void Start()
        {
            _baseY = transform.position.y;
            _currentVacuumSpeed = vacuumSpeed;
            ResolvePlayer();
            SetupVisualColors();
        }

        private void ResolvePlayer()
        {
            if (_playerTransform != null && _playerInventory != null) return;

            PlayerMovement movement = FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                _playerTransform = movement.transform;
                _playerInventory = movement.GetComponent<PlayerAmmoInventory>();
            }
        }

        private void SetupVisualColors()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            Color glowColor = (ammoType == AmmoType.Shells)
                ? new Color(1.0f, 0.55f, 0.1f) // Amber / Gold for Shotgun Shells
                : new Color(0.1f, 0.85f, 1.0f); // Cyan / Blue for Bolts

            Material mat = rend.material;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", glowColor);
                if (mat.HasProperty("_Color")) mat.color = glowColor;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", glowColor * 1.5f);
                }
            }
        }

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            ResolvePlayer();

            if (_playerTransform == null || _playerInventory == null)
            {
                ExecuteIdleBobbing();
                return;
            }

            // Oyuncunun bu cephaneye ihtiyacı var mı?
            bool needsAmmo = _playerInventory.GetCurrentAmmo(ammoType) < _playerInventory.GetMaxAmmo(ammoType);
            Vector3 playerChestPos = _playerTransform.position + Vector3.up * 1.0f;
            float distSq = (playerChestPos - transform.position).sqrMagnitude;

            if (needsAmmo && distSq <= attractionRadius * attractionRadius)
            {
                _isVacuuming = true;
            }

            if (_isVacuuming)
            {
                _currentVacuumSpeed += vacuumAcceleration * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, playerChestPos, _currentVacuumSpeed * Time.deltaTime);

                if (distSq <= pickupRadius * pickupRadius)
                {
                    Collect();
                }
            }
            else
            {
                ExecuteIdleBobbing();
            }
        }

        private void ExecuteIdleBobbing()
        {
            _bobTimer += Time.deltaTime * bobFrequency;
            float newY = _baseY + hoverHeight + Mathf.Sin(_bobTimer) * bobAmplitude;
            Vector3 pos = transform.position;
            pos.y = newY;
            transform.position = pos;

            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }

        private void Collect()
        {
            if (_playerInventory != null)
            {
                int added = _playerInventory.AddAmmo(ammoType, ammoAmount);
                if (added > 0)
                {
                    // Combat Audio: Cephane doldurma sesi
                    if (CombatAudio.Instance != null)
                    {
                        CombatAudio.Instance.PlayAmmoPickup();
                    }

                    // Hasar sayıları sistemi üzerinden "+X SHELLS" veya "+X BOLTS" göster
                    if (DamageNumberSystem.Instance != null)
                    {
                        DamageNumberSystem.Instance.SpawnDamageNumber(
                            transform.position + Vector3.up * 0.4f,
                            added,
                            true
                        );
                    }
                }
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Sahnede hızlıca kod ile prosedürel 3D Cephane Kutusu nesnesi oluşturur.
        /// </summary>
        public static AmmoPickup CreatePickupGameObject(Vector3 position, AmmoType type, int amount)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"AmmoPickup_{type}";
            go.transform.position = position;
            go.transform.localScale = (type == AmmoType.Shells)
                ? new Vector3(0.28f, 0.22f, 0.38f) // Ağır fişek kutusu
                : new Vector3(0.20f, 0.18f, 0.45f); // Ok sadağı

            Collider col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            go.layer = GameLayers.Pickup;

            AmmoPickup pickup = go.AddComponent<AmmoPickup>();
            pickup.Initialize(type, amount, position);
            return pickup;
        }
    }
}
