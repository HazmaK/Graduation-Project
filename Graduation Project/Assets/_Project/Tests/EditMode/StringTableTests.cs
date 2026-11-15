using NUnit.Framework;
using RetroFPS.Settings;
using RetroFPS.Localization;
using RetroFPS.UI.Menu;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RetroFPS.Tests
{
    public sealed class StringTableTests
    {
        private static readonly string[] RequiredKeys =
        {
            "menu.title",
            "menu.start",
            "menu.settings",
            "menu.quit",
            "menu.version_format",
            "menu.load_error",
            "settings.title",
            "settings.master_volume",
            "settings.music_volume",
            "settings.sfx_volume",
            "settings.ui_volume",
            "settings.group.audio",
            "settings.group.controls",
            "settings.group.display",
            "settings.mouse_sensitivity",
            "settings.window_mode",
            "settings.window_mode.fullscreen",
            "settings.window_mode.borderless",
            "settings.window_mode.windowed",
            "settings.resolution",
            "settings.reduce_motion",
            "settings.on",
            "settings.off",
            "settings.back",
            "settings.percent_format",
            "display.revert_title",
            "display.revert_countdown_format",
            "display.keep",
            "display.revert",
            "quit.prompt",
            "quit.yes",
            "quit.no",
        };

        [Test]
        public void MissingKeyReturnsKeyAndWarnsOnlyOnceEvenAfterCacheRebuild()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                LogAssert.Expect(LogType.Warning, "Missing menu string: missing.key");
                Assert.That(table.Get("missing.key", LanguageSetting.Turkish), Is.EqualTo("missing.key"));
                table.RebuildCache();
                Assert.That(table.Get("missing.key", LanguageSetting.Turkish), Is.EqualTo("missing.key"));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(table); }
        }

        [Test]
        public void TurkishDefaultsContainEveryRequiredKeyAndPreserveCasing()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.PopulateTurkishDefaults();
                AssertComplete(table);
                table.PopulateTurkishDefaults();
                Assert.That(table.Count, Is.EqualTo(RequiredKeys.Length + 4));
                Assert.That(table.Get("menu.start", LanguageSetting.Turkish), Is.EqualTo("BAŞLA"));
                Assert.That(table.Get("menu.quit", LanguageSetting.Turkish), Is.EqualTo("ÇIKIŞ"));
                Assert.That(table.Get("settings.back", LanguageSetting.Turkish), Is.EqualTo("GERİ"));
                Assert.That(table.Get("quit.prompt", LanguageSetting.Turkish), Is.EqualTo("Oyundan çıkılsın mı?"));
                Assert.That(table.Get("settings.percent_format", LanguageSetting.Turkish), Is.EqualTo("%{0}"));
                Assert.That(string.Format(table.Get("display.revert_countdown_format", LanguageSetting.Turkish), 10), Does.Contain("10"));
                LogAssert.NoUnexpectedReceived();
            }
            finally { Object.DestroyImmediate(table); }
        }

        [Test]
        public void GeneratedMainMenuTableContainsEveryRequiredKey()
        {
            string[] guids = AssetDatabase.FindAssets("MainMenu t:StringTable");
            foreach (string guid in guids)
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (table == null || !table.name.StartsWith("StringTable_MainMenu", System.StringComparison.Ordinal)) continue;
                table.RebuildCache();
                AssertComplete(table);
                return;
            }
            Assert.Ignore("The menu builder has not generated the MainMenu StringTable asset.");
        }

        /// <summary>Both serialized cells match the approved menu manifest, placeholders and specified limits.</summary>
        [Test]
        public void BilingualDefaultsMatchManifestAndAreIdempotent()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                Assert.That(table.PopulateBilingualDefaults(), Is.True);
                Assert.That(table.PopulateBilingualDefaults(), Is.False);
                table.ValidateEntries();
                Assert.That(table.Count, Is.EqualTo(36));
                var expected = new[]
                {
                new StringTable.Translation("menu.title", "PURGATOR", "PURGATOR"),
                new StringTable.Translation("menu.start", "BAŞLA", "START"),
                new StringTable.Translation("menu.settings", "AYARLAR", "SETTINGS"),
                new StringTable.Translation("menu.quit", "ÇIKIŞ", "QUIT"),
                new StringTable.Translation("menu.version_format", "v{0}", "v{0}"),
                new StringTable.Translation("menu.load_error", "Seviye yüklenemedi", "Level could not be loaded"),
                new StringTable.Translation("settings.title", "AYARLAR", "SETTINGS"),
                new StringTable.Translation("settings.master_volume", "Ana Ses", "Master Volume"),
                new StringTable.Translation("settings.music_volume", "Müzik", "Music"),
                new StringTable.Translation("settings.sfx_volume", "Ses Efektleri", "Sound Effects"),
                new StringTable.Translation("settings.ui_volume", "Arayüz Sesi", "UI Volume"),
                new StringTable.Translation("settings.group.audio", "SES", "AUDIO"),
                new StringTable.Translation("settings.group.controls", "KONTROL", "CONTROLS"),
                new StringTable.Translation("settings.group.display", "GÖRÜNTÜ", "DISPLAY"),
                new StringTable.Translation("settings.mouse_sensitivity", "Fare Hassasiyeti", "Mouse Sensitivity"),
                new StringTable.Translation("settings.window_mode", "Pencere Modu", "Window Mode"),
                new StringTable.Translation("settings.window_mode.fullscreen", "Tam Ekran", "Fullscreen"),
                new StringTable.Translation("settings.window_mode.borderless", "Çerçevesiz", "Borderless"),
                new StringTable.Translation("settings.window_mode.windowed", "Pencere", "Windowed"),
                new StringTable.Translation("settings.resolution", "Çözünürlük", "Resolution"),
                new StringTable.Translation("settings.reduce_motion", "Hareketi Azalt", "Reduce Motion"),
                new StringTable.Translation("settings.on", "AÇIK", "ON"),
                new StringTable.Translation("settings.off", "KAPALI", "OFF"),
                new StringTable.Translation("settings.back", "GERİ", "BACK"),
                new StringTable.Translation("settings.percent_format", "%{0}", "{0}%"),
                new StringTable.Translation("display.revert_title", "Görüntü ayarları korunsun mu?", "Keep these display settings?"),
                new StringTable.Translation("display.revert_countdown_format", "{0} saniye içinde geri alınacak", "Reverting in {0} seconds"),
                new StringTable.Translation("display.keep", "Onayla", "Keep"),
                new StringTable.Translation("display.revert", "Geri al", "Revert"),
                new StringTable.Translation("quit.prompt", "Oyundan çıkılsın mı?", "Quit the game?"),
                new StringTable.Translation("quit.yes", "EVET", "YES"),
                new StringTable.Translation("quit.no", "HAYIR", "NO"),
                new StringTable.Translation("settings.group.general", "GENEL", "GENERAL"),
                new StringTable.Translation("settings.language", "Dil", "Language"),
                new StringTable.Translation("language.tr", "Türkçe", "Türkçe"),
                new StringTable.Translation("language.en", "English", "English")
                };
                foreach (var row in expected)
                {
                    Assert.That(table.Get(row.Key, LanguageSetting.Turkish), Is.EqualTo(row.Turkish), row.Key);
                    Assert.That(table.Get(row.Key, LanguageSetting.English), Is.EqualTo(row.English), row.Key);
                }
                var data = new SerializedObject(table);
                SerializedProperty entries = data.FindProperty("entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    string key = entry.FindPropertyRelative("key").stringValue;
                    string tr = entry.FindPropertyRelative("value").stringValue;
                    string en = entry.FindPropertyRelative("en").stringValue;
                    Assert.That(string.IsNullOrWhiteSpace(tr), Is.False, key);
                    Assert.That(string.IsNullOrWhiteSpace(en), Is.False, key);
                    Assert.That(table.Get(key, LanguageSetting.Turkish), Is.SameAs(table.Get(key, LanguageSetting.Turkish)));
                    Assert.That(table.Get(key, LanguageSetting.English), Is.EqualTo(en));
                    var placeholders = new System.Text.RegularExpressions.Regex(@"\{[0-9]+(?:,[^}:]+)?(?::[^}]+)?\}");
                    CollectionAssert.AreEqual(PlaceholderValues(placeholders, tr), PlaceholderValues(placeholders, en), key);
                    int limit = key == "menu.title" ? 16 : key == "menu.load_error" || key == "quit.prompt" || key.StartsWith("display.revert_") ? 40
                        : key.StartsWith("display.") || key == "quit.yes" || key == "quit.no" ? 10
                        : key.StartsWith("menu.") || key == "settings.back" ? 14
                        : key.StartsWith("language.") || key.StartsWith("settings.window_mode.") || key == "settings.on" || key == "settings.off" ? 18 : 26;
                    if (key != "menu.version_format" && key != "settings.percent_format")
                    { Assert.That(tr.Length, Is.LessThanOrEqualTo(limit), key); Assert.That(en.Length, Is.LessThanOrEqualTo(limit), key); }
                }
                Assert.That(table.Get("settings.percent_format", LanguageSetting.English), Is.EqualTo("{0}%"));
                Assert.That(table.Get("language.tr", LanguageSetting.English), Is.EqualTo("Türkçe"));
            }
            finally { Object.DestroyImmediate(table); }
        }

        /// <summary>Builders preserve authored and unknown values and fill only empty cells.</summary>
        [Test]
        public void UpsertsPreserveAuthoredCellsAndRejectDuplicateSeeds()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.UpsertEntries(new[] { new StringTable.Translation("menu.start", "ÖZEL", ""), new StringTable.Translation("custom", "TR", "EN") });
                Assert.Throws<System.InvalidOperationException>(() => table.ValidateEntries());
                table.PopulateBilingualDefaults();
                Assert.That(table.Get("menu.start", LanguageSetting.Turkish), Is.EqualTo("ÖZEL"));
                Assert.That(table.Get("menu.start", LanguageSetting.English), Is.EqualTo("START"));
                Assert.That(table.Get("custom", LanguageSetting.English), Is.EqualTo("EN"));
                Assert.That(table.PopulateBilingualDefaults(), Is.False);
                int count = table.Count;
                Assert.Throws<System.ArgumentException>(() => table.UpsertEntries(new[] {
                    new StringTable.Translation("new", "TR", "EN"), new StringTable.Translation("new", "TR", "EN") }));
                Assert.That(table.Count, Is.EqualTo(count));
                var field = typeof(StringTable).GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var list = (System.Collections.IList)field.GetValue(table);
                list.Add(list[0]);
                Assert.Throws<System.InvalidOperationException>(() => table.RebuildCache());
            }
            finally { Object.DestroyImmediate(table); }
        }

        /// <summary>Fallback is Turkish then key; each language warns once across cache rebuilds.</summary>
        [Test]
        public void MissingTranslationFallbackAndProviderInjection()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.UpsertEntries(new[] { new StringTable.Translation("partial", "Türkçe", " ") });
                LogAssert.Expect(LogType.Warning, "Missing menu string: partial");
                Assert.That(table.Get("partial", LanguageSetting.English), Is.EqualTo("Türkçe"));
                table.RebuildCache();
                Assert.That(table.Get("partial", LanguageSetting.English), Is.EqualTo("Türkçe"));
                LocalizedStrings.Register(table);
                Assert.That(LocalizedStrings.Provider, Is.SameAs(table));
                Assert.That(LocalizedStrings.Get("partial", LanguageSetting.English), Is.EqualTo("Türkçe"));
                LocalizedStrings.Reset();
                Assert.That(LocalizedStrings.Get("partial", LanguageSetting.English), Is.EqualTo("partial"));
            }
            finally { LocalizedStrings.Reset(); Object.DestroyImmediate(table); }
        }

        /// <summary>The compatible lookup follows the current settings snapshot without table-owned locale state.</summary>
        [Test]
        public void CompatibleLookupTracksSettingsAuthority()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.PopulateBilingualDefaults();
                var service = new RetroFPS.Settings.SettingsService(new MemoryStore(), new NoDisplay(), new NoAudio(), GameSettingsData.Defaults());
                GameSettings.SetServiceForTests(service);
                Assert.That(table.Get("menu.start"), Is.EqualTo("START"));
                service.SetLanguage(LanguageSetting.Turkish);
                Assert.That(table.Get("menu.start"), Is.EqualTo("BAŞLA"));
            }
            finally { GameSettings.SetServiceForTests(null); Object.DestroyImmediate(table); }
        }

        private sealed class MemoryStore : ISettingsStore
        {
            public GameSettingsData Load(GameSettingsData defaults) => defaults;
            public void Write(GameSettingsData data) { }
            public void Save() { }
        }
        private sealed class NoDisplay : IDisplayApplier
        { public void Apply(WindowModeSetting mode, int width, int height) { } }
        private sealed class NoAudio : IAudioVolumeApplier
        { public void Apply(GameSettingsData data) { } }

        /// <summary>Checks the complete serialized manifests after menu, HUD and pause builder migration.</summary>
        [Test]
        public void MigratedAssetsMatchFullBilingualManifest()
        {
            var menu = new[]
            {
                new StringTable.Translation("menu.title", "PURGATOR", "PURGATOR"),
                new StringTable.Translation("menu.start", "BAŞLA", "START"),
                new StringTable.Translation("menu.settings", "AYARLAR", "SETTINGS"),
                new StringTable.Translation("menu.quit", "ÇIKIŞ", "QUIT"),
                new StringTable.Translation("menu.version_format", "v{0}", "v{0}"),
                new StringTable.Translation("menu.load_error", "Seviye yüklenemedi", "Level could not be loaded"),
                new StringTable.Translation("settings.title", "AYARLAR", "SETTINGS"),
                new StringTable.Translation("settings.master_volume", "Ana Ses", "Master Volume"),
                new StringTable.Translation("settings.music_volume", "Müzik", "Music"),
                new StringTable.Translation("settings.sfx_volume", "Ses Efektleri", "Sound Effects"),
                new StringTable.Translation("settings.ui_volume", "Arayüz Sesi", "UI Volume"),
                new StringTable.Translation("settings.group.audio", "SES", "AUDIO"),
                new StringTable.Translation("settings.group.controls", "KONTROL", "CONTROLS"),
                new StringTable.Translation("settings.group.display", "GÖRÜNTÜ", "DISPLAY"),
                new StringTable.Translation("settings.mouse_sensitivity", "Fare Hassasiyeti", "Mouse Sensitivity"),
                new StringTable.Translation("settings.window_mode", "Pencere Modu", "Window Mode"),
                new StringTable.Translation("settings.window_mode.fullscreen", "Tam Ekran", "Fullscreen"),
                new StringTable.Translation("settings.window_mode.borderless", "Çerçevesiz", "Borderless"),
                new StringTable.Translation("settings.window_mode.windowed", "Pencere", "Windowed"),
                new StringTable.Translation("settings.resolution", "Çözünürlük", "Resolution"),
                new StringTable.Translation("settings.reduce_motion", "Hareketi Azalt", "Reduce Motion"),
                new StringTable.Translation("settings.on", "AÇIK", "ON"),
                new StringTable.Translation("settings.off", "KAPALI", "OFF"),
                new StringTable.Translation("settings.back", "GERİ", "BACK"),
                new StringTable.Translation("settings.percent_format", "%{0}", "{0}%"),
                new StringTable.Translation("display.revert_title", "Görüntü ayarları korunsun mu?", "Keep these display settings?"),
                new StringTable.Translation("display.revert_countdown_format", "{0} saniye içinde geri alınacak", "Reverting in {0} seconds"),
                new StringTable.Translation("display.keep", "Onayla", "Keep"),
                new StringTable.Translation("display.revert", "Geri al", "Revert"),
                new StringTable.Translation("quit.prompt", "Oyundan çıkılsın mı?", "Quit the game?"),
                new StringTable.Translation("quit.yes", "EVET", "YES"),
                new StringTable.Translation("quit.no", "HAYIR", "NO"),
                new StringTable.Translation("settings.group.general", "GENEL", "GENERAL"),
                new StringTable.Translation("settings.language", "Dil", "Language"),
                new StringTable.Translation("language.tr", "Türkçe", "Türkçe"),
                new StringTable.Translation("language.en", "English", "English"),
                new StringTable.Translation("pause.resume", "DEVAM", "RESUME"),
                new StringTable.Translation("pause.main_menu", "ANA MENÜ", "MAIN MENU"),
                new StringTable.Translation("pause.main_menu_prompt", "Ana menüye dönülsün mü? Bu koşudaki ilerleme kaybolacak.", "Return to the main menu? This run's progress will be lost."),
                new StringTable.Translation("pause.main_menu_load_error", "Ana menü yüklenemedi. Tekrar deneyin.", "Could not load the main menu. Try again.")
            };
            var combat = new[]
            {
                new StringTable.Translation("hud.health.label", "CAN", "HEALTH"),
                new StringTable.Translation("hud.dash.label", "ATILMA", "DASH"),
                new StringTable.Translation("hud.parry.label", "PARRY", "PARRY"),
                new StringTable.Translation("hud.parry.ready", "HAZIR", "READY"),
                new StringTable.Translation("hud.parry.active", "AKTİF", "ACTIVE"),
                new StringTable.Translation("hud.parry.cooldown", "BEKLE", "WAIT"),
                new StringTable.Translation("hud.parry.success", "PARRY!", "PARRIED"),
                new StringTable.Translation("hud.hook.label", "KANCA", "HOOK"),
                new StringTable.Translation("hud.hook.ready", "HAZIR", "READY"),
                new StringTable.Translation("hud.hook.cooldown", "BEKLE", "WAIT"),
                new StringTable.Translation("hud.ammo.empty", "BOŞ", "EMPTY"),
                new StringTable.Translation("hud.weapon.melee", "YAKIN", "MELEE"),
                new StringTable.Translation("hud.ammo.bolts", "OK", "BOLTS"),
                new StringTable.Translation("hud.ammo.shells", "SAÇMA", "SHELLS"),
                new StringTable.Translation("hud.wave.counter", "DALGA {0}/{1}", "WAVE {0}/{1}"),
                new StringTable.Translation("hud.wave.remaining", "KALAN {0}", "LEFT {0}"),
                new StringTable.Translation("hud.wave.label", "DALGA", "WAVE"),
                new StringTable.Translation("hud.wave.remainingLabel", "KALAN", "LEFT"),
                new StringTable.Translation("hud.araf.roomsLabel", "ODALAR", "ROOMS"),
                new StringTable.Translation("hud.araf.ready", "ARAF: HAZIR", "ARAF: READY"),
                new StringTable.Translation("hud.araf.used", "ARAF: KULLANILDI", "ARAF: USED"),
                new StringTable.Translation("hud.araf.warning", "ARAF'TASIN — ÇIKIŞ ODASINI BUL", "IN ARAF — FIND THE EXIT ROOM"),
                new StringTable.Translation("hud.araf.rooms", "ODALAR {0}/{1}", "ROOMS {0}/{1}"),
                new StringTable.Translation("hud.araf.exitFound", "ÇIKIŞ BULUNDU", "EXIT FOUND"),
                new StringTable.Translation("hud.banner.arenaSealed", "ARENA MÜHÜRLENDİ", "ARENA SEALED"),
                new StringTable.Translation("hud.banner.wave", "DALGA {0}", "WAVE {0}"),
                new StringTable.Translation("hud.banner.waveCleared", "DALGA {0} TEMİZLENDİ", "WAVE {0} CLEARED"),
                new StringTable.Translation("hud.banner.arenaCleared", "ARENA TEMİZLENDİ", "ARENA CLEARED"),
                new StringTable.Translation("hud.truedeath.title", "GERÇEK ÖLÜM", "TRUE DEATH"),
                new StringTable.Translation("hud.truedeath.restart", "TEKRAR DENE", "TRY AGAIN"),
                new StringTable.Translation("hud.weapon.RepeaterArbalest", "ARBALET", "ARBALEST"),
                new StringTable.Translation("hud.weapon.ExecutionerBlade", "CELLAT KILICI", "EXECUTIONER"),
                new StringTable.Translation("hud.weapon.GothicShotgun", "GOTİK POMPALI", "GOTHIC SHOTGUN"),
                new StringTable.Translation("hud.symbol.slash", "/", "/"),
                new StringTable.Translation("hud.symbol.plus", "+", "+"),
                new StringTable.Translation("hud.symbol.warning", "!", "!"),
                new StringTable.Translation("hud.symbol.slotOpen", "[", "["),
                new StringTable.Translation("hud.symbol.slotClose", "]", "]"),
                new StringTable.Translation("hud.dash.counter", "ATILMA {0}/{1}", "DASH {0}/{1}"),
                new StringTable.Translation("popup.critical", "CRIT!", "CRIT!"),
                new StringTable.Translation("popup.reflected", "PARRY", "PARRY"),
                new StringTable.Translation("popup.health", "HP", "HP"),
                new StringTable.Translation("popup.bolts", "OK", "BOLTS"),
                new StringTable.Translation("popup.shells", "SAÇMA", "SHELLS"),
                new StringTable.Translation("transition.araf.enter", "ARAF", "ARAF"),
                new StringTable.Translation("transition.araf.escape", "KAÇIŞ", "ESCAPE"),
                new StringTable.Translation("boss.judge.name", "YARGIÇ", "THE JUDGE")
            };
            AssertAssetManifest("Assets/_Project/Data/UI/StringTable_MainMenu_tr.asset", menu);
            AssertAssetManifest("Assets/_Project/Data/UI/StringTable_CombatHud_tr.asset", combat);
            var catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>("Assets/_Project/Resources/Localization/LocalizationCatalog.asset");
            Assert.That(catalog, Is.Not.Null, "Run the localization builder to create the catalog.");
            Assert.That(catalog.CombatTable, Is.SameAs(AssetDatabase.LoadAssetAtPath<StringTable>("Assets/_Project/Data/UI/StringTable_CombatHud_tr.asset")));
        }

        /// <summary>The P3 builder migration shortens only the legacy English cell and is repeatable.</summary>
        [TestCase("EXECUTIONER BLADE", "EXECUTIONER")]
        [TestCase("CUSTOM BLADE", "CUSTOM BLADE")]
        public void CombatCopyRevisionPreservesOtherAuthoredCells(string english, string expected)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            System.Type builder = null;
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                builder = assembly.GetType("RetroFPS.Editor.CombatHudBuilder");
                if (builder != null) break;
            }
            Assert.That(builder, Is.Not.Null);
            System.Reflection.FieldInfo field = builder.GetField("strings", flags);
            System.Reflection.MethodInfo populate = builder.GetMethod("PopulateStrings", flags);
            object previous = field.GetValue(null);
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.UpsertEntries(new[] {
                    new StringTable.Translation("hud.weapon.ExecutionerBlade", "ÖZEL KILIÇ", english),
                    new StringTable.Translation("hud.health.label", "ÖZEL CAN", "CUSTOM HEALTH"),
                    new StringTable.Translation("custom.key", "ÖZEL", "CUSTOM") });
                field.SetValue(null, table);
                populate.Invoke(null, null);
                Assert.That(table.Get("hud.weapon.ExecutionerBlade", LanguageSetting.English), Is.EqualTo(expected));
                Assert.That(table.Get("hud.weapon.ExecutionerBlade", LanguageSetting.Turkish), Is.EqualTo("ÖZEL KILIÇ"));
                Assert.That(table.Get("hud.health.label", LanguageSetting.English), Is.EqualTo("CUSTOM HEALTH"));
                Assert.That(table.Get("hud.health.label", LanguageSetting.Turkish), Is.EqualTo("ÖZEL CAN"));
                Assert.That(table.Get("custom.key", LanguageSetting.English), Is.EqualTo("CUSTOM"));
                Assert.That(table.Get("custom.key", LanguageSetting.Turkish), Is.EqualTo("ÖZEL"));
                string snapshot = EditorJsonUtility.ToJson(table);
                populate.Invoke(null, null);
                Assert.That(EditorJsonUtility.ToJson(table), Is.EqualTo(snapshot));
            }
            finally { field.SetValue(null, previous); Object.DestroyImmediate(table); }
        }

        private static void AssertAssetManifest(string path, StringTable.Translation[] expected)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
            if (table == null) Assert.Ignore("StringTable assets are generated by the separately owned builders.");
            var data = new SerializedObject(table);
            SerializedProperty entries = data.FindProperty("entries");
            bool hasEnglish = false;
            for (int i = 0; i < entries.arraySize; i++)
                hasEnglish |= !string.IsNullOrWhiteSpace(entries.GetArrayElementAtIndex(i).FindPropertyRelative("en").stringValue);
            if (!hasEnglish) Assert.Ignore("P0 preserves assets; run the bilingual builders before asset verification.");
            Assert.That(entries.arraySize, Is.EqualTo(expected.Length), path);
            table.ValidateEntries();
            foreach (var row in expected)
            {
                SerializedProperty found = null;
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == row.Key)
                    { found = entries.GetArrayElementAtIndex(i); break; }
                Assert.That(found, Is.Not.Null, row.Key);
                Assert.That(found.FindPropertyRelative("value").stringValue, Is.EqualTo(row.Turkish), row.Key);
                Assert.That(found.FindPropertyRelative("en").stringValue, Is.EqualTo(row.English), row.Key);
            }
        }

        /// <summary>Warmed explicit-language lookups return stored references without managed allocations.</summary>
        [Test]
        public void WarmedLookupsAllocateNothing()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.PopulateBilingualDefaults();
                string tr = table.Get("menu.start", LanguageSetting.Turkish);
                string en = table.Get("menu.start", LanguageSetting.English);
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                string actualTr = null, actualEn = null;
                for (int i = 0; i < 1000; i++)
                {
                    actualTr = table.Get("menu.start", LanguageSetting.Turkish);
                    actualEn = table.Get("menu.start", LanguageSetting.English);
                }
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
                Assert.That(actualTr, Is.SameAs(tr));
                Assert.That(actualEn, Is.SameAs(en));
            }
            finally { Object.DestroyImmediate(table); }
        }

        private static string[] PlaceholderValues(System.Text.RegularExpressions.Regex pattern, string value)
        {
            var matches = pattern.Matches(value);
            var result = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++) result[i] = matches[i].Value;
            return result;
        }

        private static void AssertComplete(StringTable table)
        {
            foreach (string key in RequiredKeys)
            {
                Assert.That(table.ContainsKey(key), Is.True, key);
                Assert.That(table.Get(key, LanguageSetting.Turkish), Is.Not.Null.And.Not.Empty, key);
                Assert.That(table.Get(key, LanguageSetting.Turkish), Is.Not.EqualTo(key), key);
            }
        }
    }
}
