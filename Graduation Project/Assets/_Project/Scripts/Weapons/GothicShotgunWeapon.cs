using System.Collections.Generic;
using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.UI;

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Slot 3: Gotik Çift Namlulu Pompalı & Grapple Hook (Super Shotgun & Meat Hook).
    /// - Birincil Atış (Sol Tık): 18 saçmalı yıkıcı çift namlu darbesi (2 Shell tüketir, ağır geri tepme ve hava tepmesi).
    /// - İkincil Atış (Sağ Tık / Alt-Fire): Namlu altından fırlayan et kancası (Meat Hook / Grapple Hook).
    ///   Düşmanlara veya yüzeylere kilitlenerek zincirle oyuncuyu hedefe hızla çeker; düşmanı sersemletir (IStunnable).
    ///   Çekilirken Space (Zıplama) tuşuyla kanca serbest bırakılarak havaya sapan gibi fırlanabilir (Slingshot).
    /// </summary>
    public class GothicShotgunWeapon : WeaponBase
    {
        [Header("Çift Namlulu Ağır Saçma (Double Blast)")]
        [SerializeField] private float damagePerPellet = 18f;
        [SerializeField] private int pelletCount = 18;
        [SerializeField] private float fireRate = 0.95f;
        [SerializeField] private float range = 42f;
        [SerializeField] private float spreadAngle = 5.8f;
        [SerializeField] private float airborneKnockback = 5.2f;

        [Header("Et Kancası / Grapple Meat Hook")]
        [SerializeField] private float hookRange = 32f;
        [SerializeField] private float hookPullSpeed = 29f;
        [SerializeField] private float hookCooldown = 0.85f;
        [SerializeField] private Transform hookFirePoint;

        private float _primaryCooldownTimer = 0f;
        private float _altCooldownTimer = 0f;

        // Zincir Görselleştirici (LineRenderer)
        private LineRenderer _chainLine;
        private Material _chainMaterial;
        private PlayerMovement _movement;
        private CrosshairHUD _crosshairHUD;

        // Çoklu saçma tekil hedef tamponu (aynı saçmada aynı düşmanın collider'larına doğru hasar)
        private readonly HashSet<IDamageable> _hitTargetBuffer = new HashSet<IDamageable>();
        private readonly RaycastHit[] _hookHitsBuffer = new RaycastHit[16];

        public override WeaponSlot Slot => WeaponSlot.GothicShotgun;
        public override bool IsFullAuto => false;
        public override AmmoType RequiredAmmoType => AmmoType.Shells;
        public override int PrimaryAmmoCost => 2; // Çift namlu patlaması
        public override int AltAmmoCost => 0; // Kanca mekanik araçtır, cephane yemez

        public override bool CanFirePrimary => _primaryCooldownTimer <= 0f;
        public override bool CanFireAlt => _altCooldownTimer <= 0f;

        private void Awake()
        {
            if (string.IsNullOrEmpty(weaponDisplayName) || weaponDisplayName == "Weapon")
            {
                weaponDisplayName = "Gothic Super Shotgun";
            }

            SetupChainRenderer();
        }

        protected override void Start()
        {
            base.Start();
            ResolveMovement();
            SetupChainRenderer();
        }

        private void ResolveMovement()
        {
            if (_movement != null) return;
            _movement = GetComponentInParent<PlayerMovement>() ?? FindAnyObjectByType<PlayerMovement>();
        }

        private void SetupChainRenderer()
        {
            if (_chainLine != null) return;

            GameObject chainObj = new GameObject("Gothic_MeatHook_Chain");
            chainObj.transform.SetParent(null);
            _chainLine = chainObj.AddComponent<LineRenderer>();
            _chainLine.useWorldSpace = true;
            _chainLine.startWidth = 0.045f;
            _chainLine.endWidth = 0.045f;
            _chainLine.positionCount = 2;
            _chainLine.enabled = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");

            _chainMaterial = new Material(shader);
            Color chainCol = new Color(0.22f, 0.22f, 0.24f, 1f); // Paslı gotik demir zincir
            if (_chainMaterial.HasProperty("_BaseColor")) _chainMaterial.SetColor("_BaseColor", chainCol);
            if (_chainMaterial.HasProperty("_Color")) _chainMaterial.color = chainCol;

            _chainLine.material = _chainMaterial;
        }

        public override void Tick(float deltaTime)
        {
            if (_primaryCooldownTimer > 0f) _primaryCooldownTimer -= deltaTime;
            if (_altCooldownTimer > 0f) _altCooldownTimer -= deltaTime;

            ResolveMovement();

            // Zincir güncellemesi: Oyuncu kanca ile çekiliyorsa namlu ucu ile hedef arasına zinciri ger
            if (_movement != null && _movement.IsGrappling)
            {
                if (_chainLine != null)
                {
                    Vector3 startPos = (hookFirePoint != null) ? hookFirePoint.position : FirePoint.position;
                    Vector3 targetPos = _movement.GrappleTargetPoint;

                    _chainLine.SetPosition(0, startPos);
                    _chainLine.SetPosition(1, targetPos);
                    _chainLine.enabled = true;
                }
            }
            else
            {
                if (_chainLine != null && _chainLine.enabled)
                {
                    _chainLine.enabled = false;
                }
            }
        }

        public override void FirePrimary()
        {
            Camera cam = PlayerCamera;
            if (cam == null) return;

            // Cephane Kontrolü: 2 fişek yoksa kuru tetik tıkı çal
            if (!HasAmmo(PrimaryAmmoCost))
            {
                _primaryCooldownTimer = 0.25f;
                if (CombatAudio.Instance != null) CombatAudio.Instance.PlayDryFire();
                return;
            }

            TryConsumeAmmo(PrimaryAmmoCost);
            _primaryCooldownTimer = fireRate;

            Vector3 startPos = FirePoint != null ? FirePoint.position : cam.transform.position + cam.transform.forward * 0.45f;
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            Vector3 camUp = cam.transform.up;

            _hitTargetBuffer.Clear();

            // 18 adet saçmanın konik yayılım hesaplaması
            for (int i = 0; i < pelletCount; i++)
            {
                Vector2 circle = Random.insideUnitCircle * (spreadAngle * 0.0175f);
                Vector3 pelletDir = (camForward + camRight * circle.x + camUp * circle.y).normalized;
                Vector3 endPos = startPos + pelletDir * range;
                Vector3 rayOrigin = cam.transform.position + pelletDir * 0.35f;

                if (Physics.Raycast(rayOrigin, pelletDir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(transform.root))
                    {
                        endPos = hit.point;
                        ApplyPelletDamage(hit, damagePerPellet);
                    }
                }

                // Saçma kıvılcım izleri
                if (CombatVFXManager.Instance != null && (i % 3 == 0))
                {
                    CombatVFXManager.Instance.SpawnBulletTracer(startPos, endPos, new Color(1.0f, 0.65f, 0.15f));
                }
            }

            // Havada ateş edildiyse retro geriye tepme (Airborne Recoil Boost)
            ResolveMovement();
            if (_movement != null && !_movement.IsGrounded)
            {
                CharacterController cc = _movement.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.Move(-camForward * (airborneKnockback * Time.deltaTime));
                }
            }

            // Tok Combat Geri Bildirimleri: Recoil, Trauma, Bloom, Muzzle Flash, Audio
            if (Viewmodel != null) Viewmodel.AddRecoil(0.24f, 16.0f, 4.0f);
            if (_look != null) _look.AddTrauma(0.35f);

            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(24f);

            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.TriggerMuzzleFlash(startPos, new Color(1.0f, 0.50f, 0.08f), 6.5f);
            }

            if (CombatAudio.Instance != null)
            {
                CombatAudio.Instance.PlayShotgunFire();
            }

            // Kanca takılıysa vuruş anında kancayı serbest bırakabilir (isteğe bağlı momentum koruması)
            if (_movement != null && _movement.IsGrappling)
            {
                _movement.StopGrapple(true);
            }
        }

        public override void FireAlt()
        {
            Camera cam = PlayerCamera;
            if (cam == null) return;

            ResolveMovement();
            if (_movement == null) return;

            // Zaten kanca çekiliyorsa sağ tıka tekrar basıldığında kancayı iptal et
            if (_movement.IsGrappling)
            {
                _movement.StopGrapple(true);
                _altCooldownTimer = 0.35f;
                return;
            }

            _altCooldownTimer = hookCooldown;

            // YALNIZCA düşman hedeflerine kanca atılabilir (harita, zemin ve duvarlar filtrelenir)
            if (TryFindEnemyHookTarget(cam, out RaycastHit bestHit, out IDamageable damageable, out IStunnable stunnable, out Transform enemyTransform))
            {
                // Düşmanı sersemlet ve yerine çivile
                if (stunnable != null)
                {
                    stunnable.Stun(1.5f, Vector3.zero);
                }

                if (damageable != null)
                {
                    damageable.TakeDamage(10f, bestHit.point, bestHit.normal, false);
                    NotifyHit(false);
                }

                // Vuruş darbesi ve kıvılcımı
                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnHitImpact(bestHit.point, bestHit.normal, false, true);
                }

                // Kancayı sapla ve çekilmeyi başlat!
                _movement.StartGrapple(bestHit.point, hookPullSpeed, enemyTransform);

                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayGrappleHookLatch(bestHit.point);
                }

                if (Viewmodel != null) Viewmodel.AddRecoil(0.08f, 6.0f, 1.5f);
                if (_look != null) _look.AddTrauma(0.12f);
            }
            else
            {
                // Düşman bulunamadı veya haritada başka bir yere nişan alındı - Kanca tutunmaz, sadece fırlatma ıslığı çalar
                if (CombatAudio.Instance != null)
                {
                    CombatAudio.Instance.PlayGrappleLaunch();
                }
            }
        }

        /// <summary>
        /// Nişangah doğrultusundaki tekil düşmanı tespit eder.
        /// Duvar, zemin veya harita geometrisine kanca atılmasını kesinlikle engeller.
        /// </summary>
        private bool TryFindEnemyHookTarget(
            Camera cam,
            out RaycastHit bestHit,
            out IDamageable targetDamageable,
            out IStunnable targetStunnable,
            out Transform targetTransform)
        {
            bestHit = default;
            targetDamageable = null;
            targetStunnable = null;
            targetTransform = null;

            Vector3 rayOrigin = cam.transform.position + cam.transform.forward * 0.35f;
            Vector3 rayDir = cam.transform.forward;

            // 1. Aşama: Doğrudan merkez ışın (Raycast) kontrolü
            float maxSearchDistance = hookRange;
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit directHit, hookRange, ~0, QueryTriggerInteraction.Ignore))
            {
                if (TryValidateEnemy(directHit.collider, out targetDamageable, out targetStunnable, out targetTransform))
                {
                    bestHit = directHit;
                    if (bestHit.point == Vector3.zero && directHit.collider != null)
                    {
                        bestHit.point = directHit.collider.bounds.center;
                    }
                    return true;
                }

                // Doğrudan ışın haritaya (duvar/zemin) çarptıysa, arama mesafesini bu engelle sınırla (duvar arkasındaki düşmanı çekemez)
                maxSearchDistance = directHit.distance;
            }

            // 2. Aşama: Nişangah hafif sapmaları için toleranslı küre taraması (SphereCastNonAlloc - Sıfır GC)
            int hitCount = Physics.SphereCastNonAlloc(rayOrigin, 0.60f, rayDir, _hookHitsBuffer, maxSearchDistance, ~0, QueryTriggerInteraction.Ignore);
            float closestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hookHitsBuffer[i].collider;
                if (col == null) continue;

                if (TryValidateEnemy(col, out var dmg, out var stun, out var enemyTf))
                {
                    // Duvar arkası görüş engeli kontrolü (Line of sight check)
                    Vector3 enemyTargetPos = col.bounds.center;
                    if (Physics.Linecast(rayOrigin, enemyTargetPos, out RaycastHit wallHit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (wallHit.collider != col && !wallHit.collider.transform.IsChildOf(enemyTf))
                        {
                            continue; // Araya duvar girmiş, görüş engeli var
                        }
                    }

                    float dist = _hookHitsBuffer[i].distance;
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestHit = _hookHitsBuffer[i];
                        if (bestHit.point == Vector3.zero)
                        {
                            bestHit.point = col.bounds.center;
                        }
                        targetDamageable = dmg;
                        targetStunnable = stun;
                        targetTransform = enemyTf;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Verilen collider'ın geçerli bir düşman olup olmadığını doğrular.
        /// Harita nesneleri, duvarlar, zemin ve oyuncunun kendisi false döner.
        /// </summary>
        private bool TryValidateEnemy(
            Collider col,
            out IDamageable damageable,
            out IStunnable stunnable,
            out Transform enemyTransform)
        {
            damageable = null;
            stunnable = null;
            enemyTransform = null;

            if (col == null || col.transform.IsChildOf(transform.root)) return false;

            // 1. Hitbox bileşeni (kafa, gövde, uzuvlar)
            Hitbox hitbox = col.GetComponentInParent<Hitbox>();
            if (hitbox != null)
            {
                damageable = hitbox.Target ?? hitbox.GetComponentInParent<IDamageable>();
            }
            else
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            // Oyuncunun kendi gövdesi asla düşman değildir
            if (damageable is PlayerHealth)
            {
                damageable = null;
                return false;
            }

            // 2. Düşman sözleşmeleri (IStunnable, IEncounterEnemy)
            stunnable = col.GetComponentInParent<IStunnable>();
            IEncounterEnemy encounterEnemy = col.GetComponentInParent<IEncounterEnemy>();

            bool isEnemy = damageable != null || stunnable != null || encounterEnemy != null;
            if (isEnemy)
            {
                enemyTransform = (encounterEnemy as Component)?.transform
                              ?? (stunnable as Component)?.transform
                              ?? (damageable as Component)?.transform
                              ?? col.transform.root;
                return true;
            }

            return false;
        }

        private void ApplyPelletDamage(RaycastHit hit, float damage)
        {
            Hitbox hitbox = hit.collider.GetComponentInParent<Hitbox>();
            if (hitbox != null)
            {
                bool isCrit = hitbox.IsCriticalZone;
                float finalDmg = damage * hitbox.DamageMultiplier;
                hitbox.TakeDamage(damage, hit.point, hit.normal, false);
                NotifyHit(isCrit);

                if (CombatVFXManager.Instance != null)
                    CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, isCrit, true);

                if (DamageNumberSystem.Instance != null)
                    DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, finalDmg, isCrit);

                if (CombatAudio.Instance != null)
                    CombatAudio.Instance.PlayFleshImpact(hit.point, isCrit);

                return;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal, false);
                NotifyHit(false);

                if (CombatVFXManager.Instance != null)
                    CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, false, true);

                if (DamageNumberSystem.Instance != null)
                    DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, damage, false);

                if (CombatAudio.Instance != null)
                    CombatAudio.Instance.PlayFleshImpact(hit.point, false);

                return;
            }

            // Çevre / Duvar Sekmesi (Sparks & Dust)
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, false, false);
            }
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            if (_chainLine != null)
            {
                _chainLine.enabled = false;
            }
            if (_movement != null && _movement.IsGrappling)
            {
                _movement.StopGrapple(false);
            }
        }

        private void OnDestroy()
        {
            if (_chainLine != null && _chainLine.gameObject != null)
            {
                Destroy(_chainLine.gameObject);
            }
        }
    }
}
