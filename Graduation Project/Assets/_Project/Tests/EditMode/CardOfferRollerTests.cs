using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Stats;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Determinism, eligibility and visit limits for card offers.</summary>
    public sealed class CardOfferRollerTests : CardTestFixture
    {
        /// <summary>A disabled tier is excluded from every offer slot and does not consume an offer.</summary>
        [TestCase(CardRarity.Common)]
        [TestCase(CardRarity.Rare)]
        [TestCase(CardRarity.Relic)]
        public void ZeroWeightTierIsNeverOffered(CardRarity disabledTier)
        {
            var common = CreateRarityCard("common", CardRarity.Common);
            var rare = CreateRarityCard("rare", CardRarity.Rare);
            var relic = CreateRarityCard("relic", CardRarity.Relic);
            var cards = new[] { common, rare, relic };
            var catalog = CreateCatalog(cards);
            catalog.Configure(cards, disabledTier == CardRarity.Common ? 0f : 10f,
                disabledTier == CardRarity.Rare ? 0f : 5f,
                disabledTier == CardRarity.Relic ? 0f : 1.5f);
            for (ulong seed = 0; seed < 64; seed++)
            {
                var roller = new CardOfferRoller(new RunRandom(seed));
                Assert.That(roller.TryRoll(catalog, null, out CardOffer offer), Is.True);
                Assert.That(offer.Count, Is.EqualTo(2));
                for (int i = 0; i < offer.Count; i++)
                    Assert.That(offer.Get(i).Rarity, Is.Not.EqualTo(disabledTier));
                Assert.That(roller.TryRoll(catalog, null, out _), Is.False);
                Assert.That(roller.OffersMade, Is.EqualTo(1));
            }
        }

        /// <summary>First picks follow per-card weights rather than a uniform rarity distribution.</summary>
        [Test]
        public void RelicShareMatchesWeightAcrossTwoThousandSeededPicks()
        {
            var common = CreateRarityCard("common", CardRarity.Common);
            var relic = CreateRarityCard("relic", CardRarity.Relic);
            var catalog = CreateCatalog(common, relic);
            int relicPicks = 0;
            const int picks = 2000;
            for (ulong seed = 0; seed < picks; seed++)
            {
                var roller = new CardOfferRoller(new RunRandom(seed));
                Assert.That(roller.TryRoll(catalog, null, out CardOffer offer), Is.True);
                // Only the first pick is sampled: the second must be the remaining card.
                if (offer.Get(0) == relic) relicPicks++;
            }
            Assert.That((float)relicPicks / picks, Is.InRange(0.08f, 0.18f));
        }

        /// <summary>Mixed-rarity catalogs preserve determinism across both offers in a visit.</summary>
        [Test]
        public void SameSeedProducesSameMixedRarityOffers()
        {
            var catalog = CreateCatalog(CreateRarityCard("common_a", CardRarity.Common),
                CreateRarityCard("rare_a", CardRarity.Rare), CreateRarityCard("relic_a", CardRarity.Relic),
                CreateRarityCard("common_b", CardRarity.Common), CreateRarityCard("rare_b", CardRarity.Rare),
                CreateRarityCard("relic_b", CardRarity.Relic));
            for (ulong seed = 0; seed < 32; seed++)
            {
                var first = new CardOfferRoller(new RunRandom(seed));
                var second = new CardOfferRoller(new RunRandom(seed));
                for (int i = 0; i < CardOfferRoller.MaxOffersPerVisit; i++)
                {
                    Assert.That(first.TryRoll(catalog, null, out CardOffer left), Is.True);
                    Assert.That(second.TryRoll(catalog, null, out CardOffer right), Is.True);
                    Assert.That(left.Count, Is.EqualTo(2));
                    Assert.That(right.Count, Is.EqualTo(left.Count));
                    for (int j = 0; j < left.Count; j++)
                        Assert.That(right.Get(j), Is.SameAs(left.Get(j)));
                }
            }
        }

        /// <summary>Existing configure callers retain Common rarity and default weights; negative weights clamp to zero.</summary>
        [Test]
        public void DefaultRarityAndCatalogWeightClampingMatchContract()
        {
            var card = CreateCard("default");
            var catalog = CreateCatalog(card);
            Assert.That(card.Rarity, Is.EqualTo(CardRarity.Common));
            Assert.That(catalog.GetWeight(CardRarity.Common), Is.EqualTo(10f));
            Assert.That(catalog.GetWeight(CardRarity.Rare), Is.EqualTo(5f));
            Assert.That(catalog.GetWeight(CardRarity.Relic), Is.EqualTo(1.5f));
            catalog.Configure(new[] { card }, -10f, -5f, -1.5f);
            Assert.That(catalog.GetWeight(CardRarity.Common), Is.Zero);
            Assert.That(catalog.GetWeight(CardRarity.Rare), Is.Zero);
            Assert.That(catalog.GetWeight(CardRarity.Relic), Is.Zero);
            var roller = new CardOfferRoller(new RunRandom(7));
            Assert.That(roller.TryRoll(catalog, null, out _), Is.False);
            Assert.That(roller.OffersMade, Is.Zero);
        }

        private CurseBlessingDefinition CreateRarityCard(string id, CardRarity rarity)
        {
            var card = CreateCard(id);
            card.Configure(card.Id, card.NameKey, card.BoonKey, card.CurseKey, card.FlavorKey,
                card.AccentColor, null, rarity);
            return card;
        }

        /// <summary>Matching seeds and calls produce identical offers.</summary>
        [Test]
        public void SameSeedAndCallOrderProduceSameOffers()
        {
            var catalog = CreateCatalog(CreateCard("a"), CreateCard("b"), CreateCard("c"),
                CreateCard("d"), CreateCard("e"), CreateCard("f"));
            for (ulong seed = 0; seed < 32; seed++)
            {
                var first = new CardOfferRoller(new RunRandom(seed));
                var second = new CardOfferRoller(new RunRandom(seed));
                var inventory = new CardInventory(new StatSheet());
                for (int i = 0; i < CardOfferRoller.MaxOffersPerVisit; i++)
                {
                    Assert.That(first.TryRoll(catalog, inventory, out CardOffer left), Is.True);
                    Assert.That(second.TryRoll(catalog, inventory, out CardOffer right), Is.True);
                    Assert.That(left.Count, Is.EqualTo(2));
                    Assert.That(right.Count, Is.EqualTo(left.Count));
                    Assert.That(right.Get(0), Is.SameAs(left.Get(0)));
                    Assert.That(right.Get(1), Is.SameAs(left.Get(1)));
                }
            }
        }

        /// <summary>Nulls, owned identities, duplicate entries and earlier offers are excluded.</summary>
        [Test]
        public void OffersExcludeOwnedAndPreviouslyShownCardsByIdentity()
        {
            var owned = CreateCard("owned");
            var a = CreateCard("a");
            var catalog = CreateCatalog(null, owned, CreateCard("owned"), a, a, CreateCard("a"),
                CreateCard("b"), CreateCard("c"), CreateCard("d"));
            for (ulong seed = 0; seed < 32; seed++)
            {
                var inventory = new CardInventory(new StatSheet());
                inventory.TryAcquire(owned);
                var roller = new CardOfferRoller(new RunRandom(seed));
                var shown = new HashSet<string>();
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(roller.TryRoll(catalog, inventory, out CardOffer offer), Is.True);
                    Assert.That(offer.Count, Is.EqualTo(2));
                    for (int j = 0; j < offer.Count; j++)
                    {
                        Assert.That(inventory.Has(offer.Get(j)), Is.False);
                        Assert.That(shown.Add(offer.Get(j).Id), Is.True);
                    }
                }
                Assert.That(shown.Count, Is.EqualTo(4));
            }
        }

        /// <summary>Two successful offers exhaust the visit even when more candidates remain.</summary>
        [Test]
        public void MaximumTwoOffersPerVisit()
        {
            var roller = new CardOfferRoller(new RunRandom(42));
            var catalog = CreateCatalog(CreateCard("a"), CreateCard("b"), CreateCard("c"),
                CreateCard("d"), CreateCard("e"));
            Assert.That(roller.OffersMade, Is.Zero);
            Assert.That(roller.CanOffer, Is.True);
            Assert.That(roller.TryRoll(catalog, null, out _), Is.True);
            Assert.That(roller.OffersMade, Is.EqualTo(1));
            Assert.That(roller.CanOffer, Is.True);
            Assert.That(roller.TryRoll(catalog, null, out _), Is.True);
            Assert.That(roller.OffersMade, Is.EqualTo(2));
            Assert.That(roller.CanOffer, Is.False);
            Assert.That(roller.TryRoll(catalog, null, out CardOffer rejected), Is.False);
            Assert.That(rejected.Count, Is.Zero);
            Assert.That(roller.OffersMade, Is.EqualTo(2));
        }

        /// <summary>A lone eligible card is a valid offer and cannot appear again after refusal.</summary>
        [Test]
        public void SingleCandidateProducesOneCardOffer()
        {
            var card = CreateCard("only");
            var catalog = CreateCatalog(null, card, card);
            var roller = new CardOfferRoller(new RunRandom(1));
            Assert.That(roller.TryRoll(catalog, null, out CardOffer offer), Is.True);
            Assert.That(offer.Count, Is.EqualTo(1));
            Assert.That(offer.Get(0), Is.SameAs(card));
            Assert.That(roller.OffersMade, Is.EqualTo(1));
            Assert.That(roller.TryRoll(catalog, null, out offer), Is.False);
            Assert.That(offer.Count, Is.Zero);
            Assert.That(roller.OffersMade, Is.EqualTo(1));
        }

        /// <summary>Invalid or exhausted catalogs do not consume an offer or advance the RNG.</summary>
        [Test]
        public void ZeroCandidatesAndNullCatalogDoNotConsumeOffer()
        {
            var roller = new CardOfferRoller(new RunRandom(7));
            var inventory = new CardInventory(new StatSheet());
            var owned = CreateCard("owned");
            inventory.TryAcquire(owned);
            Assert.That(roller.TryRoll(null, inventory, out CardOffer offer), Is.False);
            Assert.That(offer.Count, Is.Zero);
            Assert.That(roller.TryRoll(CreateCatalog(), inventory, out offer), Is.False);
            Assert.That(roller.TryRoll(CreateCatalog(null, owned), inventory, out offer), Is.False);
            Assert.That(roller.OffersMade, Is.Zero);
            Assert.That(roller.CanOffer, Is.True);
            var catalog = CreateCatalog(CreateCard("a"), CreateCard("b"), CreateCard("c"));
            var fresh = new CardOfferRoller(new RunRandom(7));
            Assert.That(roller.TryRoll(catalog, inventory, out offer), Is.True);
            Assert.That(fresh.TryRoll(catalog, inventory, out CardOffer expected), Is.True);
            Assert.That(offer.Get(0), Is.SameAs(expected.Get(0)));
            Assert.That(offer.Get(1), Is.SameAs(expected.Get(1)));
        }

        /// <summary>Offer slots compact nulls and containment requires the actual offered definition.</summary>
        [Test]
        public void OfferCompactsNullEntriesAndChecksExactMembership()
        {
            var card = CreateCard("card");
            var duplicate = CreateCard("card");
            CardOffer empty = default;
            Assert.That(empty.Count, Is.Zero);
            Assert.That(empty.Contains(null), Is.False);
            var offer = new CardOffer(null, card);
            Assert.That(offer.Count, Is.EqualTo(1));
            Assert.That(offer.Get(0), Is.SameAs(card));
            Assert.That(offer.Contains(card), Is.True);
            Assert.That(offer.Contains(duplicate), Is.False);
            Assert.That(offer.Contains(null), Is.False);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => offer.Get(1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => offer.Get(-1));
        }
    }
}
