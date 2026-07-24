// ============================================================================
// TreeGenerator.cs
// ----------------------------------------------------------------------------
// Places trees on top of generated terrain.
//
// Design:
//   * Deterministic — same seed + same chunk => same trees, every time.
//     This is essential so saved/reloaded worlds keep their trees, and so
//     trees don't "pop" differently each visit.
//   * Operates per-chunk but is border-aware: a tree whose canopy spills into
//     a neighbouring chunk still writes those leaf blocks via the VoxelWorld,
//     so canopies never get sliced at chunk edges.
//   * Uses a hash of (worldX, worldZ, seed) to decide tree placement, giving
//     natural-looking scattered distribution without storing any state.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.Core;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>
    /// Adds trees to chunks after the base terrain has been generated.
    /// </summary>
    public sealed class TreeGenerator
    {
        // Cached block IDs.
        private readonly byte logId;
        private readonly byte leavesId;
        private readonly byte grassId;

        private readonly int seed;

        // Tuning.
        [System.Serializable]
        public struct Settings
        {
            public float Density;     // 0..1 chance-ish per column (scaled down internally)
            public int MinTrunkHeight;
            public int MaxTrunkHeight;
            public int CanopyRadius;
        }

        private readonly Settings settings;

        public TreeGenerator(int seed, BlockRegistry registry, Settings settings)
        {
            this.seed = seed;
            this.settings = settings;

            logId    = registry.GetByName("kalpa:log")?.Id    ?? 0;
            leavesId = registry.GetByName("kalpa:leaves")?.Id ?? 0;
            grassId  = registry.GetByName("kalpa:grass")?.Id  ?? 1;

            if (logId == 0 || leavesId == 0)
            {
                Debug.LogWarning("[TreeGenerator] Missing 'kalpa:log' or 'kalpa:leaves' block. " +
                                 "Trees will not generate. Create those BlockData assets.");
            }
        }

        /// <summary>
        /// Populate one chunk with trees. Reads/writes through <paramref name="world"/>
        /// so canopies crossing chunk borders are handled correctly, provided the
        /// neighbouring chunk already exists.
        /// </summary>
        public void Generate(Chunk chunk, VoxelWorld world)
        {
            if (logId == 0 || leavesId == 0) return;

            int originX = chunk.Coordinate.WorldOriginX;
            int originZ = chunk.Coordinate.WorldOriginZ;

            for (int lx = 0; lx < GameConstants.ChunkSize; lx++)
            for (int lz = 0; lz < GameConstants.ChunkSize; lz++)
            {
                int wx = originX + lx;
                int wz = originZ + lz;

                if (!ShouldPlaceTree(wx, wz)) continue;

                // Find the grass surface height in this column.
                int surfaceY = FindSurface(chunk, lx, lz);
                if (surfaceY < 0) continue;

                // Only grow on grass.
                if (chunk.GetBlockLocal(lx, surfaceY, lz) != grassId) continue;

                PlaceTree(world, wx, surfaceY + 1, wz);
            }
        }

        // --------------------------------------------------------------------
        // Placement decision — deterministic hash based scatter.
        // --------------------------------------------------------------------

        private bool ShouldPlaceTree(int wx, int wz)
        {
            // Hash the coordinate + seed to a pseudo-random value in [0,1).
            uint h = Hash((uint)wx, (uint)wz, (uint)seed);
            float r = (h & 0xFFFFFF) / (float)0x1000000; // 24-bit fraction

            // Space trees out: only consider ~every few blocks, then apply density.
            // Using coordinate parity thinning keeps trees from clustering densely.
            bool sparseGate = ((wx * 73 + wz * 91) & 3) == 0; // ~1 in 4 columns eligible
            if (!sparseGate) return false;

            // settings.Density is scaled so 0.1 gives a pleasant forest, not a wall.
            return r < settings.Density;
        }

        private static uint Hash(uint x, uint z, uint seed)
        {
            unchecked
            {
                uint h = seed;
                h ^= x * 0x9E3779B1u;
                h = (h << 13) | (h >> 19);
                h ^= z * 0x85EBCA77u;
                h = (h << 17) | (h >> 15);
                h *= 0xC2B2AE3Du;
                h ^= h >> 16;
                return h;
            }
        }

        // --------------------------------------------------------------------
        // Surface finding
        // --------------------------------------------------------------------

        private int FindSurface(Chunk chunk, int lx, int lz)
        {
            for (int y = GameConstants.ChunkHeight - 1; y >= 0; y--)
            {
                if (chunk.GetBlockLocal(lx, y, lz) != GameConstants.AirBlockId)
                    return y;
            }
            return -1;
        }

        // --------------------------------------------------------------------
        // Tree construction
        // --------------------------------------------------------------------

        private void PlaceTree(VoxelWorld world, int baseX, int baseY, int baseZ)
        {
            // Deterministic trunk height from coordinate hash.
            uint h = Hash((uint)baseX, (uint)baseZ, (uint)(seed + 999));
            int range = Mathf.Max(1, settings.MaxTrunkHeight - settings.MinTrunkHeight + 1);
            int trunkHeight = settings.MinTrunkHeight + (int)(h % (uint)range);

            // Trunk.
            for (int i = 0; i < trunkHeight; i++)
            {
                int y = baseY + i;
                if (y >= GameConstants.ChunkHeight) break;
                world.SetBlock(baseX, y, baseZ, logId);
            }

            // Canopy — a rounded blob of leaves centred near the top of the trunk.
            int topY = baseY + trunkHeight - 1;
            int r = Mathf.Max(1, settings.CanopyRadius);

            for (int dy = -1; dy <= r; dy++)
            {
                // Radius shrinks toward the top for a rounded shape.
                int layerRadius = (dy >= r - 1) ? r - 1 : r;
                int cy = topY + dy;
                if (cy < 0 || cy >= GameConstants.ChunkHeight) continue;

                for (int dx = -layerRadius; dx <= layerRadius; dx++)
                for (int dz = -layerRadius; dz <= layerRadius; dz++)
                {
                    // Skip the far corners for a rounder canopy.
                    if (Mathf.Abs(dx) == layerRadius && Mathf.Abs(dz) == layerRadius)
                    {
                        // deterministic corner trim
                        uint ch = Hash((uint)(baseX + dx), (uint)(baseZ + dz), (uint)(seed + dy + 7));
                        if ((ch & 1) == 0) continue;
                    }

                    int wx = baseX + dx;
                    int wy = cy;
                    int wz = baseZ + dz;

                    // Don't overwrite the trunk.
                    if (dx == 0 && dz == 0 && wy <= topY) continue;

                    // Only place leaves into air (don't carve into terrain/other trees).
                    if (world.GetBlock(wx, wy, wz) == GameConstants.AirBlockId)
                        world.SetBlock(wx, wy, wz, leavesId);
                }
            }
        }
    }
}
