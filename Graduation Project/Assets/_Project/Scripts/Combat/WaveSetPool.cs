using System;
using UnityEngine;
using RetroFPS.Run;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Bir oda için çalışma zamanında seed'li rastgele <see cref="WaveSet"/> seçimi sağlayan havuz (ScriptableObject).
    /// Shuffle-bag davranışı ile aynı koşu (run) içinde tüm geçerli setler kullanılana kadar tekrarları önler.
    /// Not: Ağırlığı (weight) 0 veya negatif olan ya da WaveSet referansı atanmamış girdiler devre dışı kabul edilir.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSetPool_New", menuName = "RetroFPS/Combat/Wave Set Pool", order = 11)]
    public class WaveSetPool : ScriptableObject
    {
        /// <summary>
        /// Havuzdaki tek bir WaveSet girdisi ve seçilme ağırlığı. Ağırlık &lt;= 0 ise seçime dahil edilmez.
        /// </summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("Seçilebilecek dalga seti asset'i.")]
            [SerializeField] private WaveSet waveSet;

            [Tooltip("Seçilme ağırlığı. 0 veya negatif ise bu girdi seçime dahil edilmez (önerilen varsayılan: 1).")]
            [SerializeField, Min(0f)] private float weight;

            public Entry(WaveSet waveSet, float weight = 1f)
            {
                this.waveSet = waveSet;
                this.weight = weight;
            }

            public WaveSet WaveSet => waveSet;
            public float Weight => weight;
        }

        [Tooltip("Level designer notları (bu havuzun hangi odalar/katlar için tasarlandığı vb.).")]
        [SerializeField, TextArea] private string designerNotes;

        [Tooltip("Açıkken aynı run içinde tüm geçerli WaveSet'ler seçilene kadar aynı set tekrar seçilmez (shuffle-bag).")]
        [SerializeField] private bool avoidRepeatsInRun = true;

        [Tooltip("Havuzdaki aday dalga setleri ve ağırlıkları (ağırlık <= 0 olanlar hariç tutulur).")]
        [SerializeField] private Entry[] entries;

        [NonSerialized] private bool[] _usedInBag;
        [NonSerialized] private int _bagSeed;
        [NonSerialized] private bool _bagInitialized;
        [NonSerialized] private float[] _weightsScratch;

        public string DesignerNotes => designerNotes;
        public bool AvoidRepeatsInRun => avoidRepeatsInRun;
        public Entry[] Entries => entries;

        /// <summary>
        /// Havuzdaki geçerli (WaveSet atanmış ve ağırlığı &gt; 0 olan) girdi sayısı (Editor araçları için).
        /// </summary>
        public int ValidCount
        {
            get
            {
                if (entries == null) return 0;
                int count = 0;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (IsValidEntry(entries[i])) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Geçerli girdiler arasındaki <paramref name="i"/> indeksli <see cref="WaveSet"/>'i döner (Editor araçları için).
        /// </summary>
        public WaveSet GetWaveSet(int i)
        {
            if (entries == null || i < 0) return null;
            int validIdx = 0;
            for (int idx = 0; idx < entries.Length; idx++)
            {
                if (!IsValidEntry(entries[idx])) continue;
                if (validIdx == i) return entries[idx].WaveSet;
                validIdx++;
            }
            return null;
        }

        private void OnEnable()
        {
            ResetBagState();
        }

        /// <summary>
        /// Verilen deterministik akış (<paramref name="rng"/>) ve koşu tohumu (<paramref name="runSeed"/>) ile
        /// ağırlıklı rastgele bir <see cref="WaveSet"/> seçer. Geçerli girdi yoksa null döner.
        /// </summary>
        public WaveSet Pick(RunRandom rng, int runSeed)
        {
            if (entries == null || entries.Length == 0) return null;

            int len = entries.Length;
            EnsureBuffers(len);

            if (!_bagInitialized || runSeed != _bagSeed)
            {
                Array.Clear(_usedInBag, 0, len);
                _bagSeed = runSeed;
                _bagInitialized = true;
            }

            int totalValid = 0;
            int availableValid = 0;

            for (int i = 0; i < len; i++)
            {
                if (IsValidEntry(entries[i]))
                {
                    totalValid++;
                    if (!avoidRepeatsInRun || !_usedInBag[i])
                    {
                        _weightsScratch[i] = entries[i].Weight;
                        availableValid++;
                    }
                    else
                    {
                        _weightsScratch[i] = 0f;
                    }
                }
                else
                {
                    _weightsScratch[i] = 0f;
                }
            }

            if (totalValid == 0) return null;

            // Torbadaki tüm geçerli setler kullanıldıysa torbayı sıfırla ve ağırlıkları yeniden doldur
            if (avoidRepeatsInRun && availableValid == 0)
            {
                Array.Clear(_usedInBag, 0, len);
                for (int i = 0; i < len; i++)
                {
                    _weightsScratch[i] = IsValidEntry(entries[i]) ? entries[i].Weight : 0f;
                }
            }

            if (rng == null)
            {
                rng = RunEvents.GetStream(RunStream.Encounter);
            }

            int pickedIndex = rng.WeightedIndex(_weightsScratch, len);
            if (pickedIndex < 0 || pickedIndex >= len) return null;

            if (avoidRepeatsInRun)
            {
                _usedInBag[pickedIndex] = true;
            }

            return entries[pickedIndex].WaveSet;
        }

        private static bool IsValidEntry(Entry entry)
        {
            return entry.WaveSet != null && entry.Weight > 0f;
        }

        private void EnsureBuffers(int length)
        {
            if (_weightsScratch == null || _weightsScratch.Length != length)
            {
                _weightsScratch = new float[length];
            }

            if (_usedInBag == null || _usedInBag.Length != length)
            {
                _usedInBag = new bool[length];
                _bagInitialized = false;
            }
        }

        private void ResetBagState()
        {
            _bagSeed = 0;
            _bagInitialized = false;
            if (_usedInBag != null)
            {
                Array.Clear(_usedInBag, 0, _usedInBag.Length);
            }
        }
    }
}
