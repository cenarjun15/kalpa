// ============================================================================
// BlockTextureAtlas.cs  (Phase 13 — cultural + ore procedural textures)
// ----------------------------------------------------------------------------
// When a BlockData has no assigned texture, the atlas asks (in order):
//   1. CulturalTextureGenerator (temple stone, sandstone, thatch…)
//   2. OreTextureGenerator      (coal, iron, gold ore)
//   3. Fallback checkerboard.
// ============================================================================

using Kalpa.Core;
using UnityEngine;

namespace Kalpa.Blocks
{
    public sealed class BlockTextureAtlas
    {
        public const int TileSize = 64;
        private const int AtlasTilesPerSide = 8;
        private const int AtlasPixels = TileSize * AtlasTilesPerSide;

        public Texture2D Atlas { get; private set; }
        private readonly Rect[] uvByBlockId = new Rect[GameConstants.MaxBlockTypes];
        private static Texture2D fallbackTile;

        public void Build(BlockRegistry registry)
        {
            Atlas = new Texture2D(AtlasPixels, AtlasPixels, TextureFormat.RGBA32, false)
            {
                name = "BlockTextureAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };

            var clear = new Color[AtlasPixels * AtlasPixels];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(1f, 0f, 1f, 1f);
            Atlas.SetPixels(clear);

            int slot = 0;
            for (int id = 1; id < GameConstants.MaxBlockTypes; id++)
            {
                var data = registry.GetById((byte)id);
                if (data == null) continue;

                if (slot >= AtlasTilesPerSide * AtlasTilesPerSide)
                {
                    Debug.LogError("[Atlas] Out of atlas slots! Increase AtlasTilesPerSide.");
                    break;
                }

                int tileX = slot % AtlasTilesPerSide;
                int tileY = slot / AtlasTilesPerSide;

                Texture2D source = data.Texture != null
                    ? EnsureReadable(data.Texture, data.name)
                    : (Kalpa.World.CulturalTextureGenerator.TryGenerate(data.InternalName, id)
                       ?? Kalpa.World.OreTextureGenerator.TryGenerate(data.InternalName, id));

                if (source != null)
                    CopyTileScaled(source, Atlas, tileX * TileSize, tileY * TileSize, TileSize, data.DebugColor);
                else
                    CopyTileScaled(GetFallbackTile(), Atlas, tileX * TileSize, tileY * TileSize, TileSize, data.DebugColor);

                const float bleed = 0.5f;
                uvByBlockId[id] = new Rect(
                    (tileX * TileSize + bleed) / AtlasPixels,
                    (tileY * TileSize + bleed) / AtlasPixels,
                    (TileSize - 2f * bleed) / AtlasPixels,
                    (TileSize - 2f * bleed) / AtlasPixels);

                slot++;
            }

            Atlas.Apply(false, true);
            Debug.Log($"[Atlas] Packed {slot} block textures into {AtlasPixels}×{AtlasPixels} atlas.");
        }

        public Rect GetUV(byte blockId) => uvByBlockId[blockId];

        private static Texture2D EnsureReadable(Texture2D source, string debugName)
        {
            if (source.isReadable) return source;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            { name = source.name + "_readable" };
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        private static void CopyTileScaled(Texture2D src, Texture2D dst,
                                           int offsetX, int offsetY, int tileSize, Color tint)
        {
            int srcW = src.width, srcH = src.height;
            var srcPixels = src.GetPixels();
            var tile = new Color[tileSize * tileSize];
            for (int y = 0; y < tileSize; y++)
            {
                int sy = Mathf.FloorToInt((y / (float)tileSize) * srcH);
                for (int x = 0; x < tileSize; x++)
                {
                    int sx = Mathf.FloorToInt((x / (float)tileSize) * srcW);
                    tile[y * tileSize + x] = srcPixels[sy * srcW + sx] * tint;
                }
            }
            dst.SetPixels(offsetX, offsetY, tileSize, tileSize, tile);
        }

        private static Texture2D GetFallbackTile()
        {
            if (fallbackTile != null) return fallbackTile;
            fallbackTile = new Texture2D(TileSize, TileSize, TextureFormat.RGBA32, false)
            { name = "FallbackTile", filterMode = FilterMode.Point };
            var px = new Color[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
            for (int x = 0; x < TileSize; x++)
            {
                bool check = ((x / 8) + (y / 8)) % 2 == 0;
                px[y * TileSize + x] = check ? new Color(1f, 0f, 1f, 1f) : new Color(0f, 0f, 0f, 1f);
            }
            fallbackTile.SetPixels(px); fallbackTile.Apply();
            return fallbackTile;
        }
    }
}
