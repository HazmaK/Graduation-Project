using NUnit.Framework;
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
                Assert.That(table.Get("missing.key"), Is.EqualTo("missing.key"));
                table.RebuildCache();
                Assert.That(table.Get("missing.key"), Is.EqualTo("missing.key"));
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
                Assert.That(table.Count, Is.EqualTo(RequiredKeys.Length));
                Assert.That(table.Get("menu.start"), Is.EqualTo("BAŞLA"));
                Assert.That(table.Get("menu.quit"), Is.EqualTo("ÇIKIŞ"));
                Assert.That(table.Get("settings.back"), Is.EqualTo("GERİ"));
                Assert.That(table.Get("quit.prompt"), Is.EqualTo("Oyundan çıkılsın mı?"));
                Assert.That(table.Get("settings.percent_format"), Is.EqualTo("%{0}"));
                Assert.That(string.Format(table.Get("display.revert_countdown_format"), 10), Does.Contain("10"));
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

        private static void AssertComplete(StringTable table)
        {
            foreach (string key in RequiredKeys)
            {
                Assert.That(table.ContainsKey(key), Is.True, key);
                Assert.That(table.Get(key), Is.Not.Null.And.Not.Empty, key);
                Assert.That(table.Get(key), Is.Not.EqualTo(key), key);
            }
        }
    }
}
