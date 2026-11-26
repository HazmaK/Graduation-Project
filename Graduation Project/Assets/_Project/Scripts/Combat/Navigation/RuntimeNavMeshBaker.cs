using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Bakes collider navigation for a generated level hierarchy.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeNavMeshBaker : MonoBehaviour
    {
        [SerializeField, Tooltip("Hierarchy to bake; null uses this transform.")] private Transform root;
        [SerializeField, Tooltip("Bake automatically on Start.")] private bool bakeOnStart = false;
        [SerializeField, Tooltip("Layers included in addition to the ground mask.")] private LayerMask includeLayers = ~0;
        [SerializeField, Tooltip("Navigation agent type ID; zero is Humanoid.")] private int agentTypeId = 0;
        private NavMeshSurface surface;
        private bool hasData;

        /// <summary>Effective hierarchy root.</summary>
        public Transform Root => root != null ? root : transform;
        /// <summary>Whether this baker currently has registered navigation data.</summary>
        public bool HasData => hasData && surface != null && surface.navMeshData != null;
        /// <summary>Raised after the synchronous bake completes.</summary>
        public event Action<RuntimeNavMeshBaker> Baked;

        /// <summary>Bakes child physics colliders using the ground and include-layer masks.</summary>
        public void Bake()
        {
            GameObject rootObject = Root.gameObject;
            if (surface != null && surface.gameObject != rootObject)
                Clear();
            surface = rootObject.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = rootObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = includeLayers.value & GameLayers.GroundMask;
            surface.agentTypeID = agentTypeId;
            surface.BuildNavMesh();
            hasData = surface.navMeshData != null;
            Baked?.Invoke(this);
        }

        /// <summary>Changes the hierarchy root and bakes it.</summary>
        public void Bake(Transform newRoot)
        {
            root = newRoot;
            Bake();
        }

        /// <summary>Removes the surface's registered navigation data.</summary>
        public void Clear()
        {
            if (surface != null)
                surface.RemoveData();
            hasData = false;
        }

        private void Start()
        {
            if (bakeOnStart)
                Bake();
        }

        private void OnDestroy() => Clear();
    }
}
