using RetroFPS.Localization;
using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Composes gameplay localization before any scene, including direct gameplay launches.</summary>
    public static class LocalizationBootstrap
    {
        private static bool warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarning() => warned = false;

        /// <summary>Loads the build-retained catalog and registers its provider without a menu scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            GameSettings.EnsureInitialized();
            LocalizationCatalog catalog = Resources.Load<LocalizationCatalog>("Localization/LocalizationCatalog");
            if (catalog != null && catalog.CombatTable != null)
            {
                LocalizedStrings.Register(catalog.CombatTable);
                return;
            }
            if (warned) return;
            warned = true;
            Debug.LogWarning("[LocalizationBootstrap] Localization catalog or combat table is missing.");
        }
    }
}
