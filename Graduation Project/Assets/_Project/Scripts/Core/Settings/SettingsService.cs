using System;
using System.Collections.Generic;

namespace RetroFPS.Settings
{
    /// <summary>Engine-independent settings application and display transaction logic.</summary>
    public sealed class SettingsService : ISettingsService
    {
        private readonly ISettingsStore store;
        private readonly IDisplayApplier display;
        private readonly IAudioVolumeApplier audio;
        private readonly GameSettingsData defaults;
        private GameSettingsData current;
        private GameSettingsData committed;
        private bool hasPendingDisplay;

        /// <inheritdoc/>
        public GameSettingsData Current => current;
        /// <inheritdoc/>
        public bool HasPendingDisplay => hasPendingDisplay;
        /// <inheritdoc/>
        public event Action<GameSettingsData> Changed;

        /// <summary>Loads, validates and applies saved values. Optional options repair unavailable saved sizes.</summary>
        public SettingsService(ISettingsStore store, IDisplayApplier display,
            IAudioVolumeApplier audio, GameSettingsData defaults,
            IReadOnlyList<ResolutionOption> resolutions = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.display = display ?? throw new ArgumentNullException(nameof(display));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.defaults = defaults.Clamp(GameSettingsData.Defaults());
            current = store.Load(this.defaults).Clamp(this.defaults);
            // A first-boot desktop size need not appear in Screen.resolutions.
            if (resolutions != null && resolutions.Count > 0 &&
                (current.ResolutionWidth != this.defaults.ResolutionWidth ||
                 current.ResolutionHeight != this.defaults.ResolutionHeight))
            {
                ResolutionOption nearest = resolutions[ResolutionOptions.FindNearestIndex(
                    resolutions, current.ResolutionWidth, current.ResolutionHeight)];
                current = current.WithDisplay(current.WindowMode, nearest.Width, nearest.Height);
            }
            committed = current;
            display.Apply(current.WindowMode, current.ResolutionWidth, current.ResolutionHeight);
            audio.Apply(current);
        }

        /// <inheritdoc/>
        public void SetVolume(string key, float value)
        {
            float master = current.MasterVolume, music = current.MusicVolume;
            float sfx = current.SfxVolume, ui = current.UiVolume;
            switch (key)
            {
                case "masterVolume": master = GameSettingsData.Unit(value, defaults.MasterVolume); break;
                case "musicVolume": music = GameSettingsData.Unit(value, defaults.MusicVolume); break;
                case "sfxVolume": sfx = GameSettingsData.Unit(value, defaults.SfxVolume); break;
                case "uiVolume": ui = GameSettingsData.Unit(value, defaults.UiVolume); break;
                default: throw new ArgumentException("Unknown volume key.", nameof(key));
            }
            current = new GameSettingsData(master, music, sfx, ui, current.MouseSensitivity,
                current.WindowMode, current.ResolutionWidth, current.ResolutionHeight, current.ReduceMotion);
            audio.Apply(current);
            PersistNonDisplay();
        }

        /// <inheritdoc/>
        public void SetSensitivity(float value)
        {
            current = new GameSettingsData(current.MasterVolume, current.MusicVolume,
                current.SfxVolume, current.UiVolume, GameSettingsData.Unit(value, defaults.MouseSensitivity),
                current.WindowMode, current.ResolutionWidth, current.ResolutionHeight, current.ReduceMotion);
            PersistNonDisplay();
        }

        /// <inheritdoc/>
        public void SetReduceMotion(bool value)
        {
            current = new GameSettingsData(current.MasterVolume, current.MusicVolume,
                current.SfxVolume, current.UiVolume, current.MouseSensitivity,
                current.WindowMode, current.ResolutionWidth, current.ResolutionHeight, value);
            PersistNonDisplay();
        }

        /// <inheritdoc/>
        public void RequestDisplay(WindowModeSetting mode, int width, int height)
        {
            if (!GameSettingsData.ValidMode(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            display.Apply(mode, width, height);
            current = current.WithDisplay(mode, width, height);
            hasPendingDisplay = true;
            Changed?.Invoke(current);
        }

        /// <inheritdoc/>
        public void ConfirmDisplay()
        {
            if (!hasPendingDisplay) return;
            committed = current;
            hasPendingDisplay = false;
            Save();
            Changed?.Invoke(current);
        }

        /// <inheritdoc/>
        public void RevertDisplay()
        {
            if (!hasPendingDisplay) return;
            display.Apply(committed.WindowMode, committed.ResolutionWidth, committed.ResolutionHeight);
            current = current.WithDisplay(committed.WindowMode, committed.ResolutionWidth, committed.ResolutionHeight);
            hasPendingDisplay = false;
            Changed?.Invoke(current);
        }

        /// <inheritdoc/>
        public void Save()
        {
            store.Write(committed);
            store.Save();
        }

        private void PersistNonDisplay()
        {
            committed = current.WithDisplay(committed.WindowMode, committed.ResolutionWidth, committed.ResolutionHeight);
            store.Write(committed);
            Changed?.Invoke(current);
        }
    }
}
