using RetroFPS.Presentation;
using UnityEngine;

namespace RetroFPS.Run
{
    /// <summary>Receives presentation restart requests and delegates validation to the run director.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RunDirector))]
    public sealed class RunRestartHandler : MonoBehaviour
    {
        [SerializeField] private RunDirector director;

        private void Awake()
        {
            if (director == null) director = GetComponent<RunDirector>();
        }

        private void OnEnable() => HudRequests.NewRunRequested += HandleNewRunRequested;
        private void OnDisable() => HudRequests.NewRunRequested -= HandleNewRunRequested;

        private void HandleNewRunRequested()
        {
            if (director != null) director.RestartRun();
        }
    }
}
