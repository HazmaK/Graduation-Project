using System;
using System.Collections.Generic;
using RetroFPS.Localization;
using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Pre-cased bilingual strings with ordinal runtime caches.</summary>
    [CreateAssetMenu(fileName = "MainMenu", menuName = "RetroFPS/Menu/String Table")]
    public sealed class StringTable : ScriptableObject, ILocalizedStringProvider
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private string value;
            [SerializeField] private string en;
            internal string Key => key;
            internal string Turkish => value;
            internal string English => en;
            internal Entry(Translation source) { key = source.Key; value = source.Turkish; en = source.English; }
            internal bool Fill(Translation source)
            {
                bool changed = false;
                if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(source.Turkish))
                { value = source.Turkish; changed = true; }
                if (string.IsNullOrWhiteSpace(en) && !string.IsNullOrWhiteSpace(source.English))
                { en = source.English; changed = true; }
                return changed;
            }
        }

        /// <summary>Immutable bilingual seed used by builders.</summary>
        public readonly struct Translation
        {
            /// <summary>Ordinal key.</summary>
            public string Key { get; }
            /// <summary>Turkish copy.</summary>
            public string Turkish { get; }
            /// <summary>English copy.</summary>
            public string English { get; }
            /// <summary>Creates a keyed seed.</summary>
            public Translation(string key, string turkish, string english)
            { Key = key; Turkish = turkish; English = english; }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> englishLookup = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> warnedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> warnedEnglishKeys = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Number of distinct authored keys.</summary>
        public int Count => lookup.Count;
        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        /// <summary>Rebuilds both caches outside rendering; rejects duplicate keys.</summary>
        public void RebuildCache()
        {
            CheckKeys();
            lookup.Clear(); englishLookup.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                lookup.Add(entry.Key, entry.Turkish);
                englishLookup.Add(entry.Key, entry.English);
            }
        }

        /// <summary>Uses the sole live settings language authority.</summary>
        public string Get(string key) => Get(key, GameSettings.Current.Language);

        /// <inheritdoc/>
        public string Get(string key, LanguageSetting language)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!LanguageDefaults.IsValid(language)) throw new ArgumentOutOfRangeException(nameof(language));
            Dictionary<string, string> selected = language == LanguageSetting.Turkish ? lookup : englishLookup;
            if (selected.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text)) return text;
            HashSet<string> warnings = language == LanguageSetting.Turkish ? warnedKeys : warnedEnglishKeys;
            if (warnings.Add(key)) Debug.LogWarning("Missing menu string: " + key, this);
            return lookup.TryGetValue(key, out text) && !string.IsNullOrWhiteSpace(text) ? text : key;
        }

        /// <summary>Reports whether an exact authored key exists.</summary>
        public bool ContainsKey(string key) => key != null && lookup.ContainsKey(key);

        private void CheckKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] == null || string.IsNullOrWhiteSpace(entries[i].Key) || !keys.Add(entries[i].Key))
                    throw new InvalidOperationException("Invalid or duplicate string key at index " + i);
        }

        /// <summary>Rejects missing translations without consulting runtime fallback.</summary>
        public void ValidateEntries()
        {
            CheckKeys();
            for (int i = 0; i < entries.Count; i++)
                if (string.IsNullOrWhiteSpace(entries[i].Turkish) || string.IsNullOrWhiteSpace(entries[i].English))
                    throw new InvalidOperationException("Missing bilingual translation: " + entries[i].Key);
        }

        /// <summary>Fills absent/empty cells independently, preserves authored/unknown values and rebuilds once.
        /// Rejects duplicate input/existing keys before mutation; returns whether serialized data changed.</summary>
        public bool UpsertEntries(IReadOnlyList<Translation> defaults)
        {
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));
            CheckKeys();
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < defaults.Count; i++)
                if (string.IsNullOrWhiteSpace(defaults[i].Key) || !seeds.Add(defaults[i].Key))
                    throw new ArgumentException("Invalid or duplicate seed key.", nameof(defaults));
            var authored = new Dictionary<string, Entry>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++) authored.Add(entries[i].Key, entries[i]);
            bool changed = false;
            for (int i = 0; i < defaults.Count; i++)
            {
                Translation seed = defaults[i];
                if (authored.TryGetValue(seed.Key, out Entry entry)) changed |= entry.Fill(seed);
                else { entries.Add(new Entry(seed)); changed = true; }
            }
            if (changed) RebuildCache();
            return changed;
        }

        /// <summary>Non-destructively fills the approved 36-key bilingual MainMenu manifest.</summary>
        public bool PopulateBilingualDefaults() => UpsertEntries(new[]
        {
            new Translation("menu.title", "PURGATOR", "PURGATOR"),
            new Translation("menu.start", "BAŞLA", "START"),
            new Translation("menu.settings", "AYARLAR", "SETTINGS"),
            new Translation("menu.quit", "ÇIKIŞ", "QUIT"),
            new Translation("menu.version_format", "v{0}", "v{0}"),
            new Translation("menu.load_error", "Seviye yüklenemedi", "Level could not be loaded"),
            new Translation("settings.title", "AYARLAR", "SETTINGS"),
            new Translation("settings.master_volume", "Ana Ses", "Master Volume"),
            new Translation("settings.music_volume", "Müzik", "Music"),
            new Translation("settings.sfx_volume", "Ses Efektleri", "Sound Effects"),
            new Translation("settings.ui_volume", "Arayüz Sesi", "UI Volume"),
            new Translation("settings.group.audio", "SES", "AUDIO"),
            new Translation("settings.group.controls", "KONTROL", "CONTROLS"),
            new Translation("settings.group.display", "GÖRÜNTÜ", "DISPLAY"),
            new Translation("settings.mouse_sensitivity", "Fare Hassasiyeti", "Mouse Sensitivity"),
            new Translation("settings.window_mode", "Pencere Modu", "Window Mode"),
            new Translation("settings.window_mode.fullscreen", "Tam Ekran", "Fullscreen"),
            new Translation("settings.window_mode.borderless", "Çerçevesiz", "Borderless"),
            new Translation("settings.window_mode.windowed", "Pencere", "Windowed"),
            new Translation("settings.resolution", "Çözünürlük", "Resolution"),
            new Translation("settings.reduce_motion", "Hareketi Azalt", "Reduce Motion"),
            new Translation("settings.post_processing", "Görüntü İşleme", "Post-Processing"),
            new Translation("settings.brightness", "Parlaklık", "Brightness"),
            new Translation("settings.difficulty", "Zorluk", "Difficulty"),
            new Translation("settings.difficulty.easy", "Kolay", "Easy"),
            new Translation("settings.difficulty.normal", "Normal", "Normal"),
            new Translation("settings.difficulty.hard", "Zor", "Hard"),
            new Translation("settings.screen_effects", "Ekran Efektleri", "Screen Effects"),
            new Translation("settings.on", "AÇIK", "ON"),
            new Translation("settings.off", "KAPALI", "OFF"),
            new Translation("settings.back", "GERİ", "BACK"),
            new Translation("settings.percent_format", "%{0}", "{0}%"),
            new Translation("display.revert_title", "Görüntü ayarları korunsun mu?", "Keep these display settings?"),
            new Translation("display.revert_countdown_format", "{0} saniye içinde geri alınacak", "Reverting in {0} seconds"),
            new Translation("display.keep", "Onayla", "Keep"),
            new Translation("display.revert", "Geri al", "Revert"),
            new Translation("quit.prompt", "Oyundan çıkılsın mı?", "Quit the game?"),
            new Translation("quit.yes", "EVET", "YES"),
            new Translation("quit.no", "HAYIR", "NO"),
            new Translation("settings.group.general", "GENEL", "GENERAL"),
            new Translation("settings.language", "Dil", "Language"),
            new Translation("language.tr", "Türkçe", "Türkçe"),
            new Translation("language.en", "English", "English")
        });

        /// <summary>Compatible wrapper; now fills both languages without erasing authored values.</summary>
        public void PopulateTurkishDefaults() => PopulateBilingualDefaults();
    }
}
