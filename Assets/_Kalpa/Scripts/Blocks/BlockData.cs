// ============================================================================
// BlockData.cs
// ----------------------------------------------------------------------------
// ScriptableObject describing a single block type.
// One asset = one block type. Designers create these in the Unity editor,
// no code changes required to add a new block later.
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

        [Tooltip("Placeholder tint used until textures are added in Phase 2.")]
        [SerializeField] private Color debugColor = Color.white;

        // --------------------------------------------------------------------
        // Physical behaviour
        // --------------------------------------------------------------------

        [Header("Physics")]

        [Tooltip("Whether player collides with this block.")]
        [SerializeField] private bool isSolid = true;

        [Tooltip("Whether raycasts can see through it (used for face culling later).")]
        [SerializeField] private bool isTransparent = false;

        [Tooltip("Hardness — how long it takes to break. 0 = instant.")]
        [SerializeField, Min(0f)] private float hardness = 1.0f;

        // --------------------------------------------------------------------
        // Public accessors (read-only from outside)
        // --------------------------------------------------------------------

        public byte Id => id;
        public string InternalName => internalName;
        public string DisplayName => displayName;
        public BlockCategory Category => category;
        public Color DebugColor => debugColor;
        public bool IsSolid => isSolid;
        public bool IsTransparent => isTransparent;
        public float Hardness => hardness;

        // --------------------------------------------------------------------
        // Validation — runs whenever asset is edited in inspector
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
