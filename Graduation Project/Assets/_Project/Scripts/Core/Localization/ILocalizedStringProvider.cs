using RetroFPS.Settings;

namespace RetroFPS.Localization
{
    /// <summary>Explicit-language lookup boundary shared by presentation and gameplay.</summary>
    public interface ILocalizedStringProvider
    {
        /// <summary>Returns stored copy, falling back to Turkish and then the key.</summary>
        string Get(string key, LanguageSetting language);
    }
}
