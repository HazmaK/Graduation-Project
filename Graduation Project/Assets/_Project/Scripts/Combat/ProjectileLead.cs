using System;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Pure first-order interception with a bounded, partial prediction.</summary>
    public static class ProjectileLead
    {
        /// <summary>Returns normalized partial intercept aim, or direct aim when interception is impossible.</summary>
        public static Vector3 Direction(Vector3 origin, Vector3 targetPos, Vector3 targetVelocity,
            float projectileSpeed, float leadFactor, float maxLeadSeconds)
        {
            Vector3 offset = targetPos - origin;
            Vector3 direct = offset.sqrMagnitude > 0f ? offset.normalized : Vector3.forward;
            if (projectileSpeed <= 0f || leadFactor <= 0f || maxLeadSeconds <= 0f) return direct;

            double a = Vector3.Dot(targetVelocity, targetVelocity) - (double)projectileSpeed * projectileSpeed;
            double b = 2.0 * Vector3.Dot(offset, targetVelocity);
            double c = Vector3.Dot(offset, offset);
            double time;
            if (Math.Abs(a) < 1e-9)
            {
                if (Math.Abs(b) < 1e-9) return direct;
                time = -c / b;
            }
            else
            {
                double discriminant = b * b - 4.0 * a * c;
                if (discriminant < 0.0) return direct;
                // Stable quadratic roots avoid cancellation for targets moving almost at orb speed.
                double q = -0.5 * (b + (b >= 0.0 ? Math.Sqrt(discriminant) : -Math.Sqrt(discriminant)));
                double first = q / a;
                double second = q != 0.0 ? c / q : -1.0;
                time = first > 0.0 ? first : second;
                if (second > 0.0 && second < time) time = second;
            }

            if (time <= 0.0 || double.IsNaN(time) || double.IsInfinity(time)) return direct;
            float prediction = (float)Math.Min(time, maxLeadSeconds) * Mathf.Clamp01(leadFactor);
            Vector3 aim = offset + targetVelocity * prediction;
            return aim.sqrMagnitude > 0f ? aim.normalized : direct;
        }
    }
}
