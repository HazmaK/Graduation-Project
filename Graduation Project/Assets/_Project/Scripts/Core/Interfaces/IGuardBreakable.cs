namespace RetroFPS.Combat
{
    /// <summary>Enemy whose guard can be forced open by an external effect.</summary>
    public interface IGuardBreakable
    {
        /// <summary>Forces the guard open for the supplied duration in seconds.</summary>
        void BreakGuard(float duration);
    }
}
