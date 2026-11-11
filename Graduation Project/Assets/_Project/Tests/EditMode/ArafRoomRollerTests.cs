using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    public class ArafRoomRollerTests
    {
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                {
                    Object.DestroyImmediate(_createdAssets[i]);
                }
            }
            _createdAssets.Clear();
        }

        private ArafRoomDefinition CreateDefinition(
            string displayName,
            ArafRoomKind kind,
            float weight = 1f,
            int minDepth = 0,
            int maxPerRun = 99,
            bool sealDoors = true)
        {
            ArafRoomDefinition def = ScriptableObject.CreateInstance<ArafRoomDefinition>();
            _createdAssets.Add(def);

            SerializedObject so = new SerializedObject(def);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("minDepth").intValue = minDepth;
            so.FindProperty("maxPerRun").intValue = maxPerRun;
            so.FindProperty("sealDoorsUntilCleared").boolValue = sealDoors;
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        private ArafRoomTable CreateTable(
            ArafRoomDefinition[] rooms,
            ArafRoomDefinition exitRoom,
            float exitBaseChance = 0.10f,
            float exitChancePerReveal = 0.15f,
            int exitMinDepth = 3)
        {
            ArafRoomTable table = ScriptableObject.CreateInstance<ArafRoomTable>();
            _createdAssets.Add(table);

            SerializedObject so = new SerializedObject(table);
            SerializedProperty roomsProp = so.FindProperty("rooms");
            roomsProp.arraySize = rooms != null ? rooms.Length : 0;
            for (int i = 0; i < roomsProp.arraySize; i++)
            {
                roomsProp.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
            }
            so.FindProperty("exitRoom").objectReferenceValue = exitRoom;
            so.FindProperty("exitBaseChance").floatValue = exitBaseChance;
            so.FindProperty("exitChancePerReveal").floatValue = exitChancePerReveal;
            so.FindProperty("exitMinDepth").intValue = exitMinDepth;
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        [Test]
        public void ExitAlwaysSpawnsExactlyOnce_AcrossManySeeds()
        {
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f);
            ArafRoomDefinition ambush = CreateDefinition("Ambush", ArafRoomKind.Ambush, weight: 1f);
            ArafRoomDefinition shrine = CreateDefinition("Shrine", ArafRoomKind.Shrine, weight: 0.5f, maxPerRun: 1);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { combat, ambush, shrine }, exit, 0.10f, 0.15f, 3);

            const int totalSlots = 7;
            for (ulong seed = 1UL; seed <= 120UL; seed++)
            {
                ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(seed), totalSlots);
                int exitCount = 0;
                for (int slot = 0; slot < totalSlots; slot++)
                {
                    int depth = slot + 1;
                    ArafRoomDefinition rolled = roller.Roll(depth);
                    Assert.IsNotNull(rolled);
                    if (ReferenceEquals(rolled, exit))
                    {
                        exitCount++;
                    }
                }
                Assert.AreEqual(1, exitCount, $"Seed {seed} did not spawn exit exactly once.");
                Assert.IsTrue(roller.ExitSpawned);
                Assert.AreEqual(totalSlots, roller.RevealedCount);
            }
        }

        [Test]
        public void LastSlot_IsGuaranteedExit_WhenNotYetSpawned()
        {
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { combat }, exit, exitBaseChance: 0f, exitChancePerReveal: 0f, exitMinDepth: 99);

            const int totalSlots = 5;
            ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(42UL), totalSlots);

            for (int i = 0; i < totalSlots - 1; i++)
            {
                ArafRoomDefinition rolled = roller.Roll(slotDepth: 1);
                Assert.AreSame(combat, rolled);
                Assert.IsFalse(roller.ExitSpawned);
            }

            ArafRoomDefinition lastRolled = roller.Roll(slotDepth: 1);
            Assert.AreSame(exit, lastRolled);
            Assert.IsTrue(roller.ExitSpawned);
        }

        [Test]
        public void ExitNeverSpawnsBelowExitMinDepth_UnlessLastSlot()
        {
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { combat }, exit, exitBaseChance: 1f, exitChancePerReveal: 1f, exitMinDepth: 4);

            const int totalSlots = 6;
            for (ulong seed = 1UL; seed <= 50UL; seed++)
            {
                ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(seed), totalSlots);
                for (int i = 0; i < totalSlots - 1; i++)
                {
                    ArafRoomDefinition rolled = roller.Roll(slotDepth: 3);
                    Assert.AreSame(combat, rolled);
                }

                ArafRoomDefinition finalSlot = roller.Roll(slotDepth: 2);
                Assert.AreSame(exit, finalSlot);
            }
        }

        [Test]
        public void CurrentExitChance_IncreasesPerNonExitReveal_AndCapsAtOne()
        {
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { combat }, exit, exitBaseChance: 0.25f, exitChancePerReveal: 0.40f, exitMinDepth: 10);

            ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(7UL), totalSlots: 6);
            Assert.AreEqual(0.25f, roller.CurrentExitChance, 1e-5f);

            roller.Roll(slotDepth: 1);
            Assert.AreEqual(0.65f, roller.CurrentExitChance, 1e-5f);

            roller.Roll(slotDepth: 1);
            Assert.AreEqual(1.0f, roller.CurrentExitChance, 1e-5f);

            roller.Roll(slotDepth: 1);
            Assert.AreEqual(1.0f, roller.CurrentExitChance, 1e-5f);
        }

        [Test]
        public void MaxPerRun_IsRespected_WithFallbackWhenExhausted()
        {
            ArafRoomDefinition shrine = CreateDefinition("Shrine", ArafRoomKind.Shrine, weight: 100f, minDepth: 0, maxPerRun: 1);
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f, minDepth: 0, maxPerRun: 2);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { shrine, combat }, exit, exitBaseChance: 0f, exitChancePerReveal: 0f, exitMinDepth: 99);

            ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(99UL), totalSlots: 5);

            int shrineCount = 0;
            int combatCount = 0;
            for (int i = 0; i < 3; i++)
            {
                ArafRoomDefinition rolled = roller.Roll(slotDepth: 2);
                if (ReferenceEquals(rolled, shrine)) shrineCount++;
                else if (ReferenceEquals(rolled, combat)) combatCount++;
            }

            Assert.AreEqual(1, shrineCount);
            Assert.AreEqual(2, combatCount);

            // Both non-exit definitions have reached MaxPerRun; fallback still picks a non-exit room before the last slot.
            ArafRoomDefinition fallbackRoll = roller.Roll(slotDepth: 2);
            Assert.IsTrue(ReferenceEquals(fallbackRoll, shrine) || ReferenceEquals(fallbackRoll, combat));
        }

        [Test]
        public void MinDepth_IsRespected_WithFallbackWhenNoneQualifies()
        {
            ArafRoomDefinition shallow = CreateDefinition("Shallow", ArafRoomKind.Combat, weight: 1f, minDepth: 2, maxPerRun: 99);
            ArafRoomDefinition deep = CreateDefinition("Deep", ArafRoomKind.Ambush, weight: 100f, minDepth: 5, maxPerRun: 99);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { shallow, deep }, exit, exitBaseChance: 0f, exitChancePerReveal: 0f, exitMinDepth: 99);

            ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(123UL), totalSlots: 10);

            for (int i = 0; i < 5; i++)
            {
                ArafRoomDefinition rolled = roller.Roll(slotDepth: 3);
                Assert.AreSame(shallow, rolled);
            }

            // At depth 1 neither room satisfies MinDepth; fallback ignores limits and still returns a non-exit room.
            ArafRoomDefinition fallback = roller.Roll(slotDepth: 1);
            Assert.IsNotNull(fallback);
            Assert.AreNotSame(exit, fallback);
        }

        [Test]
        public void Roll_IsDeterministic_ForSameSeed()
        {
            ArafRoomDefinition a = CreateDefinition("A", ArafRoomKind.Combat, weight: 1f);
            ArafRoomDefinition b = CreateDefinition("B", ArafRoomKind.Ambush, weight: 2f);
            ArafRoomDefinition c = CreateDefinition("C", ArafRoomKind.Ossuary, weight: 1.5f, minDepth: 2, maxPerRun: 2);
            ArafRoomDefinition exit = CreateDefinition("Exit", ArafRoomKind.Exit, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { a, b, c }, exit, 0.12f, 0.18f, 2);

            const int totalSlots = 8;
            ArafRoomRoller roller1 = new ArafRoomRoller(table, new RunRandom(2026UL), totalSlots);
            ArafRoomRoller roller2 = new ArafRoomRoller(table, new RunRandom(2026UL), totalSlots);

            for (int i = 0; i < totalSlots; i++)
            {
                int depth = (i % 4) + 1;
                Assert.AreSame(roller1.Roll(depth), roller2.Roll(depth));
                Assert.AreEqual(roller1.CurrentExitChance, roller2.CurrentExitChance, 1e-6f);
                Assert.AreEqual(roller1.ExitSpawned, roller2.ExitSpawned);
            }
        }
    }
}
