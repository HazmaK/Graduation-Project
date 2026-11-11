using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Oda içi tetikleyici (pusu veya tapınak alanı) çarpışmalarını bağlı olduğu <see cref="ArafRoom"/> bileşenine iletir.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArafRoomTriggerRelay : MonoBehaviour
    {
        private ArafRoom _owner;

        /// <summary>Tetikleyici olaylarının iletileceği hedef odayı bağlar.</summary>
        public void Bind(ArafRoom owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_owner != null)
            {
                _owner.HandleActivationTriggerEnter(other);
            }
        }
    }
}
