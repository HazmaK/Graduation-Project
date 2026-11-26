using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Merkez, kanatlar ve atlanabilir ada zincirlerinden deterministik Araf dünyası üretir.</summary>
    public static class ArafWorldGenerator
    {
        /// <summary>Verilen akıştan sahne nesnesi oluşturmadan dünya topolojisini üretir.</summary>
        public static ArafWorldLayout Generate(ArafWorldSettings s, RunRandom rng)
        {
            Validate(s, rng);
            var mouths = new LabyrinthMouth[s.WingCount];
            for (int i = 0; i < mouths.Length; i++)
                mouths[i] = BorderMouth(s.Hub, WingDirection(i));
            LabyrinthLayout hub = LabyrinthGenerator.Generate(s.Hub, rng,
                new GridPos(s.Hub.Width / 2, s.Hub.Height / 2), mouths);
            var sections = new ArafSection[s.WingCount + 1];
            var chains = new ArafIslandChain[s.WingCount];
            sections[0] = new ArafSection(0, hub, Vector3.zero, -1);
            for (int i = 0; i < chains.Length; i++)
            {
                LabyrinthDir outward = mouths[i].Outward;
                Vector3 direction = Direction(outward);
                Vector3 lateral = new Vector3(direction.z, 0f, -direction.x);
                Vector3 start = hub.CellToLocal(mouths[i].Cell.X, mouths[i].Cell.Y)
                    + direction * (hub.CellSize * 0.5f);
                float height = s.GetWingHeightOffset(i);
                int n = rng.Range(s.PlatformsMin, s.PlatformsMax + 1);
                // Son ağız atlaması da yükselme bütçesine dahildir.
                double required = Math.Ceiling(Math.Max(0d, height) / s.MaxRisePerHop) - 1d;
                if (required > int.MaxValue - 1)
                    throw new ArgumentOutOfRangeException(nameof(s), "Yükselme için çok fazla ada gerekiyor.");
                n = Math.Max(n, (int)required);
                var platforms = new ArafPlatform[n];
                float distance = 0f;
                for (int p = 0; p < n; p++)
                {
                    distance += rng.Range(s.GapMin, s.GapMax) + s.PlatformSize * (p == 0 ? 0.5f : 1f);
                    // Komşu karelerin yan aralıkları örtüşür; gerçek kenar boşluğu eksen boşluğudur.
                    float limit = Math.Min(s.PlatformSize * 0.5f, s.LateralJitter);
                    float jitter = rng.Range(-limit, limit);
                    Vector3 center = start + direction * distance + lateral * jitter;
                    center.y = (float)((double)height * (p + 1) / (n + 1));
                    platforms[p] = new ArafPlatform(center, s.PlatformSize);
                }
                distance += s.PlatformSize * 0.5f + rng.Range(s.GapMin, s.GapMax);
                Vector3 end = start + direction * distance;
                end.y = height;
                LabyrinthMouth wingMouth = BorderMouth(s.Wing, (LabyrinthDir)(((int)outward + 2) & 3));
                LabyrinthLayout wing = LabyrinthGenerator.Generate(s.Wing, rng, wingMouth.Cell,
                    new[] { wingMouth });
                // Kenar ortaları, çift boyutlu ızgaralarda da aynı eksende hizalanır.
                Vector3 wingExit = wing.CellToLocal(wingMouth.Cell.X, wingMouth.Cell.Y)
                    - direction * (wing.CellSize * 0.5f);
                sections[i + 1] = new ArafSection(i + 1, wing, end - wingExit, 0);
                chains[i] = new ArafIslandChain(i + 1, i, start, end, platforms);
            }
            var world = new ArafWorldLayout(sections, chains);
            ValidateFootprints(world);
            return world;
        }

        private static LabyrinthDir WingDirection(int i)
            => i == 0 ? LabyrinthDir.North : i == 1 ? LabyrinthDir.East : LabyrinthDir.West;

        private static LabyrinthMouth BorderMouth(LabyrinthSettings s, LabyrinthDir dir)
        {
            switch (dir)
            {
                case LabyrinthDir.North: return new LabyrinthMouth(new GridPos(s.Width / 2, s.Height - 1), dir);
                case LabyrinthDir.East: return new LabyrinthMouth(new GridPos(s.Width - 1, s.Height / 2), dir);
                case LabyrinthDir.South: return new LabyrinthMouth(new GridPos(s.Width / 2, 0), dir);
                default: return new LabyrinthMouth(new GridPos(0, s.Height / 2), dir);
            }
        }

        private static Vector3 Direction(LabyrinthDir dir)
        {
            switch (dir)
            {
                case LabyrinthDir.North: return Vector3.forward;
                case LabyrinthDir.East: return Vector3.right;
                case LabyrinthDir.South: return Vector3.back;
                default: return Vector3.left;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Validate(ArafWorldSettings s, RunRandom rng)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (s.Hub == null || s.Wing == null) throw new ArgumentException("Bölüm ayarları gereklidir.", nameof(s));
            if (s.WingCount < 0 || s.WingCount > 3 || s.PlatformsMin < 1
                || s.PlatformsMax < s.PlatformsMin || s.PlatformsMax == int.MaxValue
                || !Finite(s.PlatformSize) || s.PlatformSize <= 0f
                || !Finite(s.GapMin) || s.GapMin <= 0f || !Finite(s.GapMax) || s.GapMax < s.GapMin
                || !Finite(s.MaxRisePerHop) || s.MaxRisePerHop <= 0f
                || !Finite(s.LateralJitter) || s.LateralJitter < 0f)
                throw new ArgumentOutOfRangeException(nameof(s), "Dünya üretim sınırları geçersiz.");
            for (int i = 0; i < s.WingHeightOffsetCount; i++)
                if (!Finite(s.GetWingHeightOffset(i))) throw new ArgumentOutOfRangeException(nameof(s));
        }

        private static bool Overlaps(Vector3 a, float ax, float az, Vector3 b, float bx, float bz)
            => Math.Abs((double)a.x - b.x) < ((double)ax + bx) * 0.5d
                && Math.Abs((double)a.z - b.z) < ((double)az + bz) * 0.5d;

        private static void ValidateFootprints(ArafWorldLayout world)
        {
            // Olağandışı bölüm boyutları da sessizce çakışan bir dünya döndüremez.
            for (int i = 0; i < world.SectionCount; i++)
            {
                ArafSection a = world.GetSection(i);
                float width = a.Layout.Width * a.Layout.CellSize;
                float depth = a.Layout.Height * a.Layout.CellSize;
                for (int j = 0; j < i; j++)
                {
                    ArafSection b = world.GetSection(j);
                    if (Overlaps(a.Origin, width, depth, b.Origin,
                        b.Layout.Width * b.Layout.CellSize, b.Layout.Height * b.Layout.CellSize))
                        throw new ArgumentException("Bölüm boyutları çakışan dünya oluşturuyor.");
                }
                for (int c = 0; c < world.ChainCount; c++)
                for (int p = 0; p < world.GetChain(c).PlatformCount; p++)
                {
                    ArafPlatform b = world.GetChain(c).GetPlatform(p);
                    if (Overlaps(a.Origin, width, depth, b.Center, b.Size, b.Size))
                        throw new ArgumentException("Bölüm ve ada boyutları çakışıyor.");
                }
            }
            for (int c = 0; c < world.ChainCount; c++)
            for (int p = 0; p < world.GetChain(c).PlatformCount; p++)
            {
                ArafPlatform a = world.GetChain(c).GetPlatform(p);
                for (int d = 0; d <= c; d++)
                for (int q = 0; q < (d == c ? p : world.GetChain(d).PlatformCount); q++)
                {
                    ArafPlatform b = world.GetChain(d).GetPlatform(q);
                    if (Overlaps(a.Center, a.Size, a.Size, b.Center, b.Size, b.Size))
                        throw new ArgumentException("Ada boyutları çakışıyor.");
                }
            }
        }
    }
}
