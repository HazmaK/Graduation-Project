using UnityEngine;

namespace RetroFPS.Pause
{
    /// <summary>Allocation-free consumer access; only the lifecycle adapter advances the presentation clock.</summary>
    public static class GamePause
    {
        private static IPauseService service;
        private static float unscaledTime;
        private static float unscaledDeltaTime;

        /// <summary>Current injected owner, or null outside gameplay.</summary>
        public static IPauseService Service => service;
        /// <summary>Whether menu pause owns the environment.</summary>
        public static bool IsPaused => service != null && service.IsPaused;
        /// <summary>Whether simulation is blocked by pause or departure.</summary>
        public static bool IsSimulationBlocked => service != null && service.SimulationBlocked;
        /// <summary>Whether gameplay input is blocked, including resume/release latches.</summary>
        public static bool IsGameplayInputBlocked => service != null && service.GameplayInputBlocked;
        /// <summary>Accumulated real gameplay seconds excluding pause and departure; hitstop still advances it.</summary>
        public static float UnscaledTime => unscaledTime;
        /// <summary>Latest adapter delta, immediately zero if pause was acquired after clock advancement.</summary>
        public static float UnscaledDeltaTime => IsSimulationBlocked ? 0f : unscaledDeltaTime;

        /// <summary>Binds/unbinds the lifecycle owner; preserves elapsed time across additive rebinding.</summary>
        public static void SetService(IPauseService replacement)
        {
            service = replacement;
            unscaledDeltaTime = 0f;
        }

        /// <summary>Injects an isolated test owner and resets its clock; null clears the test state.</summary>
        public static void SetServiceForTests(IPauseService replacement)
        {
            SetService(replacement);
            unscaledTime = 0f;
        }

        /// <summary>
        /// Called once per frame by PauseRuntime after eligibility/input processing and before consumers.
        /// Supply that frame's real unscaled delta, never elapsed time since the last unpaused frame.
        /// </summary>
        public static void AdvanceClock(float realUnscaledDeltaTime)
        {
            unscaledDeltaTime = !IsSimulationBlocked && realUnscaledDeltaTime > 0f
                && !float.IsInfinity(realUnscaledDeltaTime) ? realUnscaledDeltaTime : 0f;
            unscaledTime += unscaledDeltaTime;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SetServiceForTests(null);
        }
    }
}
