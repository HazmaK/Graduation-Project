namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Tek arena dalgasının değişmez düşman planı.</summary>
    public readonly struct ArafArenaWave
    {
        /// <summary>Ghoul sayısı.</summary>
        public int Ghouls { get; }
        /// <summary>Uçan göz sayısı.</summary>
        public int Eyes { get; }
        /// <summary>Kefaretçi sayısı.</summary>
        public int Penitents { get; }
        /// <summary>Dalgada bir elit bulunur.</summary>
        public bool HasElite { get; }
        /// <summary>Dalga planını oluşturur.</summary>
        public ArafArenaWave(int ghouls, int eyes, int penitents, bool hasElite)
        {
            Ghouls = ghouls;
            Eyes = eyes;
            Penitents = penitents;
            HasElite = hasElite;
        }
    }
}
