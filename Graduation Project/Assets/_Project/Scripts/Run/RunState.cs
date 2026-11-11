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
        private readonly RunRandom[] _streams = new RunRandom[7];
        private int _floorIndex;
        private bool _arafChargeUsed;
        private RunPhase _phase;

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
        }

        /// <summary>
        /// Koşu başına tek seferlik olan Araf hakkını tüketir.
        /// </summary>
        public void ConsumeArafCharge()
        {
            _arafChargeUsed = true;
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
