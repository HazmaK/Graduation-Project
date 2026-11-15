using NUnit.Framework;
using RetroFPS.Pause;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Tests.EditMode;
using RetroFPS.UI.HUD;
using UnityEngine;

namespace RetroFPS.Tests
{
    public sealed class CardOfferViewTests
    {
        private GameObject root, layer;
        private CardOfferView view;
        private CurseBlessingDefinition card;
        private IPauseService previous;
        private PauseSnapshot original;
        private int resolutions;
        [SetUp]
        public void SetUp()
        {
            original = new PauseSnapshot(Time.timeScale, Time.fixedDeltaTime, AudioListener.pause, (int)Cursor.lockState, Cursor.visible);
            previous = GamePause.Service; GamePause.SetService(null);
            RunEvents.SetCurrentRun(new RunState(123));
            card = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            card.Configure("test", "name", "boon", "curse", "flavor", Color.white, null);
            root = new GameObject("Offer test"); root.SetActive(false);
            view = root.AddComponent<CardOfferView>();
            layer = new GameObject("Layer"); layer.transform.SetParent(root.transform); layer.SetActive(false);
            LocalizationHudTestContext.Set(view, "offerLayer", layer);
            foreach (string field in new[] { "first", "second", "refuse" })
            {
                var choice = new GameObject(field); choice.transform.SetParent(layer.transform);
                LocalizationHudTestContext.Set(view, field, choice.AddComponent<CardOfferChoiceView>());
            }
            resolutions = 0; RunEvents.OnCardOfferResolved += Resolved;
            root.SetActive(true);
            LocalizationHudTestContext.Call(view, "OnEnable"); // EditMode does not run OnEnable for plain MonoBehaviours.
        }
        [TearDown]
        public void TearDown()
        {
            LocalizationHudTestContext.Call(view, "OnDisable");
            Object.DestroyImmediate(root); Object.DestroyImmediate(card);
            RunEvents.OnCardOfferResolved -= Resolved; RunEvents.SetCurrentRun(null);
            GamePause.SetService(previous);
            Time.timeScale = original.TimeScale; Time.fixedDeltaTime = original.FixedDeltaTime;
            AudioListener.pause = original.AudioPaused; Cursor.lockState = (CursorLockMode)original.CursorLockMode; Cursor.visible = original.CursorVisible;
        }
        private void Resolved(CurseBlessingDefinition chosen) => resolutions++;
        private void Open() => RunEvents.RaiseCardOfferOpened(new CardOffer(card, null));
        [Test]
        public void ModalBlocksFacadeAndRestoresExactEnvironmentAfterSingleConfirmation()
        {
            Time.timeScale = 0.37f; AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.Confined; Cursor.visible = false;
            Open();
            Assert.That(layer.activeSelf, Is.True); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(GamePause.IsGameplayInputBlocked, Is.True); Assert.That(GamePause.IsSimulationBlocked, Is.True);
            Assert.That(AudioListener.pause, Is.True); Assert.That(Cursor.visible, Is.True);
            view.Confirm(); Assert.That(resolutions, Is.Zero);
            ((CardOfferGate)LocalizationHudTestContext.Get(view, "gate")).Tick(0.4d);
            view.Confirm(); view.Confirm();
            Assert.That(resolutions, Is.EqualTo(1)); Assert.That(RunEvents.CurrentRun.Cards.Count, Is.EqualTo(1));
            Assert.That(view.IsOpen, Is.False); Assert.That(layer.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.37f)); Assert.That(AudioListener.pause, Is.False);
            Assert.That(GamePause.Service, Is.Null); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Confined)); Assert.That(Cursor.visible, Is.False);
        }
        [Test]
        public void RefusalAcquiresNothingAndDoesNotResolveTwice()
        {
            Open(); view.Select(1);
            ((CardOfferGate)LocalizationHudTestContext.Get(view, "gate")).Tick(0.4d);
            view.Confirm(); view.Confirm();
            Assert.That(resolutions, Is.EqualTo(1)); Assert.That(RunEvents.CurrentRun.Cards.Count, Is.Zero);
        }
        [Test]
        public void RunChangeClosesWithoutResolving()
        {
            Open(); RunEvents.SetCurrentRun(new RunState(456)); view.Confirm();
            Assert.That(resolutions, Is.Zero); Assert.That(view.IsOpen, Is.False); Assert.That(GamePause.Service, Is.Null);
        }
        [Test]
        public void TrueDeathResolvesAsRefusedOnceAndKeepsCursorAvailable()
        {
            Open();
            LocalizationHudTestContext.Call(view, "TrueDeath"); view.Confirm();
            Assert.That(resolutions, Is.EqualTo(1)); Assert.That(RunEvents.CurrentRun.Cards.Count, Is.Zero);
            Assert.That(view.IsOpen, Is.False); Assert.That(Cursor.visible, Is.True);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
        }
        [Test]
        public void ExternalResolutionRestoresAndPreventsASecondRequest()
        {
            Open(); RunEvents.ResolveCardOffer(null); view.Confirm();
            Assert.That(resolutions, Is.EqualTo(1)); Assert.That(view.IsOpen, Is.False); Assert.That(GamePause.Service, Is.Null);
        }
    }
}
