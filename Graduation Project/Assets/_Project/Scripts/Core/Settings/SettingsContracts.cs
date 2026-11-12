using System;

namespace RetroFPS.Settings
{
    /// <summary>Settings requests shared by menus and gameplay.</summary>
    public interface ISettingsService
    {
        /// <summary>Current live values, including a pending display preview.</summary>
        GameSettingsData Current { get; }
        /// <summary>Whether display confirmation is required.</summary>
        bool HasPendingDisplay { get; }
        /// <summary>Raised after a live settings change.</summary>
        event Action<GameSettingsData> Changed;
        /// <summary>Applies and stages persistence for one UX volume key.</summary>
        void SetVolume(string key, float value);
        /// <summary>Applies and stages persistence for normalized sensitivity.</summary>
        void SetSensitivity(float value);
        /// <summary>Applies and stages persistence for the accessibility preference.</summary>
        void SetReduceMotion(bool value);
        /// <summary>Previews display values without committing them.</summary>
        void RequestDisplay(WindowModeSetting mode, int width, int height);
        /// <summary>Commits the preview and flushes persistence.</summary>
        void ConfirmDisplay();
        /// <summary>Restores the last committed display values.</summary>
        void RevertDisplay();
        /// <summary>Flushes committed values; pending display values are excluded.</summary>
        void Save();
    }

    /// <summary>Persistence boundary for deterministic settings logic.</summary>
    public interface ISettingsStore
    {
        /// <summary>Loads saved values, using defaults for missing entries.</summary>
        GameSettingsData Load(GameSettingsData defaults);
        /// <summary>Stages a committed settings snapshot.</summary>
        void Write(GameSettingsData data);
        /// <summary>Flushes staged values to durable storage.</summary>
        void Save();
    }

    /// <summary>Platform display boundary.</summary>
    public interface IDisplayApplier
    {
        /// <summary>Requests a display mode and pixel size.</summary>
        void Apply(WindowModeSetting mode, int width, int height);
    }

    /// <summary>Audio output boundary, replaceable with a mixer adapter.</summary>
    public interface IAudioVolumeApplier
    {
        /// <summary>Applies audio gains from the snapshot.</summary>
        void Apply(GameSettingsData data);
    }
}
