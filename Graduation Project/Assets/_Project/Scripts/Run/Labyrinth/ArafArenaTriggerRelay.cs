using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Arena iç tetikleyicisinin girişini karşılaşma denetleyicisine iletir.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafArenaTriggerRelay : MonoBehaviour
    {
        private ArafArena _owner;
        /// <summary>Tetikleyicinin bağlı olduğu arenayı kaydeder.</summary>
        public void Bind(ArafArena owner) => _owner = owner;
        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null) _owner.HandleActivationTriggerEnter(other);
        }
    }
}
