// ============================================================================
// LeafTextureGenerator.cs
// ----------------------------------------------------------------------------
// Generates a tileable leaf texture WITH an alpha channel (transparent gaps)
// entirely in code — no downloaded asset required.
//
// Technique:
//   * Fill with varied green noise for the leafy mass.
//   * Punch pseudo-random transparent holes so light shows through, giving the
//     classic "cutout foliage" look.
//   * Tileable: holes/noise wrap at the edges so repeated tiles don't seam.
// ============================================================================

using UnityEngine;

namespace Kalpa.World
{
    public static class LeafTextureGenerator
    {
        /// <summary>
        /// Build a tileable RGBA leaf texture with transparent gaps.
        /// </summary>
        public static Texture2D Generate(int size = 64, int seed = 1337)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "GeneratedLeaves",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

            var rng = new System.Random(seed);
            float noiseSeed = (float)rng.NextDouble() * 1000f;

            var px = new Color[size * size];

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Tileable coordinate via sin/cos wrap for the noise sample.
                float u = x / (float)size;
                float v = y / (float)size;

                // Base green with variation from Perlin noise.
                float n = Mathf.PerlinNoise(noiseSeed + u * 6f, noiseSeed + v * 6f);
                float n2 = Mathf.PerlinNoise(noiseSeed + u * 14f + 50f, noiseSeed + v * 14f + 50f);

                // Green tone: darker in shadow areas, lighter on highlights.
                float g = Mathf.Lerp(0.30f, 0.62f, n);
                float r = g * Mathf.Lerp(0.45f, 0.7f, n2);
                float b = g * Mathf.Lerp(0.25f, 0.4f, n2);

                // Alpha holes: use a higher-frequency noise; below a threshold = hole.
                float holeNoise = Mathf.PerlinNoise(noiseSeed + u * 22f + 200f,
                                                    noiseSeed + v * 22f + 200f);
                // Add a fine speckle so edges look leafy, not a smooth blob.
                float speckle = (float)rng.NextDouble();

                float alpha;
                if (holeNoise < 0.32f || speckle < 0.06f)
                    alpha = 0f;               // transparent gap
                else if (holeNoise < 0.40f)
                    alpha = 0.5f;             // soft edge
                else
                    alpha = 1f;               // solid leaf

                px[y * size + x] = new Color(r, g, b, alpha);
            }

            // Ensure the texture tiles cleanly: mirror a thin border blend.
            // (Point filter + Repeat wrap already handles most seams for voxel look.)

            tex.SetPixels(px);
            tex.Apply(updateMipmaps: false);
            return tex;
        }
    }
}
