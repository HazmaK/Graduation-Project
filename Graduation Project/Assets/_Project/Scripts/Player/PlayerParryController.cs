using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RetroFPS.Combat;
using RetroFPS.Weapons;

namespace RetroFPS
{
    /// <summary>Counter-Verdict quick blade strike, melee stun and aim-directed projectile parry.</summary>
    [RequireComponent(typeof(PlayerLook))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class PlayerParryController : MonoBehaviour
    {
        [Header("Counter-Verdict")]
        [SerializeField] private bool enableCounter = true;
        [SerializeField, Min(0f)] private float counterActiveWindow = 0.18f;
        [SerializeField, Min(0f)] private float counterWhiffRecovery = 0.35f;
        [SerializeField] private float counterAngle = 110f;
        [SerializeField] private float projectileCatchRadius = 4.2f;

        [Header("Stun and Reflection")]
        [SerializeField] private float meleeStunDuration = 2.2f;
        [SerializeField] private float meleeFallbackRadius = 4f;
        [SerializeField] private float reflectedSpeedMultiplier = 1.7f;
        [SerializeField] private float reflectedDamageMultiplier = 2.5f;
        [SerializeField] private AudioClip customParrySound;

        private PlayerLook _look;
        private Camera _cam;
        private WeaponController _weapons;
        private CounterVerdictTimer _timer;
        private bool _wasGameplayBlocked;
        private readonly Collider[] _overlapResults = new Collider[16];

        /// <summary>Raised when a new Counter-Verdict strike starts.</summary>
        public event Action OnParryTriggered;
        /// <summary>Raised for every successful parry; true for projectiles, false for melee.</summary>
        public event Action<bool> OnParrySuccess;

        /// <summary>Whether Counter-Verdict is enabled in gameplay configuration.</summary>
        public bool CounterEnabled => enableCounter;
        /// <summary>Whether the strike's parry window is open.</summary>
        public bool IsParrying => CounterEnabled && isActiveAndEnabled && !Pause.GamePause.IsGameplayInputBlocked
            && !Pause.GamePause.IsSimulationBlocked && _timer != null && _timer.IsActive;
        /// <summary>Whether an unsuccessful strike is in whiff recovery.</summary>
        public bool IsOnCooldown => CounterEnabled && isActiveAndEnabled && _timer != null && _timer.IsRecovering;
        /// <summary>Seconds of whiff recovery remaining.</summary>
        public float CooldownRemaining => CounterEnabled && isActiveAndEnabled && _timer != null ? _timer.RecoveryRemaining : 0f;
        /// <summary>Whiff recovery fraction, from one to zero.</summary>
        public float CooldownNormalized => CounterEnabled && isActiveAndEnabled && _timer != null ? _timer.RecoveryNormalized : 0f;

        private void Awake()
        {
            _timer = new CounterVerdictTimer(counterActiveWindow, counterWhiffRecovery);
            _look = GetComponent<PlayerLook>();
            _weapons = GetComponent<WeaponController>();
            if (_look != null) _cam = _look.PlayerCam;
            if (_cam == null) _cam = Camera.main ?? FindAnyObjectByType<Camera>();

            Transform holder = _look != null ? _look.CameraHolder : null;
            if (holder == null && _cam != null) holder = _cam.transform.parent ?? _cam.transform;
            Transform leftover = holder != null ? holder.Find("FP_LeftHand_Socket") : null;
            if (leftover == null) leftover = transform.Find("FP_LeftHand_Socket");
            if (leftover != null)
            {
                if (Application.isPlaying) Destroy(leftover.gameObject);
                else DestroyImmediate(leftover.gameObject);
            }

            // Synthesis allocates once at initialization, never on a parry press.
            if (customParrySound == null) GetOrCreateParryClip();
        }

        private void Start()
        {
            if (_weapons == null) _weapons = GetComponent<WeaponController>();
            if (_cam == null && _look != null) _cam = _look.PlayerCam;
        }

        private void OnDisable()
        {
            _timer?.Reset();
        }

        private void Update()
        {
            if (Pause.GamePause.IsGameplayInputBlocked || Pause.GamePause.IsSimulationBlocked)
            {
                if (!_wasGameplayBlocked) _timer?.Reset();
                _wasGameplayBlocked = true;
                return;
            }
            _wasGameplayBlocked = false;
            if (!CounterEnabled)
            {
                _timer?.Reset();
                return;
            }

            _timer.Tick(Time.deltaTime);
            HandleInput();
            if (IsParrying) CheckAndReflectProjectiles();
        }

        private void HandleInput()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                        || (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame);
            if (!pressed || !_timer.TryStart()) return;

            OnParryTriggered?.Invoke();
            if (_weapons != null) _weapons.TryPlayQuickBlade();
        }

        private void CheckAndReflectProjectiles()
        {
            if (!CounterEnabled || !IsParrying || _cam == null) return;
            Vector3 aimDirection = _cam.transform.forward;
            Vector3 camPos = _cam.transform.position;
            for (int i = EnemyProjectile.ActiveProjectiles.Count - 1; i >= 0; i--)
            {
                if (i >= EnemyProjectile.ActiveProjectiles.Count) continue;
                EnemyProjectile proj = EnemyProjectile.ActiveProjectiles[i];
                if (proj == null || proj.IsDeflected) continue;
                Vector3 toProj = proj.transform.position - camPos;
                if (toProj.sqrMagnitude > projectileCatchRadius * projectileCatchRadius
                    || (toProj.sqrMagnitude >= 0.0001f
                        && Vector3.Angle(aimDirection, toProj.normalized) > counterAngle * 0.5f)) continue;

                proj.Deflect(aimDirection, reflectedSpeedMultiplier, reflectedDamageMultiplier);
                if (!proj.IsDeflected) continue;
                TriggerParryFeedback(true);
                if (DamageNumberSystem.Instance != null)
                    DamageNumberSystem.Instance.SpawnDamageNumber(proj.transform.position, proj.Damage, true, true);
            }
        }

        /// <summary>Checks whether a target lies in the counter's forward acceptance cone.</summary>
        public bool IsFacingTarget(Vector3 targetPos)
        {
            if (!isActiveAndEnabled || !CounterEnabled || _cam == null) return false;
            Vector3 toTarget = targetPos - _cam.transform.position;
            return toTarget.sqrMagnitude < 0.0001f
                || Vector3.Angle(_cam.transform.forward, toTarget.normalized) <= counterAngle * 0.5f;
        }

        /// <summary>Returns the crosshair direction used by reflected projectiles.</summary>
        public Vector3 GetAimDirection()
        {
            if (!CounterEnabled) return Vector3.zero;
            return _cam != null ? _cam.transform.forward : transform.forward;
        }

        /// <summary>Records a contact-path projectile parry without closing the active window.</summary>
        public void NotifyProjectileParried()
        {
            if (!isActiveAndEnabled || !CounterEnabled || !IsParrying || Pause.GamePause.IsSimulationBlocked) return;
            TriggerParryFeedback(true);
        }

        /// <summary>Blocks a frontal melee hit during the active window and stuns its attacker.</summary>
        public bool TryParryMelee(Vector3 hitPoint, Vector3 hitNormal, float damage, GameObject attacker = null)
        {
            if (!isActiveAndEnabled || !CounterEnabled || Pause.GamePause.IsGameplayInputBlocked || !IsParrying || _cam == null)
                return false;

            Vector3 toAttacker = attacker != null
                ? attacker.transform.position - transform.position
                : hitPoint - transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude >= 0.0001f)
            {
                Vector3 forward = Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f)
                    forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (Vector3.Angle(forward, toAttacker.normalized) > counterAngle * 0.5f) return false;
            }

            bool stunned = false;
            if (attacker != null)
            {
                IStunnable stunnable = attacker.GetComponentInParent<IStunnable>();
                if (stunnable != null)
                {
                    stunnable.Stun(meleeStunDuration, toAttacker.normalized);
                    stunned = true;
                }
            }
            if (!stunned)
            {
                int count = Physics.OverlapSphereNonAlloc(transform.position, meleeFallbackRadius,
                    _overlapResults, GameLayers.PlayerAttackMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider col = _overlapResults[i];
                    if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;
                    IStunnable stunnable = CombatTargetLookup.Get(col).Stunnable;
                    if (stunnable == null) continue;
                    stunnable.Stun(meleeStunDuration, (col.transform.position - transform.position).normalized);
                    break;
                }
            }

            TriggerParryFeedback(false);
            if (DamageNumberSystem.Instance != null && attacker != null)
                DamageNumberSystem.Instance.SpawnDamageNumber(attacker.transform.position + Vector3.up * 1.5f, 0f, true, true);
            return true;
        }

        private static AudioClip _cachedParryClip;

        private static AudioClip GetOrCreateParryClip()
        {
            if (_cachedParryClip != null) return _cachedParryClip;

            int sampleRate = 44100;
            float duration = 0.24f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 20f); // Keskin sönümlenme

                // Metalik çınlama harmonikleri: 1150 Hz, 2400 Hz, 4600 Hz
                float tone1 = Mathf.Sin(2f * Mathf.PI * 1150f * t);
                float tone2 = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.65f;
                float tone3 = Mathf.Sin(2f * Mathf.PI * 4600f * t) * 0.35f;

                // Ağır bas gövdesi: 110 Hz
                float bass = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 30f) * 0.9f;

                // Transient metalik vuruş gürültüsü
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 140f) * 0.5f;

                samples[i] = Mathf.Clamp((tone1 + tone2 + tone3 + bass + noise) * envelope, -1f, 1f);
            }

            _cachedParryClip = AudioClip.Create("Synthesized_ParryClang", sampleCount, 1, sampleRate, false);
            _cachedParryClip.SetData(samples, 0);
            return _cachedParryClip;
        }

        private void TriggerParryFeedback(bool isProjectile)
        {
            _timer.NotifySuccess();
            Vector3 effectPos = _cam != null
                ? _cam.transform.position + _cam.transform.forward * 1.5f
                : transform.position + transform.forward * 1.5f;

            Vector3 effectForward = _cam != null ? _cam.transform.forward : transform.forward;

            // 1. İşitsel Geri Bildirim: Tok Metalik Kalkan Çınlaması (CLANG!)
            AudioClip clipToPlay = customParrySound != null ? customParrySound : _cachedParryClip;
            CombatAudio combatAudio = CombatAudio.Instance;
            if (clipToPlay != null && combatAudio != null)
            {
                combatAudio.PlayClipAt(clipToPlay, effectPos, 1.0f);
            }

            // 2. Görsel Şok Dalgası ve Kıvılcım Patlaması
            ParryVFX.Spawn(effectPos, effectForward, isProjectile);

            // 3. Kamera Travması & FOV Darbesi (Kinetic FOV Punch)
            if (_look != null)
            {
                _look.AddTrauma(0.55f);
                _look.DoFovKick(-5.0f); // Anlık odaklanma zoom'u
            }

            // 4. Mikro Hitstop (Ekran Dondurma - 0.10 saniye)
            if (HitstopManager.Instance != null)
            {
                HitstopManager.Instance.TriggerHitstop(0.10f, 0.02f);
            }

            OnParrySuccess?.Invoke(isProjectile);
        }
    }
}
