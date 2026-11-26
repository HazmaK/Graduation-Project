using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Taş arena kapısını kaydırır; fizik engeli komutla hemen değişir.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafArenaDoor : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Collider blocker;
        [SerializeField] private float travelSeconds = 0.35f;
        [SerializeField] private float raisedHeight = 5.2f;
        private bool _isClosed;
        private float _from;
        private float _to;
        private float _elapsed;
        /// <summary>Kapının fizik geçişini engelleyip engellemediği.</summary>
        public bool IsClosed => _isClosed;

        /// <summary>Kapıyı kapatır veya açar; açık kapı yükseldikten sonra kendini kapatır.</summary>
        public void SetClosed(bool closed, bool instant = false)
        {
            _isClosed = closed;
            if (blocker != null) blocker.enabled = closed;
            if (closed && !gameObject.activeSelf) gameObject.SetActive(true);
            _from = visual != null ? visual.localPosition.y : 0f;
            _to = closed ? 0f : raisedHeight;
            _elapsed = 0f;
            if (instant || visual == null || travelSeconds <= 0f)
            {
                MoveVisual(_to);
                enabled = false;
                if (!closed) gameObject.SetActive(false);
                return;
            }
            enabled = true;
        }

        private void MoveVisual(float height)
        {
            if (visual == null) return;
            Vector3 position = visual.localPosition;
            position.y = height;
            visual.localPosition = position;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / travelSeconds);
            MoveVisual(Mathf.Lerp(_from, _to, t));
            if (t < 1f) return;
            enabled = false;
            if (!_isClosed) gameObject.SetActive(false);
        }
    }
}
