using System;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>
    /// Labirent oda yuvaları arasından birbirine en uzak mühür yuvalarını seçen saf C# yardımcı sınıf.
    /// </summary>
    public static class ArafSealSlotPicker
    {
        /// <summary>
        /// En uzak nokta (farthest-point) yöntemiyle en fazla <paramref name="count"/> adet oda yuvası indeksi seçer.
        /// İlk yuva en yüksek derinliğe sahip olandır; sonrakiler seçili yuvalara en yakın uzaklığı enbüyükler.
        /// Eşitlikler daha yüksek derinlik ve ardından <paramref name="rng"/> ile bozulur.
        /// </summary>
        public static int Pick(LabyrinthLayout layout, int count, RunRandom rng, int[] results)
        {
            if (layout == null || results == null || count <= 0 || results.Length <= 0 || layout.RoomCount <= 0)
            {
                return 0;
            }

            int roomCount = layout.RoomCount;
            int targetCount = Math.Min(count, Math.Min(roomCount, results.Length));
            RunRandom random = rng ?? new RunRandom(1UL);

            int firstIndex = 0;
            int bestDepth = layout.GetRoom(0).Depth;
            int tieCount = 1;
            for (int i = 1; i < roomCount; i++)
            {
                int depth = layout.GetRoom(i).Depth;
                if (depth > bestDepth)
                {
                    firstIndex = i;
                    bestDepth = depth;
                    tieCount = 1;
                }
                else if (depth == bestDepth)
                {
                    tieCount++;
                    if (random.Range(0, tieCount) == 0)
                    {
                        firstIndex = i;
                    }
                }
            }

            results[0] = firstIndex;
            int picked = 1;

            while (picked < targetCount)
            {
                int bestIndex = -1;
                float bestMinDist = -1f;
                int bestCandidateDepth = int.MinValue;
                tieCount = 0;

                for (int i = 0; i < roomCount; i++)
                {
                    if (Contains(results, picked, i)) continue;

                    Vector3 candidateCenter = layout.GetRoom(i).LocalCenter;
                    float minDistSq = float.MaxValue;
                    for (int k = 0; k < picked; k++)
                    {
                        Vector3 delta = candidateCenter - layout.GetRoom(results[k]).LocalCenter;
                        float distSq = delta.sqrMagnitude;
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                        }
                    }

                    float minDist = Mathf.Sqrt(minDistSq);
                    int depth = layout.GetRoom(i).Depth;
                    float eps = 1e-4f * Math.Max(1f, bestMinDist);

                    if (bestIndex < 0 || minDist > bestMinDist + eps)
                    {
                        bestIndex = i;
                        bestMinDist = minDist;
                        bestCandidateDepth = depth;
                        tieCount = 1;
                    }
                    else if (Mathf.Abs(minDist - bestMinDist) <= eps)
                    {
                        if (depth > bestCandidateDepth)
                        {
                            bestIndex = i;
                            bestMinDist = minDist;
                            bestCandidateDepth = depth;
                            tieCount = 1;
                        }
                        else if (depth == bestCandidateDepth)
                        {
                            tieCount++;
                            if (random.Range(0, tieCount) == 0)
                            {
                                bestIndex = i;
                            }
                        }
                    }
                }

                if (bestIndex < 0) break;
                results[picked++] = bestIndex;
            }

            return picked;
        }

        private static bool Contains(int[] buffer, int length, int value)
        {
            for (int i = 0; i < length; i++)
            {
                if (buffer[i] == value) return true;
            }
            return false;
        }
    }
}
