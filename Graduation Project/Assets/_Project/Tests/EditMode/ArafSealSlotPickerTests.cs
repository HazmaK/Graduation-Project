using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>En uzak nokta mühür yuvası seçicisinin sözleşme testleri.</summary>
    public sealed class ArafSealSlotPickerTests
    {
        [Test]
        public void Pick_ReturnsMinOfCountRoomsAndBuffer()
        {
            LabyrinthLayout layout = LabyrinthGenerator.Generate(new LabyrinthSettings(), new RunRandom(42UL));
            int rooms = layout.RoomCount;
            Assert.That(rooms, Is.GreaterThanOrEqualTo(3));

            int[] exact = new int[3];
            Assert.AreEqual(Math.Min(3, rooms), ArafSealSlotPicker.Pick(layout, 3, new RunRandom(1UL), exact));

            int[] oversized = new int[20];
            Assert.AreEqual(rooms, ArafSealSlotPicker.Pick(layout, 12, new RunRandom(1UL), oversized));

            int[] smallBuffer = new int[2];
            Assert.AreEqual(2, ArafSealSlotPicker.Pick(layout, 5, new RunRandom(1UL), smallBuffer));

            Assert.AreEqual(0, ArafSealSlotPicker.Pick(layout, 0, new RunRandom(1UL), exact));
            Assert.AreEqual(0, ArafSealSlotPicker.Pick(layout, 3, new RunRandom(1UL), Array.Empty<int>()));
            Assert.AreEqual(0, ArafSealSlotPicker.Pick(null, 3, new RunRandom(1UL), exact));
            Assert.AreEqual(0, ArafSealSlotPicker.Pick(layout, 3, new RunRandom(1UL), null));
        }

        [Test]
        public void Pick_IndicesAreDistinctAndInRange()
        {
            LabyrinthSettings settings = new LabyrinthSettings();
            int[] buffer = new int[3];
            var seen = new HashSet<int>();

            for (ulong seed = 1UL; seed <= 50UL; seed++)
            {
                LabyrinthLayout layout = LabyrinthGenerator.Generate(settings, new RunRandom(seed));
                int count = ArafSealSlotPicker.Pick(layout, 3, new RunRandom(seed * 7UL), buffer);
                Assert.AreEqual(Math.Min(3, layout.RoomCount), count);

                seen.Clear();
                for (int i = 0; i < count; i++)
                {
                    Assert.That(buffer[i], Is.InRange(0, layout.RoomCount - 1));
                    Assert.IsTrue(seen.Add(buffer[i]), $"Seed {seed} produced duplicate slot index {buffer[i]}.");
                }
            }
        }

        [Test]
        public void Pick_IsDeterministic_ForSameSeed()
        {
            LabyrinthLayout layout = LabyrinthGenerator.Generate(new LabyrinthSettings(), new RunRandom(1337UL));
            int[] first = new int[3];
            int[] second = new int[3];

            int countA = ArafSealSlotPicker.Pick(layout, 3, new RunRandom(9001UL), first);
            int countB = ArafSealSlotPicker.Pick(layout, 3, new RunRandom(9001UL), second);

            Assert.AreEqual(countA, countB);
            for (int i = 0; i < countA; i++)
            {
                Assert.AreEqual(first[i], second[i]);
            }
        }

        [Test]
        public void Pick_FirstPickHasMaxDepth()
        {
            LabyrinthSettings settings = new LabyrinthSettings();
            int[] buffer = new int[3];

            for (ulong seed = 1UL; seed <= 50UL; seed++)
            {
                LabyrinthLayout layout = LabyrinthGenerator.Generate(settings, new RunRandom(seed));
                int count = ArafSealSlotPicker.Pick(layout, 3, new RunRandom(seed), buffer);
                if (count == 0) continue;

                int maxDepth = int.MinValue;
                for (int i = 0; i < layout.RoomCount; i++)
                {
                    maxDepth = Math.Max(maxDepth, layout.GetRoom(i).Depth);
                }

                Assert.AreEqual(maxDepth, layout.GetRoom(buffer[0]).Depth,
                    $"Seed {seed}: first pick did not have maximum depth.");
            }
        }

        [Test]
        public void Pick_IsMoreSpreadThanDeepestThree_OnGeneratedLayouts()
        {
            LabyrinthSettings settings = new LabyrinthSettings();
            int[] pickerBuffer = new int[3];
            int[] deepestBuffer = new int[3];

            double totalPickerMinDist = 0.0;
            double totalDeepestMinDist = 0.0;
            int evaluated = 0;
            int strictlyBetter = 0;

            for (ulong seed = 1UL; seed <= 60UL; seed++)
            {
                LabyrinthLayout layout = LabyrinthGenerator.Generate(settings, new RunRandom(seed));
                if (layout.RoomCount < 4) continue;

                int picked = ArafSealSlotPicker.Pick(layout, 3, new RunRandom(seed), pickerBuffer);
                Assert.AreEqual(3, picked);

                PickDeepestThree(layout, deepestBuffer);

                float pickerMinDist = MinPairwiseDistance(layout, pickerBuffer, 3);
                float deepestMinDist = MinPairwiseDistance(layout, deepestBuffer, 3);

                totalPickerMinDist += pickerMinDist;
                totalDeepestMinDist += deepestMinDist;
                evaluated++;
                if (pickerMinDist > deepestMinDist + 1e-4f)
                {
                    strictlyBetter++;
                }
            }

            Assert.That(evaluated, Is.GreaterThan(10));
            Assert.That(strictlyBetter, Is.GreaterThan(0));
            Assert.That(totalPickerMinDist, Is.GreaterThan(totalDeepestMinDist));
        }

        private static void PickDeepestThree(LabyrinthLayout layout, int[] results)
        {
            int[] indices = new int[layout.RoomCount];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;

            Array.Sort(indices, (a, b) => layout.GetRoom(b).Depth.CompareTo(layout.GetRoom(a).Depth));
            for (int i = 0; i < 3; i++) results[i] = indices[i];
        }

        private static float MinPairwiseDistance(LabyrinthLayout layout, int[] indices, int count)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = layout.GetRoom(indices[i]).LocalCenter;
                for (int j = i + 1; j < count; j++)
                {
                    float dist = Vector3.Distance(a, layout.GetRoom(indices[j]).LocalCenter);
                    if (dist < minDist) minDist = dist;
                }
            }
            return minDist;
        }
    }
}
