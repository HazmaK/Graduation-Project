using System;
using System.Collections.Generic;
using RetroFPS.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RetroFPS.Editor
{
    /// <summary>Migrates the existing Flying Eye prefab in place without replacing its root.</summary>
    public static class FlyingEyeBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/Enemy_FlyingEye_Watcher.prefab";
        private const string MaterialDirectory = "Assets/_Project/Art/Enemies/FlyingEye/Placeholder";
        private static readonly string[] ArtNames =
        {
            "Body_Sclera", "Eye_Iris", "Pupil_Visual", "Horn_Top", "Horn_L", "Horn_R",
            "Tendril_Center", "Tendril_L", "Tendril_R"
        };

        /// <summary>Builds the art, hitbox, sockets and telegraph hierarchy in the existing prefab.</summary>
        [MenuItem("RetroFPS/Build Flying Eye")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build Flying Eye must run outside Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("The existing Flying Eye prefab is required.");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[FlyingEyeBuilder] Updated Flying Eye art, pupil hitbox, sockets, view and heavy telegraph in place.");
        }

        /// <summary>Applies the same migration to an Eye constructed by editor setup tools.</summary>
        public static void ConfigureRoot(GameObject root)
        {
            FlyingEyeAI ai = root.GetComponent<FlyingEyeAI>();
            if (ai == null) throw new InvalidOperationException("Flying Eye root requires FlyingEyeAI.");
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("Flying Eye requires the URP Unlit shader.");
            Material orbMaterial = MaterialAsset("Mat_Eye_ChargeOrb", shader);
            Material lineMaterial = MaterialAsset("Mat_Eye_AimLine", shader);

            Transform visual = Child(root.transform, "Visual");
            Transform hitboxes = Child(root.transform, "Hitboxes");
            Transform sockets = Child(root.transform, "Sockets");
            Transform telegraph = Child(root.transform, "Telegraph");
            Transform pupil = visual.Find("Pupil_Visual");
            if (pupil == null) pupil = root.transform.Find("Pupil_Head");
            Vector3 pupilPosition = pupil != null ? root.transform.InverseTransformPoint(pupil.position) : new Vector3(0f, 0f, 0.48f);
            Quaternion pupilRotation = pupil != null ? Quaternion.Inverse(root.transform.rotation) * pupil.rotation : Quaternion.identity;
            if (pupil != null) pupil.name = "Pupil_Visual";

            for (int i = 0; i < ArtNames.Length; i++)
            {
                Transform part = root.transform.Find(ArtNames[i]);
                if (part != null) part.SetParent(visual, false);
            }
            // Gameplay never depends on the replaceable model subtree.
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (MonoBehaviour script in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);

            Transform hitboxTransform = hitboxes.Find("Hitbox_Pupil");
            bool newHitbox = hitboxTransform == null;
            if (newHitbox) hitboxTransform = Child(hitboxes, "Hitbox_Pupil");
            if (newHitbox)
            {
                hitboxTransform.localPosition = pupilPosition;
                hitboxTransform.localRotation = pupilRotation;
            }
            hitboxTransform.localScale = Vector3.one;
            SphereCollider sphere = Ensure<SphereCollider>(hitboxTransform.gameObject);
            sphere.radius = 0.18f;
            sphere.center = Vector3.zero;
            sphere.isTrigger = false;
            Hitbox hitbox = Ensure<Hitbox>(hitboxTransform.gameObject);
            SerializedObject hitboxData = new SerializedObject(hitbox);
            hitboxData.FindProperty("targetEntityComponent").objectReferenceValue = ai;
            hitboxData.FindProperty("damageMultiplier").floatValue = 2f;
            hitboxData.FindProperty("isCriticalZone").boolValue = true;
            hitboxData.ApplyModifiedPropertiesWithoutUndo();

            Transform fire = sockets.Find("Socket_FirePoint");
            if (fire == null)
            {
                fire = root.transform.Find("FirePoint");
                if (fire != null)
                {
                    fire.name = "Socket_FirePoint";
                    fire.SetParent(sockets, false);
                }
                else fire = Child(sockets, "Socket_FirePoint");
            }
            Pose(fire, new Vector3(0f, 0f, 0.65f));
            Transform chargeSocket = Child(sockets, "Socket_Telegraph");
            Pose(chargeSocket, new Vector3(0f, 0f, 0.75f));
            Transform death = Child(sockets, "Socket_DeathFx");
            Pose(death, Vector3.zero);

            Transform orb = telegraph.Find("ChargeOrb");
            if (orb == null)
            {
                GameObject orbObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orbObject.name = "ChargeOrb";
                orb = orbObject.transform;
                orb.SetParent(telegraph, false);
            }
            foreach (Collider collider in orb.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            orb.localPosition = chargeSocket.localPosition;
            orb.localRotation = Quaternion.identity;
            orb.localScale = Vector3.zero;
            Renderer orbRenderer = orb.GetComponent<Renderer>();
            orbRenderer.sharedMaterial = orbMaterial;
            orb.gameObject.SetActive(false);

            Transform lineTransform = Child(telegraph, "AimLine");
            LineRenderer line = Ensure<LineRenderer>(lineTransform.gameObject);
            line.sharedMaterial = lineMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = 1f;
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.zero);
            line.enabled = false;

            FlyingEyeView view = Ensure<FlyingEyeView>(root);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            var glow = new List<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                string name = renderers[i].name;
                if (name.IndexOf("iris", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("pupil", StringComparison.OrdinalIgnoreCase) >= 0) glow.Add(renderers[i]);
            }
            SerializedObject data = new SerializedObject(view);
            Reference(data, "visualRoot", visual);
            References(data, "tintRenderers", renderers);
            References(data, "glowRenderers", glow.ToArray());
            Reference(data, "fireSocket", fire);
            Reference(data, "telegraphSocket", chargeSocket);
            Reference(data, "deathFxSocket", death);
            Reference(data, "chargeOrb", orb);
            Reference(data, "chargeOrbRenderer", orbRenderer);
            Reference(data, "aimLine", line);
            data.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject aiData = new SerializedObject(ai);
            Reference(aiData, "view", view);
            Reference(aiData, "firePoint", fire);
            aiData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Reference(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;

        private static void References(SerializedObject data, string name, Renderer[] values)
        {
            SerializedProperty array = data.FindProperty(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static T Ensure<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            var node = new GameObject(name);
            node.layer = parent.gameObject.layer;
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private static void Pose(Transform target, Vector3 position)
        {
            target.localPosition = position;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static Material MaterialAsset(string name, Shader shader)
        {
            string path = MaterialDirectory + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            EnsureFolder(MaterialDirectory);
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
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
