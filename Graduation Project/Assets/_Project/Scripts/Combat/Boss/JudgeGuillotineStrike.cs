using RetroFPS.Pause;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>A pooled, telegraphed falling blade that damages once on impact.</summary>
    public class JudgeGuillotineStrike : MonoBehaviour
    {
        [SerializeField] private Transform marker;
        [SerializeField] private Transform blade;
        [SerializeField] private float dropHeight = 12f;
        [SerializeField] private float lingerTime = 0.4f;

        private readonly Collider[] _overlaps = new Collider[32];
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];
        private PlayerHealth _player;
        private Transform _playerTransform;
        private bool _initialized;
        private bool _hasMarker;
        private bool _hasBlade;
        private bool _isBusy;
        private bool _impacted;
        private int _playerMask;
        private Vector3 _groundPosition;
        private Vector3 _dropPosition;
        private float _fallDelay;
        private float _radius;
        private float _damage;
        private float _hitHeight;
        private float _age;

        /// <summary>Whether this instance is performing a strike or lingering.</summary>
        public bool IsBusy => _isBusy;

        private void Awake()
        {
            if (!_isBusy) HideVisuals();
        }

        /// <summary>Starts a telegraphed strike at the nearest ground below the supplied point.</summary>
        public void Activate(Vector3 groundPosition, float fallDelay, float radius, float damage, float hitHeight)
        {
            Initialize();
            Cancel();
            _groundPosition = groundPosition;
            int count = Physics.RaycastNonAlloc(groundPosition + Vector3.up * 3f,
                Vector3.down, _groundHits, Mathf.Infinity, GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (_groundHits[i].distance >= nearest) continue;
                nearest = _groundHits[i].distance;
                _groundPosition = _groundHits[i].point;
            }

            _fallDelay = Mathf.Max(0f, fallDelay);
            _radius = Mathf.Max(0f, radius);
            _damage = Mathf.Max(0f, damage);
            _hitHeight = Mathf.Max(0f, hitHeight);
            _age = 0f;
            _impacted = false;
            _dropPosition = _groundPosition + Vector3.up * Mathf.Max(0f, dropHeight);
            if (_hasMarker)
            {
                marker.position = _groundPosition;
                Vector3 scale = marker.localScale;
                scale.x = _radius * 2f;
                scale.z = _radius * 2f;
                marker.localScale = scale;
                marker.gameObject.SetActive(true);
            }
            if (_hasBlade)
            {
                blade.position = _dropPosition;
                blade.gameObject.SetActive(true);
            }
            _isBusy = true;
            enabled = true;
            gameObject.SetActive(true);
        }

        /// <summary>Immediately stops the strike and hides its visuals.</summary>
        public void Cancel()
        {
            _isBusy = false;
            HideVisuals();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _hasMarker = marker != null;
            _hasBlade = blade != null;
            _playerMask = 1 << GameLayers.Player;
            _player = FindAnyObjectByType<PlayerHealth>();
            if (_player != null) _playerTransform = _player.transform;
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (!_isBusy) return;
            _age += Time.deltaTime;
            if (!_impacted)
            {
                float progress = _fallDelay > 0f ? Mathf.Clamp01(_age / _fallDelay) : 1f;
                if (_hasBlade) blade.position = Vector3.Lerp(_dropPosition, _groundPosition, progress);
                if (_age < _fallDelay) return;
                _impacted = true;
                if (_hasMarker) marker.gameObject.SetActive(false);
                // Broad phase encloses the full falling column; exact radial/height checks below
                // keep the marked footprint cylindrical rather than a ground-only sphere.
                float halfHeight = _hitHeight * 0.5f;
                Vector3 center = _groundPosition + Vector3.up * halfHeight;
                int count = Physics.OverlapSphereNonAlloc(center,
                    Mathf.Sqrt(_radius * _radius + halfHeight * halfHeight), _overlaps,
                    _playerMask, QueryTriggerInteraction.Collide);
                if (_player != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        Transform hit = _overlaps[i].transform;
                        if (hit != _playerTransform && !hit.IsChildOf(_playerTransform)) continue;
                        Vector3 point = _playerTransform.position;
                        Vector3 delta = point - _groundPosition;
                        if (delta.y < 0f || delta.y > _hitHeight) continue;
                        delta.y = 0f;
                        if (delta.sqrMagnitude > _radius * _radius) continue;
                        _player.TakeUnparryableDamage(_damage, _playerTransform.position, Vector3.up);
                        break;
                    }
                }
            }
            if (_age >= _fallDelay + Mathf.Max(0f, lingerTime)) Cancel();
        }

        private void OnDisable() => Cancel();

        private void HideVisuals()
        {
            if (marker != null) marker.gameObject.SetActive(false);
            if (blade != null) blade.gameObject.SetActive(false);
        }
    }
}
