using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS
{
    /// <summary>Null-safe Flying Eye presentation, independent of combat decisions.</summary>
    [DisallowMultipleComponent]
    public sealed class FlyingEyeView : MonoBehaviour
    {
        [Tooltip("Replaceable art subtree.")] [SerializeField] private Transform visualRoot;
        [Tooltip("Art renderers receiving body tints and flashes.")] [SerializeField] private Renderer[] tintRenderers;
        [Tooltip("Iris and pupil renderers receiving charge glow.")] [SerializeField] private Renderer[] glowRenderers;
        [Tooltip("Projectile origin.")] [SerializeField] private Transform fireSocket;
        [Tooltip("Charge presentation origin.")] [SerializeField] private Transform telegraphSocket;
        [Tooltip("Death effect origin.")] [SerializeField] private Transform deathFxSocket;
        [Tooltip("Prebuilt charge orb.")] [SerializeField] private Transform chargeOrb;
        [Tooltip("Charge orb tint renderer.")] [SerializeField] private Renderer chargeOrbRenderer;
        [Tooltip("Prebuilt world-space heavy aim line.")] [SerializeField] private LineRenderer aimLine;
        [Tooltip("Light charge colour.")] [SerializeField] private Color lightColor = new Color(0.85f, 0.15f, 0.95f);
        [Tooltip("Heavy charge colour.")] [SerializeField] private Color heavyColor = new Color(1f, 0.35f, 0.05f);
        [Tooltip("Locked heavy aim line colour.")] [SerializeField] private Color lockedColor = Color.white;
        [Tooltip("Grounded flicker colour.")] [SerializeField] private Color groundedColor = new Color(1f, 0.9f, 0.2f);
        [Tooltip("Charge orb scale at windup start.")] [SerializeField] private float chargeMinScale = 0.1f;
        [Tooltip("Charge orb scale at launch.")] [SerializeField] private float chargeMaxScale = 1f;
        [Tooltip("Light charge orb max scale as a fraction of the heavy max scale.")] [SerializeField, Range(0.05f, 1f)] private float lightChargeScaleFraction = 0.45f;
        [Tooltip("Charge pulse frequency in radians per second.")] [SerializeField] private float pulseFrequency = 18f;
        [Tooltip("Charge pulse brightness variation.")] [SerializeField] private float pulseAmplitude = 0.25f;
        [Tooltip("Charge glow brightness at full windup.")] [SerializeField] private float glowIntensity = 3f;
        [Tooltip("Grounded flicker frequency in radians per second.")] [SerializeField] private float groundedFlickerFrequency = 24f;
        [Tooltip("Grounded tint strength at flicker minimum.")] [SerializeField] private float groundedMinTint = 0.35f;
        [Tooltip("Forward fire offset without a socket.")] [SerializeField] private float fallbackFireOffset = 0.65f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _block;
        private Color[] _baseTints;
        private Color[] _baseGlows;
        private Color[] _baseTintEmission;
        private Color[] _baseGlowEmission;
        private float _elapsed;
        private float _flashRemaining;
        private Color _flashColor;
        private bool _visible = true;
        private bool _grounded;
        private bool _windup;
        private bool _locked;
        private FlyingEyeAttack _kind;
        private float _progress;
        private Vector3 _aimPoint;

        /// <summary>World projectile origin, with a fallback for incomplete art.</summary>
        public Vector3 FirePosition => fireSocket != null ? fireSocket.position : transform.position + transform.forward * fallbackFireOffset;
        /// <summary>World death effect origin.</summary>
        public Vector3 DeathFxPosition => deathFxSocket != null ? deathFxSocket.position : transform.position;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            CacheColors(tintRenderers, out _baseTints, out _baseTintEmission);
            CacheColors(glowRenderers, out _baseGlows, out _baseGlowEmission);
            HideWindup();
        }

        private static void CacheColors(Renderer[] renderers, out Color[] colors, out Color[] emission)
        {
            int count = renderers != null ? renderers.Length : 0;
            colors = new Color[count];
            emission = new Color[count];
            for (int i = 0; i < count; i++)
            {
                Material material = renderers[i] != null ? renderers[i].sharedMaterial : null;
                colors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) :
                    material != null && material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
                emission[i] = material != null && material.HasProperty(EmissionColorId) ? material.GetColor(EmissionColorId) : Color.black;
            }
        }

        /// <summary>Displays charge progress and the heavy aim prediction or lock.</summary>
        public void ShowWindup(FlyingEyeAttack kind, float progress01, Vector3 aimPoint, bool aimLocked)
        {
            _windup = true;
            _kind = kind;
            _progress = Mathf.Clamp01(progress01);
            _aimPoint = aimPoint;
            _locked = aimLocked;
            Render();
        }

        /// <summary>Hides charge effects after launch, cancellation, disable or respawn.</summary>
        public void HideWindup()
        {
            _windup = false;
            if (chargeOrb != null)
            {
                chargeOrb.localScale = Vector3.zero;
                chargeOrb.gameObject.SetActive(false);
            }
            if (aimLine != null) aimLine.enabled = false;
            Render();
        }

        /// <summary>Displays a temporary hit or stun flash.</summary>
        public void Flash(Color color, float seconds)
        {
            _flashColor = color;
            _flashRemaining = Mathf.Max(0f, seconds);
            Render();
        }

        /// <summary>Enables or clears the grounded yellow flicker.</summary>
        public void SetGrounded(bool grounded)
        {
            _grounded = grounded;
            _flashRemaining = 0f;
            Render();
        }

        /// <summary>Shows or hides art and clears transient effects for pooled reuse.</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            _flashRemaining = 0f;
            _grounded = false;
            if (visualRoot != null) visualRoot.gameObject.SetActive(visible);
            SetRendererVisibility(tintRenderers, visible);
            SetRendererVisibility(glowRenderers, visible);
            HideWindup();
        }

        /// <summary>Advances presentation using the simulation delta supplied by the AI.</summary>
        public void Tick(float dt)
        {
            _elapsed += dt;
            _flashRemaining = Mathf.Max(0f, _flashRemaining - dt);
            Render();
        }

        private static void SetRendererVisibility(Renderer[] renderers, bool visible)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }

        private void Render()
        {
            if (_block == null) return; // Safe even if the AI's Awake runs first.
            bool heavy = _windup && _kind == FlyingEyeAttack.Heavy;
            bool charging = _windup;
            float pulse = 1f + Mathf.Sin(_elapsed * pulseFrequency) * pulseAmplitude;
            Color chargeColor = _kind == FlyingEyeAttack.Heavy ? heavyColor : lightColor;
            float maxScale = heavy ? chargeMaxScale : chargeMaxScale * lightChargeScaleFraction;
            ApplyArt(tintRenderers, _baseTints, _baseTintEmission, charging ? _progress : 0f, chargeColor, false);
            ApplyArt(glowRenderers, _baseGlows, _baseGlowEmission, charging ? _progress : 0f, chargeColor, true);
            if (chargeOrb != null)
            {
                chargeOrb.gameObject.SetActive(_visible && charging);
                chargeOrb.localScale = charging ? Vector3.one * Mathf.Lerp(chargeMinScale, maxScale, _progress) : Vector3.zero;
                if (telegraphSocket != null) chargeOrb.position = telegraphSocket.position;
            }
            if (chargeOrbRenderer != null && charging) Tint(chargeOrbRenderer, chargeColor * pulse, chargeColor * (pulse * glowIntensity));
            if (aimLine != null)
            {
                aimLine.enabled = _visible && heavy;
                if (heavy)
                {
                    aimLine.SetPosition(0, FirePosition);
                    aimLine.SetPosition(1, _aimPoint);
                    Color color = _locked ? lockedColor : heavyColor;
                    aimLine.startColor = color;
                    aimLine.endColor = color;
                    Tint(aimLine, color, color);
                }
            }
        }

        private void ApplyArt(Renderer[] renderers, Color[] baseColors, Color[] baseEmission,
            float charge, Color chargeColor, bool glow)
        {
            if (renderers == null || baseColors == null) return;
            float flicker = Mathf.Lerp(groundedMinTint, 1f,
                (Mathf.Sin(_elapsed * groundedFlickerFrequency) + 1f) * 0.5f);
            for (int i = 0; i < renderers.Length && i < baseColors.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color color = Color.Lerp(baseColors[i], chargeColor, charge);
                Color emission = Color.Lerp(baseEmission[i], chargeColor * glowIntensity, glow ? charge : 0f);
                if (_grounded) color = Color.Lerp(baseColors[i], groundedColor, flicker);
                if (_flashRemaining > 0f) color = _flashColor;
                Tint(renderers[i], color, emission);
            }
        }

        private void Tint(Renderer renderer, Color color, Color emission)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _block.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(_block);
        }
    }
}
