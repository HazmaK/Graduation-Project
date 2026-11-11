using UnityEngine;

namespace RetroFPS.Run
{
    /// <summary>
    /// Araf'tan kuleye geri dönüşü tetikleyen kan portalı (Trigger Collider).
    /// Canlı oyuncu içine girdiğinde <see cref="RunEvents.RaiseEscapePortalReached"/> olayını yükseltir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class ArafEscapePortal : MonoBehaviour
    {
        [SerializeField] private bool startSealed = true;
        [SerializeField] private Renderer[] visuals;
        [SerializeField] private Light portalLight;

        private bool _triggered;
        private bool _isSealed;
        private MaterialPropertyBlock _propertyBlock;
        private Color[] _baseColors;
        private Color[] _emissionColors;
        private float _openLightIntensity;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Portal mühürlüyken kaçış tetiklenmez.</summary>
        public bool IsSealed => _isSealed;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            int count = visuals != null ? visuals.Length : 0;
            _baseColors = new Color[count];
            _emissionColors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                Material material = visuals[i] != null ? visuals[i].sharedMaterial : null;
                _baseColors[i] = material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId) : Color.white;
                _emissionColors[i] = material != null && material.HasProperty(EmissionColorId)
                    ? material.GetColor(EmissionColorId) : Color.black;
            }
            if (portalLight != null) _openLightIntensity = portalLight.intensity;
        }

        private void OnEnable()
        {
            _triggered = false;
            SetSealed(startSealed);
        }

        /// <summary>Mührü değiştirir; önbellekteki renklerle görseli karartır ve ışığı kapatır.</summary>
        public void SetSealed(bool isSealed)
        {
            _isSealed = isSealed;
            if (_propertyBlock != null)
            {
                for (int i = 0; i < _baseColors.Length; i++)
                {
                    if (visuals[i] == null) continue;
                    visuals[i].GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor(BaseColorId, _baseColors[i] * (isSealed ? 0.15f : 1f));
                    _propertyBlock.SetColor(EmissionColorId, _emissionColors[i] * (isSealed ? 0.02f : 1f));
                    visuals[i].SetPropertyBlock(_propertyBlock);
                }
            }
            if (portalLight != null)
            {
                portalLight.intensity = _openLightIntensity;
                portalLight.enabled = !isSealed;
            }
        }

        private void Reset()
        {
            if (TryGetComponent(out Collider col))
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isSealed || _triggered || other == null) return;

            PlayerHealth player = null;
            if (!other.TryGetComponent(out player) && other.attachedRigidbody != null)
            {
                other.attachedRigidbody.TryGetComponent(out player);
            }
            if (player == null)
            {
                player = other.GetComponentInParent<PlayerHealth>();
            }

            if (player == null || player.IsDead) return;

            _triggered = true;
            RunEvents.RaiseEscapePortalReached();
        }
    }
}
