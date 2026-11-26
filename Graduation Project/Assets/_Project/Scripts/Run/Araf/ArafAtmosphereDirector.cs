using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Applies Araf fog and blends awakening changes with an optional audio sting.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafAtmosphereDirector : MonoBehaviour
    {
        [SerializeField] private ArafLabyrinthDirector labyrinth;
        [SerializeField] private AudioSource stingSource;
        [SerializeField] private AudioClip awakeningSting;
        [SerializeField] private float fogLerpSeconds = 3f;

        private ArafLevel _level;
        private bool _inAraf;
        private bool _lerping;
        private int _awakening;
        private float _elapsed;
        private float _fromStart;
        private float _fromEnd;
        private float _toStart;
        private float _toEnd;

        private void Awake()
        {
            if (labyrinth == null) labyrinth = GetComponent<ArafLabyrinthDirector>();
            _level = labyrinth != null ? labyrinth.GetComponent<ArafLevel>() : GetComponent<ArafLevel>();
        }

        private void OnEnable()
        {
            RunEvents.OnEnteredAraf += HandleEntered;
            RunEvents.OnEscapedAraf += HandleEscaped;
            RunEvents.OnArafAwakeningChanged += HandleAwakening;
            if (RunEvents.CurrentRun != null && RunEvents.CurrentRun.Phase == RunPhase.InAraf) HandleEntered();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _inAraf = false;
            _lerping = false;
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            RunEvents.OnEnteredAraf -= HandleEntered;
            RunEvents.OnEscapedAraf -= HandleEscaped;
            RunEvents.OnArafAwakeningChanged -= HandleAwakening;
        }

        private void HandleEntered()
        {
            _inAraf = true;
            _lerping = false;
            _awakening = RunEvents.ArafAwakeningLevel;
            ArafTuning tuning = _level != null ? _level.Tuning : null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = tuning != null ? tuning.FogColor : new Color(0.10f, 0.05f, 0.16f);
            GetFog(tuning, _awakening, out _toStart, out _toEnd);
            RenderSettings.fogStartDistance = _toStart;
            RenderSettings.fogEndDistance = _toEnd;
        }

        private void HandleEscaped()
        {
            _inAraf = false;
            _lerping = false;
        }

        private void HandleAwakening(int level)
        {
            if (!_inAraf) return;
            if (level > _awakening && stingSource != null && awakeningSting != null)
                stingSource.PlayOneShot(awakeningSting);
            _awakening = level;
            _fromStart = RenderSettings.fogStartDistance;
            _fromEnd = RenderSettings.fogEndDistance;
            GetFog(_level != null ? _level.Tuning : null, level, out _toStart, out _toEnd);
            _elapsed = 0f;
            _lerping = true;
        }

        private static void GetFog(ArafTuning tuning, int level, out float start, out float end)
        {
            if (tuning != null)
            {
                start = tuning.GetFogStart(level);
                end = tuning.GetFogEnd(level);
                return;
            }
            int index = (uint)level < 4u ? level : 3;
            start = 20f - index * 4f;
            end = index == 3 ? 70f : 140f - index * 25f;
        }

        private void Update()
        {
            if (!_inAraf || !_lerping) return;
            _elapsed += Time.deltaTime;
            float t = fogLerpSeconds > 0f ? Mathf.Clamp01(_elapsed / fogLerpSeconds) : 1f;
            t = t * t * (3f - 2f * t);
            RenderSettings.fogStartDistance = Mathf.Lerp(_fromStart, _toStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fromEnd, _toEnd, t);
            if (t >= 1f) _lerping = false;
        }
    }
}
