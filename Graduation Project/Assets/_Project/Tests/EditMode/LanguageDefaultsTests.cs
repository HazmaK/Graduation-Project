using System;
using NUnit.Framework;
using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.Tests
{
    /// <summary>System mapping and exact preference-code fallback coverage.</summary>
    public sealed class LanguageDefaultsTests
    {
        /// <summary>Every Unity locale, including Unknown, follows the Turkish-only rule.</summary>
        [Test]
        public void EverySystemLanguageUsesExactMapping()
        {
            foreach (SystemLanguage language in Enum.GetValues(typeof(SystemLanguage)))
                Assert.That(UnityLanguageDefaults.FromSystemLanguage(language), Is.EqualTo(
                    language == SystemLanguage.Turkish ? LanguageSetting.Turkish : LanguageSetting.English), language.ToString());
            Assert.That(UnityLanguageDefaults.FromSystemLanguage((SystemLanguage)int.MaxValue), Is.EqualTo(LanguageSetting.English));
        }

        /// <summary>Only exact stable codes override either system default; only the touched key is restored.</summary>
        [TestCase(LanguageSetting.English)]
        [TestCase(LanguageSetting.Turkish)]
        public void PreferencesUseValidCodesOrSystemDefault(LanguageSetting system)
        {
            bool existed = PlayerPrefs.HasKey("language");
            string saved = PlayerPrefs.GetString("language");
            try
            {
                var store = new PlayerPrefsSettingsStore();
                GameSettingsData defaults = GameSettingsData.Defaults(1920, 1080, system);
                PlayerPrefs.DeleteKey("language");
                Assert.That(store.Load(defaults).Language, Is.EqualTo(system));
                foreach (string code in new[] { "", "unknown", "TR", "EN", " tr", "en " })
                {
                    PlayerPrefs.SetString("language", code);
                    Assert.That(store.Load(defaults).Language, Is.EqualTo(system), code);
                }
                foreach (LanguageSetting language in Enum.GetValues(typeof(LanguageSetting)))
                {
                    string code = LanguageDefaults.ToCode(language);
                    PlayerPrefs.SetString("language", code);
                    Assert.That(new PlayerPrefsSettingsStore().Load(defaults).Language, Is.EqualTo(language));
                }
            }
            finally
            {
                if (existed) PlayerPrefs.SetString("language", saved); else PlayerPrefs.DeleteKey("language");
            }
        }

        /// <summary>Invalid snapshot language falls back without disturbing the supplied default.</summary>
        [Test]
        public void ClampRepairsInvalidLanguage()
        {
            var defaults = GameSettingsData.Defaults(1920, 1080, LanguageSetting.Turkish);
            var invalid = new GameSettingsData(1, 1, 1, 1, 0.5f, WindowModeSetting.Windowed,
                1280, 720, false, (LanguageSetting)99);
            Assert.That(invalid.Clamp(defaults).Language, Is.EqualTo(LanguageSetting.Turkish));
            Assert.Throws<ArgumentOutOfRangeException>(() => LanguageDefaults.ToCode((LanguageSetting)99));
        }
        /// <summary>Save/recreate round-trips both codes, restoring every touched preference without DeleteAll.</summary>
        [TestCase(LanguageSetting.English)]
        [TestCase(LanguageSetting.Turkish)]
        public void PlayerPrefsRoundTripsBothLanguages(LanguageSetting language)
        {
            string[] floats = { "masterVolume", "musicVolume", "sfxVolume", "uiVolume", "mouseSensitivity" };
            string[] ints = { "windowMode", "reduceMotion" };
            string[] strings = { "resolution", "language" };
            var existed = new bool[9];
            var floatValues = new float[5];
            var intValues = new int[2];
            var stringValues = new string[2];
            for (int i = 0; i < floats.Length; i++) { existed[i] = PlayerPrefs.HasKey(floats[i]); floatValues[i] = PlayerPrefs.GetFloat(floats[i]); }
            for (int i = 0; i < ints.Length; i++) { existed[i + 5] = PlayerPrefs.HasKey(ints[i]); intValues[i] = PlayerPrefs.GetInt(ints[i]); }
            for (int i = 0; i < strings.Length; i++) { existed[i + 7] = PlayerPrefs.HasKey(strings[i]); stringValues[i] = PlayerPrefs.GetString(strings[i]); }
            try
            {
                var store = new PlayerPrefsSettingsStore();
                store.Write(GameSettingsData.Defaults(1920, 1080, language));
                store.Save();
                Assert.That(PlayerPrefs.GetString("language"), Is.EqualTo(language == LanguageSetting.Turkish ? "tr" : "en"));
                var opposite = language == LanguageSetting.Turkish ? LanguageSetting.English : LanguageSetting.Turkish;
                Assert.That(new PlayerPrefsSettingsStore().Load(GameSettingsData.Defaults(1920, 1080, opposite)).Language, Is.EqualTo(language));
            }
            finally
            {
                for (int i = 0; i < floats.Length; i++)
                    if (existed[i]) PlayerPrefs.SetFloat(floats[i], floatValues[i]); else PlayerPrefs.DeleteKey(floats[i]);
                for (int i = 0; i < ints.Length; i++)
                    if (existed[i + 5]) PlayerPrefs.SetInt(ints[i], intValues[i]); else PlayerPrefs.DeleteKey(ints[i]);
                for (int i = 0; i < strings.Length; i++)
                    if (existed[i + 7]) PlayerPrefs.SetString(strings[i], stringValues[i]); else PlayerPrefs.DeleteKey(strings[i]);
                PlayerPrefs.Save();
            }
        }

    }
}
