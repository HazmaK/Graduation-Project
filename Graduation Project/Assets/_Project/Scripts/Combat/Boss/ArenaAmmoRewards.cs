using System.Collections;
using System.Collections.Generic;
using RetroFPS.Weapons;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Queues boss skill rewards and scatters existing ammo pickups onto clear arena ground.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaAmmoRewards : MonoBehaviour
    {
        [SerializeField] private AmmoPickup ammoPickupPrefab;
        [SerializeField, Min(1)] private int shellAmount = 6;
        [SerializeField, Min(1)] private int boltAmount = 20;
        [SerializeField, Min(0f)] private float scatterRadius = 2.5f;
        [SerializeField, Min(0f)] private float arenaInset = 1.5f;
        [SerializeField, Min(0.01f)] private float groundProbeLift = 3f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 40f;
        [SerializeField, Min(0.01f)] private float clearance = 0.6f;
        [SerializeField, Min(1)] private int placementAttempts = 12;

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];
        private readonly List<AmmoPickup> _drops = new List<AmmoPickup>(32);
        private PlayerAmmoInventory _inventory;
        private Bounds _arena;
        private bool _hasArena;
        private int _pending;

        private void Awake() => _inventory = FindAnyObjectByType<PlayerAmmoInventory>();
        private void OnEnable() => StartCoroutine(Dispatch());
        private void OnDisable() { StopAllCoroutines(); ClearRewards(); }

        /// <summary>Sets world bounds used to keep reward pickups inside the arena.</summary>
        public void SetArena(Bounds arena) { _arena = arena; _hasArena = true; }

        /// <summary>Queues spawn events for dispatch outside the frame-update call stack.</summary>
        public void QueueDrops(int count) => _pending += Mathf.Max(0, count);

        /// <summary>Clears pending and uncollected rewards when this encounter resets.</summary>
        public void ClearRewards()
        {
            _pending = 0;
            for (int i = 0; i < _drops.Count; i++)
                if (_drops[i] != null)
                {
                    _drops[i].gameObject.SetActive(false);
                    Destroy(_drops[i].gameObject);
                }
            _drops.Clear();
        }

        private IEnumerator Dispatch()
        {
            while (true)
            {
                yield return null;
                int count = _pending;
                _pending = 0;
                if (count <= 0 || !_hasArena) continue;
                if (_inventory == null) _inventory = FindAnyObjectByType<PlayerAmmoInventory>();
                for (int i = _drops.Count - 1; i >= 0; i--)
                    if (_drops[i] == null) _drops.RemoveAt(i);
                Vector3 origin = transform.position;
                for (int i = 0; i < count; i++)
                    for (int attempt = 0; attempt < placementAttempts; attempt++)
                    {
                        float angle = (i / (float)count + attempt / (float)placementAttempts) * Mathf.PI * 2f;
                        Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * scatterRadius;
                        candidate.x = Mathf.Clamp(candidate.x, _arena.min.x + arenaInset, _arena.max.x - arenaInset);
                        candidate.z = Mathf.Clamp(candidate.z, _arena.min.z + arenaInset, _arena.max.z - arenaInset);
                        if (!AmmoRewardUtility.TryGroundPosition(origin, candidate, groundProbeLift,
                            groundProbeDistance, clearance, _hits, _overlaps, out Vector3 position)) continue;
                        _drops.Add(AmmoRewardUtility.Spawn(ammoPickupPrefab, _inventory, position, shellAmount, boltAmount));
                        break;
                    }
            }
        }
    }
}
