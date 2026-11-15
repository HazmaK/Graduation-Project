using System.Reflection;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Stats;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    public sealed class ArafCardAltarTests
    {
        private GameObject _root;
        private GameObject _player;
        private GameObject _active;
        private GameObject _spent;
        private ArafCardAltar _altar;
        private CurseBlessingDefinition _card;
        private Collider _playerCollider;

        [SetUp]
        public void SetUp()
        {
            RunEvents.SetCurrentRun(new RunState(123));
            _card = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            _card.Configure("altar_test", "name", "boon", "curse", "flavor", Color.red,
                new StatModifier[0]);
            _root = new GameObject("AltarTest");
            _altar = _root.AddComponent<ArafCardAltar>();
            _active = new GameObject("Active");
            _active.transform.SetParent(_root.transform);
            _spent = new GameObject("Spent");
            _spent.transform.SetParent(_root.transform);
            _spent.SetActive(false);
            SerializedObject settings = new SerializedObject(_altar);
            settings.FindProperty("activeVisual").objectReferenceValue = _active;
            settings.FindProperty("spentVisual").objectReferenceValue = _spent;
            settings.FindProperty("armDelay").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // Invoke lifecycle explicitly: these are EditMode tests without physics or a frame loop.
            Invoke("OnDisable");
            Invoke("Awake");
            Invoke("OnEnable");
            _player = new GameObject("PlayerTest");
            _player.SetActive(false);
            _player.AddComponent<PlayerHealth>();
            _playerCollider = _player.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_altar != null) Invoke("OnDisable");
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_player);
            RunEvents.SetCurrentRun(null);
            Object.DestroyImmediate(_card);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnOfferResolutionSpendsAltarPermanently(bool choose)
        {
            _altar.Initialize(new CardOffer(_card, null));
            Invoke("OnTriggerEnter", _playerCollider);
            Assert.That(RunEvents.IsCardOfferOpen, Is.True);
            Assert.That(_altar.IsSpent, Is.False);
            RunEvents.ResolveCardOffer(choose ? _card : null);
            Assert.That(_altar.IsSpent, Is.True);
            Assert.That(_root.GetComponent<SphereCollider>().enabled, Is.False);
            Assert.That(_active.activeSelf, Is.False);
            Assert.That(_spent.activeSelf, Is.True);
            _altar.Initialize(new CardOffer(_card, null));
            Invoke("OnTriggerStay", _playerCollider);
            Assert.That(RunEvents.IsCardOfferOpen, Is.False);
            Invoke("OnDisable");
            Invoke("OnEnable");
            Assert.That(_altar.IsSpent, Is.True);
        }

        [Test]
        public void ForeignOfferResolutionDoesNotSpendInitializedAltar()
        {
            _altar.Initialize(new CardOffer(_card, null));
            RunEvents.RaiseCardOfferOpened(new CardOffer(_card, null));
            Invoke("OnTriggerEnter", _playerCollider);
            RunEvents.ResolveCardOffer(null);
            Assert.That(_altar.IsSpent, Is.False);
            Assert.That(_active.activeSelf, Is.True);
            Assert.That(_root.GetComponent<SphereCollider>().enabled, Is.True);
        }

        [Test]
        public void SpawnDelayBlocksEnterAndStay()
        {
            SerializedObject settings = new SerializedObject(_altar);
            settings.FindProperty("armDelay").floatValue = 10f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            _altar.Initialize(new CardOffer(_card, null));
            Invoke("OnTriggerEnter", _playerCollider);
            Invoke("OnTriggerStay", _playerCollider);
            Assert.That(RunEvents.IsCardOfferOpen, Is.False);
            typeof(ArafCardAltar).GetField("_armedAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_altar, Time.time - 1f);
            Invoke("OnTriggerStay", _playerCollider);
            Assert.That(RunEvents.IsCardOfferOpen, Is.True);
        }

        [Test]
        public void ResolutionDuringOpenedEventStillSpendsAltarAndCannotReopenOnStay()
        {
            int opened = 0;
            System.Action<CardOffer> resolveImmediately = offer =>
            {
                opened++;
                RunEvents.ResolveCardOffer(null);
            };
            RunEvents.OnCardOfferOpened += resolveImmediately;
            try
            {
                _altar.Initialize(new CardOffer(_card, null));
                Invoke("OnTriggerEnter", _playerCollider);
                Invoke("OnTriggerStay", _playerCollider);
                Invoke("OnTriggerEnter", _playerCollider);
                Assert.That(opened, Is.EqualTo(1));
                Assert.That(_altar.IsSpent, Is.True);
            }
            finally { RunEvents.OnCardOfferOpened -= resolveImmediately; }
        }

        [Test]
        public void DisabledAltarDoesNotConsumeForeignResolution()
        {
            _altar.Initialize(new CardOffer(_card, null));
            Invoke("OnDisable");
            RunEvents.RaiseCardOfferOpened(new CardOffer(_card, null));
            RunEvents.ResolveCardOffer(null);
            Assert.That(_altar.IsSpent, Is.False);
        }

        private void Invoke(string method, params object[] arguments)
        {
            typeof(ArafCardAltar).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_altar, arguments);
        }
    }
}
