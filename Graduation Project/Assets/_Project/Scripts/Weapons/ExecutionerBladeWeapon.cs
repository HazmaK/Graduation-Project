using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.Stats;
using RetroFPS.UI;

namespace RetroFPS.Weapons
{
    /// <summary>Slot 2 melee driver: buffered combo, projectile cutting, execution lunge and height-scaled slam.</summary>
    public class ExecutionerBladeWeapon : WeaponBase
    {
        [Header("Combo")]
        [SerializeField] private float lightSlashDamage = 34f;
        [SerializeField] private float backhandSlashDamage = 42f;
        [SerializeField] private float overheadBaseDamage = 71.11f;
        [SerializeField] private float comboResetTime = 0.70f;
        [SerializeField] private float slashRange = 3.4f;
        [SerializeField] private float slashAngle = 85f;
        [SerializeField] private float lightSlashCooldown = 0.22f;
        [SerializeField] private float backhandSlashCooldown = 0.22f;
        [SerializeField] private float overheadCooldown = 0.44f;
        [SerializeField] private float overheadWindup = 0.10f;
        [SerializeField] private float primaryBufferWindow = 0.15f;
        [SerializeField] private float criticalMultiplier = 1.35f;
        [SerializeField] private float finisherRangeMultiplier = 1.15f;
        [SerializeField] private float finisherAngleMultiplier = 0.75f;
        [SerializeField] private float finisherStunSeconds = 1f;
        [SerializeField] private float finisherKnockback = 12f;
        [SerializeField] private float stepInDistance = 1.6f;
        [SerializeField] private float stepInImpulse = 2.5f;
        [SerializeField] private float guardBounceRecovery = 0.30f;
        [SerializeField] private float guardBouncePunch = 2f;
        [SerializeField] private float sunderDuration = 4f;
        [SerializeField] private float sunderMultiplier = 1.25f;

        [Header("Speed Edge")]
        [SerializeField] private float speedEdgeMinSpeed = 8f;
        [SerializeField] private float speedEdgeMaxSpeed = 13f;
        [SerializeField] private float speedEdgeMaxBonus = 0.35f;
        [SerializeField] private float speedEdgePitchThreshold = 1.2f;
        [SerializeField] private float speedEdgePitchMultiplier = 1.08f;

        [Header("Combo feedback")]
        [SerializeField] private float lightHitstopDuration = 0.035f;
        [SerializeField] private float lightHitstopScale = 0.05f;
        [SerializeField] private float overheadHitstopDuration = 0.10f;
        [SerializeField] private float overheadHitstopScale = 0.02f;
        [SerializeField] private float lightPitchPunch = -1.1f;
        [SerializeField] private float overheadPitchPunch = -3f;
        [SerializeField] private float slashYawPunch = 1.2f;
        [SerializeField] private float lightTrauma = 0.14f;
        [SerializeField] private float backhandTrauma = 0.16f;
        [SerializeField] private float overheadTrauma = 0.32f;
        [SerializeField] private float whiffTraumaMultiplier = 0.5f;
        [SerializeField] private float lightSwingPitch = 1.05f;
        [SerializeField] private float backhandSwingPitch = 0.95f;
        [SerializeField] private float overheadAnimationDuration = 0.28f;
        [SerializeField] private float lightBloom = 10f;
        [SerializeField] private float backhandBloom = 12f;
        [SerializeField] private float overheadBloom = 18f;
        [SerializeField] private int finisherGibCount = 2;

        [Header("Counter-Verdict Quick Swipe")]
        [SerializeField] private float quickSwipeDamage = 25f;
        [SerializeField, Min(0f)] private float quickSwipeDuration = 0.18f;
        [SerializeField] private float quickSwipePitch = 0.95f;

        [Header("Executioner's Lunge")]
        [SerializeField] private float lungeDamage = 95f;
        [SerializeField] private float lungeSpeed = 28f;
        [SerializeField] private float lungeDuration = 0.22f;
        [SerializeField] private float executionLungeCooldown = 2.2f;
        [SerializeField] private float stunnedHarvestHeal = 20f;
        [SerializeField] private float adrenalineDuration = 1.5f;
        [SerializeField, Min(1)] private int harvestTargetCapacity = 512;
        [SerializeField] private float lungeSweepDistance = 7.5f;
        [SerializeField] private float lungeSweepRadius = 0.75f;
        [SerializeField] private float lungeDirectionMinSqrMagnitude = 0.01f;
        [SerializeField] private float lungePrimaryRecovery = 0.35f;
        [SerializeField] private float lungeAnimationDuration = 0.28f;
        [SerializeField] private float lungeBloom = 24f;
        [SerializeField] private float chainResetCooldown = 0.10f;
        [SerializeField] private float chainResetHitstopDuration = 0.06f;
        [SerializeField] private float chainResetHitstopScale = 0.03f;
        [SerializeField] private float chainResetFovKick = 6f;
        [SerializeField] private float harvestOverhealCap = 125f;
        [SerializeField] private float adrenalineMultiplier = 1.35f;
        [SerializeField] private float harvestHitstopDuration = 0.08f;
        [SerializeField] private float harvestHitstopScale = 0.02f;
        [SerializeField] private int harvestGibCount = 3;

        [Header("Meteor Slam")]
        [SerializeField] private float slamDownwardSpeed = 34f;
        [SerializeField] private float slamCooldown = 1.25f;
        [SerializeField] private float slamLandingCooldown = 1f;
        [SerializeField] private float slamMinHeight = 1f;
        [SerializeField] private float slamMaxHeight = 6f;
        /// <summary>Maximum height credit for a slam following a lunge in the same airtime; set very high to disable.</summary>
        [SerializeField, Min(0f)] private float lungeSlamMaxHeight = 4f;
        [SerializeField] private float slamLightDamage = 45f;
        [SerializeField] private float slamHeavyDamage = 120f;
        [SerializeField] private float slamLightRadius = 3f;
        [SerializeField] private float slamRadius = 5.5f;
        [SerializeField] private float slamLightStun = 0.5f;
        [SerializeField] private float slamHeavyStun = 1.8f;
        [SerializeField] private float slamGuardBreakHeight = 3.5f;
        [SerializeField] private float slamGuardBreakDuration = 2f;
        [SerializeField] private float slamSightHeight = 0.5f;
        [SerializeField] private float slamLightTrauma = 0.22f;
        [SerializeField] private float slamHeavyTrauma = 0.5f;
        [SerializeField] private float slamLightHitstop = 0.05f;
        [SerializeField] private float slamHeavyHitstop = 0.12f;
        [SerializeField] private float slamHitstopScale = 0.02f;
        [SerializeField] private float slamLookDownThreshold = -0.42f;
        [SerializeField] private float slamPrimaryRecovery = 0.5f;
        [SerializeField] private float slamAnimationDuration = 0.35f;
        [SerializeField] private float slamBloom = 26f;
        [SerializeField] private float slamPitchPunch = -3f;
        [SerializeField] private float slamBurstHeight = 0.2f;
        [SerializeField] private float slamKnockbackUp = 0.35f;
        [SerializeField] private float slamKnockbackSpeed = 4.5f;
        [SerializeField] private int slamGibCount = 3;

        [Header("Projectile cutting")]
        [SerializeField] private bool enableProjectileSlice = true;
        [SerializeField] private float projectileSliceRadius = 2.8f;
        [SerializeField] private float projectileSliceHalfAngle = 75f;
        [SerializeField] private float lungeSliceRadius = 1.8f;
        [SerializeField] private float lungeSliceHalfAngle = 90f;
        [SerializeField] private float projectileCutHitstopDuration = 0.04f;
        [SerializeField] private float projectileCutHitstopScale = 0.05f;

        private readonly Collider[] _overlapBuffer = new Collider[32];
        private readonly RaycastHit[] _lungeHitsBuffer = new RaycastHit[32];
        private readonly RaycastHit[] _visibilityHits = new RaycastHit[32];
        private readonly IDamageable[] _hitEntitiesBuffer = new IDamageable[32];
        private int _hitEntityCount;
        private BladeAltState _altState;
        private float _slamStartY;
        private bool _bladeLungeActive;
        private System.Action _slamLandedCallback;
        private PlayerMovement _movement;
        private PlayerHealth _playerHealth;
        private CrosshairHUD _crosshairHUD;
        private BloodHarvestTracker _harvestTracker;
        private BladeComboState _combo;

        /// <inheritdoc />
        public override WeaponSlot Slot => WeaponSlot.ExecutionerBlade;
        /// <inheritdoc />
        public override bool IsFullAuto => false;
        /// <summary>Allows fresh presses through the controller during the combo input-buffer window.</summary>
        public override bool CanFirePrimary => _combo != null && _combo.CanAcceptPress;
        /// <inheritdoc />
        public override bool CanFireAlt => _altState != null && _movement != null && PlayerCamera != null
            && _altState.CanFire(_movement.IsGrounded, PlayerCamera.transform.forward.y, slamLookDownThreshold);
        /// <summary>One-based next step, retained for HUD and controller consumers.</summary>
        public int CurrentComboStep => _combo != null ? _combo.NextStep : 1;
        private Vector3 AttackerPosition => _movement != null ? _movement.transform.position : transform.root.position;

        private void Awake()
        {
            InitializeState();
            if (string.IsNullOrEmpty(weaponDisplayName) || weaponDisplayName == "Weapon")
                weaponDisplayName = "Executioner Blade";
        }

        /// <inheritdoc />
        public override void Initialize(PlayerLook look, Camera playerCamera)
        {
            // Registered holstered visuals can still be inactive, so their Awake may not have run yet.
            InitializeState();
            base.Initialize(look, playerCamera);
            ResolvePlayer();
        }

        protected override void Start()
        {
            base.Start();
            ResolvePlayer();
        }

        private void InitializeState()
        {
            if (_altState == null) _altState = new BladeAltState();
            if (_combo == null)
                _combo = new BladeComboState(lightSlashCooldown, backhandSlashCooldown, overheadCooldown,
                    comboResetTime, primaryBufferWindow, overheadWindup);
            if (_harvestTracker == null) _harvestTracker = new BloodHarvestTracker(harvestTargetCapacity);
            if (_slamLandedCallback == null) _slamLandedCallback = OnMeteorSlamLanded;
        }

        private void ResolvePlayer()
        {
            if (_movement == null)
                _movement = GetComponentInParent<PlayerMovement>() ?? FindAnyObjectByType<PlayerMovement>();
            if (_playerHealth == null)
                _playerHealth = GetComponentInParent<PlayerHealth>() ?? FindAnyObjectByType<PlayerHealth>();
            if (_crosshairHUD == null) _crosshairHUD = FindAnyObjectByType<CrosshairHUD>();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            AdvanceCooldowns(deltaTime);
            Camera cam = PlayerCamera;
            if (cam == null) { _combo.CancelPending(); return; }
            if (_combo.TryResolveFinisher()) ResolveComboSwing(cam, 3);
            if (_combo.TryStartBuffered(out int step)) StartComboSwing(cam, step);
            if (_bladeLungeActive)
            {
                _bladeLungeActive = _movement != null && _movement.IsLunging;
                if (_bladeLungeActive && enableProjectileSlice)
                    SliceNearbyProjectiles(cam.transform.position, cam.transform.forward, lungeSliceRadius, lungeSliceHalfAngle);
            }
        }

        /// <summary>Advances recovery while holstered, cancelling delayed hits and input without attacks.</summary>
        public override void TickHolstered(float deltaTime)
        {
            _combo.CancelPending();
            AdvanceCooldowns(deltaTime);
        }

        private void AdvanceCooldowns(float deltaTime)
        {
            _combo.Advance(deltaTime);
            // A grounded lunge start is still "grounded" for its first frames; do not clear the airtime marker mid-lunge.
            _altState.Advance(deltaTime, _movement != null && _movement.IsGrounded && !_movement.IsLunging);
        }

        /// <inheritdoc />
        public override void OnUnequip()
        {
            CancelPendingActions();
            base.OnUnequip();
        }

        private void OnDisable()
        {
            CancelPendingActions();
        }

        /// <summary>Cancels buffered swings, finisher wind-up and lunge projectile cutting.</summary>
        public override void CancelPendingActions()
        {
            _combo?.CancelPending();
            _bladeLungeActive = false;
        }

        /// <summary>Immediate 25-damage backhand; does not consume combo input, cooldown or Speed Edge.</summary>
        public void QuickSwipe()
        {
            if (Pause.GamePause.IsGameplayInputBlocked) return;
            if (Viewmodel != null) Viewmodel.TriggerMeleeBackhand(quickSwipeDuration);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSwing(quickSwipePitch);
            Camera cam = PlayerCamera;
            if (cam != null) ExecuteSlashCone(cam, quickSwipeDamage, 0);
        }

        /// <inheritdoc />
        public override void FirePrimary()
        {
            Camera cam = PlayerCamera;
            if (Pause.GamePause.IsGameplayInputBlocked || cam == null) return;
            if (_combo.Press(out int step)) StartComboSwing(cam, step);
        }

        private float SpeedEdgeMultiplier()
            => BladeSpeedEdge.Multiplier(_movement != null ? _movement.CurrentHorizontalSpeed : 0f,
                speedEdgeMinSpeed, speedEdgeMaxSpeed, speedEdgeMaxBonus);

        private void StartComboSwing(Camera cam, int step)
        {
            if (Viewmodel != null)
            {
                if (step == 1) Viewmodel.TriggerMeleeSlash(lightSlashCooldown);
                else if (step == 2) Viewmodel.TriggerMeleeBackhand(backhandSlashCooldown);
                else Viewmodel.TriggerMeleeOverhead(overheadAnimationDuration);
            }
            if (CombatAudio.Instance != null)
            {
                if (step == 3) CombatAudio.Instance.PlayBladeFinisher();
                else PlaySwingSound(step, SpeedEdgeMultiplier());
            }
            if (_look != null)
                _look.AddViewPunch(step == 3 ? overheadPitchPunch : lightPitchPunch,
                    step == 3 ? 0f : step == 1 ? -slashYawPunch : slashYawPunch);
            if (_crosshairHUD != null)
                _crosshairHUD.TriggerRecoilBloom(step == 1 ? lightBloom : step == 2 ? backhandBloom : overheadBloom);
            if (step != 3) ResolveComboSwing(cam, step);
        }

        private void PlaySwingSound(int step, float multiplier)
        {
            float pitch = step == 2 ? backhandSwingPitch : lightSwingPitch;
            if (multiplier >= speedEdgePitchThreshold) pitch *= speedEdgePitchMultiplier;
            CombatAudio.Instance.PlayBladeSwing(pitch);
        }

        private void ResolveComboSwing(Camera cam, int step)
        {
            float speedMultiplier = SpeedEdgeMultiplier();
            if (step == 3 && speedMultiplier >= speedEdgePitchThreshold && CombatAudio.Instance != null)
                PlaySwingSound(step, speedMultiplier);
            if (enableProjectileSlice)
                SliceNearbyProjectiles(cam.transform.position, cam.transform.forward, projectileSliceRadius, projectileSliceHalfAngle);
            float damage = step == 1 ? lightSlashDamage : step == 2 ? backhandSlashDamage : overheadBaseDamage;
            ExecuteSlashCone(cam, damage * speedMultiplier, step);
        }

        private void SliceNearbyProjectiles(Vector3 origin, Vector3 forward, float radius, float halfAngle)
        {
            var projectiles = EnemyProjectile.ActiveProjectiles;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (i >= projectiles.Count) continue;
                EnemyProjectile projectile = projectiles[i];
                if (projectile == null || projectile.IsDeflected) continue;
                Vector3 point = projectile.transform.position;
                Vector3 toProjectile = point - origin;
                if (toProjectile.sqrMagnitude > radius * radius || Vector3.Angle(forward, toProjectile) > halfAngle) continue;
                projectile.Cut();
                if (CombatVFXManager.Instance != null) CombatVFXManager.Instance.SpawnOccultEnergyBurst(point, true);
                if (HitstopManager.Instance != null)
                    HitstopManager.Instance.TriggerHitstop(projectileCutHitstopDuration, projectileCutHitstopScale);
                if (CombatAudio.Instance != null) CombatAudio.Instance.PlayCritCrunch();
            }
        }

        private void ExecuteSlashCone(Camera cam, float damage, int comboStep)
        {
            bool finisher = comboStep == 3;
            damage *= DamageMultiplier();
            float range = finisher ? slashRange * finisherRangeMultiplier : slashRange;
            float angle = (finisher ? slashAngle * finisherAngleMultiplier : slashAngle) * 0.5f;
            int count = Physics.OverlapSphereNonAlloc(cam.transform.position, range, _overlapBuffer,
                GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            BeginTargets();
            int damaged = 0;
            bool blocked = false;
            bool damagedEnemy = false;
            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestEnemy = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (IsSelf(col)) continue;
                Vector3 point = col.bounds.center;
                Vector3 toTarget = point - cam.transform.position;
                if (Vector3.Angle(cam.transform.forward, toTarget) > angle || !HasWorldVisibility(cam.transform.position, point)) continue;
                CombatTargetLookup.Binding binding = CombatTargetLookup.Get(col);
                IDamageable target = ResolveDamageOwner(binding.Entity);
                if (!AcceptTarget(target, binding.Enemy)) continue;
                bool crit = finisher;
                float finalDamage = crit ? damage * criticalMultiplier : damage;
                if (!DealBladeDamage(target, point, finalDamage, crit)) { blocked = true; continue; }
                damaged++;
                IEncounterEnemy enemy = target as IEncounterEnemy ?? binding.Enemy;
                if (enemy != null)
                {
                    damagedEnemy = true;
                    Vector3 enemyPosition = enemy.EnemyTransform != null ? enemy.EnemyTransform.position : point;
                    float distance = (enemyPosition - AttackerPosition).sqrMagnitude;
                    if (distance < nearestDistance) { nearestDistance = distance; nearestEnemy = enemyPosition; }
                }
                if (finisher && (enemy == null || enemy.IsAlive))
                {
                    Transform anchor = target is Component owner && owner != null ? owner.transform
                        : enemy != null ? enemy.EnemyTransform : col.transform;
                    SunderMarks.Apply(target, anchor, sunderDuration, sunderMultiplier);
                }
                if (CombatVFXManager.Instance != null)
                {
                    CombatVFXManager.Instance.SpawnBladeSlashImpact(point, finisher ? Vector3.down : cam.transform.right, crit);
                    if (finisher) CombatVFXManager.Instance.SpawnFleshGibs(point, cam.transform.forward, finisherGibCount);
                }
                if (finisher && target is IStunnable stunnable)
                    stunnable.Stun(finisherStunSeconds, cam.transform.forward * finisherKnockback);
            }
            if (comboStep == 0) return;
            if (damagedEnemy && HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(finisher ? overheadHitstopDuration : lightHitstopDuration,
                    finisher ? overheadHitstopScale : lightHitstopScale);
            if (_look != null)
            {
                float trauma = comboStep == 1 ? lightTrauma : comboStep == 2 ? backhandTrauma : overheadTrauma;
                _look.AddTrauma(damaged > 0 ? trauma : trauma * whiffTraumaMultiplier);
            }
            if (damaged == 0 && blocked)
            {
                _combo.GuardBounce(guardBounceRecovery);
                if (_look != null) _look.AddViewPunch(guardBouncePunch, 0f);
            }
            if (!finisher && damagedEnemy && _movement != null && _movement.IsGrounded
                && stepInImpulse > 0f && nearestDistance > stepInDistance * stepInDistance)
            {
                Vector3 direction = nearestEnemy - AttackerPosition;
                direction.y = 0f;
                _movement.AddImpulse(direction.normalized * stepInImpulse);
            }
        }

        /// <inheritdoc />
        public override void FireAlt()
        {
            Camera cam = PlayerCamera;
            if (Pause.GamePause.IsGameplayInputBlocked || !CanFireAlt || cam == null || _movement == null) return;
            if (BladeAltState.WantsSlam(_movement.IsGrounded, cam.transform.forward.y, slamLookDownThreshold)) ExecuteMeteorSlamAction();
            else ExecuteLungeAction(cam);
        }

        private void ExecuteLungeAction(Camera cam)
        {
            _altState.StartLunge(executionLungeCooldown);
            _combo.DelayPrimary(lungePrimaryRecovery);
            if (Viewmodel != null) Viewmodel.TriggerMeleeLunge(lungeAnimationDuration);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeLunge();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(lungeBloom);
            Vector3 direction = cam.transform.forward;
            if (_movement.IsGrounded && direction.y < 0f)
            {
                direction.y = 0f;
                if (direction.sqrMagnitude <= lungeDirectionMinSqrMagnitude)
                {
                    direction = _movement.transform.forward;
                    direction.y = 0f;
                }
                direction.Normalize();
            }
            _movement.StartLunge(direction, lungeSpeed, lungeDuration, null, false);
            _bladeLungeActive = true;
            if (enableProjectileSlice)
                SliceNearbyProjectiles(cam.transform.position, cam.transform.forward, lungeSliceRadius, lungeSliceHalfAngle);

            Vector3 origin = cam.transform.position;
            // Initial sphere overlaps (including the floor) must not set the wall limit.
            int wallCount = Physics.RaycastNonAlloc(origin, direction, _visibilityHits, lungeSweepDistance,
                GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            if (wallCount == _visibilityHits.Length) return;
            float clipDistance = lungeSweepDistance;
            for (int i = 0; i < wallCount; i++)
                if (IsWorldGeometry(_visibilityHits[i].collider))
                    clipDistance = Mathf.Min(clipDistance, _visibilityHits[i].distance);

            int count = Physics.SphereCastNonAlloc(origin, lungeSweepRadius, direction,
                _lungeHitsBuffer, lungeSweepDistance, GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            // NonAlloc queries are unordered; resolve enemies from nearest to farthest.
            SortLungeHits(count);
            BeginTargets();
            bool harvested = false;
            bool killed = false;
            // A saturated result set is incomplete, so fail closed.
            if (count == _lungeHitsBuffer.Length) return;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _lungeHitsBuffer[i];
                Collider col = hit.collider;
                if (IsSelf(col) || IsWorldGeometry(col) || hit.distance > clipDistance + lungeSweepRadius) continue;
                CombatTargetLookup.Binding binding = CombatTargetLookup.Get(col);
                IDamageable target = ResolveDamageOwner(binding.Entity);
                if (!AcceptTarget(target, binding.Enemy)) continue;
                IEncounterEnemy enemy = target as IEncounterEnemy ?? binding.Enemy;
                IStunState stun = target as IStunState;
                bool wasAlive = enemy != null && enemy.IsAlive;
                bool wasStunned = stun != null && stun.IsStunned;
                uint sequence = stun != null ? stun.StunSequence : 0;
                float damage = lungeDamage * DamageMultiplier();
                Vector3 point = hit.point == Vector3.zero ? col.ClosestPoint(origin) : hit.point;
                if (point == Vector3.zero) point = col.bounds.center;
                if (!DealBladeDamage(target, point, damage, true)) continue;
                if (wasAlive && !enemy.IsAlive) killed = true;
                if (!harvested && _harvestTracker.TryHarvest(target, wasAlive,
                    enemy != null && enemy.IsAlive, wasStunned, sequence))
                {
                    harvested = true;
                    TriggerBloodHarvest(point);
                }
            }
            if (killed)
            {
                _altState.ResetLungeCooldown(chainResetCooldown);
                if (HitstopManager.Instance != null)
                    HitstopManager.Instance.TriggerHitstop(chainResetHitstopDuration, chainResetHitstopScale);
                if (_look != null) _look.DoFovKick(_movement.LungeFov + chainResetFovKick);
            }
        }

        private void SortLungeHits(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = _lungeHitsBuffer[i];
                int j = i - 1;
                while (j >= 0 && _lungeHitsBuffer[j].distance > hit.distance)
                {
                    _lungeHitsBuffer[j + 1] = _lungeHitsBuffer[j];
                    j--;
                }
                _lungeHitsBuffer[j + 1] = hit;
            }
        }

        private void TriggerBloodHarvest(Vector3 point)
        {
            if (_playerHealth != null)
                _playerHealth.Heal(PlayerStats.Get(StatId.HarvestHeal, stunnedHarvestHeal), true, harvestOverhealCap);
            if (_movement != null) _movement.ApplySpeedBoost(adrenalineMultiplier, adrenalineDuration);
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(harvestHitstopDuration, harvestHitstopScale);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayCritCrunch();
            if (CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnFleshGibs(point, transform.forward, harvestGibCount);
        }

        private void ExecuteMeteorSlamAction()
        {
            _altState.StartSlam(slamCooldown, _bladeLungeActive && _movement.IsLunging);
            _combo.DelayPrimary(slamPrimaryRecovery);
            _bladeLungeActive = false;
            _slamStartY = _movement.transform.position.y;
            if (Viewmodel != null) Viewmodel.TriggerMeleeSlam(slamAnimationDuration);
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeLunge();
            if (_crosshairHUD != null) _crosshairHUD.TriggerRecoilBloom(slamBloom);
            _movement.StartMeteorSlam(slamDownwardSpeed, _slamLandedCallback);
        }

        private void OnMeteorSlamLanded()
        {
            // Slam is committed: landing still resolves while this blade is holstered/inactive, without viewmodel calls.
            if (this == null || _movement == null) return;
            _altState.SetSlamCooldown(slamLandingCooldown);
            Vector3 center = AttackerPosition;
            var impact = new SlamImpactScaling(_slamStartY, center.y, slamMinHeight, slamMaxHeight,
                slamLightDamage, slamHeavyDamage, slamLightRadius, slamRadius, slamLightStun, slamHeavyStun,
                _altState.SlamHeightLimit(lungeSlamMaxHeight));
            if (_look != null)
            {
                _look.AddViewPunch(slamPitchPunch, 0f);
                _look.AddTrauma(Mathf.Lerp(slamLightTrauma, slamHeavyTrauma, impact.T));
            }
            if (CombatAudio.Instance != null) CombatAudio.Instance.PlayBladeSlam(center);
            if (CombatVFXManager.Instance != null)
            {
                CombatVFXManager.Instance.SpawnOccultEnergyBurst(center + Vector3.up * slamBurstHeight, false);
            }
            if (HitstopManager.Instance != null)
                HitstopManager.Instance.TriggerHitstop(Mathf.Lerp(slamLightHitstop, slamHeavyHitstop, impact.T), slamHitstopScale);
            int count = Physics.OverlapSphereNonAlloc(center, impact.Radius, _overlapBuffer,
                GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            BeginTargets();
            bool hitEnemy = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (IsSelf(col)) continue;
                Vector3 point = col.bounds.center;
                if (!HasWorldVisibility(center + Vector3.up * slamSightHeight, point)) continue;
                CombatTargetLookup.Binding binding = CombatTargetLookup.Get(col);
                IDamageable target = ResolveDamageOwner(binding.Entity);
                if (!AcceptTarget(target, binding.Enemy)) continue;
                if (impact.Height >= slamGuardBreakHeight && target is IGuardBreakable guard)
                    guard.BreakGuard(slamGuardBreakDuration);
                // Evaluate guard before stun: a low slam must not open the guard before checking it.
                if (!DealBladeDamage(target, point, impact.Damage * DamageMultiplier(), true)) continue;
                if ((target as IEncounterEnemy ?? binding.Enemy) != null) hitEnemy = true;
                Vector3 knockback = (point - center).normalized;
                knockback.y = slamKnockbackUp;
                if (target is IStunnable stunnable) stunnable.Stun(impact.StunDuration, knockback * slamKnockbackSpeed);
            }
            // Gore (blood pool + bone gibs) only when the slam actually lands on an enemy, never on empty ground.
            if (hitEnemy && CombatVFXManager.Instance != null)
                CombatVFXManager.Instance.SpawnFleshGibs(center, Vector3.up, slamGibCount);
        }

        private static float DamageMultiplier()
            => PlayerStats.Get(StatId.DamageDealt, 1f) * PlayerStats.Get(StatId.MeleeDamage, 1f);

        private bool DealBladeDamage(IDamageable target, Vector3 point, float damage, bool crit)
        {
            bool blocked = target is IMeleeGuard guard && guard.BlocksMeleeFrom(AttackerPosition);
            // Match IMeleeGuard's attacker direction rather than a spherecast surface normal.
            Vector3 targetPosition = target is Component component && component != null ? component.transform.position : point;
            Vector3 normal = (AttackerPosition - targetPosition).normalized;
            MeleeDamageScope.Begin();
            try { target.TakeDamage(damage, point, normal, crit); }
            finally { MeleeDamageScope.End(); }
            if (blocked)
            {
                if (CombatVFXManager.Instance != null)
                    CombatVFXManager.Instance.SpawnHitImpact(point, normal, false, isFlesh: false);
                return false;
            }
            NotifyHit(crit);
            if (DamageNumberSystem.Instance != null)
                DamageNumberSystem.Instance.SpawnDamageNumber(point, damage, crit);
            return true;
        }

        private bool IsSelf(Collider col)
            => col == null || col.transform.IsChildOf(transform.root);

        private void BeginTargets()
        {
            for (int i = 0; i < _hitEntityCount; i++) _hitEntitiesBuffer[i] = null;
            _hitEntityCount = 0;
        }

        private static IDamageable ResolveDamageOwner(IDamageable target)
            => target is IDamageProxy proxy ? proxy.DamageOwner : target;

        private bool AcceptTarget(IDamageable target, IEncounterEnemy boundEnemy)
        {
            if (target == null || target is PlayerHealth || (target is Object obj && obj == null)) return false;
            IEncounterEnemy enemy = target as IEncounterEnemy ?? boundEnemy;
            if (enemy != null && !enemy.IsAlive) return false;
            for (int i = 0; i < _hitEntityCount; i++)
                if (ReferenceEquals(_hitEntitiesBuffer[i], target)) return false;
            if (_hitEntityCount == _hitEntitiesBuffer.Length) return false;
            _hitEntitiesBuffer[_hitEntityCount++] = target;
            return true;
        }

        private bool IsWorldGeometry(Collider col)
        {
            if (IsSelf(col) || col.gameObject.layer == GameLayers.Projectile) return false;
            CombatTargetLookup.Binding binding = CombatTargetLookup.Get(col);
            return binding.Entity == null && binding.Enemy == null;
        }

        private bool HasWorldVisibility(Vector3 origin, Vector3 point)
        {
            Vector3 direction = point - origin;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon) return true;
            int count = Physics.RaycastNonAlloc(origin, direction / distance, _visibilityHits, distance,
                GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
            if (count == _visibilityHits.Length) return false;
            for (int i = 0; i < count; i++)
                if (IsWorldGeometry(_visibilityHits[i].collider)) return false;
            return true;
        }
    }
}
