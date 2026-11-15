using RetroFPS.UI.HUD;
using UnityEngine;

namespace RetroFPS.Pause
{
    /// <summary>Player prefab composition root; Gameplay knows only the Core request sink.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-11000)]
    public sealed class PauseBindings : MonoBehaviour
    {
        [SerializeField] private PauseRuntime runtime;
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private HudEventSystemGuard eventSystemGuard;
        [SerializeField] private TrueDeathView trueDeath;
        private bool deathOpen;
        private void Start() => pauseMenu.Bind(runtime, eventSystemGuard);
        private void OnEnable()
        {
            runtime.Bind(health, pauseMenu);
            deathOpen = trueDeath != null && trueDeath.IsOpen;
            runtime.SetTrueDeathOpen(deathOpen);
        }
        private void OnDisable() { if (runtime != null) runtime.Unbind(pauseMenu); }
        private void Update()
        {
            // Sample a latched death card before PauseRuntime polls a fresh toggle this frame.
            bool open = trueDeath != null && trueDeath.IsOpen;
            if (deathOpen == open) return;
            deathOpen = open;
            runtime.SetTrueDeathOpen(open);
        }
    }
}
