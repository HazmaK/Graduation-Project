using System;
using UnityEngine;
using RetroFPS.Combat;
using RetroFPS.Run.Cards;

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
        private static bool _isCardOfferOpen;
        private static CardOffer _currentCardOffer;
        private static bool _isFloorSummaryOpen;
        private static RunSummary _lastFloorSummary;

        /// <summary>Whether the current floor summary is awaiting continuation.</summary>
        public static bool IsFloorSummaryOpen => _isFloorSummaryOpen;

        /// <summary>Most recently captured floor summary, or an empty summary for a new run.</summary>
        public static RunSummary LastFloorSummary => _lastFloorSummary;

        /// <summary>Raised once when a floor summary opens.</summary>
        public static event Action<RunSummary> OnFloorCleared;

        /// <summary>Raised after an open floor summary closes and the floor advances.</summary>
        public static event Action OnFloorContinued;

        /// <summary>Captures a Tower floor unless there is no run or a summary is already open.</summary>
        public static void RaiseFloorCleared()
        {
            if (_currentRun == null || _currentRun.Phase != RunPhase.Tower || _isFloorSummaryOpen) return;
            _lastFloorSummary = _currentRun.CreateSummary();
            _isFloorSummaryOpen = true;
            OnFloorCleared?.Invoke(_lastFloorSummary);
        }

        /// <summary>Clears summary ownership without advancing the floor or notifying continuation listeners.</summary>
        public static void CloseFloorSummary() => _isFloorSummaryOpen = false;

        /// <summary>Closes an open summary, advances the current floor, and notifies listeners.</summary>
        public static void ContinueAfterFloorClear()
        {
            if (!_isFloorSummaryOpen) return;
            _isFloorSummaryOpen = false;
            _currentRun?.AdvanceFloor();
            OnFloorContinued?.Invoke();
        }

        /// <summary>Whether an offer is waiting for a choice or refusal.</summary>
        public static bool IsCardOfferOpen => _isCardOfferOpen;

        /// <summary>The currently open offer, or an empty offer when closed.</summary>
        public static CardOffer CurrentCardOffer => _currentCardOffer;

        /// <summary>Raised when a new card offer opens.</summary>
        public static event Action<CardOffer> OnCardOfferOpened;

        /// <summary>Raised when an offer closes; null means refused or acquisition failed.</summary>
        public static event Action<CurseBlessingDefinition> OnCardOfferResolved;

        /// <summary>Raised after the current run successfully acquires an offered card.</summary>
        public static event Action<CurseBlessingDefinition> OnCardAcquired;

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
            _isCardOfferOpen = false;
            _currentCardOffer = default;
            _isFloorSummaryOpen = false;
            _lastFloorSummary = default;
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
                _fallbackStreams = new RunRandom[8];
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
        {
            CloseFloorSummary();
            OnTrueDeath?.Invoke();
        }

        /// <summary>
        /// Kaçış portalına ulaşma olayını tetikler.
        /// </summary>
        public static void RaiseEscapePortalReached()
            => OnEscapePortalReached?.Invoke();

        /// <summary>Araf oda keşif durumunu arayüze ve diğer dinleyicilere iletir.</summary>
        public static void RaiseArafRoomsChanged(int revealed, int total, bool exitRevealed)
            => OnArafRoomsChanged?.Invoke(revealed, total, exitRevealed);

        /// <summary>Opens an offer unless another offer is already open.</summary>
        public static void RaiseCardOfferOpened(CardOffer offer)
        {
            if (_isCardOfferOpen) return;
            _currentCardOffer = offer;
            _isCardOfferOpen = true;
            OnCardOfferOpened?.Invoke(offer);
        }

        /// <summary>Closes the open offer and acquires a valid choice, or reports refusal.</summary>
        public static void ResolveCardOffer(CurseBlessingDefinition chosenOrNull)
        {
            if (!_isCardOfferOpen) return;
            CurseBlessingDefinition chosen = _currentCardOffer.Contains(chosenOrNull) ? chosenOrNull : null;
            RunState run = _currentRun;
            // Close before callbacks so a reentrant resolution cannot acquire twice.
            _isCardOfferOpen = false;
            _currentCardOffer = default;
            if (chosen != null && run != null && run.Cards.TryAcquire(chosen))
                OnCardAcquired?.Invoke(chosen);
            else
                chosen = null;
            OnCardOfferResolved?.Invoke(chosen);
        }

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
            OnCardOfferOpened = null;
            OnCardOfferResolved = null;
            OnCardAcquired = null;
            OnFloorCleared = null;
            OnFloorContinued = null;
            _isFloorSummaryOpen = false;
            _lastFloorSummary = default;
            _isCardOfferOpen = false;
            _currentCardOffer = default;
            _activeEncounter = null;
            _currentRun = null;
            _fallbackStreams = null;
            _fallbackSeed = 0;
        }
    }
}
