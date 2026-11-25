using RetroFPS.Pause;
using System;
using RetroFPS.Combat;
using RetroFPS.Run;
using UnityEngine;
using UnityEngine.Events;

namespace RetroFPS
{
    /// <summary>Seals The Judge's arena and owns its checkpoint and purgatory lifecycle.</summary>
    [DisallowMultipleComponent]
    public sealed class BossEncounter : MonoBehaviour, IRetryableEncounter
    {
        [SerializeField] private JudgeBossAI boss;
        [SerializeField] private BoxCollider arenaTrigger;
        [SerializeField] private GameObject[] barriers;
        [SerializeField] private ArenaAmmoCache[] ammoCaches;
        [SerializeField] private Transform retryCheckpoint;
        [SerializeField] private Transform bossSpawnAnchor;
        [SerializeField, Min(0f)] private float retryWarmupDuration = 2.5f;
        [SerializeField] private UnityEvent onBossDefeated = new UnityEvent();

        private PlayerHealth _player;
        private Transform _playerTransform;
        private Func<bool> _retryHandler;
        private bool _triggered;
        private bool _active;
        private bool _defeated;
        private bool _suspended;
        private bool _warmingUp;
        private bool _hasCheckpoint;
        private float _warmupRemaining;
        private Vector3 _checkpointPosition;
        private Quaternion _checkpointRotation;

        /// <inheritdoc />
        public bool CanResumeAfterDeath => _active && !_defeated && _player != null && _hasCheckpoint;

        private void Awake()
        {
            _retryHandler = HandleLegacyRetry;
            if (arenaTrigger != null) arenaTrigger.isTrigger = true;
        }

        private void Start()
        {
            _player = FindAnyObjectByType<PlayerHealth>();
            if (_player != null) _playerTransform = _player.transform;
            SetBarriers(false);
        }

        private void OnEnable()
        {
            if (boss != null) boss.OnEnemyDeath += HandleBossDeath;
            if (_active) RegisterRetry();
        }

        private void OnDisable()
        {
            SetAmmoSupply(false);
            if (boss != null) boss.OnEnemyDeath -= HandleBossDeath;
            ReleaseRetry();
            // A suspended encounter can be disabled while Araf owns the scene. Preserve its retry token.
            if (_suspended) return;
            _active = _warmingUp = false;
            if (!_defeated) _triggered = false;
            if (boss != null && !_defeated) boss.ResetBoss();
            SetBarriers(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != GameLayers.Player) return;
            if (_player == null)
            {
                _player = other.GetComponentInParent<PlayerHealth>();
                if (_player != null) _playerTransform = _player.transform;
            }
            TryBegin();
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            // The Gauntlet bounds is a child of the encounter object. This also handles a player
            // already inside at scene load, without adding a Rigidbody or modifying that hierarchy.
            if (!_triggered && !_defeated && _playerTransform != null && arenaTrigger != null
                && arenaTrigger.enabled && arenaTrigger.gameObject.activeInHierarchy
                && _playerTransform.gameObject.layer == GameLayers.Player
                && arenaTrigger.bounds.Contains(_playerTransform.position)) TryBegin();
            if (!_warmingUp) return;
            _warmupRemaining -= Time.deltaTime;
            if (_warmupRemaining > 0f) return;
            _warmingUp = false;
            if (boss != null) { boss.Engage(); SetAmmoSupply(true); }
        }

        private void TryBegin()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (_triggered || _defeated || _suspended || boss == null || arenaTrigger == null) return;
            if (_player == null || _player.IsDead) return;
            _triggered = _active = true;
            Transform checkpoint = retryCheckpoint != null ? retryCheckpoint : _playerTransform;
            _checkpointPosition = checkpoint.position;
            _checkpointRotation = Quaternion.Euler(0f, checkpoint.eulerAngles.y, 0f);
            _hasCheckpoint = true;
            boss.SetArena(arenaTrigger.bounds, bossSpawnAnchor);
            RegisterRetry();
            SetBarriers(true);
            boss.Engage();
            SetAmmoSupply(true);
        }

        private void RegisterRetry()
        {
            if (_player == null || !_hasCheckpoint) return;
            _player.RetryInterceptor = _retryHandler;
            RunEvents.RegisterActiveEncounter(this);
        }

        private void ReleaseRetry()
        {
            if (_player != null && _player.RetryInterceptor == _retryHandler) _player.RetryInterceptor = null;
            RunEvents.UnregisterActiveEncounter(this);
        }

        private void HandleBossDeath(IEncounterEnemy enemy)
        {
            if (_defeated) return;
            _defeated = true;
            _active = _warmingUp = false;
            SetAmmoSupply(false);
            SetBarriers(false);
            ReleaseRetry();
            onBossDefeated?.Invoke();
            if ((RunEvents.CurrentRun == null || RunEvents.CurrentRun.Phase == RunPhase.Tower)
                && (_player == null || !_player.IsDead))
                RunEvents.RaiseFloorCleared();
        }

        /// <inheritdoc />
        public void SuspendForPurgatory()
        {
            if (!CanResumeAfterDeath) return;
            _active = _warmingUp = false;
            _suspended = true;
            ReleaseRetry();
            boss.ResetBoss();
            SetAmmoSupply(false);
            SetBarriers(false);
        }

        /// <inheritdoc />
        public void ResumeAfterPurgatory()
        {
            if (!_suspended || _player == null || boss == null || arenaTrigger == null) return;
            _suspended = false;
            // Match WaveSpawnDirector: revive at the recorded checkpoint and restart the encounter warmup.
            _player.Revive(_checkpointPosition, _checkpointRotation);
            boss.SetArena(arenaTrigger.bounds, bossSpawnAnchor);
            boss.ResetBoss();
            SetAmmoSupply(false);
            _active = _warmingUp = true;
            _triggered = true;
            _warmupRemaining = Mathf.Max(0f, retryWarmupDuration);
            RegisterRetry();
            SetBarriers(true);
        }

        private bool HandleLegacyRetry()
        {
            if (!CanResumeAfterDeath) return false;
            SuspendForPurgatory();
            ResumeAfterPurgatory();
            return true;
        }

        private void SetBarriers(bool active)
        {
            if (barriers == null) return;
            for (int i = 0; i < barriers.Length; i++)
                if (barriers[i] != null) barriers[i].SetActive(active);
        }

        private void SetAmmoSupply(bool active)
        {
            if (ammoCaches == null) return;
            for (int i = 0; i < ammoCaches.Length; i++)
                if (ammoCaches[i] != null) ammoCaches[i].SetActiveSupply(active);
        }
    }
}
