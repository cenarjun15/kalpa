// ============================================================================
// BlockData.cs  (Phase 5A update — texture support)
// ----------------------------------------------------------------------------
// Adds a texture reference so each block type maps to one tile in the atlas.
// Backward-compatible: DebugColor still exists as a fallback tint / for blocks
// without a texture.
// ============================================================================

using UnityEngine;

namespace Kalpa.Blocks
{
    /// <summary>
    /// Data-only definition of a block type.
    /// Create instances via: Assets → Create → Kalpa → Block Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBlockData",
        menuName = "Kalpa/Block Data",
        order = 1)]
    public sealed class BlockData : ScriptableObject
    {
        // --------------------------------------------------------------------
        // Identity
        // --------------------------------------------------------------------

        [Header("Identity")]

        [Tooltip("Unique numeric ID for this block. 0 is reserved for Air.")]
        [SerializeField] private byte id = 1;

        [Tooltip("Short internal identifier (e.g. 'kalpa:grass'). Used for saves & mods.")]
        [SerializeField] private string internalName = "kalpa:new_block";

        [Tooltip("Display name shown in UI.")]
        [SerializeField] private string displayName = "New Block";

        [Tooltip("Broad classification for filtering & rules.")]
        [SerializeField] private BlockCategory category = BlockCategory.Natural;

        // --------------------------------------------------------------------
        // Appearance
        // --------------------------------------------------------------------

        [Header("Appearance")]

        [Tooltip("Texture used for all 6 faces of this block. " +
                 "If null, DebugColor is used as a flat tint.")]
        [SerializeField] private Texture2D texture;

        [Tooltip("Tint applied on top of the texture (multiplied). " +
                 "Also used as fallback if Texture is null.")]
        [SerializeField] private Color debugColor = Color.white;

        // --------------------------------------------------------------------
        // Physical behaviour
        // --------------------------------------------------------------------

        [Header("Physics")]

        [SerializeField] private bool isSolid = true;
        [SerializeField] private bool isTransparent = false;
        [SerializeField, Min(0f)] private float hardness = 1.0f;

        // --------------------------------------------------------------------
        // Public accessors
        // --------------------------------------------------------------------

        public byte Id => id;
        public string InternalName => internalName;
        public string DisplayName => displayName;
        public BlockCategory Category => category;
        public Texture2D Texture => texture;
        public Color DebugColor => debugColor;
        public bool IsSolid => isSolid;
        public bool IsTransparent => isTransparent;
        public float Hardness => hardness;

        // --------------------------------------------------------------------
        // Validation
        // --------------------------------------------------------------------

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(internalName))
                internalName = "kalpa:unnamed";
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Unnamed Block";
        }
    }
}
