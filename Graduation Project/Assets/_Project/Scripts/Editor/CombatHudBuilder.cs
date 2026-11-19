using System;
using System.Text;
using RetroFPS.Presentation;
using RetroFPS.Run;
using RetroFPS.Settings;
using RetroFPS.UI.HUD;
using RetroFPS.UI.Menu;
using RetroFPS.Weapons;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Builds the combat HUD and performs only targeted player/director prefab and SampleScene migration.</summary>
    public static class CombatHudBuilder
    {
        private const string Data = "Assets/_Project/Data/UI/";
        private const string HudPath = "Assets/_Project/Prefabs/UI/CombatHud/CombatHud.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        private const string DirectorPath = "Assets/_Project/Prefabs/Systems/RunDirector.prefab";
        private const string SamplePath = "Assets/Scenes/SampleScene.unity";
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        private static HudConfig config;
        private static HudTheme theme;
        private static StringTable strings;
        private static MenuTheme Palette => theme.MenuTheme;

        [MenuItem("RetroFPS/Build Combat HUD", false, 23)]
        private static void BuildMenu() => BuildAll();

        /// <summary>Returns 0 on success, -1 in play mode, -2 for dirty scenes, -3 for missing dependencies, -4 on failure.</summary>
        public static int BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Error(-1, "Exit Play Mode before building.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) return Error(-2, "Save all open scenes before building.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(DirectorPath) == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(SamplePath) == null)
                return Error(-3, "Player, RunDirector prefab or SampleScene is missing.");
            try
            {
                EnsureAssets();
                EditPrefab(PlayerPath, WirePlayerPrefab);
                EditPrefab(DirectorPath, WireDirectorPrefab);
                EnsureSampleDirector();
                AssetDatabase.SaveAssets();
                Debug.Log("[CombatHudBuilder] Combat HUD built; Player, RunDirector and SampleScene are wired. Final art/fonts and Play Mode profiling remain manual.");
                return 0;
            }
            catch (Exception exception) { Debug.LogException(exception); return -4; }
        }

        /// <summary>Idempotently wires a player created by QuickSetup, without rebuilding or saving its scene.</summary>
        public static void EnsurePlayerHud(GameObject player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            EnsureAssets(); WirePlayer(player);
        }

        private static int Error(int code, string message) { Debug.LogError("[CombatHudBuilder] " + message); return code; }

        private static void EnsureAssets()
        {
            var menu = AssetDatabase.LoadAssetAtPath<MenuTheme>(Data + "MenuTheme_KanVeKemik.asset");
            if (menu == null || menu.UiFont == null || menu.TitleFont == null ||
                MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSeal") == null || MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSealPressed") == null)
                throw new InvalidOperationException("Build Main Menu first: its shared theme, fonts and both seal sprites are required.");
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath) == null) throw new InvalidOperationException("Input actions are missing.");
            string[] actionNames = { "Point", "Navigate", "Submit", "Cancel", "Click", "RightClick", "ScrollWheel" };
            for (int i = 0; i < actionNames.Length; i++)
                if (ActionReference(actionNames[i]) == null) throw new InvalidOperationException("Missing importer-owned UI reference: " + actionNames[i]);
            EnsureFolder(Data.TrimEnd('/')); EnsureFolder("Assets/_Project/Prefabs/UI/CombatHud");
            config = Asset<HudConfig>(Data + "HudConfig_Default.asset");
            theme = Asset<HudTheme>(Data + "HudTheme_KanVeKemik.asset");
            strings = Asset<StringTable>(Data + "StringTable_CombatHud_tr.asset");
            PopulateStrings();
            EnsureLocalizationCatalog();
            HudPlaceholderArt.Generate();
            Set(theme, "menuTheme", menu);
            string[] fields = { "plate", "chip", "barTrack", "barFill", "cross", "diamond", "counterVerdict", "hook", "weaponBolt", "weaponShell", "weaponBlade", "sigilAraf", "arrow", "hatch" };
            string[] sprites = { "HUD_Plate_9S", "HUD_Chip_9S", "HUD_Bar_Track", "HUD_Bar_Fill", "HUD_Cross", "HUD_Diamond", "HUD_CounterVerdict", "HUD_Hook", "HUD_Weapon_Bolt", "HUD_Weapon_Shell", "HUD_Weapon_Blade", "HUD_Sigil_Araf", "HUD_Arrow", "HUD_Hatch" };
            for (int i = 0; i < fields.Length; i++) Set(theme, fields[i], HudPlaceholderArt.Sprite(sprites[i]));
            Set(theme, "deathSeal", MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSeal"));
            Set(theme, "deathSealPressed", MainMenuPlaceholderArt.Sprite("Sigil_A_BoneSealPressed"));
            ValidateGlyphs(menu);
            // New child GameObjects originate in an owned scratch scene even while editing prefab
            // contents. Their construction cannot dirty the user's active scene. Additive creation
            // also preserves loaded menu fonts/themes (there is no NewScene(Single) asset unload).
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) != null) EditPrefab(HudPath, BuildHud);
                else
                {
                    GameObject root = new GameObject("CombatHud");
                    try { BuildHud(root); Save(root, HudPath); }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scratch, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static void PopulateStrings()
        {
            StringTable.Translation[] defaults =
            {
                new StringTable.Translation("hud.card.title", "B\u0130R BEDEL SE\u00c7", "CHOOSE YOUR PRICE"),
                new StringTable.Translation("hud.card.boon.label", "L\u00dcTUF", "BOON"),
                new StringTable.Translation("hud.card.curse.label", "LANET", "CURSE"),
                new StringTable.Translation("hud.card.refuse", "REDDET", "REFUSE"),
                new StringTable.Translation("hud.card.hint", "SE\u00c7: A/D   ONAYLA: ENTER", "SELECT: A/D   CONFIRM: ENTER"),
                new StringTable.Translation("hud.card.acquired.banner", "BEDEL \u00d6DEND\u0130: {0}", "PRICE PAID: {0}"),
                new StringTable.Translation("hud.card.strip.title", "KARTLAR", "CARDS"),
                new StringTable.Translation("hud.card.rarity.common", "SIRADAN", "COMMON"),
                new StringTable.Translation("hud.card.rarity.rare", "NAD\u0130R", "RARE"),
                new StringTable.Translation("hud.card.rarity.relic", "KAD\u0130M", "RELIC"),
                new StringTable.Translation("hud.card.blood_debt.name", "Kan Borcu", "Blood Debt"),
                new StringTable.Translation("hud.card.blood_debt.boon", "T\u00fcm hasar +%30", "All damage +30%"),
                new StringTable.Translation("hud.card.blood_debt.curse", "Maksimum can -%25", "Max health -25%"),
                new StringTable.Translation("hud.card.blood_debt.flavor", "Kule borcunu etinden tahsil eder.", "The tower collects its due in flesh."),
                new StringTable.Translation("hud.card.starving_steel.name", "A\u00e7 \u00c7elik", "Starving Steel"),
                new StringTable.Translation("hud.card.starving_steel.boon", "B\u0131\u00e7ak hasar\u0131 +%45, Kan Hasad\u0131 %75 fazla iyile\u015ftirir", "Blade damage +45%, Blood Harvest heals +75%"),
                new StringTable.Translation("hud.card.starving_steel.curse", "D\u00fc\u015fmanlar can k\u00fcresi d\u00fc\u015f\u00fcrmez", "Enemies drop no health orbs"),
                new StringTable.Translation("hud.card.starving_steel.flavor", "\u00c7elik doymad\u0131k\u00e7a sen de doymazs\u0131n.", "While the steel hungers, so do you."),
                new StringTable.Translation("hud.card.last_breath.name", "Son Nefes", "Last Breath"),
                new StringTable.Translation("hud.card.last_breath.boon", "Can yar\u0131n\u0131n alt\u0131ndayken hasar +%50, h\u0131z +%20", "Below half health: damage +50%, speed +20%"),
                new StringTable.Translation("hud.card.last_breath.curse", "T\u00fcm iyile\u015fmeler -%35", "All healing -35%"),
                new StringTable.Translation("hud.card.last_breath.flavor", "\u00d6l\u00fcm yakla\u015ft\u0131k\u00e7a elin a\u011f\u0131rla\u015f\u0131r.", "The nearer death comes, the heavier your hand."),
                new StringTable.Translation("hud.card.bone_wings.name", "Kemik Kanatlar", "Bone Wings"),
                new StringTable.Translation("hud.card.bone_wings.boon", "Dash %25 h\u0131zl\u0131 dolar, h\u0131z +%12", "Dash recharges 25% faster, speed +12%"),
                new StringTable.Translation("hud.card.bone_wings.curse", "Al\u0131nan hasar +%25", "Damage taken +25%"),
                new StringTable.Translation("hud.card.bone_wings.flavor", "Hafif kemik \u00e7abuk k\u0131r\u0131l\u0131r.", "Light bones break fast."),
                new StringTable.Translation("hud.card.brimming_chalice.name", "Dolu Kadeh", "Brimming Chalice"),
                new StringTable.Translation("hud.card.brimming_chalice.boon", "Fazla can s\u0131n\u0131r\u0131 +50; fazla can varken at\u0131\u015f h\u0131z\u0131 +%35", "Overheal cap +50; while overhealed, fire rate +35%"),
                new StringTable.Translation("hud.card.brimming_chalice.curse", "Can yar\u0131n\u0131n alt\u0131ndayken h\u0131z -%15", "Below half health: speed -15%"),
                new StringTable.Translation("hud.card.brimming_chalice.flavor", "Kadeh doluyken herkes cesurdur.", "Everyone is brave while the cup is full."),
                new StringTable.Translation("hud.card.glutton_barrel.name", "Obur Namlu", "Glutton's Barrel"),
                new StringTable.Translation("hud.card.glutton_barrel.boon", "At\u0131\u015f h\u0131z\u0131 +%40", "Fire rate +40%"),
                new StringTable.Translation("hud.card.glutton_barrel.curse", "Toplanan cephane -%50", "Ammo pickups -50%"),
                new StringTable.Translation("hud.card.glutton_barrel.flavor", "\u00c7abuk yer, a\u00e7 kal\u0131r.", "It eats fast and starves faster."),
                new StringTable.Translation("hud.card.iron_hide.name", "Demir Post", "Iron Hide"),
                new StringTable.Translation("hud.card.iron_hide.boon", "Al\u0131nan hasar -%20", "Damage taken -20%"),
                new StringTable.Translation("hud.card.iron_hide.curse", "H\u0131z -%10", "Speed -10%"),
                new StringTable.Translation("hud.card.iron_hide.flavor", "Z\u0131rh etine kaynar, ad\u0131m\u0131n a\u011f\u0131rla\u015f\u0131r.", "Armor fuses to flesh, and every step grows heavy."),
                new StringTable.Translation("hud.card.carrion_crow.name", "Le\u015f Kargas\u0131", "Carrion Crow"),
                new StringTable.Translation("hud.card.carrion_crow.boon", "Toplanan cephane +%75", "Ammo pickups +75%"),
                new StringTable.Translation("hud.card.carrion_crow.curse", "Can k\u00fcresi \u015fans\u0131 -%25", "Health orb chance -25%"),
                new StringTable.Translation("hud.card.carrion_crow.flavor", "Karga diriyi de\u011fil, geride kalan\u0131 besler.", "The crow feeds not the living, but what is left behind."),
                new StringTable.Translation("hud.card.pilgrims_flask.name", "Hac\u0131 Mataras\u0131", "Pilgrim's Flask"),
                new StringTable.Translation("hud.card.pilgrims_flask.boon", "\u0130yile\u015fmeler +%35, can k\u00fcresi \u015fans\u0131 +%50", "Healing +35%, health orb chance +50%"),
                new StringTable.Translation("hud.card.pilgrims_flask.curse", "At\u0131\u015f h\u0131z\u0131 -%12", "Fire rate -12%"),
                new StringTable.Translation("hud.card.pilgrims_flask.flavor", "Her yudum can verir, her dua eli yava\u015flat\u0131r.", "Every sip mends the flesh, every prayer slows the hand."),
                new StringTable.Translation("hud.card.gilded_vein.name", "Yald\u0131zl\u0131 Damar", "Gilded Vein"),
                new StringTable.Translation("hud.card.gilded_vein.boon", "Fazla can s\u0131n\u0131r\u0131 +50; fazla can varken al\u0131nan hasar -%25", "Overheal cap +50; while overhealed, damage taken -25%"),
                new StringTable.Translation("hud.card.gilded_vein.curse", "Maksimum can -%20", "Max health -20%"),
                new StringTable.Translation("hud.card.gilded_vein.flavor", "Alt\u0131n damarlar\u0131 korur ama kalbi daralt\u0131r.", "Gold shields the veins, yet narrows the heart."),
                new StringTable.Translation("hud.card.executioners_oath.name", "Cellat Yemini", "Executioner's Oath"),
                new StringTable.Translation("hud.card.executioners_oath.boon", "B\u0131\u00e7ak hasar\u0131 +%75, dash %25 h\u0131zl\u0131 dolar", "Blade damage +75%, dash recharges 25% faster"),
                new StringTable.Translation("hud.card.executioners_oath.curse", "T\u00fcm hasar -%20", "All damage -20%"),
                new StringTable.Translation("hud.card.executioners_oath.flavor", "Yemin yaln\u0131zca yak\u0131ndan vurana sad\u0131kt\u0131r.", "The oath keeps faith only with those who strike up close."),
                new StringTable.Translation("hud.card.bell_tower.name", "\u00c7an Kulesi", "Bell Tower"),
                new StringTable.Translation("hud.card.bell_tower.boon", "At\u0131\u015f h\u0131z\u0131 +%50, al\u0131nan hasar -%20", "Fire rate +50%, damage taken -20%"),
                new StringTable.Translation("hud.card.bell_tower.curse", "H\u0131z -%15, dash %20 yava\u015f dolar", "Speed -15%, dash recharges 20% slower"),
                new StringTable.Translation("hud.card.bell_tower.flavor", "Tun\u00e7 \u00e7an\u0131n alt\u0131nda duran e\u011filmez ama k\u0131p\u0131rdayamaz.", "Who stands beneath the bronze bell never bends, yet barely moves."),
                new StringTable.Translation("hud.card.judges_scale.name", "Yarg\u0131\u00e7 Terazisi", "Judge's Scale"),
                new StringTable.Translation("hud.card.judges_scale.boon", "T\u00fcm hasar +%60", "All damage +60%"),
                new StringTable.Translation("hud.card.judges_scale.curse", "Al\u0131nan hasar +%40", "Damage taken +40%"),
                new StringTable.Translation("hud.card.judges_scale.flavor", "Terazi iki kefeyi de kanla tartar.", "The scale weighs both pans in blood."),
                new StringTable.Translation("hud.card.hollow_saint.name", "Oyuk Aziz", "Hollow Saint"),
                new StringTable.Translation("hud.card.hollow_saint.boon", "Dash %60 h\u0131zl\u0131 dolar, h\u0131z +%25", "Dash recharges 60% faster, speed +25%"),
                new StringTable.Translation("hud.card.hollow_saint.curse", "Maksimum can -%25", "Max health -25%"),
                new StringTable.Translation("hud.card.hollow_saint.flavor", "Kemiklerinden bo\u015falan her gram r\u00fczg\u00e2ra d\u00f6n\u00fc\u015f\u00fcr.", "Every ounce hollowed from the bone turns to wind."),
                new StringTable.Translation("hud.card.red_hour.name", "K\u0131z\u0131l Saat", "Red Hour"),
                new StringTable.Translation("hud.card.red_hour.boon", "At\u0131\u015f h\u0131z\u0131 +%50, b\u0131\u00e7ak hasar\u0131 +%50, h\u0131z +%15", "Fire rate +50%, blade damage +50%, speed +15%"),
                new StringTable.Translation("hud.card.red_hour.curse", "\u0130yile\u015fmeler -%25, can k\u00fcresi \u015fans\u0131 -%50", "Healing -25%, health orb chance -50%"),
                new StringTable.Translation("hud.card.red_hour.flavor", "K\u0131z\u0131l saatte merhamet kurur, yaln\u0131zca k\u0131y\u0131m kal\u0131r.", "In the red hour mercy dries up, leaving only slaughter."),
                new StringTable.Translation("boss.judge.name", "YARGI\u00C7", "THE JUDGE"),
                new StringTable.Translation("hud.health.label", "CAN", "HEALTH"),
                new StringTable.Translation("hud.dash.label", "ATILMA", "DASH"),
                new StringTable.Translation("hud.parry.label", "HÜKÜM", "PARRY"),
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
                new StringTable.Translation("transition.araf.escape", "KAÇIŞ", "ESCAPE")
            };
            if (strings.UpsertEntries(defaults)) EditorUtility.SetDirty(strings);
            ApplyEnglishCopyV1();
            ApplyTurkishCopyV2();
            ApplyCounterVerdictCopy();
            strings.ValidateEntries();
            for (int i = 0; i < defaults.Length; i++)
            {
                string key = defaults[i].Key;
                string tr = strings.Get(key, LanguageSetting.Turkish), en = strings.Get(key, LanguageSetting.English);
                if (tr.Length > 200 || en.Length > 200)
                    throw new InvalidOperationException("HUD text exceeds fixed capacity: " + key);
                // Formatting outside gameplay catches malformed or mismatched authored placeholders.
                string.Format(System.Globalization.CultureInfo.InvariantCulture, tr, "999", "999");
                string.Format(System.Globalization.CultureInfo.InvariantCulture, en, "999", "999");
                if (PlaceholderSignature(tr) != PlaceholderSignature(en) ||
                    PlaceholderSignature(tr) != PlaceholderSignature(defaults[i].Turkish))
                    throw new InvalidOperationException("HUD placeholder mismatch: " + key);
            }
        }

        // P3 copy revision v1: migrate only the original oversized English weapon name.
        // Other authored cells (including a custom replacement for this name) remain untouched.
        private static void ApplyEnglishCopyV1()
        {
            var serialized = new SerializedObject(strings);
            SerializedProperty entries = serialized.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("key").stringValue != "hud.weapon.ExecutionerBlade") continue;
                SerializedProperty english = entry.FindPropertyRelative("en");
                if (english.stringValue != "EXECUTIONER BLADE") return;
                english.stringValue = "EXECUTIONER";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                strings.RebuildCache();
                EditorUtility.SetDirty(strings);
                return;
            }
        }

        // Retain the approved short success state; the ability label is migrated separately below.
        private static void ApplyTurkishCopyV2()
        {
            var serialized = new SerializedObject(strings);
            SerializedProperty entries = serialized.FindProperty("entries");
            bool changed = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("key").stringValue;
                SerializedProperty turkish = entry.FindPropertyRelative("value");
                // "SAVUŞTURULDU" overflowed the parry state cell.
                if (key == "hud.parry.success" && turkish.stringValue == "SAVUŞTURULDU") { turkish.stringValue = "PARRY!"; changed = true; }
            }
            if (!changed) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            strings.RebuildCache();
            EditorUtility.SetDirty(strings);
        }

        // The ability identity changed. Replace both label translations even in an already populated table.
        // Keep the stable localization key so existing prefab bindings survive until the next rebuild.
        private static void ApplyCounterVerdictCopy()
        {
            var serialized = new SerializedObject(strings);
            SerializedProperty entries = serialized.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("key").stringValue != "hud.parry.label") continue;
                SerializedProperty turkish = entry.FindPropertyRelative("value");
                SerializedProperty english = entry.FindPropertyRelative("en");
                if (turkish.stringValue == "HÜKÜM" && english.stringValue == "PARRY") return;
                turkish.stringValue = "HÜKÜM";
                english.stringValue = "PARRY";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                strings.RebuildCache();
                EditorUtility.SetDirty(strings);
                return;
            }
        }

        private static string PlaceholderSignature(string text)
        {
            var tokens = new System.Collections.Generic.List<string>();
            foreach (System.Text.RegularExpressions.Match token in System.Text.RegularExpressions.Regex.Matches(text, @"\{[^{}]+\}"))
                tokens.Add(token.Value);
            tokens.Sort(StringComparer.Ordinal);
            return string.Join("|", tokens);
        }

        private static void EnsureLocalizationCatalog()
        {
            EnsureFolder("Assets/_Project/Resources/Localization");
            var catalog = Asset<LocalizationCatalog>("Assets/_Project/Resources/Localization/LocalizationCatalog.asset");
            var serialized = new SerializedObject(catalog);
            SerializedProperty reference = serialized.FindProperty("combatTable");
            if (reference.objectReferenceValue == strings) return;
            reference.objectReferenceValue = strings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void ValidateGlyphs(MenuTheme menu)
        {
            var all = new StringBuilder("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzÇçĞğİıÖöŞşÜü%/+!—'[]0123456789");
            var so = new SerializedObject(strings); SerializedProperty entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                all.Append(entries.GetArrayElementAtIndex(i).FindPropertyRelative("value").stringValue);
                all.Append(entries.GetArrayElementAtIndex(i).FindPropertyRelative("en").stringValue);
            }
            string characters = all.ToString();
            if (menu.FallbackFont != null && menu.FallbackFont.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                int glyphCount = menu.FallbackFont.glyphTable.Count;
                menu.FallbackFont.TryAddCharacters(characters, out string missing);
                if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[CombatHudBuilder] Missing fallback glyphs: " + missing);
                if (menu.FallbackFont.glyphTable.Count != glyphCount) EditorUtility.SetDirty(menu.FallbackFont);
            }
            foreach (TMP_FontAsset font in new[] { menu.UiFont, menu.TitleFont })
                if (!font.HasCharacters(characters, out uint[] missing, true, true))
                    throw new InvalidOperationException("Shared HUD font lacks required characters; missing count: " + missing.Length);
            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacyFont.RequestCharactersInTexture(characters, 52, FontStyle.Normal);
            for (int i = 0; i < characters.Length; i++)
                if (!char.IsWhiteSpace(characters[i]) && !legacyFont.HasCharacter(characters[i]))
                    throw new InvalidOperationException("Transition/popup font lacks required character: " + characters[i]);
        }

        private static void BuildHud(GameObject root)
        {
            RectTransform canvasRect = Rect("Canvas", root.transform); Stretch(canvasRect);
            Canvas canvas = Component<Canvas>(canvasRect.gameObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; canvas.pixelPerfect = true;
            var scaler = Component<UnityEngine.UI.CanvasScaler>(canvasRect.gameObject);
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
            Component<UnityEngine.UI.GraphicRaycaster>(canvasRect.gameObject);
            RectTransform hudRoot = Rect("HudRoot", canvasRect); Stretch(hudRoot);
            CanvasGroup gameplay = Component<CanvasGroup>(hudRoot.gameObject); gameplay.alpha = 1f;
            gameplay.interactable = false; gameplay.blocksRaycasts = false;
            RectTransform safe = Rect("SafeArea", hudRoot); Stretch(safe);
            RectTransform bl = Corner("BottomLeft", safe, Vector2.zero, theme.HealthPlateSize);
            RectTransform br = Corner("BottomRight", safe, Vector2.right, theme.AmmoPlateSize);
            RectTransform tr = Corner("TopRight", safe, Vector2.one, theme.EncounterZoneSize);
            RectTransform announcements = Rect("Announcements", safe);
            announcements.anchorMin = announcements.anchorMax = announcements.pivot = new Vector2(0.5f, 1f);
            announcements.anchoredPosition = new Vector2(0f, -76f); announcements.sizeDelta = theme.MergedBannerSize;
            BuildHealth(bl); BuildAbilities(bl); BuildAmmo(br); BuildEncounter(tr); BuildBanner(announcements); BuildBoss(safe);
            BuildActiveCards(tr, safe);
            HudEventSystemGuard guard = BuildEventSystem(root);
            RectTransform offers = Rect("CardOfferLayer", canvasRect); Stretch(offers);
            BuildCardOffer(offers, canvasRect);
            RectTransform death = Rect("DeathLayer", canvasRect); Stretch(death);
            RectTransform deathContent = BuildDeath(death, canvasRect, gameplay, guard);
            HudLayout layout = Component<HudLayout>(root);
            Set(layout, "config", config); Set(layout, "canvasRect", canvasRect); Set(layout, "hudRoot", hudRoot);
            Set(layout, "safeArea", safe); Set(layout, "deathContent", deathContent);
        }

        private static void BuildHealth(RectTransform parent)
        {
            RectTransform plate = Plate("HealthPlate", parent, theme.HealthPlateSize);
            var view = Component<HealthHudView>(plate.gameObject); Style(view);
            var low = Image("LowBorder", plate, theme.Chip, Palette.Danger, 0, 0, 400, 150);
            Stretch(low.rectTransform); low.type = UnityEngine.UI.Image.Type.Sliced; low.fillCenter = false;
            low.pixelsPerUnitMultiplier = 1f / theme.LowHealthBorderWidth; low.enabled = false;
            Image("Cross", plate, theme.Cross, Palette.Text, 12, 14, 27, 27);
            TMP_Text number = Label("Current", plate, null, theme.HealthNumberSize, 96, 3, 156, 52);
            // Ellipsis hides the whole line when the large number's line height exceeds the rect,
            // which left the health value blank. Digits are bounded, so let it overflow instead.
            number.overflowMode = TextOverflowModes.Overflow;
            RetireLabels(plate, "Maximum", "Slash");
            RectTransform overflow = At("Overflow", plate, 258, 11, 106, 36);
            RetireLabels(overflow, "Plus");
            TMP_Text extra = Label("Amount", overflow, null, theme.OverhealNumberSize, 0, 0, 106, 36); extra.color = Palette.Focus;
            overflow.gameObject.SetActive(false);
            RectTransform warning = Chip("Warning", plate, 49, 10, 36, 36, "hud.symbol.warning", 28); warning.gameObject.SetActive(false);
            UnityEngine.UI.Image fill = Bar("HealthBar", plate, 12, 56, theme.HealthBarSize.x, theme.HealthBarSize.y, Palette.Accent);
            var tick = Image("LeadingTick", fill.transform.parent, null, Palette.Text, 0, 0, 3, theme.HealthBarSize.y);
            tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            tick.rectTransform.pivot = new Vector2(1f, 0.5f); tick.rectTransform.anchoredPosition = Vector2.zero;
            var edge = Image("DamageEdge", fill.transform.parent, null, Palette.Focus, 0, 0, 0, 0); Stretch(edge.rectTransform); edge.enabled = false;
            Set(view, "currentText", number); Set(view, "overflowText", extra);
            Set(view, "overflowGroup", overflow.gameObject); Set(view, "warningChip", warning.gameObject);
            Set(view, "fill", fill); Set(view, "leadingTick", tick); Set(view, "lowBorder", low); Set(view, "damageEdge", edge);
        }

        private static void BuildActiveCards(RectTransform zone, RectTransform safe)
        {
            // Encounter/Araf plate reaches 150px; leave 18px before the compact card list.
            RectTransform container = At("ActiveCards", zone, 0, 168, 340, 180);
            container.anchorMin = container.anchorMax = container.pivot = Vector2.one;
            container.anchoredPosition = new Vector2(0, -168);
            RectTransform plate = Plate("List", container, new Vector2(340, 180));
            var view = Component<ActiveCardsHudView>(container.gameObject); Set(view, "strings", strings); Set(view, "list", plate.gameObject);
            Set(view, "plate", plate);
            // Plate height is driven at runtime from the visible row count (8 pad + 32 header + 28 per row + 8 pad).
            TMP_Text header = Label("Header", plate, "hud.card.strip.title", 20, 12, 8, 316, 28);
            header.color = Palette.Focus; header.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            var serialized = new SerializedObject(view);
            SerializedProperty rows = serialized.FindProperty("rows"); rows.arraySize = 6;
            SerializedProperty markers = serialized.FindProperty("markers"); markers.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                float y = 40 + i * 28;
                var marker = Image("Marker" + i, plate, null, Palette.Text, 12, y + 4, 4, 20);
                TMP_Text row = Label("Card" + i, plate, null, 20, 24, y, 304, 28);
                row.fontStyle = FontStyles.Bold | FontStyles.SmallCaps; row.color = Palette.Text;
                rows.GetArrayElementAtIndex(i).objectReferenceValue = row;
                markers.GetArrayElementAtIndex(i).objectReferenceValue = marker;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            plate.gameObject.SetActive(false);
            RectTransform bannerRoot = Rect("CardAnnouncement", safe);
            bannerRoot.anchorMin = bannerRoot.anchorMax = bannerRoot.pivot = new Vector2(0.5f, 1f);
            // Separate from the encounter queue's reserved two-line announcement area.
            bannerRoot.anchoredPosition = new Vector2(0, -240); bannerRoot.sizeDelta = new Vector2(880, 72);
            RectTransform bannerPlate = Plate("Plate", bannerRoot, new Vector2(880, 72));
            TMP_Text message = Label("Message", bannerPlate, null, 24, 18, 12, 844, 48);
            message.alignment = TextAlignmentOptions.Center;
            var group = Component<CanvasGroup>(bannerRoot.gameObject); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var banner = Component<CardAcquiredBannerView>(bannerRoot.gameObject);
            Set(banner, "config", config); Set(banner, "strings", strings); Set(banner, "group", group); Set(banner, "label", message);
            Set(view, "banner", banner);
            // No own Canvas: the banner was verified visible with the shared HUD canvas (the "invisible" captures were stale frames).
            if (bannerRoot.TryGetComponent(out Canvas staleBannerCanvas)) UnityEngine.Object.DestroyImmediate(staleBannerCanvas);
            bannerRoot.SetAsLastSibling();
        }

        private static void BuildCardOffer(RectTransform layer, RectTransform canvas)
        {
            var backdrop = Image("Backdrop", layer, null, theme.DeathOverlay, 0, 0, 0, 0);
            Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            RectTransform content = Rect("Content", layer);
            content.anchorMin = content.anchorMax = content.pivot = Vector2.one * 0.5f;
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(1160, 700);
            TMP_Text title = Label("Title", content, "hud.card.title", 42, 0, 0, 1160, 70);
            title.font = Palette.TitleFont; title.alignment = TextAlignmentOptions.Center;
            CardOfferChoiceView first = BuildOfferChoice("First", content, 20);
            CardOfferChoiceView second = BuildOfferChoice("Second", content, 600);
            RectTransform refuseRect = Plate("Refuse", content, new Vector2(360, 72));
            refuseRect.anchoredPosition = new Vector2(400, -556);
            refuseRect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            TMP_Text refuseLabel = Label("Label", refuseRect, "hud.card.refuse", 28, 12, 8, 336, 56);
            refuseLabel.alignment = TextAlignmentOptions.Center;
            var refuse = Component<CardOfferChoiceView>(refuseRect.gameObject);
            Set(refuse, "nameText", refuseLabel); Set(refuse, "frame", ChoiceFrame(refuseRect, 360, 72));
            WireChoiceButton(refuse, refuseRect);
            TMP_Text hint = Label("Hint", content, "hud.card.hint", 22, 0, 650, 1160, 40);
            hint.alignment = TextAlignmentOptions.Center;
            var view = Component<CardOfferView>(canvas.gameObject);
            Set(view, "strings", strings); Set(view, "offerLayer", layer.gameObject);
            Set(view, "first", first); Set(view, "second", second); Set(view, "refuse", refuse);
            layer.SetAsLastSibling(); layer.gameObject.SetActive(false);
        }

        private static CardOfferChoiceView BuildOfferChoice(string name, Transform content, float x)
        {
            RectTransform card = Plate(name, content, new Vector2(540, 440));
            card.anchoredPosition = new Vector2(x, -90);
            card.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            TMP_Text nameText = Label("Name", card, null, 32, 24, 18, 332, 54);
            TMP_Text rarity = Label("Rarity", card, null, 20, 360, 20, 156, 28);
            rarity.alignment = TextAlignmentOptions.MidlineRight;
            rarity.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            Label("BoonLabel", card, "hud.card.boon.label", 22, 24, 90, 492, 34).color = new Color(1f, 0.70f, 0.15f);
            TMP_Text boon = Label("Boon", card, null, 24, 24, 126, 492, 92);
            boon.color = new Color(1f, 0.70f, 0.15f); boon.textWrappingMode = TextWrappingModes.Normal;
            Label("CurseLabel", card, "hud.card.curse.label", 22, 24, 228, 492, 34).color = Palette.Danger;
            TMP_Text curse = Label("Curse", card, null, 24, 24, 264, 492, 68);
            curse.color = Palette.Danger; curse.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text flavor = Label("Flavor", card, null, 20, 24, 350, 492, 72);
            flavor.fontStyle = FontStyles.Italic; flavor.color = new Color(160f / 255f, 150f / 255f, 132f / 255f);
            flavor.textWrappingMode = TextWrappingModes.Normal;
            var rarityBorder = Image("RarityBorder", card, theme.Chip, Palette.Text, 0, 0, 540, 440);
            Stretch(rarityBorder.rectTransform); rarityBorder.type = UnityEngine.UI.Image.Type.Sliced;
            rarityBorder.fillCenter = false; rarityBorder.pixelsPerUnitMultiplier = 1f / 2f; rarityBorder.enabled = true;
            var view = Component<CardOfferChoiceView>(card.gameObject);
            Set(view, "nameText", nameText); Set(view, "boonText", boon); Set(view, "curseText", curse); Set(view, "flavorText", flavor);
            Set(view, "rarityText", rarity); Set(view, "rarityBorder", rarityBorder);
            Set(view, "frame", ChoiceFrame(card, 540, 440));
            WireChoiceButton(view, card);
            return view;
        }

        private static void WireChoiceButton(CardOfferChoiceView view, RectTransform rect)
        {
            var button = Component<CardOfferButton>(rect.gameObject);
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Set(view, "button", button);
        }

        private static UnityEngine.UI.Image ChoiceFrame(RectTransform rect, float width, float height)
        {
            var frame = Image("SelectionFrame", rect, theme.Chip, Palette.Focus, 0, 0, width, height);
            Stretch(frame.rectTransform); frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.fillCenter = false; frame.pixelsPerUnitMultiplier = 1f / 3f; frame.enabled = false;
            return frame;
        }

        private static void BuildAbilities(RectTransform parent)
        {
            RectTransform row = At("Abilities", parent, 12, 78, 376, 60);
            var view = Component<AbilityHudView>(row.gameObject); Style(view); Set(view, "strings", strings);
            RectTransform dash = At("Dash", row, 0, 0, 128, 60);
            var pip = Image("PipTemplate", dash, theme.Diamond, Palette.Text, 0, 0, 27, 27);
            Image("Inner", pip.transform, theme.Diamond, theme.Track, 6, 6, 15, 15);
            var pipFill = Image("Fill", pip.transform, theme.Diamond, Palette.Text, 0, 0, 27, 27); Radial(pipFill);
            pip.gameObject.SetActive(false);
            RetireLabels(dash, "Label", "Slash", "Maximum");
            TMP_Text count = Label("Current", dash, null, 20, 0, 31, 128, 27);
            count.characterSpacing = 0f;
            RectTransform parry = At("Parry", row, 136, 0, 128, 60);
            CanvasGroup parryGroup = Component<CanvasGroup>(parry.gameObject);
            // Retain the Parry hierarchy and serialized bindings; this cell now presents Counter-Verdict.
            var parryFill = AbilityGlyph(parry, theme.CounterVerdict, 18f);
            // KARŞI HÜKÜM exceeds this single-line cell; HÜKÜM keeps the 20px text floor.
            Label("Label", parry, "hud.parry.label", 20, 22, 0, 106, 27).characterSpacing = 0f;

            var activeChip = Image("ActiveChip", parry, theme.Chip, Palette.Focus, 0, 30, 128, 28);
            activeChip.type = UnityEngine.UI.Image.Type.Sliced; activeChip.enabled = false;
            TMP_Text parryState = Label("State", parry, null, 20, 0, 30, 128, 28);
            RectTransform hook = At("Hook", row, 270, 0, 106, 60);
            CanvasGroup hookGroup = Component<CanvasGroup>(hook.gameObject);
            var hookFill = AbilityGlyph(hook, theme.Hook);
            TMP_Text hookLabel = Label("Label", hook, "hud.hook.label", 20, 30, 0, 76, 27);
            // "KANCA" is slightly wider than the 106px cell allows after the glyph; the plate has a 12px margin to absorb it.
            hookLabel.characterSpacing = 0f; hookLabel.overflowMode = TextOverflowModes.Overflow;
            TMP_Text hookState = Label("State", hook, null, 20, 0, 30, 106, 28);
            parryState.characterSpacing = hookState.characterSpacing = 0f;
            Set(view, "pipRoot", dash); Set(view, "pipTemplate", pip); Set(view, "dashCurrent", count);
            Set(view, "parryCell", parryGroup); Set(view, "hookCell", hookGroup); Set(view, "parryState", parryState); Set(view, "hookState", hookState);
            Set(view, "parryFill", parryFill); Set(view, "hookFill", hookFill); Set(view, "activeChip", activeChip);
        }

        private static UnityEngine.UI.Image AbilityGlyph(Transform parent, Sprite sprite, float size = 27f)
        {
            var outline = Image("GlyphOutline", parent, sprite, Palette.Text, 0, 0, size, size);
            Image("Inner", outline.transform, sprite, theme.Track, size * 2f / 9f, size * 2f / 9f, size * 5f / 9f, size * 5f / 9f);
            var fill = Image("Fill", outline.transform, sprite, Palette.Text, 0, 0, size, size); Radial(fill); return fill;
        }

        private static void BuildAmmo(RectTransform parent)
        {
            RectTransform plate = Plate("WeaponPlate", parent, theme.AmmoPlateSize);
            var view = Component<WeaponAmmoHudView>(plate.gameObject); Style(view); Set(view, "strings", strings);
            var glyphOutline = Image("WeaponOutline", plate, theme.WeaponBolt, Palette.Text, 9, 9, 42, 33);
            var glyph = Image("WeaponGlyph", plate, theme.WeaponBolt, theme.TypeBolts, 12, 12, 36, 27);
            TMP_Text name = Label("WeaponName", plate, null, 20, 56, 12, 206, 27);
            name.characterSpacing = 0f;
            RetireLabels(plate, "SlotOpen", "SlotClose");
            TMP_Text slot = Label("Slot", plate, null, 20, 270, 12, 38, 27);
            slot.characterSpacing = 0f; slot.alignment = TextAlignmentOptions.MidlineRight;
            RectTransform ammo = At("Ammo", plate, 0, 43, 320, 65);
            TMP_Text current = Label("Current", ammo, null, 48, 12, 0, 216, 54);
            current.overflowMode = TextOverflowModes.Overflow; // bounded digits; Ellipsis would blank the 48px line
            RetireLabels(ammo, "Slash", "Maximum");
            var fill = Bar("AmmoBar", ammo, 12, 53, theme.AmmoBarSize.x, theme.AmmoBarSize.y, theme.TypeBolts);
            // 88px fits "EMPTY"/"BOŞ" at 20px; ends at 308 inside the 320px plate.
            // WeaponAmmoHudView owns this text (BOŞ/EMPTY); a LocalizedText on it overwrote the word with "!"
            // on every language change, so the chip gets no key and any previously generated one is removed.
            RectTransform warning = Chip("Warning", ammo, 220, 12, 88, 36, null, 20);
            LocalizedText staleLocalized = warning.Find("Text").GetComponent<LocalizedText>();
            if (staleLocalized != null) Object.DestroyImmediate(staleLocalized, true);
            var highlight = Image("Highlight", warning, theme.Chip, Palette.Text, 0, 0, 88, 36);
            Stretch(highlight.rectTransform); highlight.type = UnityEngine.UI.Image.Type.Sliced; highlight.fillCenter = false; highlight.pixelsPerUnitMultiplier = 1f / 3f;
            TMP_Text warningText = warning.Find("Text").GetComponent<TMP_Text>(); warningText.transform.SetAsLastSibling();
            TMP_Text melee = Label("Melee", plate, "hud.weapon.melee", 20, 12, 56, 296, 35);
            warning.gameObject.SetActive(false); melee.gameObject.SetActive(false);
            Set(view, "weaponName", name); Set(view, "slotText", slot); Set(view, "currentText", current);
            Set(view, "warningText", warningText); Set(view, "meleeText", melee); Set(view, "ammoGroup", ammo.gameObject);
            Set(view, "warningChip", warning.gameObject); Set(view, "glyph", glyph); Set(view, "glyphOutline", glyphOutline); Set(view, "fill", fill); Set(view, "warningHighlight", highlight);
        }

        private static void BuildEncounter(RectTransform zone)
        {
            RectTransform plate = Plate("EncounterPlate", zone, new Vector2(theme.EncounterPlateWidth, theme.ExplorationPlateHeight));
            plate.anchorMin = plate.anchorMax = plate.pivot = Vector2.one; plate.anchoredPosition = Vector2.zero;
            var view = Component<EncounterRunHudView>(zone.gameObject); Style(view); Set(view, "strings", strings); Set(view, "plate", plate);
            RectTransform run = At("Run", plate, 12, 7, 316, 27);
            var sigil = Image("SigilOutline", run, theme.SigilAraf, Palette.Text, 0, 3, 15, 21);
            Image("Inner", sigil.transform, theme.SigilAraf, theme.Track, 3, 3, 9, 15);
            var sigilFill = Image("SigilFill", sigil.transform, theme.SigilAraf, Palette.Accent, 3, 3, 9, 15);
            TMP_Text charge = Label("Charge", run, null, 20, 24, 0, 292, 27);
            RectTransform wave = At("Wave", plate, 12, 40, 316, 68);
            var opacity = Component<CanvasGroup>(wave.gameObject);
            RetireLabels(wave, "CounterLabel", "WaveSlash", "WaveMaximum", "RemainingLabel");
            TMP_Text counter = Label("Counter", wave, null, 28, 0, 0, 316, 34);
            TMP_Text remaining = Label("Remaining", wave, null, 24, 0, 33, 316, 29);
            var remainingFill = Bar("RemainingBar", wave, 0, 62, 316, theme.RemainingBarHeight, Palette.Text);
            var underline = Image("RemainingUnderline", remaining.transform, null, Palette.Text, 0, 27, 120, 3); underline.enabled = false;
            // Two 20px lines need ~52px; at 44px the second line was cut to "ÇIKIŞ OD…". Araf plate is 150 high: 40 + 110 fits.
            RectTransform araf = At("Araf", plate, 12, 40, 316, 110);
            RectTransform warning = Chip("Warning", araf, 0, 0, 316, 52, "hud.araf.warning", 20);
            TMP_Text warningText = warning.Find("Text").GetComponent<TMP_Text>(); warningText.textWrappingMode = TextWrappingModes.Normal;
            warningText.lineSpacing = -4f;
            warningText.characterSpacing = 0f;
            // A persistent shape cue accompanies the warning copy inside the same opaque chip.
            Label("Exclamation", warning, "hud.symbol.warning", 20, 4, 0, 12, 52).color = Palette.Background;
            warningText.rectTransform.offsetMin = new Vector2(20, warningText.rectTransform.offsetMin.y);
            RetireLabels(araf, "RoomsLabel", "RoomsSlash", "RoomsMaximum");
            TMP_Text rooms = Label("Rooms", araf, null, 24, 0, 54, 316, 30);
            RectTransform exit = At("Exit", araf, 0, 85, 316, 25);
            Image("Arrow", exit, theme.Arrow, Palette.Text, 0, 4, 15, 15);
            Label("Text", exit, "hud.araf.exitFound", 20, 24, 0, 292, 25);
            wave.gameObject.SetActive(false); araf.gameObject.SetActive(false); exit.gameObject.SetActive(false);
            Set(view, "runGroup", run.gameObject); Set(view, "waveGroup", wave.gameObject); Set(view, "arafGroup", araf.gameObject);
            Set(view, "exitGroup", exit.gameObject); Set(view, "waveOpacity", opacity); Set(view, "waveText", counter);
            Set(view, "remainingText", remaining); Set(view, "chargeText", charge); Set(view, "roomsText", rooms);
            Set(view, "remainingFill", remainingFill); Set(view, "remainingUnderline", underline); Set(view, "sigilFill", sigilFill);
        }

        private static void BuildBoss(RectTransform parent)
        {
            RectTransform container = Corner("BossBar", parent, new Vector2(0.5f, 1f), new Vector2(520f, 44f));
            container.anchoredPosition = new Vector2(0f, -16f);
            RectTransform plate = Plate("BossPlate", container, new Vector2(520f, 44f));
            TMP_Text name = Label("Name", plate, null, 20f, 12f, 2f, 448f, 25f);
            name.alignment = TextAlignmentOptions.Center;
            // This dynamic label belongs exclusively to BossBarView, including language changes.
            LocalizedText obsoleteOwner = name.GetComponent<LocalizedText>();
            if (obsoleteOwner != null) Object.DestroyImmediate(obsoleteOwner);
            var fill = Bar("Health", plate, 12f, 29f, 496f, 9f, Palette.Accent);
            Image("PhaseOneTrack", plate, theme.Diamond, theme.Track, 472f, 9f, 12f, 12f);
            Image("PhaseTwoTrack", plate, theme.Diamond, theme.Track, 490f, 9f, 12f, 12f);
            var first = Image("PhaseOne", plate, theme.Diamond, Palette.Accent, 472f, 9f, 12f, 12f);
            var second = Image("PhaseTwo", plate, theme.Diamond, Palette.Accent, 490f, 9f, 12f, 12f);
            var view = Component<BossBarView>(container.gameObject);
            Set(view, "strings", strings); Set(view, "plate", plate.gameObject); Set(view, "nameText", name);
            Set(view, "healthFill", fill); Set(view, "firstPip", first); Set(view, "secondPip", second);
            fill.fillAmount = 0f; first.enabled = second.enabled = false;
            plate.gameObject.SetActive(false);
        }

        private static void BuildBanner(RectTransform parent)
        {
            RectTransform plate = Plate("BannerPlate", parent, theme.BannerSize);
            plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(0.5f, 1f); plate.anchoredPosition = Vector2.zero;
            var group = Component<CanvasGroup>(plate.gameObject); group.alpha = 0f; group.blocksRaycasts = false;
            TMP_Text first = Label("FirstLine", plate, null, theme.BannerTextSize, 12, 3, 576, 50);
            first.alignment = TextAlignmentOptions.Center;
            first.rectTransform.anchorMin = first.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            first.rectTransform.pivot = Vector2.one * 0.5f; first.rectTransform.anchoredPosition = new Vector2(0, -30);
            TMP_Text second = Label("SecondLine", plate, null, theme.MergedBannerTextSize, 12, 52, 576, 36); second.alignment = TextAlignmentOptions.Center;
            var rule = Image("Rule", plate, null, Palette.Accent, 12, 0, 576, theme.BannerRuleHeight);
            rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f); rule.rectTransform.anchoredPosition = new Vector2(0, 3);
            var view = Component<HudBannerView>(parent.gameObject); Style(view); Set(view, "strings", strings);
            Set(view, "group", group); Set(view, "plate", plate); Set(view, "firstLine", first); Set(view, "secondLine", second); Set(view, "rule", rule);
        }

        private static HudEventSystemGuard BuildEventSystem(GameObject root)
        {
            Transform child = root.transform.Find("EventSystem");
            GameObject events = child != null ? child.gameObject : new GameObject("EventSystem");
            events.transform.SetParent(root.transform, false);
            var system = Component<EventSystem>(events); var module = Component<InputSystemUIInputModule>(events);
            module.deselectOnBackgroundClick = false;
            Set(module, "m_ActionsAsset", AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            string[] fields = { "m_PointAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_LeftClickAction", "m_RightClickAction", "m_ScrollWheelAction" };
            string[] names = { "Point", "Navigate", "Submit", "Cancel", "Click", "RightClick", "ScrollWheel" };
            for (int i = 0; i < fields.Length; i++) Set(module, fields[i], ActionReference(names[i]));
            var guard = Component<HudEventSystemGuard>(events); Set(guard, "bundledSystem", system); Set(guard, "bundledModule", module);
            return guard;
        }

        private static RectTransform BuildDeath(RectTransform layer, RectTransform canvas, CanvasGroup gameplay, HudEventSystemGuard guard)
        {
            var group = Component<CanvasGroup>(layer.gameObject); group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            var backdrop = Image("Backdrop", layer, null, theme.DeathOverlay, 0, 0, 0, 0); Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            RectTransform content = Rect("Content", layer); content.anchorMin = content.anchorMax = content.pivot = Vector2.one * 0.5f;
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(720, 280);
            TMP_Text title = Label("Title", content, "hud.truedeath.title", theme.DeathTitleSize, 0, 0, 720, 90);
            title.font = Palette.TitleFont; title.characterSpacing = theme.DeathTitleTracking; title.alignment = TextAlignmentOptions.Center;
            Image("TitleRule", content, null, Palette.Accent, (720f - theme.DeathTitleRuleWidth) * 0.5f, 97, theme.DeathTitleRuleWidth, theme.BannerRuleHeight);
            RectTransform buttonRect = At("RestartButton", content, 180, 156, theme.DeathButtonSize.x, theme.DeathButtonSize.y);
            // Set the graphic before adding Selectable: its edit-mode Awake must see valid dependencies.
            var background = Component<UnityEngine.UI.Image>(buttonRect.gameObject); background.color = Palette.Panel; background.raycastTarget = true;
            var border = Image("Border", buttonRect, theme.Chip, Palette.Text, 0, 0, 360, 72);
            Stretch(border.rectTransform); border.type = UnityEngine.UI.Image.Type.Sliced; border.fillCenter = false;
            border.pixelsPerUnitMultiplier = 1f / theme.DeathButtonBorderWidth;
            var button = Component<UnityEngine.UI.Button>(buttonRect.gameObject);
            button.targetGraphic = background; button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = button, selectOnDown = button, selectOnLeft = button, selectOnRight = button };
            TMP_Text label = Label("Label", buttonRect, "hud.truedeath.restart", theme.DeathButtonTextSize, 62, 0, 270, 72);
            var seal = Image("FocusSeal", buttonRect, theme.DeathSeal, Color.white, 19, 19.5f, 27, 33);
            var underline = Image("FocusUnderline", buttonRect, MainMenuPlaceholderArt.Sprite("FocusUnderline"), Color.white, 62, 58, 264, 6);
            underline.type = UnityEngine.UI.Image.Type.Sliced; underline.pixelsPerUnitMultiplier = 1f / 3f;
            var view = Component<TrueDeathView>(canvas.gameObject); Style(view);
            Set(view, "deathLayer", layer.gameObject); Set(view, "cardGroup", group); Set(view, "gameplayGroup", gameplay);
            Set(view, "restartButton", button); Set(view, "buttonLabel", label); Set(view, "focusSeal", seal); Set(view, "focusUnderline", underline); Set(view, "eventSystemGuard", guard);
            guard.GetComponent<EventSystem>().firstSelectedGameObject = button.gameObject;
            layer.SetAsLastSibling(); layer.gameObject.SetActive(false);
            return content;
        }

        private static void WirePlayerPrefab(GameObject player)
        {
            WirePlayer(player);
#pragma warning disable CS0618
            foreach (RetroFPS.UI.AmmoHUD legacy in player.GetComponents<RetroFPS.UI.AmmoHUD>()) Object.DestroyImmediate(legacy);
#pragma warning restore CS0618
        }

        private static void WirePlayer(GameObject player)
        {
            EnsurePlayerHudWiring(player);
            PauseMenuBuilder.EnsurePlayerPauseWiring(player);
        }

        /// <summary>Repairs only Player HUD ownership and references, without rebuilding assets or saving scenes.</summary>
        internal static void EnsurePlayerHudWiring(GameObject player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            config = AssetDatabase.LoadAssetAtPath<HudConfig>(Data + "HudConfig_Default.asset");
            if (config == null) throw new InvalidOperationException("Build Combat HUD before wiring Player.");
            HudBindings bindings = SinglePlayerComponent<HudBindings>(player);
            GameObject hud = SingleNestedPrefab(player, AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
            hud.name = "CombatHud"; hud.transform.localPosition = Vector3.zero; hud.transform.localRotation = Quaternion.identity; hud.transform.localScale = Vector3.one;
            Set(bindings, "health", player.GetComponent<PlayerHealth>()); Set(bindings, "movement", player.GetComponent<PlayerMovement>());
            Set(bindings, "parry", player.GetComponent<PlayerParryController>()); Set(bindings, "weapons", player.GetComponent<WeaponController>());
            Set(bindings, "ammo", player.GetComponent<PlayerAmmoInventory>()); Set(bindings, "config", config); Set(bindings, "hud", hud);
            Set(bindings, "healthView", hud.GetComponentInChildren<HealthHudView>(true)); Set(bindings, "abilityView", hud.GetComponentInChildren<AbilityHudView>(true));
            Set(bindings, "weaponView", hud.GetComponentInChildren<WeaponAmmoHudView>(true)); Set(bindings, "encounterView", hud.GetComponentInChildren<EncounterRunHudView>(true));
            Set(bindings, "bossView", hud.GetComponentInChildren<BossBarView>(true));
            Set(bindings, "bannerView", hud.GetComponentInChildren<HudBannerView>(true)); Set(bindings, "deathView", hud.GetComponentInChildren<TrueDeathView>(true));
            Set(bindings, "cardOfferView", hud.GetComponentInChildren<CardOfferView>(true));
            Set(bindings, "activeCardsView", hud.GetComponentInChildren<ActiveCardsHudView>(true));
            var feedback = Component<PlayerDamageFeedback>(player); Set(feedback, "hudConfig", config);
            var debug = Component<PlayerDebugHUD>(player); Set(debug, "hudConfig", config); Set(debug, "showHUD", false);
            string[] sources = { "health", "movement", "parry", "weapons", "ammo" };
            foreach (string source in sources)
                if (new SerializedObject(bindings).FindProperty(source).objectReferenceValue == null) throw new InvalidOperationException("Player is missing HUD source: " + source);
        }

        // Retain the first authored instance (and its local IDs); later duplicates are builder debris.
        internal static GameObject SingleNestedPrefab(GameObject player, GameObject prefab)
        {
            if (prefab == null) throw new InvalidOperationException("Required nested prefab is missing.");
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject retained = null;
            for (int i = 0; i < player.transform.childCount;)
            {
                GameObject child = player.transform.GetChild(i).gameObject;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child) != path) { i++; continue; }
                if (retained == null) { retained = child; i++; }
                else Object.DestroyImmediate(child);
            }
            return retained != null ? retained : (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        }

        internal static T SinglePlayerComponent<T>(GameObject player) where T : Component
        {
            T retained = Component<T>(player);
            foreach (T candidate in player.GetComponentsInChildren<T>(true))
                if (candidate != retained) Object.DestroyImmediate(candidate);
            return retained;
        }

        private static void WireDirectorPrefab(GameObject root)
        {
#pragma warning disable CS0618
            foreach (RetroFPS.UI.RunHUD legacy in root.GetComponents<RetroFPS.UI.RunHUD>()) Object.DestroyImmediate(legacy);
#pragma warning restore CS0618
            var handler = Component<RunRestartHandler>(root); Set(handler, "director", root.GetComponent<RunDirector>());
        }

        private static void EnsureSampleDirector()
        {
            Scene originalActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(SamplePath); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Additive);
            try
            {
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects()) count += root.GetComponentsInChildren<RunDirector>(true).Length;
                if (count > 1) throw new InvalidOperationException("SampleScene already contains multiple RunDirectors; resolve them without deleting authored objects.");
                if (count == 0)
                {
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DirectorPath), scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save SampleScene.");
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
            }
        }

        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { string before = Fingerprint(root); edit(root); if (before != Fingerprint(root)) Save(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static string Fingerprint(GameObject root)
        {
            var result = new StringBuilder();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                result.Append(EditorJsonUtility.ToJson(component)); result.Append(EditorJsonUtility.ToJson(component.gameObject));
                // ToJson does not reflect object-reference changes, so fold references in explicitly.
                SerializedProperty it = new SerializedObject(component).GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                    { Object reference = it.objectReferenceValue; result.Append(it.propertyPath).Append('=').Append(reference == null ? "null" : reference.GetType().Name + ":" + reference.name).Append(';'); }
            }
            return result.ToString();
        }
        private static void Save(GameObject root, string path)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                { PrefabUtility.RecordPrefabInstancePropertyModifications(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject); }
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new InvalidOperationException("Cannot save prefab: " + path);
        }
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
        private static InputActionReference ActionReference(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ActionsPath))
                if (asset is InputActionReference reference && reference.action != null && reference.action.actionMap.name == "UI" && reference.action.name == name) return reference;
            return null;
        }
        private static T Component<T>(GameObject root) where T : Component
        { T component = root.GetComponent<T>(); return component != null ? component : root.AddComponent<T>(); }
        private static void Style(Object view) { Set(view, "config", config); Set(view, "theme", theme); }
        private static RectTransform Rect(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return (RectTransform)existing;
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform At(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static RectTransform Corner(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            RectTransform rect = Rect(name, parent); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size; return rect;
        }
        private static RectTransform Plate(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = At(name, parent, 0, 0, size.x, size.y);
            // Separate tinted layers keep palette swaps in theme data, including the border and opaque bar tracks.
            var border = Component<UnityEngine.UI.Image>(rect.gameObject); border.sprite = theme.Plate;
            border.color = theme.Border; border.type = UnityEngine.UI.Image.Type.Sliced; border.pixelsPerUnitMultiplier = 1f / 3f; border.raycastTarget = false;
            var inner = Image("Backing", rect, theme.Plate, Palette.Background, 3, 3, size.x - 6, size.y - 6);
            Stretch(inner.rectTransform); inner.rectTransform.offsetMin = new Vector2(3, 3); inner.rectTransform.offsetMax = new Vector2(-3, -3);
            Color color = Palette.Background; color.a = config.PlateAlpha; inner.color = color;
            inner.type = UnityEngine.UI.Image.Type.Sliced; inner.pixelsPerUnitMultiplier = 1f / 3f;
            // Border-only source prevents stacking a second opaque backing behind the 0.90 plate.
            border.fillCenter = false;
            inner.transform.SetAsFirstSibling(); return rect;
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Sprite sprite, Color color, float x, float y, float width, float height)
        {
            RectTransform rect = At(name, parent, x, y, width, height);
            var image = Component<UnityEngine.UI.Image>(rect.gameObject); image.sprite = sprite; image.color = color; image.raycastTarget = false; return image;
        }
        private static UnityEngine.UI.Image Bar(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var outline = Image(name, parent, theme.BarTrack, Palette.Text, x, y, width, height);
            var track = Image("Track", outline.transform, theme.BarTrack, theme.Track, 3, 3, width - 6, Mathf.Max(1, height - 6));
            if (height <= 6) { track.rectTransform.sizeDelta = new Vector2(width - 6, height - 2); track.rectTransform.anchoredPosition = new Vector2(3, -1); }
            var fill = Image("Fill", track.transform, theme.BarFill, color, 0, 0, width - 6, Mathf.Max(1, height - 6));
            Stretch(fill.rectTransform); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; return fill;
        }
        private static void Radial(UnityEngine.UI.Image image)
        { image.type = UnityEngine.UI.Image.Type.Filled; image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; image.fillOrigin = 2; }
        private static RectTransform Chip(string name, Transform parent, float x, float y, float width, float height, string key, float size)
        {
            var chip = Image(name, parent, theme.Chip, Palette.Danger, x, y, width, height); chip.type = UnityEngine.UI.Image.Type.Sliced;
            TMP_Text text = Label("Text", chip.transform, key, size, 3, 0, width - 6, height); text.color = Palette.Background; text.alignment = TextAlignmentOptions.Center;
            return chip.rectTransform;
        }
        // Obsolete text leaves have no live bindings; remove them from the generated prefab.
        private static void RetireLabels(Transform parent, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform child = parent.Find(names[i]);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
        }
        private static TMP_Text Label(string name, Transform parent, string key, float size, float x, float y, float width, float height)
        {
            RectTransform rect = At(name, parent, x, y, width, height);
            var label = Component<TextMeshProUGUI>(rect.gameObject); label.font = Palette.UiFont; label.fontSize = size;
            label.fontStyle = FontStyles.Bold; label.color = Palette.Text; label.raycastTarget = false;
            label.enableAutoSizing = false; label.fontSizeMin = theme.MinimumTextSize; label.fontSizeMax = size;
            label.characterSpacing = size == theme.LabelSize ? theme.LabelTracking : 0f;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.vertexBufferAutoSizeReduction = false; label.richText = true;
            label.gameObject.SetActive(true);
            label.text = string.Empty;
            if (key != null)
            {
                var localized = Component<LocalizedText>(rect.gameObject); Set(localized, "strings", strings); Set(localized, "key", key); Set(localized, "label", label);
                // Runtime labels resolve the table on enable; prefab text contains no hardcoded copy.
            }
            return label;
        }
        private static void Set(Object target, string property, Object value)
        { var so = new SerializedObject(target); so.FindProperty(property).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string property, string value)
        { var so = new SerializedObject(target); so.FindProperty(property).stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string property, bool value)
        { var so = new SerializedObject(target); so.FindProperty(property).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
