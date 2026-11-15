namespace RetroFPS.Combat
{
    /// <summary>Read-only accepted stun state for rewards without inferring stun from recovery poses.</summary>
    public interface IStunState
    {
        /// <summary>Whether an accepted stun is currently active.</summary>
        bool IsStunned { get; }
        /// <summary>Monotonic identity incremented for each accepted stun, including across pooled spawns.</summary>
        uint StunSequence { get; }
    }
}
