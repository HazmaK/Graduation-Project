using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf labirenti için olası oda havuzunu ve çıkış odası olasılık kurallarını barındırır.</summary>
    [CreateAssetMenu(fileName = "ArafRoomTable_New", menuName = "RetroFPS/Araf/Room Table", order = 31)]
    public class ArafRoomTable : ScriptableObject
    {
        [SerializeField] private ArafRoomDefinition[] rooms;
        [SerializeField] private ArafRoomDefinition exitRoom;
        [SerializeField, Range(0f, 1f)] private float exitBaseChance = 0.10f;
        [SerializeField, Range(0f, 1f)] private float exitChancePerReveal = 0.15f;
        [SerializeField, Min(0)] private int exitMinDepth = 3;

        /// <summary>Normal (çıkış dışı) oda tanımlarının sayısı.</summary>
        public int RoomCount => rooms != null ? rooms.Length : 0;

        /// <summary>Verilen dizindeki normal oda tanımını döndürür.</summary>
        public ArafRoomDefinition GetRoom(int i)
        {
            if (rooms == null || i < 0 || i >= rooms.Length) return null;
            return rooms[i];
        }

        /// <summary>Kaçış portalını içeren çıkış odası tanımı.</summary>
        public ArafRoomDefinition ExitRoom => exitRoom;

        /// <summary>Derinlik şartı sağlandığında çıkış odasının başlangıç gelme olasılığı.</summary>
        public float ExitBaseChance => exitBaseChance;

        /// <summary>Çıkış olmayan her oda açılışında çıkış olasılığına eklenen artış miktarı.</summary>
        public float ExitChancePerReveal => exitChancePerReveal;

        /// <summary>Son yuva haricinde çıkış odasının belirebileceği en düşük derinlik.</summary>
        public int ExitMinDepth => exitMinDepth;

        private void OnValidate()
        {
            exitBaseChance = Mathf.Clamp01(exitBaseChance);
            exitChancePerReveal = Mathf.Clamp01(exitChancePerReveal);
            if (exitMinDepth < 0) exitMinDepth = 0;
        }
    }
}
