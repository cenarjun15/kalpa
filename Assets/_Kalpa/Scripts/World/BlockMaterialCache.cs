// ============================================================================
// BlockMaterialCache.cs  (Phase 11 — adds cutout material)
// ----------------------------------------------------------------------------
// Three materials now:
//   * AtlasMaterial            — opaque geometry
//   * TransparentAtlasMaterial — alpha-blended (glass, water)
//   * CutoutMaterial           — alpha-TESTED foliage (leaves), samples the
//                                generated leaf texture directly (not the atlas)
//                                because cutout needs per-pixel alpha the atlas
//                                doesn't preserve.
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
        public Material CutoutMaterial { get; private set; }

        public BlockMaterialCache()
        {
            shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                  ?? Shader.Find("Standard");

            if (shader == null) Debug.LogError("[BlockMaterialCache] No usable shader!");
            else Debug.Log($"[BlockMaterialCache] Using shader: {shader.name}");
        }

        public void BuildAtlasMaterial(Texture2D atlasTexture)
        {
            AtlasMaterial = new Material(shader) { name = "BlockAtlasMaterial" };
            AssignTexture(AtlasMaterial, atlasTexture);
            SetCommon(AtlasMaterial);

            TransparentAtlasMaterial = new Material(shader) { name = "BlockAtlasMaterial_Transparent" };
            AssignTexture(TransparentAtlasMaterial, atlasTexture);
            SetCommon(TransparentAtlasMaterial);
            ConfigureTransparent(TransparentAtlasMaterial);
        }

        /// <summary>Build the cutout foliage material from a leaf texture with alpha holes.</summary>
        public void BuildCutoutMaterial(Texture2D leafTexture)
        {
            CutoutMaterial = new Material(shader) { name = "BlockLeafCutout" };
            AssignTexture(CutoutMaterial, leafTexture);
            SetCommon(CutoutMaterial);
            ConfigureCutout(CutoutMaterial);
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

        private static void ConfigureTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>
        /// Alpha-TEST (cutout): pixels below the cutoff are discarded, so foliage
        /// has hard-edged transparent gaps and still writes depth (no sorting issues).
        /// </summary>
        private static void ConfigureCutout(Material mat)
        {
            // URP alpha clipping.
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            // Cutout is opaque queue (AlphaTest) — writes depth, no blending.
            mat.renderQueue = (int)RenderQueue.AlphaTest;

            // Render both sides so you see leaves from inside the canopy.
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
        }

        public Material GetMaterial(BlockData data)
        {
            if (data == null) return AtlasMaterial;
            if (data.IsCutout) return CutoutMaterial;
            if (data.IsTransparent) return TransparentAtlasMaterial;
            return AtlasMaterial;
        }
    }
}
