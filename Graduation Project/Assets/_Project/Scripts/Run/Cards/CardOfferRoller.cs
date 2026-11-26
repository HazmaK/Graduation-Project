using System;

namespace RetroFPS.Run.Cards
{
    /// <summary>Deterministic, rarity-weighted offers without repeated identities or allocations during picks.</summary>
    public sealed class CardOfferRoller
    {
        /// <summary>Maximum successful offers during the run's Araf visit.</summary>
        public const int MaxOffersCap = 6;

        private readonly RunRandom _rng;
        private readonly string[] _seenIds = new string[MaxOffersCap * 2];
        private int _seenCount;
        private int _offersMade;
        private int _maxOffers = 2;

        /// <summary>Creates a roller using the run's dedicated card random stream.</summary>
        public CardOfferRoller(RunRandom rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>Number of successful offers made so far.</summary>
        public int OffersMade => _offersMade;
        /// <summary>Whether the offer limit permits another roll.</summary>
        public bool CanOffer => _offersMade < MaxOffers;

        /// <summary>Maximum successful offers permitted for this visit.</summary>
        public int MaxOffers => _maxOffers;

        /// <summary>Sets the visit limit, clamped between zero and the supported cap.</summary>
        public void ConfigureVisit(int maxOffers)
            => _maxOffers = Math.Max(0, Math.Min(MaxOffersCap, maxOffers));

        /// <summary>Offers up to two distinct unseen, unowned cards without allocating candidate storage.</summary>
        public bool TryRoll(CardCatalog catalog, CardInventory inventory, out CardOffer offer)
            => TryRoll(catalog, inventory, CardRarityMultipliers.One, out offer);

        /// <summary>Offers unseen cards using rarity multipliers, retrying each empty pick with default weights.</summary>
        public bool TryRoll(CardCatalog catalog, CardInventory inventory, in CardRarityMultipliers rarity, out CardOffer offer)
        {
            offer = default;
            if (!CanOffer || catalog == null) return false;
            CurseBlessingDefinition first = PickWithFallback(catalog, inventory, in rarity);
            if (first == null) return false;
            _seenIds[_seenCount++] = first.Id;
            CurseBlessingDefinition second = PickWithFallback(catalog, inventory, in rarity);
            if (second != null) _seenIds[_seenCount++] = second.Id;
            offer = new CardOffer(first, second);
            _offersMade++;
            return true;
        }

        private CurseBlessingDefinition PickWithFallback(CardCatalog catalog, CardInventory inventory,
            in CardRarityMultipliers rarity)
            => Pick(catalog, inventory, in rarity) ?? Pick(catalog, inventory, CardRarityMultipliers.One);

        private CurseBlessingDefinition Pick(CardCatalog catalog, CardInventory inventory, in CardRarityMultipliers rarity)
        {
            CurseBlessingDefinition selected = null;
            float total = 0f;
            for (int i = 0; i < catalog.Count; i++)
            {
                CurseBlessingDefinition card = catalog.Get(i);
                if (!IsCandidate(catalog, i, card, inventory)) continue;
                float weight = catalog.GetWeight(card.Rarity) * rarity.Get(card.Rarity);
                if (weight <= 0f) continue;
                total += weight;
                // Weighted reservoir sampling retains candidates in proportion to their tier weight.
                if (_rng.Value01 * total < weight) selected = card;
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
