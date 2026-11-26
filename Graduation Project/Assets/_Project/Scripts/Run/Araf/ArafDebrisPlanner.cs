using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Plans deterministic cosmetic debris in a rounded horizontal shell.</summary>
    public static class ArafDebrisPlanner
    {
        private const int PlacementAttempts = 32;
        private const float MinimumSpacingSquared = 64f;

        /// <summary>
        /// Writes up to the smallest buffer capacity without allocating. Negative counts produce zero.
        /// Negative margins/thicknesses use zero. After 32 attempts the last candidate is accepted,
        /// so crowded shells can contain pieces closer than eight metres. Null inputs produce zero.
        /// </summary>
        public static int Plan(Bounds worldBoundsLocal, int count, float margin, float shellThickness,
            RunRandom rng, Vector3[] positions, Quaternion[] rotations, float[] scales)
        {
            if (rng == null || positions == null || rotations == null || scales == null) return 0;
            count = Mathf.Min(Mathf.Max(0, count), Mathf.Min(positions.Length, Mathf.Min(rotations.Length, scales.Length)));
            margin = Mathf.Max(0f, margin);
            shellThickness = Mathf.Max(0f, shellThickness);
            Vector3 min = worldBoundsLocal.min;
            Vector3 max = worldBoundsLocal.max;
            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = default;
                for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                {
                    candidate = SampleShell(min, max, rng.Range(margin, margin + shellThickness), rng);
                    bool spaced = true;
                    for (int j = 0; j < i; j++)
                    {
                        if ((candidate - positions[j]).sqrMagnitude >= MinimumSpacingSquared) continue;
                        spaced = false;
                        break;
                    }
                    if (spaced) break;
                }
                positions[i] = candidate;
                rotations[i] = Quaternion.Euler(rng.Range(0f, 360f), rng.Range(0f, 360f), rng.Range(0f, 360f));
                scales[i] = rng.Range(0.6f, 2.2f);
            }
            return count;
        }

        private static Vector3 SampleShell(Vector3 min, Vector3 max, float distance, RunRandom rng)
        {
            float width = max.x - min.x;
            float depth = max.z - min.z;
            float straightLength = 2f * (width + depth);
            float along = rng.Range(0f, straightLength + 2f * Mathf.PI * distance);
            float y = rng.Range(min.y - 30f, max.y + 40f);
            if (along < width) return new Vector3(min.x + along, y, min.z - distance);
            along -= width;
            if (along < width) return new Vector3(min.x + along, y, max.z + distance);
            along -= width;
            if (along < depth) return new Vector3(min.x - distance, y, min.z + along);
            along -= depth;
            if (along < depth) return new Vector3(max.x + distance, y, min.z + along);
            along -= depth;
            // The remaining perimeter consists of four quarter-circle corners.
            float angle = distance > 0f ? along / distance : 0f;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            return new Vector3((x >= 0f ? max.x : min.x) + x * distance, y,
                (z >= 0f ? max.z : min.z) + z * distance);
        }
    }
}
