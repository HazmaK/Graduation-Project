namespace RetroFPS.Settings
{
    /// <summary>Supported languages with stable serialized identities.</summary>
    public enum LanguageSetting
    {
        /// <summary>English (en).</summary>
        English = 0,
        /// <summary>Turkish (tr).</summary>
        Turkish = 1
    }

    /// <summary>Engine-independent language code and default policy.</summary>
    public static class LanguageDefaults
    {
        /// <summary>Only a Turkish system locale selects Turkish.</summary>
        public static LanguageSetting FromSystemLanguage(bool isTurkish) =>
            isTurkish ? LanguageSetting.Turkish : LanguageSetting.English;
        /// <summary>Reports whether a serialized language is supported.</summary>
        public static bool IsValid(LanguageSetting language) =>
            language == LanguageSetting.English || language == LanguageSetting.Turkish;
        /// <summary>Reads exact preference codes, falling back for absent or invalid codes.</summary>
        public static LanguageSetting FromCode(string code, LanguageSetting fallback) =>
            code == "tr" ? LanguageSetting.Turkish : code == "en" ? LanguageSetting.English : fallback;
        /// <summary>Returns the stable persistence code; rejects invalid languages.</summary>
        public static string ToCode(LanguageSetting language)
        {
            if (!IsValid(language)) throw new System.ArgumentOutOfRangeException(nameof(language));
            return language == LanguageSetting.Turkish ? "tr" : "en";
        }
    }
}
