using System;
using RetroFPS.Combat;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace RetroFPS.Run
{
    /// <summary>Forwards combat kills and player damage into the active run's statistics.</summary>
    [DisallowMultipleComponent]
    public sealed class RunRecordRecorder : MonoBehaviour
    {
        private PlayerHealth _player;
        private Action<Vector3, bool, bool> _killHandler;
        private Action<float> _damageHandler;
        private UnityAction<Scene, LoadSceneMode> _sceneLoadedHandler;
        private bool _damageSubscribed;
        private bool _started;

        private void Awake()
        {
            _killHandler = HandleEnemyKilled;
            _damageHandler = HandleDamageTaken;
            _sceneLoadedHandler = HandleSceneLoaded;
        }

        private void OnEnable()
        {
            CombatEvents.OnEnemyKilled += _killHandler;
            SceneManager.sceneLoaded += _sceneLoadedHandler;
            if (_started) ResolvePlayer();
        }

        private void Start()
        {
            _started = true;
            ResolvePlayer();
        }

        private void OnDisable()
        {
            CombatEvents.OnEnemyKilled -= _killHandler;
            SceneManager.sceneLoaded -= _sceneLoadedHandler;
            if (_damageSubscribed && _player != null) _player.OnDamageTaken -= _damageHandler;
            _damageSubscribed = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_player == null) ResolvePlayer();
        }

        private void ResolvePlayer()
        {
            if (_player == null)
            {
                _damageSubscribed = false;
                _player = FindAnyObjectByType<PlayerHealth>();
            }
            if (_player == null || _damageSubscribed) return;
            _player.OnDamageTaken += _damageHandler;
            _damageSubscribed = true;
        }

        private void HandleEnemyKilled(Vector3 position, bool wasCritical, bool wasGibbed)
        {
            RunState run = RunEvents.CurrentRun;
            if (run == null) return;
            RunPhase phase = run.Phase;
            bool inAraf = phase == RunPhase.FallingToAraf || phase == RunPhase.InAraf || phase == RunPhase.EscapingAraf;
            run.Record.RecordKill(inAraf, wasCritical, wasGibbed);
        }

        private void HandleDamageTaken(float amount) => RunEvents.CurrentRun?.Record.RecordDamage(amount);
    }
}
