using System;
using NUnit.Framework;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    public sealed class MainMenuStateMachineTests
    {
        private static MainMenuStateMachine Ready()
        {
            var machine = new MainMenuStateMachine();
            machine.Tick(0.6f);
            return machine;
        }

        [Test]
        public void FirstLoadLocksInputUntilFadeEnds()
        {
            var machine = new MainMenuStateMachine();
            Assert.That(machine.InputLocked, Is.True);
            Assert.That(machine.Start(), Is.False);
            Assert.That(machine.OpenSettings(), Is.False);
            Assert.That(machine.Back(), Is.False);
            machine.Tick(0.59f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.FirstLoad));
            machine.Tick(0.02f);
            Assert.That(machine.InputLocked, Is.False);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Start));
        }

        [Test]
        public void ReducedFirstLoadEndsWithin100Milliseconds()
        {
            var machine = new MainMenuStateMachine(reduceMotion: true);
            machine.Tick(0.1f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.Root));
        }

        [Test]
        public void RootBackIsSilentNoOp()
        {
            var machine = Ready();
            int sounds = 0;
            Action<UiAudioEventType> handler = _ => sounds++;
            MenuEvents.UiAudio += handler;
            try
            {
                Assert.That(machine.Back(), Is.False);
                Assert.That(machine.State, Is.EqualTo(MainMenuState.Root));
                Assert.That(machine.InputLocked, Is.False);
                Assert.That(sounds, Is.Zero);
            }
            finally { MenuEvents.UiAudio -= handler; }
        }

        [Test]
        public void SettingsBackRestoresSettingsOpener()
        {
            var machine = Ready();
            Assert.That(machine.OpenSettings(), Is.True);
            machine.Tick(0.08f);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.MasterVolume));
            Assert.That(machine.Start(), Is.False);
            Assert.That(machine.OpenQuitConfirm(), Is.False);
            Assert.That(machine.Back(), Is.True);
            machine.Tick(0.08f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.Root));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Settings));
        }

        [Test]
        public void QuitDefaultsToNoAndBackRestoresQuitOpener()
        {
            var machine = Ready();
            machine.OpenQuitConfirm();
            machine.Tick(0.08f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.QuitConfirm));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.No));
            Assert.That(machine.Start(), Is.False);
            machine.Back();
            machine.Tick(0.08f);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Quit));
        }

        [Test]
        public void PressLockDefersActionAndRejectsDoubleActivation()
        {
            var machine = Ready();
            machine.OpenSettings();
            Assert.That(machine.InputLocked, Is.True);
            Assert.That(machine.OpenQuitConfirm(), Is.False);
            Assert.That(machine.Back(), Is.False);
            machine.Tick(0.04f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.Root));
            machine.Tick(0.04f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.Settings));
            Assert.That(machine.InputLocked, Is.False);
        }

        [Test]
        public void StartFiresOnceAndLoadingRejectsAllActions()
        {
            var machine = Ready();
            int starts = 0;
            string scene = null;
            Action<string> handler = name => { starts++; scene = name; };
            MenuEvents.StartRequested += handler;
            try
            {
                Assert.That(machine.Start(), Is.True);
                Assert.That(starts, Is.Zero);
                Assert.That(machine.Start(), Is.False);
                machine.Tick(0.08f);
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(scene, Is.EqualTo("TestLevel_Gauntlet"));
                Assert.That(machine.InputLocked, Is.True);
                Assert.That(machine.Back(), Is.False);
                Assert.That(machine.Start(), Is.False);
                Assert.That(machine.OpenSettings(), Is.False);
                Assert.That(machine.OpenQuitConfirm(), Is.False);
                Assert.That(machine.ConfirmQuit(), Is.False);
                Assert.That(machine.KeepDisplay(), Is.False);
                Assert.That(machine.RevertDisplay(), Is.False);
                Assert.That(machine.OpenRevertPrompt(MenuFocusTarget.Resolution), Is.False);
                machine.Tick(100f);
                Assert.That(starts, Is.EqualTo(1));
                machine.LoadFailed();
                Assert.That(machine.State, Is.EqualTo(MainMenuState.Root));
                Assert.That(machine.HasLoadError, Is.True);
                Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Start));
                Assert.That(machine.InputLocked, Is.False);
                machine.Start();
                machine.Tick(0.08f);
                Assert.That(starts, Is.EqualTo(2));
                Assert.That(machine.HasLoadError, Is.False);
            }
            finally { MenuEvents.StartRequested -= handler; }
        }

        [Test]
        public void LoadFailureNotificationIsNotRepublishedDuringRecovery()
        {
            var machine = Ready();
            machine.Start();
            machine.Tick(0.08f);
            int notifications = 0;
            Action observer = () => notifications++;
            Action recover = machine.LoadFailed;
            MenuEvents.LoadFailed += observer;
            MenuEvents.LoadFailed += recover;
            try
            {
                MenuEvents.RaiseLoadFailed();
                Assert.That(notifications, Is.EqualTo(1));
                Assert.That(machine.State, Is.EqualTo(MainMenuState.Root));
                Assert.That(machine.HasLoadError, Is.True);
                Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Start));
                Assert.That(machine.InputLocked, Is.False);
            }
            finally
            {
                MenuEvents.LoadFailed -= observer;
                MenuEvents.LoadFailed -= recover;
            }
        }

        [TestCase(MenuFocusTarget.Resolution)]
        [TestCase(MenuFocusTarget.WindowMode)]
        public void DisplayPromptSupersedesPendingSettingsBack(MenuFocusTarget opener)
        {
            var machine = Ready();
            machine.OpenSettings();
            machine.Tick(0.08f);
            machine.Back();
            machine.Tick(0.04f);
            Assert.That(machine.InputLocked, Is.True);
            Assert.That(machine.OpenRevertPrompt(opener), Is.True);
            machine.Tick(0.04f);
            Assert.That(machine.State, Is.EqualTo(MainMenuState.RevertPrompt));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Revert));
            Assert.That(machine.InputLocked, Is.False);
            bool? result = null;
            machine.DisplayResolved += keep => result = keep;
            machine.Tick(10f);
            Assert.That(result, Is.False);
            Assert.That(machine.FocusTarget, Is.EqualTo(opener));
        }

        [TestCase(MenuFocusTarget.Resolution)]
        [TestCase(MenuFocusTarget.WindowMode)]
        public void DisplayExpiryRevertsAndRestoresOpener(MenuFocusTarget opener)
        {
            var machine = Ready();
            machine.OpenSettings();
            machine.Tick(0.08f);
            bool? kept = null;
            int resolutions = 0;
            machine.DisplayResolved += keep => { kept = keep; resolutions++; };
            Assert.That(machine.OpenRevertPrompt(opener), Is.True);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Revert));
            machine.Tick(10f);
            machine.Tick(10f);
            Assert.That(kept, Is.False);
            Assert.That(resolutions, Is.EqualTo(1));
            Assert.That(machine.State, Is.EqualTo(MainMenuState.Settings));
            Assert.That(machine.FocusTarget, Is.EqualTo(opener));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DisplayKeepAndBackStopDeadline(bool keep)
        {
            var machine = Ready();
            machine.OpenSettings();
            machine.Tick(0.08f);
            machine.OpenRevertPrompt(MenuFocusTarget.Resolution);
            bool? result = null;
            machine.DisplayResolved += value => result = value;
            if (keep) machine.KeepDisplay(); else machine.Back();
            machine.Tick(0.08f);
            Assert.That(result, Is.EqualTo(keep));
            Assert.That(machine.Countdown.Running, Is.False);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Resolution));
        }

        [Test]
        public void DeadlineWinsOverUnfinishedKeepPress()
        {
            var machine = Ready();
            machine.OpenSettings();
            machine.Tick(0.08f);
            machine.OpenRevertPrompt(MenuFocusTarget.Resolution, 0.04f);
            bool? result = null;
            machine.DisplayResolved += value => result = value;
            machine.KeepDisplay();
            machine.Tick(0.08f);
            Assert.That(result, Is.False);
        }
    }
}
