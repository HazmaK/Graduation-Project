using UnityEngine;

namespace RetroFPS.Run.Cards
{
    /// <summary>Offers a room's cards once, after a short scaled-time spawn delay.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class ArafCardAltar : MonoBehaviour
    {
        [SerializeField] private GameObject activeVisual;
        [SerializeField] private GameObject spentVisual;
        [SerializeField, Min(0f)] private float armDelay = 0.75f;

        private Collider _trigger;
        private CardOffer _offer;
        private float _armedAt;
        private bool _initialized;
        private bool _openedOffer;
        private bool _spent;

        /// <summary>Whether this altar's offer has been chosen or refused.</summary>
        public bool IsSpent => _spent;

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
        }

        private void Reset()
        {
            if (TryGetComponent(out SphereCollider trigger))
            {
                trigger.isTrigger = true;
                trigger.radius = 1.6f;
            }
        }

        private void OnEnable() => RunEvents.OnCardOfferResolved += HandleOfferResolved;
        private void OnDisable() => RunEvents.OnCardOfferResolved -= HandleOfferResolved;
        private void OnDestroy() => RunEvents.OnCardOfferResolved -= HandleOfferResolved;

        /// <summary>Arms an unspent altar with the supplied offer.</summary>
        public void Initialize(CardOffer offer)
        {
            if (_spent || _openedOffer) return;
            _offer = offer;
            _initialized = offer.Count > 0;
            _armedAt = Time.time + Mathf.Max(0f, armDelay);
        }

        private void OnTriggerEnter(Collider other) => TryOpen(other);

        // Also lets a player already at the spawn point receive the offer after the delay.
        private void OnTriggerStay(Collider other) => TryOpen(other);

        private void TryOpen(Collider other)
        {
            if (!_initialized || _spent || _openedOffer || Time.time < _armedAt ||
                RunEvents.IsCardOfferOpen || other == null) return;

            PlayerHealth player = null;
            if (!other.TryGetComponent(out player) && other.attachedRigidbody != null)
                other.attachedRigidbody.TryGetComponent(out player);
            if (player == null) player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead) return;

            // Set ownership first: an opened-event subscriber may resolve synchronously.
            _openedOffer = true;
            RunEvents.RaiseCardOfferOpened(_offer);
        }

        private void HandleOfferResolved(CurseBlessingDefinition chosenOrNull)
        {
            if (!_openedOffer || _spent) return;
            _openedOffer = false;
            _spent = true;
            if (_trigger != null) _trigger.enabled = false;
            if (activeVisual != null) activeVisual.SetActive(false);
            if (spentVisual != null) spentVisual.SetActive(true);
        }
    }
}
