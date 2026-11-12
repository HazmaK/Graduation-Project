using System;
using System.Collections.Generic;

namespace RetroFPS.Settings
{
    /// <summary>A refresh-rate-independent display size.</summary>
    public readonly struct ResolutionOption : IEquatable<ResolutionOption>
    {
        /// <summary>Pixel width.</summary>
        public int Width { get; }
        /// <summary>Pixel height.</summary>
        public int Height { get; }
        /// <summary>Creates a display size.</summary>
        public ResolutionOption(int width, int height) { Width = width; Height = height; }
        /// <inheritdoc/>
        public bool Equals(ResolutionOption other) => Width == other.Width && Height == other.Height;
        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ResolutionOption other && Equals(other);
        /// <inheritdoc/>
        public override int GetHashCode() => unchecked(Width * 397 ^ Height);
    }

    /// <summary>Deterministic resolution filtering and nearest-size selection.</summary>
    public static class ResolutionOptions
    {
        /// <summary>Deduplicates sizes, filters below 1280x720 and sorts width then height.
        /// If none survive, returns the desktop fallback even on a smaller desktop.</summary>
        public static ResolutionOption[] Build(IEnumerable<ResolutionOption> supported, ResolutionOption desktop)
        {
            if (supported == null) throw new ArgumentNullException(nameof(supported));
            var unique = new HashSet<ResolutionOption>();
            foreach (ResolutionOption size in supported)
                if (size.Width >= 1280 && size.Height >= 720) unique.Add(size);
            var result = new List<ResolutionOption>(unique);
            result.Sort((a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));
            if (result.Count == 0)
                result.Add(desktop.Width > 0 && desktop.Height > 0 ? desktop : new ResolutionOption(1920, 1080));
            return result.ToArray();
        }

        /// <summary>Finds the closest size by squared pixel distance; ties choose the first entry.
        /// Returns -1 for an empty list.</summary>
        public static int FindNearestIndex(IReadOnlyList<ResolutionOption> options, int width, int height)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            for (int i = 0; i < options.Count; i++)
            {
                double dx = (double)options[i].Width - width, dy = (double)options[i].Height - height;
                double distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        /// <summary>Selects saved size if present, otherwise desktop size, using nearest match.</summary>
        public static int FindSelectedIndex(IReadOnlyList<ResolutionOption> options,
            int savedWidth, int savedHeight, ResolutionOption desktop)
        {
            return savedWidth > 0 && savedHeight > 0
                ? FindNearestIndex(options, savedWidth, savedHeight)
                : FindNearestIndex(options, desktop.Width, desktop.Height);
        }
    }
}
