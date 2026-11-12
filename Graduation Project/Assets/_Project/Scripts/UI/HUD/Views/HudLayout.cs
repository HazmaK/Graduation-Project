using RetroFPS.Presentation;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>Keeps corner anchors on the intersection of the device safe area and the authored inset.</summary>
    public sealed class HudLayout : MonoBehaviour
    {
        [SerializeField] private HudConfig config;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform hudRoot;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform deathContent;
        private int width = -1, height = -1;
        private Rect previousSafe;
        private float previousScale = -1f, previousInset = -1f;

        private void LateUpdate()
        {
            if (config == null || canvasRect == null || hudRoot == null || safeArea == null) return;
            Rect screenSafe = Screen.safeArea;
            float scale = Mathf.Clamp(config.HudScale, 0.75f, 1.5f);
            float inset = config.SafeInsetPercent * 0.01f;
            if (width == Screen.width && height == Screen.height && previousSafe == screenSafe &&
                previousScale == scale && previousInset == inset) return;
            width = Screen.width; height = Screen.height; previousSafe = screenSafe;
            previousScale = scale; previousInset = inset;
            if (width <= 0 || height <= 0) return;
            Vector2 size = canvasRect.rect.size / scale;
            hudRoot.anchorMin = hudRoot.anchorMax = hudRoot.pivot = Vector2.one * 0.5f;
            hudRoot.anchoredPosition = Vector2.zero;
            hudRoot.sizeDelta = size;
            hudRoot.localScale = Vector3.one * scale;
            float left = Mathf.Max(screenSafe.xMin, width * inset) / width;
            float bottom = Mathf.Max(screenSafe.yMin, height * inset) / height;
            float right = Mathf.Min(screenSafe.xMax, width * (1f - inset)) / width;
            float top = Mathf.Min(screenSafe.yMax, height * (1f - inset)) / height;
            Vector2 minimum = safeArea.anchorMin; minimum.x = left; minimum.y = bottom;
            Vector2 maximum = safeArea.anchorMax; maximum.x = Mathf.Max(left, right); maximum.y = Mathf.Max(bottom, top);
            safeArea.anchorMin = minimum; safeArea.anchorMax = maximum;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            if (deathContent != null) deathContent.localScale = Vector3.one * scale;
        }
    }
}
