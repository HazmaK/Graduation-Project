using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Oyunun ekran çözünürlüğünü 1920x1080 Full HD olarak sabitleyen ve
    /// kenar boşlukları veya taşma olmadan tam ekran çalışmasını sağlayan yönetici.
    /// </summary>
    public static class GameResolutionManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Enforce1080pResolution()
        {
            // Oyun başlarken 1920x1080 Full HD ve kenarlıksız tam ekran moduna al
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
            Application.targetFrameRate = 144;
            Debug.Log("<color=cyan>[GameResolutionManager]</color> Çözünürlük 1920x1080 Full HD (FullScreenWindow) olarak kilitlendi.");
        }
    }
}
