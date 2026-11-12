using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Applies persisted settings before scene load and sets the target frame rate.
    /// </summary>
    public static class GameResolutionManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyStartupSettings()
        {
            Settings.GameSettings.EnsureInitialized();
            Application.targetFrameRate = 144;
        }
    }
}
