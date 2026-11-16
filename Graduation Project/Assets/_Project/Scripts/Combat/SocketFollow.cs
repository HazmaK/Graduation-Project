using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Drag a model bone into Target to follow replacement art; an empty target preserves the authored socket.</summary>
    [DisallowMultipleComponent]
    public sealed class SocketFollow : MonoBehaviour
    {
        [Tooltip("Model bone to follow; leave empty to retain the authored pose.")]
        [SerializeField] private Transform target;
        [Tooltip("Also follow the target's world rotation.")]
        [SerializeField] private bool followRotation = true;

        /// <summary>The optional model bone followed by this socket.</summary>
        public Transform Target => target;

        /// <summary>Assigns a replacement model bone, or null to stop following.</summary>
        public void SetTarget(Transform replacement) => target = replacement;

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position;
            if (followRotation) transform.rotation = target.rotation;
        }
    }
}
