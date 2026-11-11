using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Bir odanın perdelerini, giriş tetiklerini ve mühürlerini yönetir.</summary>
    [DisallowMultipleComponent]
    public class ArafRoomSlot : MonoBehaviour
    {
        private int _slotIndex;
        private RoomSlotInfo _info;
        private GameObject[] _veils;
        private Collider[] _doorwayTriggers;
        private GameObject[] _barriers;
        private bool _entryRaised;
        private bool _isRevealed;
        private ArafRoom _room;

        /// <summary>Yerleşimdeki oda indeksi.</summary>
        public int SlotIndex => _slotIndex;
        /// <summary>Odanın yerleşim bilgisi.</summary>
        public RoomSlotInfo Info => _info;
        /// <summary>Oda içeriğinin açılıp açılmadığı.</summary>
        public bool IsRevealed => _isRevealed;
        /// <summary>Açılan oda; boş sonuçta null.</summary>
        public ArafRoom Room => _room;
        /// <summary>Canlı oyuncu ilk kez kapıdan girdiğinde yükseltilir.</summary>
        public event Action<ArafRoomSlot> OnPlayerEnteredDoorway;

        /// <summary>Üreticinin hazırladığı oda ve kapı nesnelerini kaydeder.</summary>
        public void Initialize(int index, RoomSlotInfo info, GameObject[] veils, Collider[] doorwayTriggers, GameObject[] barriers)
        {
            _slotIndex = index;
            _info = info;
            _veils = veils;
            _doorwayTriggers = doorwayTriggers;
            _barriers = barriers;
            _entryRaised = false;
            _isRevealed = false;
            _room = null;
            SetBarriersActive(false);
        }

        internal void NotifyPlayerEntered(PlayerHealth player)
        {
            if (_isRevealed || _entryRaised || player == null || player.IsDead) return;
            _entryRaised = true;
            OnPlayerEnteredDoorway?.Invoke(this);
        }

        /// <summary>Sonucu kaydeder, perdeleri ve tekrar giriş tetiklerini kapatır.</summary>
        public void MarkRevealed(ArafRoom room)
        {
            _room = room;
            _isRevealed = true;
            if (_veils != null)
                for (int i = 0; i < _veils.Length; i++)
                    if (_veils[i] != null) _veils[i].SetActive(false);
            if (_doorwayTriggers != null)
                for (int i = 0; i < _doorwayTriggers.Length; i++)
                    if (_doorwayTriggers[i] != null) _doorwayTriggers[i].enabled = false;
        }

        /// <summary>Odanın bütün kapı mühürlerini açar veya kapatır.</summary>
        public void SetBarriersActive(bool active)
        {
            if (_barriers == null) return;
            for (int i = 0; i < _barriers.Length; i++)
                if (_barriers[i] != null) _barriers[i].SetActive(active);
        }
    }
}
