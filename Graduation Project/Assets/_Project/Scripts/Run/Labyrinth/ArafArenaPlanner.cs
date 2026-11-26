using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Arena rollerini, dalgalarını ve doğuş noktalarını tahsissiz planlar.</summary>
    public static class ArafArenaPlanner
    {
        /// <summary>Merkezi sakin tutarak her kanadın mühür ve isteğe bağlı arenalarını seçer.</summary>
        public static void AssignRoles(int[] sectionRoomOffsets, int[] sectionRoomCounts,
            bool[] sectionIsHub, bool[] isSealSlot, int optionalArenasPerWing, RunRandom rng, ArafRoomRole[] output)
        {
            if (sectionRoomOffsets == null || sectionRoomCounts == null || sectionIsHub == null ||
                isSealSlot == null || output == null || rng == null ||
                sectionRoomCounts.Length < sectionRoomOffsets.Length || sectionIsHub.Length < sectionRoomOffsets.Length)
                throw new ArgumentException("Arena rol dizileri eksik veya kısa.");
            int total = 0;
            for (int s = 0; s < sectionRoomOffsets.Length; s++)
            {
                if (sectionRoomCounts[s] < 0 || sectionRoomOffsets[s] != total || sectionRoomCounts[s] > int.MaxValue - total)
                    throw new ArgumentException("Oda aralıkları sıralı ve bitişik olmalı.");
                total += sectionRoomCounts[s];
            }
            if (output.Length != total || isSealSlot.Length < total)
                throw new ArgumentException("Arena rol tamponu toplam yuva sayısıyla eşleşmeli.");
            Array.Clear(output, 0, output.Length);
            for (int s = 0; s < sectionRoomOffsets.Length; s++)
            {
                if (sectionIsHub[s]) continue;
                int start = sectionRoomOffsets[s];
                int end = start + sectionRoomCounts[s];
                int available = 0;
                for (int i = start; i < end; i++)
                {
                    if (isSealSlot[i]) output[i] = ArafRoomRole.SealArena;
                    else available++;
                }
                int remaining = Mathf.Min(Mathf.Max(0, optionalArenasPerWing), available);
                for (int i = start; i < end && remaining > 0; i++)
                {
                    if (isSealSlot[i]) continue;
                    if (rng.Range(0, available) < remaining)
                    {
                        output[i] = ArafRoomRole.OptionalArena;
                        remaining--;
                    }
                    available--;
                }
            }
        }

        /// <summary>Başlangıç uyanışından dalgaları üretir; son dalgaya kefaretçi ve mühür eliti ekler.</summary>
        public static int BuildWaves(ArafRoomRole role, int awakening, ArafArenaWaveSettings settings, ArafArenaWave[] output)
        {
            if (settings == null || output == null) throw new ArgumentException("Dalga ayarları ve tampon gerekli.");
            if (role == ArafRoomRole.Calm) return 0;
            int count = Mathf.Min(output.Length, role == ArafRoomRole.SealArena ? settings.SealWaves(awakening) : settings.OptionalWaves(awakening));
            for (int i = 0; i < count; i++)
            {
                bool final = i == count - 1;
                bool elite = final && role == ArafRoomRole.SealArena;
                output[i] = new ArafArenaWave(settings.Ghouls(awakening), settings.Eyes(awakening),
                    final ? settings.FinalWavePenitents(awakening) + (elite ? settings.SealFinalPenitents : 0) : 0, elite);
            }
            return count;
        }

        /// <summary>Son dalga dışında canlı eşiğinde ilerlemeye izin verir.</summary>
        public static bool ShouldAdvance(int aliveInWave, int waveIndex, int waveCount, int advanceWhenAliveAtMost)
            => waveIndex >= 0 && waveIndex < waveCount - 1 && aliveInWave <= advanceWhenAliveAtMost;
        /// <summary>Son dalga doğduktan sonra bütün dalgaların ölmesini bekler.</summary>
        public static bool IsCleared(int aliveTotal, int waveIndex, int waveCount)
            => waveCount > 0 && waveIndex == waveCount - 1 && aliveTotal == 0;

        /// <summary>Uzak noktaları deterministik karıştırır, yetmezse döngüsel kullanır; hiçbiri uygun değilse en uzakları seçer.</summary>
        public static int PickSpawnPoints(Vector3[] candidates, int candidateCount, Vector3 player,
            float minDistance, int needed, RunRandom rng, int[] output)
        {
            if (candidates == null || output == null || rng == null) return 0;
            int count = Mathf.Clamp(candidateCount, 0, candidates.Length);
            int written = Mathf.Clamp(needed, 0, output.Length);
            if (count == 0 || written == 0) return 0;
            float minimum = Mathf.Max(0f, minDistance);
            float limit = minimum * minimum;
            float farthest = -1f;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                float distance = HorizontalSquared(candidates[i], player);
                farthest = Mathf.Max(farthest, distance);
                if (distance >= limit) any = true;
            }
            if (!any) limit = farthest;
            // Reservoir sampling uses the caller's output as scratch, including when it is shorter than candidates.
            int eligible = 0;
            for (int i = 0; i < count; i++)
            {
                if (HorizontalSquared(candidates[i], player) < limit) continue;
                eligible++;
                if (eligible <= written) output[eligible - 1] = i;
                else
                {
                    int replace = rng.Range(0, eligible);
                    if (replace < written) output[replace] = i;
                }
            }
            int unique = Mathf.Min(eligible, written);
            rng.Shuffle(output, unique);
            for (int i = unique; i < written; i++) output[i] = output[i % unique];
            return written;
        }

        private static float HorizontalSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
