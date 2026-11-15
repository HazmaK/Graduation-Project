using System.Globalization;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;
using RetroFPS.UI.Menu;

namespace RetroFPS.UI.HUD
{
    /// <summary>Uses the existing fixed banner queue with event-cached card name payloads.</summary>
    public sealed class CardAcquiredBannerModel
    {
        private readonly HudBannerQueue queue;
        private readonly CurseBlessingDefinition[] cards = new CurseBlessingDefinition[CardInventory.MaxCards];
        private readonly string[] lines = new string[CardInventory.MaxCards];
        private int count;
        /// <summary>Creates a bounded card payload cache and the standard HUD queue.</summary>
        public CardAcquiredBannerModel(double hold, double fadeIn, double fadeOut) { queue = new HudBannerQueue(hold, hold, fadeIn, fadeOut); }
        /// <summary>Current cached message or empty when hidden.</summary>
        public string Text => queue.HasCurrent ? lines[queue.Current.Wave - 1] : string.Empty;
        /// <summary>Whether there is visible content.</summary>
        public bool Visible => queue.HasCurrent;
        /// <summary>Queues one acquisition, formatting exactly once for this language.</summary>
        public void Enqueue(CurseBlessingDefinition card, StringTable strings, LanguageSetting language, double now)
        {
            if (card == null || strings == null || count == cards.Length) return;
            cards[count] = card; lines[count] = Format(card, strings, language); count++;
            queue.Enqueue(HudBannerKind.Wave, count, now);
        }
        /// <summary>Refreshes cached payloads without restarting holds or pending content.</summary>
        public void RefreshLanguage(StringTable strings, LanguageSetting language)
        {
            if (strings == null) return;
            for (int i = 0; i < count; i++) lines[i] = Format(cards[i], strings, language);
        }
        private static string Format(CurseBlessingDefinition card, StringTable strings, LanguageSetting language) =>
            string.Format(CultureInfo.InvariantCulture, strings.Get("hud.card.acquired.banner", language), strings.Get(card.NameKey, language));
        /// <summary>Advances the existing queue without formatting or allocation.</summary>
        public void Tick(double now) => queue.Tick(now);
        /// <summary>Clears both queued and cached content on run replacement.</summary>
        public void Clear()
        {
            queue.Reset();
            for (int i = 0; i < count; i++) { cards[i] = null; lines[i] = null; }
            count = 0;
        }
    }
}
