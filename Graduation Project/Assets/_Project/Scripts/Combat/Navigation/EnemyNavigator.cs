using UnityEngine;

namespace RetroFPS.Combat.Navigation
{
    /// <summary>Reusable, allocation-free target planning and horizontal corner following.</summary>
    public sealed class EnemyNavigator
    {
        /// <summary>Capacity of each reusable path corner buffer.</summary>
        public const int MaxCorners = 32;
        private readonly INavMeshQuery query;
        private readonly EnemyNavSettings settings;
        private readonly IUnreachableTargetResponse unreachableResponse;
        private readonly Vector3[] corners;
        private bool needsPlan = true;
        private float elapsed;
        private Vector3 lastTarget;

        /// <summary>Creates a navigator; the optional response may be null.</summary>
        public EnemyNavigator(INavMeshQuery query, EnemyNavSettings settings, IUnreachableTargetResponse unreachableResponse)
        {
            this.query = query;
            this.settings = settings;
            this.unreachableResponse = unreachableResponse;
            corners = new Vector3[MaxCorners];
        }

        /// <summary>Status of the path to the target, independent of alternative paths.</summary>
        public NavPathStatus Status { get; private set; }
        /// <summary>Current movement intent.</summary>
        public ChaseIntent Intent { get; private set; }
        /// <summary>Requested target or policy destination.</summary>
        public Vector3 Destination { get; private set; }
        /// <summary>Valid steering corners.</summary>
        public int CornerCount { get; private set; }
        /// <summary>Current steering corner index.</summary>
        public int CurrentCorner { get; private set; }

        /// <summary>Replans on invalidation, interval expiry or target movement, then returns the steering point.</summary>
        public Vector3 Tick(float deltaTime, Vector3 selfPosition, Vector3 targetPosition, bool useUnreachableResponse = true)
        {
            elapsed += deltaTime;
            if (needsPlan || elapsed >= settings.RepathInterval
                || (targetPosition - lastTarget).sqrMagnitude > settings.TargetMoveThreshold * settings.TargetMoveThreshold)
                Plan(selfPosition, targetPosition, useUnreachableResponse);

            if (Status == NavPathStatus.NoNavMesh)
            {
                Destination = targetPosition;
                return targetPosition;
            }
            if (CornerCount == 0)
                return selfPosition;
            while (CurrentCorner < CornerCount - 1 && IsAtCorner(selfPosition))
                CurrentCorner++;
            return corners[CurrentCorner];
        }

        /// <summary>Returns a valid path corner for visualization.</summary>
        public Vector3 GetCorner(int index)
        {
            if ((uint)index >= (uint)CornerCount)
                throw new System.ArgumentOutOfRangeException(nameof(index));
            return corners[index];
        }

        /// <summary>Forces a new plan next tick and resets the response cache.</summary>
        public void Invalidate()
        {
            needsPlan = true;
            unreachableResponse?.Reset();
        }

        /// <summary>Whether the last corner has been reached horizontally.</summary>
        public bool HasArrived(Vector3 selfPosition) => CornerCount > 0 && CurrentCorner == CornerCount - 1 && IsAtCorner(selfPosition);

        /// <summary>Whether vertical separation is within the inclusive tolerance.</summary>
        public static bool IsWithinVerticalReach(Vector3 a, Vector3 b, float tolerance) => Mathf.Abs(a.y - b.y) <= tolerance;

        private void Plan(Vector3 self, Vector3 target, bool useResponse)
        {
            needsPlan = false;
            elapsed = 0f;
            lastTarget = target;
            CornerCount = 0;
            CurrentCorner = 0;
            Destination = target;
            if (!query.SamplePosition(self, settings.SampleMaxDistance, out Vector3 sampledSelf))
            {
                Status = NavPathStatus.NoNavMesh;
                Intent = ChaseIntent.Direct;
                return;
            }

            Vector3 sampledTarget;
            bool hasTarget = query.SamplePosition(target, settings.SampleMaxDistance, out sampledTarget);
            bool usedWideSample = false;
            if (!hasTarget)
            {
                hasTarget = query.SamplePosition(target, settings.TargetSampleMaxDistance, out sampledTarget);
                usedWideSample = hasTarget;
            }
            int count = 0;
            Status = hasTarget ? query.CalculatePath(sampledSelf, sampledTarget, corners, out count) : NavPathStatus.Unreachable;
            Vector3 targetOffset = sampledTarget - target;
            // A narrow-sampled jump remains a valid pursuit target; only extended projection can imply an unreachable height.
            if (Status == NavPathStatus.Complete && usedWideSample
                && (targetOffset.x * targetOffset.x + targetOffset.z * targetOffset.z > settings.PartialEndAttackRadius * settings.PartialEndAttackRadius
                    || !IsWithinVerticalReach(sampledTarget, target, settings.ReachableHeightTolerance)))
                Status = NavPathStatus.Partial;
            CornerCount = Status == NavPathStatus.Unreachable ? 0 : count;
            Vector3 pathEnd = CornerCount > 0 ? corners[CornerCount - 1] : sampledSelf;
            Intent = ChaseIntent.Pursue;
            if (Status != NavPathStatus.Complete)
            {
                Destination = pathEnd;
                if (useResponse && unreachableResponse != null)
                {
                    Intent = unreachableResponse.Resolve(sampledSelf, target, Status, pathEnd, out Vector3 destination);
                    Destination = destination;
                    if (!Destination.Equals(sampledTarget))
                    {
                        NavPathStatus alternative = query.CalculatePath(sampledSelf, Destination, corners, out count);
                        CornerCount = alternative == NavPathStatus.Unreachable ? 0 : count;
                    }
                }
            }
            CurrentCorner = CornerCount > 1 ? 1 : 0;
        }

        private bool IsAtCorner(Vector3 self)
        {
            Vector3 difference = corners[CurrentCorner] - self;
            return difference.x * difference.x + difference.z * difference.z <= settings.CornerReachDistance * settings.CornerReachDistance;
        }
    }
}
