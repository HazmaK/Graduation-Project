using System;
using System.Globalization;
using RetroFPS.Localization;
using RetroFPS.Settings;

namespace RetroFPS.Combat
{
    /// <summary>Pure, explicit-language popup copy. Construction is confined to language changes.</summary>
    public sealed class DamagePopupTextCache
    {
        /// <summary>Amounts below this bound have complete cached labels.</summary>
        public const int CachedAmounts = 4096;
        private readonly string[][] labels = new string[6][];
        private readonly string[] prefixes = new string[6];
        private readonly string[] suffixes = new string[6];
        private readonly string[] digits = new string[10];

        /// <summary>Builds six label families and their multipart pieces from the shared provider.</summary>
        public DamagePopupTextCache(ILocalizedStringProvider provider, LanguageSetting language)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!LanguageDefaults.IsValid(language)) throw new ArgumentOutOfRangeException(nameof(language));
            prefixes[0] = string.Empty;
            prefixes[1] = provider.Get("popup.critical", language) + " ";
            prefixes[2] = provider.Get("popup.reflected", language) + " ";
            prefixes[3] = prefixes[4] = prefixes[5] = "+";
            suffixes[0] = suffixes[1] = suffixes[2] = string.Empty;
            suffixes[3] = " " + provider.Get("popup.health", language);
            suffixes[4] = " " + provider.Get("popup.bolts", language);
            suffixes[5] = " " + provider.Get("popup.shells", language);
            for (int kind = 0; kind < labels.Length; kind++) labels[kind] = new string[CachedAmounts];
            for (int amount = 0; amount < CachedAmounts; amount++)
            {
                string number = amount.ToString(CultureInfo.InvariantCulture);
                if (amount < digits.Length) digits[amount] = number;
                labels[0][amount] = number;
                for (int kind = 1; kind < labels.Length; kind++)
                    labels[kind][amount] = prefixes[kind] + number + suffixes[kind];
            }
        }

        /// <summary>Gets a stored complete label for an amount in 0–4095.</summary>
        public string GetLabel(DamagePopupKind kind, int amount) => labels[(int)kind][amount];
        /// <summary>Gets the pre-spaced prefix for arbitrary integer rendering.</summary>
        public string GetPrefix(DamagePopupKind kind) => prefixes[(int)kind];
        /// <summary>Gets the pre-spaced suffix for arbitrary integer rendering.</summary>
        public string GetSuffix(DamagePopupKind kind) => suffixes[(int)kind];
        /// <summary>Gets an invariant, preallocated digit.</summary>
        public string GetDigit(int digit) => digits[digit];
    }
}
