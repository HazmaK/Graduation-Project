using System;

namespace RetroFPS.Weapons
{
    /// <summary>Fixed-capacity, allocation-free per-target harvest decisions; full ledgers fail closed.</summary>
    public sealed class BloodHarvestTracker
    {
        private struct Entry
        {
            internal object Target;
            internal uint StunSequence;
            internal bool HasStunHarvest;
        }

        private readonly Entry[] _entries;
        private int _count;

        /// <summary>Allocates bookkeeping once when the weapon initializes.</summary>
        public BloodHarvestTracker(int capacity) => _entries = new Entry[Math.Max(1, capacity)];

        /// <summary>Grants a living target once per accepted stun, or a kill on its alive-to-dead edge.</summary>
        public bool TryHarvest(object target, bool wasAlive, bool isAlive, bool wasStunned, uint stunSequence)
        {
            if (target == null || !wasAlive) return false;
            // An actual kill is observable only once; subsequent hits snapshot wasAlive=false.
            if (!isAlive) return true;
            if (!wasStunned) return false;
            int slot = 0;
            for (; slot < _count; slot++)
                if (ReferenceEquals(_entries[slot].Target, target)) break;
            if (slot == _count)
            {
                if (_count == _entries.Length) return false;
                _entries[slot].Target = target;
                _count++;
            }
            if (_entries[slot].HasStunHarvest && _entries[slot].StunSequence == stunSequence) return false;
            _entries[slot].StunSequence = stunSequence;
            _entries[slot].HasStunHarvest = true;
            return true;
        }
    }
}
