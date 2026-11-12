using System.IO;
using NUnit.Framework;
using RetroFPS.Presentation;
using RetroFPS.Run;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace RetroFPS.Tests
{
    /// <summary>Checks generated prefab contracts after an asset reload; no builder is run by these tests.</summary>
    public sealed class CombatHudPrefabTests
    {
        private const string HudPath = "Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string DirectorPath = "Assets/_Project/Prefabs/Systems/RunDirector.prefab";

        private static GameObject Hud()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (prefab == null) Assert.Ignore("Run RetroFPS/Build Combat HUD first; the prefab is generated.");
            return prefab;
        }
        private static Object Reference(Object owner, string field)
        {
            Assert.IsNotNull(owner);
            var property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, field);
            return property.objectReferenceValue;
        }

        [Test]
        public void Hud_HasOneOverlayCanvasAndHeightMatchedScaler()
        {
            GameObject hud = Hud();
            Canvas[] canvases = hud.GetComponentsInChildren<Canvas>(true);
            var scalers = hud.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true);
            Assert.AreEqual(1, canvases.Length); Assert.AreEqual(1, scalers.Length);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvases[0].renderMode);
            Assert.AreEqual(100, canvases[0].sortingOrder);
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, scalers[0].uiScaleMode);
            Assert.AreEqual(new Vector2(1920, 1080), scalers[0].referenceResolution);
            Assert.AreEqual(1f, scalers[0].matchWidthOrHeight);
            Assert.IsNotNull(canvases[0].GetComponent<UnityEngine.UI.GraphicRaycaster>());
            Assert.IsFalse(Reference(hud.GetComponentInChildren<TrueDeathView>(true), "deathLayer") is GameObject layer && layer.activeSelf);
        }

        [Test]
        public void Hud_InputReferencesArePersistentAfterReloadAndFocusIsWired()
        {
            Hud();
            AssetDatabase.ImportAsset(HudPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            GameObject hud = Hud();
            var modules = hud.GetComponentsInChildren<InputSystemUIInputModule>(true);
            Assert.AreEqual(1, modules.Length);
            string[] fields = { "m_ActionsAsset", "m_PointAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_LeftClickAction", "m_RightClickAction", "m_ScrollWheelAction" };
            foreach (string field in fields)
            {
                Object reference = Reference(modules[0], field); Assert.IsNotNull(reference, field);
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference, out string guid, out long fileId), field);
                Assert.IsNotEmpty(guid, field); Assert.AreNotEqual(0L, fileId, field);
            }
            Assert.IsFalse(modules[0].deselectOnBackgroundClick);
            var view = hud.GetComponentInChildren<TrueDeathView>(true);
            foreach (string field in new[] { "restartButton", "buttonLabel", "focusSeal", "focusUnderline", "eventSystemGuard", "gameplayGroup", "cardGroup" })
                Assert.IsNotNull(Reference(view, field), field);
            var system = hud.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true);
            Assert.AreEqual(((UnityEngine.UI.Button)Reference(view, "restartButton")).gameObject, system.firstSelectedGameObject);
        }

        [Test]
        public void Player_NestsHudAndSerializesAllSourcesAndOneSharedConfig()
        {
            Hud();
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Assert.IsNotNull(player);
            HudBindings[] bindings = player.GetComponentsInChildren<HudBindings>(true);
            Assert.AreEqual(1, bindings.Length);
            foreach (string field in new[] { "health", "movement", "parry", "weapons", "ammo", "hud", "healthView", "abilityView", "weaponView", "encounterView", "bannerView", "deathView" })
            {
                Object value = Reference(bindings[0], field); Assert.IsNotNull(value, field);
                if (value is Component source) Assert.IsTrue(source.transform.IsChildOf(player.transform), field + " escapes Player.prefab");
            }
            Object config = Reference(bindings[0], "config"); Assert.IsInstanceOf<HudConfig>(config);
            Assert.AreEqual(config, Reference(player.GetComponent<PlayerDamageFeedback>(), "hudConfig"));
            var debug = player.GetComponent<PlayerDebugHUD>();
            Assert.AreEqual(config, Reference(debug, "hudConfig"));
            Assert.IsFalse(new SerializedObject(debug).FindProperty("showHUD").boolValue);
            GameObject nested = (GameObject)Reference(bindings[0], "hud");
            Assert.AreEqual(HudPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nested));
            foreach (MonoBehaviour view in nested.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var field = new SerializedObject(view).FindProperty("config");
                if (field != null) Assert.AreEqual(config, field.objectReferenceValue, view.GetType().Name);
            }
            Assert.AreEqual(1, player.GetComponentsInChildren<Canvas>(true).Length);
            Assert.AreEqual(1, player.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true).Length);
            // Standalone HUD must contain no enclosing-player source references.
            Assert.IsEmpty(Hud().GetComponentsInChildren<HudBindings>(true));
        }

        [Test]
        public void MigratedPrefabs_HaveNoLegacyHudAndDirectorHasWiredRestartHandler()
        {
            Hud();
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            GameObject director = AssetDatabase.LoadAssetAtPath<GameObject>(DirectorPath);
#pragma warning disable CS0618
            Assert.IsEmpty(player.GetComponentsInChildren<RetroFPS.UI.AmmoHUD>(true));
            Assert.IsEmpty(director.GetComponentsInChildren<RetroFPS.UI.RunHUD>(true));
#pragma warning restore CS0618
            string ammoGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/UI/AmmoHUD.cs");
            string runGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/UI/RunHUD.cs");
            Assert.IsNotEmpty(ammoGuid); Assert.IsNotEmpty(runGuid);
            StringAssert.DoesNotContain(ammoGuid, File.ReadAllText(PlayerPath));
            StringAssert.DoesNotContain(runGuid, File.ReadAllText(DirectorPath));
            var handler = director.GetComponent<RunRestartHandler>(); Assert.IsNotNull(handler);
            Assert.AreEqual(director.GetComponent<RunDirector>(), Reference(handler, "director"));
        }

        [Test]
        public void StringTable_CoversAllCopyAndSharedFontsCoverTurkishGlyphs()
        {
            Hud();
            var table = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/_Project/Data/UI/StringTable_CombatHud_tr.asset");
            Assert.IsNotNull(table); table.RebuildCache();
            string[] keys = { "health.label", "dash.label", "parry.label", "parry.ready", "parry.active", "parry.cooldown", "parry.success", "hook.label", "hook.ready", "hook.cooldown", "ammo.empty", "weapon.melee", "ammo.bolts", "ammo.shells", "wave.counter", "wave.remaining", "araf.ready", "araf.used", "araf.warning", "araf.rooms", "araf.exitFound", "banner.arenaSealed", "banner.wave", "banner.waveCleared", "banner.arenaCleared", "truedeath.title", "truedeath.restart", "weapon.RepeaterArbalest", "weapon.ExecutionerBlade", "weapon.GothicShotgun" };
            foreach (string key in keys) Assert.IsTrue(table.ContainsKey("hud." + key), key);
            var theme = AssetDatabase.LoadAssetAtPath<HudTheme>("Assets/_Project/Data/UI/HudTheme_KanVeKemik.asset");
            Assert.IsNotNull(theme); Assert.IsNotNull(theme.MenuTheme);
            foreach (var font in new[] { theme.MenuTheme.UiFont, theme.MenuTheme.TitleFont })
                Assert.IsTrue(font.HasCharacters("ÇçĞğİıÖöŞşÜü%/+!—'[]0123456789", out uint[] missing, true, false), font.name);
        }

        /// <summary>Generated counters share one text flow and the weapon slot reserves separate horizontal space.</summary>
        [Test]
        public void Hud_CountersAndWeaponSlotCannotOverlapAdjacentText()
        {
            GameObject hud = Hud();
            var weapon = hud.GetComponentInChildren<WeaponAmmoHudView>(true);
            var name = (TMPro.TMP_Text)Reference(weapon, "weaponName");
            var slot = (TMPro.TMP_Text)Reference(weapon, "slotText");
            Assert.LessOrEqual(name.rectTransform.anchoredPosition.x + name.rectTransform.sizeDelta.x,
                slot.rectTransform.anchoredPosition.x - 8f);
            Assert.AreEqual(TMPro.TextOverflowModes.Ellipsis, name.overflowMode);
            Assert.GreaterOrEqual(name.fontSize, 20f);
            var encounter = hud.GetComponentInChildren<EncounterRunHudView>(true);
            foreach (string field in new[] { "waveText", "remainingText", "roomsText" })
            {
                var text = (TMPro.TMP_Text)Reference(encounter, field);
                Assert.AreEqual(0f, text.rectTransform.anchoredPosition.x, field);
                Assert.AreEqual(316f, text.rectTransform.sizeDelta.x, field);
                Assert.AreEqual(TMPro.TextOverflowModes.Ellipsis, text.overflowMode, field);
            }
            var dash = (TMPro.TMP_Text)Reference(hud.GetComponentInChildren<AbilityHudView>(true), "dashCurrent");
            Assert.AreEqual(128f, dash.rectTransform.sizeDelta.x);
            foreach (Transform parent in new[] { dash.transform.parent, name.transform.parent,
                ((TMPro.TMP_Text)Reference(weapon, "currentText")).transform.parent })
            {
                foreach (string obsolete in new[] { "Slash", "Maximum", "SlotOpen", "SlotClose" })
                {
                    Transform child = parent.Find(obsolete);
                    if (child != null) Assert.IsFalse(child.gameObject.activeSelf, obsolete);
                }
            }
        }
        [Test]
        public void MainMenu_ContainsNoCombatHud()
        {
            Hud();
            const string path = "Assets/Scenes/MainMenu.unity";
            if (!File.Exists(path)) Assert.Ignore("MainMenu has not been generated.");
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Assert.IsEmpty(root.GetComponentsInChildren<HudBindings>(true));
                    Assert.IsEmpty(root.GetComponentsInChildren<HealthHudView>(true));
                    Assert.IsEmpty(root.GetComponentsInChildren<TrueDeathView>(true));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
