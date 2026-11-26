using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Uyanışa bağlı arena dalgalarını ve doğuş sınırlarını saklar.</summary>
    [Serializable]
    public sealed class ArafArenaWaveSettings
    {
        [SerializeField] private int[] optionalWavesByAwakening = { 2, 2, 3, 3 };
        [SerializeField] private int[] sealWavesByAwakening = { 3, 3, 3, 3 };
        [SerializeField] private int[] ghoulsByAwakening = { 3, 3, 4, 4 };
        [SerializeField] private int[] eyesByAwakening = { 0, 1, 1, 2 };
        [SerializeField] private int[] finalWavePenitentsByAwakening = { 0, 0, 1, 1 };
        [SerializeField] private int sealFinalPenitents = 1;
        [SerializeField] private int advanceWhenAliveAtMost = 1;
        [SerializeField] private float telegraphSeconds = 0.6f;
        [SerializeField] private float minSpawnDistance = 4f;
        [SerializeField] private float realertInterval = 0.5f;
        [SerializeField] private int maxAlivePerArena = 12;

        /// <summary>Varsayılan ayarları oluşturur.</summary>
        public ArafArenaWaveSettings() { }

        /// <summary>Tüm ayarları alır; dizileri kopyalayarak dış değişikliklerden korur.</summary>
        public ArafArenaWaveSettings(int[] optionalWavesByAwakening, int[] sealWavesByAwakening,
            int[] ghoulsByAwakening, int[] eyesByAwakening, int[] finalWavePenitentsByAwakening,
            int sealFinalPenitents, int advanceWhenAliveAtMost, float telegraphSeconds,
            float minSpawnDistance, float realertInterval, int maxAlivePerArena)
        {
            Copy(optionalWavesByAwakening, sealWavesByAwakening, ghoulsByAwakening,
                eyesByAwakening, finalWavePenitentsByAwakening, sealFinalPenitents,
                advanceWhenAliveAtMost, telegraphSeconds, minSpawnDistance, realertInterval, maxAlivePerArena);
        }

        /// <summary>İsteğe bağlı arena dalga sayısı.</summary>
        public int OptionalWaves(int awakening) => Value(optionalWavesByAwakening, awakening);
        /// <summary>Mühür arenası dalga sayısı.</summary>
        public int SealWaves(int awakening) => Value(sealWavesByAwakening, awakening);
        /// <summary>Dalga başına ghoul sayısı.</summary>
        public int Ghouls(int awakening) => Value(ghoulsByAwakening, awakening);
        /// <summary>Dalga başına uçan göz sayısı.</summary>
        public int Eyes(int awakening) => Value(eyesByAwakening, awakening);
        /// <summary>Son dalganın kefaretçi sayısı.</summary>
        public int FinalWavePenitents(int awakening) => Value(finalWavePenitentsByAwakening, awakening);
        /// <summary>Mühür son dalgasına eklenen kefaretçi sayısı.</summary>
        public int SealFinalPenitents => Mathf.Max(0, sealFinalPenitents);
        /// <summary>Sonraki dalgayı açan canlı eşiği.</summary>
        public int AdvanceWhenAliveAtMost => Mathf.Max(0, advanceWhenAliveAtMost);
        /// <summary>Kızıl doğuş uyarısının süresi.</summary>
        public float TelegraphSeconds => Mathf.Max(0f, telegraphSeconds);
        /// <summary>Oyuncuya en az yatay doğuş mesafesi.</summary>
        public float MinSpawnDistance => Mathf.Max(0f, minSpawnDistance);
        /// <summary>Tekrar alarm aralığı.</summary>
        public float RealertInterval => Mathf.Max(0.01f, realertInterval);
        /// <summary>Arenada aynı anda izlenebilen canlı sayısı.</summary>
        public int MaxAlivePerArena => Mathf.Max(1, maxAlivePerArena);
        /// <summary>Önceden ayrılacak dalga tamponunun kapasitesi.</summary>
        public int MaxWaveCount => Mathf.Max(Max(optionalWavesByAwakening), Max(sealWavesByAwakening));

        private static int Value(int[] values, int awakening)
            => values == null || values.Length == 0 ? 0 : Mathf.Max(0, values[Mathf.Clamp(awakening, 0, values.Length - 1)]);
        private static int Max(int[] values)
        {
            int result = 0;
            if (values != null)
                for (int i = 0; i < values.Length; i++) result = Mathf.Max(result, values[i]);
            return result;
        }

        private void Copy(int[] optional, int[] seal, int[] ghouls, int[] eyes, int[] penitents,
            int sealExtra, int advance, float telegraph, float distance, float realert, int maxAlive)
        {
            optionalWavesByAwakening = optional != null ? (int[])optional.Clone() : Array.Empty<int>();
            sealWavesByAwakening = seal != null ? (int[])seal.Clone() : Array.Empty<int>();
            ghoulsByAwakening = ghouls != null ? (int[])ghouls.Clone() : Array.Empty<int>();
            eyesByAwakening = eyes != null ? (int[])eyes.Clone() : Array.Empty<int>();
            finalWavePenitentsByAwakening = penitents != null ? (int[])penitents.Clone() : Array.Empty<int>();
            sealFinalPenitents = sealExtra;
            advanceWhenAliveAtMost = advance;
            telegraphSeconds = telegraph;
            minSpawnDistance = distance;
            realertInterval = realert;
            maxAlivePerArena = maxAlive;
        }

#if UNITY_EDITOR
        /// <summary>Editör ve testler için ayarları dizileri kopyalayarak değiştirir.</summary>
        public void Configure(int[] optional, int[] seal, int[] ghouls, int[] eyes, int[] penitents,
            int sealExtra, int advance, float telegraph, float distance, float realert, int maxAlive)
            => Copy(optional, seal, ghouls, eyes, penitents, sealExtra, advance, telegraph, distance, realert, maxAlive);
#endif
    }
}
