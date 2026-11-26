using UnityEngine;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Pursues attackable endpoints, otherwise caches a reachable ambush ring position.</summary>
    public sealed class MeleeUnreachableResponse : IUnreachableTargetResponse
    {
        private readonly INavMeshQuery query;
        private readonly EnemyNavSettings settings;
        private readonly Vector3[] corners;
        private bool hasAmbush;
        private Vector3 anchor;
        private Vector3 ambush;

        /// <summary>Allocates the policy's corner buffer once.</summary>
        public MeleeUnreachableResponse(INavMeshQuery query, EnemyNavSettings settings)
        {
            this.query = query;
            this.settings = settings;
            corners = new Vector3[EnemyNavigator.MaxCorners];
        }

        /// <inheritdoc />
        public ChaseIntent Resolve(Vector3 self, Vector3 target, NavPathStatus status, Vector3 pathEnd, out Vector3 destination)
        {
            if (HorizontalSquared(pathEnd, target) <= settings.PartialEndAttackRadius * settings.PartialEndAttackRadius
                && EnemyNavigator.IsWithinVerticalReach(target, pathEnd, settings.ReachableHeightTolerance))
            {
                destination = pathEnd;
                return ChaseIntent.Pursue;
            }

            if (!hasAmbush || HorizontalSquared(anchor, target) > settings.AmbushRefreshDistance * settings.AmbushRefreshDistance)
            {
                hasAmbush = false;
                anchor = target;
                float bestDistance = float.PositiveInfinity;
                float startAngle = Mathf.Atan2(self.z - target.z, self.x - target.x);
                int samples = settings.AmbushSampleCount;
                for (int i = 0; i < samples; i++)
                {
                    float fraction = samples > 1 ? (float)i / (samples - 1) : 0f;
                    float radius = Mathf.Lerp(settings.AmbushRadiusMin, settings.AmbushRadiusMax, fraction);
                    float angle = startAngle + (Mathf.PI * 2f * i / samples);
                    Vector3 point = new Vector3(target.x + Mathf.Cos(angle) * radius, self.y, target.z + Mathf.Sin(angle) * radius);
                    if (!query.SamplePosition(point, settings.SampleMaxDistance, out Vector3 projected)
                        || query.CalculatePath(self, projected, corners, out _) != NavPathStatus.Complete)
                        continue;

                    float distance = HorizontalSquared(self, projected);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        ambush = projected;
                        hasAmbush = true;
                    }
                }
            }

            destination = hasAmbush ? ambush : pathEnd;
            return hasAmbush ? ChaseIntent.Ambush : ChaseIntent.Pursue;
        }

        /// <inheritdoc />
        public void Reset() => hasAmbush = false;

        private static float HorizontalSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
