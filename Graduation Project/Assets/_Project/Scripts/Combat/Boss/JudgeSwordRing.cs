using RetroFPS.Pause;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>Allocation-free orbit and volley formation for The Judge's decorative swords.</summary>
    [DisallowMultipleComponent]
    public sealed class JudgeSwordRing : MonoBehaviour
    {
        [SerializeField] private Transform[] swords;
        [SerializeField] private float radius = 2.5f;
        [SerializeField] private float heightOffset = 1.5f;
        [SerializeField] private float angularSpeed = 35f;
        [SerializeField] private Vector3 orbitSwordEuler = Vector3.zero;
        [SerializeField] private Vector3 lineSwordEuler = new Vector3(90f, 0f, 0f);

        private float[] _hiddenUntil;
        private bool _visible = true;
        private bool _line;
        private float _angle;
        private float _spacing;
        private float _blend;
        private Vector3 _center;
        private Vector3 _right;

        /// <summary>Number of launch slots in the ring.</summary>
        public int Count => swords != null ? swords.Length : 0;

        private void Awake() => _hiddenUntil = new float[Count];

        /// <summary>Shows or hides the ring and resets temporary launch gaps.</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            _line = false;
            _angle = 0f;
            for (int i = 0; i < Count; i++)
            {
                if (_hiddenUntil != null) _hiddenUntil[i] = 0f;
                if (swords[i] != null) swords[i].gameObject.SetActive(visible);
            }
            PositionSwords();
        }

        /// <summary>Returns a sword's world position, or the ring center when no swords exist.</summary>
        public Vector3 GetLaunchPosition(int index)
        {
            if (Count == 0) return transform.position + Vector3.up * heightOffset;
            int slot = Mathf.Abs(index % Count);
            return swords[slot] != null ? swords[slot].position : transform.position;
        }

        /// <summary>Blends orbit positions into a world-space horizontal volley line.</summary>
        public void FormLine(Vector3 center, Vector3 right, float spacing, float blend)
        {
            _line = true;
            _center = center;
            right.y = 0f;
            _right = right.sqrMagnitude > 0f ? right.normalized : transform.right;
            _spacing = spacing;
            _blend = Mathf.Clamp01(blend);
            PositionSwords();
        }

        /// <summary>Returns swords to their orbit after a volley or interruption.</summary>
        public void ReleaseLine() => _line = false;

        /// <summary>Hides a launch slot until the supplied number of seconds has elapsed.</summary>
        public void HideSwordTemporarily(int index, float seconds)
        {
            if (Count == 0 || _hiddenUntil == null) return;
            int slot = Mathf.Abs(index % Count);
            _hiddenUntil[slot] = Time.time + Mathf.Max(0f, seconds);
            if (swords[slot] != null) swords[slot].gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (GamePause.IsSimulationBlocked) return;
            if (!_visible) return;
            _angle = Mathf.Repeat(_angle + angularSpeed * Time.deltaTime, 360f);
            PositionSwords();
        }

        private void PositionSwords()
        {
            for (int i = 0; i < Count; i++)
            {
                Transform sword = swords[i];
                if (sword == null) continue;
                bool show = _visible && (_hiddenUntil == null || Time.time >= _hiddenUntil[i]);
                if (sword.gameObject.activeSelf != show) sword.gameObject.SetActive(show);
                float angle = (_angle + 360f * i / Count) * Mathf.Deg2Rad;
                Vector3 orbit = transform.position + Vector3.up * heightOffset
                    + (Vector3.right * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle)) * radius;
                Vector3 line = _center + _right * ((i - (Count - 1) * 0.5f) * _spacing);
                sword.position = _line ? Vector3.Lerp(orbit, line, _blend) : orbit;
                Quaternion orbitRotation = Quaternion.Euler(orbitSwordEuler);
                Vector3 lineForward = Vector3.Cross(_right, Vector3.up);
                if (!_line || lineForward.sqrMagnitude < 1e-6f) { sword.rotation = orbitRotation; continue; }
                Quaternion lineRotation = Quaternion.LookRotation(lineForward) * Quaternion.Euler(lineSwordEuler);
                sword.rotation = Quaternion.Slerp(orbitRotation, lineRotation, _blend);
            }
        }
    }
}
