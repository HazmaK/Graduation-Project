using UnityEngine;
using UnityEngine.AI;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Unity NavMesh queries sharing one reusable native path.</summary>
    public sealed class UnityNavMeshQuery : INavMeshQuery
    {
        private readonly int areaMask;
        private readonly NavMeshPath path;

        /// <summary>Allocates the reusable path and selects the allowed navigation areas.</summary>
        public UnityNavMeshQuery(int areaMask = NavMesh.AllAreas)
        {
            this.areaMask = areaMask;
            path = new NavMeshPath();
        }

        /// <inheritdoc />
        public bool SamplePosition(Vector3 position, float maxDistance, out Vector3 hit)
        {
            bool found = NavMesh.SamplePosition(position, out NavMeshHit result, maxDistance, areaMask);
            hit = result.position;
            return found;
        }

        /// <inheritdoc />
        public NavPathStatus CalculatePath(Vector3 from, Vector3 to, Vector3[] corners, out int count)
        {
            count = 0;
            if (!NavMesh.CalculatePath(from, to, areaMask, path) || path.status == NavMeshPathStatus.PathInvalid)
                return NavPathStatus.Unreachable;

            count = path.GetCornersNonAlloc(corners);
            return path.status == NavMeshPathStatus.PathComplete ? NavPathStatus.Complete : NavPathStatus.Partial;
        }
    }
}
