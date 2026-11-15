using RetroFPS.Pause;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>A pooled low rotating arm that follows a pivot and allows jumping over it.</summary>
    public class JudgeScaleSweep : MonoBehaviour
    {
        [SerializeField] private Transform bar;
        [SerializeField] private float barHalfHeight = 0.35f;
        [SerializeField] private float barHalfWidth = 0.3f;
        [SerializeField] private float barCenterHeight = 0.45f;
        [SerializeField] private float rehitInterval = 0.6f;
        [SerializeField] private float spinUpTime = 0.4f;
        [SerializeField, Range(0.05f, 1f)] private float telegraphThickness = 0.3f;
        [SerializeField] private float groundProbeLift = 3f;

        private readonly Collider[] _overlaps = new Collider[32];
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];
        private PlayerHealth _player;
        private Transform _playerTransform;
        private Transform _pivot;
        private bool _initialized;
        private bool _hasBar;
        private bool _isBusy;
        private bool _prepared;
        private int _playerMask;
        private float _groundY;
        private float _length;
        private float _angularSpeed;
        private float _duration;
        private float _damage;
        private float _age;
        private float _angle;
        private float _nextHitTime;
        private Vector3 _halfExtents;

        /// <summary>Whether this instance is sweeping.</summary>
        public bool IsBusy => _isBusy;

        private void Awake()
        {
            if (!_isBusy && !_prepared && bar != null) bar.gameObject.SetActive(false);
        }

        /// <summary>Shows a thin, harmless arm at a fixed world yaw throughout the windup.</summary>
        public void Prepare(Transform pivot, float length, float startYawDegrees)
        {
            Initialize();
            Cancel();
            if (pivot == null || length <= 0f) return;
            _pivot = pivot;
            _length = length;
            _angle = startYawDegrees;
            _groundY = pivot.position.y;
            int count = Physics.RaycastNonAlloc(pivot.position + Vector3.up * groundProbeLift, Vector3.down,
                _groundHits, Mathf.Infinity, GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (_groundHits[i].distance >= nearest) continue;
                nearest = _groundHits[i].distance;
                _groundY = _groundHits[i].point.y;
            }
            _halfExtents = new Vector3(Mathf.Max(0.001f, barHalfWidth),
                Mathf.Max(0.001f, barHalfHeight), _length * 0.5f);
            if (_hasBar)
            {
                bar.localScale = new Vector3(_halfExtents.x * 2f * telegraphThickness,
                    _halfExtents.y * 2f * telegraphThickness, _length);
                PositionBar(Quaternion.Euler(0f, _angle, 0f));
                bar.gameObject.SetActive(true);
            }
            _prepared = true;
            enabled = true;
            gameObject.SetActive(true);
        }

        /// <summary>Starts rotating from the prepared yaw with spin-up; preparation is required.</summary>
        public void Activate(Transform pivot, float length, float angularSpeedDegrees, float duration, float damage)
        {
            if (!_prepared || _pivot != pivot || !Mathf.Approximately(_length, length) || duration <= 0f)
            {
                Cancel();
                return;
            }
            _prepared = false;
            _angularSpeed = angularSpeedDegrees;
            _duration = duration;
            _damage = Mathf.Max(0f, damage);
            _age = _nextHitTime = 0f;
            if (_hasBar) bar.localScale = _halfExtents * 2f;
            _isBusy = true;
        }

        /// <summary>Immediately stops the sweep and hides the arm.</summary>
        public void Cancel()
        {
            _isBusy = false;
            _prepared = false;
            _pivot = null;
            if (bar != null) bar.gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _hasBar = bar != null;
            _playerMask = 1 << GameLayers.Player;
            _player = FindAnyObjectByType<PlayerHealth>();
            if (_player != null) _playerTransform = _player.transform;
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (_prepared)
            {
                if (_pivot == null) Cancel();
                else PositionBar(Quaternion.Euler(0f, _angle, 0f));
                return;
            }
            if (!_isBusy) return;
            if (_pivot == null || _age >= _duration)
            {
                Cancel();
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, _duration - _age);
            // Integrate the linear ramp so the first step starts at zero angular speed.
            float rampStart = spinUpTime > 0f ? Mathf.Clamp01(_age / spinUpTime) : 1f;
            _age += dt;
            float rampEnd = spinUpTime > 0f ? Mathf.Clamp01(_age / spinUpTime) : 1f;
            _angle = Mathf.Repeat(_angle + _angularSpeed * dt * (rampStart + rampEnd) * 0.5f, 360f);
            Quaternion rotation = Quaternion.Euler(0f, _angle, 0f);
            Vector3 center = PositionBar(rotation);
            int count = Physics.OverlapBoxNonAlloc(center, _halfExtents, _overlaps, rotation,
                _playerMask, QueryTriggerInteraction.Collide);
            if (_player != null && _age >= _nextHitTime)
            {
                for (int i = 0; i < count; i++)
                {
                    Transform hit = _overlaps[i].transform;
                    if (hit != _playerTransform && !hit.IsChildOf(_playerTransform)) continue;
                    _nextHitTime = _age + Mathf.Max(0f, rehitInterval);
                    _player.TakeUnparryableDamage(_damage, _playerTransform.position, Vector3.up);
                    break;
                }
            }
            if (_age >= _duration) Cancel();
        }

        private Vector3 PositionBar(Quaternion rotation)
        {
            Vector3 center = _pivot.position;
            center.y = _groundY + barCenterHeight;
            center += rotation * Vector3.forward * (_length * 0.5f);
            if (_hasBar) bar.SetPositionAndRotation(center, rotation);
            return center;
        }

        private void OnDisable() => Cancel();
    }
}
