using System.Collections;
using RetroFPS.Weapons;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Fixed arena supply point with one persistent pickup and a collection-based recharge.</summary>
    [DisallowMultipleComponent]
    public sealed class ArenaAmmoCache : MonoBehaviour
    {
        [SerializeField] private AmmoPickup ammoPickupPrefab;
        [SerializeField] private GameObject readyVisual;
        [SerializeField, Min(0.01f)] private float respawnDelay = 18f;
        [SerializeField, Min(1)] private int shellAmount = 6;
        [SerializeField, Min(1)] private int boltAmount = 20;
        [SerializeField, Min(0f)] private float pickupHeight = 0.6f;

        private PlayerAmmoInventory _inventory;
        private AmmoPickup _pickup;
        private bool _activeSupply;
        private float _remaining;
        private bool _waitingForCollection;

        private void Awake() => _inventory = FindAnyObjectByType<PlayerAmmoInventory>();
        private void OnEnable() => StartCoroutine(Supply());

        /// <summary>Starts with an immediate pickup or clears the supply and its recharge timer.</summary>
        public void SetActiveSupply(bool active)
        {
            if (_activeSupply == active) return;
            _activeSupply = active;
            _remaining = 0f;
            _waitingForCollection = false;
            if (_pickup != null)
            {
                _pickup.gameObject.SetActive(false);
                Destroy(_pickup.gameObject);
                _pickup = null;
            }
            if (readyVisual != null) readyVisual.SetActive(false);
        }

        private IEnumerator Supply()
        {
            while (true)
            {
                yield return null;
                if (!_activeSupply || _pickup != null) continue;
                if (_waitingForCollection)
                {
                    _waitingForCollection = false;
                    _remaining = respawnDelay;
                    if (readyVisual != null) readyVisual.SetActive(false);
                }
                _remaining -= Time.deltaTime;
                if (_remaining > 0f) continue;
                if (_inventory == null) _inventory = FindAnyObjectByType<PlayerAmmoInventory>();
                _pickup = AmmoRewardUtility.Spawn(ammoPickupPrefab, _inventory,
                    transform.position + Vector3.up * pickupHeight, shellAmount, boltAmount);
                _pickup.SetLifetime(0f);
                _waitingForCollection = true;
                if (readyVisual != null) readyVisual.SetActive(true);
            }
        }

        private void OnDisable() { StopAllCoroutines(); SetActiveSupply(false); }
    }
}
