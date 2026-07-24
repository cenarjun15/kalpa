// ============================================================================
// CulturalTextureGenerator.cs
// ----------------------------------------------------------------------------
// Procedurally generates tileable textures for India-themed blocks — no asset
// downloads required. Each generator is keyed by the block's internal name, so
// BlockData assets need NO texture assigned; the atlas fills them in at boot.
//
// Blocks provided:
//   kalpa:temple_stone      — weathered grey temple granite with carved lines
//   kalpa:carved_sandstone  — warm sandstone with horizontal carving bands
//   kalpa:terracotta        — reddish-brown clay with subtle mottling
//   kalpa:thatch            — dry straw roof texture (directional strands)
//   kalpa:mud_wall          — earthy mud/adobe with straw fleck
//   kalpa:patterned_marble  — white marble with a faint jaali-style diamond grid
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Kalpa.World
{
    public static class CulturalTextureGenerator
    {
        public const int Size = 64;

        /// <summary>
        /// Returns a generated texture for the given internal block name, or null
        /// if we don't have a generator for it (atlas then falls back to debug colour).
        /// </summary>
        public static Texture2D TryGenerate(string internalName, int seedSalt)
        {
            switch (internalName)
            {
                case "kalpa:temple_stone":     return TempleStone(seedSalt);
                case "kalpa:carved_sandstone": return CarvedSandstone(seedSalt);
                case "kalpa:terracotta":       return Terracotta(seedSalt);
                case "kalpa:thatch":           return Thatch(seedSalt);
                case "kalpa:mud_wall":         return MudWall(seedSalt);
                case "kalpa:patterned_marble": return PatternedMarble(seedSalt);
                default:                       return null;
            }
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private static Texture2D NewTex(string name)
            => new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

        private static float Noise(float x, float y, float freq, float off)
            => Mathf.PerlinNoise(off + x * freq, off + y * freq);

        // --------------------------------------------------------------------
        // Generators
        // --------------------------------------------------------------------

        private static Texture2D TempleStone(int salt)
        {
            var tex = NewTex("TempleStone");
            var px = new Color[Size * Size];
            float off = salt * 13.7f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Noise(x, y, 0.12f, off) * 0.5f + Noise(x, y, 0.3f, off + 40) * 0.5f;
                float g = Mathf.Lerp(0.42f, 0.58f, n);
                // Carved horizontal grooves every 16px.
                if (y % 16 == 0 || x % 16 == 0) g *= 0.7f;
                px[y * Size + x] = new Color(g, g, g * 0.98f, 1f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }

        private static Texture2D CarvedSandstone(int salt)
        {
            var tex = NewTex("CarvedSandstone");
            var px = new Color[Size * Size];
            float off = salt * 9.1f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Noise(x, y, 0.15f, off);
                float band = Mathf.Sin(y * 0.4f) * 0.06f;
                float r = Mathf.Lerp(0.76f, 0.88f, n) + band;
                float g = Mathf.Lerp(0.60f, 0.70f, n) + band;
                float b = Mathf.Lerp(0.38f, 0.46f, n);
                if (y % 8 == 0) { r *= 0.85f; g *= 0.85f; b *= 0.85f; } // carving line
                px[y * Size + x] = new Color(r, g, b, 1f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }

        private static Texture2D Terracotta(int salt)
        {
            var tex = NewTex("Terracotta");
            var px = new Color[Size * Size];
            float off = salt * 5.3f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Noise(x, y, 0.2f, off) * 0.6f + Noise(x, y, 0.5f, off + 20) * 0.4f;
                float r = Mathf.Lerp(0.62f, 0.78f, n);
                float g = Mathf.Lerp(0.30f, 0.40f, n);
                float b = Mathf.Lerp(0.22f, 0.28f, n);
                px[y * Size + x] = new Color(r, g, b, 1f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }

        private static Texture2D Thatch(int salt)
        {
            var tex = NewTex("Thatch");
            var px = new Color[Size * Size];
            var rng = new System.Random(salt * 101 + 7);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Directional straw: brightness varies mostly along X (strands).
                float strand = Mathf.PerlinNoise(x * 0.6f, y * 0.08f + salt);
                float speck = (float)rng.NextDouble() * 0.1f;
                float r = Mathf.Lerp(0.72f, 0.88f, strand) - speck;
                float g = Mathf.Lerp(0.55f, 0.70f, strand) - speck;
                float b = Mathf.Lerp(0.28f, 0.38f, strand);
                px[y * Size + x] = new Color(r, g, b, 1f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }

        private static Texture2D MudWall(int salt)
        {
            var tex = NewTex("MudWall");
            var px = new Color[Size * Size];
            var rng = new System.Random(salt * 71 + 3);
            float off = salt * 3.3f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Noise(x, y, 0.18f, off);
                float r = Mathf.Lerp(0.55f, 0.66f, n);
                float g = Mathf.Lerp(0.42f, 0.50f, n);
                float b = Mathf.Lerp(0.30f, 0.36f, n);
                // Straw flecks.
                if (rng.NextDouble() < 0.03) { r += 0.15f; g += 0.12f; }
                px[y * Size + x] = new Color(r, g, b, 1f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }

        private static Texture2D PatternedMarble(int salt)
        {
            var tex = NewTex("PatternedMarble");
            var px = new Color[Size * Size];
            float off = salt * 6.7f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float veins = Noise(x, y, 0.08f, off);
                float baseC = Mathf.Lerp(0.88f, 0.97f, veins);
                // Faint jaali-style diamond lattice.
                float diamond = Mathf.Abs(((x + y) % 16) - 8) + Mathf.Abs(((x - y + 64) % 16) - 8);
                if (diamond < 2f) baseC *= 0.90f;
                px[y * Size + x] = new Color(baseC, baseC, baseC * 1.0f, 1f);
            }
            tex.SetPixels(px); tex.Apply(); return tex;
        }

        // --------------------------------------------------------------------
        // The set of internal names this generator supports (for iteration).
        // --------------------------------------------------------------------

        public static readonly IReadOnlyList<string> SupportedNames = new[]
        {
            "kalpa:temple_stone",
            "kalpa:carved_sandstone",
            "kalpa:terracotta",
            "kalpa:thatch",
            "kalpa:mud_wall",
            "kalpa:patterned_marble",
        };
    }
}
