// ============================================================================
// OreTextureGenerator.cs
// ----------------------------------------------------------------------------
// Procedurally generates ore block textures: a stone-grey base with coloured
// mineral speckles. No downloads. Keyed by internal name, filled in by the atlas.
//
// Blocks:
//   kalpa:coal_ore   — grey stone with black speckles
//   kalpa:iron_ore   — grey stone with tan/orange speckles
//   kalpa:gold_ore   — grey stone with yellow speckles
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Kalpa.World
{
    public static class OreTextureGenerator
    {
        public const int Size = 64;

        public static Texture2D TryGenerate(string internalName, int seedSalt)
        {
            switch (internalName)
            {
                case "kalpa:coal_ore": return Ore(seedSalt, new Color(0.09f, 0.09f, 0.10f));
                case "kalpa:iron_ore": return Ore(seedSalt, new Color(0.80f, 0.55f, 0.38f));
                case "kalpa:gold_ore": return Ore(seedSalt, new Color(0.95f, 0.80f, 0.20f));
                default:               return null;
            }
        }

        private static Texture2D Ore(int salt, Color mineral)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Ore",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

            var rng = new System.Random(salt * 131 + 17);
            float off = salt * 7.3f;
            var px = new Color[Size * Size];

            // Stone-grey base.
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Mathf.PerlinNoise(off + x * 0.15f, off + y * 0.15f);
                float g = Mathf.Lerp(0.42f, 0.56f, n);
                px[y * Size + x] = new Color(g, g, g, 1f);
            }

            // Mineral speckle blobs.
            int blobs = 10 + rng.Next(6);
            for (int b = 0; b < blobs; b++)
            {
                int cx = rng.Next(Size);
                int cy = rng.Next(Size);
                int radius = 2 + rng.Next(3);
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int x = ((cx + dx) % Size + Size) % Size; // wrap for tileability
                    int y = ((cy + dy) % Size + Size) % Size;
                    // Slight shade variation on the mineral.
                    float v = 0.85f + (float)rng.NextDouble() * 0.3f;
                    px[y * Size + x] = new Color(mineral.r * v, mineral.g * v, mineral.b * v, 1f);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static readonly IReadOnlyList<string> SupportedNames = new[]
        {
            "kalpa:coal_ore", "kalpa:iron_ore", "kalpa:gold_ore",
        };
    }
}
