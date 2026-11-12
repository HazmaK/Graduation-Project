using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Pre-cased Turkish menu strings with an ordinal runtime lookup cache.</summary>
    [CreateAssetMenu(fileName = "MainMenu", menuName = "RetroFPS/Menu/String Table")]
    public sealed class StringTable : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private string value;
            public string Key => key;
            public string Value => value;
            public Entry(string key, string value) { this.key = key; this.value = value; }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> warnedKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Gets the number of distinct localized keys.</summary>
        public int Count => lookup.Count;

        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        /// <summary>Rebuilds runtime storage after authoring; not called in a hot path.</summary>
        public void RebuildCache()
        {
            lookup.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && !string.IsNullOrEmpty(entry.Key)) lookup[entry.Key] = entry.Value ?? entry.Key;
            }
        }

        /// <summary>Returns localized text, or the original key with a single warning per missing key.</summary>
        public string Get(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (lookup.TryGetValue(key, out string value)) return value;
            if (warnedKeys.Add(key)) Debug.LogWarning("Missing menu string: " + key, this);
            return key;
        }

        /// <summary>Reports whether an exact package-compatible key exists.</summary>
        public bool ContainsKey(string key) => key != null && lookup.ContainsKey(key);

        /// <summary>Replaces authored entries with all Turkish defaults from UX section 13.</summary>
        public void PopulateTurkishDefaults()
        {
            entries.Clear();
            entries.Add(new Entry("menu.title", "PURGATOR"));
            entries.Add(new Entry("menu.start", "BAŞLA"));
            entries.Add(new Entry("menu.settings", "AYARLAR"));
            entries.Add(new Entry("menu.quit", "ÇIKIŞ"));
            entries.Add(new Entry("menu.version_format", "v{0}"));
            entries.Add(new Entry("menu.load_error", "Seviye yüklenemedi"));
            entries.Add(new Entry("settings.title", "AYARLAR"));
            entries.Add(new Entry("settings.master_volume", "Ana Ses"));
            entries.Add(new Entry("settings.music_volume", "Müzik"));
            entries.Add(new Entry("settings.sfx_volume", "Ses Efektleri"));
            entries.Add(new Entry("settings.ui_volume", "Arayüz Sesi"));
            entries.Add(new Entry("settings.group.audio", "SES"));
            entries.Add(new Entry("settings.group.controls", "KONTROL"));
            entries.Add(new Entry("settings.group.display", "GÖRÜNTÜ"));
            entries.Add(new Entry("settings.mouse_sensitivity", "Fare Hassasiyeti"));
            entries.Add(new Entry("settings.window_mode", "Pencere Modu"));
            entries.Add(new Entry("settings.window_mode.fullscreen", "Tam Ekran"));
            entries.Add(new Entry("settings.window_mode.borderless", "Çerçevesiz"));
            entries.Add(new Entry("settings.window_mode.windowed", "Pencere"));
            entries.Add(new Entry("settings.resolution", "Çözünürlük"));
            entries.Add(new Entry("settings.reduce_motion", "Hareketi Azalt"));
            entries.Add(new Entry("settings.on", "AÇIK"));
            entries.Add(new Entry("settings.off", "KAPALI"));
            entries.Add(new Entry("settings.back", "GERİ"));
            entries.Add(new Entry("settings.percent_format", "%{0}"));
            entries.Add(new Entry("display.revert_title", "Görüntü ayarları korunsun mu?"));
            entries.Add(new Entry("display.revert_countdown_format", "{0} saniye içinde geri alınacak"));
            entries.Add(new Entry("display.keep", "Onayla"));
            entries.Add(new Entry("display.revert", "Geri al"));
            entries.Add(new Entry("quit.prompt", "Oyundan çıkılsın mı?"));
            entries.Add(new Entry("quit.yes", "EVET"));
            entries.Add(new Entry("quit.no", "HAYIR"));
            RebuildCache();
        }
    }
}
