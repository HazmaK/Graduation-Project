namespace RetroFPS.Combat.Navigation
{
    /// <summary>Reachability of the path to the target.</summary>
    public enum NavPathStatus
    {
        /// <summary>The enemy could not be projected onto a NavMesh.</summary>
        NoNavMesh,
        /// <summary>The path reaches its requested endpoint.</summary>
        Complete,
        /// <summary>The path stops short of its requested endpoint.</summary>
        Partial,
        /// <summary>No usable path exists.</summary>
        Unreachable
    }

    /// <summary>The navigation policy's current movement intent.</summary>
    public enum ChaseIntent
    {
        /// <summary>Steer directly without a NavMesh.</summary>
        Direct,
        /// <summary>Follow the path toward the target or reachable endpoint.</summary>
        Pursue,
        /// <summary>Wait at an accessible position around the target.</summary>
        Ambush
    }
}
