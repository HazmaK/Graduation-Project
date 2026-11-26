using UnityEngine;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Policy for partial and unreachable target paths.</summary>
    public interface IUnreachableTargetResponse
    {
        /// <summary>Resolves a partial or unreachable path into an intent and alternative destination.</summary>
        ChaseIntent Resolve(Vector3 self, Vector3 target, NavPathStatus status, Vector3 pathEnd, out Vector3 destination);
        /// <summary>Forgets cached state after a respawn or state change.</summary>
        void Reset();
    }
}
