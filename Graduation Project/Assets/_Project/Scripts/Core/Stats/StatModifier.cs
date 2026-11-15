using UnityEngine;

namespace RetroFPS.Stats
{
    /// <summary>How a modifier contributes to a statistic.</summary>
    public enum StatOp
    {
        /// <summary>Adds to the shared percentage bonus.</summary>
        AddPercent = 0,
        /// <summary>Multiplies the final value.</summary>
        Multiply = 1,
        /// <summary>Adds to the base before percentage bonuses.</summary>
        AddFlat = 2
    }

    /// <summary>Runtime requirement for a modifier to apply.</summary>
    public enum StatCondition
    {
        /// <summary>Applies without a context.</summary>
        Always = 0,
        /// <summary>Applies strictly below half effective maximum health.</summary>
        HealthBelowHalf = 1,
        /// <summary>Applies while overhealed.</summary>
        Overhealed = 2
    }

    /// <summary>Serialized, read-only description of one stat adjustment.</summary>
    [System.Serializable]
    public struct StatModifier
    {
        [SerializeField] private StatId stat;
        [SerializeField] private StatOp op;
        [SerializeField] private float value;
        [SerializeField] private StatCondition condition;

        /// <summary>Creates a modifier with an optional runtime condition.</summary>
        public StatModifier(StatId stat, StatOp op, float value, StatCondition condition = StatCondition.Always)
        {
            this.stat = stat;
            this.op = op;
            this.value = value;
            this.condition = condition;
        }

        /// <summary>Statistic to adjust.</summary>
        public StatId Stat => stat;
        /// <summary>Adjustment operation.</summary>
        public StatOp Op => op;
        /// <summary>Adjustment magnitude; percentages use fractions.</summary>
        public float Value => value;
        /// <summary>Requirement evaluated when the statistic is read.</summary>
        public StatCondition Condition => condition;
    }
}
