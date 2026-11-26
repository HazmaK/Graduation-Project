using System;
using NUnit.Framework;
using RetroFPS.Combat.Navigation;
using UnityEngine;

namespace RetroFPS.Tests.EditMode.Navigation
{
    /// <summary>Deterministic planning, corner following and allocation checks.</summary>
    public sealed class EnemyNavigatorTests
    {
        private EnemyNavSettings settings;
        private FakeNavMeshQuery query;
        private EnemyNavigator navigator;
        private Vector3 self;
        private Vector3 target;

        /// <summary>Creates fresh default settings and a scripted query for every test.</summary>
        [SetUp]
        public void SetUp()
        {
            settings = new EnemyNavSettings();
            self = Vector3.zero;
            target = Vector3.forward * settings.TargetSampleMaxDistance;
            query = new FakeNavMeshQuery();
            navigator = new EnemyNavigator(query, settings, new MeleeUnreachableResponse(query, settings));
        }

        /// <summary>Stable targets only replan when the periodic interval expires.</summary>
        [Test]
        public void Tick_RespectsRepathInterval()
        {
            navigator.Tick(0f, self, target);
            navigator.Tick(settings.RepathInterval * 0.5f, self, target);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(1));
            navigator.Tick(settings.RepathInterval * 0.5f, self, target);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(2));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Complete));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Pursue));
            Assert.That(navigator.Destination, Is.EqualTo(target));
        }

        /// <summary>Movement must exceed the threshold measured from the last plan.</summary>
        [Test]
        public void Tick_TargetMovementForcesReplanBeforeInterval()
        {
            navigator.Tick(0f, self, target);
            navigator.Tick(0f, self, target + Vector3.right * settings.TargetMoveThreshold);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(1));
            navigator.Tick(0f, self, target + Vector3.right * (settings.TargetMoveThreshold + settings.CornerReachDistance));
            Assert.That(query.CalculatePathCalls, Is.EqualTo(2));
        }

        /// <summary>Explicit invalidation replans immediately and resets the policy.</summary>
        [Test]
        public void Invalidate_ForcesReplanAndResetsResponse()
        {
            var response = new RecordingResponse();
            navigator = new EnemyNavigator(query, settings, response);
            navigator.Tick(0f, self, target);
            navigator.Invalidate();
            Assert.That(response.ResetCalls, Is.EqualTo(1));
            navigator.Tick(0f, self, target);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(2));
        }

        /// <summary>Following starts at corner one and ignores vertical offset for arrival.</summary>
        [Test]
        public void Tick_AdvancesCornersAndReportsArrival()
        {
            Vector3 middle = Vector3.forward * settings.PartialEndAttackRadius;
            query.SetCorners(self, middle, target);
            Assert.That(navigator.Tick(0f, self, target), Is.EqualTo(middle));
            Assert.That(navigator.CurrentCorner, Is.EqualTo(1));
            Assert.That(navigator.CornerCount, Is.EqualTo(3));
            Assert.That(navigator.GetCorner(2), Is.EqualTo(target));
            Assert.That(navigator.HasArrived(self), Is.False);
            Vector3 atMiddle = middle + Vector3.right * settings.CornerReachDistance
                + Vector3.up * settings.TargetSampleMaxDistance;
            Assert.That(navigator.Tick(0f, atMiddle, target), Is.EqualTo(target));
            Assert.That(navigator.CurrentCorner, Is.EqualTo(2));
            Assert.That(navigator.HasArrived(target + Vector3.up * settings.TargetSampleMaxDistance), Is.True);
            Assert.That(navigator.HasArrived(target + Vector3.right * settings.PartialEndAttackRadius), Is.False);
        }

        /// <summary>The follower can advance across multiple reached corners in a single tick.</summary>
        [Test]
        public void Tick_SkipsConsecutiveReachedCorners()
        {
            query.SetCorners(self, self, target);
            Assert.That(navigator.Tick(0f, self, target), Is.EqualTo(target));
            Assert.That(navigator.CurrentCorner, Is.EqualTo(2));
        }

        /// <summary>Failure to project self returns the live target without computing a path.</summary>
        [Test]
        public void Tick_NoNavMeshSteersDirectly()
        {
            query.SampleSucceeds = false;
            Assert.That(navigator.Tick(0f, self, target), Is.EqualTo(target));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.NoNavMesh));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Direct));
            Assert.That(navigator.Destination, Is.EqualTo(target));
            Assert.That(navigator.CornerCount, Is.Zero);
            Assert.That(query.CalculatePathCalls, Is.Zero);
            Vector3 movedTarget = target + Vector3.right * settings.CornerReachDistance;
            Assert.That(navigator.Tick(0f, self, movedTarget), Is.EqualTo(movedTarget));
        }

        /// <summary>Target projection retries with the configured extended sampling radius.</summary>
        [Test]
        public void Tick_TargetProjectionUsesExtendedRadius()
        {
            query.MinimumTargetSampleDistance = settings.TargetSampleMaxDistance;
            navigator.Tick(0f, self, target);
            Assert.That(query.SampleCalls, Is.EqualTo(3));
            Assert.That(query.LastSampleDistance, Is.EqualTo(settings.TargetSampleMaxDistance));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Complete));
        }

        /// <summary>Unprojectable targets hold at self when no response is enabled.</summary>
        [Test]
        public void Tick_TargetProjectionFailureIsUnreachableAndHolds()
        {
            query.MinimumTargetSampleDistance = float.PositiveInfinity;
            Assert.That(navigator.Tick(0f, self, target, false), Is.EqualTo(self));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Unreachable));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Pursue));
            Assert.That(navigator.Destination, Is.EqualTo(self));
            Assert.That(query.CalculatePathCalls, Is.Zero);
            Assert.That(navigator.HasArrived(self), Is.False);
        }

        /// <summary>An invalid target path without a response has no corners and holds position.</summary>
        [Test]
        public void Tick_UnreachablePathWithoutResponseHolds()
        {
            query.FirstPathStatus = NavPathStatus.Unreachable;
            navigator = new EnemyNavigator(query, settings, null);
            Assert.That(navigator.Tick(0f, self, target), Is.EqualTo(self));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Unreachable));
            Assert.That(navigator.Destination, Is.EqualTo(self));
            Assert.That(navigator.CornerCount, Is.Zero);
        }

        /// <summary>A failed alternative route holds while preserving the original target status.</summary>
        [Test]
        public void Tick_FailedAlternativePreservesTargetStatus()
        {
            query.FirstPathStatus = NavPathStatus.Partial;
            query.FirstPathEnd = self;
            query.SubsequentPathStatus = NavPathStatus.Unreachable;
            navigator = new EnemyNavigator(query, settings, new RecordingResponse());
            Assert.That(navigator.Tick(0f, self, target), Is.EqualTo(self));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Partial));
            Assert.That(navigator.CornerCount, Is.Zero);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(2));
        }

        /// <summary>A partial endpoint inside both melee bounds remains a pursuit destination.</summary>
        [Test]
        public void Tick_PartialNearTargetPursuesPathEnd()
        {
            Vector3 pathEnd = target - Vector3.forward * (settings.PartialEndAttackRadius * 0.5f);
            target += Vector3.up * settings.ReachableHeightTolerance;
            query.FirstPathStatus = NavPathStatus.Partial;
            query.FirstPathEnd = pathEnd;
            Assert.That(navigator.Tick(0f, self, target), Is.EqualTo(pathEnd));
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Partial));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Pursue));
            Assert.That(navigator.Destination, Is.EqualTo(pathEnd));
            Assert.That(query.CalculatePathCalls, Is.EqualTo(2));
        }

        /// <summary>Far or vertically separated targets cause an accessible ambush-ring destination.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Tick_PartialFarOrHighTargetAmbushes(bool high)
        {
            Vector3 pathEnd = high
                ? target + Vector3.up * (settings.ReachableHeightTolerance + settings.CornerReachDistance)
                : self;
            query.FirstPathStatus = NavPathStatus.Partial;
            query.FirstPathEnd = pathEnd;
            navigator.Tick(0f, self, target);
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Partial));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Ambush));
            Vector3 offset = navigator.Destination - target;
            float horizontalDistance = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
            Assert.That(horizontalDistance, Is.InRange(settings.AmbushRadiusMin - settings.SampleMaxDistance,
                settings.AmbushRadiusMax + settings.SampleMaxDistance));
            Assert.That(query.CalculatePathCalls, Is.EqualTo(settings.AmbushSampleCount + 2));
            Assert.That(navigator.GetCorner(navigator.CornerCount - 1), Is.EqualTo(navigator.Destination));
        }

        /// <summary>A complete path projected below a high target uses a reachable ambush ring.</summary>
        [Test]
        public void Tick_CompletePathSampledFourMetresBelowTargetAmbushes()
        {
            target += Vector3.up * 4f;
            query.TargetToProject = target;
            query.ProjectedTarget = target - Vector3.up * 4f;
            query.MinimumOverriddenTargetSampleDistance = settings.TargetSampleMaxDistance;
            navigator.Tick(0f, self, target);
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Partial));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Ambush));
            Assert.That(query.CalculatePathCalls, Is.EqualTo(settings.AmbushSampleCount + 2));
            Assert.That(navigator.GetCorner(navigator.CornerCount - 1), Is.EqualTo(navigator.Destination));
        }

        /// <summary>A jump-height target found by narrow projection remains a complete pursuit.</summary>
        [Test]
        public void Tick_CompletePathNarrowSampleOnePointFiveMetresAboveGroundPursues()
        {
            target += Vector3.up * 1.5f;
            query.TargetToProject = target;
            query.ProjectedTarget = target - Vector3.up * 1.5f;
            navigator.Tick(0f, self, target);
            Assert.That(navigator.Status, Is.EqualTo(NavPathStatus.Complete));
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Pursue));
            Assert.That(query.SampleCalls, Is.EqualTo(2));
            Assert.That(query.LastSampleDistance, Is.EqualTo(settings.SampleMaxDistance));
        }

        /// <summary>Disabled or missing responses preserve the original partial endpoint.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Tick_WithoutResponsePursuesPartialEnd(bool nullResponse)
        {
            query.FirstPathStatus = NavPathStatus.Partial;
            query.FirstPathEnd = Vector3.forward * settings.CornerReachDistance;
            if (nullResponse)
                navigator = new EnemyNavigator(query, settings, null);
            navigator.Tick(0f, self, target, nullResponse);
            Assert.That(navigator.Intent, Is.EqualTo(ChaseIntent.Pursue));
            Assert.That(navigator.Destination, Is.EqualTo(query.FirstPathEnd.Value));
            Assert.That(query.CalculatePathCalls, Is.EqualTo(1));
        }

        /// <summary>Vertical reach is symmetric and inclusive at both boundaries.</summary>
        [TestCase(1.6f, 1.6f, true)]
        [TestCase(-1.6f, 1.6f, true)]
        [TestCase(1.6001f, 1.6f, false)]
        [TestCase(-1.6001f, 1.6f, false)]
        [TestCase(0f, 0f, true)]
        public void IsWithinVerticalReach_IncludesBoundaries(float height, float tolerance, bool expected)
        {
            Assert.That(EnemyNavigator.IsWithinVerticalReach(Vector3.zero, Vector3.up * height, tolerance), Is.EqualTo(expected));
        }

        /// <summary>Ambush cache refreshes only after horizontal anchor movement, or Reset.</summary>
        [Test]
        public void Response_CachesRefreshesAndResetsAmbush()
        {
            var response = new MeleeUnreachableResponse(query, settings);
            response.Resolve(self, target, NavPathStatus.Unreachable, self, out Vector3 first);
            int calls = query.CalculatePathCalls;
            response.Resolve(self, target + Vector3.up * settings.TargetSampleMaxDistance,
                NavPathStatus.Unreachable, self, out Vector3 cached);
            Assert.That(cached, Is.EqualTo(first));
            Assert.That(query.CalculatePathCalls, Is.EqualTo(calls));
            Vector3 movedTarget = target + Vector3.forward * (settings.AmbushRefreshDistance + settings.CornerReachDistance);
            response.Resolve(self, movedTarget, NavPathStatus.Unreachable, self, out _);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(calls + settings.AmbushSampleCount));
            response.Reset();
            response.Resolve(self, movedTarget, NavPathStatus.Unreachable, self, out _);
            Assert.That(query.CalculatePathCalls, Is.EqualTo(calls + settings.AmbushSampleCount * 2));
        }

        /// <summary>No reachable ring samples falls back to the supplied endpoint.</summary>
        [Test]
        public void Response_NoValidAmbushPursuesPathEnd()
        {
            query.SubsequentPathStatus = NavPathStatus.Unreachable;
            query.FirstPathStatus = NavPathStatus.Unreachable;
            var response = new MeleeUnreachableResponse(query, settings);
            Assert.That(response.Resolve(self, target, NavPathStatus.Unreachable, self, out Vector3 destination),
                Is.EqualTo(ChaseIntent.Pursue));
            Assert.That(destination, Is.EqualTo(self));
        }

        /// <summary>Steering and repeated planning allocate no managed bytes after warm-up.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Tick_AfterWarmupAllocatesZeroBytes(bool ambush)
        {
            if (ambush)
            {
                query.FirstPathStatus = NavPathStatus.Partial;
                query.FirstPathEnd = self;
                query.PartialForTarget = target;
            }
            for (int i = 0; i < 200; i++)
                navigator.Tick(settings.RepathInterval, self, target);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
                navigator.Tick(settings.RepathInterval, self, target);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private sealed class RecordingResponse : IUnreachableTargetResponse
        {
            /// <summary>Number of policy resets.</summary>
            public int ResetCalls { get; private set; }

            /// <inheritdoc />
            public ChaseIntent Resolve(Vector3 position, Vector3 requestedTarget, NavPathStatus status, Vector3 pathEnd, out Vector3 destination)
            {
                destination = pathEnd;
                return ChaseIntent.Pursue;
            }

            /// <inheritdoc />
            public void Reset() => ResetCalls++;
        }
    }

    /// <summary>Scriptable nonallocating query with separate first and subsequent path results.</summary>
    internal sealed class FakeNavMeshQuery : INavMeshQuery
    {
        private Vector3[] scriptedCorners;

        /// <summary>Number of path calculations.</summary>
        public int CalculatePathCalls { get; private set; }
        /// <summary>Number of projection requests.</summary>
        public int SampleCalls { get; private set; }
        /// <summary>Most recent projection radius.</summary>
        public float LastSampleDistance { get; private set; }
        /// <summary>Whether projections are permitted.</summary>
        public bool SampleSucceeds { get; set; } = true;
        /// <summary>Minimum projection radius for nonzero target positions.</summary>
        public float MinimumTargetSampleDistance { get; set; }
        /// <summary>Target-specific minimum projection radius.</summary>
        public float MinimumOverriddenTargetSampleDistance { get; set; }
        /// <summary>Optional position whose projection is overridden.</summary>
        public Vector3? TargetToProject { get; set; }
        /// <summary>Projection returned for the overridden target.</summary>
        public Vector3 ProjectedTarget { get; set; }
        /// <summary>First path result.</summary>
        public NavPathStatus FirstPathStatus { get; set; } = NavPathStatus.Complete;
        /// <summary>Later path results.</summary>
        public NavPathStatus SubsequentPathStatus { get; set; } = NavPathStatus.Complete;
        /// <summary>Optional first path endpoint override.</summary>
        public Vector3? FirstPathEnd { get; set; }
        /// <summary>Optional destination that always produces the first scripted result.</summary>
        public Vector3? PartialForTarget { get; set; }

        /// <summary>Installs a reusable corner sequence outside measured code.</summary>
        public void SetCorners(params Vector3[] corners) => scriptedCorners = corners;

        /// <inheritdoc />
        public bool SamplePosition(Vector3 position, float maxDistance, out Vector3 hit)
        {
            SampleCalls++;
            LastSampleDistance = maxDistance;
            hit = TargetToProject.HasValue && position == TargetToProject.Value ? ProjectedTarget : position;
            float minimumDistance = TargetToProject.HasValue && position == TargetToProject.Value
                ? Mathf.Max(MinimumTargetSampleDistance, MinimumOverriddenTargetSampleDistance)
                : MinimumTargetSampleDistance;
            return SampleSucceeds && (position == Vector3.zero || maxDistance >= minimumDistance);
        }

        /// <inheritdoc />
        public NavPathStatus CalculatePath(Vector3 from, Vector3 to, Vector3[] corners, out int count)
        {
            CalculatePathCalls++;
            bool first = CalculatePathCalls == 1 || (PartialForTarget.HasValue && PartialForTarget.Value == to);
            NavPathStatus status = first ? FirstPathStatus : SubsequentPathStatus;
            count = 0;
            if (status == NavPathStatus.Unreachable)
                return status;
            if (scriptedCorners != null)
            {
                count = Mathf.Min(scriptedCorners.Length, corners.Length);
                for (int i = 0; i < count; i++)
                    corners[i] = scriptedCorners[i];
            }
            else
            {
                corners[0] = from;
                corners[1] = first && FirstPathEnd.HasValue ? FirstPathEnd.Value : to;
                count = 2;
            }
            return status;
        }
    }
}
