using NUnit.Framework;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    public sealed class HoldAcceleratorTests
    {
        [Test]
        public void SustainedRepeatAcceleratesAndCaps()
        {
            var hold = new HoldAccelerator();
            Assert.That(hold.Repeat(0f), Is.EqualTo(1));
            Assert.That(hold.Repeat(0.4f), Is.EqualTo(1));
            for (int i = 0; i < 30; i++) hold.Repeat(0.1f);
            Assert.That(hold.Repeat(0.1f), Is.EqualTo(4));
        }

        [Test]
        public void GapReleaseAndDirectionChangeResetAcceleration()
        {
            var hold = new HoldAccelerator();
            hold.Repeat(0f);
            hold.Repeat(0.4f);
            Assert.That(hold.Repeat(0.4f), Is.GreaterThan(1));
            Assert.That(hold.Repeat(0.6f), Is.EqualTo(1));
            hold.Repeat(0.4f);
            hold.Repeat(0.4f);
            Assert.That(hold.Repeat(0.1f, -1), Is.EqualTo(1));
            hold.Reset();
            Assert.That(hold.Repeat(0.1f), Is.EqualTo(1));
            Assert.That(hold.Repeat(0f, 0), Is.Zero);
        }
    }
}
