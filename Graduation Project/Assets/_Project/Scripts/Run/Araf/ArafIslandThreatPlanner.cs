using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Deterministically distributes island threats without allocating temporary storage.</summary>
    public static class ArafIslandThreatPlanner
    {
        /// <summary>
        /// Writes chain/platform pairs round-robin, avoiding nearby platforms and chain endpoints.
        /// Exhausts distinct eligible platforms before permitting repeats; caller buffers bound the result.
        /// </summary>
        public static int PickPlatforms(ArafWorldLayout world, int count, Vector3 avoidLocal,
            float minDistance, RunRandom rng, int[] chainResults, int[] platformResults)
        {
            if (world == null || rng == null || chainResults == null || platformResults == null
                || world.ChainCount == 0 || count <= 0) return 0;
            int limit = Mathf.Min(count, Mathf.Min(chainResults.Length, platformResults.Length));
            if (limit == 0) return 0;
            float distance = Mathf.Max(0f, minDistance);
            float minSquared = distance * distance;
            int nextChain = rng.Range(0, world.ChainCount);
            int written = 0;
            while (written < limit)
            {
                int selectedChain = -1;
                int selectedPlatform = -1;
                for (int pass = 0; pass < 2 && selectedChain < 0; pass++)
                for (int offset = 0; offset < world.ChainCount; offset++)
                {
                    int c = (nextChain + offset) % world.ChainCount;
                    ArafIslandChain chain = world.GetChain(c);
                    int first = chain.PlatformCount >= 3 ? 1 : 0;
                    int end = chain.PlatformCount >= 3 ? chain.PlatformCount - 1 : chain.PlatformCount;
                    int candidates = 0;
                    int picked = -1;
                    for (int p = first; p < end; p++)
                    {
                        if ((chain.GetPlatform(p).Center - avoidLocal).sqrMagnitude < minSquared) continue;
                        if (pass == 0 && WasUsed(c, p, written, chainResults, platformResults)) continue;
                        if (rng.Range(0, ++candidates) == 0) picked = p;
                    }
                    if (picked < 0) continue;
                    selectedChain = c;
                    selectedPlatform = picked;
                    break;
                }
                if (selectedChain < 0) break;
                chainResults[written] = selectedChain;
                platformResults[written++] = selectedPlatform;
                nextChain = (selectedChain + 1) % world.ChainCount;
            }
            return written;
        }

        private static bool WasUsed(int chain, int platform, int count, int[] chains, int[] platforms)
        {
            for (int i = 0; i < count; i++)
                if (chains[i] == chain && platforms[i] == platform) return true;
            return false;
        }
    }
}
