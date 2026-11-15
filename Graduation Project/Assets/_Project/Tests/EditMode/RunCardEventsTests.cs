using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Stats;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Run card lifecycle, event routing and run-owned dependencies.</summary>
    public sealed class RunCardEventsTests : CardTestFixture
    {
        /// <summary>A valid choice applies modifiers, publishes acquisition, then resolves once.</summary>
        [Test]
        public void ValidChoiceAcquiresAndClosesOfferInEventOrder()
        {
            var run = new RunState(42);
            RunEvents.SetCurrentRun(run);
            var chosen = CreateCard("chosen", new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 25f));
            var other = CreateCard("other");
            var events = new List<string>();
            RunEvents.OnCardOfferOpened += offer =>
            {
                Assert.That(RunEvents.IsCardOfferOpen, Is.True);
                Assert.That(RunEvents.CurrentCardOffer.Contains(chosen), Is.True);
                events.Add("opened");
            };
            run.Cards.OnCardAcquired += card => events.Add("inventory");
            RunEvents.OnCardAcquired += card =>
            {
                Assert.That(card, Is.SameAs(chosen));
                Assert.That(run.Stats.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(125f));
                events.Add("acquired");
            };
            RunEvents.OnCardOfferResolved += card =>
            {
                Assert.That(card, Is.SameAs(chosen));
                Assert.That(RunEvents.IsCardOfferOpen, Is.False);
                Assert.That(RunEvents.CurrentCardOffer.Count, Is.Zero);
                events.Add("resolved");
            };
            RunEvents.RaiseCardOfferOpened(new CardOffer(chosen, other));
            RunEvents.ResolveCardOffer(chosen);
            RunEvents.ResolveCardOffer(chosen);
            Assert.That(run.Cards.Count, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(new[] { "opened", "inventory", "acquired", "resolved" }));
        }

        /// <summary>Explicit refusal and foreign choices, including matching identities, resolve to null.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void RefusalOrForeignCardClosesWithoutAcquisition(bool foreign)
        {
            var run = new RunState(42);
            RunEvents.SetCurrentRun(run);
            var offered = CreateCard("offered");
            var choice = foreign ? CreateCard("offered") : null;
            int acquired = 0;
            int resolved = 0;
            RunEvents.OnCardAcquired += card => acquired++;
            RunEvents.OnCardOfferResolved += card => { resolved++; Assert.That(card, Is.Null); };
            RunEvents.RaiseCardOfferOpened(new CardOffer(offered, null));
            RunEvents.ResolveCardOffer(choice);
            Assert.That(RunEvents.IsCardOfferOpen, Is.False);
            Assert.That(RunEvents.CurrentCardOffer.Count, Is.Zero);
            Assert.That(run.Cards.Count, Is.Zero);
            Assert.That(acquired, Is.Zero);
            Assert.That(resolved, Is.EqualTo(1));
        }

        /// <summary>Resolving without an open offer never acquires or publishes resolution.</summary>
        [Test]
        public void NoOpenOfferMakesResolutionANoOp()
        {
            var run = new RunState(42);
            RunEvents.SetCurrentRun(run);
            int events = 0;
            RunEvents.OnCardAcquired += card => events++;
            RunEvents.OnCardOfferResolved += card => events++;
            RunEvents.ResolveCardOffer(CreateCard("foreign"));
            RunEvents.ResolveCardOffer(null);
            Assert.That(run.Cards.Count, Is.Zero);
            Assert.That(events, Is.Zero);
        }

        /// <summary>An already-open offer cannot be replaced or raise a second opened event.</summary>
        [Test]
        public void OpeningWhileOpenPreservesOriginalOffer()
        {
            var first = CreateCard("first");
            var second = CreateCard("second");
            int opened = 0;
            RunEvents.OnCardOfferOpened += offer => opened++;
            RunEvents.RaiseCardOfferOpened(new CardOffer(first, null));
            RunEvents.RaiseCardOfferOpened(new CardOffer(second, null));
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(RunEvents.CurrentCardOffer.Contains(first), Is.True);
            Assert.That(RunEvents.CurrentCardOffer.Contains(second), Is.False);
        }

        /// <summary>Replacing or clearing the run clears an unresolved offer before run-change callbacks.</summary>
        [Test]
        public void RunChangesClearOpenOfferState()
        {
            RunEvents.SetCurrentRun(new RunState(1));
            var card = CreateCard("card");
            RunEvents.OnRunChanged += run =>
            {
                Assert.That(RunEvents.IsCardOfferOpen, Is.False);
                Assert.That(RunEvents.CurrentCardOffer.Count, Is.Zero);
            };
            RunEvents.RaiseCardOfferOpened(new CardOffer(card, null));
            RunEvents.SetCurrentRun(new RunState(2));
            RunEvents.RaiseCardOfferOpened(new CardOffer(card, null));
            RunEvents.SetCurrentRun(null);
            Assert.That(RunEvents.IsCardOfferOpen, Is.False);
        }

        /// <summary>Missing runs and failed acquisitions still close and resolve as refusal.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FailedAcquisitionResolvesAsRefused(bool hasRun)
        {
            var card = CreateCard("card");
            if (hasRun)
            {
                var run = new RunState(42);
                run.Cards.TryAcquire(card);
                RunEvents.SetCurrentRun(run);
            }
            int resolved = 0;
            int acquired = 0;
            RunEvents.OnCardOfferResolved += choice => { resolved++; Assert.That(choice, Is.Null); };
            RunEvents.OnCardAcquired += choice => acquired++;
            RunEvents.RaiseCardOfferOpened(new CardOffer(card, null));
            RunEvents.ResolveCardOffer(card);
            Assert.That(RunEvents.IsCardOfferOpen, Is.False);
            Assert.That(resolved, Is.EqualTo(1));
            Assert.That(acquired, Is.Zero);
        }

        /// <summary>An acquisition callback cannot resolve the same offer recursively.</summary>
        [Test]
        public void ReentrantResolveDoesNotResolveOrAcquireTwice()
        {
            var run = new RunState(42);
            RunEvents.SetCurrentRun(run);
            var first = CreateCard("first");
            var second = CreateCard("second");
            int resolved = 0;
            run.Cards.OnCardAcquired += card => RunEvents.ResolveCardOffer(second);
            RunEvents.OnCardOfferResolved += card => resolved++;
            RunEvents.RaiseCardOfferOpened(new CardOffer(first, second));
            RunEvents.ResolveCardOffer(first);
            Assert.That(run.Cards.Count, Is.EqualTo(1));
            Assert.That(run.Cards.Has(second), Is.False);
            Assert.That(resolved, Is.EqualTo(1));
        }

        /// <summary>Run dependencies share the correct sheet and dedicated cached RNG stream.</summary>
        [Test]
        public void RunStateOwnsStatsInventoryAndDedicatedCardRoller()
        {
            var run = new RunState(42);
            Assert.That(run.Stats, Is.Not.Null);
            Assert.That(run.Cards, Is.Not.Null);
            Assert.That(run.CardOffers, Is.Not.Null);
            var card = CreateCard("owned", new StatModifier(StatId.MaxHealth, StatOp.AddFlat, 10f));
            Assert.That(run.Cards.TryAcquire(card), Is.True);
            Assert.That(run.Stats.Evaluate(StatId.MaxHealth, 100f), Is.EqualTo(110f));
            var catalog = CreateCatalog(CreateCard("a"), CreateCard("b"), CreateCard("c"));
            var rng = run.GetStream(RunStream.ArafCards);
            Assert.That(run.GetStream(RunStream.ArafCards), Is.SameAs(rng));
            var expectedRng = RunRandom.Derive(42, RunStream.ArafCards);
            Assert.That(rng.NextUInt(), Is.EqualTo(expectedRng.NextUInt()));
            var expectedRoller = new CardOfferRoller(expectedRng);
            Assert.That(run.CardOffers.TryRoll(catalog, run.Cards, out CardOffer actual), Is.True);
            Assert.That(expectedRoller.TryRoll(catalog, run.Cards, out CardOffer expected), Is.True);
            Assert.That(actual.Get(0), Is.SameAs(expected.Get(0)));
            Assert.That(actual.Get(1), Is.SameAs(expected.Get(1)));
            Assert.That(rng.NextUInt(), Is.EqualTo(expectedRng.NextUInt()));
            var fresh = new RunState(42);
            Assert.That(fresh.Stats, Is.Not.SameAs(run.Stats));
            Assert.That(fresh.Cards.Count, Is.Zero);
            Assert.That(fresh.CardOffers.OffersMade, Is.Zero);
        }

        /// <summary>The fallback stream array also supports the new card stream without an active run.</summary>
        [Test]
        public void FallbackSupportsCardStream()
        {
            var rng = RunEvents.GetStream(RunStream.ArafCards);
            Assert.That(rng, Is.Not.Null);
            Assert.That(RunEvents.GetStream(RunStream.ArafCards), Is.SameAs(rng));
        }

        /// <summary>SubsystemRegistration reset clears any open card-offer state.</summary>
        [Test]
        public void ResetStaticsClearsOpenOfferState()
        {
            RunEvents.RaiseCardOfferOpened(new CardOffer(CreateCard("open"), null));
            Assert.That(RunEvents.IsCardOfferOpen, Is.True);
            MethodInfo reset = typeof(RunEvents).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            var attr = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration));
            reset.Invoke(null, null);
            Assert.That(RunEvents.IsCardOfferOpen, Is.False);
            Assert.That(RunEvents.CurrentCardOffer.Count, Is.Zero);
        }
    }
}
