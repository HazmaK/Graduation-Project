using System;
using UnityEngine;

namespace RetroFPS.Presentation
{
    /// <summary>Presentation requests with no gameplay or scene dependency.</summary>
    public static class HudRequests
    {
        /// <summary>Raised when the death card confirms a new run.</summary>
        public static event Action NewRunRequested;

        /// <summary>Requests a new run; gameplay owns validation and restart.</summary>
        public static void RequestNewRun() => NewRunRequested?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => NewRunRequested = null;
    }
}
