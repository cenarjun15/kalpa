// ============================================================================
// VoxelWorld.cs  (Phase 8 update — chunk removal + modification flagging)
// ----------------------------------------------------------------------------
// Adds:
//   * RemoveChunk + ChunkRemoved event (so the streamer can unload chunks and
//     the renderer can destroy the matching GameObject).
//   * SetBlock now flags the owning chunk as IsModified, so edits survive
//     unload/reload.
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

        public event Action<BlockPosition, byte, byte> BlockChanged;
        public event Action<Chunk> ChunkAdded;
        public event Action<Chunk> ChunkRemoved;

        // --------------------------------------------------------------------
        // Storage
        // --------------------------------------------------------------------

        private readonly Dictionary<ChunkCoordinate, Chunk> chunks
            = new Dictionary<ChunkCoordinate, Chunk>(capacity: 256);

        private readonly BlockRegistry registry;

        public VoxelWorld(BlockRegistry registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // --------------------------------------------------------------------
        // Chunk management
        // --------------------------------------------------------------------

        public void AddChunk(Chunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            chunks[chunk.Coordinate] = chunk;
            ChunkAdded?.Invoke(chunk);
        }

        /// <summary>Remove a chunk from the world. Fires ChunkRemoved.</summary>
        public void RemoveChunk(ChunkCoordinate coord)
        {
            if (chunks.TryGetValue(coord, out var chunk))
            {
                chunks.Remove(coord);
                ChunkRemoved?.Invoke(chunk);
            }
        }

        public Chunk GetChunk(ChunkCoordinate coord)
            => chunks.TryGetValue(coord, out var c) ? c : null;

        public bool HasChunk(ChunkCoordinate coord) => chunks.ContainsKey(coord);

        public IEnumerable<Chunk> AllChunks => chunks.Values;

        public int ChunkCount => chunks.Count;

        // --------------------------------------------------------------------
        // Block-level access (world coordinates)
        // --------------------------------------------------------------------

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
                chunk.IsModified = true; // player-edit → persist before unload
                BlockChanged?.Invoke(new BlockPosition(worldX, y, worldZ), oldId, newId);
            }
        }

        public void SetBlock(BlockPosition pos, byte newId) => SetBlock(pos.X, pos.Y, pos.Z, newId);
    }
}
