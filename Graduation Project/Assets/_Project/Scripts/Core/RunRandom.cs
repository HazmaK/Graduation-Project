namespace RetroFPS.Run
{
    /// <summary>Koşunun birbirinden bağımsız rastgelelik akışları.</summary>
    public enum RunStream
    {
        /// <summary>Karşılaşma seçimleri.</summary>
        Encounter = 1,
        /// <summary>Ganimet seçimleri.</summary>
        Loot = 2,
        /// <summary>Araf seçimleri.</summary>
        Araf = 3,
        /// <summary>Görsel ve işitsel seçimler.</summary>
        Cosmetic = 4,
        /// <summary>Araf labirent yerleşimi.</summary>
        ArafLayout = 5,
        /// <summary>Araf oda içerik seçimleri.</summary>
        ArafRooms = 6
    }

    /// <summary>SplitMix64 ile başlatılan, tahsissiz ve deterministik xoshiro128** üreteci.</summary>
    public sealed class RunRandom
    {
        private uint _s0;
        private uint _s1;
        private uint _s2;
        private uint _s3;

        /// <summary>Verilen tohumdan yeni bir rastgelelik dizisi oluşturur.</summary>
        public RunRandom(ulong seed)
        {
            ulong first = SplitMix64(ref seed);
            ulong second = SplitMix64(ref seed);
            _s0 = (uint)first;
            _s1 = (uint)(first >> 32);
            _s2 = (uint)second;
            _s3 = (uint)(second >> 32);
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }

        /// <summary>Ana tohum ve akış kimliğinden bağımsız, tekrarlanabilir bir üreteç türetir.</summary>
        public static RunRandom Derive(int masterSeed, RunStream stream)
        {
            ulong seed = ((ulong)(uint)masterSeed << 32) | (uint)stream;
            return new RunRandom(SplitMix64(ref seed));
        }

        /// <summary>Dizideki sonraki 32 bit işaretsiz değeri üretir.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                uint result = RotateLeft(_s1 * 5u, 7) * 9u;
                uint t = _s1 << 9;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = RotateLeft(_s3, 11);
                return result;
            }
        }

        /// <summary>Durumu ilerleterek [0, 1) aralığında bir değer üretir.</summary>
        public float Value01 => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Alt sınır dahil, üst sınır hariç yansız tamsayı üretir; boş aralıkta alt sınırı döndürür.</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            unchecked
            {
                uint width = (uint)((long)maxExclusive - minInclusive);
                ulong product = (ulong)NextUInt() * width;
                uint low = (uint)product;
                if (low < width)
                {
                    uint threshold = (0u - width) % width;
                    while (low < threshold)
                    {
                        product = (ulong)NextUInt() * width;
                        low = (uint)product;
                    }
                }
                return (int)((long)minInclusive + (long)(product >> 32));
            }
        }

        /// <summary>Verilen kayan noktalı sınırlar arasında bir değer üretir.</summary>
        public float Range(float minInclusive, float maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            return (float)(minInclusive + ((double)maxInclusive - minInclusive) * Value01);
        }

        /// <summary>Verilen olasılıkla doğru döndürür; 0 ve 1 sınırları kesindir.</summary>
        public bool Chance(float probability01)
        {
            if (!(probability01 > 0f)) return false;
            if (probability01 >= 1f) return true;
            return Value01 < probability01;
        }

        /// <summary>İlk count ağırlık arasından seçer; pozitif ağırlık yoksa -1 döndürür.</summary>
        public int WeightedIndex(float[] weights, int count)
        {
            if (weights == null || count <= 0) return -1;
            if (count > weights.Length) count = weights.Length;
            double total = 0d;
            int lastPositive = -1;
            int infiniteCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                if (float.IsPositiveInfinity(weights[i])) infiniteCount++;
                total += weights[i];
                lastPositive = i;
            }
            if (lastPositive < 0) return -1;
            if (infiniteCount > 0)
            {
                int selected = Range(0, infiniteCount);
                for (int i = 0; i < count; i++)
                {
                    if (float.IsPositiveInfinity(weights[i]) && selected-- == 0) return i;
                }
            }
            double target = Value01 * total;
            for (int i = 0; i < count; i++)
            {
                if (!(weights[i] > 0f)) continue;
                if (target < weights[i]) return i;
                target -= weights[i];
            }
            return lastPositive;
        }

        /// <summary>Dizinin ilk count öğesini Fisher-Yates yöntemiyle yerinde karıştırır.</summary>
        public void Shuffle<T>(T[] array, int count)
        {
            if (array == null || count <= 1) return;
            if (count > array.Length) count = array.Length;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T item = array[i];
                array[i] = array[j];
                array[j] = item;
            }
        }

        private static uint RotateLeft(uint value, int bits)
            => (value << bits) | (value >> (32 - bits));

        private static ulong SplitMix64(ref ulong state)
        {
            unchecked
            {
                ulong value = (state += 0x9E3779B97F4A7C15UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }
}
