using NUnit.Framework;
using RetroFPS.UI.Menu;

namespace RetroFPS.Tests
{
    public sealed class MenuTweenTests
    {
        [TestCase(MenuEase.Linear)]
        [TestCase(MenuEase.EaseIn)]
        [TestCase(MenuEase.EaseOut)]
        [TestCase(MenuEase.EaseInOut)]
        public void EasingHasClampedEndpointsAndMonotonicProgress(MenuEase ease)
        {
            Assert.That(MenuTween.Evaluate(-1f, ease), Is.Zero);
            Assert.That(MenuTween.Evaluate(2f, ease), Is.EqualTo(1f));
            float previous = 0f;
            for (int i = 0; i <= 20; i++)
            {
                float value = MenuTween.Evaluate(i / 20f, ease);
                Assert.That(value, Is.GreaterThanOrEqualTo(previous));
                previous = value;
            }
            var tween = new MenuTween();
            tween.Start(0.6f, ease);
            Assert.That(tween.Value, Is.Zero);
            tween.Tick(0.6f);
            Assert.That(tween.Complete, Is.True);
            Assert.That(tween.Value, Is.EqualTo(1f));
        }

        [Test]
        public void CurvesHaveDistinctMidpoints()
        {
            Assert.That(MenuTween.Evaluate(0.5f, MenuEase.EaseIn), Is.EqualTo(0.25f));
            Assert.That(MenuTween.Evaluate(0.5f, MenuEase.EaseOut), Is.EqualTo(0.75f));
            Assert.That(MenuTween.Evaluate(0.5f, MenuEase.EaseInOut), Is.EqualTo(0.5f));
        }

        [Test]
        public void ReducedMotionClampsFadesAndSkipsOtherMotion()
        {
            Assert.That(MenuTween.ResolveDuration(0.6f, false, true), Is.EqualTo(0.6f));
            Assert.That(MenuTween.ResolveDuration(0.6f, true, true, 1f), Is.EqualTo(0.1f));
            Assert.That(MenuTween.ResolveDuration(0.2f, true, false), Is.Zero);
            Assert.That(MenuTween.ResolveDuration(0.6f, true, true, 0f), Is.Zero);
            var tween = new MenuTween();
            tween.Start(0f);
            Assert.That(tween.Complete, Is.True);
            Assert.That(tween.Value, Is.EqualTo(1f));
            tween.Start(1f);
            tween.Tick(-1f);
            Assert.That(tween.Value, Is.Zero);
        }
    }
}
