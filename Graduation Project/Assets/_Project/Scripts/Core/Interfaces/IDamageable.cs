using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Hasar alabilen tüm varlıklar (Düşmanlar, Test Mankenleri, Kırılabilir Objeler) için standart arayüz.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Nesneye hasar uygular.
        /// </summary>
        /// <param name="amount">Uygulanan hasar miktarı</param>
        /// <param name="hitPoint">Darbe noktası</param>
        /// <param name="hitNormal">Darbe yüzey normali</param>
        /// <param name="isCritical">Kritik vuruş mu (örn. kafadan vuruş)?</param>
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false);
    }
}
