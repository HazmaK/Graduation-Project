using System;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests
{
    public sealed class PauseStringTableTests
    {
        private static void Upsert(StringTable table)
        {
            Type builder = Type.GetType("RetroFPS.Editor.PauseMenuBuilder, RetroFPS.Editor", true);
            builder.GetMethod("UpsertStrings", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { table });
        }
        [Test]
        public void GeneratedTableContainsExactAuthoredPauseTranslations()
        {
            StringTable table = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/_Project/Data/UI/StringTable_MainMenu_tr.asset");
            Assert.That(table, Is.Not.Null);
            string[] keys = { "pause.resume", "pause.main_menu", "pause.main_menu_prompt", "pause.main_menu_load_error" };
            string[] tr = { "DEVAM", "ANA MENÜ", "Ana menüye dönülsün mü? Bu koşudaki ilerleme kaybolacak.", "Ana menü yüklenemedi. Tekrar deneyin." };
            string[] en = { "RESUME", "MAIN MENU", "Return to the main menu? This run's progress will be lost.", "Could not load the main menu. Try again." };
            table.ValidateEntries();
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(table.ContainsKey(keys[i]), Is.True, "Run RetroFPS/Build Pause Menu first: " + keys[i]);
                Assert.That(table.Get(keys[i], LanguageSetting.Turkish), Is.EqualTo(tr[i]));
                Assert.That(table.Get(keys[i], LanguageSetting.English), Is.EqualTo(en[i]));
            }
        }
        [Test]
        public void ExactBilingualManifestIsPreCasedCompleteUniqueAndIdempotent()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.PopulateBilingualDefaults(); int original = table.Count;
                Upsert(table); Upsert(table);
                table.ValidateEntries();
                Assert.That(table.Count, Is.EqualTo(original + 4));
                string[] keys = { "pause.resume", "pause.main_menu", "pause.main_menu_prompt", "pause.main_menu_load_error" };
                string[] tr = { "DEVAM", "ANA MENÜ", "Ana menüye dönülsün mü? Bu koşudaki ilerleme kaybolacak.", "Ana menü yüklenemedi. Tekrar deneyin." };
                string[] en = { "RESUME", "MAIN MENU", "Return to the main menu? This run's progress will be lost.", "Could not load the main menu. Try again." };
                for (int i = 0; i < keys.Length; i++)
                {
                    Assert.That(table.Get(keys[i], LanguageSetting.Turkish), Is.EqualTo(tr[i]));
                    Assert.That(table.Get(keys[i], LanguageSetting.English), Is.EqualTo(en[i]));
                }
                Assert.That(table.Get("menu.settings", LanguageSetting.Turkish), Is.EqualTo("AYARLAR"));
                Assert.That(table.Get("quit.no", LanguageSetting.Turkish), Is.EqualTo("HAYIR"));
                Assert.That(table.Get("quit.yes", LanguageSetting.Turkish), Is.EqualTo("EVET"));
            }
            finally { UnityEngine.Object.DestroyImmediate(table); }
        }
        [Test]
        public void UpsertPreservesAuthoredPauseAndAllSharedSettingsCells()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                table.PopulateBilingualDefaults();
                table.UpsertEntries(new[] { new StringTable.Translation("pause.resume", "ÖZEL", "CUSTOM"), new StringTable.Translation("custom.key", "koru", "preserve") });
                var data = new SerializedObject(table); SerializedProperty entries = data.FindProperty("entries");
                string before = EditorJsonUtility.ToJson(table);
                string[] sharedKeys = new string[entries.arraySize]; string[] tr = new string[entries.arraySize], en = new string[entries.arraySize];
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    sharedKeys[i] = entry.FindPropertyRelative("key").stringValue;
                    tr[i] = entry.FindPropertyRelative("value").stringValue; en[i] = entry.FindPropertyRelative("en").stringValue;
                }
                Upsert(table);
                for (int i = 0; i < sharedKeys.Length; i++)
                {
                    Assert.That(table.Get(sharedKeys[i], LanguageSetting.Turkish), Is.EqualTo(tr[i]));
                    Assert.That(table.Get(sharedKeys[i], LanguageSetting.English), Is.EqualTo(en[i]));
                }
                string once = EditorJsonUtility.ToJson(table); Upsert(table);
                Assert.That(EditorJsonUtility.ToJson(table), Is.EqualTo(once)); Assert.That(once, Is.Not.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(table); }
        }
        [Test]
        public void SharedFontsCoverTurkishGlyphsWithoutRuntimeCasing()
        {
            MenuTheme theme = AssetDatabase.LoadAssetAtPath<MenuTheme>("Assets/_Project/Data/UI/MenuTheme_KanVeKemik.asset");
            Assert.That(theme, Is.Not.Null, "Existing main-menu theme is required.");
            foreach (char glyph in "ÇçĞğİıÖöŞşÜüDEVAM ANA MENÜ")
                if (!char.IsWhiteSpace(glyph)) Assert.That(theme.UiFont.HasCharacter(glyph, true), Is.True, "Missing Turkish glyph: " + glyph);
        }
    }
}
