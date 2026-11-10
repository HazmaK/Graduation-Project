using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Projenin fizik katmanlarının (Layer) ve bunlardan türetilen sorgu maskelerinin tek kaynağı.
    /// Katman isimleri ProjectSettings/TagManager.asset ile birebir eşleşmelidir.
    /// Değerler ilk erişimde bir kez çözülür ve önbelleğe alınır (sıfır GC).
    /// Not: MonoBehaviour alan başlatıcılarında (constructor) değil, Awake/Start/Update içinde kullanın.
    /// </summary>
    public static class GameLayers
    {
        public const string PlayerLayerName = "Player";
        public const string EnemyLayerName = "Enemy";
        public const string ProjectileLayerName = "Projectile";
        public const string PickupLayerName = "Pickup";

        private static bool _resolved;
        private static int _player, _enemy, _projectile, _pickup;
        private static int _playerAttackMask, _enemySightMask, _enemyObstacleMask, _enemyProjectileMask, _groundMask;

        /// <summary>Oyuncu katman indeksi (tanımlı değilse Default = 0).</summary>
        public static int Player { get { EnsureResolved(); return _player; } }

        /// <summary>Düşman katman indeksi (tanımlı değilse Default = 0).</summary>
        public static int Enemy { get { EnsureResolved(); return _enemy; } }

        /// <summary>Düşman mermisi katman indeksi (tanımlı değilse Default = 0).</summary>
        public static int Projectile { get { EnsureResolved(); return _projectile; } }

        /// <summary>Can küresi / cephane kutusu katman indeksi (tanımlı değilse Default = 0).</summary>
        public static int Pickup { get { EnsureResolved(); return _pickup; } }

        /// <summary>Oyuncu silahları ve parry: oyuncunun kendisi, pickup'lar ve Ignore Raycast hariç her şey.</summary>
        public static int PlayerAttackMask { get { EnsureResolved(); return _playerAttackMask; } }

        /// <summary>Düşman görüş hattı: diğer düşmanlar, mermiler, pickup'lar ve Ignore Raycast hariç.</summary>
        public static int EnemySightMask { get { EnsureResolved(); return _enemySightMask; } }

        /// <summary>Düşman engelden kaçınma: oyuncu, mermiler, pickup'lar ve Ignore Raycast hariç (müttefiklerden kaçınır).</summary>
        public static int EnemyObstacleMask { get { EnsureResolved(); return _enemyObstacleMask; } }

        /// <summary>Düşman mermisi çarpışması: diğer mermiler, pickup'lar ve Ignore Raycast hariç.</summary>
        public static int EnemyProjectileMask { get { EnsureResolved(); return _enemyProjectileMask; } }

        /// <summary>Zemin taraması: yalnızca statik dünya (oyuncu, düşman, mermi, pickup ve Ignore Raycast hariç).</summary>
        public static int GroundMask { get { EnsureResolved(); return _groundMask; } }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            _player = Resolve(PlayerLayerName);
            _enemy = Resolve(EnemyLayerName);
            _projectile = Resolve(ProjectileLayerName);
            _pickup = Resolve(PickupLayerName);

            int playerBit = Bit(_player);
            int enemyBit = Bit(_enemy);
            int projectileBit = Bit(_projectile);
            int pickupBit = Bit(_pickup);
            int ignoreRaycastBit = Physics.IgnoreRaycastLayer;

            _playerAttackMask = ~(playerBit | pickupBit | ignoreRaycastBit);
            _enemySightMask = ~(enemyBit | projectileBit | pickupBit | ignoreRaycastBit);
            _enemyObstacleMask = ~(playerBit | projectileBit | pickupBit | ignoreRaycastBit);
            _enemyProjectileMask = ~(projectileBit | pickupBit | ignoreRaycastBit);
            _groundMask = ~(playerBit | enemyBit | projectileBit | pickupBit | ignoreRaycastBit);
        }

        private static int Resolve(string layerName)
        {
            int index = LayerMask.NameToLayer(layerName);
            if (index < 0)
            {
                Debug.LogWarning($"[GameLayers] '{layerName}' katmanı TagManager'da tanımlı değil; Default (0) kullanılıyor ve maskelerden dışlanmıyor.");
                return 0;
            }
            return index;
        }

        // Default (0) katmanını asla dışlama: tanımsız katman maskeyi daraltmamalı.
        private static int Bit(int layer) => layer > 0 ? 1 << layer : 0;
    }
}
