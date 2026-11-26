using UnityEngine;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Allocation-free projection and path queries.</summary>
    public interface INavMeshQuery
    {
        /// <summary>Projects position within maxDistance; returns false if nothing is found.</summary>
        bool SamplePosition(Vector3 position, float maxDistance, out Vector3 hit);

        /// <summary>Writes valid corners into the supplied buffer; unreachable paths have zero corners.</summary>
        NavPathStatus CalculatePath(Vector3 from, Vector3 to, Vector3[] corners, out int count);
    }
}
