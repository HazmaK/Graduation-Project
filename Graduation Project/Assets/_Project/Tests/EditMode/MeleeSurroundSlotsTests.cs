using System.Reflection;
using NUnit.Framework;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Deterministic surround geometry and exclusive slot ownership.</summary>
    public sealed class MeleeSurroundSlotsTests
    {
        [TearDown]
        public void TearDown() => MeleeSurroundSlots.Shared.Clear();

        [TestCase(0, 0f, 1f)]
        [TestCase(2, 1f, 0f)]
        [TestCase(4, 0f, -1f)]
        [TestCase(6, -1f, 0f)]
        public void SlotDirection_FollowsClockwiseCardinals(int slot, float x, float z)
        {
            Vector3 direction = MeleeSurroundSlots.SlotDirection(slot);
            Assert.That(direction.x, Is.EqualTo(x).Within(0.0001f));
            Assert.That(direction.y, Is.Zero);
            Assert.That(direction.z, Is.EqualTo(z).Within(0.0001f));
        }

        [Test]
        public void EverySlot_IsUnitLength_AndPositionPreservesTargetHeight()
        {
            Vector3 target = new Vector3(4f, 7f, -9f);
            for (int i = 0; i < MeleeSurroundSlots.SlotCount; i++)
            {
                Assert.That(MeleeSurroundSlots.SlotDirection(i).magnitude, Is.EqualTo(1f).Within(0.0001f));
                Vector3 position = MeleeSurroundSlots.SlotPosition(i, target, 3f);
                Assert.That(position.y, Is.EqualTo(target.y));
                Assert.That(Vector3.Distance(position, target), Is.EqualTo(3f).Within(0.0001f));
                var slots = new MeleeSurroundSlots();
                Assert.That(slots.Claim(1, target, position), Is.EqualTo(i));
            }
        }

        [Test]
        public void Claim_KeepsOwnershipDespiteTargetAndSelfMovement()
        {
            var slots = new MeleeSurroundSlots();
            Assert.That(slots.Claim(10, Vector3.zero, new Vector3(2f, 100f, 0f)), Is.EqualTo(2));
            Assert.That(slots.Claim(10, Vector3.one, Vector3.back), Is.EqualTo(2));
            Assert.That(slots.SlotOf(10), Is.EqualTo(2));
            slots.Release(10);
            Assert.That(slots.SlotOf(10), Is.EqualTo(-1));
            Assert.That(slots.Claim(10, Vector3.zero, Vector3.back), Is.EqualTo(4));
        }

        [Test]
        public void FullPool_RejectsNewOwner_AndReusesReleasedSlot()
        {
            var slots = new MeleeSurroundSlots();
            bool[] seen = new bool[MeleeSurroundSlots.SlotCount];
            for (int owner = 1; owner <= MeleeSurroundSlots.SlotCount; owner++)
            {
                int slot = slots.Claim(owner, Vector3.zero, Vector3.forward);
                Assert.That(slot, Is.InRange(0, MeleeSurroundSlots.SlotCount - 1));
                Assert.That(seen[slot], Is.False);
                seen[slot] = true;
            }
            Assert.That(slots.Claim(99, Vector3.zero, Vector3.forward), Is.EqualTo(-1));
            Assert.That(slots.Claim(1, Vector3.zero, Vector3.back), Is.EqualTo(0));
            int released = slots.SlotOf(4);
            slots.Release(4);
            Assert.That(slots.Claim(99, Vector3.zero, Vector3.forward), Is.EqualTo(released));
            slots.Release(999);
            slots.Clear();
            for (int owner = 1; owner <= MeleeSurroundSlots.SlotCount; owner++)
                Assert.That(slots.SlotOf(owner), Is.EqualTo(-1));
            Assert.That(slots.SlotOf(99), Is.EqualTo(-1));
        }

        [Test]
        public void ClosestFreeSlot_IsSelectedWhenPreferredSlotIsOccupied()
        {
            var slots = new MeleeSurroundSlots();
            Assert.That(slots.Claim(1, Vector3.zero, Vector3.forward), Is.Zero);
            Assert.That(slots.Claim(2, Vector3.zero, new Vector3(0.2f, 0f, 1f)), Is.EqualTo(1));
        }

        [Test]
        public void InvalidOwners_DoNotClaimEmptySlots_AndCoincidentPositionIsDeterministic()
        {
            var slots = new MeleeSurroundSlots();
            Assert.That(slots.Claim(0, Vector3.zero, Vector3.zero), Is.EqualTo(-1));
            Assert.That(slots.Claim(-1, Vector3.zero, Vector3.zero), Is.EqualTo(-1));
            slots.Release(0);
            Assert.That(slots.SlotOf(0), Is.EqualTo(-1));
            Assert.That(slots.Claim(1, Vector3.zero, Vector3.zero), Is.Zero);
        }

        [Test]
        public void OwnerKeys_ArePositiveAndIncreasing_AndSubsystemResetClearsSharedClaims()
        {
            int first = MeleeSurroundSlots.NextOwnerKey();
            int second = MeleeSurroundSlots.NextOwnerKey();
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.GreaterThan(first));
            MeleeSurroundSlots shared = MeleeSurroundSlots.Shared;
            shared.Clear();
            shared.Claim(first, Vector3.zero, Vector3.forward);
            typeof(MeleeSurroundSlots).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            Assert.That(MeleeSurroundSlots.Shared.SlotOf(first), Is.EqualTo(-1));
            Assert.That(MeleeSurroundSlots.NextOwnerKey(), Is.EqualTo(1));
        }
    }
}
