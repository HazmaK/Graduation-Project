using UnityEngine;

namespace RetroFPS
{
    /// <summary>A pooled low shock wall that travels forward and damages once.</summary>
    public class JudgeShockLine : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float halfWidth = 2.5f;
        [SerializeField] private float halfHeight = 0.35f;
        [SerializeField] private float centerHeight = 0.4f;
        [SerializeField] private float halfDepth = 0.4f;

        private readonly Collider[] _overlaps = new Collider[32];
        private readonly RaycastHit[] _rayHits = new RaycastHit[16];
        private PlayerHealth _player;
        private Transform _playerTransform;
        private bool _initialized;
        private bool _hasVisual;
        private bool _isBusy;
        private bool _hasHit;
        private int _playerMask;
        private int _obstacleMask;
        private Vector3 _position;
        private Vector3 _direction;
        private Vector3 _halfExtents;
        private Quaternion _rotation;
        private float _speed;
        private float _travelLength;
        private float _travelled;
        private float _damage;

        /// <summary>Whether this instance is travelling.</summary>
        public bool IsBusy => _isBusy;

        private void Awake()
        {
            if (!_isBusy && visual != null) visual.gameObject.SetActive(false);
        }

        /// <summary>Launches a low wall; suppressDamage prevents a second hit after its execution circle connected.</summary>
        public void Activate(Vector3 origin, Vector3 direction, float speed, float travelLength, float damage,
            bool suppressDamage = false)
        {
            Initialize();
            Cancel();
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.000001f || speed <= 0f || travelLength <= 0f) return;
            _direction = direction.normalized;
            _rotation = Quaternion.LookRotation(_direction, Vector3.up);
            _position = origin;
            int count = Physics.RaycastNonAlloc(origin + Vector3.up * 3f, Vector3.down,
                _rayHits, Mathf.Infinity, GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (_rayHits[i].distance >= nearest) continue;
                nearest = _rayHits[i].distance;
                _position.y = _rayHits[i].point.y;
            }
            _position.y += centerHeight;
            _halfExtents = new Vector3(Mathf.Max(0.001f, halfWidth),
                Mathf.Max(0.001f, halfHeight), Mathf.Max(0.001f, halfDepth));
            _speed = speed;
            _travelLength = travelLength;
            _travelled = 0f;
            _damage = Mathf.Max(0f, damage);
            _hasHit = suppressDamage;
            if (_hasVisual)
            {
                visual.localScale = _halfExtents * 2f;
                visual.SetPositionAndRotation(_position, _rotation);
                visual.gameObject.SetActive(true);
            }
            _isBusy = true;
            enabled = true;
            gameObject.SetActive(true);
        }

        /// <summary>Immediately stops travel and hides the wall.</summary>
        public void Cancel()
        {
            _isBusy = false;
            if (visual != null) visual.gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _hasVisual = visual != null;
            _playerMask = 1 << GameLayers.Player;
            _obstacleMask = GameLayers.EnemyObstacleMask;
            _player = FindAnyObjectByType<PlayerHealth>();
            if (_player != null) _playerTransform = _player.transform;
        }

        private void Update()
        {
            if (!_isBusy) return;
            float step = Mathf.Min(_speed * Time.deltaTime, _travelLength - _travelled);
            if (step > 0f && Physics.RaycastNonAlloc(_position, _direction, _rayHits, step,
                _obstacleMask, QueryTriggerInteraction.Ignore) > 0)
            {
                Cancel();
                return;
            }
            _position += _direction * step;
            _travelled += step;
            if (_hasVisual) visual.SetPositionAndRotation(_position, _rotation);
            if (!_hasHit && _player != null)
            {
                int count = Physics.OverlapBoxNonAlloc(_position, _halfExtents, _overlaps,
                    _rotation, _playerMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    Transform hit = _overlaps[i].transform;
                    if (hit != _playerTransform && !hit.IsChildOf(_playerTransform)) continue;
                    _hasHit = true;
                    _player.TakeUnparryableDamage(_damage, _playerTransform.position, -_direction);
                    break;
                }
            }
            if (_travelled >= _travelLength) Cancel();
        }

        private void OnDisable() => Cancel();
    }
}
