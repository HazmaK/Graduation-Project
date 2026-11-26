using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Mühür odası prefab kökünde yer alır; oda temizlendiğinde mühür görselini gizleyip <see cref="OnSealTaken"/> olayını yükseltir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArafSealRoom : MonoBehaviour
    {
        [SerializeField] private ArafRoom room;
        [SerializeField] private ArafArena arena;
        [SerializeField] private GameObject sealVisual;
        [SerializeField] private Transform altarAnchor;

        private bool _isTaken;
        private bool _subscribed;

        /// <summary>Odanın mührü alındığında tam bir kez yükseltilir.</summary>
        public event Action<ArafSealRoom> OnSealTaken;

        /// <summary>Mührün alınıp alınmadığı.</summary>
        public bool IsTaken => _isTaken;

        /// <summary>Mühür alındığında kart sunağının doğurulacağı konum.</summary>
        public Transform AltarAnchor => altarAnchor;

        private void Awake()
        {
            if (arena == null) arena = GetComponent<ArafArena>();
            if (room == null)
            {
                room = GetComponent<ArafRoom>();
            }
        }

        private void OnEnable()
        {
            if (_isTaken) return;
            if (arena != null)
            {
                if (arena.IsCleared) { TakeSeal(); return; }
                if (!_subscribed)
                {
                    arena.OnCleared += HandleArenaCleared;
                    _subscribed = true;
                }
                return;
            }
            if (room == null) return;

            if (room.IsCleared)
            {
                TakeSeal();
                return;
            }

            if (!_subscribed)
            {
                room.OnCleared += HandleRoomCleared;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void HandleRoomCleared(ArafRoom clearedRoom)
        {
            TakeSeal();
        }

        private void HandleArenaCleared(ArafArena clearedArena) => TakeSeal();

        private void TakeSeal()
        {
            if (_isTaken) return;
            _isTaken = true;
            Unsubscribe();

            if (sealVisual != null)
            {
                sealVisual.SetActive(false);
            }

            OnSealTaken?.Invoke(this);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (arena != null) arena.OnCleared -= HandleArenaCleared;
            if (room != null)
            {
                room.OnCleared -= HandleRoomCleared;
            }
            _subscribed = false;
        }
    }
}
