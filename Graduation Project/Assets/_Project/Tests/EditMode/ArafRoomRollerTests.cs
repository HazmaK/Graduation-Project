using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Rol ve ağırlık temelli oda seçiminin testleri.</summary>
    public class ArafRoomRollerTests
    {
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        /// <summary>Geçici test varlıklarını temizler.</summary>
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

        private ArafRoomTable CreateTable(ArafRoomDefinition[] rooms, ArafRoomDefinition sealRoom, ArafRoomDefinition arenaRoom = null)
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
            so.FindProperty("sealRoom").objectReferenceValue = sealRoom;
            so.FindProperty("arenaRoom").objectReferenceValue = arenaRoom;
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        /// <summary>İsteğe bağlı arena rolü ağırlık, derinlik ve adet sınırından bağımsızdır.</summary>
        [Test]
        public void OptionalArenaAlwaysReturnsArenaDefinition()
        {
            var calm = CreateDefinition("Calm", ArafRoomKind.Ossuary);
            var seal = CreateDefinition("Seal", ArafRoomKind.Seal);
            var arena = CreateDefinition("Arena", ArafRoomKind.Arena, weight: 0f, minDepth: 99, maxPerRun: 0);
            var table = CreateTable(new[] { calm }, seal, arena);
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var roller = new ArafRoomRoller(table, new RunRandom(seed), 7);
                for (int depth = 0; depth < 7; depth++)
                    Assert.That(roller.Roll(depth, ArafRoomRole.OptionalArena), Is.SameAs(arena));
                Assert.That(roller.RevealedCount, Is.EqualTo(7));
            }
        }

        /// <summary>Oda seçimi sözleşmesinin ilgili sınırlarını doğrular.</summary>
        [Test]
        public void SealSlot_AlwaysReturnsSealRoom_AcrossDepthsAndSeeds()
        {
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 100f);
            ArafRoomDefinition ambush = CreateDefinition("Ambush", ArafRoomKind.Ambush, weight: 50f);
            ArafRoomDefinition seal = CreateDefinition("Seal", ArafRoomKind.Seal, weight: 1f, minDepth: 99, maxPerRun: 1);
            ArafRoomTable table = CreateTable(new[] { combat, ambush }, seal);

            for (ulong seed = 1UL; seed <= 50UL; seed++)
            {
                ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(seed), totalSlots: 7);
                Assert.AreSame(seal, roller.Roll(slotDepth: 0, role: ArafRoomRole.SealArena));
                Assert.AreSame(seal, roller.Roll(slotDepth: 2, role: ArafRoomRole.SealArena));
                Assert.AreSame(seal, roller.Roll(slotDepth: 10, role: ArafRoomRole.SealArena));
                Assert.AreEqual(3, roller.RevealedCount);
            }
        }

        /// <summary>Oda seçimi sözleşmesinin ilgili sınırlarını doğrular.</summary>
        [Test]
        public void NonSealSlot_NeverReturnsSealRoom_EvenWhenPoolEmptyOrExhausted()
        {
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f, maxPerRun: 1);
            ArafRoomDefinition seal = CreateDefinition("Seal", ArafRoomKind.Seal, weight: 1f);
            ArafRoomTable populated = CreateTable(new[] { combat }, seal);
            ArafRoomTable empty = CreateTable(new ArafRoomDefinition[0], seal);

            for (ulong seed = 1UL; seed <= 40UL; seed++)
            {
                ArafRoomRoller roller = new ArafRoomRoller(populated, new RunRandom(seed), totalSlots: 7);
                for (int i = 0; i < 7; i++)
                {
                    ArafRoomDefinition rolled = roller.Roll(slotDepth: i, role: ArafRoomRole.Calm);
                    Assert.AreSame(combat, rolled);
                    Assert.AreNotSame(seal, rolled);
                }

                ArafRoomRoller emptyRoller = new ArafRoomRoller(empty, new RunRandom(seed), totalSlots: 4);
                Assert.IsNull(emptyRoller.Roll(slotDepth: 3, role: ArafRoomRole.Calm));
                Assert.AreSame(seal, emptyRoller.Roll(slotDepth: 3, role: ArafRoomRole.SealArena));
            }
        }

        /// <summary>Oda seçimi sözleşmesinin ilgili sınırlarını doğrular.</summary>
        [Test]
        public void MaxPerRun_IsRespected_WithFallbackWhenExhausted()
        {
            ArafRoomDefinition shrine = CreateDefinition("Shrine", ArafRoomKind.Shrine, weight: 100f, minDepth: 0, maxPerRun: 1);
            ArafRoomDefinition combat = CreateDefinition("Combat", ArafRoomKind.Combat, weight: 1f, minDepth: 0, maxPerRun: 2);
            ArafRoomDefinition seal = CreateDefinition("Seal", ArafRoomKind.Seal, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { shrine, combat }, seal);

            ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(99UL), totalSlots: 5);

            int shrineCount = 0;
            int combatCount = 0;
            for (int i = 0; i < 3; i++)
            {
                ArafRoomDefinition rolled = roller.Roll(slotDepth: 2, role: ArafRoomRole.Calm);
                if (ReferenceEquals(rolled, shrine)) shrineCount++;
                else if (ReferenceEquals(rolled, combat)) combatCount++;
            }

            Assert.AreEqual(1, shrineCount);
            Assert.AreEqual(2, combatCount);

            // Both normal definitions have reached MaxPerRun; fallback still picks a normal room, never SealRoom.
            ArafRoomDefinition fallbackRoll = roller.Roll(slotDepth: 2, role: ArafRoomRole.Calm);
            Assert.IsTrue(ReferenceEquals(fallbackRoll, shrine) || ReferenceEquals(fallbackRoll, combat));
            Assert.AreNotSame(seal, fallbackRoll);
        }

        /// <summary>Oda seçimi sözleşmesinin ilgili sınırlarını doğrular.</summary>
        [Test]
        public void MinDepth_IsRespected_WithFallbackWhenNoneQualifies()
        {
            ArafRoomDefinition shallow = CreateDefinition("Shallow", ArafRoomKind.Combat, weight: 1f, minDepth: 2, maxPerRun: 99);
            ArafRoomDefinition deep = CreateDefinition("Deep", ArafRoomKind.Ambush, weight: 100f, minDepth: 5, maxPerRun: 99);
            ArafRoomDefinition seal = CreateDefinition("Seal", ArafRoomKind.Seal, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { shallow, deep }, seal);

            ArafRoomRoller roller = new ArafRoomRoller(table, new RunRandom(123UL), totalSlots: 10);

            for (int i = 0; i < 5; i++)
            {
                ArafRoomDefinition rolled = roller.Roll(slotDepth: 3, role: ArafRoomRole.Calm);
                Assert.AreSame(shallow, rolled);
            }

            // At depth 1 neither room satisfies MinDepth; fallback ignores limits and still returns a non-seal room.
            ArafRoomDefinition fallback = roller.Roll(slotDepth: 1, role: ArafRoomRole.Calm);
            Assert.IsNotNull(fallback);
            Assert.AreNotSame(seal, fallback);
        }

        /// <summary>Oda seçimi sözleşmesinin ilgili sınırlarını doğrular.</summary>
        [Test]
        public void Roll_IsDeterministic_ForSameSeed()
        {
            ArafRoomDefinition a = CreateDefinition("A", ArafRoomKind.Combat, weight: 1f);
            ArafRoomDefinition b = CreateDefinition("B", ArafRoomKind.Ambush, weight: 2f);
            ArafRoomDefinition c = CreateDefinition("C", ArafRoomKind.Ossuary, weight: 1.5f, minDepth: 2, maxPerRun: 2);
            ArafRoomDefinition seal = CreateDefinition("Seal", ArafRoomKind.Seal, weight: 1f);
            ArafRoomTable table = CreateTable(new[] { a, b, c }, seal);

            const int totalSlots = 8;
            ArafRoomRoller roller1 = new ArafRoomRoller(table, new RunRandom(2026UL), totalSlots);
            ArafRoomRoller roller2 = new ArafRoomRoller(table, new RunRandom(2026UL), totalSlots);

            Assert.AreEqual(totalSlots, roller1.TotalSlots);
            for (int i = 0; i < totalSlots; i++)
            {
                int depth = (i % 4) + 1;
                bool isSeal = i == 1 || i == 4 || i == 7;
                Assert.AreSame(roller1.Roll(depth, isSeal ? ArafRoomRole.SealArena : ArafRoomRole.Calm), roller2.Roll(depth, isSeal ? ArafRoomRole.SealArena : ArafRoomRole.Calm));
                Assert.AreEqual(roller1.RevealedCount, roller2.RevealedCount);
            }
        }
    }
}
