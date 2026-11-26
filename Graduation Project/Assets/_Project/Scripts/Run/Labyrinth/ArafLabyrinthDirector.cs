using System;
using RetroFPS.Combat.Navigation;
using RetroFPS.Run.Araf;
using RetroFPS.Run.Cards;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf kökünde, içerik kapalıyken tohumlu labirenti, mühür yuvalarını ve merkez portalı üretir.</summary>
    [DisallowMultipleComponent]
    public class ArafLabyrinthDirector : MonoBehaviour
    {
        [SerializeField] private ArafWorldSettings world = new ArafWorldSettings();
        [SerializeField, HideInInspector] private LabyrinthSettings settings = new LabyrinthSettings();
        [SerializeField] private LabyrinthKit kit;
        [SerializeField] private ArafRoomTable roomTable;
        [SerializeField] private ArafLevel level;
        [SerializeField] private RuntimeNavMeshBaker navMeshBaker;
        [SerializeField] private CardCatalog cardCatalog;
        [SerializeField] private ArafCardAltar cardAltarPrefab;
        [SerializeField] private ArafEscapePortal hubPortalPrefab;
        [SerializeField] private ArafSealBeacon sealBeaconPrefab;
        [SerializeField] private ArafSealLightPillar sealLightPillarPrefab;
        [SerializeField] private float eliteDamage = 1.4f;
        [SerializeField] private float eliteSpeed = 1.4f;
        [SerializeField] private float eliteHealth = 1.25f;
        [SerializeField] private bool logGeneration = true;

        private ArafWorldLayout _world;
        private LabyrinthLayout _layout;
        private Transform[] _sectionRoots;
        private int[] _slotSections;
        private int[] _slotLocalIndices;
        private int _totalRooms;
        private Bounds _worldBoundsLocal;
        private Vector3 _entryPositionWorld;
        private ArafRoomRoller _roller;
        private ArafTuning _tuning;
        private ArafSealTracker _seals;
        private ArafAwakening _awakening;
        private ArafRoomSlot[] _slots;
        private RunRandom _roomsRng;
        private Func<int> _awakeningProvider;
        private int _optionalArenaCount;
        private bool[] _isSealSlot;
        private ArafSealBeacon[] _slotBeacons;
        private ArafSealLightPillar[] _slotLightPillars;
        private ArafSealRoom[] _sealRooms;
        private ArafEscapePortal _hubPortal;
        private Transform _generated;
        private bool _navMeshReady;
        private bool _subscribedContentActive;

        /// <summary>Üretilen çok bölümlü Araf dünyası; üretilmeden önce null.</summary>
        public ArafWorldLayout World => _world;
        /// <summary>Dünyadaki bölüm sayısı (merkez dahil).</summary>
        public int SectionCount => _world != null ? _world.SectionCount : 0;
        /// <summary>Belirtilen bölümün labirent yerleşimini döndürür.</summary>
        public LabyrinthLayout GetSectionLayout(int section)
            => _world != null && (uint)section < (uint)_world.SectionCount ? _world.GetSection(section).Layout : null;
        /// <summary>Belirtilen bölümün kök dönüşümünü döndürür.</summary>
        public Transform GetSectionRoot(int section)
            => _sectionRoots != null && (uint)section < (uint)_sectionRoots.Length ? _sectionRoots[section] : null;
        /// <summary>Küresel oda yuvasının ait olduğu bölüm indeksini döndürür.</summary>
        public int GetSlotSection(int globalSlot)
            => _slotSections != null && (uint)globalSlot < (uint)_slotSections.Length ? _slotSections[globalSlot] : -1;
        /// <summary>Küresel oda yuvasının kendi bölümü içindeki yerel oda indeksini döndürür.</summary>
        public int GetSlotLocalIndex(int globalSlot)
            => _slotLocalIndices != null && (uint)globalSlot < (uint)_slotLocalIndices.Length ? _slotLocalIndices[globalSlot] : -1;
        /// <summary>Yönetici yerel uzayında tüm bölüm ayak izlerini ve ada platformlarını kapsayan sınır kutusu.</summary>
        public Bounds WorldBoundsLocal => _worldBoundsLocal;
        /// <summary>Yönetici yerel uzayında boşluk yüksekliği.</summary>
        public float VoidHeightLocal => _worldBoundsLocal.min.y - 12f;
        /// <summary>Oyuncunun Araf'ta doğacağı dünya konumu.</summary>
        public Vector3 EntryPositionWorld
            => _world != null ? _entryPositionWorld : (level != null && level.EntryPoint != null ? level.EntryPoint.position : transform.position);

        /// <summary>Üretilen merkez labirent hücre yerleşimi.</summary>
        public LabyrinthLayout Layout => _layout;
        /// <summary>Üretilen labirent hiyerarşisinin kök dönüşümü.</summary>
        public Transform GeneratedRoot => _generated;
        /// <summary>Araf çalışma zamanı NavMesh'inin hazırlanıp hazırlanmadığı.</summary>
        public bool IsNavMeshReady => _navMeshReady;
        /// <summary>Tüm bölümlerdeki toplam oda yuvası sayısı.</summary>
        public int TotalRooms => _totalRooms;
        /// <summary>Toplanan mühür sayısı.</summary>
        public int SealsCollected => _seals != null ? _seals.Collected : 0;
        /// <summary>Bu koşudaki toplam mühür sayısı.</summary>
        public int SealsTotal => _seals != null ? _seals.Total : 0;
        /// <summary>Mevcut uyanış seviyesi.</summary>
        public int AwakeningLevel => _awakening != null ? _awakening.Level : 0;
        /// <summary>Etkin Araf labirent yöneticisi.</summary>
        public static ArafLabyrinthDirector Active { get; private set; }

        /// <summary>Araf çalışma zamanı NavMesh'i ilk kez hazır olduğunda yükseltilir.</summary>
        public event Action NavMeshReady;
        /// <summary>Bir mühür alındığında (yeni uyanış seviyesi, küresel mühür oda yuvası indeksi) ile yükseltilir.</summary>
        public event Action<int, int> SealTaken;

        private void Awake()
        {
            Active = this;
            _awakeningProvider = GetAwakening;
        }

        private void Start()
        {
            if (level == null) level = GetComponent<ArafLevel>();
            if (level == null || level.ContentRoot == null || kit == null || roomTable == null || world == null)
            {
                Debug.LogWarning("[ArafLabyrinthDirector] Seviye, içerik kökü, parça kiti, oda tablosu veya dünya ayarları eksik.", this);
                return;
            }

            _tuning = level.Tuning;
            int maxAwakening = 3;
            if (_tuning == null)
            {
                Debug.LogWarning("[ArafLabyrinthDirector] ArafLevel.Tuning atanmamış; varsayılan (3 uyanış) değeri kullanılıyor.", this);
            }
            else
            {
                maxAwakening = Mathf.Max(0, _tuning.MaxAwakening);
            }

            RunRandom layoutRng = RunEvents.GetStream(RunStream.ArafLayout);
            _world = ArafWorldGenerator.Generate(world, layoutRng);
            _layout = _world.Hub.Layout;

            int sectionCount = _world.SectionCount;
            _sectionRoots = new Transform[sectionCount];
            int[] sectionSlotOffsets = new int[sectionCount];
            int totalRooms = 0;
            for (int s = 0; s < sectionCount; s++)
            {
                sectionSlotOffsets[s] = totalRooms;
                totalRooms += _world.GetSection(s).Layout.RoomCount;
            }
            _totalRooms = totalRooms;

            _slotSections = new int[totalRooms];
            _slotLocalIndices = new int[totalRooms];
            for (int s = 0; s < sectionCount; s++)
            {
                int count = _world.GetSection(s).Layout.RoomCount;
                int offset = sectionSlotOffsets[s];
                for (int r = 0; r < count; r++)
                {
                    int globalSlot = offset + r;
                    _slotSections[globalSlot] = s;
                    _slotLocalIndices[globalSlot] = r;
                }
            }

            _isSealSlot = new bool[totalRooms];
            int pickedCount = 0;
            int[] sealBuffer = new int[1];
            for (int s = 0; s < sectionCount; s++)
            {
                ArafSection section = _world.GetSection(s);
                if (section.IsHub || section.Layout.RoomCount <= 0) continue;
                int picked = ArafSealSlotPicker.Pick(section.Layout, 1, layoutRng, sealBuffer);
                if (picked > 0)
                {
                    int globalSlot = sectionSlotOffsets[s] + sealBuffer[0];
                    if (globalSlot >= 0 && globalSlot < totalRooms)
                    {
                        _isSealSlot[globalSlot] = true;
                        pickedCount++;
                    }
                }
            }

            _roomsRng = RunEvents.GetStream(RunStream.ArafRooms);
            _roller = new ArafRoomRoller(roomTable, _roomsRng, totalRooms);
            _seals = new ArafSealTracker(pickedCount);
            _awakening = new ArafAwakening(maxAwakening);

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

            _slots = new ArafRoomSlot[totalRooms];
            _slotBeacons = new ArafSealBeacon[totalRooms];
            _slotLightPillars = new ArafSealLightPillar[totalRooms];
            _sealRooms = new ArafSealRoom[totalRooms];

            for (int s = 0; s < sectionCount; s++)
            {
                ArafSection section = _world.GetSection(s);
                Transform sectionRoot = new GameObject($"Section_{s}").transform;
                sectionRoot.SetParent(_generated, false);
                sectionRoot.localPosition = section.Origin;
                sectionRoot.localRotation = Quaternion.identity;
                _sectionRoots[s] = sectionRoot;

                BuildGeometry(section.Layout, sectionRoot);
                BuildSectionSlots(section.Layout, sectionRoot, sectionSlotOffsets[s]);
            }

            // ArafRooms akışının sırası: önce roller, sonra küresel yuva sırasında oda zarları.
            int[] sectionCounts = new int[sectionCount];
            bool[] sectionHubs = new bool[sectionCount];
            for (int i = 0; i < sectionCount; i++)
            {
                sectionCounts[i] = _world.GetSection(i).Layout.RoomCount;
                sectionHubs[i] = _world.GetSection(i).IsHub;
            }
            ArafRoomRole[] roles = new ArafRoomRole[totalRooms];
            ArafArenaPlanner.AssignRoles(sectionSlotOffsets, sectionCounts, sectionHubs, _isSealSlot,
                _tuning != null ? _tuning.OptionalArenasPerWing : 1, _roomsRng, roles);
            int sealArenas = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] == ArafRoomRole.OptionalArena) _optionalArenaCount++;
                else if (roles[i] == ArafRoomRole.SealArena) sealArenas++;
            }
            RunEvents.CurrentRun?.CardOffers.ConfigureVisit(_seals.Total + _optionalArenaCount);
            for (int i = 0; i < _slots.Length; i++) BuildRoom(_slots[i], roles[i]);
            if (logGeneration)
                Debug.Log($"[ArafLabyrinthDirector] Arenalar: {sealArenas} mühür, {_optionalArenaCount} isteğe bağlı; {totalRooms - sealArenas - _optionalArenaCount} sakin oda.", this);

            BuildIslands();
            ComputeWorldBoundsLocal();
            BuildHubPortal();
            MoveEntryPoint();

            RunEvents.RaiseArafSealsChanged(0, _seals.Total);
            RunEvents.RaiseArafAwakeningChanged(0);

            if (level.IsContentActive)
            {
                BakeNavMeshOnce();
            }
            else
            {
                level.ContentActiveChanged += HandleContentActiveChanged;
                _subscribedContentActive = true;
            }

            if (logGeneration)
                Debug.Log($"[ArafLabyrinthDirector] Araf dünyası hazır: {sectionCount} bölüm, {TotalRooms} oda, {SealsTotal} mühür.", this);
        }

        private static GameObject Place(GameObject prefab, Transform parent, Transform spaceRoot, Vector3 localPosition, Quaternion localRotation, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab, parent);
            instance.transform.SetPositionAndRotation(spaceRoot.TransformPoint(localPosition), spaceRoot.rotation * localRotation);
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

        private void BuildGeometry(LabyrinthLayout layout, Transform sectionRoot)
        {
            float size = layout.CellSize;
            float height = kit.WallHeight;
            float thickness = kit.WallThickness;
            Vector3 floorScale = new Vector3(layout.Width * size, 1f, layout.Height * size);
            Place(kit.FloorTile, sectionRoot, sectionRoot, Vector3.zero, Quaternion.identity, floorScale);
            Place(kit.CeilingTile, sectionRoot, sectionRoot, Vector3.up * height, Quaternion.identity, floorScale);
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    BuildWall(layout, sectionRoot, x, y, LabyrinthDir.North);
                    BuildWall(layout, sectionRoot, x, y, LabyrinthDir.East);
                    if (y == 0) BuildWall(layout, sectionRoot, x, y, LabyrinthDir.South);
                    if (x == 0) BuildWall(layout, sectionRoot, x, y, LabyrinthDir.West);
                    if (kit.Torch != null && layout.GetRoomIndex(x, y) < 0 &&
                        (x + y) % Mathf.Max(1, kit.TorchEveryNCells) == 0)
                    {
                        for (int d = 0; d < 4; d++)
                        {
                            LabyrinthDir direction = (LabyrinthDir)d;
                            if (!layout.HasWall(x, y, direction)) continue;
                            Vector3 outward = Direction(direction);
                            Vector3 position = layout.CellToLocal(x, y) + outward * (size * 0.5f - thickness * 0.5f - 0.05f);
                            position.y = 2.6f;
                            Place(kit.Torch, sectionRoot, sectionRoot, position, Quaternion.LookRotation(-outward), Vector3.one);
                            break;
                        }
                    }
                }
            }
            if (kit.Pillar == null) return;
            for (int y = 0; y <= layout.Height; y++)
            {
                for (int x = 0; x <= layout.Width; x++)
                {
                    if (!VertexHasWall(layout, x, y)) continue;
                    Vector3 position = new Vector3((x - layout.Width * 0.5f) * size, height * 0.5f, (y - layout.Height * 0.5f) * size);
                    Place(kit.Pillar, sectionRoot, sectionRoot, position, Quaternion.identity, new Vector3(thickness, height, thickness));
                }
            }
        }

        private void BuildWall(LabyrinthLayout layout, Transform sectionRoot, int x, int y, LabyrinthDir direction)
        {
            if (!layout.HasWall(x, y, direction)) return;
            Vector3 position = layout.CellToLocal(x, y) + Direction(direction) * (layout.CellSize * 0.5f);
            position.y = kit.WallHeight * 0.5f;
            float yaw = direction == LabyrinthDir.East || direction == LabyrinthDir.West ? 90f : 0f;
            Place(kit.WallSegment, sectionRoot, sectionRoot, position, Quaternion.Euler(0f, yaw, 0f),
                new Vector3(layout.CellSize + kit.WallThickness, kit.WallHeight, kit.WallThickness));
        }

        private static bool VertexHasWall(LabyrinthLayout layout, int x, int y)
        {
            // Dört komşu hücrenin köşeye değen kenarları; sınır dışına sorgu yapılmaz.
            for (int cy = y - 1; cy <= y; cy++)
            {
                for (int cx = x - 1; cx <= x; cx++)
                {
                    if (cx < 0 || cy < 0 || cx >= layout.Width || cy >= layout.Height) continue;
                    if (layout.HasWall(cx, cy, cy < y ? LabyrinthDir.North : LabyrinthDir.South) ||
                        layout.HasWall(cx, cy, cx < x ? LabyrinthDir.East : LabyrinthDir.West)) return true;
                }
            }
            return false;
        }

        private void BuildSectionSlots(LabyrinthLayout layout, Transform sectionRoot, int slotOffset)
        {
            int roomCount = layout.RoomCount;
            for (int i = 0; i < roomCount; i++)
            {
                int globalSlot = slotOffset + i;
                RoomSlotInfo info = layout.GetRoom(i);
                GameObject slotObject = new GameObject($"RoomSlot_{globalSlot}");
                slotObject.transform.SetParent(sectionRoot, false);
                slotObject.transform.SetPositionAndRotation(sectionRoot.TransformPoint(info.LocalCenter), sectionRoot.rotation);
                ArafRoomSlot slot = slotObject.AddComponent<ArafRoomSlot>();
                _slots[globalSlot] = slot;
                int count = info.Doorways.Length;
                GameObject[] barriers = new GameObject[count];
                for (int d = 0; d < count; d++)
                {
                    Doorway doorway = info.Doorways[d];
                    Vector3 middle = doorway.LocalPosition + Vector3.up * (kit.WallHeight * 0.5f);
                    barriers[d] = Place(kit.DoorBarrier, slot.transform, sectionRoot, middle, doorway.LocalRotation,
                        new Vector3(layout.CellSize, kit.WallHeight, kit.WallThickness));
                }
                slot.Initialize(globalSlot, info, barriers);

                if (_isSealSlot != null && globalSlot < _isSealSlot.Length && _isSealSlot[globalSlot])
                {
                    if (sealBeaconPrefab != null)
                    {
                        _slotBeacons[globalSlot] = Instantiate(sealBeaconPrefab, slot.transform.position, slot.transform.rotation, slot.transform);
                    }
                    if (sealLightPillarPrefab != null)
                    {
                        Vector3 pillarLocalPos = new Vector3(0f, kit.WallHeight + 0.05f, 0f);
                        ArafSealLightPillar pillar = Instantiate(sealLightPillarPrefab, slot.transform);
                        pillar.transform.SetLocalPositionAndRotation(pillarLocalPos, Quaternion.identity);
                        _slotLightPillars[globalSlot] = pillar;
                    }
                }
            }
        }

        private void BuildIslands()
        {
            Transform islandsRoot = new GameObject("Islands").transform;
            islandsRoot.SetParent(_generated, false);
            if (kit.IslandPlatform == null) return;

            float thickness = Mathf.Max(0.01f, kit.IslandThickness);
            for (int c = 0; c < _world.ChainCount; c++)
            {
                ArafIslandChain chain = _world.GetChain(c);
                for (int p = 0; p < chain.PlatformCount; p++)
                {
                    ArafPlatform platform = chain.GetPlatform(p);
                    Vector3 center = new Vector3(platform.Center.x, platform.Center.y - thickness * 0.5f, platform.Center.z);
                    Vector3 scale = new Vector3(platform.Size, thickness, platform.Size);
                    Place(kit.IslandPlatform, islandsRoot, _generated, center, Quaternion.identity, scale);
                }
            }
        }

        private void ComputeWorldBoundsLocal()
        {
            float wallHeight = kit != null ? kit.WallHeight : 5f;
            float thickness = kit != null ? Mathf.Max(0.01f, kit.IslandThickness) : 1.2f;
            bool hasBounds = false;
            Bounds bounds = default;

            for (int s = 0; s < _world.SectionCount; s++)
            {
                ArafSection section = _world.GetSection(s);
                LabyrinthLayout layout = section.Layout;
                Bounds sectionBounds = new Bounds(
                    section.Origin + Vector3.up * (wallHeight * 0.5f),
                    new Vector3(layout.Width * layout.CellSize, wallHeight, layout.Height * layout.CellSize));
                if (!hasBounds)
                {
                    bounds = sectionBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(sectionBounds);
                }
            }

            for (int c = 0; c < _world.ChainCount; c++)
            {
                ArafIslandChain chain = _world.GetChain(c);
                for (int p = 0; p < chain.PlatformCount; p++)
                {
                    ArafPlatform platform = chain.GetPlatform(p);
                    Bounds platformBounds = new Bounds(
                        new Vector3(platform.Center.x, platform.Center.y - thickness * 0.5f, platform.Center.z),
                        new Vector3(platform.Size, thickness, platform.Size));
                    if (!hasBounds)
                    {
                        bounds = platformBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(platformBounds);
                    }
                }
            }

            _worldBoundsLocal = bounds;
        }

        private void BuildHubPortal()
        {
            if (hubPortalPrefab == null) return;
            Transform hubRoot = GetSectionRoot(0);
            LabyrinthLayout hubLayout = _world.Hub.Layout;
            Quaternion facing = hubRoot.rotation;
            for (int d = 0; d < 4; d++)
            {
                if (hubLayout.HasWall(hubLayout.StartCell.X, hubLayout.StartCell.Y, (LabyrinthDir)d)) continue;
                facing = hubRoot.rotation * Quaternion.LookRotation(Direction((LabyrinthDir)d));
                break;
            }
            _hubPortal = Instantiate(hubPortalPrefab, hubRoot.TransformPoint(hubLayout.StartLocalPosition), facing, hubRoot);
            // Mühür gerektirmeyen bir ayarda (SealCount = 0) portal baştan açık olmalı.
            _hubPortal.SetSealed(_seals == null || !_seals.IsComplete);
        }

        private void MoveEntryPoint()
        {
            Transform hubRoot = GetSectionRoot(0);
            LabyrinthLayout hubLayout = _world.Hub.Layout;
            Vector3 forwardLocal = Vector3.forward;
            Quaternion facing = hubRoot.rotation;
            for (int d = 0; d < 4; d++)
            {
                if (hubLayout.HasWall(hubLayout.StartCell.X, hubLayout.StartCell.Y, (LabyrinthDir)d)) continue;
                forwardLocal = Direction((LabyrinthDir)d);
                facing = hubRoot.rotation * Quaternion.LookRotation(forwardLocal);
                break;
            }
            Vector3 entryLocal = hubLayout.StartLocalPosition + forwardLocal * 2.2f;
            _entryPositionWorld = hubRoot.TransformPoint(entryLocal) + Vector3.up * 0.1f;
            if (level.EntryPoint == null) return;
            level.EntryPoint.SetPositionAndRotation(_entryPositionWorld, facing);
        }

        private int GetAwakening() => AwakeningLevel;

        private void BuildRoom(ArafRoomSlot slot, ArafRoomRole role)
        {
            ArafRoomDefinition definition = _roller.Roll(slot.Info.Depth, role);
            ArafArena arena = null;
            ArafRoom room = null;
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning("[ArafLabyrinthDirector] Seçilen oda veya prefab eksik; yuva boş açıldı.", this);
            }
            else
            {
                GameObject instance = Instantiate(definition.Prefab, slot.transform.position, slot.transform.rotation, slot.transform);
                room = instance.GetComponent<ArafRoom>();
                if (role != ArafRoomRole.Calm)
                {
                    arena = instance.GetComponent<ArafArena>();
                    if (arena != null)
                    {
                        float eyeHeight = Mathf.Min(_tuning != null ? _tuning.EyeHoverHeight : 3f, kit.WallHeight - 1.5f);
                        arena.Configure(role, roomTable.ArenaWaves, _awakeningProvider,
                            eliteDamage, eliteSpeed, eliteHealth, _roomsRng, eyeHeight);
                        arena.OnStarted += HandleArenaStarted;
                        arena.OnCleared += HandleArenaCleared;
                    }
                    else Debug.LogWarning("[ArafLabyrinthDirector] Arena prefabında ArafArena eksik.", instance);
                }
                ArafSealRoom sealRoom = instance.GetComponent<ArafSealRoom>();
                if (sealRoom != null)
                {
                    if (_sealRooms != null && slot.SlotIndex >= 0 && slot.SlotIndex < _sealRooms.Length)
                    {
                        _sealRooms[slot.SlotIndex] = sealRoom;
                    }
                    sealRoom.OnSealTaken += HandleSealTaken;
                }
                if (room == null)
                    Debug.LogWarning("[ArafLabyrinthDirector] Oda prefab kökünde ArafRoom eksik.", instance);
                else
                {
                    room.Activate(definition, eliteDamage, eliteSpeed, eliteHealth);
                }
            }
            slot.SetRoom(room, arena, role);
            if (logGeneration)
            {
                string roomName = definition != null ? definition.DisplayName : "Boş oda";
                Debug.Log($"[ArafLabyrinthDirector] Oda seçildi: {roomName}; derinlik: {slot.Info.Depth}.", this);
            }
        }

        private void HandleArenaStarted(ArafArena arena)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].Arena == arena) _slots[i].SetBarriersActive(true);
        }

        private void HandleArenaCleared(ArafArena arena)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].Arena == arena) _slots[i].SetBarriersActive(false);
            if (arena.Role == ArafRoomRole.OptionalArena)
                SpawnAltar(arena.AltarAnchor != null ? arena.AltarAnchor : arena.transform,
                    _tuning != null ? _tuning.GetArenaRarity(AwakeningLevel) : CardRarityMultipliers.One);
        }

        private void SpawnAltar(Transform anchor, CardRarityMultipliers rarity)
        {
            RunState run = RunEvents.CurrentRun;
            if (run == null || cardCatalog == null || cardAltarPrefab == null || anchor == null) return;
            run.CardOffers.ConfigureVisit(_seals.Total + _optionalArenaCount);
            if (!run.CardOffers.TryRoll(cardCatalog, run.Cards, rarity, out CardOffer offer)) return;
            ArafCardAltar altar = Instantiate(cardAltarPrefab, anchor.position, anchor.rotation, anchor);
            altar.Initialize(offer);
        }

        private void HandleSealTaken(ArafSealRoom sealRoom)
        {
            if (sealRoom != null)
            {
                sealRoom.OnSealTaken -= HandleSealTaken;
            }
            if (_seals == null || !_seals.TryCollect(out int idx)) return;

            int sealSlotIndex = -1;
            if (_sealRooms != null)
            {
                for (int i = 0; i < _sealRooms.Length; i++)
                {
                    if (!ReferenceEquals(_sealRooms[i], sealRoom)) continue;
                    sealSlotIndex = i;
                    if (_slotBeacons != null && i < _slotBeacons.Length && _slotBeacons[i] != null)
                    {
                        _slotBeacons[i].Silence();
                    }
                    if (_slotLightPillars != null && i < _slotLightPillars.Length && _slotLightPillars[i] != null)
                    {
                        _slotLightPillars[i].Extinguish();
                    }
                    break;
                }
            }

            if (_awakening != null && _awakening.Raise())
            {
                RunEvents.RaiseArafAwakeningChanged(_awakening.Level);
            }

            SealTaken?.Invoke(AwakeningLevel, sealSlotIndex);

            if (sealRoom != null)
                SpawnAltar(sealRoom.AltarAnchor != null ? sealRoom.AltarAnchor : sealRoom.transform,
                    _tuning != null ? _tuning.GetSealRarity(idx) : CardRarityMultipliers.One);

            RunEvents.RaiseArafSealsChanged(_seals.Collected, _seals.Total);
            if (_seals.IsComplete && _hubPortal != null)
            {
                _hubPortal.SetSealed(false);
            }
        }

        private void HandleContentActiveChanged(bool active)
        {
            if (!active) return;
            BakeNavMeshOnce();
        }

        private void BakeNavMeshOnce()
        {
            if (_navMeshReady || _generated == null) return;
            if (_subscribedContentActive && level != null)
            {
                level.ContentActiveChanged -= HandleContentActiveChanged;
                _subscribedContentActive = false;
            }
            if (navMeshBaker != null)
            {
                navMeshBaker.Bake(_generated);
            }
            // NavMesh olmasa da devriyeler doğar (düşmanlar düz takibe düşer); sessiz kalmasın diye uyar.
            if (navMeshBaker == null || !navMeshBaker.HasData)
                Debug.LogWarning("[ArafLabyrinthDirector] Araf NavMesh'i üretilemedi; düşmanlar düz takip moduna düşecek.", this);
            _navMeshReady = true;
            NavMeshReady?.Invoke();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_subscribedContentActive && level != null)
            {
                level.ContentActiveChanged -= HandleContentActiveChanged;
                _subscribedContentActive = false;
            }
            if (_slots != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] == null) continue;
                    if (_slots[i].Arena != null)
                    {
                        _slots[i].Arena.OnStarted -= HandleArenaStarted;
                        _slots[i].Arena.OnCleared -= HandleArenaCleared;
                    }
                }
            }
            if (_sealRooms != null)
            {
                for (int i = 0; i < _sealRooms.Length; i++)
                {
                    if (_sealRooms[i] != null) _sealRooms[i].OnSealTaken -= HandleSealTaken;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_world == null && (world == null || world.Hub == null)) return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            float wallHeight = kit != null ? kit.WallHeight : 5f;
            if (_world != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(_worldBoundsLocal.center, _worldBoundsLocal.size);
                for (int s = 0; s < _world.SectionCount; s++)
                {
                    ArafSection sec = _world.GetSection(s);
                    LabyrinthLayout lay = sec.Layout;
                    Gizmos.color = sec.IsHub ? Color.green : Color.cyan;
                    Gizmos.DrawWireCube(sec.Origin + Vector3.up * (wallHeight * 0.5f),
                        new Vector3(lay.Width * lay.CellSize, wallHeight, lay.Height * lay.CellSize));
                }
            }
            else
            {
                LabyrinthSettings hub = world.Hub;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(Vector3.up * (wallHeight * 0.5f),
                    new Vector3(hub.Width * hub.CellSize, wallHeight, hub.Height * hub.CellSize));
            }
            Gizmos.matrix = previous;
        }
    }
}
