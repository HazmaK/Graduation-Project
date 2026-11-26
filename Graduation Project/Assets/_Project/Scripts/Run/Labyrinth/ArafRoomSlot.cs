using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Üretimde kurulmuş odanın rolünü ve arena kapılarını saklar.</summary>
    [DisallowMultipleComponent]
    public class ArafRoomSlot : MonoBehaviour
    {
        private int _slotIndex;
        private RoomSlotInfo _info;
        private GameObject[] _barriers;
        private ArafArenaDoor[] _doors;
        private bool _isRevealed;
        private ArafRoom _room;
        private ArafArena _arena;
        private ArafRoomRole _role;
        /// <summary>Yerleşimdeki oda indeksi.</summary>
        public int SlotIndex => _slotIndex;
        /// <summary>Odanın yerleşim bilgisi.</summary>
        public RoomSlotInfo Info => _info;
        /// <summary>Odanın üretim sırasında kurulup kurulmadığı.</summary>
        public bool IsRevealed => _isRevealed;
        /// <summary>Kurulan oda; boş sonuçta null.</summary>
        public ArafRoom Room => _room;
        /// <summary>Odaya bağlı arena; sakin odalarda null.</summary>
        public ArafArena Arena => _arena;
        /// <summary>Üretimde belirlenen oda rolü.</summary>
        public ArafRoomRole Role => _role;

        /// <summary>Üreticinin hazırladığı oda ve kapı nesnelerini kaydeder.</summary>
        public void Initialize(int index, RoomSlotInfo info, GameObject[] barriers)
        {
            _slotIndex = index;
            _info = info;
            _barriers = barriers;
            _doors = new ArafArenaDoor[barriers != null ? barriers.Length : 0];
            for (int i = 0; i < _doors.Length; i++)
            {
                if (barriers[i] == null) continue;
                barriers[i].TryGetComponent(out _doors[i]);
                if (_doors[i] != null) _doors[i].SetClosed(false, true);
                else barriers[i].SetActive(false);
            }
        }

        /// <summary>Kurulan oda ile birlikte arena ve rolünü kaydeder.</summary>
        public void SetRoom(ArafRoom room, ArafArena arena, ArafRoomRole role)
        {
            _room = room;
            _arena = arena;
            _role = role;
            _isRevealed = true;
        }

        /// <summary>Odanın bütün kapılarını animasyonla açar veya kapatır.</summary>
        public void SetBarriersActive(bool active)
        {
            if (_barriers == null) return;
            for (int i = 0; i < _barriers.Length; i++)
            {
                if (_barriers[i] == null) continue;
                if (_doors[i] != null)
                {
                    if (active) _barriers[i].SetActive(true);
                    _doors[i].SetClosed(active);
                }
                else _barriers[i].SetActive(active);
            }
        }
    }
}
