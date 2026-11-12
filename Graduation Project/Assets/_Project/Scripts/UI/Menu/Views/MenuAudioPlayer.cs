using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Semantic sound receiver; unassigned placeholder clips deliberately remain silent.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MenuAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private MenuTheme theme;
        [SerializeField] private AudioClip[] clips;
        private UiAudioThrottle throttle;

        private void Awake() => throttle = new UiAudioThrottle(theme.AudioThrottleSeconds);
        private void OnEnable()
        {
            MenuEvents.UiAudio += Play;
            GameSettings.Changed += Apply;
            Apply(GameSettings.Current);
        }
        private void OnDisable()
        {
            MenuEvents.UiAudio -= Play;
            GameSettings.Changed -= Apply;
        }
        private void Update() => throttle.Tick(Time.unscaledDeltaTime);
        private void Apply(GameSettingsData data) => source.volume = data.UiVolume;
        private void Play(UiAudioEventType type)
        {
            if (!throttle.TryPlay(type)) return;
            int index = (int)type;
            if (index >= 0 && index < clips.Length && clips[index] != null) source.PlayOneShot(clips[index]);
        }
    }
}
