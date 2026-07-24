// ============================================================================
// BlockData.cs  (Phase 11 — cutout support)
// ----------------------------------------------------------------------------
// Adds IsCutout: blocks flagged cutout use the alpha-tested leaf material and
// render with see-through gaps (foliage). A block should be either normal,
// transparent (glass/water), OR cutout — not more than one.
// ============================================================================

using UnityEngine;

namespace Kalpa.Blocks
{
    [CreateAssetMenu(fileName = "NewBlockData", menuName = "Kalpa/Block Data", order = 1)]
    public sealed class BlockData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private byte id = 1;
        [SerializeField] private string internalName = "kalpa:new_block";
        [SerializeField] private string displayName = "New Block";
        [SerializeField] private BlockCategory category = BlockCategory.Natural;

        [Header("Appearance")]
        [SerializeField] private Texture2D texture;
        [SerializeField] private Color debugColor = Color.white;

        [Header("Physics")]
        [SerializeField] private bool isSolid = true;
        [SerializeField] private bool isTransparent = false;

        [Tooltip("Alpha-tested foliage rendering (see-through gaps, like leaves).")]
        [SerializeField] private bool isCutout = false;

        [SerializeField, Min(0f)] private float hardness = 1.0f;

        // Accessors
        public byte Id => id;
        public string InternalName => internalName;
        public string DisplayName => displayName;
        public BlockCategory Category => category;
        public Texture2D Texture => texture;
        public Color DebugColor => debugColor;
        public bool IsSolid => isSolid;
        public bool IsTransparent => isTransparent;
        public bool IsCutout => isCutout;
        public float Hardness => hardness;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(internalName)) internalName = "kalpa:unnamed";
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "Unnamed Block";
        }
    }
}
