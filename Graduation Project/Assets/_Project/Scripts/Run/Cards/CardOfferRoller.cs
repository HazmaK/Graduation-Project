using System;

namespace RetroFPS.Run.Cards
{
    /// <summary>Deterministic, uniform offers without repeated card identities.</summary>
    public sealed class CardOfferRoller
    {
        /// <summary>Maximum successful offers during the run's Araf visit.</summary>
        public const int MaxOffersPerVisit = 2;

        private readonly RunRandom _rng;
        private readonly string[] _seenIds = new string[MaxOffersPerVisit * 2];
        private int _seenCount;
        private int _offersMade;

        /// <summary>Creates a roller using the run's dedicated card random stream.</summary>
        public CardOfferRoller(RunRandom rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>Number of successful offers made so far.</summary>
        public int OffersMade => _offersMade;
        /// <summary>Whether the offer limit permits another roll.</summary>
        public bool CanOffer => _offersMade < MaxOffersPerVisit;

        /// <summary>Offers up to two distinct unseen, unowned cards without allocating candidate storage.</summary>
        public bool TryRoll(CardCatalog catalog, CardInventory inventory, out CardOffer offer)
        {
            offer = default;
            if (!CanOffer || catalog == null) return false;
            CurseBlessingDefinition first = Pick(catalog, inventory);
            if (first == null) return false;
            _seenIds[_seenCount++] = first.Id;
            CurseBlessingDefinition second = Pick(catalog, inventory);
            if (second != null) _seenIds[_seenCount++] = second.Id;
            offer = new CardOffer(first, second);
            _offersMade++;
            return true;
        }

        private CurseBlessingDefinition Pick(CardCatalog catalog, CardInventory inventory)
        {
            CurseBlessingDefinition selected = null;
            int candidates = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                CurseBlessingDefinition card = catalog.Get(i);
                if (!IsCandidate(catalog, i, card, inventory)) continue;
                // Reservoir sampling gives each distinct eligible identity equal probability.
                if (_rng.Range(0, ++candidates) == 0) selected = card;
            }
            return selected;
        }

        private bool IsCandidate(CardCatalog catalog, int index, CurseBlessingDefinition card, CardInventory inventory)
        {
            if (card == null || (inventory != null && inventory.Has(card))) return false;
            for (int i = 0; i < _seenCount; i++)
            {
                if (string.Equals(_seenIds[i], card.Id, StringComparison.Ordinal)) return false;
            }
            for (int i = 0; i < index; i++)
            {
                CurseBlessingDefinition previous = catalog.Get(i);
                if (previous != null && string.Equals(previous.Id, card.Id, StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}
