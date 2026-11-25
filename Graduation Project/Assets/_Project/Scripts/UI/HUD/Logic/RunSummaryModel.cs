using System;
using System.Globalization;
using System.Text;
using RetroFPS.Localization;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;

namespace RetroFPS.UI.HUD
{
    /// <summary>One formatted label/value pair in a run summary.</summary>
    public readonly struct RunSummaryRow
    {
        /// <summary>Localized row label.</summary>
        public string Label { get; }
        /// <summary>Formatted row value.</summary>
        public string Value { get; }

        /// <summary>Creates an immutable summary row.</summary>
        public RunSummaryRow(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    /// <summary>Pure formatter turning a <see cref="RunSummary"/> into localized display rows and text blocks.</summary>
    public static class RunSummaryModel
    {
        /// <summary>Total number of rows produced, including the final acquired-cards row.</summary>
        public const int TotalRowCount = 9;
        /// <summary>Number of numeric/status stat rows preceding the acquired-cards row.</summary>
        public const int StatRowCount = 8;
        /// <summary>Index of the acquired-cards row in the array returned by <see cref="BuildRows(in RunSummary, ILocalizedStringProvider, LanguageSetting)"/>.</summary>
        public const int CardsRowIndex = 8;

        private const string EmDash = "\u2014";

        /// <summary>Formats elapsed seconds as <c>mm:ss</c>, or <c>h:mm:ss</c> when one hour or longer.</summary>
        public static string FormatTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return "00:00";
            long total = (long)Math.Floor(seconds);
            long hours = total / 3600L;
            long minutes = (total % 3600L) / 60L;
            long secs = total % 60L;
            if (hours > 0L)
                return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, secs);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, secs);
        }

        /// <summary>Rounds nonnegative damage to the nearest integer, rounding midpoints away from zero.</summary>
        public static int RoundDamage(float damage)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return 0;
            if (damage >= int.MaxValue) return int.MaxValue;
            return (int)Math.Round(damage, MidpointRounding.AwayFromZero);
        }

        /// <summary>Builds all <see cref="TotalRowCount"/> localized summary rows for the given run summary.</summary>
        public static RunSummaryRow[] BuildRows(in RunSummary summary, ILocalizedStringProvider strings, LanguageSetting language)
        {
            var rows = new RunSummaryRow[TotalRowCount];
            BuildRows(in summary, strings, language, rows);
            return rows;
        }

        /// <summary>Populates a caller-owned buffer of at least <see cref="TotalRowCount"/> summary rows.</summary>
        public static void BuildRows(in RunSummary summary, ILocalizedStringProvider strings, LanguageSetting language, RunSummaryRow[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length < TotalRowCount)
                throw new ArgumentException("Destination buffer must hold at least " + TotalRowCount + " rows.", nameof(destination));

            CultureInfo culture = CultureInfo.InvariantCulture;
            destination[0] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.floor", language),
                summary.FloorNumber.ToString(culture));
            destination[1] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.total_time", language),
                FormatTime(summary.TotalSeconds));
            destination[2] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.araf_time", language),
                FormatTime(summary.ArafSeconds));
            destination[3] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.total_kills", language),
                summary.TotalKills.ToString(culture));
            destination[4] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.kills_split", language),
                string.Format(culture, "{0} / {1}", summary.TowerKills, summary.ArafKills));
            destination[5] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.crit_kills", language),
                summary.CriticalKills.ToString(culture));
            destination[6] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.damage_taken", language),
                RoundDamage(summary.DamageTaken).ToString(culture));
            destination[7] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.araf_charge", language),
                Resolve(strings, summary.ArafChargeUsed ? "hud.floor_summary.araf_used" : "hud.floor_summary.araf_ready", language));
            destination[8] = new RunSummaryRow(
                Resolve(strings, "hud.floor_summary.cards", language),
                FormatCards(in summary, strings, language));
        }

        /// <summary>Formats all summary rows into a multi-line text block suitable for a single text component.</summary>
        public static string FormatBlock(in RunSummary summary, ILocalizedStringProvider strings, LanguageSetting language)
        {
            RunSummaryRow[] rows = BuildRows(in summary, strings, language);
            var builder = new StringBuilder(256);
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0) builder.Append('\n');
                builder.Append(rows[i].Label).Append(": ").Append(rows[i].Value);
            }
            return builder.ToString();
        }

        private static string FormatCards(in RunSummary summary, ILocalizedStringProvider strings, LanguageSetting language)
        {
            int count = summary.CardCount;
            if (count <= 0)
                return Resolve(strings, "hud.floor_summary.cards_none", language, EmDash);

            var names = new StringBuilder(96);
            int appended = 0;
            for (int i = 0; i < count; i++)
            {
                CurseBlessingDefinition card = summary.GetCard(i);
                if (card == null) continue;
                string name = !string.IsNullOrEmpty(card.NameKey)
                    ? Resolve(strings, card.NameKey, language)
                    : card.Id;
                if (string.IsNullOrEmpty(name)) continue;
                if (appended > 0) names.Append(", ");
                names.Append(name);
                appended++;
            }

            if (appended == 0)
                return Resolve(strings, "hud.floor_summary.cards_none", language, EmDash);

            return string.Format(CultureInfo.InvariantCulture, "{0} ({1})", count, names);
        }

        private static string Resolve(ILocalizedStringProvider strings, string key, LanguageSetting language, string fallbackIfMissing = null)
        {
            if (strings != null) return strings.Get(key, language);
            if (LocalizedStrings.Provider != null) return LocalizedStrings.Get(key, language);
            return fallbackIfMissing ?? key;
        }
    }
}
