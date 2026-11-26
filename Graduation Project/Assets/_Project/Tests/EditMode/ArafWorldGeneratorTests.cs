using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Araf dünyasının atlama, yerleşim ve deterministik üretim sözleşmeleri.</summary>
    public sealed class ArafWorldGeneratorTests
    {
        /// <summary>Varsayılan dünya ayarları tasarımdaki değerlerle aynıdır.</summary>
        [Test]
        public void DefaultsMatchContract()
        {
            var s = new ArafWorldSettings();
            Assert.That(s.Hub.Width, Is.EqualTo(7));
            Assert.That(s.Hub.Height, Is.EqualTo(7));
            Assert.That(s.Wing.Width, Is.EqualTo(7));
            Assert.That(s.Wing.Height, Is.EqualTo(7));
            Assert.That(s.Hub.CellSize, Is.EqualTo(6f));
            Assert.That(s.Wing.CellSize, Is.EqualTo(6f));
            Assert.That(s.Hub.RoomSlotCount, Is.EqualTo(2));
            Assert.That(s.Wing.RoomSlotCount, Is.EqualTo(3));
            Assert.That(s.WingCount, Is.EqualTo(3));
            Assert.That(s.WingHeightOffsetCount, Is.EqualTo(3));
            Assert.That(s.GetWingHeightOffset(0), Is.EqualTo(2.4f));
            Assert.That(s.GetWingHeightOffset(1), Is.EqualTo(-3.2f));
            Assert.That(s.GetWingHeightOffset(2), Is.EqualTo(4f));
            Assert.That(s.PlatformsMin, Is.EqualTo(4));
            Assert.That(s.PlatformsMax, Is.EqualTo(6));
            Assert.That(s.PlatformSize, Is.EqualTo(5f));
            Assert.That(s.GapMin, Is.EqualTo(2f));
            Assert.That(s.GapMax, Is.EqualTo(3.5f));
            Assert.That(s.MaxRisePerHop, Is.EqualTo(0.8f));
            Assert.That(s.LateralJitter, Is.EqualTo(1.5f));
        }

        /// <summary>Üç yüz tohumda bütün bölümler, atlamalar, derinlikler ve tekrar üretim doğrulanır.</summary>
        [Test]
        public void DefaultWorldMeetsAllConstraintsAcross300Seeds()
        {
            var s = new ArafWorldSettings();
            float minGap = float.MaxValue;
            float maxGap = float.MinValue;
            float maxRise = float.MinValue;
            float minY = float.MaxValue;
            int platformCount = 0;
            int hopCount = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var firstRng = new RunRandom(seed);
                var secondRng = new RunRandom(seed);
                ArafWorldLayout world = ArafWorldGenerator.Generate(s, firstRng);
                ArafWorldLayout second = ArafWorldGenerator.Generate(s, secondRng);
                AssertWorld(s, world, ref minGap, ref maxGap, ref maxRise, ref minY, ref hopCount);
                AssertSameWorld(world, second);
                Assert.That(secondRng.NextUInt(), Is.EqualTo(firstRng.NextUInt()));
                for (int c = 0; c < world.ChainCount; c++) platformCount += world.GetChain(c).PlatformCount;
            }
            Console.WriteLine("Araf sweep: seeds=300, sections=1200, chains=900, platforms={0}, hops={1}, "
                + "gap=[{2:F6},{3:F6}], maxRise={4:F6}, minY={5:F6}",
                platformCount, hopCount, minGap, maxGap, maxRise, minY);
        }

        /// <summary>Yüksek kanatlar ada üst sınırını aşar; kısa ve boş ofset dizileri güvenle işlenir.</summary>
        [TestCase(12f)]
        [TestCase(-30f)]
        [TestCase(0f)]
        public void HeightRequirementsOverridePlatformMaximumAndOffsetsWrap(float height)
        {
            var defaults = new ArafWorldSettings();
            var offsets = new[] { height };
            var s = new ArafWorldSettings(defaults.Hub, defaults.Wing, wingHeightOffsets: offsets,
                platformsMin: 1, platformsMax: 1, lateralJitter: 20f);
            offsets[0] = 100f;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                ArafWorldLayout world = ArafWorldGenerator.Generate(s, new RunRandom(seed));
                Assert.That(world.GetSection(3).Origin.y, Is.EqualTo(height));
                Assert.That(world.GetChain(0).PlatformCount, Is.EqualTo(Math.Max(1,
                    (int)Math.Ceiling(Math.Max(0d, height) / s.MaxRisePerHop) - 1)));
                float minGap = float.MaxValue, maxGap = float.MinValue, maxRise = float.MinValue, minY = float.MaxValue;
                int hops = 0;
                AssertWorld(s, world, ref minGap, ref maxGap, ref maxRise, ref minY, ref hops);
            }
            var empty = new ArafWorldSettings(defaults.Hub, defaults.Wing, wingHeightOffsets: Array.Empty<float>());
            Assert.That(ArafWorldGenerator.Generate(empty, new RunRandom(1)).GetSection(3).Origin.y, Is.Zero);
        }

        /// <summary>Çift ve dikdörtgen ızgaralarda zincir uçları gerçek ağız kenarlarına hizalanır.</summary>
        [Test]
        public void RectangularEvenSectionsAndFixedGapsRemainValid()
        {
            var s = new ArafWorldSettings(new LabyrinthSettings(8, 6, 6f, 2, 0.15f, 1, 2),
                new LabyrinthSettings(6, 8, 6f, 3, 0.15f, 1, 2), gapMin: 2.5f, gapMax: 2.5f,
                lateralJitter: 0f);
            for (ulong seed = 1; seed <= 30; seed++)
            {
                float minGap = float.MaxValue, maxGap = float.MinValue, maxRise = float.MinValue, minY = float.MaxValue;
                int hops = 0;
                AssertWorld(s, ArafWorldGenerator.Generate(s, new RunRandom(seed)),
                    ref minGap, ref maxGap, ref maxRise, ref minY, ref hops);
            }
        }

        /// <summary>Eksik ve geçersiz dünya ayarları açık hatalar verir.</summary>
        [Test]
        public void InvalidSettingsAreRejected()
        {
            var d = new ArafWorldSettings();
            Assert.Throws<ArgumentNullException>(() => ArafWorldGenerator.Generate(null, new RunRandom(1)));
            Assert.Throws<ArgumentNullException>(() => ArafWorldGenerator.Generate(d, null));
            Assert.Throws<ArgumentException>(() => ArafWorldGenerator.Generate(
                new ArafWorldSettings(null, d.Wing), new RunRandom(1)));
            ArafWorldSettings[] invalid =
            {
                new ArafWorldSettings(d.Hub, d.Wing, wingCount: 4),
                new ArafWorldSettings(d.Hub, d.Wing, platformsMin: 0),
                new ArafWorldSettings(d.Hub, d.Wing, platformsMin: 5, platformsMax: 4),
                new ArafWorldSettings(d.Hub, d.Wing, platformSize: float.NaN),
                new ArafWorldSettings(d.Hub, d.Wing, gapMin: 0f),
                new ArafWorldSettings(d.Hub, d.Wing, gapMin: 4f, gapMax: 3f),
                new ArafWorldSettings(d.Hub, d.Wing, maxRisePerHop: 0f),
                new ArafWorldSettings(d.Hub, d.Wing, lateralJitter: float.PositiveInfinity),
                new ArafWorldSettings(d.Hub, d.Wing, wingHeightOffsets: new[] { float.NaN })
            };
            foreach (ArafWorldSettings s in invalid)
                Assert.Throws<ArgumentOutOfRangeException>(() => ArafWorldGenerator.Generate(s, new RunRandom(1)));
        }

        private static void AssertWorld(ArafWorldSettings s, ArafWorldLayout world,
            ref float minGap, ref float maxGap, ref float maxRise, ref float minY, ref int hops)
        {
            Assert.That(world.SectionCount, Is.EqualTo(s.WingCount + 1));
            Assert.That(world.ChainCount, Is.EqualTo(s.WingCount));
            Assert.That(world.Hub, Is.SameAs(world.GetSection(0)));
            Assert.That(world.Hub.IsHub, Is.True);
            Assert.That(world.Hub.MouthIndex, Is.EqualTo(-1));
            Assert.That(world.Hub.Origin, Is.EqualTo(Vector3.zero));
            Assert.That(world.Hub.Layout.StartCell, Is.EqualTo(new GridPos(s.Hub.Width / 2, s.Hub.Height / 2)));
            Assert.That(world.Hub.Layout.MouthCount, Is.EqualTo(s.WingCount));
            var centers = new List<Vector3>();
            var sizes = new List<Vector2>();
            for (int i = 0; i < world.SectionCount; i++)
            {
                ArafSection section = world.GetSection(i);
                Assert.That(section.Index, Is.EqualTo(i));
                Assert.That(section.IsHub, Is.EqualTo(i == 0));
                Assert.That(section.Layout.RoomCount, Is.EqualTo(i == 0 ? s.Hub.RoomSlotCount : s.Wing.RoomSlotCount));
                Assert.That(section.Origin.y, Is.GreaterThan(-999f));
                minY = Math.Min(minY, section.Origin.y);
                AssertDepths(section.Layout);
                centers.Add(section.Origin);
                sizes.Add(new Vector2(section.Layout.Width * section.Layout.CellSize,
                    section.Layout.Height * section.Layout.CellSize));
                for (int y = 0; y < section.Layout.Height; y++)
                for (int x = 0; x < section.Layout.Width; x++)
                    Assert.That(world.CellToLocal(i, x, y),
                        Is.EqualTo(section.Origin + section.Layout.CellToLocal(x, y)));
            }
            for (int c = 0; c < world.ChainCount; c++)
            {
                ArafIslandChain chain = world.GetChain(c);
                ArafSection wing = world.GetSection(chain.WingSection);
                Assert.That(chain.WingSection, Is.EqualTo(c + 1));
                Assert.That(chain.HubMouth, Is.EqualTo(c));
                Assert.That(wing.MouthIndex, Is.Zero);
                Assert.That(wing.Layout.MouthCount, Is.EqualTo(1));
                LabyrinthMouth hubMouth = world.Hub.Layout.GetMouth(chain.HubMouth);
                LabyrinthMouth wingMouth = wing.Layout.GetMouth(wing.MouthIndex);
                Assert.That(hubMouth.Outward, Is.EqualTo(c == 0 ? LabyrinthDir.North : c == 1 ? LabyrinthDir.East : LabyrinthDir.West));
                Assert.That((int)wingMouth.Outward, Is.EqualTo(((int)hubMouth.Outward + 2) & 3));
                Assert.That(wing.Layout.StartCell, Is.EqualTo(wingMouth.Cell));
                Vector3 direction = hubMouth.Outward == LabyrinthDir.North ? Vector3.forward
                    : hubMouth.Outward == LabyrinthDir.East ? Vector3.right : Vector3.left;
                Assert.That(Vector3.Distance(chain.Start, world.CellToLocal(0, hubMouth.Cell.X, hubMouth.Cell.Y)
                    + direction * (world.Hub.Layout.CellSize * 0.5f)), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(chain.End, world.CellToLocal(wing.Index, wingMouth.Cell.X, wingMouth.Cell.Y)
                    - direction * (wing.Layout.CellSize * 0.5f)), Is.LessThan(0.0001f));
                Assert.That(wing.Origin.y, Is.EqualTo(s.GetWingHeightOffset(c)));
                Assert.That(chain.PlatformCount, Is.GreaterThanOrEqualTo(s.PlatformsMin));
                if (s.GetWingHeightOffset(c) <= (s.PlatformsMax + 1) * s.MaxRisePerHop)
                    Assert.That(chain.PlatformCount, Is.LessThanOrEqualTo(s.PlatformsMax));
                Vector3 previous = chain.Start;
                float previousSize = 0f;
                for (int p = 0; p <= chain.PlatformCount; p++)
                {
                    bool isEnd = p == chain.PlatformCount;
                    Vector3 current = isEnd ? chain.End : chain.GetPlatform(p).Center;
                    float size = isEnd ? 0f : chain.GetPlatform(p).Size;
                    // Bağımsız AABB kenar uzaklığı; uç ağızlar nokta olarak değerlendirilir.
                    float dx = Math.Max(0f, Math.Abs(current.x - previous.x) - (previousSize + size) * 0.5f);
                    float dz = Math.Max(0f, Math.Abs(current.z - previous.z) - (previousSize + size) * 0.5f);
                    float gap = (float)Math.Sqrt((double)dx * dx + (double)dz * dz);
                    float rise = current.y - previous.y;
                    Assert.That(gap, Is.InRange(s.GapMin - 0.01f, s.GapMax + 0.01f));
                    Assert.That(rise, Is.LessThanOrEqualTo(s.MaxRisePerHop + 0.01f));
                    Assert.That(current.y, Is.GreaterThan(-999f));
                    minGap = Math.Min(minGap, gap);
                    maxGap = Math.Max(maxGap, gap);
                    maxRise = Math.Max(maxRise, rise);
                    minY = Math.Min(minY, current.y);
                    hops++;
                    if (!isEnd)
                    {
                        Assert.That(size, Is.EqualTo(s.PlatformSize));
                        float lateral = direction.z == 0f ? current.z - chain.Start.z : current.x - chain.Start.x;
                        Assert.That(Math.Abs(lateral), Is.LessThanOrEqualTo(s.LateralJitter + 0.0001f));
                        Assert.That(current.y, Is.EqualTo((float)((double)chain.End.y * (p + 1) / (chain.PlatformCount + 1))).Within(0.0001f));
                        centers.Add(current);
                        sizes.Add(new Vector2(size, size));
                    }
                    previous = current;
                    previousSize = size;
                }
            }
            for (int i = 0; i < centers.Count; i++)
            for (int j = 0; j < i; j++)
            {
                bool overlap = Math.Abs(centers[i].x - centers[j].x) < (sizes[i].x + sizes[j].x) * 0.5f
                    && Math.Abs(centers[i].z - centers[j].z) < (sizes[i].y + sizes[j].y) * 0.5f;
                Assert.That(overlap, Is.False, "Bölüm veya ada ayak izleri çakıştı.");
            }
        }

        private static void AssertDepths(LabyrinthLayout layout)
        {
            var depths = new int[layout.Width * layout.Height];
            for (int i = 0; i < depths.Length; i++) depths[i] = -1;
            var queue = new Queue<GridPos>();
            queue.Enqueue(layout.StartCell);
            depths[layout.StartCell.Y * layout.Width + layout.StartCell.X] = 0;
            while (queue.Count > 0)
            {
                GridPos cell = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    if (layout.HasWall(cell.X, cell.Y, (LabyrinthDir)d)) continue;
                    int x = cell.X + (d == 1 ? 1 : d == 3 ? -1 : 0);
                    int y = cell.Y + (d == 0 ? 1 : d == 2 ? -1 : 0);
                    if (x < 0 || x >= layout.Width || y < 0 || y >= layout.Height) continue;
                    Assert.That(layout.HasWall(x, y, (LabyrinthDir)((d + 2) & 3)), Is.False);
                    if (depths[y * layout.Width + x] >= 0) continue;
                    depths[y * layout.Width + x] = depths[cell.Y * layout.Width + cell.X] + 1;
                    queue.Enqueue(new GridPos(x, y));
                }
            }
            for (int y = 0; y < layout.Height; y++)
            for (int x = 0; x < layout.Width; x++)
            {
                Assert.That(depths[y * layout.Width + x], Is.GreaterThanOrEqualTo(0));
                Assert.That(layout.GetDepth(x, y), Is.EqualTo(depths[y * layout.Width + x]));
            }
            for (int i = 0; i < layout.MouthCount; i++)
            {
                LabyrinthMouth mouth = layout.GetMouth(i);
                Assert.That(layout.GetRoomIndex(mouth.Cell.X, mouth.Cell.Y), Is.EqualTo(-1));
                Assert.That(layout.HasWall(mouth.Cell.X, mouth.Cell.Y, mouth.Outward), Is.False);
            }
            for (int r = 0; r < layout.RoomCount; r++)
            {
                GridPos min = layout.GetRoom(r).Min;
                int depth = int.MaxValue;
                for (int y = min.Y; y <= min.Y + 1; y++)
                for (int x = min.X; x <= min.X + 1; x++) depth = Math.Min(depth, depths[y * layout.Width + x]);
                Assert.That(layout.GetRoom(r).Depth, Is.EqualTo(depth));
            }
        }

        private static void AssertSameWorld(ArafWorldLayout a, ArafWorldLayout b)
        {
            Assert.That(b.SectionCount, Is.EqualTo(a.SectionCount));
            Assert.That(b.ChainCount, Is.EqualTo(a.ChainCount));
            for (int s = 0; s < a.SectionCount; s++)
            {
                Assert.That(b.GetSection(s).Origin, Is.EqualTo(a.GetSection(s).Origin));
                LabyrinthLayout left = a.GetSection(s).Layout;
                LabyrinthLayout right = b.GetSection(s).Layout;
                Assert.That(right.StartCell, Is.EqualTo(left.StartCell));
                Assert.That(right.RoomCount, Is.EqualTo(left.RoomCount));
                for (int r = 0; r < left.RoomCount; r++) Assert.That(right.GetRoom(r).Min, Is.EqualTo(left.GetRoom(r).Min));
                for (int y = 0; y < left.Height; y++)
                for (int x = 0; x < left.Width; x++)
                {
                    Assert.That(right.GetDepth(x, y), Is.EqualTo(left.GetDepth(x, y)));
                    Assert.That(right.GetRoomIndex(x, y), Is.EqualTo(left.GetRoomIndex(x, y)));
                    for (int d = 0; d < 4; d++)
                        Assert.That(right.HasWall(x, y, (LabyrinthDir)d), Is.EqualTo(left.HasWall(x, y, (LabyrinthDir)d)));
                }
            }
            for (int c = 0; c < a.ChainCount; c++)
            {
                ArafIslandChain left = a.GetChain(c), right = b.GetChain(c);
                Assert.That(right.WingSection, Is.EqualTo(left.WingSection));
                Assert.That(right.HubMouth, Is.EqualTo(left.HubMouth));
                Assert.That(right.Start, Is.EqualTo(left.Start));
                Assert.That(right.End, Is.EqualTo(left.End));
                Assert.That(right.PlatformCount, Is.EqualTo(left.PlatformCount));
                for (int p = 0; p < left.PlatformCount; p++) Assert.That(right.GetPlatform(p), Is.EqualTo(left.GetPlatform(p)));
            }
        }
    }
}
