using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Merkez veya kanat labirenti ve dünya köküne göre yerleşimi.</summary>
    public sealed class ArafSection
    {
        /// <summary>Dünyadaki bölüm indeksi; merkez sıfırdır.</summary>
        public int Index { get; }
        /// <summary>Bölümün merkez labirenti olup olmadığı.</summary>
        public bool IsHub => Index == 0;
        /// <summary>Bölümün hücre topolojisi.</summary>
        public LabyrinthLayout Layout { get; }
        /// <summary>Labirent kökünün yerel uzayında bölümün taban merkezi.</summary>
        public Vector3 Origin { get; }
        /// <summary>Kanadın kendi labirentindeki ağız indeksi; merkez için -1.</summary>
        public int MouthIndex { get; }

        internal ArafSection(int index, LabyrinthLayout layout, Vector3 origin, int mouthIndex)
        {
            Index = index;
            Layout = layout;
            Origin = origin;
            MouthIndex = mouthIndex;
        }
    }

    /// <summary>Yüzen kare adanın değişmez üst yüzeyi.</summary>
    public readonly struct ArafPlatform
    {
        /// <summary>Labirent kökünün yerel uzayında üst yüzey merkezi.</summary>
        public Vector3 Center { get; }
        /// <summary>Kare yüzeyin kenar uzunluğu.</summary>
        public float Size { get; }

        /// <summary>Adanın üst yüzey geometrisini oluşturur.</summary>
        public ArafPlatform(Vector3 center, float size)
        {
            Center = center;
            Size = size;
        }
    }

    /// <summary>Bir merkez ağzını kanat ağzına bağlayan sıralı ada zinciri.</summary>
    public sealed class ArafIslandChain
    {
        private readonly ArafPlatform[] platforms;

        /// <summary>Bağlanılan kanadın dünya bölüm indeksi.</summary>
        public int WingSection { get; }
        /// <summary>Merkez labirentindeki ağız indeksi.</summary>
        public int HubMouth { get; }
        /// <summary>Merkez ağzının sınır kenarındaki taban noktası.</summary>
        public Vector3 Start { get; }
        /// <summary>Kanat ağzının sınır kenarındaki taban noktası.</summary>
        public Vector3 End { get; }
        /// <summary>Zincirdeki ada sayısı.</summary>
        public int PlatformCount => platforms.Length;

        internal ArafIslandChain(int wingSection, int hubMouth, Vector3 start, Vector3 end,
            ArafPlatform[] platforms)
        {
            WingSection = wingSection;
            HubMouth = hubMouth;
            Start = start;
            End = end;
            this.platforms = platforms;
        }

        /// <summary>Merkezden kanada doğru sıralanmış adayı döndürür.</summary>
        public ArafPlatform GetPlatform(int i) => platforms[i];
    }

    /// <summary>Araf bölüm ve ada geometrisinin sahneden bağımsız sonucu.</summary>
    public sealed class ArafWorldLayout
    {
        private readonly ArafSection[] sections;
        private readonly ArafIslandChain[] chains;

        /// <summary>Merkez dahil bölüm sayısı.</summary>
        public int SectionCount => sections.Length;
        /// <summary>Merkez ile kanatlar arasındaki zincir sayısı.</summary>
        public int ChainCount => chains.Length;
        /// <summary>Merkez labirent bölümü.</summary>
        public ArafSection Hub => sections[0];

        internal ArafWorldLayout(ArafSection[] sections, ArafIslandChain[] chains)
        {
            this.sections = sections;
            this.chains = chains;
        }

        /// <summary>Belirtilen bölümü döndürür; sıfır merkezdir.</summary>
        public ArafSection GetSection(int i) => sections[i];
        /// <summary>Belirtilen ada zincirini döndürür.</summary>
        public ArafIslandChain GetChain(int i) => chains[i];
        /// <summary>Bölüm hücresini labirent kökünün yerel uzayına dönüştürür.</summary>
        public Vector3 CellToLocal(int section, int x, int y)
            => sections[section].Origin + sections[section].Layout.CellToLocal(x, y);
    }
}
