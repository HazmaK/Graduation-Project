using System.Collections.Generic;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Slot 2: Ağır İnfaz Kılıcı / Cleaver.
    /// Birincil Atış: Önündeki 75 derecelik koniyi biçen kavisli yakın dövüş darbesi (Slash).
    /// İkincil Atış (Alt-Fire): İleri uzanan yüksek hasarlı saplama darbesi (Thrust Impale).
    /// Sıfır GC tahsisi için Physics.OverlapSphereNonAlloc ve yeniden kullanılan tamponlar kullanır.
    /// </summary>
    public class ExecutionerBladeWeapon : WeaponBase
    {
        [Header("Birincil Biçme (Slash) Ayarları")]
        [SerializeField] private float meleeSlashDamage = 75f;
        [SerializeField] private float meleeSlashRange = 3.2f;
        [SerializeField] private float meleeSlashAngle = 75f;
        [SerializeField] private float meleeSlashRate = 0.35f;

        [Header("İkincil Saplama (Thrust Impale) Ayarları")]
        [SerializeField] private float meleeThrustDamage = 130f;
        [SerializeField] private float meleeThrustRange = 4.0f;
        [SerializeField] private float meleeThrustCooldown = 1.1f;

        private float _primaryCooldownTimer = 0f;
        private float _altCooldownTimer = 0f;

        // Sıfır GC önbellek tamponları
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly List<IDamageable> _hitEntitiesBuffer = new List<IDamageable>(16);
        private RetroFPS.UI.CrosshairHUD _crosshairHUD;

        public override WeaponSlot Slot => WeaponSlot.ExecutionerBlade;
        public override bool IsFullAuto => false;
        public override bool CanFirePrimary => _primaryCooldownTimer <= 0f;
        public override bool CanFireAlt => _altCooldownTimer <= 0f;

        private void Awake()
        {
            if (string.IsNullOrEmpty(weaponDisplayName) || weaponDisplayName == "Weapon")
            {
                weaponDisplayName = "Executioner Blade";
            }
        }

        public override void Tick(float deltaTime)
        {
            if (_primaryCooldownTimer > 0f) _primaryCooldownTimer -= deltaTime;
            if (_altCooldownTimer > 0f) _altCooldownTimer -= deltaTime;
        }

        public override void FirePrimary()
        {
            Camera cam = PlayerCamera;
            if (!CanFirePrimary || cam == null) return;

            _primaryCooldownTimer = meleeSlashRate;

            if (Viewmodel != null)
            {
                Viewmodel.TriggerMeleeSlash(0.22f);
            }

            if (_look != null)
            {
                _look.AddTrauma(0.12f);
            }

            // Combat Juice: Kılıç savurma sesi ve nişangah açılması
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSwing();
            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<RetroFPS.UI.CrosshairHUD>();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(12f);

            // Sıfır GC: OverlapSphereNonAlloc ile ön koni alan hasarı
            int hitCount = Physics.OverlapSphereNonAlloc(
                cam.transform.position,
                meleeSlashRange,
                _overlapBuffer,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            _hitEntitiesBuffer.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col.transform.IsChildOf(transform.root)) continue;

                Vector3 dirToTarget = (col.bounds.center - cam.transform.position).normalized;
                float angle = Vector3.Angle(cam.transform.forward, dirToTarget);

                if (angle <= meleeSlashAngle * 0.5f)
                {
                    IDamageable target = col.GetComponent<Hitbox>();
                    if (target == null) target = col.GetComponentInParent<IDamageable>();

                    if (target != null && !_hitEntitiesBuffer.Contains(target))
                    {
                        _hitEntitiesBuffer.Add(target);
                        Vector3 hitPos = col.bounds.center;
                        target.TakeDamage(meleeSlashDamage, hitPos, -dirToTarget, false);
                        NotifyHit(false);

                        // Combat Juice: Kan, hasar sayısı, tok kesme sesi
                        if (CombatVFXManager.Instance != null)
                            CombatVFXManager.Instance.SpawnHitImpact(hitPos, -dirToTarget, false, true);

                        if (DamageNumberSystem.Instance != null)
                            DamageNumberSystem.Instance.SpawnDamageNumber(hitPos, meleeSlashDamage, false);

                        if (CombatAudio.Instance != null)
                            CombatAudio.Instance.PlayFleshImpact(hitPos, false);

                        if (_look != null)
                        {
                            _look.AddTrauma(0.18f);
                        }
                    }
                }
            }
        }

        public override void FireAlt()
        {
            Camera cam = PlayerCamera;
            if (!CanFireAlt || cam == null) return;

            _altCooldownTimer = meleeThrustCooldown;
            _primaryCooldownTimer = 0.50f;

            if (Viewmodel != null)
            {
                Viewmodel.AddRecoil(0.20f, 15f, 0f);
            }

            if (_look != null)
            {
                _look.AddTrauma(0.35f);
            }

            // Combat Juice: Saplama sesi ve nişangah açılması
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeThrust();
            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<RetroFPS.UI.CrosshairHUD>();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(22f);

            // Güçlü ileri saplama darbesi (Thrust Impale)
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.SphereCast(ray, 0.45f, out RaycastHit hit, meleeThrustRange, ~0, QueryTriggerInteraction.Ignore))
            {
                Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null)
                {
                    float finalDmg = meleeThrustDamage * hitbox.DamageMultiplier;
                    hitbox.TakeDamage(meleeThrustDamage, hit.point, hit.normal, true);
                    NotifyHit(true);

                    // Combat Juice: Kan, altın kıvılcım, kritik hasar sayısı, et sesi
                    if (CombatVFXManager.Instance != null)
                        CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, true, true);

                    if (DamageNumberSystem.Instance != null)
                        DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, finalDmg, true);

                    if (CombatAudio.Instance != null)
                        CombatAudio.Instance.PlayFleshImpact(hit.point, true);

                    if (_look != null) _look.AddTrauma(0.25f);
                    return;
                }

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(meleeThrustDamage, hit.point, hit.normal, true);
                    NotifyHit(true);

                    if (CombatVFXManager.Instance != null)
                        CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, true, true);

                    if (DamageNumberSystem.Instance != null)
                        DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, meleeThrustDamage, true);

                    if (CombatAudio.Instance != null)
                        CombatAudio.Instance.PlayFleshImpact(hit.point, true);

                    if (_look != null) _look.AddTrauma(0.25f);
                    return;
                }

                // Çevreye saplanma
                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, false, false);
                }
            }
        }
    }
}
