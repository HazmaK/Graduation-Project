using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.UI;

namespace RetroFPS.Weapons
{
    /// <summary>Double-barrel blast and enemy-only meat hook.</summary>
    public class GothicShotgunWeapon : WeaponBase
    {
        [Header("Double Blast")]
        [SerializeField] private float damagePerPellet = 18f;
        [SerializeField] private int pelletCount = 18;
        [SerializeField] private float fireRate = 0.95f;
        [SerializeField] private float range = 42f;
        [SerializeField] private float spreadAngle = 5.8f;
        [SerializeField] private float airborneKnockback = 6.5f;

        [Header("Meat Hook")]
        [SerializeField] private float hookRange = 32f;
        [SerializeField] private float hookPullSpeed = 29f;
        [SerializeField] private float hookCooldown = 0.85f;
        [SerializeField] private Transform hookFirePoint;

        private float _primaryCooldownTimer;
        private float _altCooldownTimer;
        private LineRenderer _chainLine;
        private Material _chainMaterial;
        private PlayerMovement _movement;
        private CrosshairHUD _crosshairHUD;

        private const int BlastTargetCapacity = 12;
        private readonly IDamageable[] _blastTargets = new IDamageable[BlastTargetCapacity];
        private readonly float[] _blastDamage = new float[BlastTargetCapacity];
        private readonly float[] _blastDisplayDamage = new float[BlastTargetCapacity];
        private readonly bool[] _blastCrit = new bool[BlastTargetCapacity];
        private readonly Vector3[] _blastPoints = new Vector3[BlastTargetCapacity];
        private readonly Vector3[] _blastNormals = new Vector3[BlastTargetCapacity];
        private readonly RaycastHit[] _rayHits = new RaycastHit[32];
        private readonly RaycastHit[] _hookHitsBuffer = new RaycastHit[16];
        private int _blastTargetCount;
        private int _wallImpactCount;
        private bool _blastHit;
        private bool _blastAnyCrit;

        public override WeaponSlot Slot => WeaponSlot.GothicShotgun;
        public override bool IsFullAuto => false;
        public override AmmoType RequiredAmmoType => AmmoType.Shells;
        public override int PrimaryAmmoCost => 2;
        public override int AltAmmoCost => 0;
        public override bool CanFirePrimary => _primaryCooldownTimer <= 0f;
        public override bool CanFireAlt => _altCooldownTimer <= 0f;
        /// <summary>Remaining hook cooldown fraction; zero is ready.</summary>
        public float HookCooldownNormalized => hookCooldown <= 0f ? 0f : Mathf.Clamp01(_altCooldownTimer / hookCooldown);

        private void Awake()
        {
            if (string.IsNullOrEmpty(weaponDisplayName) || weaponDisplayName == "Weapon")
                weaponDisplayName = "Gothic Super Shotgun";
            SetupChainRenderer();
        }

        public override void Initialize(PlayerLook look, Camera playerCamera)
        {
            base.Initialize(look, playerCamera);
            ResolveMovement();
        }

        protected override void Start()
        {
            base.Start();
            ResolveMovement();
            _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
            SetupChainRenderer();
        }

        private void ResolveMovement()
        {
            if (_movement == null)
                _movement = GetComponentInParent<PlayerMovement>() ?? FindAnyObjectByType<PlayerMovement>();
        }

        private void SetupChainRenderer()
        {
            if (_chainLine != null) return;
            GameObject chainObj = new GameObject("Gothic_MeatHook_Chain");
            _chainLine = chainObj.AddComponent<LineRenderer>();
            _chainLine.useWorldSpace = true;
            _chainLine.startWidth = 0.045f;
            _chainLine.endWidth = 0.045f;
            _chainLine.positionCount = 2;
            _chainLine.enabled = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _chainMaterial = new Material(shader);
            Color color = new Color(0.22f, 0.22f, 0.24f, 1f);
            if (_chainMaterial.HasProperty("_BaseColor")) _chainMaterial.SetColor("_BaseColor", color);
            if (_chainMaterial.HasProperty("_Color")) _chainMaterial.color = color;
            _chainLine.sharedMaterial = _chainMaterial;
        }

        public override void Tick(float deltaTime)
        {
            if (_primaryCooldownTimer > 0f) _primaryCooldownTimer -= deltaTime;
            if (_altCooldownTimer > 0f) _altCooldownTimer -= deltaTime;
            if (_chainLine == null) return;
            bool grappling = _movement != null && _movement.IsGrappling;
            _chainLine.enabled = grappling;
            if (grappling)
            {
                _chainLine.SetPosition(0, hookFirePoint != null ? hookFirePoint.position : FirePoint.position);
                _chainLine.SetPosition(1, _movement.GrappleTargetPoint);
            }
        }

        public override void FirePrimary()
        {
            Camera cam = PlayerCamera;
            if (cam == null || !CanFirePrimary) return;
            if (!TryConsumeAmmo(PrimaryAmmoCost))
            {
                _primaryCooldownTimer = 0.25f;
                if (CombatAudio.Instance != null) CombatAudio.Instance.PlayDryFire();
                return;
            }

            _primaryCooldownTimer = fireRate;
            Vector3 startPos = FirePoint.position;
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            Vector3 camUp = cam.transform.up;
            _blastTargetCount = 0;
            _wallImpactCount = 0;
            _blastHit = false;
            _blastAnyCrit = false;

            // Resolve all pellet rays before applying accumulated damage.
            for (int i = 0; i < pelletCount; i++)
            {
                Vector2 circle = Random.insideUnitCircle * (spreadAngle * Mathf.Deg2Rad);
                Vector3 direction = (camForward + camRight * circle.x + camUp * circle.y).normalized;
                Vector3 endPos = startPos + direction * range;
                Vector3 origin = cam.transform.position + direction * 0.35f;
                if (CombatRaycast.Nearest(origin, direction, range, GameLayers.PlayerAttackMask, _rayHits, out RaycastHit hit)
                    && !hit.collider.transform.IsChildOf(transform.root))
                {
                    endPos = hit.point;
                    AccumulatePellet(hit, damagePerPellet);
                }
                if (i % 3 == 0 && CombatVFXManager.Instance != null)
                    CombatVFXManager.Instance.SpawnBulletTracer(startPos, endPos, new Color(1f, 0.65f, 0.15f));
            }
            for (int i = 0; i < _blastTargetCount; i++)
            {
                ApplyBlastTarget(_blastTargets[i], _blastDamage[i], _blastDisplayDamage[i],
                    _blastPoints[i], _blastNormals[i], _blastCrit[i]);
                _blastTargets[i] = null;
            }
            if (_blastHit) NotifyHit(_blastAnyCrit);

            // AddImpulse kanca sırasında yok sayılır: önce kancayı bırak, sonra geri tepmeyi uygula.
            if (_movement != null && _movement.IsGrappling) _movement.StopGrapple(true);
            if (_movement != null && !_movement.IsGrounded)
                _movement.AddImpulse(-camForward * airborneKnockback);
            if (Viewmodel != null) Viewmodel.AddRecoil(0.24f, 16f, 4f);
            if (_look != null)
            {
                _look.AddTrauma(0.35f);
                _look.AddViewPunch(4.2f, Random.Range(-1.1f, 1.1f));
            }
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(24f);
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.TriggerMuzzleFlash(startPos, new Color(1f, 0.5f, 0.08f), 6.5f);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayShotgunFire();
        }

        private void AccumulatePellet(RaycastHit hit, float damage)
        {
            CombatTargetLookup.Binding binding = CombatTargetLookup.Get(hit.collider);
            IDamageable target = binding.Damageable;
            if (target == null)
            {
                if (_wallImpactCount < 6 && CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, false, false);
                    _wallImpactCount++;
                }
                return;
            }
            bool crit = binding.Hitbox != null && binding.Hitbox.IsCriticalZone;
            float displayDamage = binding.Hitbox != null ? damage * binding.Hitbox.DamageMultiplier : damage;
            _blastHit = true;
            _blastAnyCrit |= crit;
            for (int i = 0; i < _blastTargetCount; i++)
            {
                if (!ReferenceEquals(_blastTargets[i], target)) continue;
                _blastDamage[i] += damage;
                _blastDisplayDamage[i] += displayDamage;
                _blastCrit[i] |= crit;
                return;
            }
            if (_blastTargetCount == BlastTargetCapacity)
            {
                ApplyBlastTarget(target, damage, displayDamage, hit.point, hit.normal, crit);
                return;
            }
            int index = _blastTargetCount++;
            _blastTargets[index] = target;
            _blastDamage[index] = damage;
            _blastDisplayDamage[index] = displayDamage;
            _blastCrit[index] = crit;
            _blastPoints[index] = hit.point;
            _blastNormals[index] = hit.normal;
        }

        private static void ApplyBlastTarget(IDamageable target, float damage, float displayDamage,
            Vector3 point, Vector3 normal, bool crit)
        {
            // Hitbox multiplies the raw sum exactly once.
            target.TakeDamage(damage, point, normal, crit);
            if (DamageNumberSystem.Instance != null)
                DamageNumberSystem.Instance.SpawnDamageNumber(point, displayDamage, crit);
        }

        public override void FireAlt()
        {
            Camera cam = PlayerCamera;
            if (cam == null || _movement == null || !CanFireAlt) return;
            if (_movement.IsGrappling)
            {
                _movement.StopGrapple(true);
                _altCooldownTimer = 0.35f;
                return;
            }
            _altCooldownTimer = hookCooldown;
            if (_look != null) _look.AddViewPunch(0.8f, 0f);
            if (TryFindEnemyHookTarget(cam, out RaycastHit hit, out IDamageable damageable,
                out IStunnable stunnable, out Transform enemyTransform))
            {
                if (stunnable != null) stunnable.Stun(1.5f, Vector3.zero);
                if (damageable != null)
                {
                    damageable.TakeDamage(10f, hit.point, hit.normal, false);
                    NotifyHit(false);
                    if (DamageNumberSystem.Instance != null)
                        DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, 10f, false);
                }
                _movement.StartGrapple(hit.point, hookPullSpeed, enemyTransform);
                if (CombatAudio.Instance != null) CombatAudio.Instance.PlayGrappleHookLatch(hit.point);
                if (Viewmodel != null) Viewmodel.AddRecoil(0.08f, 6f, 1.5f);
                if (_look != null) _look.AddTrauma(0.12f);
            }
            else if (CombatAudio.Instance != null) CombatAudio.Instance.PlayGrappleLaunch();
        }

        private bool TryFindEnemyHookTarget(Camera cam, out RaycastHit bestHit,
            out IDamageable damageable, out IStunnable stunnable, out Transform enemyTransform)
        {
            bestHit = default;
            damageable = null;
            stunnable = null;
            enemyTransform = null;
            Vector3 origin = cam.transform.position + cam.transform.forward * 0.35f;
            Vector3 direction = cam.transform.forward;
            float maxDistance = hookRange;
            if (CombatRaycast.Nearest(origin, direction, hookRange, GameLayers.PlayerAttackMask, _rayHits, out RaycastHit directHit))
            {
                if (TryValidateEnemy(directHit.collider, out damageable, out stunnable, out enemyTransform))
                {
                    bestHit = directHit;
                    return true;
                }
                maxDistance = directHit.distance;
            }
            int count = Physics.SphereCastNonAlloc(origin, 0.60f, direction, _hookHitsBuffer,
                maxDistance, GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = _hookHitsBuffer[i].collider;
                if (col == null || _hookHitsBuffer[i].distance >= closest) continue;
                if (!TryValidateEnemy(col, out IDamageable candidate, out IStunnable stun, out Transform target)) continue;
                Vector3 toTarget = col.bounds.center - origin;
                if (CombatRaycast.Nearest(origin, toTarget.normalized, toTarget.magnitude,
                    GameLayers.PlayerAttackMask, _rayHits, out RaycastHit wallHit)
                    && wallHit.collider != col && !wallHit.collider.transform.IsChildOf(target)) continue;
                closest = _hookHitsBuffer[i].distance;
                bestHit = _hookHitsBuffer[i];
                if (bestHit.point == Vector3.zero) bestHit.point = col.bounds.center;
                damageable = candidate;
                stunnable = stun;
                enemyTransform = target;
                found = true;
            }
            return found;
        }

        private bool TryValidateEnemy(Collider col, out IDamageable damageable,
            out IStunnable stunnable, out Transform enemyTransform)
        {
            damageable = null;
            stunnable = null;
            enemyTransform = null;
            if (col == null || col.transform.IsChildOf(transform.root)) return false;
            CombatTargetLookup.Binding binding = CombatTargetLookup.Get(col);
            damageable = binding.Entity;
            if (damageable is PlayerHealth) return false;
            stunnable = binding.Stunnable;
            IEncounterEnemy enemy = binding.Enemy;
            if (enemy != null && !enemy.IsAlive) return false;
            if (damageable == null && stunnable == null && enemy == null) return false;
            enemyTransform = (enemy as Component)?.transform ?? (stunnable as Component)?.transform
                ?? (damageable as Component)?.transform ?? col.transform.root;
            return true;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            if (_chainLine != null) _chainLine.enabled = false;
            if (_movement != null && _movement.IsGrappling) _movement.StopGrapple(false);
        }

        private void OnDestroy()
        {
            if (_chainLine != null) Destroy(_chainLine.gameObject);
            if (_chainMaterial != null) Destroy(_chainMaterial);
        }
    }
}
