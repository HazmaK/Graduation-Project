using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Records stable grounded poses for recovery from an Araf void fall.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafSafeGround : MonoBehaviour
    {
        [SerializeField] private ArafLabyrinthDirector labyrinth;
        [SerializeField, Min(0f)] private float stableSeconds = 0.3f;

        private PlayerMovement _player;
        private Transform _playerTransform;
        private bool _playerSearched;
        private bool _hasSafePose;
        private float _groundedSeconds;
        private Vector3 _lastSafePosition;
        private Quaternion _lastSafeRotation;

        /// <summary>Last stable ground position, or the entry position before the first record.</summary>
        public Vector3 LastSafePosition => _hasSafePose ? _lastSafePosition
            : labyrinth != null ? labyrinth.EntryPositionWorld : transform.position;

        /// <summary>Yaw-only rotation at the last stable position.</summary>
        public Quaternion LastSafeRotation => _hasSafePose ? _lastSafeRotation
            : Quaternion.Euler(0f, labyrinth != null ? labyrinth.transform.eulerAngles.y : transform.eulerAngles.y, 0f);

        private void Awake()
        {
            if (labyrinth == null) labyrinth = GetComponent<ArafLabyrinthDirector>();
        }

        private void OnEnable()
        {
            RunEvents.OnEnteredAraf += FindPlayerOnce;
            if (RunEvents.CurrentRun != null && RunEvents.CurrentRun.Phase == RunPhase.InAraf)
                FindPlayerOnce();
        }

        private void OnDisable()
        {
            RunEvents.OnEnteredAraf -= FindPlayerOnce;
            _groundedSeconds = 0f;
        }

        private void Update()
        {
            if (RunEvents.CurrentRun == null || RunEvents.CurrentRun.Phase != RunPhase.InAraf
                || labyrinth == null || labyrinth.World == null)
            {
                _groundedSeconds = 0f;
                return;
            }

            if (_player == null || _playerTransform == null || !_player.IsGrounded)
            {
                _groundedSeconds = 0f;
                return;
            }

            Vector3 position = _playerTransform.position;
            Vector3 voidLocal = Vector3.up * labyrinth.VoidHeightLocal;
            float voidWorldY = labyrinth.transform.TransformPoint(voidLocal).y;
            if (position.y <= voidWorldY + 4f)
            {
                _groundedSeconds = 0f;
                return;
            }

            _groundedSeconds += Time.deltaTime;
            if (_groundedSeconds < stableSeconds) return;
            _lastSafePosition = position;
            _lastSafeRotation = Quaternion.Euler(0f, _playerTransform.eulerAngles.y, 0f);
            _hasSafePose = true;
        }

        private void FindPlayerOnce()
        {
            if (_playerSearched) return;
            _playerSearched = true;
            _player = FindAnyObjectByType<PlayerMovement>();
            if (_player != null) _playerTransform = _player.transform;
        }

        /// <summary>Sets an explicit recovery pose and restarts the grounded stability interval.</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            _lastSafePosition = position;
            _lastSafeRotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            _hasSafePose = true;
            _groundedSeconds = 0f;
        }
    }
}
