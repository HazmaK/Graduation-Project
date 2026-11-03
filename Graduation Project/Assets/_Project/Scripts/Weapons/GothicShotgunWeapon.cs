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

            Vector3 rayOrigin = cam.transform.position + cam.transform.forward * 0.35f;
            Vector3 rayDir = cam.transform.forward;

            if (Physics.SphereCast(rayOrigin, 0.35f, rayDir, out RaycastHit hit, hookRange, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(transform.root))
                {
                    // Düşmana mı çarptı?
                    IStunnable stunnable = hit.collider.GetComponentInParent<IStunnable>();
                    if (stunnable != null)
                    {
                        stunnable.Stun(1.5f, Vector3.zero); // Düşmanı yerine çivile / sersemlet
                    }

                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(10f, hit.point, hit.normal, false);
                        NotifyHit(false);
                    }

                    // Kancayı sapla ve çekilmeyi başlat!
                    _movement.StartGrapple(hit.point, hookPullSpeed, hit.transform);

                    if (CombatAudio.Instance != null)
                    {
                        CombatAudio.Instance.PlayGrappleHookLatch(hit.point);
                    }

                    if (Viewmodel != null) Viewmodel.AddRecoil(0.08f, 6.0f, 1.5f);
                    if (_look != null) _look.AddTrauma(0.12f);

                    return;
                }
            }

            // Iska geçildiyse kanca boşluğa atılma sesi
            if (CombatAudio.Instance != null)
            {
                CombatAudio.Instance.PlayGrappleLaunch();
            }
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
