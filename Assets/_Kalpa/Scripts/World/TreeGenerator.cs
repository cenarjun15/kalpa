// ============================================================================
// TreeGenerator.cs  (Phase 10 — biome-aware density)
// ----------------------------------------------------------------------------
// Tree placement now queries the TerrainGenerator for per-column biome density,
// so deserts stay bare, plains are sparse, and forests are dense — automatically.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    public sealed class TreeGenerator
    {
        private readonly byte logId;
        private readonly byte leavesId;
        private readonly byte grassId;
        private readonly int seed;
        private readonly Settings settings;
        private readonly TerrainGenerator terrain; // for biome density lookup

        [System.Serializable]
        public struct Settings
        {
            public float Density;       // global multiplier (biome density is multiplied by this)
            public int MinTrunkHeight;
            public int MaxTrunkHeight;
            public int CanopyRadius;
        }

        public TreeGenerator(int seed, BlockRegistry registry, Settings settings,
                             TerrainGenerator terrain)
        {
            this.seed = seed;
            this.settings = settings;
            this.terrain = terrain;

            logId    = registry.GetByName("kalpa:log")?.Id    ?? 0;
            leavesId = registry.GetByName("kalpa:leaves")?.Id ?? 0;
            grassId  = registry.GetByName("kalpa:grass")?.Id  ?? 1;

            if (logId == 0 || leavesId == 0)
                Debug.LogWarning("[TreeGenerator] Missing 'kalpa:log' or 'kalpa:leaves'. Trees disabled.");
        }

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

                // Per-biome density × global multiplier.
                float biomeDensity = terrain != null ? terrain.GetTreeDensityAt(wx, wz) : 0.1f;
                float density = biomeDensity * settings.Density * 10f; // scale to usable range
                if (density <= 0f) continue;

                if (!ShouldPlaceTree(wx, wz, density)) continue;

                int surfaceY = FindSurface(chunk, lx, lz);
                if (surfaceY < 0) continue;

                // Trees only grow on grass.
                if (chunk.GetBlockLocal(lx, surfaceY, lz) != grassId) continue;

                PlaceTree(world, wx, surfaceY + 1, wz);
            }
        }

        private bool ShouldPlaceTree(int wx, int wz, float density)
        {
            bool sparseGate = ((wx * 73 + wz * 91) & 3) == 0;
            if (!sparseGate) return false;

            uint h = Hash((uint)wx, (uint)wz, (uint)seed);
            float r = (h & 0xFFFFFF) / (float)0x1000000;
            return r < density;
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

        private int FindSurface(Chunk chunk, int lx, int lz)
        {
            for (int y = GameConstants.ChunkHeight - 1; y >= 0; y--)
                if (chunk.GetBlockLocal(lx, y, lz) != GameConstants.AirBlockId)
                    return y;
            return -1;
        }

        private void PlaceTree(VoxelWorld world, int baseX, int baseY, int baseZ)
        {
            uint h = Hash((uint)baseX, (uint)baseZ, (uint)(seed + 999));
            int range = Mathf.Max(1, settings.MaxTrunkHeight - settings.MinTrunkHeight + 1);
            int trunkHeight = settings.MinTrunkHeight + (int)(h % (uint)range);

            for (int i = 0; i < trunkHeight; i++)
            {
                int y = baseY + i;
                if (y >= GameConstants.ChunkHeight) break;
                world.SetBlock(baseX, y, baseZ, logId);
            }

            int topY = baseY + trunkHeight - 1;
            int r = Mathf.Max(1, settings.CanopyRadius);

            for (int dy = -1; dy <= r; dy++)
            {
                int layerRadius = (dy >= r - 1) ? r - 1 : r;
                int cy = topY + dy;
                if (cy < 0 || cy >= GameConstants.ChunkHeight) continue;

                for (int dx = -layerRadius; dx <= layerRadius; dx++)
                for (int dz = -layerRadius; dz <= layerRadius; dz++)
                {
                    if (Mathf.Abs(dx) == layerRadius && Mathf.Abs(dz) == layerRadius)
                    {
                        uint ch = Hash((uint)(baseX + dx), (uint)(baseZ + dz), (uint)(seed + dy + 7));
                        if ((ch & 1) == 0) continue;
                    }

                    int wx = baseX + dx, wy = cy, wz = baseZ + dz;
                    if (dx == 0 && dz == 0 && wy <= topY) continue;
                    if (world.GetBlock(wx, wy, wz) == GameConstants.AirBlockId)
                        world.SetBlock(wx, wy, wz, leavesId);
                }
            }
        }
    }
}
