using RetroFPS.Run.Cards;
using RetroFPS.Stats;

namespace RetroFPS.Run
{
    /// <summary>
    /// Bir koşunun (Run) çalışma zamanındaki ana evrelerini temsil eder.
    /// </summary>
    public enum RunPhase
    {
        Tower,
        FallingToAraf,
        InAraf,
        EscapingAraf,
        TrueDeath
    }

    /// <summary>
    /// Bir koşunun (Run) saf durum verilerini ve Araf hakkı kurallarını tutan POCO sınıfı.
    /// UnityEngine bağımlılığı içermez.
    /// </summary>
    public sealed class RunState
    {
        private readonly int _seed;
        private readonly RunRandom[] _streams = new RunRandom[9];
        private int _floorIndex;
        private bool _arafChargeUsed;
        private RunPhase _phase;
        private float _elapsedSeconds;

        /// <summary>Raised once when the run's Araf charge is consumed.</summary>
        public event System.Action<bool> OnArafChargeChanged;

        /// <summary>Unscaled run duration, frozen at true death.</summary>
        public float ElapsedSeconds => _elapsedSeconds;

        /// <summary>Statistics accumulated during this run.</summary>
        public RunRecord Record { get; }

        /// <summary>Stat modifiers owned by this run.</summary>
        public StatSheet Stats { get; }

        /// <summary>Acquired cards contributing to this run's statistics.</summary>
        public CardInventory Cards { get; }

        /// <summary>Deterministic offers for this run's Araf visit.</summary>
        public CardOfferRoller CardOffers { get; }

        /// <summary>
        /// Koşunun rastgelelik tohumu (Seed).
        /// </summary>
        public int Seed => _seed;

        /// <summary>
        /// Oyuncunun bulunduğu mevcut kule katı indeksi (0 tabanlı).
        /// </summary>
        public int FloorIndex => _floorIndex;

        /// <summary>
        /// Bu koşuda Araf (ikinci şans) hakkının kullanılıp kullanılmadığı.
        /// </summary>
        public bool ArafChargeUsed => _arafChargeUsed;

        /// <summary>
        /// Koşunun mevcut evresi.
        /// </summary>
        public RunPhase Phase => _phase;

        /// <summary>
        /// Oyuncunun öldüğünde Araf'a çekilip çekilemeyeceğini belirtir (hak kullanılmamış ve kule evresinde olmalı).
        /// </summary>
        public bool CanEnterAraf => !_arafChargeUsed && _phase == RunPhase.Tower;

        /// <summary>
        /// Belirtilen tohum değeriyle yeni bir koşu durumu oluşturur.
        /// </summary>
        /// <param name="seed">Koşu tohumu.</param>
        public RunState(int seed)
        {
            _seed = seed;
            _floorIndex = 0;
            _arafChargeUsed = false;
            _phase = RunPhase.Tower;
            Record = new RunRecord();
            Stats = new StatSheet();
            Cards = new CardInventory(Stats);
            CardOffers = new CardOfferRoller(GetStream(RunStream.ArafCards));
        }

        /// <summary>
        /// Koşu başına tek seferlik olan Araf hakkını tüketir.
        /// </summary>
        public void ConsumeArafCharge()
        {
            if (_arafChargeUsed) return;
            _arafChargeUsed = true;
            OnArafChargeChanged?.Invoke(true);
        }

        /// <summary>Accumulates positive unscaled time in every phase except true death.</summary>
        public void AdvanceElapsedTime(float unscaledDeltaSeconds)
        {
            if (_phase != RunPhase.TrueDeath && unscaledDeltaSeconds > 0f &&
                !float.IsInfinity(unscaledDeltaSeconds))
            {
                _elapsedSeconds += unscaledDeltaSeconds;
                Record.AdvanceTime(unscaledDeltaSeconds, _phase);
            }
        }

        /// <summary>Captures cumulative statistics and card membership for the current floor.</summary>
        public RunSummary CreateSummary()
        {
            var cards = new CurseBlessingDefinition[Cards.Count];
            for (int i = 0; i < cards.Length; i++) cards[i] = Cards.Get(i);
            return new RunSummary(_floorIndex + 1, _elapsedSeconds, Record.ArafSeconds,
                Record.TotalKills, Record.TowerKills, Record.ArafKills, Record.CriticalKills,
                Record.GibKills, Record.DamageTaken, _arafChargeUsed, cards);
        }

        /// <summary>Akışın koşu tohumundan türetilen, önbelleğe alınmış üretecini döndürür.</summary>
        public RunRandom GetStream(RunStream stream)
        {
            int index = (int)stream;
            return _streams[index] ?? (_streams[index] = RunRandom.Derive(Seed, stream));
        }

        /// <summary>
        /// Koşunun mevcut evresini günceller.
        /// </summary>
        /// <param name="p">Yeni koşu evresi.</param>
        public void SetPhase(RunPhase p)
        {
            _phase = p;
        }

        /// <summary>
        /// Kule kat indeksini bir artırır.
        /// </summary>
        public void AdvanceFloor()
        {
            _floorIndex++;
        }
    }
}
