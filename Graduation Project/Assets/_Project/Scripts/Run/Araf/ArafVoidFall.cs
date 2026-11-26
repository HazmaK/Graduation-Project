using RetroFPS.Combat;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Builds the void trigger and recovers players while killing fallen enemies.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafVoidFall : MonoBehaviour
    {
        private const float BoundsMargin = 40f;
        private const float TriggerThickness = 6f;
        private const float ReentryDelay = 0.5f;

        [SerializeField] private ArafLabyrinthDirector labyrinth;
        [SerializeField] private ArafSafeGround safe;
        [SerializeField, Min(0f)] private float fallDamage = 20f;

        private ArafLevel _level;
        private GameObject _trigger;
        private float _nextPlayerFallTime;

        private void Awake()
        {
            if (labyrinth == null) labyrinth = GetComponent<ArafLabyrinthDirector>();
            if (safe == null) safe = GetComponent<ArafSafeGround>();
            _level = GetComponent<ArafLevel>();
        }

        private void OnEnable()
        {
            if (labyrinth != null) labyrinth.NavMeshReady += TryCreateTrigger;
            if (_level != null) _level.ContentActiveChanged += HandleContentActiveChanged;
            TryCreateTrigger();
        }

        private void Start()
        {
            // The director may generate after this component's OnEnable.
            if (safe == null) safe = GetComponent<ArafSafeGround>();
            TryCreateTrigger();
        }

        private void OnDisable()
        {
            if (labyrinth != null) labyrinth.NavMeshReady -= TryCreateTrigger;
            if (_level != null) _level.ContentActiveChanged -= HandleContentActiveChanged;
        }

        private void HandleContentActiveChanged(bool active)
        {
            if (active) TryCreateTrigger();
        }

        private void TryCreateTrigger()
        {
            if (_trigger != null || labyrinth == null || labyrinth.World == null
                || labyrinth.GeneratedRoot == null || !labyrinth.GeneratedRoot.gameObject.activeInHierarchy) return;

            Bounds bounds = labyrinth.WorldBoundsLocal;
            _trigger = new GameObject("VoidFallTrigger");
            Transform triggerTransform = _trigger.transform;
            triggerTransform.SetParent(labyrinth.GeneratedRoot, false);
            // Bounds are expressed in director space, even when GeneratedRoot has another parent.
            Vector3 center = bounds.center;
            center.y = labyrinth.VoidHeightLocal - TriggerThickness * 0.5f;
            triggerTransform.SetPositionAndRotation(labyrinth.transform.TransformPoint(center), labyrinth.transform.rotation);
            BoxCollider box = _trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(bounds.size.x + BoundsMargin * 2f, TriggerThickness,
                bounds.size.z + BoundsMargin * 2f);
            Rigidbody body = _trigger.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            ArafVoidFallTrigger relay = _trigger.AddComponent<ArafVoidFallTrigger>();
            relay.Initialize(this);
        }

        internal void HandleTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || other == null) return;
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                if (RunEvents.CurrentRun == null || RunEvents.CurrentRun.Phase != RunPhase.InAraf
                    || player.IsDead || Time.time < _nextPlayerFallTime || safe == null) return;
                PlayerMovement movement = player.GetComponent<PlayerMovement>();
                if (movement == null) return;
                _nextPlayerFallTime = Time.time + ReentryDelay;
                Vector3 position = safe.LastSafePosition;
                Quaternion rotation = safe.LastSafeRotation;
                float cap = ArafFallRules.ComputeFallDamage(player.CurrentHealth, fallDamage);
                if (cap > 0f)
                {
                    // PlayerHealth, hasarı DamageTaken stat'ı (kart lanetleri) ile sonradan çarpar; düşmenin
                    // öldürmemesi için ham hasarı, çarpılmış sonuç cap'i aşmayacak şekilde küçült.
                    float raw = cap;
                    float applied = RetroFPS.Stats.PlayerStats.Get(RetroFPS.Stats.StatId.DamageTaken, raw);
                    if (applied > cap && applied > 0f) raw *= cap / applied;
                    if (RetroFPS.Stats.PlayerStats.Get(RetroFPS.Stats.StatId.DamageTaken, raw) > cap) raw = 0f;
                    // Parry düşüşü engellememeli; dash i-frame'i ise hasarı bilerek bağışlar.
                    if (raw > 0f) player.TakeUnparryableDamage(raw, player.transform.position, Vector3.up);
                }
                movement.TeleportTo(position, rotation);
                safe.ResetTo(position, rotation);
                return;
            }

            IEncounterEnemy enemy = other.GetComponentInParent<IEncounterEnemy>();
            if (enemy == null || !enemy.IsAlive) return;
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null && enemy.EnemyGameObject != null)
                enemy.EnemyGameObject.TryGetComponent(out damageable);
            if (damageable != null)
                damageable.TakeDamage(100000f, other.transform.position, Vector3.up, false);
        }
    }

    /// <summary>Forwards generated void trigger contacts to its owning director component.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArafVoidFallTrigger : MonoBehaviour
    {
        private ArafVoidFall _owner;

        internal void Initialize(ArafVoidFall owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null) _owner.HandleTriggerEnter(other);
        }
    }
}
