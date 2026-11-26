using UnityEngine;
using RetroFPS.Pause;

namespace RetroFPS.Combat
{
    /// <summary>Replaceable, pause-aware Penitent presentation independent of combat decisions.</summary>
    [DisallowMultipleComponent]
    public sealed class PenitentView : MonoBehaviour
    {
        [Tooltip("Replaceable art subtree.")] [SerializeField] private Transform visualRoot;
        [Tooltip("Renderers receiving body tints and damage flashes.")] [SerializeField] private Renderer[] tintRenderers;
        [Tooltip("Renderers receiving elite emission.")] [SerializeField] private Renderer[] glowRenderers;
        [Tooltip("Optional placeholder lid pivot; ignored when an Animator is assigned.")] [SerializeField] private Transform shieldPivot;
        [Tooltip("All independent shield piece hitboxes.")] [SerializeField] private Collider[] shieldColliders;
        [Tooltip("Independent hitbox subtree.")] [SerializeField] private Transform hitboxRoot;
        [Tooltip("Optional shield centre bone socket.")] [SerializeField] private Transform shieldCenterSocket;
        [Tooltip("Optional ground impact socket.")] [SerializeField] private Transform slamImpactSocket;
        [Tooltip("Optional head socket.")] [SerializeField] private Transform headSocket;
        [Tooltip("Optional death effect socket.")] [SerializeField] private Transform deathFxSocket;
        [Tooltip("Unit-cube renderer using RetroFPS/SlamAreaProjector; projects background, border, charge fill and impact flash onto upward-facing ground.")]
        [SerializeField] private Renderer slamAreaProjector;
        [Tooltip("Optional amber light enabled only during the slam telegraph.")] [SerializeField] private Light telegraphLight;
        [Tooltip("Optional replacement model Animator.")] [SerializeField] private Animator animator;
        [Tooltip("Guard pivot position.")] [SerializeField] private Vector3 guardPosition = new Vector3(0f, 0.25f, 0.46f);
        [Tooltip("Guard pivot local Euler rotation.")] [SerializeField] private Vector3 guardEuler;
        [Tooltip("Overhead pivot position.")] [SerializeField] private Vector3 overheadPosition = new Vector3(0f, 1.9f, 0.1f);
        [Tooltip("Overhead pivot local Euler rotation.")] [SerializeField] private Vector3 overheadEuler = new Vector3(-100f, 0f, 0f);
        [Tooltip("Strike pivot position.")] [SerializeField] private Vector3 strikePosition = new Vector3(0f, 0.1f, 0.6f);
        [Tooltip("Strike pivot local Euler rotation.")] [SerializeField] private Vector3 strikeEuler = new Vector3(85f, 0f, 0f);
        [Tooltip("Lowered pivot position.")] [SerializeField] private Vector3 loweredPosition = new Vector3(0.5f, 0.1f, 0.35f);
        [Tooltip("Lowered pivot local Euler rotation.")] [SerializeField] private Vector3 loweredEuler = new Vector3(30f, 0f, -35f);
        [Tooltip("Seconds for non-windup placeholder pose transitions.")] [SerializeField] private float poseDuration = 0.12f;
        [Tooltip("Ordinary hit flash seconds.")] [SerializeField] private float hitFlashDuration = 0.06f;
        [Tooltip("Critical hit flash seconds.")] [SerializeField] private float criticalFlashDuration = 0.12f;
        [Tooltip("Shield block flash seconds.")] [SerializeField] private float blockFlashDuration = 0.06f;
        [Tooltip("Ordinary hit flash colour.")] [SerializeField] private Color hitColor = new Color(2.2f, 2.2f, 2.2f);
        [Tooltip("Critical hit flash colour.")] [SerializeField] private Color criticalColor = new Color(3.2f, 2.8f, 0.35f);
        [Tooltip("Shield block flash colour.")] [SerializeField] private Color blockColor = new Color(1.8f, 2.2f, 2.6f);
        [Tooltip("Elite art tint.")] [SerializeField] private Color eliteColor = new Color(1f, 0.35f, 0.05f);
        [Tooltip("Elite tint blend.")] [SerializeField, Range(0f, 1f)] private float eliteTintStrength = 0.45f;
        [Tooltip("Elite glow brightness.")] [SerializeField] private float eliteGlowIntensity = 2f;
        [Tooltip("Amber colour at the start of the lid glow ramp.")] [SerializeField] private Color windupStartColor = new Color(1f, 0.6f, 0.05f);
        [Tooltip("Full rectangle background colour.")] [SerializeField] private Color areaOutlineColor = new Color(0.38f, 0.025f, 0.015f, 0.24f);
        [Tooltip("Full rectangle border colour, readable without bloom.")] [SerializeField] private Color areaBorderColor = new Color(1f, 0.16f, 0.03f, 0.9f);
        [Tooltip("Growing charge fill colour.")] [SerializeField] private Color areaFillColor = new Color(1f, 0.18f, 0.035f, 0.65f);
        [Tooltip("Impact flash colour.")] [SerializeField] private Color areaImpactColor = new Color(1f, 0.85f, 0.6f, 0.9f);
        [Tooltip("Impact flash fade duration in seconds.")] [SerializeField] private float areaImpactDuration = 0.25f;
        [Tooltip("Ground probe starts this far above the enemy's feet plane.")] [SerializeField] private float groundProbeHeight = 2f;
        [Tooltip("Ground probe distance below its start.")] [SerializeField] private float groundProbeDistance = 6f;
        [Tooltip("Steepest surface (degrees from flat) the slam area is drawn on; walls and step risers are skipped.")]
        [SerializeField, Range(0f, 89f)] private float areaMaxSurfaceAngle = 60f;
        [Tooltip("Border line thickness in metres.")] [SerializeField] private float areaBorderWidth = 0.08f;
        [Tooltip("The area fades out within this distance of the camera so the viewmodel and the player's feet stay clean.")]
        [SerializeField] private float areaCameraNearFade = 1.2f;
        [Tooltip("Pulse speed at windup start, radians per second.")] [SerializeField] private float pulseStartFrequency = 8f;
        [Tooltip("Pulse speed at windup completion, radians per second.")] [SerializeField] private float pulseEndFrequency = 32f;
        [Tooltip("Lid tint and emission colour at the end of windup.")] [SerializeField] private Color windupShieldColor = new Color(2f, 0.25f, 0.02f);
        [Tooltip("Lid emission strength at windup completion.")] [SerializeField] private float windupShieldIntensity = 4f;
        [Tooltip("Telegraph light intensity at windup start.")] [SerializeField] private float telegraphLightStartIntensity = 1.5f;
        [Tooltip("Telegraph light intensity at windup completion.")] [SerializeField] private float telegraphLightEndIntensity = 6f;
        [Tooltip("Impact fallback in root local coordinates.")] [SerializeField] private Vector3 impactFallback = new Vector3(0f, 0f, 1.6f);
        [Tooltip("Shield fallback in root local coordinates.")] [SerializeField] private Vector3 shieldFallback = new Vector3(0f, 1.1f, 0.6f);
        [Tooltip("Head fallback in root local coordinates.")] [SerializeField] private Vector3 headFallback = new Vector3(0f, 1.9f, 0f);
        [Tooltip("Death fallback in root local coordinates.")] [SerializeField] private Vector3 deathFallback = new Vector3(0f, 1f, 0f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int GuardDownId = Animator.StringToHash("GuardDown");
        private static readonly int StunnedId = Animator.StringToHash("Stunned");
        private static readonly int WindupId = Animator.StringToHash("Windup");
        private static readonly int SlamId = Animator.StringToHash("Slam");
        private static readonly int HitId = Animator.StringToHash("Hit");
        private static readonly int DieId = Animator.StringToHash("Die");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int AreaColorId = Shader.PropertyToID("_AreaColor");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int ImpactColorId = Shader.PropertyToID("_ImpactColor");
        private static readonly int ChargeId = Shader.PropertyToID("_Charge");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");
        private static readonly int BorderSizeId = Shader.PropertyToID("_BorderSize");
        private static readonly int MinUpDotId = Shader.PropertyToID("_MinUpDot");
        private static readonly int NearFadeId = Shader.PropertyToID("_NearFade");
        private MaterialPropertyBlock _block, _areaBlock;
        private Color[] _tints, _tintEmission, _glows, _glowEmission;
        private Collider[] _hitboxes;
        private Collider[] _colliders;
        private Renderer _shieldRenderer;
        private readonly RaycastHit[] _groundHits = new RaycastHit[32];
        private Color _shieldTint, _shieldEmission;
        private bool _dead, _guardLowered, _stunned, _elite, _windup, _poseActive;
        private float _windupDuration, _windupElapsed, _phase, _flashRemaining, _blockRemaining;
        private float _areaLength, _areaWidth, _areaHeight, _impactRemaining, _poseElapsed, _poseSeconds;
        private Color _flashColor;
        private Vector3 _poseStart, _poseEnd;
        private Vector3 _areaOrigin, _areaForward;
        private Quaternion _rotationStart, _rotationEnd;
        private Quaternion _areaFrame = Quaternion.identity;

        /// <summary>World slam impact origin with a forward fallback.</summary>
        public Vector3 SlamImpactPosition => slamImpactSocket != null ? slamImpactSocket.position : Fallback(impactFallback);
        /// <summary>World shield centre with a torso-height fallback.</summary>
        public Vector3 ShieldCenterPosition => shieldCenterSocket != null ? shieldCenterSocket.position : Fallback(shieldFallback);
        /// <summary>World head position with an authored-height fallback.</summary>
        public Vector3 HeadPosition => headSocket != null ? headSocket.position : Fallback(headFallback);
        /// <summary>World death effect position with a torso fallback.</summary>
        public Vector3 DeathFxPosition => deathFxSocket != null ? deathFxSocket.position : Fallback(deathFallback);

        /// <summary>Ground plane selected once for the current area telegraph.</summary>
        public float SlamAreaGroundHeight => _areaOrigin.y;

        private Vector3 Fallback(Vector3 offset) => transform.position + transform.rotation * offset;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _areaBlock = new MaterialPropertyBlock();
            CacheColors(tintRenderers, out _tints, out _tintEmission);
            CacheColors(glowRenderers, out _glows, out _glowEmission);
            _hitboxes = hitboxRoot != null ? hitboxRoot.GetComponentsInChildren<Collider>(true) : new Collider[0];
            _colliders = GetComponentsInChildren<Collider>(true);
            _shieldRenderer = shieldPivot != null ? shieldPivot.GetComponentInChildren<Renderer>(true) : null;
            Material shieldMaterial = _shieldRenderer != null ? _shieldRenderer.sharedMaterial : null;
            _shieldTint = shieldMaterial != null && shieldMaterial.HasProperty(BaseColorId) ? shieldMaterial.GetColor(BaseColorId) : Color.white;
            _shieldEmission = shieldMaterial != null && shieldMaterial.HasProperty(EmissionId) ? shieldMaterial.GetColor(EmissionId) : Color.black;
            ApplyHitboxes();
            Render();
        }

        private void Update()
        {
            if (GamePause.IsSimulationBlocked || _dead) return;
            float dt = Time.deltaTime;
            _flashRemaining = Mathf.Max(0f, _flashRemaining - dt);
            _blockRemaining = Mathf.Max(0f, _blockRemaining - dt);
            _impactRemaining = Mathf.Max(0f, _impactRemaining - dt);
            if (_windup)
            {
                _windupElapsed += dt;
                _phase += Mathf.Lerp(pulseStartFrequency, pulseEndFrequency, Progress) * dt;
            }
            if (animator == null && shieldPivot != null && _poseActive)
            {
                _poseElapsed += dt;
                float t = _poseSeconds > 0f ? Mathf.Clamp01(_poseElapsed / _poseSeconds) : 1f;
                shieldPivot.localPosition = Vector3.Lerp(_poseStart, _poseEnd, t);
                shieldPivot.localRotation = Quaternion.Slerp(_rotationStart, _rotationEnd, t);
                _poseActive = t < 1f;
            }
            Render();
        }

        private float Progress => _windupDuration > 0f ? Mathf.Clamp01(_windupElapsed / _windupDuration) : 1f;

        /// <summary>Restores visible art, hitboxes and guard, clearing transient state and tints.</summary>
        public void ResetVisual()
        {
            _dead = _stunned = _guardLowered = _elite = false;
            _flashRemaining = _blockRemaining = _impactRemaining = 0f;
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            SetVisibility(tintRenderers, true);
            SetVisibility(glowRenderers, true);
            if (hitboxRoot != null) hitboxRoot.gameObject.SetActive(true);
            if (animator != null)
            {
                animator.Rebind();
                animator.SetBool(GuardDownId, false);
                animator.SetBool(StunnedId, false);
                animator.SetFloat(SpeedId, 0f);
            }
            EndSlamTelegraph();
            Pose(guardPosition, guardEuler, 0f);
            ApplyHitboxes();
            Render();
        }

        /// <summary>Lowers or raises the lid and disables or enables its independent hitbox.</summary>
        public void SetGuardLowered(bool lowered)
        {
            if (_guardLowered == lowered) return;
            _guardLowered = lowered;
            ApplyGuard();
        }

        /// <summary>Locks a complete ground outline in world space and grows the charge fill toward its far edge.
        /// The area is projected onto every upward-facing surface within <paramref name="areaHeight"/> above or below
        /// its ground plane, matching the slam's vertical damage tolerance.</summary>
        public void BeginSlamTelegraph(float windupDuration, Vector3 areaOrigin, Vector3 areaForward,
            float areaLength, float areaWidth, float areaHeight)
        {
            if (_dead) return;
            _windup = true;
            _impactRemaining = 0f;
            _windupElapsed = _phase = 0f;
            _windupDuration = Mathf.Max(0f, windupDuration);
            _areaLength = Mathf.Max(0f, areaLength);
            _areaWidth = Mathf.Max(0f, areaWidth);
            _areaHeight = Mathf.Max(0.01f, areaHeight);
            _areaForward = Vector3.ProjectOnPlane(areaForward, Vector3.up).normalized;
            if (_areaForward.sqrMagnitude < 0.000001f) _areaForward = Vector3.forward;
            _areaOrigin = areaOrigin;
            _areaOrigin.y = transform.position.y;
            Vector3 centre = _areaOrigin + _areaForward * (_areaLength * 0.5f);
            int count = Physics.RaycastNonAlloc(centre + Vector3.up * groundProbeHeight, Vector3.down,
                _groundHits, groundProbeDistance, GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _groundHits[i].collider;
                if (hit == null || ContainsCollider(hit) || _groundHits[i].distance >= nearest) continue;
                nearest = _groundHits[i].distance;
                _areaOrigin.y = _groundHits[i].point.y;
            }
            _areaFrame = Quaternion.LookRotation(_areaForward, Vector3.up);
            if (animator != null) animator.SetTrigger(WindupId);
            Pose(overheadPosition, overheadEuler, _windupDuration);
            Render();
        }

        private bool ContainsCollider(Collider candidate)
        {
            if (_colliders == null) return false;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] == candidate) return true;
            return false;
        }

        /// <summary>Flashes the full cached rectangle, then fades independently of the charge visuals.</summary>
        public void PlaySlamImpact()
        {
            if (_dead) return;
            _impactRemaining = Mathf.Max(0f, areaImpactDuration);
            Render();
        }

        /// <summary>Hides windup effects on strike, cancellation, stun or death.</summary>
        public void EndSlamTelegraph()
        {
            _windup = false;
            if (telegraphLight != null) telegraphLight.enabled = false;
            if (animator != null) animator.ResetTrigger(WindupId);
            ApplyGuard();
            Render();
        }

        /// <summary>Ends windup and brings the placeholder lid down or triggers the model strike.</summary>
        public void PlaySlamStrike()
        {
            EndSlamTelegraph();
            if (animator != null) animator.SetTrigger(SlamId);
            Pose(strikePosition, strikeEuler, poseDuration);
        }

        /// <summary>Flashes only the shield; combat owns the hit's VFX and audio.</summary>
        public void PlayBlockFeedback(Vector3 point, Vector3 normal)
        {
            _blockRemaining = blockFlashDuration;
            Render();
        }

        /// <summary>Displays a white ordinary hit flash or a longer gold critical flash.</summary>
        public void FlashHit(bool critical)
        {
            _flashRemaining = critical ? criticalFlashDuration : hitFlashDuration;
            _flashColor = critical ? criticalColor : hitColor;
            if (animator != null) animator.SetTrigger(HitId);
            Render();
        }

        /// <summary>Displays stun, cancelling the telegraph and opening the guard while stunned.</summary>
        public void SetStunned(bool stunned)
        {
            if (_stunned == stunned) return;
            _stunned = stunned;
            if (animator != null) animator.SetBool(StunnedId, stunned);
            if (stunned) EndSlamTelegraph();
            ApplyGuard();
        }

        /// <summary>Updates the optional walking animation without lookups or allocations.</summary>
        public void SetMoveSpeed01(float normalizedSpeed)
        {
            if (animator != null) animator.SetFloat(SpeedId, Mathf.Clamp01(normalizedSpeed));
        }

        /// <summary>Enables or clears the elite tint and glow.</summary>
        public void SetElite(bool elite) { _elite = elite; Render(); }

        /// <summary>Hides art and telegraphs and disables every independent hitbox.</summary>
        public void PlayDeath()
        {
            _dead = true;
            _impactRemaining = 0f;
            EndSlamTelegraph();
            if (animator != null) animator.SetTrigger(DieId);
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            SetVisibility(tintRenderers, false);
            SetVisibility(glowRenderers, false);
            ApplyHitboxes();
        }

        private void ApplyGuard()
        {
            bool down = _guardLowered || _stunned;
            if (animator != null) animator.SetBool(GuardDownId, down);
            if (!_windup) Pose(down ? loweredPosition : guardPosition, down ? loweredEuler : guardEuler, poseDuration);
            SetShieldColliders(!_dead && !down);
        }

        private void ApplyHitboxes()
        {
            if (_hitboxes != null)
                for (int i = 0; i < _hitboxes.Length; i++)
                    if (_hitboxes[i] != null) _hitboxes[i].enabled = !_dead;
            SetShieldColliders(!_dead && !_guardLowered && !_stunned);
        }

        private void SetShieldColliders(bool enabled)
        {
            if (shieldColliders == null) return;
            for (int i = 0; i < shieldColliders.Length; i++)
                if (shieldColliders[i] != null) shieldColliders[i].enabled = enabled;
        }

        private void Pose(Vector3 position, Vector3 euler, float seconds)
        {
            if (animator != null || shieldPivot == null) return;
            _poseStart = shieldPivot.localPosition;
            _rotationStart = shieldPivot.localRotation;
            _poseEnd = position;
            _rotationEnd = Quaternion.Euler(euler);
            _poseElapsed = 0f;
            _poseSeconds = seconds;
            _poseActive = true;
            if (seconds > 0f) return;
            shieldPivot.SetLocalPositionAndRotation(position, _rotationEnd);
            _poseActive = false;
        }

        private void Render()
        {
            if (_block == null) return;
            ApplyArt(tintRenderers, _tints, _tintEmission, false);
            ApplyArt(glowRenderers, _glows, _glowEmission, true);
            if (_shieldRenderer != null)
            {
                // Restore even when a replacement lid is omitted from the optional renderer arrays.
                Color shieldColor = _elite ? Color.Lerp(_shieldTint, eliteColor, eliteTintStrength) : _shieldTint;
                Color shieldLight = _elite ? eliteColor * eliteGlowIntensity : _shieldEmission;
                if (_flashRemaining > 0f) shieldColor = _flashColor;
                if (_blockRemaining > 0f) { shieldColor = blockColor; shieldLight = blockColor; }
                ApplyWindupTint(ref shieldColor, ref shieldLight);
                Tint(_shieldRenderer, shieldColor, shieldLight);
            }
            bool show = _windup && !_dead;
            if (telegraphLight != null)
            {
                telegraphLight.enabled = show;
                if (show)
                {
                    telegraphLight.color = windupStartColor;
                    telegraphLight.intensity = Mathf.Lerp(telegraphLightStartIntensity, telegraphLightEndIntensity, Progress);
                    telegraphLight.transform.position = ShieldCenterPosition;
                }
            }
            RenderArea(show);
        }

        /// <summary>Drives the single projector box: charge (background, fill, border) while winding up, then the impact
        /// flash fading out during recovery. The box covers the area footprint and +/- the slam's height tolerance.</summary>
        private void RenderArea(bool show)
        {
            if (slamAreaProjector == null) return;
            float impact = !_dead && _impactRemaining > 0f && areaImpactDuration > 0f
                ? Mathf.Clamp01(_impactRemaining / areaImpactDuration) : 0f;
            bool visible = show || impact > 0f;
            GameObject node = slamAreaProjector.gameObject;
            if (node.activeSelf != visible) node.SetActive(visible);
            if (!visible) return;

            // Re-pin the cached world box every frame: the enemy may move or turn while the impact flash fades.
            Transform box = slamAreaProjector.transform;
            box.SetPositionAndRotation(_areaOrigin + _areaForward * (_areaLength * 0.5f), _areaFrame);
            box.localScale = Vector3.one;
            Vector3 inherited = box.lossyScale;
            box.localScale = new Vector3(_areaWidth / Mathf.Max(0.000001f, Mathf.Abs(inherited.x)),
                _areaHeight * 2f / Mathf.Max(0.000001f, Mathf.Abs(inherited.y)),
                _areaLength / Mathf.Max(0.000001f, Mathf.Abs(inherited.z)));

            slamAreaProjector.GetPropertyBlock(_areaBlock);
            _areaBlock.SetColor(AreaColorId, areaOutlineColor);
            _areaBlock.SetColor(BorderColorId, areaBorderColor);
            _areaBlock.SetColor(FillColorId, areaFillColor);
            _areaBlock.SetColor(ImpactColorId, areaImpactColor);
            _areaBlock.SetFloat(ChargeId, show ? 1f : 0f);
            _areaBlock.SetFloat(FillId, show ? Progress : 0f);
            _areaBlock.SetFloat(ImpactId, impact);
            // Shader border units: lateral runs 0..1 from centre line to side edge, forward 0..1 near to far.
            _areaBlock.SetVector(BorderSizeId, new Vector4(
                areaBorderWidth * 2f / Mathf.Max(0.0001f, _areaWidth), areaBorderWidth / Mathf.Max(0.0001f, _areaLength), 0f, 0f));
            _areaBlock.SetFloat(MinUpDotId, Mathf.Cos(areaMaxSurfaceAngle * Mathf.Deg2Rad));
            _areaBlock.SetFloat(NearFadeId, areaCameraNearFade);
            slamAreaProjector.SetPropertyBlock(_areaBlock);
        }

        private void ApplyArt(Renderer[] renderers, Color[] colors, Color[] emission, bool glow)
        {
            if (renderers == null || colors == null) return;
            for (int i = 0; i < renderers.Length && i < colors.Length; i++)
            {
                // The shield has its own feedback pass; glow renderers are drawn only in the glow pass.
                if (renderers[i] == null || renderers[i] == _shieldRenderer
                    || (!glow && Contains(glowRenderers, renderers[i]))) continue;
                Color color = _elite ? Color.Lerp(colors[i], eliteColor, eliteTintStrength) : colors[i];
                Color light = _elite && glow ? eliteColor * eliteGlowIntensity : emission[i];
                if (_flashRemaining > 0f) color = _flashColor;
                if (glow && _blockRemaining > 0f) { color = blockColor; light = blockColor; }
                if (glow) ApplyWindupTint(ref color, ref light);
                Tint(renderers[i], color, light);
            }
        }

        private void ApplyWindupTint(ref Color color, ref Color emission)
        {
            if (!_windup || _dead) return;
            float pulse = (Mathf.Sin(_phase) + 1f) * 0.5f;
            float ramp = Mathf.Lerp(0.35f, 1f, Progress);
            Color amberRed = Color.Lerp(windupStartColor, windupShieldColor, Progress);
            color = Color.Lerp(color, amberRed, ramp * Mathf.Lerp(0.5f, 1f, pulse));
            emission = amberRed * (windupShieldIntensity * ramp * Mathf.Lerp(0.5f, 1f, pulse));
        }

        private static bool Contains(Renderer[] renderers, Renderer renderer)
        {
            if (renderers == null) return false;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == renderer) return true;
            return false;
        }

        private void Tint(Renderer renderer, Color color, Color emission)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _block.SetColor(EmissionId, emission);
            renderer.SetPropertyBlock(_block);
        }

        private static void CacheColors(Renderer[] renderers, out Color[] colors, out Color[] emission)
        {
            int count = renderers != null ? renderers.Length : 0;
            colors = new Color[count];
            emission = new Color[count];
            for (int i = 0; i < count; i++)
            {
                Material mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
                colors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
                emission[i] = mat != null && mat.HasProperty(EmissionId) ? mat.GetColor(EmissionId) : Color.black;
            }
        }

        private static void SetVisibility(Renderer[] renderers, bool visible)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }
    }
}
