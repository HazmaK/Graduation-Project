using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS
{
    /// <summary>
    /// Vuruş hissini, hasar tepkilerini ve komboları test etmek için tasarlanmış Reaktif Hasar Mankeni.
    /// - Hasar aldığında beyaz/kırmızı parlar (Damage Flash)
    /// - Darbe yönüne doğru fiziksel olarak yaylanır/sarsılır (Punching Bag Wobble)
    /// - Kafadan vuruşlarda (Headshot) kritik darbe tepkisi verir
    /// - 2.5 saniye hasar almazsa canını otomatik yeniler
    /// </summary>
    public class CombatDummy : MonoBehaviour, IDamageable
    {
        [Header("Sağlık Ayarları")]
        [SerializeField] private float maxHealth = 300f;
        [SerializeField] private float autoHealDelay = 2.5f;

        [Header("Görsel Tepki (Wobble & Flash)")]
        [SerializeField] private float wobbleForce = 12f;
        [SerializeField] private float wobbleRecoverySpeed = 10f;
        [SerializeField] private Color hitFlashColor = new Color(2.2f, 2.2f, 2.2f, 1f);
        [SerializeField] private Color criticalHitFlashColor = new Color(3.2f, 2.8f, 0.35f, 1f);

        private float _currentHealth;
        private float _timeSinceLastHit;
        private Quaternion _originalRotation;
        private Quaternion _targetWobbleRotation;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private float _flashTimer = 0f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private bool[] _supportsEmission;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;

        private void Start()
        {
            CombatTargetLookup.RegisterHierarchy(this);
            _supportsEmission = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material material = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
                _supportsEmission[i] = material != null && material.HasProperty(EmissionColorId);
            }
        }

        private void Awake()
        {
            _currentHealth = maxHealth;
            _originalRotation = transform.localRotation;
            _targetWobbleRotation = _originalRotation;

            _renderers = GetComponentsInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();

            // Mankenin collider'larını hazırla
            SetupColliders();
            CombatTargetLookup.RegisterHierarchy(this);
        }

        private void SetupColliders()
        {
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;

                bool isHead = col.gameObject.name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Hitbox hb = col.GetComponent<Hitbox>();
                if (hb == null)
                {
                    hb = col.gameObject.AddComponent<Hitbox>();
                }
                hb.Initialize(this, isHead ? 2.0f : 1.0f, isHead);
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnHitImpact(hitPoint, hitNormal, isCritical, true);
            if (CombatAudio.Instance != null)
                CombatAudio.Instance.PlayFleshImpact(hitPoint, isCritical);
            _timeSinceLastHit = 0f;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            // 1. Görsel Darbe Parlaması (Flash)
            _flashTimer = isCritical ? 0.12f : 0.06f;
            ApplyFlashColor(isCritical ? criticalHitFlashColor : hitFlashColor);

            // 2. Darbe Yaylanması (Wobble): Darbe yönünün tersine doğru gövdeyi sars
            Vector3 impactDir = -hitNormal;
            impactDir.y = 0f;
            if (impactDir.sqrMagnitude > 0.01f)
            {
                Vector3 tiltAxis = Vector3.Cross(Vector3.up, impactDir.normalized);
                float angle = (amount / 20f) * wobbleForce;
                angle = Mathf.Clamp(angle, 5f, 25f);
                _targetWobbleRotation = Quaternion.AngleAxis(angle, tiltAxis) * _originalRotation;
            }

            // 3. Ağır hasarda veya kritikte mikro Hitstop tetikle
            if (HitstopManager.Instance != null)
            {
                if (isCritical || amount >= 50f)
                {
                    HitstopManager.Instance.TriggerHitstop(0.06f, 0.02f);
                }
                else if (amount >= 20f)
                {
                    HitstopManager.Instance.TriggerHitstop(0.03f, 0.05f);
                }
            }

        }

        private void Update()
        {
            // Yaylanmayı sönümle ve dik konuma geri getir
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetWobbleRotation, Time.deltaTime * 25f);
            _targetWobbleRotation = Quaternion.Slerp(_targetWobbleRotation, _originalRotation, Time.deltaTime * wobbleRecoverySpeed);

            // Flash zamanlayıcısı
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    ClearFlashColor();
                }
            }

            // Otomatik can yenilenmesi
            _timeSinceLastHit += Time.deltaTime;
            if (_timeSinceLastHit >= autoHealDelay && _currentHealth < maxHealth)
            {
                _currentHealth = Mathf.MoveTowards(_currentHealth, maxHealth, maxHealth * 0.5f * Time.deltaTime);
            }
        }

        private void ApplyFlashColor(Color color)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, color);
                _propBlock.SetColor(ColorId, color);
                if (_supportsEmission != null && i < _supportsEmission.Length && _supportsEmission[i]) _propBlock.SetColor(EmissionColorId, color);
                _renderers[i].SetPropertyBlock(_propBlock);
            }
        }

        private void ClearFlashColor()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].SetPropertyBlock(null);
            }
        }
    }

    /// <summary>
    /// Geriye dönük uyumluluk için Hitbox türevi.
    /// </summary>
    public class DummyHitbox : Hitbox
    {
    }
}
