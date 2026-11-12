using NUnit.Framework;
using RetroFPS.Settings;

namespace RetroFPS.Tests
{
    /// <summary>Pure display-size selection tests.</summary>
    public sealed class ResolutionOptionsTests
    {
        /// <summary>Refresh-rate duplicates collapse, small sizes drop, and sorting is stable by dimensions.</summary>
        [Test]
        public void BuildDeduplicatesFiltersAndSorts()
        {
            var input = new[] {
                new ResolutionOption(1920, 1080), new ResolutionOption(1280, 800),
                new ResolutionOption(1280, 720), new ResolutionOption(1920, 1080),
                new ResolutionOption(1024, 768), new ResolutionOption(1920, 600),
                new ResolutionOption(0, 0), new ResolutionOption(2560, 1440)
            };
            ResolutionOption[] result = ResolutionOptions.Build(input, new ResolutionOption(1920, 1080));
            Assert.That(result, Is.EqualTo(new[] { new ResolutionOption(1280, 720),
                new ResolutionOption(1280, 800), new ResolutionOption(1920, 1080),
                new ResolutionOption(2560, 1440) }));
        }

        /// <summary>The desktop supplies a usable option when there are no supported modes.</summary>
        [TestCase(2560, 1440)]
        [TestCase(1024, 600)]
        public void EmptyListFallsBackToDesktop(int width, int height)
        {
            ResolutionOption[] result = ResolutionOptions.Build(new ResolutionOption[0], new ResolutionOption(width, height));
            Assert.That(result, Is.EqualTo(new[] { new ResolutionOption(width, height) }));
        }

        /// <summary>A fully filtered list also falls back to desktop.</summary>
        [Test]
        public void FilteredListFallsBackToDesktop()
        {
            ResolutionOption[] result = ResolutionOptions.Build(new[] { new ResolutionOption(640, 480) },
                new ResolutionOption(3440, 1440));
            Assert.That(result[0], Is.EqualTo(new ResolutionOption(3440, 1440)));
        }

        /// <summary>Exact and nearest matches are deterministic, with first-entry tie breaking.</summary>
        [TestCase(1920, 1080, 1)]
        [TestCase(1800, 1000, 1)]
        [TestCase(1280, 720, 0)]
        [TestCase(1600, 900, 0)]
        public void NearestMatchesByPixelDistance(int width, int height, int expected)
        {
            var options = new[] { new ResolutionOption(1280, 720), new ResolutionOption(1920, 1080) };
            Assert.That(ResolutionOptions.FindNearestIndex(options, width, height), Is.EqualTo(expected));
        }

        /// <summary>Missing saved dimensions select the nearest desktop size; saved sizes take precedence.</summary>
        [Test]
        public void SelectionFallsBackToDesktopOnlyWhenSavedSizeIsMissing()
        {
            var options = new[] { new ResolutionOption(1280, 720), new ResolutionOption(2560, 1440) };
            var desktop = new ResolutionOption(2560, 1440);
            Assert.That(ResolutionOptions.FindSelectedIndex(options, 0, 0, desktop), Is.EqualTo(1));
            Assert.That(ResolutionOptions.FindSelectedIndex(options, 1280, 720, desktop), Is.Zero);
            Assert.That(ResolutionOptions.FindNearestIndex(new ResolutionOption[0], 1920, 1080), Is.EqualTo(-1));
        }
    }
}
