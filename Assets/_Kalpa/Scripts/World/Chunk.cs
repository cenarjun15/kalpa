// ============================================================================
// Chunk.cs
// ----------------------------------------------------------------------------
// One chunk = ChunkSize × ChunkHeight × ChunkSize block IDs stored in a flat
// byte array. Flat arrays are much faster than 3D arrays or dictionaries
// for the tight iteration loops the mesh builder performs.
// ============================================================================

using Kalpa.Core;
using Kalpa.Utils;

namespace Kalpa.World
{
    /// <summary>
    /// A single chunk's block data. Coordinates inside the chunk are LOCAL:
    /// x, z in [0, ChunkSize), y in [0, ChunkHeight).
    /// </summary>
    public sealed class Chunk
    {
        // --------------------------------------------------------------------
        // Identity
        // --------------------------------------------------------------------

        public ChunkCoordinate Coordinate { get; }

        /// <summary>
        /// True if the mesh renderer needs to rebuild.
        /// Set by SetBlockLocal, cleared by the renderer after rebuild.
        /// </summary>
        public bool IsDirty { get; set; } = true;

        // --------------------------------------------------------------------
        // Storage
        // --------------------------------------------------------------------

        private readonly byte[] blocks;

        // --------------------------------------------------------------------
        // Constants (cached to avoid property calls in hot loops)
        // --------------------------------------------------------------------

        private const int Size = GameConstants.ChunkSize;
        private const int Height = GameConstants.ChunkHeight;
        private const int SizeSquared = Size * Size;

        // --------------------------------------------------------------------
        // Construction
        // --------------------------------------------------------------------

        public Chunk(ChunkCoordinate coordinate)
        {
            Coordinate = coordinate;
            blocks = new byte[Size * Size * Height];
            // Array defaults to 0 (air) — nothing else to do.
        }

        // --------------------------------------------------------------------
        // Local-space access (fast — no bounds checking in release)
        // --------------------------------------------------------------------

        /// <summary>
        /// Flatten (x, y, z) into a single array index.
        /// Layout: Y is outermost so vertical slices are cache-friendly.
        /// </summary>
        private static int IndexOf(int localX, int y, int localZ)
            => y * SizeSquared + localZ * Size + localX;

        /// <summary>
        /// Get a block from local chunk coordinates.
        /// Returns AIR if out-of-bounds — safer for neighbour queries.
        /// </summary>
        public byte GetBlockLocal(int localX, int y, int localZ)
        {
            if ((uint)localX >= Size || (uint)localZ >= Size || (uint)y >= Height)
                return GameConstants.AirBlockId;
            return blocks[IndexOf(localX, y, localZ)];
        }

        /// <summary>
        /// Set a block at local chunk coordinates.
        /// Returns the previous ID at that position.
        /// Silently ignores out-of-bounds writes.
        /// </summary>
        public byte SetBlockLocal(int localX, int y, int localZ, byte id)
        {
            if ((uint)localX >= Size || (uint)localZ >= Size || (uint)y >= Height)
                return GameConstants.AirBlockId;

            int i = IndexOf(localX, y, localZ);
            byte old = blocks[i];
            if (old == id) return old;

            blocks[i] = id;
            IsDirty = true;
            return old;
        }

        // --------------------------------------------------------------------
        // Bulk helpers
        // --------------------------------------------------------------------

        /// <summary>Direct access to the underlying array. Read-only usage only.</summary>
        public byte[] RawBlocks => blocks;

        /// <summary>Erase all blocks in this chunk.</summary>
        public void Clear()
        {
            System.Array.Clear(blocks, 0, blocks.Length);
            IsDirty = true;
        }
    }
}
