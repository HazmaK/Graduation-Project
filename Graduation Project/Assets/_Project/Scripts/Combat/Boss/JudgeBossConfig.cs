using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Serialized timing, selection and driver tuning for The Judge encounter.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Combat/Judge Boss Config", fileName = "JudgeBossConfig")]
    public sealed class JudgeBossConfig : ScriptableObject
    {
        [Header("Verdict")]
        [Tooltip("Verdict Windup in seconds.")]
        [SerializeField] private float verdictWindup = 0.6f;
        /// <summary>Verdict Windup in seconds.</summary>
        public float VerdictWindup => verdictWindup;

        [Tooltip("Verdict Active in seconds.")]
        [SerializeField] private float verdictActive = 0.1f;
        /// <summary>Verdict Active in seconds.</summary>
        public float VerdictActive => verdictActive;

        [Tooltip("Verdict Recover in seconds.")]
        [SerializeField] private float verdictRecover = 0.6f;
        /// <summary>Verdict Recover in seconds.</summary>
        public float VerdictRecover => verdictRecover;

        [Header("Volley")]
        [Tooltip("Volley Windup in seconds.")]
        [SerializeField] private float volleyWindup = 0.8f;
        /// <summary>Volley Windup in seconds.</summary>
        public float VolleyWindup => volleyWindup;

        [Tooltip("Volley Active in seconds.")]
        [SerializeField] private float volleyActive = 0.1f;
        /// <summary>Volley Active in seconds.</summary>
        public float VolleyActive => volleyActive;

        [Tooltip("Volley Recover in seconds.")]
        [SerializeField] private float volleyRecover = 0.8f;
        /// <summary>Volley Recover in seconds.</summary>
        public float VolleyRecover => volleyRecover;

        [Header("Guillotine")]
        [SerializeField, Min(0f)] private float guillotineHitHeight = 12f;
        /// <summary>Height of the falling strike's damage column above its marked floor.</summary>
        public float GuillotineHitHeight => guillotineHitHeight;

        [Tooltip("Guillotine Windup in seconds.")]
        [SerializeField] private float guillotineWindup = 0.5f;
        /// <summary>Guillotine Windup in seconds.</summary>
        public float GuillotineWindup => guillotineWindup;

        [Tooltip("Guillotine Strike Interval in seconds.")]
        [SerializeField] private float guillotineStrikeInterval = 0.45f;
        /// <summary>Guillotine Strike Interval in seconds.</summary>
        public float GuillotineStrikeInterval => guillotineStrikeInterval;

        [Tooltip("Guillotine Strikes.")]
        [SerializeField] private int guillotineStrikes = 3;
        /// <summary>Guillotine Strikes.</summary>
        public int GuillotineStrikes => guillotineStrikes;

        [Tooltip("Guillotine Recover in seconds.")]
        [SerializeField] private float guillotineRecover = 0.6f;
        /// <summary>Guillotine Recover in seconds.</summary>
        public float GuillotineRecover => guillotineRecover;

        [Header("Scale")]
        [Tooltip("Scale Windup in seconds.")]
        [SerializeField] private float scaleWindup = 1.0f;
        /// <summary>Scale Windup in seconds.</summary>
        public float ScaleWindup => scaleWindup;

        [Tooltip("Scale Active in seconds.")]
        [SerializeField] private float scaleActive = 3.0f;
        /// <summary>Scale Active in seconds.</summary>
        public float ScaleActive => scaleActive;

        [Tooltip("Scale Recover in seconds.")]
        [SerializeField] private float scaleRecover = 0.8f;
        /// <summary>Scale Recover in seconds.</summary>
        public float ScaleRecover => scaleRecover;

        [Header("Repulse (Kursu Darbesi)")]
        [SerializeField, Min(0f)] private float repulseTriggerRadius = 6f;
        /// <summary>Strict horizontal proximity threshold for accumulating close time.</summary>
        public float RepulseTriggerRadius => repulseTriggerRadius;
        [SerializeField, Min(0.01f)] private float repulseCloseTime = 1.2f;
        /// <summary>Seconds of close pressure required; decays equally fast outside the threshold.</summary>
        public float RepulseCloseTime => repulseCloseTime;
        [SerializeField, Min(0f)] private float repulseCooldown = 3.5f;
        /// <summary>Seconds from the previous burst's end before another warning may start.</summary>
        public float RepulseCooldown => repulseCooldown;
        [SerializeField, Min(0f)] private float repulseTelegraph = 0.7f;
        /// <summary>Duration of the expanding red ground warning.</summary>
        public float RepulseTelegraph => repulseTelegraph;
        [SerializeField, Min(0f)] private float repulseRecover = 0.6f;
        /// <summary>Recovery after the instantaneous burst.</summary>
        public float RepulseRecover => repulseRecover;
        [SerializeField, Min(0f)] private float repulseRadius = 7.5f;
        /// <summary>Horizontal burst and warning radius in metres.</summary>
        public float RepulseRadius => repulseRadius;
        [SerializeField, Min(0f)] private float repulseDamage = 20f;
        /// <summary>Damage through the existing unparryable player health path.</summary>
        public float RepulseDamage => repulseDamage;
        [SerializeField, Min(0f)] private float repulseKnockback = 16f;
        /// <summary>Outward horizontal impulse speed.</summary>
        public float RepulseKnockback => repulseKnockback;
        [SerializeField, Min(0f)] private float repulseUpSpeed = 3f;
        /// <summary>Small upward component of the burst impulse.</summary>
        public float RepulseUpSpeed => repulseUpSpeed;
        [SerializeField, Min(0f)] private float repulseHeightAboveBoss = 3f;
        /// <summary>Damage volume extends this far above the boss root pivot.</summary>
        public float RepulseHeightAboveBoss => repulseHeightAboveBoss;
        [SerializeField, Min(0f)] private float repulseMarkerLift = 0.04f;
        /// <summary>Warning lift above the sampled floor to avoid z fighting.</summary>
        public float RepulseMarkerLift => repulseMarkerLift;
        [SerializeField, Min(0.001f)] private float repulseMarkerThickness = 0.05f;
        /// <summary>Warning ring width and fill thickness in metres.</summary>
        public float RepulseMarkerThickness => repulseMarkerThickness;
        [SerializeField, Min(8)] private int repulseMarkerSegments = 64;
        /// <summary>Number of prototype warning outline segments generated by the builder.</summary>
        public int RepulseMarkerSegments => repulseMarkerSegments;

        [Header("Reap")]
        [Tooltip("Reap Windup in seconds.")]
        [SerializeField] private float reapWindup = 0.55f;
        /// <summary>Reap Windup in seconds.</summary>
        public float ReapWindup => reapWindup;

        [Tooltip("Reap Active in seconds.")]
        [SerializeField] private float reapActive = 0.2f;
        /// <summary>Reap Active in seconds.</summary>
        public float ReapActive => reapActive;

        [Tooltip("Reap Recover in seconds.")]
        [SerializeField] private float reapRecover = 0.6f;
        /// <summary>Reap Recover in seconds.</summary>
        public float ReapRecover => reapRecover;

        [Header("Execution Strike")]
        [Tooltip("Execution Strike Windup in seconds.")]
        [SerializeField] private float executionStrikeWindup = 0.8f;
        /// <summary>Execution Strike Windup in seconds.</summary>
        public float ExecutionStrikeWindup => executionStrikeWindup;

        [Tooltip("Execution Strike Active in seconds.")]
        [SerializeField] private float executionStrikeActive = 0.15f;
        /// <summary>Execution Strike Active in seconds.</summary>
        public float ExecutionStrikeActive => executionStrikeActive;

        [Tooltip("Execution Strike Recover in seconds.")]
        [SerializeField] private float executionStrikeRecover = 1.5f;
        /// <summary>Execution Strike Recover in seconds.</summary>
        public float ExecutionStrikeRecover => executionStrikeRecover;

        [Header("Thrust")]
        [Tooltip("Thrust Windup in seconds.")]
        [SerializeField] private float thrustWindup = 0.5f;
        /// <summary>Thrust Windup in seconds.</summary>
        public float ThrustWindup => thrustWindup;

        [Tooltip("Thrust Active in seconds.")]
        [SerializeField] private float thrustActive = 0.5f;
        /// <summary>Thrust Active in seconds.</summary>
        public float ThrustActive => thrustActive;

        [Tooltip("Thrust Recover in seconds.")]
        [SerializeField] private float thrustRecover = 0.7f;
        /// <summary>Thrust Recover in seconds.</summary>
        public float ThrustRecover => thrustRecover;

        [Header("Encounter Timing and Ranges")]
        [SerializeField, Min(0f)] private float engageDelay = 3f;
        /// <summary>Seconds from engagement to the first Trial telegraph.</summary>
        public float EngageDelay => engageDelay;

        [SerializeField, Min(0f)] private float kneelGrace = 0.6f;
        /// <summary>Seconds during which damage cannot finish a newly kneeling boss.</summary>
        public float KneelGrace => kneelGrace;

        [SerializeField, Min(0f)] private float thrustStopDistance = 2.5f;
        /// <summary>Minimum horizontal distance at which a Thrust stops advancing.</summary>
        public float ThrustStopDistance => thrustStopDistance;

        [SerializeField, Min(0f)] private float executionMinSeparation = 2.2f;
        /// <summary>Minimum horizontal separation restored during Execution Idle and Recover.</summary>
        public float ExecutionMinSeparation => executionMinSeparation;

        [SerializeField, Min(0.01f)] private float stunSinkDuration = 0.5f;
        /// <summary>Seconds to descend from the current pivot height to Execution height on stun.</summary>
        public float StunSinkDuration => stunSinkDuration;

        [Tooltip("Trial Idle Gap Min in seconds.")]
        [SerializeField] private float trialIdleGapMin = 1.0f;
        /// <summary>Trial Idle Gap Min in seconds.</summary>
        public float TrialIdleGapMin => trialIdleGapMin;

        [Tooltip("Trial Idle Gap Max in seconds.")]
        [SerializeField] private float trialIdleGapMax = 1.6f;
        /// <summary>Trial Idle Gap Max in seconds.</summary>
        public float TrialIdleGapMax => trialIdleGapMax;

        [Tooltip("Execution Idle Gap Min in seconds.")]
        [SerializeField] private float executionIdleGapMin = 0.6f;
        /// <summary>Execution Idle Gap Min in seconds.</summary>
        public float ExecutionIdleGapMin => executionIdleGapMin;

        [Tooltip("Execution Idle Gap Max in seconds.")]
        [SerializeField] private float executionIdleGapMax = 1.0f;
        /// <summary>Execution Idle Gap Max in seconds.</summary>
        public float ExecutionIdleGapMax => executionIdleGapMax;

        [Tooltip("Scale Max Range.")]
        [SerializeField] private float scaleMaxRange = 14f;
        /// <summary>Scale Max Range.</summary>
        public float ScaleMaxRange => scaleMaxRange;

        [Tooltip("Thrust Min Range.")]
        [SerializeField] private float thrustMinRange = 9f;
        /// <summary>Thrust Min Range.</summary>
        public float ThrustMinRange => thrustMinRange;

        [Tooltip("Guillotine Return Fraction from zero to one.")]
        [SerializeField] private float guillotineReturnFraction = 0.5f;
        /// <summary>Guillotine Return Fraction from zero to one.</summary>
        public float GuillotineReturnFraction => guillotineReturnFraction;

        [Tooltip("Last Word Fraction from zero to one.")]
        [SerializeField] private float lastWordFraction = 0.25f;
        /// <summary>Last Word Fraction from zero to one.</summary>
        public float LastWordFraction => lastWordFraction;

        [Tooltip("Last Word Chain Gap in seconds.")]
        [SerializeField] private float lastWordChainGap = 0.25f;
        /// <summary>Last Word Chain Gap in seconds.</summary>
        public float LastWordChainGap => lastWordChainGap;

        [Tooltip("Last Word Final Recover in seconds.")]
        [SerializeField] private float lastWordFinalRecover = 2.0f;
        /// <summary>Last Word Final Recover in seconds.</summary>
        public float LastWordFinalRecover => lastWordFinalRecover;

        [Tooltip("Transition Duration in seconds.")]
        [SerializeField] private float transitionDuration = 3.0f;
        /// <summary>Transition Duration in seconds.</summary>
        public float TransitionDuration => transitionDuration;

        [Tooltip("Stun Immunity in seconds.")]
        [SerializeField] private float stunImmunity = 6.0f;
        /// <summary>Stun Immunity in seconds.</summary>
        public float StunImmunity => stunImmunity;

        [Header("Selection Weights")]
        [Tooltip("Verdict Weight for weighted attack selection.")]
        [SerializeField] private float verdictWeight = 3f;
        /// <summary>Verdict Weight for weighted attack selection.</summary>
        public float VerdictWeight => verdictWeight;

        [Tooltip("Volley Weight for weighted attack selection.")]
        [SerializeField] private float volleyWeight = 2f;
        /// <summary>Volley Weight for weighted attack selection.</summary>
        public float VolleyWeight => volleyWeight;

        [Tooltip("Trial Guillotine Weight for weighted attack selection.")]
        [SerializeField] private float trialGuillotineWeight = 2f;
        /// <summary>Trial Guillotine Weight for weighted attack selection.</summary>
        public float TrialGuillotineWeight => trialGuillotineWeight;

        [Tooltip("Scale Weight for weighted attack selection.")]
        [SerializeField] private float scaleWeight = 1.5f;
        /// <summary>Scale Weight for weighted attack selection.</summary>
        public float ScaleWeight => scaleWeight;

        [Tooltip("Reap Weight for weighted attack selection.")]
        [SerializeField] private float reapWeight = 3f;
        /// <summary>Reap Weight for weighted attack selection.</summary>
        public float ReapWeight => reapWeight;

        [Tooltip("Execution Strike Weight for weighted attack selection.")]
        [SerializeField] private float executionStrikeWeight = 2f;
        /// <summary>Execution Strike Weight for weighted attack selection.</summary>
        public float ExecutionStrikeWeight => executionStrikeWeight;

        [Tooltip("Execution Guillotine Weight for weighted attack selection.")]
        [SerializeField] private float executionGuillotineWeight = 1.5f;
        /// <summary>Execution Guillotine Weight for weighted attack selection.</summary>
        public float ExecutionGuillotineWeight => executionGuillotineWeight;

        [Header("Health and Movement")]
        [Tooltip("Phase1 Health.")]
        [SerializeField] private float phase1Health = 4500f;
        /// <summary>Phase1 Health.</summary>
        public float Phase1Health => phase1Health;

        [Tooltip("Phase2 Health.")]
        [SerializeField] private float phase2Health = 5500f;
        /// <summary>Phase2 Health.</summary>
        public float Phase2Health => phase2Health;

        [Tooltip("Hover Height Trial.")]
        [SerializeField] private float hoverHeightTrial = 4.5f;
        /// <summary>Hover Height Trial.</summary>
        public float HoverHeightTrial => hoverHeightTrial;

        [Tooltip("Hover Height Execution.")]
        [SerializeField] private float hoverHeightExecution = 0.5f;
        /// <summary>Hover Height Execution.</summary>
        public float HoverHeightExecution => hoverHeightExecution;

        [SerializeField, Min(0.01f)] private float executionClimbSpeed = 6f;
        /// <summary>Execution vertical speed while floating above terrain.</summary>
        public float ExecutionClimbSpeed => executionClimbSpeed;

        [SerializeField, Min(0f)] private float maxFloatOverHeight = 2f;
        /// <summary>Maximum obstacle top height above the arena floor that can be floated over.</summary>
        public float MaxFloatOverHeight => maxFloatOverHeight;

        [SerializeField, Min(0.01f)] private float executionIdleWatchdog = 4f;
        /// <summary>Maximum Execution idle time before a forced Guillotine.</summary>
        public float ExecutionIdleWatchdog => executionIdleWatchdog;

        [Tooltip("Move Speed Trial.")]
        [SerializeField] private float moveSpeedTrial = 3f;
        /// <summary>Move Speed Trial.</summary>
        public float MoveSpeedTrial => moveSpeedTrial;

        [Tooltip("Move Speed Execution.")]
        [SerializeField] private float moveSpeedExecution = 7f;
        /// <summary>Move Speed Execution.</summary>
        public float MoveSpeedExecution => moveSpeedExecution;

        [Tooltip("Turn Speed Degrees.")]
        [SerializeField] private float turnSpeedDegrees = 240f;
        /// <summary>Turn Speed Degrees.</summary>
        public float TurnSpeedDegrees => turnSpeedDegrees;

        [Tooltip("Preferred Distance Trial.")]
        [SerializeField] private float preferredDistanceTrial = 12f;
        /// <summary>Preferred Distance Trial.</summary>
        public float PreferredDistanceTrial => preferredDistanceTrial;

        [Header("Sword Projectiles")]
        [Tooltip("Sword Speed.")]
        [SerializeField] private float swordSpeed = 26f;
        /// <summary>Sword Speed.</summary>
        public float SwordSpeed => swordSpeed;

        [Tooltip("Sword Damage.")]
        [SerializeField] private float swordDamage = 20f;
        /// <summary>Sword Damage.</summary>
        public float SwordDamage => swordDamage;

        [Tooltip("Volley Count.")]
        [SerializeField] private int volleyCount = 5;
        /// <summary>Volley Count.</summary>
        public int VolleyCount => volleyCount;

        [Tooltip("Volley Spread Degrees.")]
        [SerializeField] private float volleySpreadDegrees = 50f;
        /// <summary>Volley Spread Degrees.</summary>
        public float VolleySpreadDegrees => volleySpreadDegrees;

        [Header("Guillotine Effects")]
        [Tooltip("Guillotine Damage.")]
        [SerializeField] private float guillotineDamage = 30f;
        /// <summary>Guillotine Damage.</summary>
        public float GuillotineDamage => guillotineDamage;

        [Tooltip("Guillotine Radius.")]
        [SerializeField] private float guillotineRadius = 2.2f;
        /// <summary>Guillotine Radius.</summary>
        public float GuillotineRadius => guillotineRadius;

        [Tooltip("Guillotine Fall Delay in seconds.")]
        [SerializeField] private float guillotineFallDelay = 0.9f;
        /// <summary>Guillotine Fall Delay in seconds.</summary>
        public float GuillotineFallDelay => guillotineFallDelay;

        [Header("Scale Effects")]
        [Tooltip("Scale Damage.")]
        [SerializeField] private float scaleDamage = 25f;
        /// <summary>Scale Damage.</summary>
        public float ScaleDamage => scaleDamage;

        [Tooltip("Scale Length.")]
        [SerializeField] private float scaleLength = 16f;
        /// <summary>Scale Length.</summary>
        public float ScaleLength => scaleLength;

        [Tooltip("Scale Angular Speed Degrees.")]
        [SerializeField] private float scaleAngularSpeedDegrees = 120f;
        /// <summary>Scale Angular Speed Degrees.</summary>
        public float ScaleAngularSpeedDegrees => scaleAngularSpeedDegrees;

        [Header("Reap Effects")]
        [Tooltip("Reap Damage.")]
        [SerializeField] private float reapDamage = 30f;
        /// <summary>Reap Damage.</summary>
        public float ReapDamage => reapDamage;

        [Tooltip("Reap Range.")]
        [SerializeField] private float reapRange = 6f;
        /// <summary>Reap Range.</summary>
        public float ReapRange => reapRange;

        [Tooltip("Reap Half Angle Degrees.")]
        [SerializeField] private float reapHalfAngleDegrees = 90f;
        /// <summary>Reap Half Angle Degrees.</summary>
        public float ReapHalfAngleDegrees => reapHalfAngleDegrees;

        [Header("Execution Effects")]
        [Tooltip("Execution Damage.")]
        [SerializeField] private float executionDamage = 35f;
        /// <summary>Execution Damage.</summary>
        public float ExecutionDamage => executionDamage;

        [Tooltip("Execution Radius.")]
        [SerializeField] private float executionRadius = 2.5f;
        /// <summary>Execution Radius.</summary>
        public float ExecutionRadius => executionRadius;

        [Tooltip("Shock Line Damage.")]
        [SerializeField] private float shockLineDamage = 20f;
        /// <summary>Shock Line Damage.</summary>
        public float ShockLineDamage => shockLineDamage;

        [Tooltip("Shock Line Speed.")]
        [SerializeField] private float shockLineSpeed = 18f;
        /// <summary>Shock Line Speed.</summary>
        public float ShockLineSpeed => shockLineSpeed;

        [Tooltip("Shock Line Length.")]
        [SerializeField] private float shockLineLength = 20f;
        /// <summary>Shock Line Length.</summary>
        public float ShockLineLength => shockLineLength;

        [Header("Thrust Effects")]
        [Tooltip("Thrust Damage.")]
        [SerializeField] private float thrustDamage = 30f;
        /// <summary>Thrust Damage.</summary>
        public float ThrustDamage => thrustDamage;

        [Tooltip("Thrust Speed.")]
        [SerializeField] private float thrustSpeed = 30f;
        /// <summary>Thrust Speed.</summary>
        public float ThrustSpeed => thrustSpeed;

        [Tooltip("Thrust Distance.")]
        [SerializeField] private float thrustDistance = 15f;
        /// <summary>Thrust Distance.</summary>
        public float ThrustDistance => thrustDistance;

        [Header("Transition and Stun Effects")]
        [Tooltip("Transition Push Speed.")]
        [SerializeField] private float transitionPushSpeed = 14f;
        /// <summary>Transition Push Speed.</summary>
        public float TransitionPushSpeed => transitionPushSpeed;

        [Tooltip("Reflected Stun Duration in seconds.")]
        [SerializeField] private float reflectedStunDuration = 2.0f;
        /// <summary>Reflected Stun Duration in seconds.</summary>
        public float ReflectedStunDuration => reflectedStunDuration;

        [Tooltip("Parry Stun Duration in seconds.")]
        [SerializeField] private float parryStunDuration = 2.2f;
        /// <summary>Parry Stun Duration in seconds.</summary>
        public float ParryStunDuration => parryStunDuration;

        [Tooltip("Ring Sword Count.")]
        [SerializeField] private int ringSwordCount = 6;
        /// <summary>Ring Sword Count.</summary>
        public int RingSwordCount => ringSwordCount;

        [Header("Encounter Identity")]
        [Tooltip("Threat Cost.")]
        [SerializeField] private int threatCost = 10;
        /// <summary>Threat Cost.</summary>
        public int ThreatCost => threatCost;

        [Tooltip("Name Loc Key.")]
        [SerializeField] private string nameLocKey = "boss.judge.name";
        /// <summary>Name Loc Key.</summary>
        public string NameLocKey => nameLocKey;

        /// <summary>Builds an immutable snapshot of the loop's tuning; driver-only values stay on this asset.</summary>
        public JudgeBossLoop.Settings CreateSettings()
        {
            return new JudgeBossLoop.Settings(
                verdictWindup: verdictWindup,
                verdictActive: verdictActive,
                verdictRecover: verdictRecover,
                volleyWindup: volleyWindup,
                volleyActive: volleyActive,
                volleyRecover: volleyRecover,
                guillotineWindup: guillotineWindup,
                guillotineStrikeInterval: guillotineStrikeInterval,
                guillotineStrikes: guillotineStrikes,
                guillotineRecover: guillotineRecover,
                scaleWindup: scaleWindup,
                scaleActive: scaleActive,
                scaleRecover: scaleRecover,
                reapWindup: reapWindup,
                reapActive: reapActive,
                reapRecover: reapRecover,
                executionStrikeWindup: executionStrikeWindup,
                executionStrikeActive: executionStrikeActive,
                executionStrikeRecover: executionStrikeRecover,
                thrustWindup: thrustWindup,
                thrustActive: thrustActive,
                thrustRecover: thrustRecover,
                trialIdleGapMin: trialIdleGapMin,
                trialIdleGapMax: trialIdleGapMax,
                executionIdleGapMin: executionIdleGapMin,
                executionIdleGapMax: executionIdleGapMax,
                scaleMaxRange: scaleMaxRange,
                thrustMinRange: thrustMinRange,
                guillotineReturnFraction: guillotineReturnFraction,
                lastWordFraction: lastWordFraction,
                lastWordChainGap: lastWordChainGap,
                lastWordFinalRecover: lastWordFinalRecover,
                transitionDuration: transitionDuration,
                stunImmunity: stunImmunity,
                verdictWeight: verdictWeight,
                volleyWeight: volleyWeight,
                trialGuillotineWeight: trialGuillotineWeight,
                scaleWeight: scaleWeight,
                reapWeight: reapWeight,
                executionStrikeWeight: executionStrikeWeight,
                executionGuillotineWeight: executionGuillotineWeight,
                engageDelay: engageDelay,
                executionIdleWatchdog: executionIdleWatchdog,
                repulseTriggerRadius: repulseTriggerRadius,
                repulseCloseTime: repulseCloseTime,
                repulseCooldown: repulseCooldown,
                repulseTelegraph: repulseTelegraph,
                repulseRecover: repulseRecover);
        }
    }
}

