using System;

namespace RetroFPS.UI.HUD
{
    /// <summary>Semantic banner kinds; localized text is cached by the view before combat.</summary>
    public enum HudBannerKind
    {
        /// <summary>No banner.</summary>
        None,
        /// <summary>The arena encounter has ended; highest priority.</summary>
        ArenaCleared,
        /// <summary>The arena has sealed.</summary>
        ArenaSealed,
        /// <summary>A one-based wave has started.</summary>
        Wave,
        /// <summary>A one-based wave has ended.</summary>
        WaveCleared
    }

    /// <summary>Immutable one- or two-line banner identity, without formatted strings.</summary>
    public readonly struct HudBanner
    {
        internal HudBanner(HudBannerKind kind, int wave, double receivedAt,
            HudBannerKind secondKind = HudBannerKind.None, int secondWave = 0)
        {
            Kind = kind;
            Wave = wave;
            ReceivedAt = receivedAt;
            SecondKind = secondKind;
            SecondWave = secondWave;
        }
        /// <summary>Gets the first line's semantic kind.</summary>
        public HudBannerKind Kind { get; }
        /// <summary>Gets the first line's one-based wave, or zero for arena messages.</summary>
        public int Wave { get; }
        /// <summary>Gets the second line's kind, or None.</summary>
        public HudBannerKind SecondKind { get; }
        /// <summary>Gets the second line's one-based wave.</summary>
        public int SecondWave { get; }
        /// <summary>Gets whether this banner needs the two-line plate.</summary>
        public bool IsMerged => SecondKind != HudBannerKind.None;
        /// <summary>Gets priority (1 highest, 3 lowest; zero for None).</summary>
        public int Priority => Kind == HudBannerKind.None ? 0 : Kind == HudBannerKind.ArenaCleared ? 1
            : Kind == HudBannerKind.WaveCleared && !IsMerged ? 3 : 2;
        internal double ReceivedAt { get; }
    }

    /// <summary>Fixed current/pending banner slots with absolute unscaled timing and encounter-scoped deduplication.</summary>
    public sealed class HudBannerQueue
    {
        private readonly double arenaHold;
        private readonly double normalHold;
        private readonly double fadeIn;
        private readonly double fadeOut;
        private readonly double mergeWindow;
        private double expiresAt;
        private bool sealedSeen;
        private bool arenaClearedSeen;
        private int lastWave = -1;
        private int lastClearedWave = -1;

        /// <summary>Creates fixed storage; caller may supply HudConfig timings in seconds.</summary>
        public HudBannerQueue(double arenaHoldSeconds = 2.2d, double normalHoldSeconds = 1.8d,
            double fadeInSeconds = 0.15d, double fadeOutSeconds = 0.3d, double mergeWindowSeconds = 0.5d)
        {
            arenaHold = Math.Max(0d, arenaHoldSeconds);
            normalHold = Math.Max(0d, normalHoldSeconds);
            fadeIn = Math.Max(0d, fadeInSeconds);
            fadeOut = Math.Max(0d, fadeOutSeconds);
            mergeWindow = Math.Max(0d, mergeWindowSeconds);
        }
        /// <summary>Gets the active immutable banner identity.</summary>
        public HudBanner Current { get; private set; }
        /// <summary>Gets the sole waiting identity; a newer waiting event replaces it.</summary>
        public HudBanner Pending { get; private set; }
        /// <summary>Gets whether an active banner exists.</summary>
        public bool HasCurrent => Current.Kind != HudBannerKind.None;
        /// <summary>Gets whether a waiting banner exists.</summary>
        public bool HasPending => Pending.Kind != HudBannerKind.None;
        /// <summary>Gets the absolute end of the current fade-out, or zero after reset.</summary>
        public double ExpiresAt => expiresAt;

        /// <summary>Accepts an event at a monotonic unscaled timestamp; wave payloads are already one-based.</summary>
        public bool Enqueue(HudBannerKind kind, int wave, double unscaledSeconds)
        {
            if (double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds)) return false;
            bool changed = Tick(unscaledSeconds);
            if (kind == HudBannerKind.None || Seen(kind, wave)) return changed;
            Remember(kind, wave);
            var incoming = new HudBanner(kind, wave, unscaledSeconds);
            if (!HasCurrent)
            {
                Start(incoming, unscaledSeconds);
                return true;
            }
            if (TryMerge(Current, incoming, out HudBanner merged))
            {
                Current = merged;
                return true;
            }
            if (incoming.Priority < Current.Priority ||
                (Current.Kind != HudBannerKind.ArenaCleared && expiresAt - unscaledSeconds < mergeWindow))
            {
                // Smaller priority values are more important. Arena clear supersedes every wave.
                int pendingWave = Math.Max(Pending.Wave, Pending.SecondWave);
                if (HasPending && Pending.Kind != HudBannerKind.ArenaCleared &&
                    Pending.Priority >= incoming.Priority &&
                    (incoming.Kind == HudBannerKind.ArenaCleared || pendingWave < incoming.Wave))
                    Pending = default;
                Start(incoming, unscaledSeconds);
                return true;
            }
            if (TryMerge(Pending, incoming, out merged)) Pending = merged;
            else Pending = incoming;
            return true;
        }

        /// <summary>Expires current content and promotes at most one pending slot, without allocating.</summary>
        public bool Tick(double unscaledSeconds)
        {
            if (double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds)) return false;
            if (!HasCurrent || unscaledSeconds < expiresAt) return false;
            double nextStart = expiresAt;
            Current = default;
            if (HasPending)
            {
                HudBanner next = Pending;
                Pending = default;
                Start(next, nextStart);
                if (unscaledSeconds >= expiresAt) Current = default;
            }
            if (!HasCurrent) expiresAt = 0d;
            return true;
        }

        /// <summary>Clears both slots and deduplication on encounter retry, true death, Araf entry or scene change.</summary>
        public void Reset()
        {
            Current = default;
            Pending = default;
            expiresAt = 0d;
            sealedSeen = false;
            arenaClearedSeen = false;
            lastWave = -1;
            lastClearedWave = -1;
        }

        private void Start(HudBanner banner, double now)
        {
            Current = banner;
            expiresAt = now + fadeIn + (banner.Kind == HudBannerKind.ArenaCleared ? arenaHold : normalHold) + fadeOut;
        }

        private bool TryMerge(HudBanner first, HudBanner second, out HudBanner merged)
        {
            bool sealedWave = first.Kind == HudBannerKind.ArenaSealed && second.Kind == HudBannerKind.Wave
                && second.ReceivedAt - first.ReceivedAt >= 0d && second.ReceivedAt - first.ReceivedAt <= mergeWindow;
            bool nextWave = first.Kind == HudBannerKind.WaveCleared && second.Kind == HudBannerKind.Wave
                && first.Wave < int.MaxValue && second.Wave == first.Wave + 1;
            if (!first.IsMerged && (sealedWave || nextWave))
            {
                merged = new HudBanner(first.Kind, first.Wave, first.ReceivedAt, second.Kind, second.Wave);
                return true;
            }
            merged = default;
            return false;
        }

        private bool Seen(HudBannerKind kind, int wave)
        {
            switch (kind)
            {
                case HudBannerKind.ArenaSealed: return sealedSeen;
                case HudBannerKind.ArenaCleared: return arenaClearedSeen;
                case HudBannerKind.Wave: return wave <= lastWave;
                case HudBannerKind.WaveCleared: return wave <= lastClearedWave;
                default: return true;
            }
        }

        private void Remember(HudBannerKind kind, int wave)
        {
            switch (kind)
            {
                case HudBannerKind.ArenaSealed: sealedSeen = true; break;
                case HudBannerKind.ArenaCleared: arenaClearedSeen = true; break;
                case HudBannerKind.Wave: lastWave = wave; break;
                case HudBannerKind.WaveCleared: lastClearedWave = wave; break;
            }
        }
    }
}
