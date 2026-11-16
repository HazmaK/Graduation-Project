using System.Collections.Generic;
using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>Persistent pool of Eye orbs sharing exactly one orb and one trail material.</summary>
    public static class EyeProjectilePool
    {
        private static readonly List<EnemyProjectile> Orbs = new List<EnemyProjectile>(128);
        private static GameObject _root;
        private static Material _orbMaterial;
        private static Material _trailMaterial;

        internal static Material SharedTrailMaterial
        {
            get
            {
                EnsureMaterials();
                return _trailMaterial;
            }
        }

        /// <summary>Takes or creates an orb, applies launch settings and activates it without reuse allocations.</summary>
        public static EnemyProjectile Launch(Transform shooter, Vector3 position, Vector3 direction,
            in EnemyProjectileSettings settings)
        {
            EnsureRoot();
            EnemyProjectile orb = null;
            for (int i = Orbs.Count - 1; i >= 0; i--)
            {
                EnemyProjectile candidate = Orbs[i];
                if (candidate == null)
                {
                    Orbs.RemoveAt(i);
                    continue;
                }
                if (!candidate.gameObject.activeSelf)
                {
                    orb = candidate;
                    break;
                }
            }
            if (orb == null) orb = CreateOrb();
            orb.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(direction.sqrMagnitude > 0f ? direction : Vector3.forward));
            orb.InitializePooled(shooter, in settings);
            orb.gameObject.SetActive(true);
            return orb;
        }

        /// <summary>Ensures at least count inactive orbs exist, warming launch caches during initialization.</summary>
        public static void Prewarm(int count)
        {
            EnsureRoot();
            int inactive = 0;
            for (int i = Orbs.Count - 1; i >= 0; i--)
            {
                if (Orbs[i] == null) Orbs.RemoveAt(i);
                else if (!Orbs[i].gameObject.activeSelf) inactive++;
            }
            int additional = Mathf.Max(0, count - inactive);
            int capacity = Orbs.Count + additional;
            if (Orbs.Capacity < capacity) Orbs.Capacity = capacity;
            EnemyProjectile.WarmLaunchCaches(capacity + EnemyProjectile.ActiveProjectiles.Count);
            for (int i = 0; i < additional; i++) CreateOrb();
        }

        private static EnemyProjectile CreateOrb()
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.SetActive(false);
            orb.name = "Flying Eye Orb";
            orb.transform.SetParent(_root.transform, false);
            orb.layer = GameLayers.Projectile;
            orb.GetComponent<SphereCollider>().isTrigger = true;
            Renderer renderer = orb.GetComponent<Renderer>();
            renderer.sharedMaterial = _orbMaterial;
            TrailRenderer trail = orb.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _trailMaterial;
            EnemyProjectile projectile = orb.AddComponent<EnemyProjectile>();
            projectile.PrepareForPool(renderer, trail);
            Orbs.Add(projectile);
            return projectile;
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;
            EnsureMaterials();
            _root = new GameObject("Eye Projectile Pool");
            _root.hideFlags = HideFlags.HideInHierarchy;
            if (Application.isPlaying) Object.DontDestroyOnLoad(_root);
        }

        private static void EnsureMaterials()
        {
            if (_orbMaterial != null && _trailMaterial != null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            if (_orbMaterial == null) _orbMaterial = new Material(shader) { name = "Eye Orb (Shared)" };
            if (_trailMaterial == null)
            {
                _trailMaterial = new Material(shader) { name = "Eye Trail (Shared)" };
                // The particle shader preserves trail vertex colours and alpha.
                Shader trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (trailShader != null) _trailMaterial.shader = trailShader;
                if (_trailMaterial.HasProperty("_Surface")) _trailMaterial.SetFloat("_Surface", 1f);
                if (_trailMaterial.HasProperty("_SrcBlend")) _trailMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (_trailMaterial.HasProperty("_DstBlend")) _trailMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (_trailMaterial.HasProperty("_ZWrite")) _trailMaterial.SetFloat("_ZWrite", 0f);
                _trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _trailMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Also handles entering Play Mode with domain and scene reload disabled.
            if (_root != null) _root.SetActive(false);
            DestroyResource(_root);
            DestroyResource(_orbMaterial);
            DestroyResource(_trailMaterial);
            _root = null;
            _orbMaterial = null;
            _trailMaterial = null;
            Orbs.Clear();
        }

        private static void DestroyResource(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Object.Destroy(resource);
            else Object.DestroyImmediate(resource);
        }
    }
}
