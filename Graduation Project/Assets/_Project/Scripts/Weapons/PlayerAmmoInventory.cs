using System;
using UnityEngine;

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Oyuncunun taşıdığı tüm cephane rezervlerini yöneten merkezi envanter bileşeni.
    /// Sıfır GC tahsisi, olay temelli (Event-driven) iletişim ve Inspector yapılandırması sunar.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAmmoInventory : MonoBehaviour
    {
        [Header("Arbalest Okları (Bolts)")]
        [SerializeField] private int maxBolts = 120;
        [SerializeField] private int startingBolts = 60;

        [Header("Pompalı Fişekleri (Shells)")]
        [SerializeField] private int maxShells = 32;
        [SerializeField] private int startingShells = 16;

        private int _currentBolts;
        private int _currentShells;

        /// <summary>
        /// Cephane miktarı değiştiğinde tetiklenir (AmmoType, mevcut miktar, maksimum kapasite).
        /// </summary>
        public event Action<AmmoType, int, int> OnAmmoChanged;

        /// <summary>
        /// Yerden cephane toplandığında tetiklenir (AmmoType, eklenen miktar).
        /// </summary>
        public event Action<AmmoType, int> OnAmmoCollected;

        public int MaxBolts => maxBolts;
        public int MaxShells => maxShells;
        public int CurrentBolts => _currentBolts;
        public int CurrentShells => _currentShells;

        private void Awake()
        {
            _currentBolts = Mathf.Clamp(startingBolts, 0, maxBolts);
            _currentShells = Mathf.Clamp(startingShells, 0, maxShells);
        }

        private void Start()
        {
            // UI ve dinleyiciler için başlangıç durumunu bildir
            NotifyAmmoChanged(AmmoType.Bolts);
            NotifyAmmoChanged(AmmoType.Shells);
        }

        /// <summary>
        /// İstenen miktarda cephane bulunup bulunmadığını kontrol eder.
        /// </summary>
        public bool HasAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None || amount <= 0) return true;

            switch (type)
            {
                case AmmoType.Bolts:
                    return _currentBolts >= amount;
                case AmmoType.Shells:
                    return _currentShells >= amount;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Cephaneyi tüketmeyi dener. Yeterli cephane varsa düşer ve true döner.
        /// </summary>
        public bool TryConsumeAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None || amount <= 0) return true;

            switch (type)
            {
                case AmmoType.Bolts:
                    if (_currentBolts >= amount)
                    {
                        _currentBolts -= amount;
                        NotifyAmmoChanged(AmmoType.Bolts);
                        return true;
                    }
                    return false;

                case AmmoType.Shells:
                    if (_currentShells >= amount)
                    {
                        _currentShells -= amount;
                        NotifyAmmoChanged(AmmoType.Shells);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Envantere cephane ekler. Maksimum kapasiteyi aşmaz.
        /// </summary>
        /// <returns>Gerçekte eklenen cephane miktarı.</returns>
        public int AddAmmo(AmmoType type, int amount)
        {
            if (type == AmmoType.None || amount <= 0) return 0;

            int added = 0;

            switch (type)
            {
                case AmmoType.Bolts:
                    int spaceBolts = maxBolts - _currentBolts;
                    added = Mathf.Min(spaceBolts, amount);
                    if (added > 0)
                    {
                        _currentBolts += added;
                        NotifyAmmoChanged(AmmoType.Bolts);
                        OnAmmoCollected?.Invoke(AmmoType.Bolts, added);
                    }
                    break;

                case AmmoType.Shells:
                    int spaceShells = maxShells - _currentShells;
                    added = Mathf.Min(spaceShells, amount);
                    if (added > 0)
                    {
                        _currentShells += added;
                        NotifyAmmoChanged(AmmoType.Shells);
                        OnAmmoCollected?.Invoke(AmmoType.Shells, added);
                    }
                    break;
            }

            return added;
        }

        /// <summary>
        /// Belirtilen cephanenin şu anki adetini döndürür.
        /// </summary>
        public int GetCurrentAmmo(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Bolts: return _currentBolts;
                case AmmoType.Shells: return _currentShells;
                default: return 0;
            }
        }

        /// <summary>
        /// Belirtilen cephanenin tavan sınırını döndürür.
        /// </summary>
        public int GetMaxAmmo(AmmoType type)
        {
            switch (type)
            {
                case AmmoType.Bolts: return maxBolts;
                case AmmoType.Shells: return maxShells;
                default: return 0;
            }
        }

        /// <summary>
        /// Cephanenin 0.0f - 1.0f aralığındaki doluluk oranını verir.
        /// </summary>
        public float GetAmmoRatio(AmmoType type)
        {
            int max = GetMaxAmmo(type);
            if (max <= 0) return 1f;
            return (float)GetCurrentAmmo(type) / max;
        }

        private void NotifyAmmoChanged(AmmoType type)
        {
            OnAmmoChanged?.Invoke(type, GetCurrentAmmo(type), GetMaxAmmo(type));
        }
    }
}
