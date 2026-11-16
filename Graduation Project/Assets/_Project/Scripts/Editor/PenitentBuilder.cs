using System;
using System.Collections.Generic;
using RetroFPS.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Builds replaceable Penitent placeholder art and independent combat hitboxes.</summary>
    public sealed class PenitentBuilder : ScriptableSingleton<PenitentBuilder>
    {
        private const string EnemyPath = "Assets/_Project/Prefabs/Enemies/Enemy_Penitent_Kefaretci.prefab";
        private const string GhoulPath = "Assets/_Project/Prefabs/Enemies/Enemy_Ghoul_Stalker.prefab";
        private const string MaterialDirectory = "Assets/_Project/Art/Enemies/Penitent/Placeholder";
        // Editor-only authoring dimensions; gameplay tuning lives on the runtime components.
        private const float FaceRadius = 0.07f;
        [SerializeField, Tooltip("Placeholder visor opening height in metres.")] private float slitHeight = 0.09f;
        [SerializeField, Tooltip("Placeholder visor opening width in metres.")] private float slitWidth = 0.22f;
        [SerializeField, Tooltip("Visor and face centre above the enemy's feet.")] private float slitCentreHeight = 1.9f;
        [SerializeField, Tooltip("Shield slab centre forward of the enemy's feet.")] private float lidForwardOffset = 0.46f;
        private const float LidWidth = 1.1f;
        private const float LidBottom = 0.15f;
        private const float LidTop = 2.35f;
        private const float LidDepth = 0.12f;
        private const float ShieldPivotHeight = 0.25f;
        private static Vector3 FacePosition => new Vector3(0f, instance.slitCentreHeight, 0.35f);

        /// <summary>Rebuilds the enemy in place, retaining existing IDs and removing obsolete generated wave assets.</summary>
        [MenuItem("RetroFPS/Build Penitent")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build Penitent must run outside Play Mode.");
            GameObject ghoul = AssetDatabase.LoadAssetAtPath<GameObject>(GhoulPath);
            if (ghoul == null || ghoul.GetComponent<EnemyHealthDropper>() == null || ghoul.GetComponent<EnemyAmmoDropper>() == null)
                throw new InvalidOperationException("The Ghoul prefab with both droppers is required.");
            EnsureFolder(MaterialDirectory);
            EnsureFolder("Assets/_Project/Prefabs/Enemies");
            bool customVisual = HasCustomVisual(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath));
            Material robe = customVisual ? null : MaterialAsset("Mat_Penitent_Robe", true, new Color(0.14f, 0.15f, 0.16f), Color.black);
            Material skin = customVisual ? null : MaterialAsset("Mat_Penitent_Skin", true, new Color(0.75f, 0.72f, 0.61f), Color.black);
            Material lid = customVisual ? null : MaterialAsset("Mat_Penitent_Lid", true, new Color(0.18f, 0.2f, 0.21f), new Color(0.12f, 0.07f, 0.025f));
            Material chain = customVisual ? null : MaterialAsset("Mat_Penitent_Chain", true, new Color(0.35f, 0.16f, 0.07f), Color.black);
            Material outline = MaterialAsset("Mat_Penitent_SlamOutline", false, new Color(0.38f, 0.025f, 0.015f, 0.24f), Color.black, true);
            Material fill = MaterialAsset("Mat_Penitent_SlamFill", false, new Color(1f, 0.18f, 0.035f, 0.65f), Color.black, true);
            Material impact = MaterialAsset("Mat_Penitent_SlamImpact", false, new Color(1f, 0.85f, 0.6f, 0.9f), Color.black, true);
            Material face = customVisual ? null : MaterialAsset("Mat_Penitent_Face", false, new Color(1.3f, 1.25f, 1.1f), new Color(0.3f, 0.28f, 0.2f));
            BuildPrefab(EnemyPath, "Enemy_Penitent_Kefaretci", root => ConfigureEnemy(root, ghoul, robe, skin, lid, chain, outline, fill, impact, face));
            DeleteStaleAsset("Assets/_Project/Prefabs/Enemies/Penitent_Shockwave.prefab");
            DeleteStaleAsset(MaterialDirectory + "/Mat_Penitent_Shockwave.mat");
            DeleteStaleAsset(MaterialDirectory + "/Mat_Penitent_Glint.mat");
            DeleteStaleAsset(MaterialDirectory + "/Mat_Penitent_SlamLine.mat");
            AssetDatabase.SaveAssets();
            Debug.Log("[PenitentBuilder] Built Penitent visor shield, fixed slam area, materials, sockets, hitboxes and view wiring.");
        }

        /// <summary>Rebinds active prefab art, preserving the current glow list when no shield art is found.</summary>
        [MenuItem("RetroFPS/Penitent/Rebind Visual Renderers")]
        public static void RebindVisualRenderers()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rebind Visual Renderers must run outside Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPath) == null)
                throw new InvalidOperationException("Run RetroFPS/Build Penitent before rebinding renderers.");
            BuildPrefab(EnemyPath, "Enemy_Penitent_Kefaretci", RebindRenderers);
            AssetDatabase.SaveAssets();
        }

        private static void RebindRenderers(GameObject root)
        {
            Transform visual = root.transform.Find("Visual");
            PenitentView view = root.GetComponent<PenitentView>();
            if (visual == null || view == null)
                throw new InvalidOperationException("Penitent requires Visual and PenitentView before rebinding.");
            var tints = new List<Renderer>();
            var glows = new List<Renderer>();
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsActiveInPrefab(renderer.transform)) continue;
                string name = renderer.gameObject.name;
                // The view tints its shield/lid renderers through the glow list; keep them out of the tint list.
                if (name.IndexOf("Lid", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                    glows.Add(renderer);
                else tints.Add(renderer);
            }
            var data = new SerializedObject(view);
            References(data, "tintRenderers", tints.ToArray());
            if (glows.Count > 0) References(data, "glowRenderers", glows.ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool IsActiveInPrefab(Transform node)
        {
            for (Transform current = node; current != null; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private static bool HasCustomVisual(GameObject root)
        {
            if (root == null) return false;
            if (root.transform.Find("Visual/Model") != null) return true;
            PenitentView view = root.GetComponent<PenitentView>();
            return view != null && Property(new SerializedObject(view), "animator").objectReferenceValue != null;
        }

        private static void ConfigureEnemy(GameObject root, GameObject ghoul,
            Material robe, Material skin, Material lidMaterial, Material chainMaterial,
            Material outlineMaterial, Material fillMaterial, Material impactMaterial, Material faceMaterial)
        {
            root.layer = ghoul.layer;
            root.tag = ghoul.tag;
            CharacterController controller = Ensure<CharacterController>(root);
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = new Vector3(0f, 1f, 0f);
            PenitentAI ai = Ensure<PenitentAI>(root);
            PenitentView view = Ensure<PenitentView>(root);
            EnemyHealthDropper health = Ensure<EnemyHealthDropper>(root);
            EnemyAmmoDropper ammo = Ensure<EnemyAmmoDropper>(root);
            EditorUtility.CopySerialized(ghoul.GetComponent<EnemyHealthDropper>(), health);
            EditorUtility.CopySerialized(ghoul.GetComponent<EnemyAmmoDropper>(), ammo);

            Transform visual = Child(root.transform, "Visual", root.layer);
            Transform hitboxes = Child(root.transform, "Hitboxes", root.layer);
            Transform sockets = Child(root.transform, "Sockets", root.layer);
            Transform telegraph = Child(root.transform, "Telegraph", root.layer);
            var viewData = new SerializedObject(view);
            bool customVisual = HasCustomVisual(root);
            Transform pivot = null;
            if (!customVisual)
            {
                RemoveLegacyPlaceholders(visual);
                Transform placeholder = Child(visual, "Placeholder", visual.gameObject.layer);
                Transform body = Primitive(placeholder, "Body", PrimitiveType.Capsule, new Vector3(0f, 1f, -0.05f), new Vector3(0.85f, 1f, 0.7f), robe);
                body.localRotation = Quaternion.Euler(12f, 0f, 0f);
                Primitive(placeholder, "Hood", PrimitiveType.Sphere, new Vector3(0f, 1.9f, 0.02f), new Vector3(0.55f, 0.6f, 0.5f), robe);
                Primitive(placeholder, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.9f, 0.18f), new Vector3(0.32f, 0.36f, 0.32f), skin);
                Primitive(placeholder, "Face_Mark", PrimitiveType.Sphere, new Vector3(0f, instance.slitCentreHeight, 0.42f), Vector3.one * 0.055f, faceMaterial);
                Chain(placeholder, "Chain_L", new Vector3(-0.38f, 1.65f, 0.1f), new Vector3(-0.43f, 1.2f, 0.6f), chainMaterial);
                Chain(placeholder, "Chain_R", new Vector3(0.38f, 1.65f, 0.1f), new Vector3(0.43f, 1.2f, 0.6f), chainMaterial);
                pivot = Child(placeholder, "ShieldPivot", placeholder.gameObject.layer);
                pivot.localPosition = new Vector3(0f, ShieldPivotHeight, instance.lidForwardOffset);
                pivot.localRotation = Quaternion.identity;
                BuildLidPieces(pivot, lidMaterial);
            }
            else
            {
                pivot = visual.Find("Placeholder/ShieldPivot");
            }

            int hitboxLayer = HitboxLayer(ghoul);
            Transform head = Child(hitboxes, "Hitbox_Head", hitboxLayer);
            if (!customVisual || head.GetComponent<SocketFollow>() == null || head.GetComponent<SocketFollow>().Target == null)
                head.localPosition = new Vector3(0f, 1.9f, 0.1f);
            Ensure<SphereCollider>(head.gameObject).radius = 0.22f;
            ConfigureHitbox(Ensure<Hitbox>(head.gameObject), ai, 2f, true);
            // Target stays null by default and is never overwritten, so a model's head bone survives rebuilds.
            Ensure<SocketFollow>(head.gameObject);
            Transform back = Child(hitboxes, "Hitbox_Back", hitboxLayer);
            back.localPosition = new Vector3(0f, 1.1f, -0.35f);
            Ensure<BoxCollider>(back.gameObject).size = new Vector3(0.7f, 1.2f, 0.35f);
            ConfigureHitbox(Ensure<Hitbox>(back.gameObject), ai, 1.5f, false);
            Transform face = Child(hitboxes, "Hitbox_Face", hitboxLayer);
            if (!customVisual || face.GetComponent<SocketFollow>() == null || face.GetComponent<SocketFollow>().Target == null)
                face.localPosition = FacePosition;
            Ensure<SphereCollider>(face.gameObject).radius = FaceRadius;
            var faceData = new SerializedObject(Ensure<PenitentWeakPoint>(face.gameObject));
            Reference(faceData, "owner", ai);
            faceData.ApplyModifiedPropertiesWithoutUndo();
            Ensure<SocketFollow>(face.gameObject);
            Transform shield = Child(hitboxes, "Hitbox_Shield", hitboxLayer);
            SocketFollow shieldFollow = Ensure<SocketFollow>(shield.gameObject);
            if (!customVisual || shieldFollow.Target == null)
            {
                shieldFollow.SetTarget(pivot);
                shield.localPosition = new Vector3(0f, ShieldPivotHeight, instance.lidForwardOffset);
                shield.localRotation = Quaternion.identity;
            }
            // Placeholder migration replaces the monolithic box. Custom art owns every existing collider,
            // including a resized legacy box, so it is rebuilt only when no shield colliders remain.
            if (!customVisual)
            {
                BoxCollider legacyShield = shield.GetComponent<BoxCollider>();
                if (legacyShield != null) Object.DestroyImmediate(legacyShield);
            }
            Collider[] shieldColliders = shield.GetComponentsInChildren<Collider>(true);
            if (!customVisual || shieldColliders.Length == 0)
            {
                BuildShieldColliders(shield);
                shieldColliders = shield.GetComponentsInChildren<Collider>(true);
            }
            ShieldPlate plate = Ensure<ShieldPlate>(shield.gameObject);
            var plateData = new SerializedObject(plate);
            Reference(plateData, "owner", ai);
            plateData.ApplyModifiedPropertiesWithoutUndo();

            Transform shieldSocket = Socket(sockets, "Socket_ShieldCenter", new Vector3(0f, 0.9f, 0.65f));
            Transform impactSocket = Socket(sockets, "Socket_SlamImpact", new Vector3(0f, 0.05f, 1.6f));
            Transform headSocket = Socket(sockets, "Socket_Head", new Vector3(0f, 1.9f, 0.1f));
            Transform deathSocket = Socket(sockets, "Socket_DeathFx", new Vector3(0f, 1f, 0f));
            RemoveChild(telegraph, "Glint");
            RemoveChild(telegraph, "SlamLine");
            Transform outline = Primitive(telegraph, "SlamArea_Outline", PrimitiveType.Quad, Vector3.zero, Vector3.one, outlineMaterial);
            BuildBorder(outline, "Near", new Vector3(0f, -0.5f, 0.001f), new Vector3(1f, 0.012f, 1f), outlineMaterial);
            BuildBorder(outline, "Far", new Vector3(0f, 0.5f, 0.001f), new Vector3(1f, 0.012f, 1f), outlineMaterial);
            BuildBorder(outline, "Left", new Vector3(-0.5f, 0f, 0.001f), new Vector3(0.015f, 1f, 1f), outlineMaterial);
            BuildBorder(outline, "Right", new Vector3(0.5f, 0f, 0.001f), new Vector3(0.015f, 1f, 1f), outlineMaterial);
            Transform fill = Primitive(telegraph, "SlamArea_Fill", PrimitiveType.Quad, Vector3.zero, Vector3.one, fillMaterial);
            Transform impact = Primitive(telegraph, "SlamArea_Impact", PrimitiveType.Quad, Vector3.zero, Vector3.one, impactMaterial);
            outline.gameObject.SetActive(false);
            fill.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
            Transform lightNode = Child(telegraph, "TelegraphLight", telegraph.gameObject.layer);
            lightNode.localPosition = new Vector3(0f, 1.55f, instance.lidForwardOffset);
            lightNode.gameObject.SetActive(true);
            Light telegraphLight = Ensure<Light>(lightNode.gameObject);
            telegraphLight.type = LightType.Point;
            telegraphLight.color = new Color(1f, 0.6f, 0.05f);
            telegraphLight.range = 6f;
            telegraphLight.shadows = LightShadows.None;
            telegraphLight.enabled = false;

            if (!customVisual)
            {
                RebindRenderers(root);
                // Rebind used its own SerializedObject; refresh before applying the remaining bindings.
                viewData.Update();
                Reference(viewData, "shieldPivot", pivot);
                Property(viewData, "guardPosition").vector3Value = pivot.localPosition;
            }
            Reference(viewData, "visualRoot", visual);
            References(viewData, "shieldColliders", shieldColliders);
            Reference(viewData, "hitboxRoot", hitboxes);
            Reference(viewData, "shieldCenterSocket", shieldSocket);
            Reference(viewData, "slamImpactSocket", impactSocket);
            Reference(viewData, "headSocket", headSocket);
            Reference(viewData, "deathFxSocket", deathSocket);
            Reference(viewData, "slamAreaOutline", outline);
            Reference(viewData, "slamAreaFill", fill);
            Reference(viewData, "slamAreaImpact", impact);
            Reference(viewData, "telegraphLight", telegraphLight);
            viewData.ApplyModifiedPropertiesWithoutUndo();
            var aiData = new SerializedObject(ai);
            MigrateFloat(aiData, "attackSettings.slamTriggerRange", 7f, 5.5f);
            MigrateFloat(aiData, "attackSettings.slamWindup", 0.9f, 1.1f);
            Reference(aiData, "view", view);
            Reference(aiData, "healthDropper", health);
            Reference(aiData, "ammoDropper", ammo);
            aiData.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(customVisual ? "[PenitentBuilder] Custom visual mode: preserved model and visual bindings."
                : "[PenitentBuilder] Placeholder mode: updated builder-owned placeholder art.");
        }

        private static void MigrateFloat(SerializedObject data, string name, float previous, float current)
        {
            SerializedProperty value = Property(data, name);
            if (Mathf.Approximately(value.floatValue, previous)) value.floatValue = current;
        }

        private static void DeleteStaleAsset(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        }

        private static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void BuildBorder(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            Transform edge = Primitive(parent, name, PrimitiveType.Quad, position, scale, material);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(1f, 0.16f, 0.03f, 0.9f));
            edge.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        private static void LidDimensions(int index, out string name, out Vector3 position, out Vector3 size)
        {
            float lower = instance.slitCentreHeight - instance.slitHeight * 0.5f;
            float upper = instance.slitCentreHeight + instance.slitHeight * 0.5f;
            float sideWidth = (LidWidth - instance.slitWidth) * 0.5f;
            if (index == 0)
            {
                name = "Shield_Lid";
                position = new Vector3(0f, (LidBottom + lower) * 0.5f - ShieldPivotHeight, 0f);
                size = new Vector3(LidWidth, lower - LidBottom, LidDepth);
            }
            else if (index == 1)
            {
                name = "Shield_Lid_Top";
                position = new Vector3(0f, (upper + LidTop) * 0.5f - ShieldPivotHeight, 0f);
                size = new Vector3(LidWidth, LidTop - upper, LidDepth);
            }
            else
            {
                name = index == 2 ? "Shield_Lid_SideL" : "Shield_Lid_SideR";
                float sign = index == 2 ? -1f : 1f;
                position = new Vector3(sign * (instance.slitWidth + sideWidth) * 0.5f,
                    instance.slitCentreHeight - ShieldPivotHeight, 0f);
                size = new Vector3(sideWidth, instance.slitHeight, LidDepth);
            }
        }

        private static void BuildLidPieces(Transform pivot, Material material)
        {
            for (int i = 0; i < 4; i++)
            {
                LidDimensions(i, out string name, out Vector3 position, out Vector3 size);
                Primitive(pivot, name, PrimitiveType.Cube, position, size, material);
            }
        }

        private static void BuildShieldColliders(Transform shield)
        {
            for (int i = 0; i < 4; i++)
            {
                LidDimensions(i, out string name, out Vector3 position, out Vector3 size);
                Transform piece = Child(shield, name, shield.gameObject.layer);
                piece.SetLocalPositionAndRotation(position, Quaternion.identity);
                piece.localScale = Vector3.one;
                BoxCollider box = Ensure<BoxCollider>(piece.gameObject);
                box.center = Vector3.zero;
                box.size = size;
                box.isTrigger = false;
            }
        }

        private static void BuildPrefab(string path, string name, Action<GameObject> configure)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            try
            {
                root.name = name;
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException("Could not save Penitent prefab: " + path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static int HitboxLayer(GameObject ghoul)
        {
            Hitbox[] hitboxes = ghoul.GetComponentsInChildren<Hitbox>(true);
            if (hitboxes.Length > 0) return hitboxes[0].gameObject.layer;
            Collider[] colliders = ghoul.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i].gameObject != ghoul) return colliders[i].gameObject.layer;
            return ghoul.layer;
        }

        private static void ConfigureHitbox(Hitbox hitbox, PenitentAI ai, float multiplier, bool critical)
        {
            var data = new SerializedObject(hitbox);
            Reference(data, "targetEntityComponent", ai);
            Property(data, "damageMultiplier").floatValue = multiplier;
            Property(data, "isCriticalZone").boolValue = critical;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Socket(Transform parent, string name, Vector3 position)
        {
            Transform socket = Child(parent, name, parent.gameObject.layer);
            SocketFollow follow = Ensure<SocketFollow>(socket.gameObject);
            if (follow.Target == null) socket.localPosition = position;
            return socket;
        }

        private static void Chain(Transform parent, string name, Vector3 from, Vector3 to, Material material)
        {
            Vector3 delta = to - from;
            Transform chain = Primitive(parent, name, PrimitiveType.Cylinder, (from + to) * 0.5f,
                new Vector3(0.045f, delta.magnitude * 0.5f, 0.045f), material);
            chain.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        }

        private static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            Transform node = parent.Find(name);
            if (node == null)
            {
                GameObject created = GameObject.CreatePrimitive(type);
                created.name = name;
                created.layer = parent.gameObject.layer;
                created.transform.SetParent(parent, false);
                node = created.transform;
                Object.DestroyImmediate(created.GetComponent<Collider>());
            }
            node.localPosition = position;
            node.localRotation = Quaternion.identity;
            node.localScale = scale;
            MeshFilter mesh = Ensure<MeshFilter>(node.gameObject);
            if (mesh.sharedMesh == null)
            {
                GameObject template = GameObject.CreatePrimitive(type);
                try { mesh.sharedMesh = template.GetComponent<MeshFilter>().sharedMesh; }
                finally { Object.DestroyImmediate(template); }
            }
            Ensure<MeshRenderer>(node.gameObject).sharedMaterial = material;
            return node;
        }

        private static void RemoveLegacyPlaceholders(Transform visual)
        {
            string[] names = { "Body", "Head", "Hood", "Chain_L", "Chain_R", "ShieldPivot" };
            foreach (string name in names)
            {
                Transform old = visual.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
        }

        private static Transform Child(Transform parent, string name, int layer)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.layer = layer;
                return existing;
            }
            var node = new GameObject(name);
            node.layer = layer;
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private static T Ensure<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static SerializedProperty Property(SerializedObject data, string name)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property != null) return property;
            Debug.LogError("[PenitentBuilder] Missing serialized property '" + name + "' on " + data.targetObject.GetType().Name + ".");
            throw new InvalidOperationException("Missing Penitent builder property: " + name);
        }

        private static void Reference(SerializedObject data, string name, Object value) => Property(data, name).objectReferenceValue = value;

        private static void References(SerializedObject data, string name, Object[] values)
        {
            SerializedProperty array = Property(data, name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static Material MaterialAsset(string name, bool lit, Color color, Color emission, bool transparent = false)
        {
            string shaderName = lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Penitent requires shader: " + shaderName);
            string path = MaterialDirectory + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(shader) { name = name };
            else material.shader = shader;
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
            }
            if (lit)
            {
                material.SetFloat("_Metallic", name == "Mat_Penitent_Lid" ? 0.55f : 0f);
                material.SetFloat("_Smoothness", 0.2f);
            }
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
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
