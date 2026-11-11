using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Tohumlu labirentin topoloji ve geometri sözleşme testleri.</summary>
    public sealed class LabyrinthGeneratorTests
    {
        private const int SeedCount = 50;

        /// <summary>Aynı ayarlar ve aynı akış durumu bütün çıktıyı tekrarlar.</summary>
        [TestCase(9, 9, 7)]
        [TestCase(5, 5, 2)]
        [TestCase(12, 12, 10)]
        public void SameSeedProducesIdenticalLayout(int width, int height, int rooms)
        {
            LabyrinthSettings settings = Settings(width, height, rooms);
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                var firstRng = new RunRandom(seed);
                var secondRng = new RunRandom(seed);
                // Üreteç yalnızca ilk tohuma değil, mevcut akış durumuna bağlıdır.
                firstRng.NextUInt();
                secondRng.NextUInt();
                LabyrinthLayout first = LabyrinthGenerator.Generate(settings, firstRng);
                LabyrinthLayout second = LabyrinthGenerator.Generate(settings, secondRng);
                Assert.That(second.Width, Is.EqualTo(first.Width));
                Assert.That(second.Height, Is.EqualTo(first.Height));
                Assert.That(second.CellSize, Is.EqualTo(first.CellSize));
                Assert.That(second.StartCell, Is.EqualTo(first.StartCell));
                Assert.That(second.RoomCount, Is.EqualTo(first.RoomCount));
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Assert.That(second.GetRoomIndex(x, y), Is.EqualTo(first.GetRoomIndex(x, y)));
                    Assert.That(second.GetDepth(x, y), Is.EqualTo(first.GetDepth(x, y)));
                    for (int dir = 0; dir < 4; dir++)
                        Assert.That(second.HasWall(x, y, (LabyrinthDir)dir),
                            Is.EqualTo(first.HasWall(x, y, (LabyrinthDir)dir)));
                }
                for (int room = 0; room < first.RoomCount; room++)
                {
                    RoomSlotInfo left = first.GetRoom(room);
                    RoomSlotInfo right = second.GetRoom(room);
                    Assert.That(right.Index, Is.EqualTo(left.Index));
                    Assert.That(right.Min, Is.EqualTo(left.Min));
                    Assert.That(right.Depth, Is.EqualTo(left.Depth));
                    Assert.That(right.LocalCenter, Is.EqualTo(left.LocalCenter));
                    Assert.That(right.Doorways.Length, Is.EqualTo(left.Doorways.Length));
                    for (int i = 0; i < left.Doorways.Length; i++)
                    {
                        Assert.That(right.Doorways[i].Inside, Is.EqualTo(left.Doorways[i].Inside));
                        Assert.That(right.Doorways[i].Dir, Is.EqualTo(left.Doorways[i].Dir));
                        Assert.That(right.Doorways[i].LocalPosition, Is.EqualTo(left.Doorways[i].LocalPosition));
                        Assert.That(right.Doorways[i].LocalRotation, Is.EqualTo(left.Doorways[i].LocalRotation));
                    }
                }
                Assert.That(secondRng.NextUInt(), Is.EqualTo(firstRng.NextUInt()));
            }
        }

        /// <summary>Ardışık farklı tohumlar farklı duvar topolojileri üretir.</summary>
        [TestCase(9, 9, 7)]
        [TestCase(5, 5, 2)]
        [TestCase(12, 12, 10)]
        public void DifferentSeedsProduceDifferentWalls(int width, int height, int rooms)
        {
            LabyrinthSettings settings = Settings(width, height, rooms);
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                LabyrinthLayout first = LabyrinthGenerator.Generate(settings, new RunRandom(seed));
                LabyrinthLayout second = LabyrinthGenerator.Generate(settings, new RunRandom(seed + 1));
                bool differs = false;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                for (int dir = 0; dir < 4; dir++)
                    differs |= first.HasWall(x, y, (LabyrinthDir)dir) != second.HasWall(x, y, (LabyrinthDir)dir);
                Assert.That(differs, Is.True, "Farklı tohumların duvarları aynı kaldı.");
            }
        }

        /// <summary>Bütün hücreler erişilir ve kaydedilen uzaklıklar bağımsız BFS ile aynıdır.</summary>
        [TestCase(9, 9, 7)]
        [TestCase(5, 5, 2)]
        [TestCase(12, 12, 10)]
        public void AllCellsAreReachableAndDepthsMatchBfs(int width, int height, int rooms)
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
                AssertReachable(LabyrinthGenerator.Generate(Settings(width, height, rooms), new RunRandom(seed)));
        }

        /// <summary>Paylaşılan duvarlar simetriktir ve bütün dış sınırlar kapalıdır.</summary>
        [TestCase(9, 9, 7)]
        [TestCase(5, 5, 2)]
        [TestCase(12, 12, 10)]
        public void WallsAreSymmetricAndBoundariesClosed(int width, int height, int rooms)
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                LabyrinthLayout layout = LabyrinthGenerator.Generate(Settings(width, height, rooms), new RunRandom(seed));
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                for (int dir = 0; dir < 4; dir++)
                {
                    GridPos next = Next(new GridPos(x, y), dir);
                    if (!InBounds(layout, next)) Assert.That(layout.HasWall(x, y, (LabyrinthDir)dir), Is.True);
                    else Assert.That(layout.HasWall(x, y, (LabyrinthDir)dir),
                        Is.EqualTo(layout.HasWall(next.X, next.Y, (LabyrinthDir)((dir + 2) % 4))));
                }
            }
        }

        /// <summary>2x2 bloklar ayrıdır, boşluk kuralını korur ve başlangıcı kapatmaz.</summary>
        [TestCase(9, 9, 7)]
        [TestCase(5, 5, 2)]
        [TestCase(12, 12, 10)]
        public void RoomsRespectGapAndStartRemainsCorridor(int width, int height, int rooms)
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                LabyrinthSettings settings = Settings(width, height, rooms);
                LabyrinthLayout layout = LabyrinthGenerator.Generate(settings, new RunRandom(seed));
                Assert.That(layout.RoomCount, Is.InRange(1, rooms));
                Assert.That(layout.GetRoomIndex(layout.StartCell.X, layout.StartCell.Y), Is.EqualTo(-1));
                Assert.That(layout.StartCell.X == 0 || layout.StartCell.X == width - 1, Is.True);
                Assert.That(layout.StartCell.Y == 0 || layout.StartCell.Y == height - 1, Is.True);
                var occupied = new bool[width * height];
                for (int i = 0; i < layout.RoomCount; i++)
                {
                    RoomSlotInfo room = layout.GetRoom(i);
                    Assert.That(room.Index, Is.EqualTo(i));
                    Assert.That(room.Min.X, Is.InRange(0, width - 2));
                    Assert.That(room.Min.Y, Is.InRange(0, height - 2));
                    for (int y = room.Min.Y; y <= room.Min.Y + 1; y++)
                    for (int x = room.Min.X; x <= room.Min.X + 1; x++)
                    {
                        Assert.That(occupied[y * width + x], Is.False);
                        occupied[y * width + x] = true;
                        Assert.That(layout.GetRoomIndex(x, y), Is.EqualTo(i));
                    }
                    for (int j = 0; j < i; j++)
                    {
                        GridPos other = layout.GetRoom(j).Min;
                        Assert.That(Math.Max(Math.Abs(room.Min.X - other.X), Math.Abs(room.Min.Y - other.Y)),
                            Is.GreaterThanOrEqualTo(2 + settings.MinRoomGap));
                    }
                }
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    Assert.That(layout.GetRoomIndex(x, y) >= 0, Is.EqualTo(occupied[y * width + x]));
            }
        }

        /// <summary>Yalnızca kayıtlı kapılar koridora açılır; iç kenarlar açık ve kapı geometrisi doğrudur.</summary>
        [TestCase(9, 9, 7, 1, 2)]
        [TestCase(5, 5, 2, 1, 2)]
        [TestCase(12, 12, 10, 1, 2)]
        [TestCase(7, 7, 12, 0, 1)]
        [TestCase(7, 7, 12, 0, 4)]
        public void DoorwaysMatchOpenPerimeterAndLocalGeometry(int width, int height, int rooms, int gap, int maxDoors)
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                LabyrinthLayout layout = LabyrinthGenerator.Generate(
                    new LabyrinthSettings(width, height, 3.5f, rooms, 0.15f, gap, maxDoors), new RunRandom(seed));
                AssertReachable(layout);
                for (int i = 0; i < layout.RoomCount; i++)
                {
                    RoomSlotInfo room = layout.GetRoom(i);
                    Assert.That(room.Doorways.Length, Is.InRange(1, maxDoors));
                    int perimeterOpenings = 0;
                    for (int y = room.Min.Y; y <= room.Min.Y + 1; y++)
                    for (int x = room.Min.X; x <= room.Min.X + 1; x++)
                    for (int dir = 0; dir < 4; dir++)
                    {
                        GridPos next = Next(new GridPos(x, y), dir);
                        if (InBounds(layout, next) && layout.GetRoomIndex(next.X, next.Y) == i)
                            Assert.That(layout.HasWall(x, y, (LabyrinthDir)dir), Is.False);
                        else if (!layout.HasWall(x, y, (LabyrinthDir)dir))
                        {
                            perimeterOpenings++;
                            Assert.That(InBounds(layout, next), Is.True);
                            Assert.That(layout.GetRoomIndex(next.X, next.Y), Is.EqualTo(-1));
                            int matches = 0;
                            foreach (Doorway doorway in room.Doorways)
                                if (doorway.Inside.X == x && doorway.Inside.Y == y && (int)doorway.Dir == dir) matches++;
                            Assert.That(matches, Is.EqualTo(1));
                        }
                    }
                    Assert.That(perimeterOpenings, Is.EqualTo(room.Doorways.Length));
                    foreach (Doorway doorway in room.Doorways)
                    {
                        Assert.That(layout.GetRoomIndex(doorway.Inside.X, doorway.Inside.Y), Is.EqualTo(i));
                        GridPos outside = Next(doorway.Inside, (int)doorway.Dir);
                        Assert.That(InBounds(layout, outside), Is.True);
                        Assert.That(layout.GetRoomIndex(outside.X, outside.Y), Is.EqualTo(-1));
                        Assert.That(layout.HasWall(doorway.Inside.X, doorway.Inside.Y, doorway.Dir), Is.False);
                        Vector3 direction = new Vector3(outside.X - doorway.Inside.X, 0f, outside.Y - doorway.Inside.Y);
                        Vector3 midpoint = (layout.CellToLocal(doorway.Inside.X, doorway.Inside.Y)
                            + layout.CellToLocal(outside.X, outside.Y)) * 0.5f;
                        Assert.That(Vector3.Distance(doorway.LocalPosition, midpoint), Is.LessThan(0.0001f));
                        Assert.That(Vector3.Distance(doorway.LocalRotation * Vector3.forward, direction), Is.LessThan(0.0001f));
                        Assert.That(Vector3.Distance(doorway.LocalRotation * Vector3.up, Vector3.up), Is.LessThan(0.0001f));
                    }
                    Vector3 expected = (layout.CellToLocal(room.Min.X, room.Min.Y)
                        + layout.CellToLocal(room.Min.X + 1, room.Min.Y + 1)) * 0.5f;
                    Assert.That(room.LocalCenter, Is.EqualTo(expected));
                }
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    Assert.That(layout.CellToLocal(x, y), Is.EqualTo(new Vector3(
                        (x - (width - 1) / 2f) * layout.CellSize, 0f, (y - (height - 1) / 2f) * layout.CellSize)));
                Assert.That(layout.StartLocalPosition, Is.EqualTo(layout.CellToLocal(layout.StartCell.X, layout.StartCell.Y)));
            }
        }

        /// <summary>Örgüsüz koridor ağacı bağlantılıdır; tam örgü yalnızca koridorlar arasında döngü ekler.</summary>
        [TestCase(9, 9, 7)]
        [TestCase(5, 5, 2)]
        [TestCase(12, 12, 10)]
        public void ZeroBraidIsPerfectCorridorMazeAndFullBraidAddsEdges(int width, int height, int rooms)
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                LabyrinthLayout tree = LabyrinthGenerator.Generate(Settings(width, height, rooms, 0f), new RunRandom(seed));
                LabyrinthLayout braided = LabyrinthGenerator.Generate(Settings(width, height, rooms, 1f), new RunRandom(seed));
                int edges = 0;
                int addedEdges = 0;
                int eligibleDeadEnds = 0;
                int corridors = width * height - tree.RoomCount * 4;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                for (int dir = 0; dir < 2; dir++)
                {
                    GridPos next = Next(new GridPos(x, y), dir);
                    if (!InBounds(tree, next)) continue;
                    bool corridorEdge = tree.GetRoomIndex(x, y) < 0 && tree.GetRoomIndex(next.X, next.Y) < 0;
                    bool open = !tree.HasWall(x, y, (LabyrinthDir)dir);
                    bool braidOpen = !braided.HasWall(x, y, (LabyrinthDir)dir);
                    if (corridorEdge && open) edges++;
                    if (open) Assert.That(braidOpen, Is.True);
                    if (open != braidOpen) { Assert.That(corridorEdge, Is.True); addedEdges++; }
                }
                Assert.That(edges, Is.EqualTo(corridors - 1));
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (tree.GetRoomIndex(x, y) >= 0) continue;
                    int openings = 0;
                    int braidOpenings = 0;
                    bool hasCandidate = false;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        if (!braided.HasWall(x, y, (LabyrinthDir)dir)) braidOpenings++;
                        if (!tree.HasWall(x, y, (LabyrinthDir)dir)) openings++;
                        else
                        {
                            GridPos next = Next(new GridPos(x, y), dir);
                            if (InBounds(tree, next) && tree.GetRoomIndex(next.X, next.Y) < 0) hasCandidate = true;
                        }
                    }
                    // Küçük ızgarada ek koridor kenarı bulunmayan çıkmazlar kalabilir.
                    if (openings != 1 || !hasCandidate) continue;
                    eligibleDeadEnds++;
                    Assert.That(braidOpenings, Is.GreaterThanOrEqualTo(2));
                }
                if (eligibleDeadEnds > 0) Assert.That(addedEdges, Is.GreaterThan(0));
                else Assert.That(addedEdges, Is.EqualTo(0));
                AssertReachable(braided);
            }
        }

        /// <summary>Küçük ızgaralarda ve fazla oda isteğinde de bağlantı ve gerçek oda sayısı korunur.</summary>
        [TestCase(1, 1, 5, 1)]
        [TestCase(1, 8, 5, 1)]
        [TestCase(8, 1, 5, 1)]
        [TestCase(2, 2, 50, 1)]
        [TestCase(3, 3, 50, 1)]
        [TestCase(5, 5, 50, 100)]
        [TestCase(8, 8, 50, 0)]
        [TestCase(9, 9, 0, 1)]
        public void ConstrainedGridsRemainConnected(int width, int height, int rooms, int gap)
        {
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                LabyrinthLayout layout = LabyrinthGenerator.Generate(
                    new LabyrinthSettings(width, height, 6f, rooms, 1f, gap, 1), new RunRandom(seed));
                Assert.That(layout.RoomCount, Is.InRange(0, rooms));
                if (width < 3 && height < 3 || width == 1 || height == 1 || rooms == 0)
                    Assert.That(layout.RoomCount, Is.EqualTo(0));
                if (gap == 100) Assert.That(layout.RoomCount, Is.EqualTo(1));
                AssertReachable(layout);
                for (int room = 0; room < layout.RoomCount; room++)
                    Assert.That(layout.GetRoom(room).Doorways.Length, Is.EqualTo(1));
            }
        }

        /// <summary>Yeni Araf akışları önbellekte tutulur ve birbirinden bağımsızdır.</summary>
        [Test]
        public void ArafStreamsAreCachedAndIndependent()
        {
            Assert.That((int)RunStream.ArafLayout, Is.EqualTo(5));
            Assert.That((int)RunStream.ArafRooms, Is.EqualTo(6));
            var state = new RunState(42);
            RunRandom layout = state.GetStream(RunStream.ArafLayout);
            RunRandom rooms = state.GetStream(RunStream.ArafRooms);
            Assert.That(state.GetStream(RunStream.ArafLayout), Is.SameAs(layout));
            Assert.That(state.GetStream(RunStream.ArafRooms), Is.SameAs(rooms));
            Assert.That(rooms, Is.Not.SameAs(layout));
            RunRandom expectedLayout = RunRandom.Derive(42, RunStream.ArafLayout);
            RunRandom expectedRooms = RunRandom.Derive(42, RunStream.ArafRooms);
            for (int i = 0; i < 100; i++)
            {
                layout.NextUInt();
                expectedLayout.NextUInt();
                Assert.That(layout.NextUInt(), Is.EqualTo(expectedLayout.NextUInt()));
                Assert.That(rooms.NextUInt(), Is.EqualTo(expectedRooms.NextUInt()));
            }
        }

        /// <summary>Varsayılan ayarlar sözleşmedeki değerleri korur.</summary>
        [Test]
        public void DefaultSettingsMatchContract()
        {
            var settings = new LabyrinthSettings();
            Assert.That(settings.Width, Is.EqualTo(9));
            Assert.That(settings.Height, Is.EqualTo(9));
            Assert.That(settings.CellSize, Is.EqualTo(6f));
            Assert.That(settings.RoomSlotCount, Is.EqualTo(7));
            Assert.That(settings.BraidChance, Is.EqualTo(0.15f));
            Assert.That(settings.MinRoomGap, Is.EqualTo(1));
            Assert.That(settings.DoorwaysPerRoomMax, Is.EqualTo(2));
        }

        /// <summary>Geçersiz üretim girdileri açıkça reddedilir.</summary>
        [Test]
        public void InvalidSettingsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => LabyrinthGenerator.Generate(null, new RunRandom(0)));
            Assert.Throws<ArgumentNullException>(() => LabyrinthGenerator.Generate(new LabyrinthSettings(), null));
            LabyrinthSettings[] invalid =
            {
                new LabyrinthSettings(0, 9, 6f, 7, 0.15f, 1, 2),
                new LabyrinthSettings(9, 0, 6f, 7, 0.15f, 1, 2),
                new LabyrinthSettings(9, 9, 0f, 7, 0.15f, 1, 2),
                new LabyrinthSettings(9, 9, float.NaN, 7, 0.15f, 1, 2),
                new LabyrinthSettings(9, 9, float.PositiveInfinity, 7, 0.15f, 1, 2),
                new LabyrinthSettings(9, 9, 6f, -1, 0.15f, 1, 2),
                new LabyrinthSettings(9, 9, 6f, 7, -0.1f, 1, 2),
                new LabyrinthSettings(9, 9, 6f, 7, float.NaN, 1, 2),
                new LabyrinthSettings(9, 9, 6f, 7, 1.1f, 1, 2),
                new LabyrinthSettings(9, 9, 6f, 7, 0.15f, -1, 2),
                new LabyrinthSettings(9, 9, 6f, 7, 0.15f, 1, 0)
            };
            foreach (LabyrinthSettings settings in invalid)
                Assert.Throws<ArgumentOutOfRangeException>(() => LabyrinthGenerator.Generate(settings, new RunRandom(0)));
        }

        private static LabyrinthSettings Settings(int width, int height, int rooms, float braid = 0.15f)
            => new LabyrinthSettings(width, height, 6f, rooms, braid, 1, 2);

        private static void AssertReachable(LabyrinthLayout layout)
        {
            var distances = new int[layout.Width * layout.Height];
            for (int i = 0; i < distances.Length; i++) distances[i] = -1;
            var queue = new Queue<GridPos>();
            queue.Enqueue(layout.StartCell);
            distances[layout.StartCell.Y * layout.Width + layout.StartCell.X] = 0;
            int reached = 0;
            while (queue.Count > 0)
            {
                GridPos cell = queue.Dequeue();
                reached++;
                for (int dir = 0; dir < 4; dir++)
                {
                    if (layout.HasWall(cell.X, cell.Y, (LabyrinthDir)dir)) continue;
                    GridPos next = Next(cell, dir);
                    Assert.That(InBounds(layout, next), Is.True);
                    int index = next.Y * layout.Width + next.X;
                    if (distances[index] >= 0) continue;
                    distances[index] = distances[cell.Y * layout.Width + cell.X] + 1;
                    queue.Enqueue(next);
                }
            }
            Assert.That(reached, Is.EqualTo(distances.Length));
            for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
                Assert.That(layout.GetDepth(x, y), Is.EqualTo(distances[y * layout.Width + x]));
            for (int i = 0; i < layout.RoomCount; i++)
            {
                GridPos min = layout.GetRoom(i).Min;
                int depth = int.MaxValue;
                for (int y = min.Y; y <= min.Y + 1; y++)
                for (int x = min.X; x <= min.X + 1; x++)
                    depth = Math.Min(depth, distances[y * layout.Width + x]);
                Assert.That(layout.GetRoom(i).Depth, Is.EqualTo(depth));
            }
        }

        private static GridPos Next(GridPos cell, int dir)
        {
            switch (dir)
            {
                case 0: return new GridPos(cell.X, cell.Y + 1);
                case 1: return new GridPos(cell.X + 1, cell.Y);
                case 2: return new GridPos(cell.X, cell.Y - 1);
                default: return new GridPos(cell.X - 1, cell.Y);
            }
        }

        private static bool InBounds(LabyrinthLayout layout, GridPos cell)
            => cell.X >= 0 && cell.X < layout.Width && cell.Y >= 0 && cell.Y < layout.Height;
    }
}
