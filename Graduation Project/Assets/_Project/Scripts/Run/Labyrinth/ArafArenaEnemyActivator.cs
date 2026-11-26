using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Arena ile düşman yapay zekâsı arasındaki tek bağdaştırıcı.
    /// Sıra: <see cref="Activate"/> (OnSpawnFromDirector) → elit ölçekleme → <see cref="Hunt"/> (BeginHunt).
    /// </summary>
    public static class ArafArenaEnemyActivator
    {
        /// <summary>Düşmanı yönetmen doğuşu olarak başlatır; av henüz başlamaz.</summary>
        public static void Activate(GameObject enemy, Vector3 position, Quaternion rotation)
        {
            if (enemy == null) return;
            if (enemy.TryGetComponent(out IEncounterEnemy encounter)) encounter.OnSpawnFromDirector(position, rotation);
        }

        /// <summary>
        /// Avı başlatır veya sürdürür. <see cref="IHuntableEnemy"/> varsa ve av durmuşsa yeniden başlatılır;
        /// yoksa eski <see cref="IAlertableEnemy.ReceiveAlert"/> yoluna düşülür.
        /// </summary>
        public static void Hunt(IHuntableEnemy huntable, IAlertableEnemy alertable, Transform player, EnemyAggressionProfile profile)
        {
            if (player == null) return;
            if (huntable != null)
            {
                if (!huntable.IsHunting && (alertable == null || !alertable.IsDead)) huntable.BeginHunt(player, profile);
                return;
            }
            if (alertable != null && !alertable.IsDead) alertable.ReceiveAlert(player);
        }
    }
}
