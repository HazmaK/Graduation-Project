using System;
using System.Collections;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf kökünde, içerik kapalıyken tohumlu labirenti ve gizli oda yuvalarını üretir.</summary>
    [DisallowMultipleComponent]
    public class ArafLabyrinthDirector : MonoBehaviour
    {
        [SerializeField] private LabyrinthSettings settings = new LabyrinthSettings();
        [SerializeField] private LabyrinthKit kit;
        [SerializeField] private ArafRoomTable roomTable;
        [SerializeField] private ArafLevel level;
        [SerializeField] private float eliteDamage = 1.4f;
        [SerializeField] private float eliteSpeed = 1.4f;
        [SerializeField] private float eliteHealth = 1.25f;
        [SerializeField] private bool logGeneration = true;

        private LabyrinthLayout _layout;
        private ArafRoomRoller _roller;
        private ArafRoomSlot[] _slots;
        private ArafRoomSlot[] _pendingReveals;
        private int _pendingRead;
        private int _pendingWrite;
        private Transform _generated;
        private int _revealedRooms;
        private bool _exitRevealed;

        /// <summary>Üretilen hücre yerleşimi.</summary>
        public LabyrinthLayout Layout => _layout;
        /// <summary>Açılmış oda yuvalarının sayısı.</summary>
        public int RevealedRooms => _revealedRooms;
        /// <summary>Toplam oda yuvası sayısı.</summary>
        public int TotalRooms => _layout != null ? _layout.RoomCount : 0;
        /// <summary>Kaçış odasının bulunup bulunmadığı.</summary>
        public bool ExitRevealed => _exitRevealed;
        /// <summary>Sıradaki uygun yuvanın kaçış odası olma ihtimali.</summary>
        public float CurrentExitChance => _roller != null ? _roller.CurrentExitChance : 0f;
        /// <summary>Etkin Araf labirent yöneticisi.</summary>
        public static ArafLabyrinthDirector Active { get; private set; }
        /// <summary>Oda keşif sayıları değiştiğinde yükseltilir.</summary>
        public event Action OnRoomsChanged;

        private void Awake()
        {
            Active = this;
        }

        private void Start()
        {
            if (level == null) level = GetComponent<ArafLevel>();
            if (level == null || level.ContentRoot == null || kit == null || roomTable == null)
            {
                Debug.LogWarning("[ArafLabyrinthDirector] Seviye, içerik kökü, parça kiti veya oda tablosu eksik.", this);
                return;
            }

            _layout = LabyrinthGenerator.Generate(settings, RunEvents.GetStream(RunStream.ArafLayout));
            _roller = new ArafRoomRoller(roomTable, RunEvents.GetStream(RunStream.ArafRooms), _layout.RoomCount);
            Transform parent = level.ContentRoot.transform;
            Transform previous = parent.Find("Labyrinth_Generated");
            if (previous != null)
            {
                previous.gameObject.SetActive(false);
                Destroy(previous.gameObject);
            }
            _generated = new GameObject("Labyrinth_Generated").transform;
            _generated.SetParent(parent, false);
            _generated.SetPositionAndRotation(transform.position, transform.rotation);
            BuildGeometry();
            BuildSlots();
            MoveEntryPoint();
            PublishRoomsChanged();
            StartCoroutine(ProcessReveals());
            if (logGeneration)
                Debug.Log($"[ArafLabyrinthDirector] Labirent hazır: {_layout.Width}x{_layout.Height}, {TotalRooms} oda.", this);
        }

        private GameObject Place(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab, parent);
            instance.transform.SetPositionAndRotation(transform.TransformPoint(localPosition), transform.rotation * localRotation);
            instance.transform.localScale = scale;
            return instance;
        }

        private static Vector3 Direction(LabyrinthDir direction)
        {
            switch (direction)
            {
                case LabyrinthDir.East: return Vector3.right;
                case LabyrinthDir.South: return Vector3.back;
                case LabyrinthDir.West: return Vector3.left;
                default: return Vector3.forward;
            }
        }

        private void BuildGeometry()
        {
            float size = _layout.CellSize;
            float height = kit.WallHeight;
            float thickness = kit.WallThickness;
            Vector3 floorScale = new Vector3(_layout.Width * size, 1f, _layout.Height * size);
            Place(kit.FloorTile, _generated, Vector3.zero, Quaternion.identity, floorScale);
            Place(kit.CeilingTile, _generated, Vector3.up * height, Quaternion.identity, floorScale);
            for (int y = 0; y < _layout.Height; y++)
            {
                for (int x = 0; x < _layout.Width; x++)
                {
                    BuildWall(x, y, LabyrinthDir.North);
                    BuildWall(x, y, LabyrinthDir.East);
                    if (y == 0) BuildWall(x, y, LabyrinthDir.South);
                    if (x == 0) BuildWall(x, y, LabyrinthDir.West);
                    if (kit.Torch != null && _layout.GetRoomIndex(x, y) < 0 &&
                        (x + y) % Mathf.Max(1, kit.TorchEveryNCells) == 0)
                    {
                        for (int d = 0; d < 4; d++)
                        {
                            LabyrinthDir direction = (LabyrinthDir)d;
                            if (!_layout.HasWall(x, y, direction)) continue;
                            Vector3 outward = Direction(direction);
                            Vector3 position = _layout.CellToLocal(x, y) + outward * (size * 0.5f - thickness * 0.5f - 0.05f);
                            position.y = 2.6f;
                            Place(kit.Torch, _generated, position, Quaternion.LookRotation(-outward), Vector3.one);
                            break;
                        }
                    }
                }
            }
            if (kit.Pillar == null) return;
            for (int y = 0; y <= _layout.Height; y++)
            {
                for (int x = 0; x <= _layout.Width; x++)
                {
                    if (!VertexHasWall(x, y)) continue;
                    Vector3 position = new Vector3((x - _layout.Width * 0.5f) * size, height * 0.5f, (y - _layout.Height * 0.5f) * size);
                    Place(kit.Pillar, _generated, position, Quaternion.identity, new Vector3(thickness, height, thickness));
                }
            }
        }

        private void BuildWall(int x, int y, LabyrinthDir direction)
        {
            if (!_layout.HasWall(x, y, direction)) return;
            Vector3 position = _layout.CellToLocal(x, y) + Direction(direction) * (_layout.CellSize * 0.5f);
            position.y = kit.WallHeight * 0.5f;
            float yaw = direction == LabyrinthDir.East || direction == LabyrinthDir.West ? 90f : 0f;
            Place(kit.WallSegment, _generated, position, Quaternion.Euler(0f, yaw, 0f),
                new Vector3(_layout.CellSize + kit.WallThickness, kit.WallHeight, kit.WallThickness));
        }

        private bool VertexHasWall(int x, int y)
        {
            // Dört komşu hücrenin köşeye değen kenarları; sınır dışına sorgu yapılmaz.
            for (int cy = y - 1; cy <= y; cy++)
            {
                for (int cx = x - 1; cx <= x; cx++)
                {
                    if (cx < 0 || cy < 0 || cx >= _layout.Width || cy >= _layout.Height) continue;
                    if (_layout.HasWall(cx, cy, cy < y ? LabyrinthDir.North : LabyrinthDir.South) ||
                        _layout.HasWall(cx, cy, cx < x ? LabyrinthDir.East : LabyrinthDir.West)) return true;
                }
            }
            return false;
        }

        private void BuildSlots()
        {
            _slots = new ArafRoomSlot[TotalRooms];
            _pendingReveals = new ArafRoomSlot[TotalRooms];
            for (int i = 0; i < _slots.Length; i++)
            {
                RoomSlotInfo info = _layout.GetRoom(i);
                GameObject slotObject = new GameObject($"RoomSlot_{i}");
                slotObject.transform.SetParent(_generated, false);
                slotObject.transform.SetPositionAndRotation(transform.TransformPoint(info.LocalCenter), transform.rotation);
                ArafRoomSlot slot = slotObject.AddComponent<ArafRoomSlot>();
                _slots[i] = slot;
                int count = info.Doorways.Length;
                GameObject[] veils = new GameObject[count];
                Collider[] triggers = new Collider[count];
                GameObject[] barriers = new GameObject[count];
                for (int d = 0; d < count; d++)
                {
                    Doorway doorway = info.Doorways[d];
                    Vector3 middle = doorway.LocalPosition + Vector3.up * (kit.WallHeight * 0.5f);
                    veils[d] = Place(kit.Veil, slot.transform, middle, doorway.LocalRotation, new Vector3(_layout.CellSize, kit.WallHeight, 1f));
                    barriers[d] = Place(kit.DoorBarrier, slot.transform, middle, doorway.LocalRotation,
                        new Vector3(_layout.CellSize, kit.WallHeight, kit.WallThickness));
                    if (barriers[d] != null) barriers[d].SetActive(false);
                    GameObject triggerObject = new GameObject($"DoorwayTrigger_{d}");
                    triggerObject.transform.SetParent(slot.transform, false);
                    Vector3 inward = middle - doorway.LocalRotation * Vector3.forward * 2f;
                    triggerObject.transform.SetPositionAndRotation(transform.TransformPoint(inward), transform.rotation * doorway.LocalRotation);
                    BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(_layout.CellSize * 0.9f, kit.WallHeight, 1.5f);
                    triggers[d] = trigger;
                    triggerObject.AddComponent<ArafRoomSlotTrigger>();
                }
                slot.Initialize(i, info, veils, triggers, barriers);
                slot.OnPlayerEnteredDoorway += QueueReveal;
            }
        }

        private void MoveEntryPoint()
        {
            if (level.EntryPoint == null) return;
            Quaternion facing = transform.rotation;
            for (int d = 0; d < 4; d++)
            {
                if (_layout.HasWall(_layout.StartCell.X, _layout.StartCell.Y, (LabyrinthDir)d)) continue;
                facing = transform.rotation * Quaternion.LookRotation(Direction((LabyrinthDir)d));
                break;
            }
            level.EntryPoint.SetPositionAndRotation(transform.TransformPoint(_layout.StartLocalPosition) + Vector3.up * 0.1f, facing);
        }

        private void QueueReveal(ArafRoomSlot slot)
        {
            if (slot.IsRevealed || _pendingWrite >= _pendingReveals.Length) return;
            _pendingReveals[_pendingWrite++] = slot;
        }

        private IEnumerator ProcessReveals()
        {
            // Tetik geri çağrısı yalnızca kuyruğa yazar; örnekleme ve metin üretimi burada yapılır.
            while (true)
            {
                while (_pendingRead < _pendingWrite)
                {
                    ArafRoomSlot slot = _pendingReveals[_pendingRead++];
                    if (slot != null && !slot.IsRevealed) Reveal(slot);
                }
                yield return null;
            }
        }

        private void Reveal(ArafRoomSlot slot)
        {
            float chance = CurrentExitChance;
            ArafRoomDefinition definition = _roller.Roll(slot.Info.Depth);
            ArafRoom room = null;
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("[ArafLabyrinthDirector] Seçilen oda veya prefab eksik; yuva boş açıldı.", this);
            }
            else
            {
                GameObject instance = Instantiate(definition.Prefab, slot.transform.position, Quaternion.identity, slot.transform);
                room = instance.GetComponent<ArafRoom>();
                if (room == null)
                    Debug.LogWarning("[ArafLabyrinthDirector] Oda prefab kökünde ArafRoom eksik.", instance);
                else
                {
                    room.OnCleared += HandleRoomCleared;
                    room.Activate(definition, eliteDamage, eliteSpeed, eliteHealth);
                }
            }
            slot.MarkRevealed(room);
            slot.SetBarriersActive(room != null && definition.SealDoorsUntilCleared && room.TotalEnemyCount > 0 && !room.IsCleared);
            _revealedRooms++;
            if (room != null && definition.Kind == ArafRoomKind.Exit) _exitRevealed = true;
            PublishRoomsChanged();
            if (logGeneration)
            {
                string roomName = definition != null ? definition.DisplayName : "Boş oda";
                Debug.Log($"[ArafLabyrinthDirector] Oda seçildi: {roomName}; derinlik: {slot.Info.Depth}; çıkış ihtimali: {chance:P0}.", this);
            }
        }

        private void HandleRoomCleared(ArafRoom room)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].Room == room) _slots[i].SetBarriersActive(false);
        }

        private void PublishRoomsChanged()
        {
            OnRoomsChanged?.Invoke();
            RunEvents.RaiseArafRoomsChanged(_revealedRooms, TotalRooms, _exitRevealed);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                _slots[i].OnPlayerEnteredDoorway -= QueueReveal;
                if (_slots[i].Room != null) _slots[i].Room.OnCleared -= HandleRoomCleared;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_layout == null && settings == null) return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            float size = _layout != null ? _layout.CellSize : settings.CellSize;
            int width = _layout != null ? _layout.Width : settings.Width;
            int height = _layout != null ? _layout.Height : settings.Height;
            float wallHeight = kit != null ? kit.WallHeight : 5f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.up * wallHeight * 0.5f, new Vector3(width * size, wallHeight, height * size));
            if (_layout != null)
            {
                Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.35f);
                for (int i = 0; i < _layout.RoomCount; i++)
                    Gizmos.DrawCube(_layout.GetRoom(i).LocalCenter + Vector3.up * 0.1f, new Vector3(size * 2f, 0.2f, size * 2f));
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_layout.StartLocalPosition + Vector3.up * 0.2f, new Vector3(size, 0.4f, size));
            }
            Gizmos.matrix = previous;
        }
    }
}
