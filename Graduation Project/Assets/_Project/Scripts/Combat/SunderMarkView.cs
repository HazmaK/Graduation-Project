using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Self-starting, pooled light placeholder for Sunder marks.</summary>
    [DisallowMultipleComponent]
    public sealed class SunderMarkView : MonoBehaviour
    {
        private const int PoolCapacity = 8;
        [SerializeField] private float anchorHeight = 1.2f;
        [SerializeField] private float lightRange = 2.5f;
        [SerializeField] private Color markerColor = new Color(1f, 0.22f, 0.04f);
        [SerializeField] private float baseIntensity = 1.5f;
        [SerializeField] private float pulseIntensity = 0.5f;
        [SerializeField] private float pulseSpeed = 8f;

        private static SunderMarkView _instance;
        private readonly Transform[] _anchors = new Transform[PoolCapacity];
        private readonly Transform[] _markers = new Transform[PoolCapacity];
        private readonly Light[] _lights = new Light[PoolCapacity];
        private readonly float[] _expiresAt = new float[PoolCapacity];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance == null) _instance = FindAnyObjectByType<SunderMarkView>(FindObjectsInactive.Include);
            if (_instance == null)
                new GameObject("Sunder_Mark_View").AddComponent<SunderMarkView>();
            // Rebind when entering Play Mode with domain/scene reload disabled.
            if (_instance != null)
            {
                _instance.gameObject.SetActive(true);
                _instance.enabled = true;
                _instance.HideAll();
                _instance.Subscribe();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < PoolCapacity; i++)
            {
                GameObject marker = new GameObject("Sunder_Marker");
                _markers[i] = marker.transform;
                _markers[i].SetParent(transform, false);
                Light light = marker.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = markerColor;
                light.range = lightRange;
                light.shadows = LightShadows.None;
                _lights[i] = light;
                marker.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_instance == this) Subscribe();
        }

        private void Subscribe()
        {
            SunderMarks.OnMarked -= HandleMarked;
            SunderMarks.OnMarked += HandleMarked;
            SunderMarks.OnCleared -= HandleCleared;
            SunderMarks.OnCleared += HandleCleared;
        }

        private void OnDisable()
        {
            SunderMarks.OnMarked -= HandleMarked;
            SunderMarks.OnCleared -= HandleCleared;
            HideAll();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void HandleMarked(Transform anchor, float duration)
        {
            if (anchor == null || !anchor.gameObject.activeInHierarchy) return;
            int index = -1;
            int soonest = 0;
            float now = Time.time;
            for (int i = 0; i < PoolCapacity; i++)
            {
                if (_anchors[i] == anchor)
                {
                    index = i;
                    break;
                }
                if (index < 0 && (_anchors[i] == null || now >= _expiresAt[i])) index = i;
                if (_expiresAt[i] < _expiresAt[soonest]) soonest = i;
            }
            if (index < 0) index = soonest;
            _anchors[index] = anchor;
            _expiresAt[index] = now + duration;
            _markers[index].position = anchor.position + Vector3.up * anchorHeight;
            _lights[index].intensity = baseIntensity;
            _markers[index].gameObject.SetActive(true);
        }

        private void HandleCleared(Transform anchor)
        {
            for (int i = 0; i < PoolCapacity; i++)
                if (_anchors[i] == anchor) Hide(i);
        }

        private void LateUpdate()
        {
            float now = Time.time;
            float intensity = baseIntensity + Mathf.Sin(now * pulseSpeed) * pulseIntensity;
            for (int i = 0; i < PoolCapacity; i++)
            {
                Transform anchor = _anchors[i];
                if (anchor == null || !anchor.gameObject.activeInHierarchy || now >= _expiresAt[i])
                {
                    Hide(i);
                    continue;
                }
                _markers[i].position = anchor.position + Vector3.up * anchorHeight;
                _lights[i].intensity = intensity;
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < PoolCapacity; i++) Hide(i);
        }

        private void Hide(int index)
        {
            _anchors[index] = null;
            if (_markers[index] != null && _markers[index].gameObject.activeSelf)
                _markers[index].gameObject.SetActive(false);
        }
    }
}
