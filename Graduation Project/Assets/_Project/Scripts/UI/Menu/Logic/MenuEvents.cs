using System;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Semantic UI sounds; playback belongs to a receiver.</summary>
    public enum UiAudioEventType { Focus, Confirm, Back, SliderStep, VolumePreview, DialogOpen, StartRun, Error }

    /// <summary>Menu integration signals, cleared when a new play session starts.</summary>
    public static class MenuEvents
    {
        /// <summary>Requests an asynchronous load of the named gameplay scene.</summary>
        public static event Action<string> StartRequested;
        /// <summary>Requests saving settings and quitting.</summary>
        public static event Action QuitConfirmed;
        /// <summary>Reports the active input context.</summary>
        public static event Action<MainMenuState> PanelChanged;
        /// <summary>Requests semantic audio feedback.</summary>
        public static event Action<UiAudioEventType> UiAudio;
        /// <summary>Reports that the loading fade reached black.</summary>
        public static event Action FadeOutComplete;
        /// <summary>Reports a scene load failure.</summary>
        public static event Action LoadFailed;
        /// <summary>Publishes a start request.</summary>
        public static void RaiseStartRequested(string sceneName) => StartRequested?.Invoke(sceneName);
        /// <summary>Publishes a confirmed quit.</summary>
        public static void RaiseQuitConfirmed() => QuitConfirmed?.Invoke();
        /// <summary>Publishes an input context change.</summary>
        public static void RaisePanelChanged(MainMenuState state) => PanelChanged?.Invoke(state);
        /// <summary>Publishes a semantic sound.</summary>
        public static void RaiseUiAudio(UiAudioEventType type) => UiAudio?.Invoke(type);
        /// <summary>Publishes fade completion.</summary>
        public static void RaiseFadeOutComplete() => FadeOutComplete?.Invoke();
        /// <summary>Publishes a failed scene load.</summary>
        public static void RaiseLoadFailed() => LoadFailed?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            StartRequested = null;
            QuitConfirmed = null;
            PanelChanged = null;
            UiAudio = null;
            FadeOutComplete = null;
            LoadFailed = null;
        }
    }
}
