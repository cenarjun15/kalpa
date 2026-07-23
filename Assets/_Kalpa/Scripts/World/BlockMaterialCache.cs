// ============================================================================
// BlockMaterialCache.cs  (Phase 5A update — atlas support)
// ----------------------------------------------------------------------------
// Now provides ONE shared material for ALL block types, textured with the atlas.
// Chunks use a single material + submesh-per-block-type — but because every
// submesh points at the same atlas, the renderer batches them into one draw
// call. This is the key perf win of Phase 5A.
// ============================================================================

using Kalpa.Blocks;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>
    /// Provides materials for chunk rendering. Now atlas-based rather than one
    /// material per block type.
    /// </summary>
    public sealed class BlockMaterialCache
    {
        private readonly Shader shader;

        /// <summary>The atlas-textured material used by every chunk mesh.</summary>
        public Material AtlasMaterial { get; private set; }

        public BlockMaterialCache()
        {
            shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Standard");

            if (shader == null)
                Debug.LogError("[BlockMaterialCache] No usable shader found!");
            else
                Debug.Log($"[BlockMaterialCache] Using shader: {shader.name}");
        }

        /// <summary>
        /// Build the single atlas material. Call once, after the atlas has been baked.
        /// </summary>
        public void BuildAtlasMaterial(Texture2D atlasTexture)
        {
            AtlasMaterial = new Material(shader) { name = "BlockAtlasMaterial" };

            if (AtlasMaterial.HasProperty("_BaseMap"))
                AtlasMaterial.SetTexture("_BaseMap", atlasTexture);
            if (AtlasMaterial.HasProperty("_MainTex"))
                AtlasMaterial.SetTexture("_MainTex", atlasTexture);

            if (AtlasMaterial.HasProperty("_BaseColor"))
                AtlasMaterial.SetColor("_BaseColor", Color.white);
            if (AtlasMaterial.HasProperty("_Color"))
                AtlasMaterial.SetColor("_Color", Color.white);

            if (AtlasMaterial.HasProperty("_Smoothness"))
                AtlasMaterial.SetFloat("_Smoothness", 0.05f);
            if (AtlasMaterial.HasProperty("_Metallic"))
                AtlasMaterial.SetFloat("_Metallic", 0f);
        }

        /// <summary>Backwards-compat: some old code may still ask for a per-block material.</summary>
        public Material GetMaterial(BlockData data) => AtlasMaterial;
    }
}
