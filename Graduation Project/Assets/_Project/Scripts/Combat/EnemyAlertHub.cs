using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Sürü alarm sistemine dahil olan tüm düşman tiplerinin (Yerdeki Ghoullar, Uçan Gözler vb.)
    /// ortak uyguladığı arayüz.
    /// </summary>
    public interface IAlertableEnemy
    {
        Vector3 Position { get; }
        bool IsDead { get; }
        void ReceiveAlert(Transform target);
    }

    /// <summary>
    /// Sürü Zekası İletişim Merkezi (Pack Alert Hub).
    /// Farklı düşman türlerinin (Ghoul, Uçan Göz vb.) birbirlerini sıfır GC tahsisiyle
    /// belirli bir mesafe içinde alarma geçirmesini sağlar.
    /// </summary>
    public static class EnemyAlertHub
    {
        private static readonly List<IAlertableEnemy> RegisteredEnemies = new List<IAlertableEnemy>();

        public static void Register(IAlertableEnemy enemy)
        {
            if (!RegisteredEnemies.Contains(enemy))
            {
                RegisteredEnemies.Add(enemy);
            }
        }

        public static void Unregister(IAlertableEnemy enemy)
        {
            RegisteredEnemies.Remove(enemy);
        }

        /// <summary>
        /// Belirli bir yarıçap içerisindeki tüm müttefikleri oyuncuya karşı alarma geçirir.
        /// </summary>
        public static void BroadcastAlert(IAlertableEnemy sender, Vector3 sourcePos, Transform target, float radius)
        {
            if (target == null) return;

            for (int i = 0; i < RegisteredEnemies.Count; i++)
            {
                var enemy = RegisteredEnemies[i];
                if (enemy == null || enemy == sender || enemy.IsDead) continue;

                float dist = Vector3.Distance(sourcePos, enemy.Position);
                if (dist <= radius)
                {
                    enemy.ReceiveAlert(target);
                }
            }
        }
    }
}
