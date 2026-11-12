using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Slow unscaled fog, glow and particles, frozen as a populated still under reduce motion.</summary>
    public sealed class MenuAmbientMotion : MonoBehaviour
    {
        [SerializeField] private MenuTheme theme;
        [SerializeField] private UnityEngine.UI.RawImage fog;
        [SerializeField] private UnityEngine.UI.Image[] torchGlows;
        [SerializeField] private ParticleSystem[] particles;
        private bool reduced;
        private float elapsed;

        private void OnEnable() => GameSettings.Changed += Apply;
        private void OnDisable() => GameSettings.Changed -= Apply;
        private void Start()
        {
            for (int i = 0; i < particles.Length; i++)
                particles[i].Simulate(particles[i].main.duration, true, true, true);
            Apply(GameSettings.Current);
        }
        private void Apply(GameSettingsData data)
        {
            reduced = data.ReduceMotion;
            for (int i = 0; i < particles.Length; i++)
                if (reduced) particles[i].Pause(true); else particles[i].Play(true);
            if (reduced) SetGlow(theme.ReducedTorchAlpha);
        }
        private void Update()
        {
            if (reduced) return;
            elapsed += Time.unscaledDeltaTime;
            Rect uv = fog.uvRect;
            uv.x = Mathf.Repeat(uv.x + theme.FogScrollSpeed * Time.unscaledDeltaTime, 1f);
            fog.uvRect = uv;
            SetGlow(Mathf.Lerp(theme.TorchAlphaMin, theme.TorchAlphaMax,
                Mathf.PerlinNoise(elapsed * theme.TorchFrequency, 0.37f)));
        }
        private void SetGlow(float alpha)
        {
            for (int i = 0; i < torchGlows.Length; i++)
            {
                Color color = torchGlows[i].color;
                color.a = alpha;
                torchGlows[i].color = color;
            }
        }
    }
}
