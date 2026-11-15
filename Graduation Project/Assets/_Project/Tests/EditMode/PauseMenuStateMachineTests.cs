using System;
using NUnit.Framework;
using RetroFPS.Pause;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    public sealed class PauseMenuStateMachineTests
    {
        private static PauseMenuStateMachine Ready()
        {
            var machine = new PauseMenuStateMachine();
            Assert.That(machine.Open(), Is.True);
            return machine;
        }
        private static void Settings(PauseMenuStateMachine machine)
        { Assert.That(machine.OpenSettings(), Is.True); machine.Tick(0.08f); }

        [Test]
        public void ClosedRejectsEveryCommandUntilAcquisitionOpensRoot()
        {
            var machine = new PauseMenuStateMachine();
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Closed));
            Assert.That(machine.InputLocked, Is.True);
            Assert.That(machine.Back(), Is.False);
            Assert.That(machine.ToggleResume(), Is.False);
            Assert.That(machine.ConfirmMainMenu(), Is.False);
            Assert.That(machine.OpenSettings(), Is.False);
            Assert.That(machine.OpenMainMenuConfirm(), Is.False);
            Assert.That(machine.Open(), Is.True);
            Assert.That(machine.Open(), Is.False);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Root));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Start));
        }
        [Test]
        public void RootBackResumesOnlyAfterPressLockAndRejectsRepeats()
        {
            var machine = Ready(); int requests = 0;
            machine.ResumeRequested += () => requests++;
            Assert.That(machine.Back(), Is.True);
            Assert.That(machine.InputLocked, Is.True);
            Assert.That(machine.Back(), Is.False);
            Assert.That(machine.OpenSettings(), Is.False);
            Assert.That(machine.ToggleResume(), Is.False);
            machine.Tick(0.079f);
            Assert.That(requests, Is.Zero);
            machine.Tick(0.002f);
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Closed));
            machine.Tick(10f);
            Assert.That(requests, Is.EqualTo(1));
        }
        [Test]
        public void SettingsBackRestoresSettingsAndMainMenuCancelRestoresItsOpener()
        {
            var machine = Ready(); Settings(machine);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Language));
            Assert.That(machine.Back(), Is.True); machine.Tick(0.08f);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Root));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Settings));
            Assert.That(machine.OpenMainMenuConfirm(), Is.True); machine.Tick(0.08f);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.ConfirmMainMenu));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.No));
            Assert.That(machine.Back(), Is.True); machine.Tick(0.08f);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Root));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Quit));
        }
        [TestCase(MenuFocusTarget.WindowMode, false)]
        [TestCase(MenuFocusTarget.Resolution, false)]
        [TestCase(MenuFocusTarget.WindowMode, true)]
        [TestCase(MenuFocusTarget.Resolution, true)]
        public void DisplayDecisionRestoresActualOpener(MenuFocusTarget opener, bool keep)
        {
            var machine = Ready(); Settings(machine);
            int resolutions = 0; bool decision = !keep;
            machine.DisplayResolved += value => { resolutions++; decision = value; };
            Assert.That(machine.OpenRevertPrompt(opener), Is.True);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.RevertPrompt));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Revert));
            Assert.That(keep ? machine.KeepDisplay() : machine.Back(), Is.True);
            machine.Tick(0.08f);
            Assert.That(resolutions, Is.EqualTo(1)); Assert.That(decision, Is.EqualTo(keep));
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Settings));
            Assert.That(machine.FocusTarget, Is.EqualTo(opener));
        }
        [Test]
        public void DeadlineWinsUnfinishedKeepAndCannotResolveTwice()
        {
            var machine = Ready(); Settings(machine);
            int resolutions = 0; bool kept = true;
            machine.DisplayResolved += value => { resolutions++; kept = value; };
            machine.OpenRevertPrompt(MenuFocusTarget.Resolution);
            machine.Tick(9.96f); Assert.That(machine.KeepDisplay(), Is.True);
            machine.Tick(0.05f); machine.Tick(1f);
            Assert.That(kept, Is.False); Assert.That(resolutions, Is.EqualTo(1));
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Resolution));
        }
        [Test]
        public void ExplicitRevertAndInvalidOpenerAreSafe()
        {
            var machine = Ready(); Settings(machine);
            Assert.That(machine.OpenRevertPrompt(MenuFocusTarget.Start), Is.False);
            bool kept = true; machine.DisplayResolved += value => kept = value;
            machine.OpenRevertPrompt(MenuFocusTarget.WindowMode);
            Assert.That(machine.RevertDisplay(), Is.True); machine.Tick(0.08f);
            Assert.That(kept, Is.False);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Settings));
        }
        [TestCase(PauseMenuState.Root)]
        [TestCase(PauseMenuState.Settings)]
        [TestCase(PauseMenuState.RevertPrompt)]
        [TestCase(PauseMenuState.ConfirmMainMenu)]
        public void StartResumesEveryOpenContextAndRevertsPreviewFirst(PauseMenuState context)
        {
            var machine = Ready(); int resolved = 0, resumed = 0;
            machine.DisplayResolved += keep => { Assert.That(keep, Is.False); resolved++; };
            machine.ResumeRequested += () => { Assert.That(resolved, Is.EqualTo(context == PauseMenuState.RevertPrompt ? 1 : 0)); resumed++; };
            if (context == PauseMenuState.Settings || context == PauseMenuState.RevertPrompt) Settings(machine);
            if (context == PauseMenuState.RevertPrompt) machine.OpenRevertPrompt(MenuFocusTarget.WindowMode);
            if (context == PauseMenuState.ConfirmMainMenu) { machine.OpenMainMenuConfirm(); machine.Tick(0.08f); }
            Assert.That(machine.ToggleResume(), Is.True);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Closed));
            Assert.That(resumed, Is.EqualTo(1));
        }
        [Test]
        public void LeavingRejectsAllInputAndPublishesOnlyOneLocalRequest()
        {
            var machine = Ready(); int leaves = 0, globalLoads = 0;
            machine.LeaveRequested += () => leaves++;
            Action<string> onStart = _ => globalLoads++;
            Action onFade = () => globalLoads++;
            MenuEvents.StartRequested += onStart; MenuEvents.FadeOutComplete += onFade;
            try
            {
                machine.OpenMainMenuConfirm(); machine.Tick(0.08f);
                Assert.That(machine.ConfirmMainMenu(), Is.True); machine.Tick(0.08f);
                Assert.That(machine.State, Is.EqualTo(PauseMenuState.Leaving));
                Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.None));
                Assert.That(machine.Back(), Is.False); Assert.That(machine.ToggleResume(), Is.False);
                Assert.That(machine.ConfirmMainMenu(), Is.False); Assert.That(machine.Open(), Is.False);
                machine.Tick(100f);
                Assert.That(leaves, Is.EqualTo(1)); Assert.That(globalLoads, Is.Zero);
            }
            finally { MenuEvents.StartRequested -= onStart; MenuEvents.FadeOutComplete -= onFade; }
        }
        [Test]
        public void FailureRestoresMainMenuFocusAndRequiresFreshConfirmation()
        {
            var machine = Ready(); int leaves = 0; machine.LeaveRequested += () => leaves++;
            machine.OpenMainMenuConfirm(); machine.Tick(0.08f); machine.ConfirmMainMenu(); machine.Tick(0.08f);
            machine.LoadFailed();
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Root));
            Assert.That(machine.HasLoadError, Is.True);
            Assert.That(machine.InputLocked, Is.False);
            Assert.That(machine.FocusTarget, Is.EqualTo(MenuFocusTarget.Quit));
            Assert.That(machine.ConfirmMainMenu(), Is.False);
            machine.Tick(10f); Assert.That(leaves, Is.EqualTo(1));
        }
        [Test]
        public void LostEligibilityDiscardsDelayedWorkRatherThanQueuingIt()
        {
            var machine = Ready(); int leaves = 0; machine.LeaveRequested += () => leaves++;
            machine.OpenMainMenuConfirm(); machine.Tick(0.08f); machine.ConfirmMainMenu();
            machine.Tick(0.08f, false); machine.Tick(100f);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Closed)); Assert.That(leaves, Is.Zero);
            machine.Open(); machine.Tick(100f);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Root)); Assert.That(leaves, Is.Zero);
        }
        [Test]
        public void ExclusiveCloseRevertsPreviewButNeverRequestsNormalResume()
        {
            var machine = Ready(); Settings(machine); int reverts = 0, resumes = 0;
            machine.DisplayResolved += keep => { Assert.That(keep, Is.False); reverts++; };
            machine.ResumeRequested += () => resumes++;
            machine.OpenRevertPrompt(MenuFocusTarget.Resolution); machine.KeepDisplay();
            machine.CancelForExclusiveFlow(); machine.CancelForExclusiveFlow(); machine.Tick(1f);
            Assert.That(reverts, Is.EqualTo(1)); Assert.That(resumes, Is.Zero);
            Assert.That(machine.State, Is.EqualTo(PauseMenuState.Closed));
        }
    }
}
