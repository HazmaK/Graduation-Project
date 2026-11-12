using System;
using System.Globalization;

namespace RetroFPS.Presentation
{
    /// <summary>Prebuilt number pieces and bounded integer writing for allocation-free presentation.</summary>
    public static class HudNumberCache
    {
        private static readonly string[] Integers = new string[1000];
        private static readonly string[] TwoDigits = new string[60];

        static HudNumberCache()
        {
            for (int i = 0; i < Integers.Length; i++) Integers[i] = i.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < TwoDigits.Length; i++) TwoDigits[i] = i.ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>Initializes all cached pieces before combat.</summary>
        public static void Prewarm() { }

        /// <summary>Writes literal copy and {0}/{1} integer tokens into caller-owned storage without allocating.</summary>
        public static int WriteTemplate(string template, int first, int second, char[] buffer)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int count = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (i + 2 < template.Length && template[i] == '{' && template[i + 2] == '}' &&
                    (template[i + 1] == '0' || template[i + 1] == '1'))
                {
                    count += WriteInteger(template[i + 1] == '0' ? first : second, buffer, count);
                    i += 2;
                }
                else
                {
                    if (count >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(buffer));
                    buffer[count++] = template[i];
                }
            }
            return count;
        }

        /// <summary>Returns a cached integer in 0..999; use WriteInteger for larger or signed values.</summary>
        public static string GetInteger(int value)
        {
            if ((uint)value >= Integers.Length) throw new ArgumentOutOfRangeException(nameof(value));
            return Integers[value];
        }

        /// <summary>Returns a cached digit in 0..9.</summary>
        public static string GetDigit(int digit)
        {
            if ((uint)digit > 9) throw new ArgumentOutOfRangeException(nameof(digit));
            return Integers[digit];
        }

        /// <summary>Returns a cached two-digit time piece in 00..59.</summary>
        public static string GetTwoDigits(int value)
        {
            if ((uint)value >= TwoDigits.Length) throw new ArgumentOutOfRangeException(nameof(value));
            return TwoDigits[value];
        }

        /// <summary>Writes the complete signed integer at offset and returns its character count, without allocating.</summary>
        public static int WriteInteger(int value, char[] buffer, int offset = 0)
        {
            long magnitude = value < 0 ? -(long)value : value;
            int count = value < 0 ? 2 : 1;
            for (long remaining = magnitude; remaining >= 10; remaining /= 10) count++;
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(offset));
            int cursor = offset + count;
            do
            {
                buffer[--cursor] = (char)('0' + magnitude % 10);
                magnitude /= 10;
            } while (magnitude > 0);
            if (value < 0) buffer[offset] = '-';
            return count;
        }
    }
}
