using System;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Generates deterministic mono placeholder audio without scene or clock state.</summary>
    public static class ArafToneSynth
    {
        /// <summary>Sample rate used by both synthesized sounds.</summary>
        public const int SampleRate = 44100;
        /// <summary>Duration of the looping seal beacon.</summary>
        public const float BeaconSeconds = 4f;
        /// <summary>Duration of the awakening sting.</summary>
        public const float StingSeconds = 2.5f;

        /// <summary>Rounds a duration to the nearest sample at the synthesis rate.</summary>
        public static int SampleCount(float seconds) => Mathf.RoundToInt(seconds * SampleRate);

        /// <summary>Fills an exact four-second buffer with a looping drone and soft bell.</summary>
        /// <exception cref="ArgumentException">The buffer length does not match the beacon duration.</exception>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        public static void FillBeacon(float[] buffer)
        {
            ValidateBuffer(buffer, BeaconSeconds);
            double tau = 2.0 * Math.PI;
            for (int i = 0; i < buffer.Length; i++)
            {
                double t = (double)i / SampleRate;
                double pulse = 0.6 + 0.4 * (0.5 - 0.5 * Math.Cos(tau * t / 2.0));
                double drone = 0.5 * Math.Sin(tau * 55.0 * t) + 0.3 * Math.Sin(tau * 82.5 * t);
                double strikeTime = t % 2.0;
                double bellEnvelope = Math.Exp(-strikeTime * 3.5) * Math.Min(1.0, strikeTime / 0.004);
                double bell = 0.25 * Math.Sin(tau * 660.0 * strikeTime)
                    + 0.12 * Math.Sin(tau * 990.0 * strikeTime);
                buffer[i] = (float)(drone * pulse + bell * bellEnvelope);
            }
            Normalize(buffer, 0.6f);
        }

        /// <summary>Fills an exact 2.5-second buffer with a descending boom, filtered noise and swell.</summary>
        /// <exception cref="ArgumentException">The buffer length does not match the sting duration.</exception>
        /// <exception cref="ArgumentNullException">The buffer is null.</exception>
        public static void FillSting(float[] buffer)
        {
            ValidateBuffer(buffer, StingSeconds);
            double tau = 2.0 * Math.PI;
            double phase = 0.0;
            double filteredNoise = 0.0;
            uint noiseState = 0x2F6E2B1u;
            for (int i = 0; i < buffer.Length; i++)
            {
                double t = (double)i / SampleRate;
                double frequency = 48.0 * Math.Pow(28.0 / 48.0, Math.Min(1.0, t / 1.2));
                double boom = Math.Sin(phase) * Math.Exp(-t * 2.2);
                phase += tau * frequency / SampleRate;

                noiseState ^= noiseState << 13;
                noiseState ^= noiseState >> 17;
                noiseState ^= noiseState << 5;
                double noise = noiseState / (double)uint.MaxValue * 2.0 - 1.0;
                filteredNoise += 0.08 * (noise - filteredNoise);
                double burst = filteredNoise * Math.Exp(-t * 9.0) * 0.5;

                double swellEnvelope = Math.Sin(Math.PI * Mathf.Clamp01((float)(t / StingSeconds)));
                swellEnvelope *= swellEnvelope;
                double swell = (Math.Sin(tau * 220.0 * t) + Math.Sin(tau * 233.08 * t)) * 0.18 * swellEnvelope;
                double fadeIn = Math.Min(1.0, t / 0.005);
                double fadeOut = Math.Min(1.0, (StingSeconds - t) / 0.1);
                buffer[i] = (float)((boom + burst + swell) * fadeIn * fadeOut);
            }
            Normalize(buffer, 0.9f);
        }

        /// <summary>Allocates a mono little-endian PCM16 RIFF/WAVE file for editor/build-time use only.</summary>
        /// <param name="samples">Samples clamped to [-1, 1], scaled by 32767 and rounded.</param>
        /// <param name="sampleRate">Positive sample rate to write into the header.</param>
        /// <returns>The complete 44-byte header followed by signed 16-bit samples.</returns>
        /// <exception cref="ArgumentNullException">Samples are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The sample rate is not positive.</exception>
        public static byte[] EncodeWav16(float[] samples, int sampleRate)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            int dataSize = checked(samples.Length * 2);
            byte[] wav = new byte[checked(44 + dataSize)];
            WriteTag(wav, 0, "RIFF");
            WriteInt32(wav, 4, 36 + dataSize);
            WriteTag(wav, 8, "WAVE");
            WriteTag(wav, 12, "fmt ");
            WriteInt32(wav, 16, 16);
            WriteInt16(wav, 20, 1);
            WriteInt16(wav, 22, 1);
            WriteInt32(wav, 24, sampleRate);
            WriteInt32(wav, 28, unchecked(sampleRate * 2));
            WriteInt16(wav, 32, 2);
            WriteInt16(wav, 34, 16);
            WriteTag(wav, 36, "data");
            WriteInt32(wav, 40, dataSize);
            for (int i = 0; i < samples.Length; i++)
                WriteInt16(wav, 44 + i * 2, Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
            return wav;
        }

        private static void ValidateBuffer(float[] buffer, float seconds)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length != SampleCount(seconds))
                throw new ArgumentException("Buffer length must match the sound duration.", nameof(buffer));
        }

        private static void Normalize(float[] buffer, float targetPeak)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(buffer[i]));
            if (peak <= 0f) return;
            float gain = targetPeak / peak;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= gain;
        }

        private static void WriteTag(byte[] bytes, int offset, string tag)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)tag[i];
        }

        private static void WriteInt16(byte[] bytes, int offset, int value)
        {
            bytes[offset] = unchecked((byte)value);
            bytes[offset + 1] = unchecked((byte)(value >> 8));
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            WriteInt16(bytes, offset, value);
            WriteInt16(bytes, offset + 2, value >> 16);
        }
    }
}
