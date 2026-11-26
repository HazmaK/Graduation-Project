using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Araf oda yuvaları açıldıkça hangi oda tanımının geleceğini belirleyen saf C# seçici.
    /// Her koşu için tek bir örnek oluşturulur.
    /// </summary>
    public sealed class ArafRoomRoller
    {
        private readonly ArafRoomTable _table;
        private readonly RunRandom _rng;
        private readonly int _totalSlots;
        private readonly int[] _spawnCounts;
        private readonly float[] _weights;

        private int _revealedCount;

        /// <summary>Şu ana kadar açılan toplam oda yuvası sayısı.</summary>
        public int RevealedCount => _revealedCount;

        /// <summary>Labirentteki toplam oda yuvası sayısı.</summary>
        public int TotalSlots => _totalSlots;

        /// <summary>Verilen tablo, rastgelelik akışı ve toplam yuva sayısı ile yeni bir oda seçici kurar.</summary>
        public ArafRoomRoller(ArafRoomTable table, RunRandom rng, int totalSlots)
        {
            _table = table;
            _rng = rng ?? new RunRandom(1UL);
            _totalSlots = totalSlots > 0 ? totalSlots : 0;

            int roomCount = _table != null ? _table.RoomCount : 0;
            _spawnCounts = new int[roomCount];
            _weights = new float[roomCount];
        }

        /// <summary>Verilen yuva derinliği ve arena rolü için sıradaki oda tanımını seçer.</summary>
        public ArafRoomDefinition Roll(int slotDepth, ArafRoomRole role)
        {
            if (_table == null) return null;

            _revealedCount++;
            if (role == ArafRoomRole.SealArena)
            {
                return _table.SealRoom;
            }
            if (role == ArafRoomRole.OptionalArena) return _table.ArenaRoom;

            int roomCount = _table.RoomCount;
            if (roomCount <= 0)
            {
                return null;
            }

            for (int i = 0; i < roomCount; i++)
            {
                ArafRoomDefinition def = _table.GetRoom(i);
                if (def != null && def.MinDepth <= slotDepth && _spawnCounts[i] < def.MaxPerRun && def.Weight > 0f)
                {
                    _weights[i] = def.Weight;
                }
                else
                {
                    _weights[i] = 0f;
                }
            }

            int selectedIndex = _rng.WeightedIndex(_weights, roomCount);

            if (selectedIndex < 0)
            {
                for (int i = 0; i < roomCount; i++)
                {
                    ArafRoomDefinition def = _table.GetRoom(i);
                    _weights[i] = (def != null && def.Weight > 0f) ? def.Weight : 0f;
                }
                selectedIndex = _rng.WeightedIndex(_weights, roomCount);
            }

            if (selectedIndex < 0)
            {
                for (int i = 0; i < roomCount; i++)
                {
                    _weights[i] = _table.GetRoom(i) != null ? 1f : 0f;
                }
                selectedIndex = _rng.WeightedIndex(_weights, roomCount);
            }

            if (selectedIndex < 0)
            {
                return null;
            }

            _spawnCounts[selectedIndex]++;
            return _table.GetRoom(selectedIndex);
        }
    }
}
