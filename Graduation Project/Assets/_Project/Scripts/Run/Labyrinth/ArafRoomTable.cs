using UnityEngine;
using UnityEngine.Serialization;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf labirenti için olası oda havuzunu ve mühür odası tanımını barındırır.</summary>
    [CreateAssetMenu(fileName = "ArafRoomTable_New", menuName = "RetroFPS/Araf/Room Table", order = 31)]
    public class ArafRoomTable : ScriptableObject
    {
        [SerializeField] private ArafRoomDefinition[] rooms;
        [FormerlySerializedAs("exitRoom")]
        [SerializeField] private ArafRoomDefinition sealRoom;
        [SerializeField] private ArafRoomDefinition arenaRoom;
        [SerializeField] private ArafArenaWaveTable arenaWaves;

        /// <summary>İsteğe bağlı arena odası tanımı.</summary>
        public ArafRoomDefinition ArenaRoom => arenaRoom;
        /// <summary>Arena düşmanları ve dalga ayarları.</summary>
        public ArafArenaWaveTable ArenaWaves => arenaWaves;

        /// <summary>Normal (mühür dışı) oda tanımlarının sayısı.</summary>
        public int RoomCount => rooms != null ? rooms.Length : 0;

        /// <summary>Verilen dizindeki normal oda tanımını döndürür.</summary>
        public ArafRoomDefinition GetRoom(int i)
        {
            if (rooms == null || i < 0 || i >= rooms.Length) return null;
            return rooms[i];
        }

        /// <summary>Mühür odası tanımı.</summary>
        public ArafRoomDefinition SealRoom => sealRoom;
    }
}
