using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Allocation-free ownership of eight evenly spaced positions around a target.</summary>
    public sealed class MeleeSurroundSlots
    {
        /// <summary>Number of available positions.</summary>
        public const int SlotCount = 8;
        private readonly int[] _owners = new int[SlotCount];
        private static readonly MeleeSurroundSlots _shared = new MeleeSurroundSlots();
        private static int _nextOwnerKey;

        /// <summary>Shared runtime slot pool, cleared at subsystem registration.</summary>
        public static MeleeSurroundSlots Shared => _shared;

        /// <summary>Returns a monotonically increasing positive owner key; throws on integer exhaustion.</summary>
        public static int NextOwnerKey() => checked(++_nextOwnerKey);

        /// <summary>Keeps an existing claim, or claims the closest free horizontal direction; returns -1 if full or invalid.</summary>
        public int Claim(int ownerKey, Vector3 target, Vector3 self)
        {
            if (ownerKey <= 0) return -1;
            int current = SlotOf(ownerKey);
            if (current >= 0) return current;
            Vector3 direction = self - target;
            direction.y = 0f;
            int best = -1;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < SlotCount; i++)
            {
                if (_owners[i] != 0) continue;
                float dot = Vector3.Dot(direction, SlotDirection(i));
                if (best < 0 || dot > bestDot)
                {
                    best = i;
                    bestDot = dot;
                }
            }
            if (best >= 0) _owners[best] = ownerKey;
            return best;
        }

        /// <summary>Releases the owner's claim, if any.</summary>
        public void Release(int ownerKey)
        {
            int slot = SlotOf(ownerKey);
            if (slot >= 0) _owners[slot] = 0;
        }

        /// <summary>Returns the owner's slot or -1 when it has no claim.</summary>
        public int SlotOf(int ownerKey)
        {
            if (ownerKey <= 0) return -1;
            for (int i = 0; i < SlotCount; i++)
                if (_owners[i] == ownerKey) return i;
            return -1;
        }

        /// <summary>Returns the unit XZ direction at slot * 45 degrees, with slot zero at +Z.</summary>
        public static Vector3 SlotDirection(int slot)
        {
            float angle = slot * (360f / SlotCount) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        /// <summary>Returns a slot position at the target's height and supplied radius.</summary>
        public static Vector3 SlotPosition(int slot, Vector3 target, float radius) =>
            target + SlotDirection(slot) * radius;

        /// <summary>Releases all claims.</summary>
        public void Clear()
        {
            for (int i = 0; i < SlotCount; i++) _owners[i] = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _shared.Clear();
            _nextOwnerKey = 0;
        }
    }
}
