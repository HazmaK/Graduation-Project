using RetroFPS.Stats;

namespace RetroFPS.Run.Araf
{
    /// <summary>Applies and removes the temporary Araf maximum-health penalty.</summary>
    public static class ArafWound
    {
        /// <summary>Reserved modifier source, separate from card sources 1 through 16.</summary>
        public const int SourceId = 1000;

        /// <summary>Replaces the existing wound with a flat penalty; null sheets and nonpositive amounts do nothing.</summary>
        public static bool Apply(StatSheet sheet, float amount)
        {
            if (sheet == null || amount <= 0f) return false;
            sheet.RemoveSource(SourceId);
            return sheet.Add(SourceId, new StatModifier(StatId.MaxHealth, StatOp.AddFlat, -amount));
        }

        /// <summary>Removes the wound without affecting other modifier sources; null sheets are ignored.</summary>
        public static void Remove(StatSheet sheet) => sheet?.RemoveSource(SourceId);
    }
}
