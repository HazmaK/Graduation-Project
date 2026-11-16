using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RetroFPS.Combat;
using RetroFPS.Pause;
using UnityEngine;
using UnityEngine.TestTools;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Records melee stun delivery without enemy simulation.</summary>
    public sealed class ParryStunProbe : MonoBehaviour, IStunnable
    {
        private bool _wasStunned;
        /// <summary>Whether the attacker received a stun.</summary>
        public bool WasStunned => _wasStunned;
        /// <inheritdoc/>
        public void Stun(float duration, Vector3 knockbackDir) => _wasStunned = true;
    }

    /// <summary>Deterministic parry guards and cone checks with injected private state.</summary>
    public sealed class PlayerParryControllerTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _player;
        private GameObject _attacker;
        private PlayerParryController _parry;
        private Camera _camera;
        private CounterVerdictTimer _timer;
        private ParryStunProbe _stun;
        private IPauseService _previousPause;
        private float _previousDelta;

        /// <summary>Builds an isolated player without running feedback initialization.</summary>
        [SetUp]
        public void SetUp()
        {
            _previousPause = GamePause.Service;
            _previousDelta = GamePause.UnscaledDeltaTime;
            GamePause.SetService(null);
            _player = new GameObject("Parry test player");
            _player.SetActive(false);
            var health = _player.AddComponent<PlayerHealth>();
            Set(health, "_currentHealth", 100f);
            _parry = _player.AddComponent<PlayerParryController>();
            var cameraObject = new GameObject("Parry test camera");
            cameraObject.transform.SetParent(_player.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _timer = new CounterVerdictTimer(0.18f, 0.35f);
            Set(_parry, "_timer", _timer);
            Set(_parry, "_cam", _camera);
            _attacker = new GameObject("Parry test attacker");
            _attacker.transform.position = Vector3.forward * 2f;
            _stun = _attacker.AddComponent<ParryStunProbe>();
            _player.SetActive(true);
        }

        /// <summary>Destroys the fixture and restores the pause owner and clock delta.</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_attacker);
            Object.DestroyImmediate(_player);
            GamePause.SetService(_previousPause);
            typeof(GamePause).GetField("unscaledDeltaTime", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, _previousDelta);
        }

        /// <summary>Melee cannot be blocked without an open strike window.</summary>
        [Test]
        public void NoActiveStrike_RejectsMelee()
        {
            Assert.That(ParryAttacker(), Is.False);
            Assert.That(_stun.WasStunned, Is.False);
        }

        /// <summary>Disabling Counter-Verdict rejects an otherwise valid melee parry.</summary>
        [Test]
        public void CounterDisabled_RejectsMelee()
        {
            _timer.TryStart();
            Set(_parry, "enableCounter", false);
            Assert.That(ParryAttacker(), Is.False);
            Assert.That(_parry.IsParrying, Is.False);
        }

        /// <summary>A disabled component cannot parry even with injected active timer state.</summary>
        [Test]
        public void ComponentDisabled_RejectsMelee()
        {
            _parry.enabled = false;
            _timer.TryStart();
            Assert.That(ParryAttacker(), Is.False);
            Assert.That(_parry.IsParrying, Is.False);
        }

        /// <summary>Behind targets are rejected, including when camera aim is vertical.</summary>
        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(-90f)]
        public void AttackerBehind_RejectsMelee(float cameraPitch)
        {
            _timer.TryStart();
            _camera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            _attacker.transform.position = Vector3.back * 2f;
            Assert.That(ParryAttacker(), Is.False);
            Assert.That(_stun.WasStunned, Is.False);
        }

        /// <summary>A missing camera cannot accept a target in the facing cone.</summary>
        [Test]
        public void NoCamera_IsFacingTargetFalse()
        {
            Set(_parry, "_cam", null);
            Assert.That(_parry.IsFacingTarget(Vector3.forward), Is.False);
        }

        /// <summary>Coincident targets are inside the projectile acceptance cone.</summary>
        [Test]
        public void TargetAtCamera_IsFacingTargetTrue()
        {
            Assert.That(_parry.IsFacingTarget(_camera.transform.position), Is.True);
        }

        /// <summary>Inactive configurations suppress recovery, facing and projectile success.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void InactiveCounter_ReportsNoCooldownOrContactSuccess(bool disableComponent)
        {
            if (disableComponent) _parry.enabled = false;
            else Set(_parry, "enableCounter", false);
            _timer.TryStart();
            _parry.NotifyProjectileParried();
            Assert.That(_timer.IsReady, Is.False);
            Assert.That(_parry.IsFacingTarget(Vector3.forward), Is.False);
            _timer.Tick(0.18f);
            Assert.That(_timer.IsRecovering, Is.True);
            Assert.That(_parry.IsOnCooldown, Is.False);
            Assert.That(_parry.CooldownRemaining, Is.Zero);
            Assert.That(_parry.CooldownNormalized, Is.Zero);
        }

        /// <summary>A frontal strike delivers the stun and succeeds without advancing a frame.</summary>
        [Test]
        public void ActiveStrikeInFront_ParriesAndStunsAttacker()
        {
            // The production VFX uses deferred Destroy; expect only those EditMode errors.
            var previousObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var previousRandom = Random.state;
            string[] materialFields = { "_ringMaterial", "_sparkMaterial", "_glintMaterial" };
            var previousMaterials = new object[materialFields.Length];
            for (int i = 0; i < materialFields.Length; i++)
                previousMaterials[i] = GetStatic(typeof(ParryVFX), materialFields[i]);
            object previousAudio = GetStatic(typeof(CombatAudio), "_isQuitting");
            object previousNumbers = GetStatic(typeof(DamageNumberSystem), "_isQuitting");
            object previousAudioInstance = GetStatic(typeof(CombatAudio), "_instance");
            object previousNumbersInstance = GetStatic(typeof(DamageNumberSystem), "_instance");
            object previousHitstop = GetStatic(typeof(HitstopManager), "<Instance>k__BackingField");
            try
            {
                SetStatic(typeof(CombatAudio), "_isQuitting", true);
                SetStatic(typeof(DamageNumberSystem), "_isQuitting", true);
                SetStatic(typeof(CombatAudio), "_instance", null);
                SetStatic(typeof(DamageNumberSystem), "_instance", null);
                SetStatic(typeof(HitstopManager), "<Instance>k__BackingField", null);
                _timer.TryStart();
                for (int i = 0; i < 10; i++)
                    LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

                Assert.That(ParryAttacker(), Is.True);
                Assert.That(_stun.WasStunned, Is.True);
                Assert.That(_timer.IsReady, Is.True);
            }
            finally
            {
                foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if ((obj.name == "Parry_ShockwaveRing" || obj.name == "Parry_Spark")
                        && System.Array.IndexOf(previousObjects, obj) < 0)
                        Object.DestroyImmediate(obj);
                for (int i = 0; i < materialFields.Length; i++)
                {
                    var current = (Material)GetStatic(typeof(ParryVFX), materialFields[i]);
                    if (current != null && current != (Material)previousMaterials[i]) Object.DestroyImmediate(current);
                    SetStatic(typeof(ParryVFX), materialFields[i], previousMaterials[i]);
                }
                Random.state = previousRandom;
                SetStatic(typeof(CombatAudio), "_isQuitting", previousAudio);
                SetStatic(typeof(DamageNumberSystem), "_isQuitting", previousNumbers);
                SetStatic(typeof(CombatAudio), "_instance", previousAudioInstance);
                SetStatic(typeof(DamageNumberSystem), "_instance", previousNumbersInstance);
                SetStatic(typeof(HitstopManager), "<Instance>k__BackingField", previousHitstop);
            }
        }

        private bool ParryAttacker() => _parry.TryParryMelee(_attacker.transform.position, Vector3.back, 10f, _attacker);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, PrivateInstance).SetValue(target, value);
        private static object GetStatic(System.Type type, string name) => type
            .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static void SetStatic(System.Type type, string name, object value) => type
            .GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    }
}
