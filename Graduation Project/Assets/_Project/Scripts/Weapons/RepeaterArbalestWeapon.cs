using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.UI;

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Slot 1: Hızlı Menzilli Arbalest / Nailgun.
    /// Birincil Atış: Namludan çıkan parlayan arbalet okları (3D Bolt & Tracer) fırlatır (Full-Auto).
    /// İkincil Atış (Alt-Fire): Yatay yay şeklinde 5 oku aynı anda fırlatır (Shotgun Spread).
    /// </summary>
    public class RepeaterArbalestWeapon : WeaponBase
    {
        [Header("Birincil Seri Atış Ayarları")]
        [SerializeField] private float primaryDamage = 22f;
        [SerializeField] private float primaryFireRate = 0.12f;
        [SerializeField] private float primaryRange = 60f;
        [SerializeField] private float primarySpread = 0.8f;

        [Header("İkincil Yatay Yay (Shotgun) Ayarları")]
        [SerializeField] private float altDamagePerPellet = 25f;
        [SerializeField] private int altPelletCount = 5;
        [SerializeField] private float altCooldown = 0.65f;

        [Header("Görsel Ok (Bolt) Efekti")]
        [SerializeField] private bool spawnVisualArrow = true;
        [SerializeField] private float boltFlightSpeed = 110f; // Görsel okun uçuş hızı

        private float _primaryCooldownTimer = 0f;
        private float _altCooldownTimer = 0f;

        // Parlayan Ok İzi (Line Tracer)
        private LineRenderer _tracerLine;
        private float _tracerTimer = 0f;

        [Header("Sustained Fire Bloom")]
        [SerializeField] private float maxBloomSpread = 2.2f;
        [SerializeField] private float bloomPerShot = 0.22f;
        [SerializeField] private float bloomRecovery = 4.5f;

        private float _bloom;
        private readonly VisualArrowFlyer[] _arrowPool = new VisualArrowFlyer[16];
        private readonly RaycastHit[] _rayHits = new RaycastHit[32];
        private int _nextArrow;
        private Transform _arrowRoot;
        private Material _boltMaterial;
        private CrosshairHUD _crosshairHUD;

        public override WeaponSlot Slot => WeaponSlot.RepeaterArbalest;
        public override bool IsFullAuto => true;
        public override AmmoType RequiredAmmoType => AmmoType.Bolts;
        public override int PrimaryAmmoCost => 1;
        public override int AltAmmoCost => 5;
        public override bool CanFirePrimary => _primaryCooldownTimer <= 0f;
        public override bool CanFireAlt => _altCooldownTimer <= 0f;

        private void Awake()
        {
            if (string.IsNullOrEmpty(weaponDisplayName) || weaponDisplayName == "Weapon")
            {
                weaponDisplayName = "Repeater Arbalest";
            }
            SetupBoltMaterial();
            SetupTracer();
        }

        protected override void Start()
        {
            base.Start();
            SetupBoltMaterial();
            SetupTracer();
            SetupArrowPool();
            _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
        }

        public override void Initialize(PlayerLook look, Camera playerCamera)
        {
            base.Initialize(look, playerCamera);
            SetupBoltMaterial();
            SetupArrowPool();
        }

        private void SetupBoltMaterial()
        {
            if (_boltMaterial != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _boltMaterial = new Material(shader);
            Color boltGlow = new Color(0.1f, 0.95f, 1f, 1f); // Parlayan enerji oku (Cyan/Gold)
            if (_boltMaterial.HasProperty("_BaseColor")) _boltMaterial.SetColor("_BaseColor", boltGlow);
            if (_boltMaterial.HasProperty("_Color")) _boltMaterial.color = boltGlow;
        }

        private void SetupTracer()
        {
            if (_tracerLine != null) return;

            GameObject tracerObj = new GameObject("Tracer_Arbalest_Bolt");
            tracerObj.transform.SetParent(null);
            _tracerLine = tracerObj.AddComponent<LineRenderer>();
            _tracerLine.useWorldSpace = true;
            _tracerLine.startWidth = 0.05f;
            _tracerLine.endWidth = 0.015f;
            _tracerLine.positionCount = 2;
            _tracerLine.enabled = false;

            if (_boltMaterial == null) SetupBoltMaterial();
            _tracerLine.material = _boltMaterial;
        }

        public override void Tick(float deltaTime)
        {
            // Recover after the firing interval; continuous fire must accumulate bloom.
            if (_primaryCooldownTimer <= 0f)
                _bloom = Mathf.MoveTowards(_bloom, 0f, bloomRecovery * deltaTime);
            if (_primaryCooldownTimer > 0f) _primaryCooldownTimer -= deltaTime;
            if (_altCooldownTimer > 0f) _altCooldownTimer -= deltaTime;

            if (_tracerTimer > 0f)
            {
                _tracerTimer -= deltaTime;
                if (_tracerTimer <= 0f && _tracerLine != null)
                {
                    _tracerLine.enabled = false;
                }
            }
        }

        public override void FirePrimary()
        {
            Camera cam = PlayerCamera;
            if (cam == null || !CanFirePrimary) return;

            // Cephane kontrolü: Ok yoksa kuru tetik tıkı çal
            if (!HasAmmo(PrimaryAmmoCost))
            {
                _primaryCooldownTimer = 0.22f;
                if (CombatAudio.Instance != null) CombatAudio.Instance.PlayDryFire();
                return;
            }

            TryConsumeAmmo(PrimaryAmmoCost);
            _primaryCooldownTimer = primaryFireRate;

            Vector3 shootDir = cam.transform.forward;
            Vector2 spread = Random.insideUnitCircle * ((primarySpread + _bloom) * 0.015f);
            shootDir += cam.transform.right * spread.x + cam.transform.up * spread.y;
            shootDir.Normalize();
            _bloom = Mathf.Min(maxBloomSpread, _bloom + bloomPerShot);

            Vector3 startPos = FirePoint != null ? FirePoint.position : cam.transform.position + cam.transform.forward * 0.4f;
            Vector3 endPos = startPos + shootDir * primaryRange;

            // Oyuncunun kendi gövdesini (CharacterController) atlamak için ışını hafifçe önden başlat
            Vector3 rayOrigin = cam.transform.position + shootDir * 0.35f;

            if (CombatRaycast.Nearest(rayOrigin, shootDir, primaryRange, GameLayers.PlayerAttackMask, _rayHits, out RaycastHit hit))
            {
                // Kendi parçalarımıza çarpmayı engelle
                if (!hit.collider.transform.IsChildOf(transform.root))
                {
                    endPos = hit.point;
                    ApplyHitDamage(hit, primaryDamage);
                }
            }

            // 1. Parlayan anlık ışık izi
            ShowTracer(startPos, endPos);

            // 2. Namludan fırlayan görsel ok (3D Visual Arrow)
            if (spawnVisualArrow)
            {
                SpawnFlyingArrow(startPos, endPos, shootDir);
            }

            // 3. Tepme ve kamera sarsıntısı
            if (Viewmodel != null) Viewmodel.AddRecoil(0.06f, 4.0f, 1.2f);
            if (_look != null)
            {
                _look.AddTrauma(0.06f);
                _look.AddViewPunch(0.55f, Random.Range(-0.25f, 0.25f));
            }

            // 4. Combat Juice: Ses, Namlu Flaş Işığı ve Nişangah Geri Tepmesi
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayArbalestFire();
            if (CombatVFXManager.Instance != null) CombatVFXManager.Instance.TriggerMuzzleFlash(startPos, new Color(0.2f, 0.95f, 1f));
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(7f);

        }

        public override void FireAlt()
        {
            Camera cam = PlayerCamera;
            if (cam == null) return;

            // Cephane kontrolü: 5 ok yoksa kuru tetik tıkı çal
            if (!HasAmmo(AltAmmoCost))
            {
                _altCooldownTimer = 0.30f;
                if (CombatAudio.Instance != null) CombatAudio.Instance.PlayDryFire();
                return;
            }

            TryConsumeAmmo(AltAmmoCost);
            _altCooldownTimer = altCooldown;
            _primaryCooldownTimer = 0.35f;

            Vector3 startPos = FirePoint != null ? FirePoint.position : cam.transform.position + cam.transform.forward * 0.4f;

            for (int i = 0; i < altPelletCount; i++)
            {
                float spreadAngle = (i - 2) * 2.8f;
                Vector3 shootDir = Quaternion.AngleAxis(spreadAngle, cam.transform.up) * cam.transform.forward;
                Vector3 endPos = startPos + shootDir * (primaryRange * 0.8f);
                Vector3 rayOrigin = cam.transform.position + shootDir * 0.35f;

                if (CombatRaycast.Nearest(rayOrigin, shootDir, primaryRange * 0.8f, GameLayers.PlayerAttackMask, _rayHits, out RaycastHit hit))
                {
                    if (!hit.collider.transform.IsChildOf(transform.root))
                    {
                        endPos = hit.point;
                        ApplyHitDamage(hit, altDamagePerPellet);
                    }
                }

                if (spawnVisualArrow)
                {
                    SpawnFlyingArrow(startPos, endPos, shootDir);
                }
            }

            if (Viewmodel != null) Viewmodel.AddRecoil(0.14f, 9.0f, 2.5f);
            if (_look != null)
            {
                _look.AddTrauma(0.22f);
                _look.AddViewPunch(2.4f, Random.Range(-0.6f, 0.6f));
            }

            // 4. Combat Juice: Ses, Alt-Fire Flaş Işığı ve Geniş Nişangah Açılması
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayArbalestAlt();
            if (CombatVFXManager.Instance != null) CombatVFXManager.Instance.TriggerMuzzleFlash(startPos, new Color(1f, 0.85f, 0.25f), 4.5f);
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(18f);
        }

        private void SetupArrowPool()
        {
            if (_arrowRoot != null) return;
            _arrowRoot = new GameObject("Arbalest_Arrow_Pool").transform;
            for (int i = 0; i < _arrowPool.Length; i++)
            {
                GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arrow.name = "Arbalest_Arrow_Bolt";
                arrow.transform.SetParent(_arrowRoot);
                arrow.transform.localScale = new Vector3(0.04f, 0.04f, 0.45f);
                arrow.GetComponent<Renderer>().sharedMaterial = _boltMaterial;
                Collider collider = arrow.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                _arrowPool[i] = arrow.AddComponent<VisualArrowFlyer>();
                arrow.SetActive(false);
            }
        }

        private void SpawnFlyingArrow(Vector3 start, Vector3 target, Vector3 dir)
        {
            VisualArrowFlyer flyer = _arrowPool[_nextArrow];
            _nextArrow = (_nextArrow + 1) % _arrowPool.Length;
            if (flyer == null) return;
            flyer.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir));
            flyer.Initialize(target, boltFlightSpeed);
            flyer.gameObject.SetActive(true);
        }

        private void ApplyHitDamage(RaycastHit hit, float damage)
        {
            CombatTargetLookup.Binding binding = CombatTargetLookup.Get(hit.collider);
            Hitbox hitbox = binding.Hitbox;
            if (hitbox != null)
            {
                bool isCrit = hitbox.IsCriticalZone;
                float finalDmg = damage * hitbox.DamageMultiplier;
                hitbox.TakeDamage(damage, hit.point, hit.normal, false);
                NotifyHit(isCrit);

                if (DamageNumberSystem.Instance != null)
                    DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, finalDmg, isCrit);

                return;
            }

            IDamageable damageable = binding.Damageable;
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal, false);
                NotifyHit(false);

                if (DamageNumberSystem.Instance != null)
                    DamageNumberSystem.Instance.SpawnDamageNumber(hit.point, damage, false);

                return;
            }

            // Çevre / Duvar Sekmesi (Sparks & Dust)
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnHitImpact(hit.point, hit.normal, false, false);
            }
        }

        private void ShowTracer(Vector3 start, Vector3 end)
        {
            if (_tracerLine == null) return;

            _tracerLine.SetPosition(0, start);
            _tracerLine.SetPosition(1, end);
            _tracerLine.enabled = true;
            _tracerTimer = 0.08f;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            if (_tracerLine != null)
            {
                _tracerLine.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_arrowRoot != null) Destroy(_arrowRoot.gameObject);
            if (_boltMaterial != null) Destroy(_boltMaterial);
            if (_tracerLine != null && _tracerLine.gameObject != null)
            {
                Destroy(_tracerLine.gameObject);
            }
        }
    }

    /// <summary>
    /// Namludan hedefe doğru yüksek hızla uçan görsel arbalet oku nesnesi.
    /// </summary>
    public class VisualArrowFlyer : MonoBehaviour
    {
        private Vector3 _targetPoint;
        private float _speed = 110f;
        private float _maxLifetime = 0.8f;
        private float _aliveTime = 0f;

        public void Initialize(Vector3 target, float speed)
        {
            _targetPoint = target;
            _speed = speed;
            _aliveTime = 0f;
        }

        private void Update()
        {
            _aliveTime += Time.deltaTime;
            if (_aliveTime >= _maxLifetime)
            {
                gameObject.SetActive(false);
                return;
            }

            float step = _speed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, _targetPoint, step);

            if (Vector3.Distance(transform.position, _targetPoint) <= 0.15f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
