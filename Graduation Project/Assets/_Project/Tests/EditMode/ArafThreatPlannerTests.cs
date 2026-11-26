using System;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Araf;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic patrol selection, return topology and wave placement contracts.</summary>
    public sealed class ArafThreatPlannerTests
    {
        [Test]
        public void PatrolsAreDistinctEligibleDeterministicAndExhaustCandidates()
        {
            for (ulong seed = 1; seed <= 50; seed++)
            {
                LabyrinthLayout layout = Generate(seed);
                var first = new GridPos[81];
                var second = new GridPos[81];
                Vector3 avoid = layout.StartLocalPosition;
                int expected = 0;
                for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                    if (layout.GetRoomIndex(x, y) < 0 && !Same(new GridPos(x, y), layout.StartCell)
                        && Vector3.Distance(layout.CellToLocal(x, y), avoid) >= 14f) expected++;

                int count = ArafThreatPlanner.PickPatrolCells(layout, avoid, 14f, new RunRandom(seed), first, 100);
                Assert.That(count, Is.EqualTo(expected));
                Assert.That(ArafThreatPlanner.PickPatrolCells(layout, avoid, 14f,
                    new RunRandom(seed), second, 100), Is.EqualTo(count));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(first[i], Is.EqualTo(second[i]));
                    AssertEligible(layout, first[i], avoid, 14f);
                    for (int j = 0; j < i; j++) Assert.That(Same(first[i], first[j]), Is.False);
                }
            }
        }

        [Test]
        public void EachPatrolAfterFirstMaximizesMinimumGridDistance()
        {
            LabyrinthLayout layout = Generate(42);
            var picks = new GridPos[10];
            int count = ArafThreatPlanner.PickPatrolCells(layout, Vector3.zero, 0f, new RunRandom(7), picks, picks.Length);
            Assert.That(count, Is.EqualTo(picks.Length));
            for (int i = 1; i < count; i++)
            {
                int chosenDistance = Nearest(picks[i], picks, i);
                for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                {
                    var candidate = new GridPos(x, y);
                    if (layout.GetRoomIndex(x, y) >= 0 || Same(candidate, layout.StartCell)) continue;
                    Assert.That(Nearest(candidate, picks, i), Is.LessThanOrEqualTo(chosenDistance));
                }
            }
        }

        [Test]
        public void ReturnPathStartsOutsideFirstDoorAndFollowsOpenDescendingEdgesToStart()
        {
            var path = new GridPos[81];
            var repeat = new GridPos[81];
            for (ulong seed = 1; seed <= 50; seed++)
            {
                LabyrinthLayout layout = Generate(seed);
                Assert.That(layout.RoomCount, Is.GreaterThan(0));
                for (int room = 0; room < layout.RoomCount; room++)
                {
                    int length = ArafThreatPlanner.BuildReturnPath(layout, room, path);
                    Assert.That(length, Is.GreaterThan(0));
                    Doorway door = layout.GetRoom(room).Doorways[0];
                    Assert.That(path[0], Is.EqualTo(Next(door.Inside, (int)door.Dir)));
                    Assert.That(layout.GetRoomIndex(path[0].X, path[0].Y), Is.EqualTo(-1));
                    Assert.That(path[length - 1], Is.EqualTo(layout.StartCell));
                    Assert.That(length, Is.EqualTo(layout.GetDepth(path[0].X, path[0].Y) + 1));
                    Assert.That(ArafThreatPlanner.BuildReturnPath(layout, room, repeat), Is.EqualTo(length));
                    for (int i = 0; i < length; i++)
                    {
                        Assert.That(path[i], Is.EqualTo(repeat[i]));
                        if (i == 0) continue;
                        Assert.That(Distance(path[i - 1], path[i]), Is.EqualTo(1));
                        Assert.That(layout.GetDepth(path[i].X, path[i].Y),
                            Is.EqualTo(layout.GetDepth(path[i - 1].X, path[i - 1].Y) - 1));
                        int dir = Direction(path[i - 1], path[i]);
                        Assert.That(layout.HasWall(path[i - 1].X, path[i - 1].Y, (LabyrinthDir)dir), Is.False);
                        Assert.That(layout.HasWall(path[i].X, path[i].Y, (LabyrinthDir)((dir + 2) % 4)), Is.False);
                    }
                }
            }
        }

        [Test]
        public void WavesAreOnPathSeparatedEligibleAndDeterministic()
        {
            var path = new GridPos[81];
            var first = new GridPos[8];
            var second = new GridPos[8];
            int total = 0;
            for (ulong seed = 1; seed <= 50; seed++)
            {
                LabyrinthLayout layout = Generate(seed);
                for (int room = 0; room < layout.RoomCount; room++)
                {
                    int length = ArafThreatPlanner.BuildReturnPath(layout, room, path);
                    Vector3 avoid = layout.CellToLocal(path[0].X, path[0].Y);
                    int count = ArafThreatPlanner.PickWaveCells(layout, path, length, avoid, 14f,
                        new RunRandom(seed), first, 8);
                    total += count;
                    Assert.That(ArafThreatPlanner.PickWaveCells(layout, path, length, avoid, 14f,
                        new RunRandom(seed), second, 8), Is.EqualTo(count));
                    for (int i = 0; i < count; i++)
                    {
                        Assert.That(first[i], Is.EqualTo(second[i]));
                        Assert.That(IndexOf(path, length, first[i]), Is.GreaterThanOrEqualTo(0));
                        AssertEligible(layout, first[i], avoid, 14f);
                        for (int j = 0; j < i; j++)
                        {
                            Assert.That(Distance(first[i], first[j]), Is.GreaterThanOrEqualTo(2));
                            Assert.That(Math.Abs(IndexOf(path, length, first[i]) - IndexOf(path, length, first[j])),
                                Is.GreaterThanOrEqualTo(2));
                        }
                    }
                }
            }
            Assert.That(total, Is.GreaterThan(0));
        }

        [Test]
        public void WavesPreferWindowAndWidenWhenItCannotSupplyEnoughCells()
        {
            var path = new GridPos[81];
            var picks = new GridPos[81];
            bool checkedWindow = false;
            bool checkedFallback = false;
            for (ulong seed = 1; seed <= 50; seed++)
            {
                LabyrinthLayout layout = Generate(seed);
                for (int room = 0; room < layout.RoomCount; room++)
                {
                    int length = ArafThreatPlanner.BuildReturnPath(layout, room, path);
                    int begin = Mathf.CeilToInt((length - 1) * 0.3f);
                    int end = Mathf.FloorToInt((length - 1) * 0.85f);
                    bool eligibleWindow = false;
                    for (int i = begin; i <= end; i++)
                        if (layout.GetRoomIndex(path[i].X, path[i].Y) < 0 && !Same(path[i], layout.StartCell)) eligibleWindow = true;
                    if (eligibleWindow)
                    {
                        Assert.That(ArafThreatPlanner.PickWaveCells(layout, path, length, Vector3.zero, 0f,
                            new RunRandom(seed), picks, 1), Is.EqualTo(1));
                        Assert.That(IndexOf(path, length, picks[0]), Is.InRange(begin, end));
                        checkedWindow = true;
                    }
                    // Request more than the entire window can hold, forcing the second pass.
                    int count = ArafThreatPlanner.PickWaveCells(layout, path, length, Vector3.zero, 0f,
                        new RunRandom(seed), picks, 81);
                    for (int i = 0; i < count; i++)
                    {
                        int index = IndexOf(path, length, picks[i]);
                        if (index < begin || index > end) checkedFallback = true;
                    }
                }
            }
            Assert.That(checkedWindow, Is.True);
            Assert.That(checkedFallback, Is.True);
        }

        [Test]
        public void EmptyInvalidAndLimitedInputsAreBounded()
        {
            LabyrinthLayout layout = Generate(42);
            var rng = new RunRandom(1);
            var buffer = new GridPos[1];
            Assert.That(ArafThreatPlanner.PickPatrolCells(layout, Vector3.zero, 0f, rng, buffer, 5), Is.EqualTo(1));
            Assert.That(ArafThreatPlanner.PickPatrolCells(layout, Vector3.zero, 0f, rng, buffer, 0), Is.Zero);
            Assert.That(ArafThreatPlanner.PickPatrolCells(layout, Vector3.zero, 10000f, rng, buffer, 1), Is.Zero);
            Assert.That(ArafThreatPlanner.PickPatrolCells(null, Vector3.zero, 0f, rng, buffer, 1), Is.Zero);
            Assert.That(ArafThreatPlanner.PickPatrolCells(layout, Vector3.zero, 0f, rng, null, 1), Is.Zero);
            Assert.That(ArafThreatPlanner.PickPatrolCells(layout, Vector3.zero, 0f, null, buffer, 1), Is.Zero);
            Assert.That(ArafThreatPlanner.BuildReturnPath(layout, -1, buffer), Is.Zero);
            Assert.That(ArafThreatPlanner.BuildReturnPath(layout, layout.RoomCount, buffer), Is.Zero);
            Assert.That(ArafThreatPlanner.BuildReturnPath(layout, 0, null), Is.Zero);
            Assert.That(ArafThreatPlanner.BuildReturnPath(layout, 0, new GridPos[0]), Is.Zero);
            Assert.That(ArafThreatPlanner.BuildReturnPath(layout, 0, buffer), Is.EqualTo(1));
            Assert.That(ArafThreatPlanner.PickWaveCells(layout, buffer, 1, Vector3.zero, 0f, rng, new GridPos[0], 1), Is.Zero);
            Assert.That(ArafThreatPlanner.PickWaveCells(layout, buffer, 0, Vector3.zero, 0f, rng, buffer, 1), Is.Zero);
            Assert.That(ArafThreatPlanner.PickWaveCells(layout, buffer, 1, Vector3.zero, 10000f, rng, new GridPos[1], 1), Is.Zero);
            var path = new GridPos[81];
            int boundedWaveCount = 0;
            for (int room = 0; room < layout.RoomCount; room++)
            {
                int length = ArafThreatPlanner.BuildReturnPath(layout, room, path);
                int count = ArafThreatPlanner.PickWaveCells(layout, path, length, Vector3.zero, 0f, rng, buffer, 8);
                Assert.That(count, Is.InRange(0, 1));
                boundedWaveCount += count;
            }
            Assert.That(boundedWaveCount, Is.GreaterThan(0));
        }

        private static LabyrinthLayout Generate(ulong seed)
            => LabyrinthGenerator.Generate(new LabyrinthSettings(), new RunRandom(seed));

        private static void AssertEligible(LabyrinthLayout layout, GridPos cell, Vector3 avoid, float minDistance)
        {
            Assert.That(layout.GetRoomIndex(cell.X, cell.Y), Is.EqualTo(-1));
            Assert.That(Same(cell, layout.StartCell), Is.False);
            Assert.That(Vector3.Distance(layout.CellToLocal(cell.X, cell.Y), avoid), Is.GreaterThanOrEqualTo(minDistance));
        }

        private static int Nearest(GridPos cell, GridPos[] picks, int length)
        {
            int nearest = int.MaxValue;
            for (int i = 0; i < length; i++) nearest = Math.Min(nearest, Distance(cell, picks[i]));
            return nearest;
        }

        private static int IndexOf(GridPos[] path, int length, GridPos cell)
        {
            for (int i = 0; i < length; i++) if (Same(path[i], cell)) return i;
            return -1;
        }

        private static bool Same(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        private static int Distance(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        private static int Direction(GridPos from, GridPos to)
            => to.Y > from.Y ? 0 : to.X > from.X ? 1 : to.Y < from.Y ? 2 : 3;

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
    }
}
