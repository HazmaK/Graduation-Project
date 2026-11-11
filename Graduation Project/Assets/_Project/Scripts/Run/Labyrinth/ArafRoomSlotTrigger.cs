using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Kapı girişini, önbellekteki oyuncuyu doğrulayarak oda yuvasına iletir.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class ArafRoomSlotTrigger : MonoBehaviour
    {
        private ArafRoomSlot _slot;
        private PlayerHealth _player;
        private Transform _playerRoot;

        private void Awake()
        {
            _slot = GetComponentInParent<ArafRoomSlot>();
            _player = FindAnyObjectByType<PlayerHealth>();
            if (_player != null) _playerRoot = _player.transform;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_slot == null || _player == null || _player.IsDead || other == null) return;
            if (other.transform.IsChildOf(_playerRoot)) _slot.NotifyPlayerEntered(_player);
        }
    }
}
