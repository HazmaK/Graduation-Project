using System;

namespace RetroFPS.UI.Menu
{
    /// <summary>Easing curves for menu transitions.</summary>
    public enum MenuEase { Linear, EaseIn, EaseOut, EaseInOut }

    /// <summary>Allocation-free normalized tween driven by caller-supplied seconds.</summary>
    public sealed class MenuTween
    {
        private float elapsed;
        private float duration;
        private MenuEase ease;
        /// <summary>Gets the eased progress.</summary>
        public float Value => Evaluate(duration <= 0f ? 1f : Math.Min(1f, elapsed / duration), ease);
        /// <summary>Gets whether the tween has finished.</summary>
        public bool Complete => elapsed >= duration;
        /// <summary>Starts or restarts a tween.</summary>
        public void Start(float seconds, MenuEase curve = MenuEase.Linear)
        {
            duration = Math.Max(0f, seconds);
            elapsed = 0f;
            ease = curve;
        }
        /// <summary>Advances using a nonnegative time delta.</summary>
        public void Tick(float deltaSeconds) => elapsed = Math.Min(duration, elapsed + Math.Max(0f, deltaSeconds));
        /// <summary>Resolves fades to at most 100 ms and other reduced-motion transitions to instant.</summary>
        public static float ResolveDuration(float seconds, bool reduceMotion, bool isFade, float reducedFadeSeconds = 0.1f)
            => reduceMotion ? (isFade ? Math.Min(0.1f, Math.Max(0f, reducedFadeSeconds)) : 0f) : Math.Max(0f, seconds);
        /// <summary>Evaluates a clamped normalized progress value.</summary>
        public static float Evaluate(float progress, MenuEase curve)
        {
            float t = Math.Max(0f, Math.Min(1f, progress));
            switch (curve)
            {
                case MenuEase.EaseIn: return t * t;
                case MenuEase.EaseOut: return 1f - (1f - t) * (1f - t);
                case MenuEase.EaseInOut: return t * t * (3f - 2f * t);
                default: return t;
            }
        }
    }
}
