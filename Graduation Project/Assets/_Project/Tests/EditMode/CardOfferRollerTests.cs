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
