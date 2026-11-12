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
        private static RunState _currentRun;
        private static RunRandom[] _fallbackStreams;
        private static int _fallbackSeed;

        /// <summary>Sahnedeki etkin koşu; yönetici bulunmadığında null döndürür.</summary>
        public static RunState CurrentRun => _currentRun;

        /// <summary>Raised when the current run is assigned or cleared.</summary>
        public static event Action<RunState> OnRunChanged;

        /// <summary>Forwards charge changes and the charge snapshot of a newly assigned run.</summary>
        public static event Action<bool> OnArafChargeChanged;

        /// <summary>Etkin koşuyu kaydeder veya null ile temizler.</summary>
        public static void SetCurrentRun(RunState state)
        {
            if (_currentRun != null) _currentRun.OnArafChargeChanged -= ForwardArafChargeChanged;
            _currentRun = state;
            if (_currentRun != null) _currentRun.OnArafChargeChanged += ForwardArafChargeChanged;
            OnRunChanged?.Invoke(state);
            OnArafChargeChanged?.Invoke(state != null && state.ArafChargeUsed);
        }

        private static void ForwardArafChargeChanged(bool used) => OnArafChargeChanged?.Invoke(used);

        /// <summary>Etkin koşunun akışını veya koşu yoksa süreç boyunca paylaşılan yedek akışı döndürür.</summary>
        public static RunRandom GetStream(RunStream stream)
        {
            if (_currentRun != null) return _currentRun.GetStream(stream);
            if (_fallbackStreams == null)
            {
                _fallbackSeed = Environment.TickCount;
                _fallbackStreams = new RunRandom[7];
            }
            int index = (int)stream;
            return _fallbackStreams[index] ??
                (_fallbackStreams[index] = RunRandom.Derive(_fallbackSeed, stream));
        }

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

        /// <summary>Araf oda keşif sayıları ve çıkış odası durumu değiştiğinde yükseltilir.</summary>
        public static event Action<int, int, bool> OnArafRoomsChanged;

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

        /// <summary>Araf oda keşif durumunu arayüze ve diğer dinleyicilere iletir.</summary>
        public static void RaiseArafRoomsChanged(int revealed, int total, bool exitRevealed)
            => OnArafRoomsChanged?.Invoke(revealed, total, exitRevealed);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_currentRun != null) _currentRun.OnArafChargeChanged -= ForwardArafChargeChanged;
            OnRunChanged = null;
            OnArafChargeChanged = null;
            OnPhaseChanged = null;
            OnEnteredAraf = null;
            OnEscapedAraf = null;
            OnTrueDeath = null;
            OnEscapePortalReached = null;
            OnArafRoomsChanged = null;
            _activeEncounter = null;
            _currentRun = null;
            _fallbackStreams = null;
            _fallbackSeed = 0;
        }
    }
}
