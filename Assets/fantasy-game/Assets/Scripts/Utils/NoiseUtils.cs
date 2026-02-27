using UnityEngine;

namespace FantasyGame.Utils
{
    public static class NoiseUtils
    {
        public const int DEFAULT_OCTAVES = 4;
        public const float DEFAULT_FREQUENCY = 0.02f;
        public const float DEFAULT_LACUNARITY = 2.0f;
        public const float DEFAULT_PERSISTENCE = 0.5f;
        public const float DEFAULT_AMPLITUDE = 30f;

        /// <summary>
        /// Multi-octave Perlin noise. Returns a height value.
        /// Uses Mathf.PerlinNoise which is WebGL-safe (no native plugins).
        /// </summary>
        public static float SampleHeight(float worldX, float worldZ, int seed,
            int octaves = DEFAULT_OCTAVES,
            float baseFrequency = DEFAULT_FREQUENCY,
            float lacunarity = DEFAULT_LACUNARITY,
            float persistence = DEFAULT_PERSISTENCE,
            float amplitude = DEFAULT_AMPLITUDE)
        {
            float height = 0f;
            float freq = baseFrequency;
            float amp = amplitude;
            float maxPossible = 0f;

            // Seed offset so different seeds produce different terrain
            float seedOffsetX = seed * 17.3f;
            float seedOffsetZ = seed * 31.7f;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = (worldX + seedOffsetX) * freq;
                float sampleZ = (worldZ + seedOffsetZ) * freq;

                // Mathf.PerlinNoise returns 0-1, remap to -1 to 1
                float noiseVal = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                height += noiseVal * amp;

                maxPossible += amp;
                freq *= lacunarity;
                amp *= persistence;
            }

            // Normalize and remap so terrain is mostly above zero
            // Shift upward so valleys are shallow and hills are tall
            height = (height / maxPossible) * amplitude;
            height += amplitude * 0.3f; // Raise base level

            return height;
        }

        /// <summary>
        /// Computes the terrain normal at a point using central differences.
        /// More accurate than mesh RecalculateNormals for terrain.
        /// </summary>
        public static Vector3 SampleNormal(float worldX, float worldZ, int seed, float sampleStep = 0.5f)
        {
            float hL = SampleHeight(worldX - sampleStep, worldZ, seed);
            float hR = SampleHeight(worldX + sampleStep, worldZ, seed);
            float hD = SampleHeight(worldX, worldZ - sampleStep, seed);
            float hU = SampleHeight(worldX, worldZ + sampleStep, seed);

            Vector3 normal = new Vector3(hL - hR, 2f * sampleStep, hD - hU).normalized;
            return normal;
        }

        /// <summary>
        /// Returns slope as dot product with up vector (1.0 = flat, 0.0 = vertical wall).
        /// </summary>
        public static float GetSlope(float worldX, float worldZ, int seed)
        {
            Vector3 normal = SampleNormal(worldX, worldZ, seed);
            return normal.y;
        }
    }
}
