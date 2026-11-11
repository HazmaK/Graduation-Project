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
        private int _nonExitReveals;
        private bool _exitSpawned;
        private float _currentExitChance;

        /// <summary>Şu ana kadar açılan toplam oda yuvası sayısı.</summary>
        public int RevealedCount => _revealedCount;

        /// <summary>Labirentteki toplam oda yuvası sayısı.</summary>
        public int TotalSlots => _totalSlots;

        /// <summary>Çıkış odasının açılıp açılmadığı.</summary>
        public bool ExitSpawned => _exitSpawned;

        /// <summary>Derinlik şartı sağlandığında bir sonraki açılışta çıkış odasının gelme olasılığı.</summary>
        public float CurrentExitChance => _currentExitChance;

        /// <summary>Verilen tablo, rastgelelik akışı ve toplam yuva sayısı ile yeni bir oda seçici kurar.</summary>
        public ArafRoomRoller(ArafRoomTable table, RunRandom rng, int totalSlots)
        {
            _table = table;
            _rng = rng ?? new RunRandom(1UL);
            _totalSlots = totalSlots > 0 ? totalSlots : 0;

            int roomCount = _table != null ? _table.RoomCount : 0;
            _spawnCounts = new int[roomCount];
            _weights = new float[roomCount];
            _currentExitChance = _table != null ? Mathf.Clamp01(_table.ExitBaseChance) : 0f;
        }

        /// <summary>Verilen yuva derinliği için sıradaki oda tanımını seçer.</summary>
        public ArafRoomDefinition Roll(int slotDepth)
        {
            if (_table == null) return null;

            _revealedCount++;
            int roomCount = _table.RoomCount;

            if (roomCount <= 0)
            {
                if (_exitSpawned) return null;
                _exitSpawned = true;
                return _table.ExitRoom;
            }

            bool isLastUnrevealedSlot = _revealedCount >= _totalSlots;
            if (!_exitSpawned && (isLastUnrevealedSlot || (slotDepth >= _table.ExitMinDepth && _rng.Chance(_currentExitChance))))
            {
                _exitSpawned = true;
                return _table.ExitRoom;
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
                    if (_table.GetRoom(i) != null)
                    {
                        _weights[i] = 1f;
                    }
                    else
                    {
                        _weights[i] = 0f;
                    }
                }
                selectedIndex = _rng.WeightedIndex(_weights, roomCount);
            }

            if (selectedIndex < 0)
            {
                if (_exitSpawned) return null;
                _exitSpawned = true;
                return _table.ExitRoom;
            }

            _spawnCounts[selectedIndex]++;
            _nonExitReveals++;

            if (!_exitSpawned)
            {
                _currentExitChance = Mathf.Min(1f, _table.ExitBaseChance + _table.ExitChancePerReveal * _nonExitReveals);
            }

            return _table.GetRoom(selectedIndex);
        }
    }
}
