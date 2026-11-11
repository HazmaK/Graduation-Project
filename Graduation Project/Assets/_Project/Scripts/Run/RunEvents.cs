using System;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS.Run
{
    /// <summary>
    /// Koşu (Run) ve Araf (Purgatory) yaşam döngüsü için merkezi, sıfır bağımlılıklı olay ve aktif karşılaşma kaydı.
    /// </summary>
    public static class RunEvents
    {
        private static IRetryableEncounter _activeEncounter;

        /// <summary>
        /// Koşu evresi değiştiğinde tetiklenir.
        /// </summary>
        public static event Action<RunPhase> OnPhaseChanged;

        /// <summary>
        /// Oyuncu Araf boyutuna tamamen giriş yaptığında tetiklenir.
        /// </summary>
        public static event Action OnEnteredAraf;

        /// <summary>
        /// Oyuncu Araf'tan kaçıp kuleye geri döndüğünde tetiklenir.
        /// </summary>
        public static event Action OnEscapedAraf;

        /// <summary>
        /// Oyuncu gerçek ölüm (True Death) durumuna düştüğünde tetiklenir.
        /// </summary>
        public static event Action OnTrueDeath;

        /// <summary>
        /// Oyuncu Araf içindeki kan/kaçış portalına ulaştığında tetiklenir.
        /// </summary>
        public static event Action OnEscapePortalReached;

        /// <summary>
        /// Oyuncunun şu anda içinde savaştığı aktif yeniden denenebilir karşılaşma.
        /// </summary>
        public static IRetryableEncounter ActiveEncounter => _activeEncounter;

        /// <summary>
        /// Aktif karşılaşmayı kaydeder.
        /// </summary>
        public static void RegisterActiveEncounter(IRetryableEncounter e)
        {
            _activeEncounter = e;
        }

        /// <summary>
        /// Kayıtlı aktif karşılaşma verilen örnek ile aynıysa kaydı temizler.
        /// </summary>
        public static void UnregisterActiveEncounter(IRetryableEncounter e)
        {
            if (ReferenceEquals(_activeEncounter, e))
            {
                _activeEncounter = null;
            }
        }

        /// <summary>
        /// Evre değişimi olayını tetikler.
        /// </summary>
        public static void RaisePhaseChanged(RunPhase phase)
            => OnPhaseChanged?.Invoke(phase);

        /// <summary>
        /// Araf'a giriş olayını tetikler.
        /// </summary>
        public static void RaiseEnteredAraf()
            => OnEnteredAraf?.Invoke();

        /// <summary>
        /// Araf'tan kaçış olayını tetikler.
        /// </summary>
        public static void RaiseEscapedAraf()
            => OnEscapedAraf?.Invoke();

        /// <summary>
        /// Gerçek ölüm olayını tetikler.
        /// </summary>
        public static void RaiseTrueDeath()
            => OnTrueDeath?.Invoke();

        /// <summary>
        /// Kaçış portalına ulaşma olayını tetikler.
        /// </summary>
        public static void RaiseEscapePortalReached()
            => OnEscapePortalReached?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnPhaseChanged = null;
            OnEnteredAraf = null;
            OnEscapedAraf = null;
            OnTrueDeath = null;
            OnEscapePortalReached = null;
            _activeEncounter = null;
        }
    }
}
