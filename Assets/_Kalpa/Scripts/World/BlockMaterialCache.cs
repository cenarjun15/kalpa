// ============================================================================
// BlockMaterialCache.cs
// ----------------------------------------------------------------------------
// Creates and caches one Material per block type, coloured by BlockData.debugColor.
// Uses URP/Lit if the URP is active, otherwise falls back to Standard.
// Phase 3+ will swap this out for a texture-atlas based system.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>
    /// Provides a cached Material for each registered block type.
    /// One material per block = allows submesh-based rendering per chunk.
    /// </summary>
    public sealed class BlockMaterialCache
    {
        private readonly Dictionary<byte, Material> byId = new Dictionary<byte, Material>();
        private readonly Shader shader;

        public BlockMaterialCache()
        {
            // Try URP Lit → URP Simple Lit → Standard.
            shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[BlockMaterialCache] No usable shader found!");
            }
            else
            {
                Debug.Log($"[BlockMaterialCache] Using shader: {shader.name}");
            }
        }

        /// <summary>
        /// Get (or create + cache) the material for a given block type.
        /// </summary>
        public Material GetMaterial(BlockData data)
        {
            if (data == null) return null;

            if (byId.TryGetValue(data.Id, out var existing))
                return existing;

            var mat = new Material(shader)
            {
                name = $"Block_{data.InternalName}"
            };

            // Set colour using whichever property the active shader exposes.
            var color = data.DebugColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);

            // Nicer defaults for a voxel game.
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);

            byId[data.Id] = mat;
            return mat;
        }

        /// <summary>Cached count. Handy for diagnostics.</summary>
        public int Count => byId.Count;
    }
}
