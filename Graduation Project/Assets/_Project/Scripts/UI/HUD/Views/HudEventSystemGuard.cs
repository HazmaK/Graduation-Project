using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace RetroFPS.UI.HUD
{
    /// <summary>Yields the bundled system to a scene-owned system without disabling unrelated input.</summary>
    public sealed class HudEventSystemGuard : MonoBehaviour
    {
        [SerializeField] private EventSystem bundledSystem;
        [SerializeField] private InputSystemUIInputModule bundledModule;
        private EventSystem activeSystem;
        private bool refresh, warned;
        /// <summary>Gets the current system used for death-button focus.</summary>
        public EventSystem ActiveSystem => activeSystem != null && activeSystem.isActiveAndEnabled ? activeSystem : EventSystem.current;
        /// <summary>Gets the authored system for allocating focus event data before combat.</summary>
        public EventSystem BundledSystem => bundledSystem;

        private void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; SceneManager.sceneUnloaded += SceneUnloaded; refresh = true; }
        private void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneUnloaded -= SceneUnloaded; }
        private void Start() => Resolve();
        private void SceneLoaded(Scene scene, LoadSceneMode mode) => refresh = true;
        private void SceneUnloaded(Scene scene) => refresh = true;
        private void LateUpdate() { if (refresh || activeSystem == null || !activeSystem.isActiveAndEnabled) Resolve(); }

        private void Resolve()
        {
            refresh = false;
            if (bundledSystem == null || bundledModule == null) return;
            // EventSystem.current exposes its enabled registry. Temporarily remove only our own entry.
            bundledSystem.enabled = false;
            EventSystem existing = EventSystem.current;
            if (existing != null && existing != bundledSystem)
            {
                bundledModule.enabled = false; activeSystem = existing;
                if (!warned && existing.currentInputModule != null && !(existing.currentInputModule is InputSystemUIInputModule))
                {
                    warned = true;
                    Debug.LogWarning("[CombatHud] Existing EventSystem does not use the New Input System.", this);
                }
                if (existing.currentInputModule is InputSystemUIInputModule module) module.deselectOnBackgroundClick = false;
            }
            else
            {
                bundledModule.enabled = true; bundledSystem.enabled = true; activeSystem = bundledSystem;
                bundledModule.deselectOnBackgroundClick = false;
            }
        }
    }
}
