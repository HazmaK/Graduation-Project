using NUnit.Framework;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    public sealed class RevertCountdownTests
    {
        [Test]
        public void DefaultDeadlineExpiresAtTenSeconds()
        {
            var timer = new RevertCountdown();
            timer.Start();
            Assert.That(timer.WholeSecondsRemaining, Is.EqualTo(10));
            timer.Tick(0.5f);
            Assert.That(timer.WholeSecondsRemaining, Is.EqualTo(10));
            timer.Tick(0.5f);
            Assert.That(timer.WholeSecondsRemaining, Is.EqualTo(9));
            timer.Tick(6f);
            Assert.That(timer.Danger, Is.True);
            Assert.That(timer.WholeSecondsRemaining, Is.EqualTo(3));
            timer.Tick(3f);
            Assert.That(timer.Expired, Is.True);
            Assert.That(timer.WholeSecondsRemaining, Is.Zero);
            Assert.That(timer.Running, Is.False);
            timer.Tick(100f);
            Assert.That(timer.Remaining, Is.Zero);
        }

        [Test]
        public void KeepStopsCountdownAndRestartClearsExpiry()
        {
            var timer = new RevertCountdown();
            timer.Start();
            timer.Tick(1f);
            timer.Stop();
            timer.Tick(20f);
            Assert.That(timer.Expired, Is.False);
            Assert.That(timer.Remaining, Is.EqualTo(9f));
            timer.Start(0f);
            Assert.That(timer.Expired, Is.True);
            timer.Start();
            Assert.That(timer.Expired, Is.False);
            timer.Tick(-2f);
            Assert.That(timer.Remaining, Is.EqualTo(10f));
        }
    }
}
