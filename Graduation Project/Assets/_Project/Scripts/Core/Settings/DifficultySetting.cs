namespace RetroFPS.Settings
{
    /// <summary>Player-selected difficulty with stable serialized identities.</summary>
    public enum DifficultySetting
    {
        /// <summary>Kolay.</summary>
        Easy = 0,
        /// <summary>Normal; identity multipliers, matches the untuned game.</summary>
        Normal = 1,
        /// <summary>Zor.</summary>
        Hard = 2
    }

    /// <summary>Engine-independent difficulty validation.</summary>
    public static class DifficultyDefaults
    {
        /// <summary>Reports whether a serialized difficulty is supported.</summary>
        public static bool IsValid(DifficultySetting difficulty) =>
            difficulty >= DifficultySetting.Easy && difficulty <= DifficultySetting.Hard;
    }
}
