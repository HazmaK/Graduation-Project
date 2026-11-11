namespace RetroFPS.Combat
{
    public static class CombatEvents
    {
        // position, wasCritical, wasGibbed
        public static event System.Action<UnityEngine.Vector3, bool, bool> OnEnemyKilled;

        public static void RaiseEnemyKilled(UnityEngine.Vector3 position, bool wasCritical, bool wasGibbed)
            => OnEnemyKilled?.Invoke(position, wasCritical, wasGibbed);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnEnemyKilled = null;
        }
    }
}
