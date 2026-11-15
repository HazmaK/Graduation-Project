using UnityEngine;

namespace RetroFPS
{
    /// <summary>Prefab-owned red boundary and expanding fill for the Judge's Repulse warning.</summary>
    [DisallowMultipleComponent]
    public sealed class JudgeRepulseWarning : MonoBehaviour
    {
        [SerializeField] private Transform fill;
        [SerializeField] private LineRenderer outline;

        private float _radius;
        private float _thickness;

        /// <summary>Shows the full danger boundary with an initially empty fill.</summary>
        public void Prepare(Vector3 position, float radius, float thickness)
        {
            _radius = Mathf.Max(0f, radius);
            _thickness = Mathf.Max(0f, thickness);
            if (outline != null)
            {
                outline.transform.localScale = new Vector3(_radius, 1f, _radius);
                outline.widthMultiplier = _thickness;
                outline.gameObject.SetActive(true);
            }
            if (fill != null) fill.gameObject.SetActive(true);
            SetProgress(position, 0f);
        }

        /// <summary>Follows the boss's ground centre and fills to the configured radius.</summary>
        public void SetProgress(Vector3 position, float progress)
        {
            transform.position = position;
            if (fill == null) return;
            float diameter = _radius * 2f * Mathf.Clamp01(progress);
            // Unity's unit cylinder has height two and diameter one.
            fill.localScale = new Vector3(diameter, _thickness * 0.5f, diameter);
        }

        /// <summary>Hides the complete warning on impact, cancellation or encounter cleanup.</summary>
        public void Cancel()
        {
            if (fill != null) fill.gameObject.SetActive(false);
            if (outline != null) outline.gameObject.SetActive(false);
        }

        private void OnDisable() => Cancel();
    }
}
