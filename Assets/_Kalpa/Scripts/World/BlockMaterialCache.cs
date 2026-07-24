// ============================================================================
// BlockMaterialCache.cs  (Phase 9 — opaque + transparent atlas materials)
// ----------------------------------------------------------------------------
// Now builds TWO materials, both textured with the same atlas:
//   * AtlasMaterial            — opaque geometry (grass, stone, wood…)
//   * TransparentAtlasMaterial — alpha-blended geometry (glass, water, leaves)
//
// The transparent material is configured for URP alpha blending and placed in
// the Transparent render queue so it draws AFTER opaque geometry.
// ============================================================================

using Kalpa.Blocks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kalpa.World
{
    public sealed class BlockMaterialCache
    {
        private readonly Shader shader;

        public Material AtlasMaterial { get; private set; }
        public Material TransparentAtlasMaterial { get; private set; }

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

        /// <summary>Build both opaque + transparent materials from the atlas texture.</summary>
        public void BuildAtlasMaterial(Texture2D atlasTexture)
        {
            // ---- Opaque ----
            AtlasMaterial = new Material(shader) { name = "BlockAtlasMaterial" };
            AssignTexture(AtlasMaterial, atlasTexture);
            SetCommon(AtlasMaterial);

            // ---- Transparent ----
            TransparentAtlasMaterial = new Material(shader) { name = "BlockAtlasMaterial_Transparent" };
            AssignTexture(TransparentAtlasMaterial, atlasTexture);
            SetCommon(TransparentAtlasMaterial);
            ConfigureTransparent(TransparentAtlasMaterial);
        }

        private static void AssignTexture(Material mat, Texture2D tex)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        private static void SetCommon(Material mat)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);
        }

        /// <summary>
        /// Configure a URP Lit material for alpha-blended transparency.
        /// This is the standard incantation URP requires when doing it from code.
        /// </summary>
        private static void ConfigureTransparent(Material mat)
        {
            // Surface Type = Transparent
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            // Blend mode = Alpha
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);

            mat.SetOverrideTag("RenderType", "Transparent");

            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);

            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Backwards-compat helper.</summary>
        public Material GetMaterial(BlockData data)
            => data != null && data.IsTransparent ? TransparentAtlasMaterial : AtlasMaterial;
    }
}
