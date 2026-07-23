// ============================================================================
// VoxelWorld.cs  (Phase 2 — chunk-based storage)
// ----------------------------------------------------------------------------
// Replaces the Phase 1 dictionary-of-blocks with a dictionary-of-chunks.
// Public API is unchanged (GetBlock / SetBlock / BlockChanged), so all Phase 1
// callers keep working. Internal storage is now chunk-based for scalability.
// ============================================================================

using System;
using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using Kalpa.Utils;

namespace Kalpa.World
{
    /// <summary>
    /// The current state of the voxel world, backed by chunks.
    /// </summary>
    public sealed class VoxelWorld
    {
        // --------------------------------------------------------------------
        // Events
        // --------------------------------------------------------------------

        /// <summary>Fired whenever a block is set, replaced, or removed.</summary>
        public event Action<BlockPosition, byte, byte> BlockChanged;

        /// <summary>Fired when a new chunk is added to the world.</summary>
        public event Action<Chunk> ChunkAdded;

        // --------------------------------------------------------------------
        // Storage
        // --------------------------------------------------------------------

        private readonly Dictionary<ChunkCoordinate, Chunk> chunks
            = new Dictionary<ChunkCoordinate, Chunk>(capacity: 256);

        private readonly BlockRegistry registry;

        // --------------------------------------------------------------------
        // Construction
        // --------------------------------------------------------------------

        public VoxelWorld(BlockRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // --------------------------------------------------------------------
        // Chunk management
        // --------------------------------------------------------------------

        /// <summary>Add a fully-populated chunk to the world.</summary>
        public void AddChunk(Chunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            chunks[chunk.Coordinate] = chunk;
            ChunkAdded?.Invoke(chunk);
        }

        /// <summary>Get a chunk by coordinate. Returns null if not loaded.</summary>
        public Chunk GetChunk(ChunkCoordinate coord)
            => chunks.TryGetValue(coord, out var c) ? c : null;

        /// <summary>True if a chunk at the given coordinate is loaded.</summary>
        public bool HasChunk(ChunkCoordinate coord) => chunks.ContainsKey(coord);

        /// <summary>Enumerate all loaded chunks.</summary>
        public IEnumerable<Chunk> AllChunks => chunks.Values;

        /// <summary>Total loaded chunk count.</summary>
        public int ChunkCount => chunks.Count;

        // --------------------------------------------------------------------
        // Block-level access (world coordinates)
        // --------------------------------------------------------------------

        /// <summary>
        /// Get the block at a world position. Returns AIR for unloaded areas
        /// or out-of-height positions.
        /// </summary>
        public byte GetBlock(int worldX, int y, int worldZ)
        {
            if ((uint)y >= GameConstants.ChunkHeight) return GameConstants.AirBlockId;

            var coord = ChunkCoordinate.FromWorldXZ(worldX, worldZ);
            if (!chunks.TryGetValue(coord, out var chunk))
                return GameConstants.AirBlockId;

            int localX = worldX - coord.WorldOriginX;
            int localZ = worldZ - coord.WorldOriginZ;
            return chunk.GetBlockLocal(localX, y, localZ);
        }

        public byte GetBlock(BlockPosition pos) => GetBlock(pos.X, pos.Y, pos.Z);

        /// <summary>
        /// Set a block at a world position.
        /// Silently ignores writes to unloaded chunks or out-of-height Y.
        /// Fires BlockChanged if the ID actually changed.
        /// </summary>
        public void SetBlock(int worldX, int y, int worldZ, byte newId)
        {
            if ((uint)y >= GameConstants.ChunkHeight) return;
            if (!registry.IsRegistered(newId))
                throw new ArgumentException($"Block ID {newId} is not registered.");

            var coord = ChunkCoordinate.FromWorldXZ(worldX, worldZ);
            if (!chunks.TryGetValue(coord, out var chunk)) return;

            int localX = worldX - coord.WorldOriginX;
            int localZ = worldZ - coord.WorldOriginZ;

            byte oldId = chunk.SetBlockLocal(localX, y, localZ, newId);
            if (oldId != newId)
            {
                BlockChanged?.Invoke(new BlockPosition(worldX, y, worldZ), oldId, newId);
            }
        }

        public void SetBlock(BlockPosition pos, byte newId) => SetBlock(pos.X, pos.Y, pos.Z, newId);
    }
}
