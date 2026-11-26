using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf merkezi, kanatları ve aralarındaki ada zincirlerinin üretim ayarları.</summary>
    [Serializable]
    public sealed class ArafWorldSettings
    {
        [SerializeField] private LabyrinthSettings hub = new LabyrinthSettings(7, 7, 6f, 2, 0.15f, 1, 2);
        [SerializeField] private LabyrinthSettings wing = new LabyrinthSettings(7, 7, 6f, 3, 0.15f, 1, 2);
        [SerializeField] private int wingCount = 3;
        [SerializeField] private float[] wingHeightOffsets = { 2.4f, -3.2f, 4f };
        [SerializeField] private int platformsMin = 4;
        [SerializeField] private int platformsMax = 6;
        [SerializeField] private float platformSize = 5f;
        [SerializeField] private float gapMin = 2f;
        [SerializeField] private float gapMax = 3.5f;
        [SerializeField] private float maxRisePerHop = 0.8f;
        [SerializeField] private float lateralJitter = 1.5f;

        /// <summary>Merkez labirentinin ayarları.</summary>
        public LabyrinthSettings Hub => hub;
        /// <summary>Her kanat labirentinin ayarları.</summary>
        public LabyrinthSettings Wing => wing;
        /// <summary>Kuzey, doğu ve batı sırasındaki kanat sayısı; en fazla üçtür.</summary>
        public int WingCount => wingCount;
        /// <summary>Kanat taban yüksekliklerinin salt okunur görünümü.</summary>
        public IReadOnlyList<float> WingHeightOffsets => Array.AsReadOnly(wingHeightOffsets ?? Array.Empty<float>());
        /// <summary>Yükseklik ofsetlerinin sayısı.</summary>
        public int WingHeightOffsetCount => wingHeightOffsets == null ? 0 : wingHeightOffsets.Length;
        /// <summary>Zincirde rastgele seçilebilen en az ada sayısı.</summary>
        public int PlatformsMin => platformsMin;
        /// <summary>Yükselme gereksinimiyle aşılabilen rastgele ada sayısı üst sınırı.</summary>
        public int PlatformsMax => platformsMax;
        /// <summary>Kare ada yüzeyinin kenar uzunluğu.</summary>
        public float PlatformSize => platformSize;
        /// <summary>Komşu yüzey kenarları arasındaki en az yatay boşluk.</summary>
        public float GapMin => gapMin;
        /// <summary>Komşu yüzey kenarları arasındaki en fazla yatay boşluk.</summary>
        public float GapMax => gapMax;
        /// <summary>Bir atlamadaki en fazla yukarı yönlü yükseklik farkı.</summary>
        public float MaxRisePerHop => maxRisePerHop;
        /// <summary>Zincir eksenine dik azami ada merkezi sapması.</summary>
        public float LateralJitter => lateralJitter;

        /// <summary>Varsayılan Araf dünya ayarlarını oluşturur.</summary>
        public ArafWorldSettings() { }

        /// <summary>Editör araçları ve testler için dünya ayarlarını oluşturur; ofset dizisini kopyalar.</summary>
        public ArafWorldSettings(LabyrinthSettings hub, LabyrinthSettings wing, int wingCount = 3,
            float[] wingHeightOffsets = null, int platformsMin = 4, int platformsMax = 6,
            float platformSize = 5f, float gapMin = 2f, float gapMax = 3.5f,
            float maxRisePerHop = 0.8f, float lateralJitter = 1.5f)
        {
            this.hub = hub;
            this.wing = wing;
            this.wingCount = wingCount;
            this.wingHeightOffsets = wingHeightOffsets == null
                ? new[] { 2.4f, -3.2f, 4f } : (float[])wingHeightOffsets.Clone();
            this.platformsMin = platformsMin;
            this.platformsMax = platformsMax;
            this.platformSize = platformSize;
            this.gapMin = gapMin;
            this.gapMax = gapMax;
            this.maxRisePerHop = maxRisePerHop;
            this.lateralJitter = lateralJitter;
        }

        /// <summary>Kanat yüksekliğini döndürür; kısa diziler sarılır, boş dizi sıfır verir.</summary>
        public float GetWingHeightOffset(int i)
        {
            if (i < 0) throw new ArgumentOutOfRangeException(nameof(i));
            return WingHeightOffsetCount == 0 ? 0f : wingHeightOffsets[i % wingHeightOffsets.Length];
        }
    }
}
