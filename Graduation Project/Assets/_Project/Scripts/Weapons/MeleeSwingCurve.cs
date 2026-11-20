using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// One pose of a melee swing in view (camera) space: a grip offset from the viewmodel rest position and the
    /// direction the blade points. The blade's roll is not authored; <see cref="SwingProfile"/> derives it so the
    /// cutting edge always leads the motion.
    /// </summary>
    [System.Serializable]
    public struct SwingKey
    {
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 bladeDirection;

        /// <summary>Creates a key from a grip offset and a blade direction.</summary>
        public SwingKey(Vector3 position, Vector3 bladeDirection)
        {
            this.position = position;
            this.bladeDirection = bladeDirection;
        }

        /// <summary>Grip offset from the rest position.</summary>
        public Vector3 Position => position;

        /// <summary>Normalized blade direction (forward if unset).</summary>
        public Vector3 Direction => bladeDirection.sqrMagnitude > 1e-6f ? bladeDirection.normalized : Vector3.forward;
    }

    /// <summary>
    /// A full swing: wind-up into the chamber pose, a fast strike through the contact pose to the follow-through,
    /// a short hold, then recovery to rest. Phase lengths are fractions of the swing duration.
    /// </summary>
    [System.Serializable]
    public struct SwingProfile
    {
        [SerializeField] private SwingKey chamber;
        [SerializeField] private SwingKey contact;
        [SerializeField] private SwingKey follow;
        [SerializeField, Range(0f, 0.6f)] private float windup;
        [SerializeField, Range(0.05f, 0.8f)] private float strike;
        [SerializeField, Range(0f, 0.5f)] private float hold;
        [SerializeField, Range(-90f, 90f), Tooltip("Turns the blade about its own axis so more of the flat shows.")]
        private float roll;

        /// <summary>Creates a swing from its three keys, phase fractions (the rest is recovery) and blade roll.</summary>
        public SwingProfile(SwingKey chamber, SwingKey contact, SwingKey follow, float windup, float strike, float hold,
            float roll = 0f)
        {
            this.roll = roll;
            this.chamber = chamber;
            this.contact = contact;
            this.follow = follow;
            this.windup = windup;
            this.strike = strike;
            this.hold = hold;
        }

        /// <summary>Pose reached at the end of the wind-up.</summary>
        public SwingKey Chamber => chamber;
        /// <summary>Pose the strike passes through halfway along its path.</summary>
        public SwingKey Contact => contact;
        /// <summary>Pose at the end of the strike.</summary>
        public SwingKey Follow => follow;
        /// <summary>Wind-up fraction of the swing.</summary>
        public float Windup => Mathf.Max(0f, windup);
        /// <summary>Strike fraction of the swing.</summary>
        public float Strike => Mathf.Max(0.01f, strike);
        /// <summary>Follow-through hold fraction of the swing.</summary>
        public float Hold => Mathf.Max(0f, hold);
        /// <summary>Blade roll about its own axis, in degrees.</summary>
        public float Roll => roll;

        /// <summary>Chamber rotation, edge turned toward the contact pose.</summary>
        public Quaternion ChamberRotation
            => MeleeSwingCurve.BladeRotation(chamber.Direction, contact.Direction - chamber.Direction, roll);
        /// <summary>Contact rotation, edge turned along the chamber-to-follow sweep.</summary>
        public Quaternion ContactRotation
            => MeleeSwingCurve.BladeRotation(contact.Direction, follow.Direction - chamber.Direction, roll);
        /// <summary>Follow-through rotation, edge still carrying the sweep.</summary>
        public Quaternion FollowRotation
            => MeleeSwingCurve.BladeRotation(follow.Direction, follow.Direction - contact.Direction, roll);
    }

    /// <summary>Pure, allocation-free evaluation of a <see cref="SwingProfile"/> over normalized time.</summary>
    public static class MeleeSwingCurve
    {
        /// <summary>
        /// Viewmodel rotation for a blade pointing along <paramref name="direction"/> whose cutting edge (local +X)
        /// faces <paramref name="motion"/>, then turned <paramref name="roll"/> degrees about the blade.
        /// Falls back to a flat, edge-sideways blade when there is no sideways motion.
        /// </summary>
        public static Quaternion BladeRotation(Vector3 direction, Vector3 motion, float roll = 0f)
        {
            Vector3 up = Vector3.Cross(direction, motion);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.up;
            Quaternion rot = Quaternion.LookRotation(direction, up);
            return roll == 0f ? rot : rot * Quaternion.AngleAxis(roll, Vector3.forward);
        }

        /// <summary>
        /// Evaluates the swing at normalized time <paramref name="t"/> (0..1).
        /// The wind-up starts from <paramref name="fromPos"/>/<paramref name="fromRot"/> so chained swings never snap.
        /// </summary>
        public static void Evaluate(in SwingProfile p, float t, Vector3 fromPos, Quaternion fromRot,
            Vector3 restPos, Quaternion restRot, out Vector3 pos, out Quaternion rot)
        {
            t = Mathf.Clamp01(t);
            float w = p.Windup;
            float s = p.Strike;
            float h = p.Hold;

            if (t < w)
            {
                float u = EaseOutQuad(t / w);
                pos = Vector3.LerpUnclamped(fromPos, restPos + p.Chamber.Position, u);
                rot = Quaternion.SlerpUnclamped(fromRot, p.ChamberRotation, u);
                return;
            }

            if (t < w + s)
            {
                EvaluatePath(in p, EaseOutCubic((t - w) / s), restPos, out pos, out rot);
                return;
            }

            Vector3 followPos = restPos + p.Follow.Position;
            Quaternion followRot = p.FollowRotation;
            if (t < w + s + h)
            {
                pos = followPos;
                rot = followRot;
                return;
            }

            float recover = Mathf.Max(0.0001f, 1f - (w + s + h));
            float r = EaseInOutCubic(Mathf.Clamp01((t - w - s - h) / recover));
            pos = Vector3.LerpUnclamped(followPos, restPos, r);
            rot = Quaternion.SlerpUnclamped(followRot, restRot, r);
        }

        /// <summary>
        /// Strike path at progress <paramref name="u"/> (0 chamber, 0.5 contact, 1 follow-through).
        /// Position follows a quadratic curve through the contact pose; rotation arcs through it.
        /// </summary>
        public static void EvaluatePath(in SwingProfile p, float u, Vector3 restPos, out Vector3 pos, out Quaternion rot)
        {
            Vector3 a = restPos + p.Chamber.Position;
            Vector3 b = restPos + p.Contact.Position;
            Vector3 c = restPos + p.Follow.Position;
            Vector3 control = 2f * b - 0.5f * (a + c);
            float iu = 1f - u;
            pos = iu * iu * a + 2f * iu * u * control + u * u * c;
            rot = u < 0.5f
                ? Quaternion.Slerp(p.ChamberRotation, p.ContactRotation, u * 2f)
                : Quaternion.Slerp(p.ContactRotation, p.FollowRotation, (u - 0.5f) * 2f);
        }

        private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
        private static float EaseOutCubic(float x) { float i = 1f - x; return 1f - i * i * i; }
        private static float EaseInOutCubic(float x)
            => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
    }
}
