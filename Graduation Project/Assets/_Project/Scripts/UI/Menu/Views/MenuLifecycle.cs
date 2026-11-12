using System;
using RetroFPS.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroFPS.UI.Menu
{
    /// <summary>Owns preload/activation and application quit; UI views only publish requests.</summary>
    public sealed class MenuLifecycle : MonoBehaviour
    {
        private AsyncOperation loading;
        private bool requested;
        private bool fadeComplete;
        private bool quitting;

        private void OnEnable()
        {
            MenuEvents.StartRequested += Load;
            MenuEvents.FadeOutComplete += Activate;
            MenuEvents.QuitConfirmed += Quit;
        }
        private void OnDisable()
        {
            MenuEvents.StartRequested -= Load;
            MenuEvents.FadeOutComplete -= Activate;
            MenuEvents.QuitConfirmed -= Quit;
        }
        private void Load(string sceneName)
        {
            if (requested) return;
            requested = true;
            fadeComplete = false;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName)) { Fail(); return; }
                loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (loading == null) { Fail(); return; }
                loading.allowSceneActivation = fadeComplete;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Fail();
            }
        }
        private void Activate()
        {
            if (!requested) return;
            fadeComplete = true;
            if (loading != null) loading.allowSceneActivation = true;
        }
        private void Fail()
        {
            requested = false;
            loading = null;
            MenuEvents.RaiseLoadFailed();
        }
        private void Quit()
        {
            if (quitting) return;
            quitting = true;
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
