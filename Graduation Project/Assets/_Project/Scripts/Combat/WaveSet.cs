using System.Text;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Yeniden kullanılabilir dalga preset'i (ScriptableObject).
    /// Dalga listesini sahneden ayırır: aynı preset birden fazla arenada paylaşılabilir,
    /// Easy/Hard varyantları ayrı asset olarak tutulabilir ve sahne dosyasında merge conflict oluşmaz.
    /// Yalnızca kalıcı prefab referansları tutar (sahne objesi referansı asset'e atanamaz).
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSet_New", menuName = "RetroFPS/Combat/Wave Set", order = 10)]
    public class WaveSet : ScriptableObject
    {
        [Tooltip("Level designer notları (hangi arena için, hedef süre, zorluk vb.)")]
        [TextArea(2, 5)]
        [SerializeField] private string designNotes;

        [Tooltip("Bu preset'in dalgaları. WaveSpawnDirector bu listeyi sırayla oynatır.")]
        [SerializeField] private WaveDefinition[] waves;

        public string DesignNotes => designNotes;
        public WaveDefinition[] Waves => waves;
        public int WaveCount => waves != null ? waves.Length : 0;

        public WaveDefinition GetWave(int index)
        {
            return waves != null && index >= 0 && index < waves.Length ? waves[index] : null;
        }

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < WaveCount; i++)
                {
                    if (waves[i] != null) total += waves[i].TotalEnemies;
                }
                return total;
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < WaveCount; i++)
            {
                if (waves[i] == null) continue;
                waves[i].InvalidateCache();
                waves[i].CalculateMetrics();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Özeti Konsola Yaz (Dalga / Düşman / Yoğunluk)")]
        private void LogSummary()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append('[').Append(name).Append("] ").Append(WaveCount).Append(" dalga, toplam ").Append(TotalEnemies).Append(" düşman\n");
            for (int i = 0; i < WaveCount; i++)
            {
                WaveDefinition w = waves[i];
                if (w == null) { sb.Append(i + 1).Append(". <boş>\n"); continue; }

                sb.Append(i + 1).Append(". ").Append(w.WaveName)
                  .Append(" | ").Append(w.CompositionSummary)
                  .Append(" | Cap ").Append(w.MaxConcurrentEnemies)
                  .Append(" | ").Append(w.DensityRating)
                  .Append(" | ").Append(w.TransitionMode)
                  .Append(i == WaveCount - 1 ? " (son dalga: her zaman hepsini öldür)" : string.Empty)
                  .Append('\n');
            }
            Debug.Log(sb.ToString(), this);
        }
#endif
    }
}
