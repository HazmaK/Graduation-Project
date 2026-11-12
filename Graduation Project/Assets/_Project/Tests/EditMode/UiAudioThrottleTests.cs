using NUnit.Framework;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    public sealed class UiAudioThrottleTests
    {
        [Test]
        public void SameEventIsSuppressedUntilFiftyMilliseconds()
        {
            var throttle = new UiAudioThrottle();
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.True);
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.False);
            throttle.Tick(0.049f);
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.False);
            throttle.Tick(0.002f);
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.True);
        }

        [Test]
        public void EventsHaveIndependentCooldownsAndNegativeTimeDoesNotAdvance()
        {
            var throttle = new UiAudioThrottle();
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.True);
            Assert.That(throttle.TryPlay(UiAudioEventType.Confirm), Is.True);
            throttle.Tick(-1f);
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.False);
            throttle.Tick(0.05f);
            Assert.That(throttle.TryPlay(UiAudioEventType.Focus), Is.True);
            Assert.That(throttle.TryPlay(UiAudioEventType.Confirm), Is.True);
        }
    }
}
