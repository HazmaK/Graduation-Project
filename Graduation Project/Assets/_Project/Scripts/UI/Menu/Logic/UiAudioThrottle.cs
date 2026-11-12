using System;

namespace RetroFPS.UI.Menu
{
    /// <summary>Independent event cooldowns; storage is allocated only at construction.</summary>
    public sealed class UiAudioThrottle
    {
        private readonly double[] remaining = new double[(int)UiAudioEventType.Error + 1];
        private readonly float intervalSeconds;
        /// <summary>Creates a throttle with the P8 50 ms default.</summary>
        public UiAudioThrottle(float intervalSeconds = 0.05f) => this.intervalSeconds = Math.Max(0f, intervalSeconds);
        /// <summary>Advances all cooldowns using caller-supplied seconds.</summary>
        public void Tick(float deltaSeconds)
        {
            double dt = Math.Max(0f, deltaSeconds);
            for (int i = 0; i < remaining.Length; i++) remaining[i] = Math.Max(0d, remaining[i] - dt);
        }
        /// <summary>Consumes an event's playback slot if its cooldown has elapsed.</summary>
        public bool TryPlay(UiAudioEventType type)
        {
            int index = (int)type;
            if (index < 0 || index >= remaining.Length || remaining[index] > 0d) return false;
            remaining[index] = intervalSeconds;
            return true;
        }
    }
}
