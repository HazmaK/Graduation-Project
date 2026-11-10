using System.Collections.Generic;
using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.UI;

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Slot 2: Cellat Kılıcı 2.0 (Executioner Blade Overhaul).
    /// - Birincil Saldırı (Sol Tık): 3 Aşamalı Ritmik Kombo Zinciri (Yatay Kesiş -> Ters Kesiş -> Ağır Ezici Finisher).
    /// - İkincil Saldırı (Sağ Tık): 
    ///     * Yerde/Düz: Cellat İnfaz Hücumu (Executioner's Lunge) — 8-10m ileri atılma, biçme, düşman öldüğünde veya sersemken vurulduğunda +25 HP can sömürüsü ve Adrenalin Hız Patlaması!
    ///     * Havada Aşağı Bakarken: Meteor Çakılması (Meteor Slam) — Yere sert iniş, 5.5m çapında şok dalgası, alan hasarı ve Ghoul'ları yere seren sersemletme (Stun).
    /// - Mermi Kesme (Blade Deflection): Savurma menziline giren düşman plazma mermilerini havada kesip geri yansıtır.
    /// Sıfır GC tahsisi, non-alloc fizik tamponları ve yüksek tokluklu kinetik geri bildirim (Juice).
    /// </summary>
    public class ExecutionerBladeWeapon : WeaponBase
    {
        [Header("3 Vuruşlu Kombo Sistemi (Combo Chain)")]
        [SerializeField] private float combo1Damage = 45f;
        [SerializeField] private float combo2Damage = 55f;
        [SerializeField] private float combo3Damage = 95f;
        [SerializeField] private float comboResetTime = 0.70f;
        [SerializeField] private float slashRange = 3.4f;
        [SerializeField] private float slashAngle = 85f;

        [Header("Sağ Tık: İnfaz Hücumu (Executioner's Lunge)")]
        [SerializeField] private float lungeDamage = 95f;
        [SerializeField] private float lungeSpeed = 28f;
        [SerializeField] private float lungeDuration = 0.22f;
        [SerializeField] private float lungeCooldown = 0.95f;
        [SerializeField] private float lungeHealAmount = 25f;
        [SerializeField] private float adrenalineDuration = 1.5f;

        [Header("Havada Sağ Tık: Meteor Çakılması (Meteor Slam)")]
        [SerializeField] private float slamDownwardSpeed = 34f;
        [SerializeField] private float slamDamage = 80f;
        [SerializeField] private float slamRadius = 5.5f;
        [SerializeField] private float slamStunDuration = 2.2f;
        [SerializeField] private float slamCooldown = 1.25f;

        [Header("Mermi Kesme (Projectile Slicing)")]
        [SerializeField] private bool enableProjectileSlice = true;
        [SerializeField] private float projectileSliceRadius = 2.8f;

        private float _primaryCooldownTimer = 0f;
        private float _altCooldownTimer = 0f;

        // Kombo durumu
        private int _comboIndex = 0;
        private float _lastSlashTime = -10f;

        // Sıfır GC önbellek tamponları
        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly RaycastHit[] _lungeHitsBuffer = new RaycastHit[16];
        private readonly List<IDamageable> _hitEntitiesBuffer = new List<IDamageable>(16);

        // Önbelleğe alınmış bileşenler
        private PlayerMovement _movement;
        private PlayerHealth _playerHealth;
        private CrosshairHUD _crosshairHUD;

        public override WeaponSlot Slot => WeaponSlot.ExecutionerBlade;
        public override bool IsFullAuto => false;
        public override bool CanFirePrimary => _primaryCooldownTimer <= 0f;
        public override bool CanFireAlt => _altCooldownTimer <= 0f;
        public int CurrentComboStep => _comboIndex + 1;

        private void Awake()
        {
            if (string.IsNullOrEmpty(weaponDisplayName) || weaponDisplayName == "Weapon")
            {
                weaponDisplayName = "Executioner Blade";
            }
        }

        protected override void Start()
        {
            base.Start();
            ResolveMovement();
            ResolvePlayerHealth();
        }

        private void ResolveMovement()
        {
            if (_movement != null) return;
            _movement = GetComponentInParent<PlayerMovement>()
                     ?? FindAnyObjectByType<PlayerMovement>();
        }

        private void ResolvePlayerHealth()
        {
            if (_playerHealth != null) return;
            _playerHealth = GetComponentInParent<PlayerHealth>()
                         ?? FindAnyObjectByType<PlayerHealth>();
        }

        public override void Tick(float deltaTime)
        {
            if (_primaryCooldownTimer > 0f) _primaryCooldownTimer -= deltaTime;
            if (_altCooldownTimer > 0f) _altCooldownTimer -= deltaTime;

            // Kombo zaman aşımı kontrolü (beklenirse başa sarar)
            if (_comboIndex > 0 && (Time.time - _lastSlashTime > comboResetTime))
            {
                _comboIndex = 0;
            }
        }

        #region Sol Tık (Birincil Kombo Saldırısı)
        public override void FirePrimary()
        {
            Camera cam = PlayerCamera;
            if (!CanFirePrimary || cam == null) return;

            // Kombo zaman aşımı kontrolü
            if (Time.time - _lastSlashTime > comboResetTime)
            {
                _comboIndex = 0;
            }

            int currentCombo = _comboIndex;
            _lastSlashTime = Time.time;

            float damage = combo1Damage;
            float cooldown = 0.20f;
            float trauma = 0.12f;
            float bloom = 10f;

            switch (currentCombo)
            {
                case 0:
                    damage = combo1Damage;
                    cooldown = 0.20f;
                    trauma = 0.12f;
                    bloom = 10f;
                    if (Viewmodel != null) Viewmodel.TriggerMeleeSlash(0.20f);
                    if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSwing(1.05f);
                    break;

                case 1:
                    damage = combo2Damage;
                    cooldown = 0.20f;
                    trauma = 0.14f;
                    bloom = 12f;
                    if (Viewmodel != null) Viewmodel.TriggerMeleeBackhand(0.20f);
                    if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSwing(0.95f);
                    break;

                case 2:
                    damage = combo3Damage;
                    cooldown = 0.38f; // Ağır bitirici toparlanması
                    trauma = 0.26f;
                    bloom = 18f;
                    if (Viewmodel != null) Viewmodel.TriggerMeleeOverhead(0.28f);
                    if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeFinisher();
                    break;
            }

            // Bir sonraki komboya ilerle
            _comboIndex = (_comboIndex + 1) % 3;
            _primaryCooldownTimer = cooldown;

            if (_look != null) _look.AddTrauma(trauma);
            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(bloom);

            // 1. Mermileri Kesme ve Geri Yansıtma
            if (enableProjectileSlice)
            {
                SliceNearbyProjectiles(cam.transform.position, cam.transform.forward, projectileSliceRadius);
            }

            // 2. Koni Alan Hasarı
            ExecuteSlashCone(cam, damage, currentCombo == 2);
        }

        private void SliceNearbyProjectiles(Vector3 origin, Vector3 forward, float radius)
        {
            var projectiles = EnemyProjectile.ActiveProjectiles;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (i >= projectiles.Count) continue;
                EnemyProjectile proj = projectiles[i];
                if (proj == null || proj.IsDeflected) continue;

                Vector3 toProj = proj.transform.position - origin;
                if (toProj.sqrMagnitude <= radius * radius)
                {
                    float angle = Vector3.Angle(forward, toProj.normalized);
                    if (angle <= 75f)
                    {
                        // Mermiyi oyuncunun baktığı yöne ölümcül hızla yansıt
                        proj.Deflect(forward, 2.0f, 3.0f);

                        if (CombatVFXManager.Instance != null)
                        {
                            CombatVFXManager.Instance.SpawnOccultEnergyBurst(proj.transform.position, true);
                        }

                        if (HitstopManager.Instance != null)
                        {
                            HitstopManager.Instance.TriggerHitstop(0.06f, 0.02f);
                        }

                        if (CombatAudio.Instance != null)
                        {
                            CombatAudio.Instance.PlayCritCrunch();
                        }
                    }
                }
            }
        }

        private void ExecuteSlashCone(Camera cam, float damage, bool isFinisher)
        {
            float range = isFinisher ? (slashRange * 1.15f) : slashRange;
            float angleLimit = isFinisher ? (slashAngle * 0.75f) : slashAngle;

            int hitCount = Physics.OverlapSphereNonAlloc(
                cam.transform.position,
                range,
                _overlapBuffer,
                GameLayers.PlayerAttackMask,
                QueryTriggerInteraction.Ignore
            );

            _hitEntitiesBuffer.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col.transform.IsChildOf(transform.root)) continue;

                Vector3 targetPos = col.bounds.center;
                Vector3 dirToTarget = (targetPos - cam.transform.position).normalized;
                float angle = Vector3.Angle(cam.transform.forward, dirToTarget);

                if (angle <= angleLimit * 0.5f)
                {
                    // Duvar arkasına vurulmasını engelle
                    if (Physics.Linecast(cam.transform.position, targetPos, out RaycastHit wallHit, GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore))
                    {
                        if (wallHit.collider != col && !wallHit.collider.transform.IsChildOf(col.transform.root))
                        {
                            continue;
                        }
                    }

                    Hitbox hitbox = col.GetComponent<Hitbox>();
                    IDamageable rootTarget = hitbox != null && hitbox.Target != null 
                        ? hitbox.Target 
                        : col.GetComponentInParent<IDamageable>();

                    if (rootTarget != null && !(rootTarget is PlayerHealth) && !_hitEntitiesBuffer.Contains(rootTarget))
                    {
                        _hitEntitiesBuffer.Add(rootTarget);

                        bool isCrit = isFinisher || (hitbox != null && hitbox.IsCriticalZone);
                        float finalDamage = isCrit ? damage * 1.35f : damage;

                        rootTarget.TakeDamage(finalDamage, targetPos, -dirToTarget, isCrit);
                        NotifyHit(isCrit);

                        // Combat Juice: Kan, hasar sayısı, tok kesme sesi
                        if (CombatVFXManager.Instance != null)
                        {
                            Vector3 slashDir = isFinisher ? Vector3.down : cam.transform.right;
                            CombatVFXManager.Instance.SpawnBladeSlashImpact(targetPos, slashDir, isCrit);
                            CombatVFXManager.Instance.SpawnHitImpact(targetPos, -dirToTarget, isCrit, true);
                        }

                        if (DamageNumberSystem.Instance != null)
                            DamageNumberSystem.Instance.SpawnDamageNumber(targetPos, finalDamage, isCrit);

                        if (CombatAudio.Instance != null)
                            CombatAudio.Instance.PlayFleshImpact(targetPos, isCrit);

                        // Finisher darbesi mikro hitstop ve ekstra kan saçar
                        if (isFinisher)
                        {
                            if (HitstopManager.Instance != null)
                            {
                                HitstopManager.Instance.TriggerHitstop(0.08f, 0.02f);
                            }

                            if (CombatVFXManager.Instance != null)
                            {
                                CombatVFXManager.Instance.SpawnFleshGibs(targetPos, cam.transform.forward, 2);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Sağ Tık (İnfaz Hücumu & Meteor Çakılması)
        public override void FireAlt()
        {
            Camera cam = PlayerCamera;
            if (!CanFireAlt || cam == null) return;

            ResolveMovement();
            if (_movement == null) return;

            bool isLookingDown = cam.transform.forward.y < -0.42f;
            bool isAirborne = !_movement.IsGrounded;

            // Havada aşağıya bakılıyorsa: Meteor Çakılması (Meteor Slam)
            if (isAirborne && isLookingDown)
            {
                ExecuteMeteorSlamAction(cam);
            }
            // Yerde veya düz/yukarı bakılıyorsa: Cellat İnfaz Hücumu (Executioner's Lunge)
            else
            {
                ExecuteLungeAction(cam);
            }
        }

        private void ExecuteLungeAction(Camera cam)
        {
            _altCooldownTimer = lungeCooldown;
            _primaryCooldownTimer = 0.35f;

            if (Viewmodel != null) Viewmodel.TriggerMeleeLunge(0.28f);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeLunge();
            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(24f);

            Vector3 lungeDir = cam.transform.forward;
            // Yerdeyken zemine takılmaması için dikey bileşeni düzelt
            if (_movement.IsGrounded && lungeDir.y < 0f)
            {
                lungeDir.y = 0f;
                lungeDir.Normalize();
            }

            // Hızlı 28 m/s atılma (~7 metre)
            _movement.StartLunge(lungeDir, lungeSpeed, lungeDuration, null);

            // Atılma güzergahındaki düşmanları biç
            Ray ray = new Ray(cam.transform.position, lungeDir);
            float sweepDist = 7.5f;

            int hitCount = Physics.SphereCastNonAlloc(ray, 0.75f, _lungeHitsBuffer, sweepDist, GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            _hitEntitiesBuffer.Clear();

            bool triggeredHarvest = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _lungeHitsBuffer[i];
                Collider col = hit.collider;
                if (col == null || col.transform.IsChildOf(transform.root)) continue;

                Hitbox hitbox = col.GetComponent<Hitbox>();
                IDamageable target = hitbox != null && hitbox.Target != null 
                    ? hitbox.Target 
                    : col.GetComponentInParent<IDamageable>();

                if (target != null && !(target is PlayerHealth) && !_hitEntitiesBuffer.Contains(target))
                {
                    _hitEntitiesBuffer.Add(target);

                    IStunnable stunnable = col.GetComponentInParent<IStunnable>();
                    bool isTargetStunned = stunnable != null;

                    float finalDamage = lungeDamage;
                    target.TakeDamage(finalDamage, hit.point, hit.normal, true);
                    NotifyHit(true);

                    if (CombatVFXManager.Instance != null)
                    {
                        CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, true, true);
                    }

                    if (DamageNumberSystem.Instance != null)
                    {
                        DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, finalDamage, true);
                    }

                    // İNFAZ SÖMÜRÜSÜ (Blood Harvest): İlk isabette oyuncuyu ödüllendir
                    if (!triggeredHarvest)
                    {
                        triggeredHarvest = true;
                        TriggerBloodHarvest(hit.point);
                    }
                }
            }
        }

        private void TriggerBloodHarvest(Vector3 impactPoint)
        {
            // 1. Oyuncuya +25 HP Can Sömürüsü (Vampiric Leech)
            ResolvePlayerHealth();
            if (_playerHealth != null)
            {
                _playerHealth.Heal(lungeHealAmount, true, 125f);
            }

            // 2. 1.5 saniyelik Adrenalin Hız Patlaması (%35)
            if (_movement != null)
            {
                _movement.ApplySpeedBoost(1.35f, adrenalineDuration);
            }

            // 3. Tok Hitstop & Ses
            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(0.08f, 0.02f);
            }

            if (CombatAudio.Instance != null)
            {
                CombatAudio.Instance.PlayCritCrunch();
                CombatAudio.Instance.PlayFleshImpact(impactPoint, true);
            }

            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnFleshGibs(impactPoint, transform.forward, 3);
            }
        }

        private void ExecuteMeteorSlamAction(Camera cam)
        {
            _altCooldownTimer = slamCooldown;
            _primaryCooldownTimer = 0.5f;

            if (Viewmodel != null) Viewmodel.TriggerMeleeSlam(0.35f);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeLunge();
            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(26f);

            _movement.StartMeteorSlam(slamDownwardSpeed, () => OnMeteorSlamLanded(cam));
        }

        private void OnMeteorSlamLanded(Camera cam)
        {
            Vector3 center = transform.position;

            // Ses & VFX
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSlam(center);
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnOccultEnergyBurst(center + Vector3.up * 0.2f, false);
                CombatVFXManager.Instance.SpawnFleshGibs(center, Vector3.up, 3);
            }

            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(0.10f, 0.02f);
            }

            // Çevredeki tüm düşmanları yere ser (Stun) ve 80 alan hasarı vur
            int hitCount = Physics.OverlapSphereNonAlloc(center, slamRadius, _overlapBuffer, GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            _hitEntitiesBuffer.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col.transform.IsChildOf(transform.root)) continue;

                Hitbox hitbox = col.GetComponent<Hitbox>();
                IDamageable target = hitbox != null && hitbox.Target != null 
                    ? hitbox.Target 
                    : col.GetComponentInParent<IDamageable>();

                if (target != null && !(target is PlayerHealth) && !_hitEntitiesBuffer.Contains(target))
                {
                    _hitEntitiesBuffer.Add(target);

                    Vector3 hitPos = col.bounds.center;
                    Vector3 knockDir = (hitPos - center).normalized;
                    knockDir.y = 0.35f;

                    // Düşmanı sersemlet ve savur
                    IStunnable stunnable = col.GetComponentInParent<IStunnable>();
                    if (stunnable != null)
                    {
                        stunnable.Stun(slamStunDuration, knockDir * 4.5f);
                    }

                    target.TakeDamage(slamDamage, hitPos, knockDir, true);
                    NotifyHit(true);

                    if (DamageNumberSystem.Instance != null)
                    {
                        DamageNumberSystem.Instance.SpawnDamageNumber(hitPos, slamDamage, true);
                    }

                    if (CombatVFXManager.Instance != null)
                    {
                        CombatVFXManager.Instance.SpawnHitImpact(hitPos, Vector3.up, true, true);
                    }
                }
            }
        }
        #endregion
    }
}
