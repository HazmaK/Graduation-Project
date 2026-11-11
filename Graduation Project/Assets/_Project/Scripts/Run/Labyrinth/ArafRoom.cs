using System;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Her Araf oda prefabının kökünde yer alan çalışma zamanı denetleyicisi.
    /// Düşman etkinleştirmesini, elit ölçeklemeyi, tapınak iyileştirmesini ve kaçış portalı mührünü yönetir.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArafRoom : MonoBehaviour
    {
        private const float ShrineOverhealCap = 150f;

        [SerializeField] private GameObject[] enemies;
        [SerializeField] private ArafEscapePortal portal;
        [SerializeField] private Collider activationTrigger;
        [SerializeField, Min(0f)] private float shrineHealAmount = 0f;

        private GameObject[] _enemyObjects;
        private IEncounterEnemy[] _encounterEnemies;
        private IEliteScalable[] _eliteEnemies;
        private Transform[] _enemyTransforms;
        private CharacterController[] _controllers;
        private bool[] _countedAlive;
        private Action<IEncounterEnemy> _deathHandler;

        private ArafRoomDefinition _definition;
        private float _damageMultiplier = 1f;
        private float _speedMultiplier = 1f;
        private float _healthMultiplier = 1f;

        private int _totalEnemyCount;
        private int _aliveEnemyCount;
        private bool _activated;
        private bool _enemiesSpawned;
        private bool _subscribed;
        private bool _isCleared;
        private bool _shrineHealed;
        private bool _triggerConsumed;

        /// <summary>Odadaki düşmanlar etkinleştirildiğinde (sayı &gt; 0 ise) yükseltilir.</summary>
        public event Action<ArafRoom> OnCombatStarted;

        /// <summary>Odadaki tüm düşmanlar öldüğünde (veya odada hiç düşman yoksa anında) tam bir kez yükseltilir.</summary>
        public event Action<ArafRoom> OnCleared;

        /// <summary>Odanın temizlenmiş olup olmadığı.</summary>
        public bool IsCleared => _isCleared;

        /// <summary>Odada hayatta kalan düşman sayısı.</summary>
        public int AliveEnemyCount => _aliveEnemyCount;

        /// <summary>Odadaki toplam benzersiz düşman sayısı.</summary>
        public int TotalEnemyCount => _totalEnemyCount;

        /// <summary>Odayı oluşturan tanım verisi.</summary>
        public ArafRoomDefinition Definition => _definition;

        /// <summary>Odada bir kaçış portalı bulunup bulunmadığı.</summary>
        public bool HasPortal => portal != null;

        private void Awake()
        {
            int count = enemies != null ? enemies.Length : 0;
            _enemyObjects = new GameObject[count];
            _encounterEnemies = new IEncounterEnemy[count];
            _eliteEnemies = new IEliteScalable[count];
            _enemyTransforms = new Transform[count];
            _controllers = new CharacterController[count];
            _countedAlive = new bool[count];
            _deathHandler = HandleEnemyDeath;

            for (int i = 0; i < count; i++)
            {
                GameObject enemyObject = enemies[i];
                if (enemyObject == null) continue;

                IEncounterEnemy enemy = enemyObject.GetComponent<IEncounterEnemy>();
                if (enemy == null)
                {
                    Debug.LogError("[ArafRoom] Düşmanda IEncounterEnemy bulunamadı.", enemyObject);
                    continue;
                }

                bool duplicate = false;
                for (int j = 0; j < _totalEnemyCount; j++)
                {
                    if (ReferenceEquals(_encounterEnemies[j], enemy))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                int index = _totalEnemyCount++;
                _enemyObjects[index] = enemyObject;
                _encounterEnemies[index] = enemy;
                _eliteEnemies[index] = enemyObject.GetComponent<IEliteScalable>();
                _controllers[index] = enemyObject.GetComponent<CharacterController>();
                _enemyTransforms[index] = enemyObject.transform;

                if (enemyObject.activeSelf)
                {
                    enemyObject.SetActive(false);
                }
            }

            _aliveEnemyCount = _totalEnemyCount;

            if (activationTrigger != null)
            {
                activationTrigger.isTrigger = true;
                if (!activationTrigger.gameObject.TryGetComponent(out ArafRoomTriggerRelay relay))
                {
                    relay = activationTrigger.gameObject.AddComponent<ArafRoomTriggerRelay>();
                }
                relay.Bind(this);
            }
        }

        private void OnEnable()
        {
            if (_activated && portal != null)
            {
                portal.SetSealed(!_isCleared);
            }

            if (!_enemiesSpawned) return;

            SubscribeEnemies();
            for (int i = 0; i < _totalEnemyCount; i++)
            {
                if (_countedAlive[i] && (_enemyTransforms[i] == null || !_encounterEnemies[i].IsAlive))
                {
                    HandleEnemyDeath(_encounterEnemies[i]);
                }
            }
            if (_aliveEnemyCount == 0)
            {
                CompleteRoom();
            }
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            for (int i = 0; i < _totalEnemyCount; i++)
            {
                if (_encounterEnemies[i] != null)
                {
                    _encounterEnemies[i].OnEnemyDeath -= _deathHandler;
                }
            }
            _subscribed = false;
        }

        /// <summary>Oda örneklendikten hemen sonra çağrılarak düşmanları, tapınağı ve portal mührünü başlatır.</summary>
        public void Activate(ArafRoomDefinition definition, float damageMultiplier, float speedMultiplier, float healthMultiplier)
        {
            if (_activated) return;
            _activated = true;

            _definition = definition;
            _damageMultiplier = damageMultiplier > 0f ? damageMultiplier : 1f;
            _speedMultiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
            _healthMultiplier = healthMultiplier > 0f ? healthMultiplier : 1f;
            _aliveEnemyCount = _totalEnemyCount;

            if (portal != null)
            {
                portal.SetSealed(true);
            }

            if (activationTrigger == null)
            {
                if (shrineHealAmount > 0f)
                {
                    PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
                    if (player != null && !player.IsDead)
                    {
                        ApplyShrineHealOnce(player);
                    }
                }

                if (_totalEnemyCount > 0)
                {
                    SpawnEnemies();
                }
                else
                {
                    CompleteRoom();
                }
            }
            else if (_totalEnemyCount == 0)
            {
                CompleteRoom();
            }
        }

        /// <summary>İç tetikleyiciye bir çarpıştırıcı girdiğinde <see cref="ArafRoomTriggerRelay"/> tarafından çağrılır.</summary>
        internal void HandleActivationTriggerEnter(Collider other)
        {
            if (!_activated || _triggerConsumed || other == null) return;

            PlayerHealth player = null;
            if (!other.TryGetComponent(out player) && other.attachedRigidbody != null)
            {
                other.attachedRigidbody.TryGetComponent(out player);
            }
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerHealth>();
            }
            if (player == null || player.IsDead) return;

            _triggerConsumed = true;

            if (shrineHealAmount > 0f)
            {
                ApplyShrineHealOnce(player);
            }

            if (!_enemiesSpawned && _totalEnemyCount > 0)
            {
                SpawnEnemies();
            }
        }

        private void ApplyShrineHealOnce(PlayerHealth player)
        {
            if (_shrineHealed || shrineHealAmount <= 0f || player == null || player.IsDead) return;
            _shrineHealed = true;
            player.Heal(shrineHealAmount, true, ShrineOverhealCap);
            Debug.Log("[ArafRoom] Tapınak kutsaması oyuncuyu iyileştirdi.", this);
        }

        private void SpawnEnemies()
        {
            if (_enemiesSpawned) return;
            _enemiesSpawned = true;
            _aliveEnemyCount = 0;

            for (int i = 0; i < _totalEnemyCount; i++)
            {
                Transform enemyTransform = _enemyTransforms[i];
                GameObject enemyObject = _enemyObjects[i];
                if (enemyTransform == null || enemyObject == null) continue;

                Vector3 position = enemyTransform.position;
                Quaternion rotation = enemyTransform.rotation;

                if (!enemyObject.activeSelf)
                {
                    enemyObject.SetActive(true);
                }

                CharacterController controller = _controllers[i];
                bool wasEnabled = controller != null && controller.enabled;
                if (wasEnabled) controller.enabled = false;
                enemyTransform.SetPositionAndRotation(position, rotation);
                if (wasEnabled) controller.enabled = true;

                _encounterEnemies[i].OnSpawnFromDirector(position, rotation);
                _eliteEnemies[i]?.ApplyEliteScaling(_damageMultiplier, _speedMultiplier, _healthMultiplier);
                _countedAlive[i] = true;
                _aliveEnemyCount++;
            }

            SubscribeEnemies();

            if (_aliveEnemyCount > 0)
            {
                OnCombatStarted?.Invoke(this);
            }
            else
            {
                CompleteRoom();
            }
        }

        private void SubscribeEnemies()
        {
            if (_subscribed) return;
            for (int i = 0; i < _totalEnemyCount; i++)
            {
                if (_enemyTransforms[i] != null)
                {
                    _encounterEnemies[i].OnEnemyDeath += _deathHandler;
                }
            }
            _subscribed = true;
        }

        private void HandleEnemyDeath(IEncounterEnemy enemy)
        {
            for (int i = 0; i < _totalEnemyCount; i++)
            {
                if (!_countedAlive[i] || !ReferenceEquals(_encounterEnemies[i], enemy)) continue;
                _countedAlive[i] = false;
                _aliveEnemyCount--;
                if (_aliveEnemyCount <= 0)
                {
                    _aliveEnemyCount = 0;
                    CompleteRoom();
                }
                return;
            }
        }

        private void CompleteRoom()
        {
            if (_isCleared) return;
            _isCleared = true;
            _aliveEnemyCount = 0;

            if (portal != null)
            {
                portal.SetSealed(false);
                Debug.Log("[ArafRoom] Tüm muhafızlar öldü; kaçış portalının mührü açıldı.", this);
            }

            OnCleared?.Invoke(this);
        }
    }
}
