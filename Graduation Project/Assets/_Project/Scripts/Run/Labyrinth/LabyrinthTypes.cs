using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Izgarada sırasıyla +Z, +X, -Z ve -X yönleri.</summary>
    public enum LabyrinthDir
    {
        /// <summary>+Z yönü.</summary>
        North = 0,
        /// <summary>+X yönü.</summary>
        East = 1,
        /// <summary>-Z yönü.</summary>
        South = 2,
        /// <summary>-X yönü.</summary>
        West = 3
    }

    /// <summary>Labirent ızgarasındaki değişmez hücre koordinatı.</summary>
    [Serializable]
    public readonly struct GridPos
    {
        /// <summary>Yatay hücre koordinatı.</summary>
        public readonly int X;
        /// <summary>Derinlik hücre koordinatı.</summary>
        public readonly int Y;

        /// <summary>Hücre koordinatını oluşturur.</summary>
        public GridPos(int x, int y) { X = x; Y = y; }
    }

    /// <summary>Bir koşunun labirent üretim ayarları; üretim sırasında doğrulanır.</summary>
    [Serializable]
    public sealed class LabyrinthSettings
    {
        [SerializeField] private int width = 9;
        [SerializeField] private int height = 9;
        [SerializeField] private float cellSize = 6f;
        [SerializeField] private int roomSlotCount = 7;
        [SerializeField] private float braidChance = 0.15f;
        [SerializeField] private int minRoomGap = 1;
        [SerializeField] private int doorwaysPerRoomMax = 2;

        /// <summary>Izgaranın hücre cinsinden genişliği.</summary>
        public int Width => width;
        /// <summary>Izgaranın hücre cinsinden yüksekliği.</summary>
        public int Height => height;
        /// <summary>Bir hücrenin metre cinsinden kenarı.</summary>
        public float CellSize => cellSize;
        /// <summary>Yerleştirilmeye çalışılacak 2x2 oda sayısı.</summary>
        public int RoomSlotCount => roomSlotCount;
        /// <summary>Bir koridor çıkmazına ek geçit açılma olasılığı.</summary>
        public float BraidChance => braidChance;
        /// <summary>Oda blokları arasında bırakılan en az boş hücre sayısı.</summary>
        public int MinRoomGap => minRoomGap;
        /// <summary>Bir odanın en fazla kapı sayısı.</summary>
        public int DoorwaysPerRoomMax => doorwaysPerRoomMax;

        /// <summary>Varsayılan üretim ayarlarını oluşturur.</summary>
        public LabyrinthSettings() { }

        /// <summary>Belirtilen üretim ayarlarını oluşturur.</summary>
        public LabyrinthSettings(int width, int height, float cellSize, int roomSlotCount,
            float braidChance, int minRoomGap, int doorwaysPerRoomMax)
        {
            this.width = width;
            this.height = height;
            this.cellSize = cellSize;
            this.roomSlotCount = roomSlotCount;
            this.braidChance = braidChance;
            this.minRoomGap = minRoomGap;
            this.doorwaysPerRoomMax = doorwaysPerRoomMax;
        }
    }

    /// <summary>Odadan koridora açılan kenarın konumu ve dışa bakan dönüşü.</summary>
    public readonly struct Doorway
    {
        /// <summary>Kapının ait olduğu oda hücresi.</summary>
        public readonly GridPos Inside;
        /// <summary>Oda hücresinden koridora doğru yön.</summary>
        public readonly LabyrinthDir Dir;
        /// <summary>Paylaşılan kenarın labirent yerel uzayındaki merkezi.</summary>
        public readonly Vector3 LocalPosition;
        /// <summary>İleri ekseni odadan dışarı bakan dönüş.</summary>
        public readonly Quaternion LocalRotation;

        /// <summary>Bir kapının değişmez geometrisini oluşturur.</summary>
        public Doorway(GridPos inside, LabyrinthDir dir, Vector3 localPosition, Quaternion localRotation)
        {
            Inside = inside;
            Dir = dir;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
        }
    }
}
