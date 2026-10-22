using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Karşılaşma ve Dalga Yöneticisi (WaveSpawnDirector) tarafından yönetilen
    /// tüm düşman tiplerinin (yer/uçan/boss) uyguladığı optimize arayüz.
    /// Sıfır GC tahsisli olay dinleme ve nesne havuzu (Object Pool) yaşam döngüsünü yönetir.
    /// </summary>
    public interface IEncounterEnemy
    {
        /// <summary>
        /// Düşman öldüğünde tetiklenen sıfır GC olay delegesi.
        /// </summary>
        event System.Action<IEncounterEnemy> OnEnemyDeath;

        /// <summary>
        /// Düşmanın şu anda hayatta ve aktif olup olmadığını bildirir.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Düşmanın Transform bileşeni (Önbellek için).
        /// </summary>
        Transform EnemyTransform { get; }

        /// <summary>
        /// Düşmanın GameObject bileşeni.
        /// </summary>
        GameObject EnemyGameObject { get; }

        /// <summary>
        /// WaveSpawnDirector tarafından havuzdan alınıp belirli bir konum ve rotasyonda uyandırıldığında çağrılır.
        /// </summary>
        /// <param name="spawnPosition">Doğuş dünya koordinatı.</param>
        /// <param name="spawnRotation">Doğuş rotasyonu.</param>
        void OnSpawnFromDirector(Vector3 spawnPosition, Quaternion spawnRotation);

        /// <summary>
        /// Dalga sonlandığında veya havuz geri dönüşümünde düşmanı pasife almak için çağrılır.
        /// </summary>
        void OnDespawnToDirector();
    }
}
