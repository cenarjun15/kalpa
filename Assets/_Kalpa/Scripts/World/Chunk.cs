// ============================================================================
// Chunk.cs  (Phase 8 update — modification tracking)
// ----------------------------------------------------------------------------
// Adds IsModified: set true whenever the player changes a block, so the streamer
// knows this chunk must be written to disk before it is unloaded. Procedurally
// generated (but untouched) chunks don't need saving — they regenerate identically
// from the seed.
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
        public ChunkCoordinate Coordinate { get; }

        /// <summary>Mesh needs rebuild.</summary>
        public bool IsDirty { get; set; } = true;

        /// <summary>Player has modified this chunk → must be persisted before unload.</summary>
        public bool IsModified { get; set; } = false;

        private readonly byte[] blocks;

        private const int Size = GameConstants.ChunkSize;
        private const int Height = GameConstants.ChunkHeight;
        private const int SizeSquared = Size * Size;

        public Chunk(ChunkCoordinate coordinate)
        {
            Coordinate = coordinate;
            blocks = new byte[Size * Size * Height];
        }

        private static int IndexOf(int localX, int y, int localZ)
            => y * SizeSquared + localZ * Size + localX;

        public byte GetBlockLocal(int localX, int y, int localZ)
        {
            if ((uint)localX >= Size || (uint)localZ >= Size || (uint)y >= Height)
                return GameConstants.AirBlockId;
            return blocks[IndexOf(localX, y, localZ)];
        }

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

        public byte[] RawBlocks => blocks;

        public void Clear()
        {
            System.Array.Clear(blocks, 0, blocks.Length);
            IsDirty = true;
        }
    }
}
