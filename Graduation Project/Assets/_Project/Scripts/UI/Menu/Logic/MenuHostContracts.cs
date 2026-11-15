using UnityEngine;
using UnityEngine.UI;

namespace RetroFPS.UI.Menu
{
    /// <summary>Shared navigation and focus boundary for main and pause menu views.</summary>
    public interface IMenuInputContext
    {
        /// <summary>Whether activation, pointer and navigation input is locked.</summary>
        bool InputLocked { get; }
        /// <summary>Safe focus destination in the active context.</summary>
        Selectable DefaultFocus { get; }
        /// <summary>Looks up a cached selectable only inside the current focus trap.</summary>
        Selectable FindActive(GameObject candidate);
        /// <summary>Routes Back through the active context.</summary>
        void Back();
    }

    /// <summary>Shared settings and display-confirmation boundary for both menu hosts.</summary>
    public interface ISettingsPanelHost
    {
        /// <summary>Whether live settings requests may be accepted.</summary>
        bool CanEditSettings { get; }
        /// <summary>Whether display confirmation may supersede a pending settings press.</summary>
        bool CanPreviewDisplay { get; }
        /// <summary>Starts safe-default display confirmation and remembers the focus opener.</summary>
        bool OpenDisplayPrompt(MenuFocusTarget opener);
    }
}
