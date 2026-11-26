using System;
using RetroFPS.Rendering.PostFx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Deterministically authors the PostFx profiles, rig and nested player integration.</summary>
    public static class PostFxBuilder
    {
        private const string DataFolder = "Assets/_Project/Data/Rendering/PostFx";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Rendering";
        private const string RigPath = PrefabFolder + "/PostFx_Rig.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player/Player.prefab";

        /// <summary>Creates or updates owned assets without replacing existing asset GUIDs.</summary>
        [MenuItem("RetroFPS/Build Post FX")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build Post FX must run outside Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null)
                throw new InvalidOperationException("Build Post FX requires the existing Player prefab.");

            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);
            VolumeProfile baseProfile = Profile("Base");
            VolumeProfile araf = Profile("Araf");
            VolumeProfile boss = Profile("Boss");
            VolumeProfile phase2 = Profile("BossPhase2");
            VolumeProfile low = Profile("LowHealth");
            VolumeProfile death = Profile("Death");
            AuthorBase(baseProfile);
            AuthorAraf(araf);
            AuthorBoss(boss);
            AuthorPhase2(phase2);
            Set(Component<ColorAdjustments>(low).saturation, -35f);
            Set(Component<Vignette>(low).intensity, 0.36f);
            ColorAdjustments deathColor = Component<ColorAdjustments>(death);
            Set(deathColor.saturation, -90f);
            Set(deathColor.contrast, 20f);
            Set(Component<Vignette>(death).intensity, 0.45f);
            Set(Component<FilmGrain>(death).intensity, 0.45f);
            SaveProfile(baseProfile);
            SaveProfile(araf);
            SaveProfile(boss);
            SaveProfile(phase2);
            SaveProfile(low);
            SaveProfile(death);

            BuildRig(DefaultConfig(), baseProfile, araf, boss, phase2, low, death);
            NestRig();
            // Save only assets owned by this builder, avoiding unrelated dirty assets.
            Debug.Log("[PostFxBuilder] Updated PostFx profiles, config and Player's nested PostFx_Rig.");
        }

        private static void AuthorBase(VolumeProfile profile)
        {
            Set(Component<Tonemapping>(profile).mode, TonemappingMode.Neutral);
            Bloom bloom = Component<Bloom>(profile);
            Set(bloom.threshold, 0.85f);
            Set(bloom.intensity, 0.60f);
            Set(bloom.scatter, 0.55f);
            Set(bloom.tint, new Color(1f, 0.90f, 0.78f));
            Set(bloom.highQualityFiltering, false);
            ColorAdjustments color = Component<ColorAdjustments>(profile);
            Set(color.contrast, 6f);
            Set(color.saturation, -4f);
            Set(color.colorFilter, new Color(1f, 0.96f, 0.90f));
            SplitToning split = Component<SplitToning>(profile);
            Set(split.shadows, new Color(0.43f, 0.41f, 0.56f));
            Set(split.highlights, new Color(0.66f, 0.52f, 0.36f));
            Set(split.balance, -10f);
            Vignette vignette = Component<Vignette>(profile);
            Set(vignette.color, new Color(0.05f, 0f, 0.01f));
            Set(vignette.intensity, 0.16f);
            Set(vignette.smoothness, 0.35f);
            Set(vignette.rounded, false);
            FilmGrain grain = Component<FilmGrain>(profile);
            Set(grain.type, FilmGrainLookup.Medium1);
            Set(grain.intensity, 0.20f);
            Set(grain.response, 0.8f);
        }

        private static void AuthorAraf(VolumeProfile profile)
        {
            ColorAdjustments color = Component<ColorAdjustments>(profile);
            Set(color.contrast, 8f);
            Set(color.saturation, 6f);
            Set(color.colorFilter, new Color(0.86f, 0.82f, 1.0f));
            SplitToning split = Component<SplitToning>(profile);
            Set(split.shadows, new Color(0.42f, 0.36f, 0.62f));
            Set(split.highlights, new Color(0.62f, 0.55f, 0.70f));
            Set(split.balance, 0f);
            Vignette vignette = Component<Vignette>(profile);
            Set(vignette.color, new Color(0.10f, 0f, 0.16f));
            Set(vignette.intensity, 0.26f);
            Set(vignette.smoothness, 0.4f);
            Set(Component<FilmGrain>(profile).intensity, 0.36f);
            Bloom bloom = Component<Bloom>(profile);
            Set(bloom.intensity, 0.65f);
            Set(bloom.tint, new Color(0.85f, 0.72f, 1.0f));
        }

        private static void AuthorBoss(VolumeProfile profile)
        {
            ColorAdjustments color = Component<ColorAdjustments>(profile);
            Set(color.contrast, 10f);
            Set(color.saturation, -18f);
            Set(color.colorFilter, new Color(0.94f, 0.96f, 1f));
            SplitToning split = Component<SplitToning>(profile);
            Set(split.shadows, new Color(0.40f, 0.44f, 0.58f));
            Set(split.highlights, new Color(0.62f, 0.57f, 0.42f));
            Set(split.balance, -5f);
            Vignette vignette = Component<Vignette>(profile);
            Set(vignette.intensity, 0.30f);
            Set(vignette.color, new Color(0f, 0f, 0.02f));
        }

        private static void AuthorPhase2(VolumeProfile profile)
        {
            ColorAdjustments color = Component<ColorAdjustments>(profile);
            Set(color.saturation, -10f);
            Set(color.colorFilter, new Color(1f, 0.86f, 0.82f));
            Vignette vignette = Component<Vignette>(profile);
            Set(vignette.color, new Color(0.25f, 0f, 0.02f));
            Set(vignette.intensity, 0.34f);
            Set(Component<FilmGrain>(profile).intensity, 0.30f);
        }

        private static VolumeProfile Profile(string suffix)
        {
            string path = DataFolder + "/PostFx_" + suffix + ".asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "PostFx_" + suffix;
                AssetDatabase.CreateAsset(profile, path);
            }
            // Never inherit obsolete/unlisted overrides from a previous builder revision.
            foreach (VolumeComponent component in profile.components)
                if (component != null) component.SetAllOverridesTo(false);
            return profile;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component)) component = profile.Add<T>(false);
            component.active = true;
            if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void Set<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void SaveProfile(VolumeProfile profile)
        {
            foreach (VolumeComponent component in profile.components)
                if (component != null) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static PostFxConfig DefaultConfig()
        {
            string path = DataFolder + "/PostFxConfig_Default.asset";
            PostFxConfig config = AssetDatabase.LoadAssetAtPath<PostFxConfig>(path);
            PostFxConfig defaults = ScriptableObject.CreateInstance<PostFxConfig>();
            defaults.name = "PostFxConfig_Default";
            if (config == null)
            {
                config = defaults;
                AssetDatabase.CreateAsset(config, path);
            }
            else
            {
                try { EditorUtility.CopySerialized(defaults, config); }
                finally { Object.DestroyImmediate(defaults); }
            }
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            return config;
        }

        private static void BuildRig(PostFxConfig config, VolumeProfile baseProfile, VolumeProfile araf,
            VolumeProfile boss, VolumeProfile phase2, VolumeProfile low, VolumeProfile death)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(RigPath) : new GameObject("PostFx_Rig");
            try
            {
                root.name = "PostFx_Rig";
                root.layer = 0;
                root.SetActive(true);
                PostFxDirector director = root.GetComponent<PostFxDirector>();
                if (director == null) director = root.AddComponent<PostFxDirector>();
                var data = new SerializedObject(director);
                data.FindProperty("config").objectReferenceValue = config;
                WireVolume(data, "baseVolume", root, "Base", 10f, baseProfile, 1f);
                WireVolume(data, "arafVolume", root, "Araf", 20f, araf, 0f);
                WireVolume(data, "bossVolume", root, "Boss", 21f, boss, 0f);
                WireVolume(data, "bossPhase2Volume", root, "BossPhase2", 22f, phase2, 0f);
                WireVolume(data, "lowHealthVolume", root, "LowHealth", 30f, low, 0f);
                WireVolume(data, "deathVolume", root, "Death", 31f, death, 0f);
                WireVolume(data, "runtimeVolume", root, "Runtime", 50f, null, 1f);
                data.ApplyModifiedPropertiesWithoutUndo();
                SavePrefab(root, RigPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void WireVolume(SerializedObject data, string field, GameObject root, string suffix,
            float priority, VolumeProfile profile, float weight)
        {
            string name = "Volume_" + suffix;
            Transform child = root.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(root.transform, false);
            }
            child.gameObject.layer = 0;
            child.gameObject.SetActive(true);
            Volume volume = child.GetComponent<Volume>();
            if (volume == null) volume = child.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
            volume.weight = weight;
            volume.enabled = weight > 0f;
            data.FindProperty(field).objectReferenceValue = volume;
        }

        private static void NestRig()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
                // Existing HUD helper retains the first matching direct child and removes duplicates.
                // Player references resolve once in Awake, keeping nested instance overrides minimal.
                CombatHudBuilder.SingleNestedPrefab(player, prefab);
                SavePrefab(player, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new InvalidOperationException("Cannot save PostFx prefab: " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
