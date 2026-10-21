using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Parry, ağır darbe veya patlamalarla sersemletilebilen tüm düşmanlar için ortak arayüz.
    /// Sistemler arası gevşek bağlılık (Decoupling) ve yeniden kullanılabilirlik sağlar.
    /// </summary>
    public interface IStunnable
    {
        /// <summary>
        /// Düşmanı belirtilen süre boyunca hareketten ve saldırıdan men eder (Stun).
        /// </summary>
        /// <param name="duration">Sersemleme süresi (saniye)</param>
        /// <param name="knockbackDir">Geriye savrulma yönü</param>
        void Stun(float duration, Vector3 knockbackDir);
    }
}
