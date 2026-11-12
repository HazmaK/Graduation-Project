using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Unscaled black overlay; loading completion is published exactly once per fade.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private MenuTheme theme;
        private readonly MenuTween tween = new MenuTween();
        private float from;
        private float to;
        private bool running;
        private bool loadingFade;
        private bool inputBlocked;

        /// <summary>Whether an overlay transition is still in progress.</summary>
        public bool IsFading => running;

        /// <summary>Fades to black for loading or from the current opacity for recovery/entry.</summary>
        public void Fade(bool toBlack, float seconds, bool notifyLoading = false)
        {
            from = overlay.alpha;
            to = toBlack ? 1f : 0f;
            loadingFade = notifyLoading;
            tween.Start(MenuTween.ResolveDuration(seconds, GameSettings.Current.ReduceMotion, true,
                theme.ReducedFadeSeconds), toBlack ? MenuEase.EaseIn : MenuEase.EaseOut);
            running = true;
            overlay.blocksRaycasts = true;
        }

        /// <summary>Blocks pointer events during a press lock as well as during fades.</summary>
        public void BlockInput(bool blocked)
        {
            inputBlocked = blocked;
            overlay.blocksRaycasts = blocked || running;
        }

        private void Update()
        {
            if (!running) return;
            tween.Tick(Time.unscaledDeltaTime);
            overlay.alpha = Mathf.LerpUnclamped(from, to, tween.Value);
            if (!tween.Complete) return;
            running = false;
            overlay.blocksRaycasts = inputBlocked;
            if (loadingFade)
            {
                loadingFade = false;
                MenuEvents.RaiseFadeOutComplete();
            }
        }
    }
}
