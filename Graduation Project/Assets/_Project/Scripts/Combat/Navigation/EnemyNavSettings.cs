using UnityEngine;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Serialized tuning shared by path following and unreachable-target policy.</summary>
    [System.Serializable]
    public sealed class EnemyNavSettings
    {
        [SerializeField, Tooltip("Seconds between periodic path plans.")] private float repathInterval = 0.4f;
        [SerializeField, Tooltip("Target movement in metres that triggers a new plan.")] private float targetMoveThreshold = 1.5f;
        [SerializeField, Tooltip("Horizontal corner arrival distance in metres.")] private float cornerReachDistance = 0.6f;
        [SerializeField, Tooltip("Self and ambush projection distance in metres.")] private float sampleMaxDistance = 2.5f;
        [SerializeField, Tooltip("Extended target projection distance in metres.")] private float targetSampleMaxDistance = 6f;
        [SerializeField, Tooltip("Maximum vertical melee reach in metres.")] private float reachableHeightTolerance = 1.6f;
        [SerializeField, Tooltip("Horizontal attack radius around a partial endpoint.")] private float partialEndAttackRadius = 2.2f;
        [SerializeField, Tooltip("Minimum ambush ring radius in metres.")] private float ambushRadiusMin = 5f;
        [SerializeField, Tooltip("Maximum ambush ring radius in metres.")] private float ambushRadiusMax = 7f;
        [SerializeField, Tooltip("Number of deterministic ambush ring samples.")] private int ambushSampleCount = 8;
        [SerializeField, Tooltip("Horizontal target movement before picking a new ambush point.")] private float ambushRefreshDistance = 3f;

        /// <summary>Seconds between periodic plans.</summary>
        public float RepathInterval => repathInterval;
        /// <summary>Target movement required to trigger a plan.</summary>
        public float TargetMoveThreshold => targetMoveThreshold;
        /// <summary>Horizontal corner arrival distance.</summary>
        public float CornerReachDistance => cornerReachDistance;
        /// <summary>Self and ambush projection distance.</summary>
        public float SampleMaxDistance => sampleMaxDistance;
        /// <summary>Extended target projection distance.</summary>
        public float TargetSampleMaxDistance => targetSampleMaxDistance;
        /// <summary>Inclusive vertical melee reach.</summary>
        public float ReachableHeightTolerance => reachableHeightTolerance;
        /// <summary>Horizontal attack radius at a partial endpoint.</summary>
        public float PartialEndAttackRadius => partialEndAttackRadius;
        /// <summary>Minimum ambush ring radius.</summary>
        public float AmbushRadiusMin => ambushRadiusMin;
        /// <summary>Maximum ambush ring radius.</summary>
        public float AmbushRadiusMax => ambushRadiusMax;
        /// <summary>Number of ambush samples.</summary>
        public int AmbushSampleCount => ambushSampleCount;
        /// <summary>Horizontal movement before refreshing the ambush anchor.</summary>
        public float AmbushRefreshDistance => ambushRefreshDistance;
    }
}
