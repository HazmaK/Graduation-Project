using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Üretilmiş 2x2 oda yuvasının konumu, derinliği ve kapıları.</summary>
    public sealed class RoomSlotInfo
    {
        /// <summary>Odanın sıfır tabanlı yuva indeksi.</summary>
        public int Index { get; }
        /// <summary>2x2 bloğun en küçük koordinatlı hücresi.</summary>
        public GridPos Min { get; }
        /// <summary>Başlangıçtan odanın en yakın hücresine BFS uzaklığı.</summary>
        public int Depth { get; }
        /// <summary>2x2 bloğun labirent yerel uzayındaki merkezi.</summary>
        public Vector3 LocalCenter { get; }
        /// <summary>Odadan koridora açılan kapılar.</summary>
        public Doorway[] Doorways { get; }

        internal RoomSlotInfo(int index, GridPos min, int depth, Vector3 localCenter, Doorway[] doorways)
        {
            Index = index;
            Min = min;
            Depth = depth;
            LocalCenter = localCenter;
            Doorways = doorways;
        }
    }

    /// <summary>Düz dizilerde tutulan labirent topolojisi ve yerel koordinat dönüşümü.</summary>
    public sealed class LabyrinthLayout
    {
        private readonly byte[] walls;
        private readonly int[] roomIndices;
        private readonly int[] depths;
        private readonly RoomSlotInfo[] rooms;

        /// <summary>Izgaranın genişliği.</summary>
        public int Width { get; }
        /// <summary>Izgaranın yüksekliği.</summary>
        public int Height { get; }
        /// <summary>Hücre kenar uzunluğu.</summary>
        public float CellSize { get; }
        /// <summary>Her zaman koridorda bulunan başlangıç hücresi.</summary>
        public GridPos StartCell { get; }
        /// <summary>Gerçekte yerleştirilebilen oda sayısı.</summary>
        public int RoomCount => rooms.Length;
        /// <summary>Başlangıç hücresinin yerel konumu.</summary>
        public Vector3 StartLocalPosition => CellToLocal(StartCell.X, StartCell.Y);

        internal LabyrinthLayout(int width, int height, float cellSize, GridPos startCell,
            byte[] walls, int[] roomIndices, int[] depths, RoomSlotInfo[] rooms)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            StartCell = startCell;
            this.walls = walls;
            this.roomIndices = roomIndices;
            this.depths = depths;
            this.rooms = rooms;
        }

        /// <summary>Belirtilen oda yuvasını döndürür.</summary>
        public RoomSlotInfo GetRoom(int index) => rooms[index];

        /// <summary>Hücrenin oda indeksini, koridor için -1 döndürür.</summary>
        public int GetRoomIndex(int x, int y) => roomIndices[CellIndex(x, y)];

        /// <summary>Hücrenin belirtilen kenarında duvar olup olmadığını döndürür.</summary>
        public bool HasWall(int x, int y, LabyrinthDir dir)
        {
            int index = CellIndex(x, y);
            if ((uint)dir > 3u) throw new ArgumentOutOfRangeException(nameof(dir));
            return (walls[index] & (1 << (int)dir)) != 0;
        }

        /// <summary>Başlangıçtan açık kenarlar üzerinden en kısa hücre uzaklığı.</summary>
        public int GetDepth(int x, int y) => depths[CellIndex(x, y)];

        /// <summary>Hücre merkezini ortalanmış labirent yerel uzayına dönüştürür.</summary>
        public Vector3 CellToLocal(int x, int y)
            => new Vector3((x - (Width - 1) / 2f) * CellSize, 0f,
                (y - (Height - 1) / 2f) * CellSize);

        private int CellIndex(int x, int y)
        {
            if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
            if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
            return y * Width + x;
        }
    }
}
