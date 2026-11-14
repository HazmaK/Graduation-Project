using System;
using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.Localization
{
    /// <summary>Scene-independent provider registry without a Menu dependency.</summary>
    public static class LocalizedStrings
    {
        private static ILocalizedStringProvider provider;
        /// <summary>Registered provider, or null before composition.</summary>
        public static ILocalizedStringProvider Provider => provider;
        /// <summary>Registers or injects a provider; repeated registration is harmless.</summary>
        public static void Register(ILocalizedStringProvider value) =>
            provider = value ?? throw new ArgumentNullException(nameof(value));
        /// <summary>Returns localized copy, or the key before registration.</summary>
        public static string Get(string key, LanguageSetting language)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return provider != null ? provider.Get(key, language) : key;
        }
        /// <summary>Clears the provider for subsystem startup and isolated tests.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => provider = null;
    }
}
