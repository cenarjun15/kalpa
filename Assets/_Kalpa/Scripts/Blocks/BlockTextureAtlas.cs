// ============================================================================
// BlockTextureAtlas.cs
// ----------------------------------------------------------------------------
// Combines every block's texture into a single big "atlas" texture, and stores
// UV rectangles telling the mesh builder where each block's tile lives inside
// the atlas.
//
// Why an atlas?
//   * ONE draw call per chunk instead of one per block type.
//   * Massive performance win — 100k blocks in view is fine with an atlas,
//     impossible without one.
//   * Same technique Minecraft (Java Edition), Vintage Story, and Terraria use.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.Blocks
{
    /// <summary>
    /// A packed texture atlas for all block textures + per-block UV lookup.
    /// </summary>
    public sealed class BlockTextureAtlas
    {
        // --------------------------------------------------------------------
        // Constants
        // --------------------------------------------------------------------

        /// <summary>Each tile in the atlas is padded to this size (pixels).</summary>
        public const int TileSize = 64;

        /// <summary>Atlas size — must be a power of 2. 8×8 = 64 tiles supported.</summary>
        private const int AtlasTilesPerSide = 8;
        private const int AtlasPixels = TileSize * AtlasTilesPerSide;

        // --------------------------------------------------------------------
        // Output
        // --------------------------------------------------------------------

        /// <summary>The packed atlas texture — bound to the block material.</summary>
        public Texture2D Atlas { get; private set; }

        /// <summary>UV rectangle for each block ID (in atlas UV space, 0..1).</summary>
        private readonly Rect[] uvByBlockId = new Rect[GameConstants.MaxBlockTypes];

        // --------------------------------------------------------------------
        // Fallback tile — a flat pink texture used when a block has no texture.
        // Deliberately obvious so missing textures are visible during dev.
        // --------------------------------------------------------------------

        private static Texture2D fallbackTile;

        // --------------------------------------------------------------------
        // Build
        // --------------------------------------------------------------------

        /// <summary>
        /// Pack all registered blocks' textures into a single atlas.
        /// </summary>
        public void Build(BlockRegistry registry)
        {
            // Create the destination atlas — RGBA32, point-filtered, no mipmaps.
            Atlas = new Texture2D(AtlasPixels, AtlasPixels, TextureFormat.RGBA32, mipChain: false)
            {
                name = "BlockTextureAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            // Clear atlas to bright pink so any "unfilled" area is obvious.
            var clear = new Color[AtlasPixels * AtlasPixels];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(1f, 0f, 1f, 1f);
            Atlas.SetPixels(clear);

            // Iterate every registered block and copy its texture into a tile.
            int slot = 0;
            for (int id = 1; id < GameConstants.MaxBlockTypes; id++)
            {
                var data = registry.GetById((byte)id);
                if (data == null) continue;

                if (slot >= AtlasTilesPerSide * AtlasTilesPerSide)
                {
                    Debug.LogError($"[Atlas] Ran out of atlas slots! Increase AtlasTilesPerSide.");
                    break;
                }

                int tileX = slot % AtlasTilesPerSide;
                int tileY = slot / AtlasTilesPerSide;

                var source = data.Texture != null
                    ? EnsureReadable(data.Texture, data.name)
                    : GetFallbackTile();

                CopyTileScaled(source, Atlas, tileX * TileSize, tileY * TileSize, TileSize, data.DebugColor);

                // Store UV rect in atlas UV space (0..1). We tighten the UV slightly
                // inward so that texture-filter bleed from neighbouring tiles is impossible.
                const float bleed = 0.5f;
                uvByBlockId[id] = new Rect(
                    (tileX * TileSize + bleed) / AtlasPixels,
                    (tileY * TileSize + bleed) / AtlasPixels,
                    (TileSize - 2f * bleed) / AtlasPixels,
                    (TileSize - 2f * bleed) / AtlasPixels);

                slot++;
            }

            Atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            Debug.Log($"[Atlas] Packed {slot} block textures into {AtlasPixels}×{AtlasPixels} atlas.");
        }

        /// <summary>Get the atlas UV rectangle for a given block ID.</summary>
        public Rect GetUV(byte blockId) => uvByBlockId[blockId];

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        /// <summary>
        /// Make sure a texture is CPU-readable so we can access its pixels.
        /// If the imported asset isn't marked Readable, we copy it through a
        /// temporary RenderTexture — works for any import setting.
        /// </summary>
        private static Texture2D EnsureReadable(Texture2D source, string debugName)
        {
            if (source.isReadable) return source;

            // Blit through a temporary RenderTexture.
            var rt = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = source.name + "_readable",
            };
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }

        /// <summary>
        /// Copy <paramref name="src"/> into <paramref name="dst"/> at the given
        /// pixel offset, scaling to <paramref name="tileSize"/> square.
        /// Uses nearest-neighbour scaling for that crisp voxel look.
        /// Applies <paramref name="tint"/> multiplicatively.
        /// </summary>
        private static void CopyTileScaled(Texture2D src, Texture2D dst,
                                           int offsetX, int offsetY,
                                           int tileSize, Color tint)
        {
            int srcW = src.width;
            int srcH = src.height;

            var srcPixels = src.GetPixels();
            var tile = new Color[tileSize * tileSize];

            for (int y = 0; y < tileSize; y++)
            {
                int sy = Mathf.FloorToInt((y / (float)tileSize) * srcH);
                for (int x = 0; x < tileSize; x++)
                {
                    int sx = Mathf.FloorToInt((x / (float)tileSize) * srcW);
                    var c = srcPixels[sy * srcW + sx];
                    // Multiply by tint so DebugColor still influences the look.
                    tile[y * tileSize + x] = c * tint;
                }
            }

            dst.SetPixels(offsetX, offsetY, tileSize, tileSize, tile);
        }

        /// <summary>Get (or create) the checkerboard fallback tile.</summary>
        private static Texture2D GetFallbackTile()
        {
            if (fallbackTile != null) return fallbackTile;

            fallbackTile = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false)
            {
                name = "FallbackTile",
                filterMode = FilterMode.Point,
            };
            var px = new Color[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                bool check = ((x / 8) + (y / 8)) % 2 == 0;
                px[y * TileSize + x] = check
                    ? new Color(1f, 0f, 1f, 1f)   // magenta
                    : new Color(0f, 0f, 0f, 1f); // black
            }
            fallbackTile.SetPixels(px);
            fallbackTile.Apply();
            return fallbackTile;
        }
    }
}
