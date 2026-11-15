namespace RetroFPS.Stats
{
    /// <summary>Live player state used by conditional modifiers.</summary>
    public interface IStatContext
    {
        /// <summary>Current health divided by effective maximum health.</summary>
        float HealthFraction { get; }
        /// <summary>Whether current health exceeds effective maximum health.</summary>
        bool IsOverhealed { get; }
    }
}
