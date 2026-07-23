// ============================================================================
// BlockCategory.cs
// ----------------------------------------------------------------------------
// Enum used to group blocks. Useful for filtering, UI, mod support, etc.
// ============================================================================

namespace Kalpa.Blocks
{
    /// <summary>
    /// Broad classification of a block type.
    /// Used by inventory filters, biome rules, and future crafting logic.
    /// </summary>
    public enum BlockCategory
    {
        Air = 0,
        Natural,      // grass, dirt, sand, gravel
        Stone,        // stone, granite, marble
        Wood,         // oak, teak, sandalwood, bamboo
        Foliage,      // leaves, flowers
        Water,        // water, lava (future)
        BuildingBlock,// bricks, planks, tiles
        Decorative,   // torches, lanterns (future)
        Cultural      // temple pillars, jharokha, chhatri (India-specific)
    }
}
