using System;

namespace RetroFPS.Stats
{
    /// <summary>Fixed-capacity modifier storage with allocation-free evaluation.</summary>
    public sealed class StatSheet
    {
        /// <summary>Maximum number of simultaneous modifiers.</summary>
        public const int MaxModifiers = 64;

        private readonly StatModifier[] _modifiers = new StatModifier[MaxModifiers];
        private readonly int[] _sources = new int[MaxModifiers];
        private IStatContext _context;
        private int _count;
        private int _version;
        private int _batchDepth;
        private bool _batchDirty;

        /// <summary>Live condition context, or null when unset.</summary>
        public IStatContext Context => _context;
        /// <summary>Number of stored modifiers.</summary>
        public int Count => _count;
        /// <summary>Revision incremented when storage or the context reference changes.</summary>
        public int Version => _version;
        /// <summary>Raised after a storage or context reference change.</summary>
        public event Action OnChanged;

        /// <summary>Defers <see cref="OnChanged"/> until the matching <see cref="EndBatch"/>; supports nesting.</summary>
        public void BeginBatch() => _batchDepth++;

        /// <summary>Closes the current batch and raises <see cref="OnChanged"/> once if any mutation occurred.</summary>
        public void EndBatch()
        {
            if (_batchDepth == 0) return;
            _batchDepth--;
            if (_batchDepth == 0 && _batchDirty)
            {
                _batchDirty = false;
                OnChanged?.Invoke();
            }
        }

        /// <summary>Assigns the live condition context; null disables conditional modifiers.</summary>
        public void SetContext(IStatContext context)
        {
            if (ReferenceEquals(_context, context)) return;
            _context = context;
            Changed();
        }

        /// <summary>
        /// Adds a finite modifier, returning false when capacity is exhausted, the value is non-finite,
        /// or a <see cref="StatId.MaxHealth"/> modifier uses a non-<see cref="StatCondition.Always"/> condition
        /// (health-based conditions depend on max health and would be circular).
        /// </summary>
        public bool Add(int sourceId, in StatModifier modifier)
        {
            if (_count == MaxModifiers || float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value))
                return false;
            if (modifier.Stat == StatId.MaxHealth && modifier.Condition != StatCondition.Always)
                return false;
            _sources[_count] = sourceId;
            _modifiers[_count++] = modifier;
            Changed();
            return true;
        }

        /// <summary>Removes all modifiers from a source and returns the number removed.</summary>
        public int RemoveSource(int sourceId)
        {
            int kept = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_sources[i] == sourceId) continue;
                _sources[kept] = _sources[i];
                _modifiers[kept++] = _modifiers[i];
            }
            int removed = _count - kept;
            if (removed == 0) return 0;
            Array.Clear(_sources, kept, removed);
            Array.Clear(_modifiers, kept, removed);
            _count = kept;
            Changed();
            return removed;
        }

        /// <summary>Removes all modifiers while preserving the live context.</summary>
        public void Clear()
        {
            if (_count == 0) return;
            Array.Clear(_sources, 0, _count);
            Array.Clear(_modifiers, 0, _count);
            _count = 0;
            Changed();
        }

        /// <summary>Applies flat, additive percentage, then multiplicative adjustments, clamped at zero.</summary>
        public float Evaluate(StatId stat, float baseValue)
        {
            float flat = 0f;
            float percent = 0f;
            float multiply = 1f;
            for (int i = 0; i < _count; i++)
            {
                ref readonly StatModifier modifier = ref _modifiers[i];
                if (modifier.Stat != stat || !Matches(modifier.Condition)) continue;
                switch (modifier.Op)
                {
                    case StatOp.AddFlat: flat += modifier.Value; break;
                    case StatOp.AddPercent: percent += modifier.Value; break;
                    case StatOp.Multiply: multiply *= modifier.Value; break;
                }
            }
            return Math.Max(0f, (baseValue + flat) * Math.Max(0f, 1f + percent) * multiply);
        }

        private bool Matches(StatCondition condition)
        {
            switch (condition)
            {
                case StatCondition.Always: return true;
                case StatCondition.HealthBelowHalf: return _context != null && _context.HealthFraction < 0.5f;
                case StatCondition.Overhealed: return _context != null && _context.IsOverhealed;
                default: return false;
            }
        }

        private void Changed()
        {
            unchecked { _version++; }
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }
            OnChanged?.Invoke();
        }
    }
}
