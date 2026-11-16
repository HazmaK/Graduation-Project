using System;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Shared Sunder marks keyed by root damage targets, never by hitboxes.</summary>
    public static class SunderMarks
    {
        private static readonly SunderTracker Tracker = new SunderTracker();
        private static readonly IDamageable[] Targets = new IDamageable[SunderTracker.Capacity];
        private static readonly Transform[] Anchors = new Transform[SunderTracker.Capacity];
        private static readonly Action<IEncounterEnemy> EnemyDeathHandler = HandleEnemyDeath;

        /// <summary>Requests a marker at an anchor for the refreshed mark duration.</summary>
        public static event Action<Transform, float> OnMarked;

        /// <summary>Clears the visual at an anchor as soon as its mark is removed.</summary>
        public static event Action<Transform> OnCleared;

        /// <summary>Marks a root damage target and refreshes its visual.</summary>
        public static void Apply(IDamageable rootTarget, Transform anchor, float duration, float multiplier)
        {
            if (IsMissing(rootTarget) || !(duration > 0f)) return;
            if (rootTarget is IEncounterEnemy deadEnemy && !deadEnemy.IsAlive)
            {
                Remove(rootTarget);
                return;
            }
            Tracker.Apply(rootTarget, Time.time, duration, multiplier);
            ReclaimTargets();
            int index = -1;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (ReferenceEquals(Targets[i], rootTarget)) { index = i; break; }
                if (index < 0 && ReferenceEquals(Targets[i], null)) index = i;
            }
            if (index < 0) return;
            Transform previousAnchor = Anchors[index];
            Targets[index] = rootTarget;
            Anchors[index] = anchor;
            if (previousAnchor != null && previousAnchor != anchor) OnCleared?.Invoke(previousAnchor);
            if (rootTarget is IEncounterEnemy enemy)
            {
                enemy.OnEnemyDeath -= EnemyDeathHandler;
                enemy.OnEnemyDeath += EnemyDeathHandler;
            }
            OnMarked?.Invoke(anchor, duration);
        }

        /// <summary>Returns one unless the root target has an active Sunder mark.</summary>
        public static float GetMultiplier(IDamageable rootTarget)
        {
            ReclaimTargets();
            return Tracker.GetMultiplier(IsMissing(rootTarget) ? null : rootTarget, Time.time);
        }

        /// <summary>Removes a root target's mark, death subscription and visual immediately.</summary>
        public static void Remove(IDamageable rootTarget)
        {
            if (ReferenceEquals(rootTarget, null)) return;
            Tracker.Remove(rootTarget);
            for (int i = 0; i < Targets.Length; i++)
                if (ReferenceEquals(Targets[i], rootTarget)) ReleaseTarget(i);
        }

        /// <summary>Removes all gameplay marks, death subscriptions and visuals.</summary>
        public static void Clear()
        {
            Tracker.Clear();
            for (int i = 0; i < Targets.Length; i++) ReleaseTarget(i);
        }

        private static void HandleEnemyDeath(IEncounterEnemy enemy)
        {
            if (enemy is IDamageable target) Remove(target);
        }

        private static void ReclaimTargets()
        {
            float now = Time.time;
            for (int i = 0; i < Targets.Length; i++)
            {
                IDamageable target = Targets[i];
                if (ReferenceEquals(target, null)) continue;
                if (IsMissing(target) || !Tracker.TryGetRemaining(target, now, out _))
                {
                    Tracker.Remove(target);
                    ReleaseTarget(i);
                }
            }
        }

        private static void ReleaseTarget(int index)
        {
            IDamageable target = Targets[index];
            Transform anchor = Anchors[index];
            Targets[index] = null;
            Anchors[index] = null;
            if (target is IEncounterEnemy enemy) enemy.OnEnemyDeath -= EnemyDeathHandler;
            if (!ReferenceEquals(target, null)) OnCleared?.Invoke(anchor);
        }

        private static bool IsMissing(IDamageable target)
        {
            return ReferenceEquals(target, null) || (target is UnityEngine.Object obj && obj == null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
            OnMarked = null;
            OnCleared = null;
        }
    }
}
