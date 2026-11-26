using System;
using System.Text;
using NUnit.Framework;
using RetroFPS.Run.Araf;
using UnityEngine;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Verifies deterministic synthesis, signal boundaries and PCM16 encoding.</summary>
    public sealed class ArafToneSynthTests
    {
        /// <summary>Durations are rounded to sample counts at 44.1 kHz.</summary>
        [TestCase(0f, 0)]
        [TestCase(1f, 44100)]
        [TestCase(4f, 176400)]
        [TestCase(2.5f, 110250)]
        [TestCase(0.00004f, 2)]
        public void SampleCountRoundsDuration(float seconds, int expected)
        {
            Assert.That(ArafToneSynth.SampleCount(seconds), Is.EqualTo(expected));
        }

        /// <summary>Both fills reject undersized, oversized and missing buffers.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void FillRejectsWrongBufferLength(bool beacon)
        {
            Action<float[]> fill = beacon ? (Action<float[]>)ArafToneSynth.FillBeacon : ArafToneSynth.FillSting;
            int count = ArafToneSynth.SampleCount(beacon ? ArafToneSynth.BeaconSeconds : ArafToneSynth.StingSeconds);
            Assert.Throws<ArgumentException>(() => fill(new float[0]));
            Assert.Throws<ArgumentException>(() => fill(new float[count - 1]));
            Assert.Throws<ArgumentException>(() => fill(new float[count + 1]));
            Assert.Throws<ArgumentNullException>(() => fill(null));
        }

        /// <summary>The beacon is finite, normalized and continuous across its loop boundary.</summary>
        [Test]
        public void BeaconHasExpectedPeakAndLoopSeam()
        {
            float[] samples = new float[ArafToneSynth.SampleCount(ArafToneSynth.BeaconSeconds)];
            ArafToneSynth.FillBeacon(samples);
            Assert.That(FinitePeak(samples), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(Mathf.Abs(samples[samples.Length - 1] - samples[0]), Is.LessThan(0.05f));
        }

        /// <summary>The sting reaches its target peak with silent attack and a faded tail.</summary>
        [Test]
        public void StingHasExpectedPeakAndFades()
        {
            float[] samples = new float[ArafToneSynth.SampleCount(ArafToneSynth.StingSeconds)];
            ArafToneSynth.FillSting(samples);
            Assert.That(FinitePeak(samples), Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(samples[0], Is.Zero);
            Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.01f));
        }

        /// <summary>Each call starts from identical oscillator and noise state and overwrites its buffer.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void FillsAreDeterministic(bool beacon)
        {
            int count = ArafToneSynth.SampleCount(beacon ? ArafToneSynth.BeaconSeconds : ArafToneSynth.StingSeconds);
            float[] first = new float[count];
            float[] second = new float[count];
            for (int i = 0; i < count; i++) second[i] = float.NaN;
            if (beacon)
            {
                ArafToneSynth.FillBeacon(first);
                ArafToneSynth.FillBeacon(second);
            }
            else
            {
                ArafToneSynth.FillSting(first);
                ArafToneSynth.FillSting(second);
            }
            Assert.That(second, Is.EqualTo(first));
        }

        /// <summary>The WAV header contains correctly sized little-endian mono PCM16 chunks.</summary>
        [TestCase(0, 44100)]
        [TestCase(3, 22050)]
        public void WavHeaderMatchesPcmContract(int count, int sampleRate)
        {
            byte[] wav = ArafToneSynth.EncodeWav16(new float[count], sampleRate);
            Assert.That(wav.Length, Is.EqualTo(44 + 2 * count));
            Assert.That(Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(ReadInt32(wav, 4), Is.EqualTo(wav.Length - 8));
            Assert.That(Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(Encoding.ASCII.GetString(wav, 12, 4), Is.EqualTo("fmt "));
            Assert.That(ReadInt32(wav, 16), Is.EqualTo(16));
            Assert.That(ReadInt16(wav, 20), Is.EqualTo(1));
            Assert.That(ReadInt16(wav, 22), Is.EqualTo(1));
            Assert.That(ReadInt32(wav, 24), Is.EqualTo(sampleRate));
            Assert.That(ReadInt32(wav, 28), Is.EqualTo(sampleRate * 2));
            Assert.That(ReadInt16(wav, 32), Is.EqualTo(2));
            Assert.That(ReadInt16(wav, 34), Is.EqualTo(16));
            Assert.That(Encoding.ASCII.GetString(wav, 36, 4), Is.EqualTo("data"));
            Assert.That(ReadInt32(wav, 40), Is.EqualTo(count * 2));
        }

        /// <summary>Out-of-range samples clamp symmetrically and fractional values round.</summary>
        [Test]
        public void WavClampsAndRoundsSamples()
        {
            byte[] wav = ArafToneSynth.EncodeWav16(new[] { 2f, -2f, 0f, 0.25f, -0.25f }, 44100);
            Assert.That(ReadInt16(wav, 44), Is.EqualTo(32767));
            Assert.That(ReadInt16(wav, 46), Is.EqualTo(-32767));
            Assert.That(ReadInt16(wav, 48), Is.Zero);
            Assert.That(ReadInt16(wav, 50), Is.EqualTo(8192));
            Assert.That(ReadInt16(wav, 52), Is.EqualTo(-8192));
        }

        /// <summary>Missing samples and nonpositive sample rates fail explicitly.</summary>
        [Test]
        public void WavRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ArafToneSynth.EncodeWav16(null, 44100));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArafToneSynth.EncodeWav16(new float[0], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArafToneSynth.EncodeWav16(new float[0], -1));
        }

        private static float FinitePeak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                Assert.That(float.IsNaN(samples[i]) || float.IsInfinity(samples[i]), Is.False);
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            return peak;
        }

        private static short ReadInt16(byte[] bytes, int offset)
        {
            return unchecked((short)(bytes[offset] | bytes[offset + 1] << 8));
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
        }
    }
}
