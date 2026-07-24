// ============================================================================
// TerrainGenerator.cs  (Phase 13 — biomes + caves + ores)
// ----------------------------------------------------------------------------
// Generation per column:
//   1. Biome selection (temperature/humidity noise) — as Phase 10.
//   2. Solid terrain fill (surface / sub-surface / deep) with biome height.
//   3. Cave carving: 3D noise removes stone below the surface to form tunnels.
//   4. Ore placement: coal/iron/gold seeded into remaining stone by depth.
// Deterministic for a given seed.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    public sealed class TerrainGenerator
    {
        private readonly BlockRegistry registry;
        private readonly int seed;
        private readonly float noiseOffset;
        private readonly float biomeOffset;
        private readonly float caveOffset;

        private struct ResolvedBiome
        {
            public byte Surface, SubSurface, Deep;
            public float HeightScale, TreeDensity;
            public bool AllowTrees;
        }
        private readonly ResolvedBiome[] biomes;

        // Ore ids.
        private readonly byte stoneId;
        private readonly byte coalId, ironId, goldId;

        private const float TerrainFreqLow = 0.015f;
        private const float TerrainFreqHigh = 0.08f;
        private const float BiomeFreq = 0.006f;
        private const int BaseAmplitude = 22;
        private const int DirtDepth = 3;

        // Cave tuning.
        private const float CaveFreq = 0.075f;
        private const float CaveThreshold = 0.42f;  // higher = fewer/smaller caves
        private const int CaveMinDepthBelowSurface = 3; // keep surface intact
        private const int CaveFloor = 3;               // don't carve bedrock-ish bottom

        public TerrainGenerator(int seed, BlockRegistry registry)
        {
            this.seed = seed;
            this.registry = registry;

            var rng = new System.Random(seed);
            noiseOffset = (float)(rng.NextDouble() * 10000);
            biomeOffset = (float)(rng.NextDouble() * 10000);
            caveOffset  = (float)(rng.NextDouble() * 10000);

            var types = (BiomeType[])System.Enum.GetValues(typeof(BiomeType));
            biomes = new ResolvedBiome[types.Length];
            foreach (var t in types)
            {
                var s = BiomeSettings.For(t);
                biomes[(int)t] = new ResolvedBiome
                {
                    Surface = Id(s.SurfaceBlock, 1),
                    SubSurface = Id(s.SubSurfaceBlock, 2),
                    Deep = Id(s.DeepBlock, 3),
                    HeightScale = s.HeightScale,
                    TreeDensity = s.TreeDensity,
                    AllowTrees = s.AllowTrees,
                };
            }

            stoneId = Id("kalpa:stone", 3);
            coalId  = Id("kalpa:coal_ore", 0);
            ironId  = Id("kalpa:iron_ore", 0);
            goldId  = Id("kalpa:gold_ore", 0);
        }

        private byte Id(string name, byte fallback)
            => registry.GetByName(name)?.Id ?? fallback;

        // --------------------------------------------------------------------
        // Biome queries (used by TreeGenerator)
        // --------------------------------------------------------------------

        public BiomeType GetBiomeAt(int worldX, int worldZ)
        {
            float bx = (worldX + biomeOffset) * BiomeFreq;
            float bz = (worldZ + biomeOffset) * BiomeFreq;
            float temperature = Mathf.PerlinNoise(bx, bz);
            float humidity = Mathf.PerlinNoise(bx + 500f, bz + 500f);

            if (temperature > 0.65f && humidity < 0.4f) return BiomeType.Desert;
            if (temperature < 0.35f) return BiomeType.Snow;
            if (humidity > 0.6f) return BiomeType.Forest;
            return BiomeType.Plains;
        }

        public float GetTreeDensityAt(int worldX, int worldZ)
        {
            var b = biomes[(int)GetBiomeAt(worldX, worldZ)];
            return b.AllowTrees ? b.TreeDensity : 0f;
        }

        // --------------------------------------------------------------------
        // Generation
        // --------------------------------------------------------------------

        public void Generate(Chunk chunk)
        {
            int originX = chunk.Coordinate.WorldOriginX;
            int originZ = chunk.Coordinate.WorldOriginZ;

            for (int lx = 0; lx < GameConstants.ChunkSize; lx++)
            for (int lz = 0; lz < GameConstants.ChunkSize; lz++)
            {
                int wx = originX + lx;
                int wz = originZ + lz;

                var biome = biomes[(int)GetBiomeAt(wx, wz)];
                int height = HeightAt(wx, wz, biome.HeightScale);

                for (int y = 0; y <= height && y < GameConstants.ChunkHeight; y++)
                {
                    // --- Cave carving ---
                    if (y > CaveFloor && y < height - CaveMinDepthBelowSurface)
                    {
                        float c = Cave3D(wx, y, wz);
                        if (c > CaveThreshold)
                            continue; // air pocket → skip placing a block
                    }

                    // --- Block selection ---
                    byte id;
                    if (y == height) id = biome.Surface;
                    else if (y >= height - DirtDepth) id = biome.SubSurface;
                    else
                    {
                        id = biome.Deep;
                        // --- Ore placement (only in stone-deep layers) ---
                        if (id == stoneId)
                        {
                            byte ore = PickOre(wx, y, wz);
                            if (ore != 0) id = ore;
                        }
                    }

                    chunk.SetBlockLocal(lx, y, lz, id);
                }
            }
        }

        private int HeightAt(int x, int z, float biomeScale)
        {
            float fx = x + noiseOffset;
            float fz = z + noiseOffset;
            float low = Mathf.PerlinNoise(fx * TerrainFreqLow, fz * TerrainFreqLow);
            float high = Mathf.PerlinNoise(fx * TerrainFreqHigh, fz * TerrainFreqHigh) * 0.3f;
            float combined = Mathf.Clamp01((low + high) / 1.3f);
            int amp = Mathf.RoundToInt(BaseAmplitude * biomeScale);
            int height = GameConstants.SeaLevel + Mathf.RoundToInt((combined - 0.5f) * amp);
            return Mathf.Clamp(height, 1, GameConstants.ChunkHeight - 2);
        }

        // --------------------------------------------------------------------
        // Caves — 3D Perlin (via 2D-perlin combination since Unity lacks 3D).
        // --------------------------------------------------------------------

        private float Cave3D(int x, int y, int z)
        {
            float fx = (x + caveOffset) * CaveFreq;
            float fy = (y + caveOffset) * CaveFreq;
            float fz = (z + caveOffset) * CaveFreq;

            // Combine 3 orthogonal 2D perlin slices to approximate 3D noise.
            float xy = Mathf.PerlinNoise(fx, fy);
            float yz = Mathf.PerlinNoise(fy, fz);
            float xz = Mathf.PerlinNoise(fx, fz);
            return (xy + yz + xz) / 3f;
        }

        // --------------------------------------------------------------------
        // Ores — deterministic hash, depth-gated rarity.
        // --------------------------------------------------------------------

        private byte PickOre(int x, int y, int z)
        {
            uint h = Hash((uint)x, (uint)y, (uint)z, (uint)seed);
            float r = (h & 0xFFFFFF) / (float)0x1000000; // 0..1

            // Depth from surface influences which ores appear.
            // Gold only deep, iron mid+, coal common at most depths.
            // Rarity thresholds (chance per stone block):
            //   coal ~4%, iron ~2.5%, gold ~0.8% (deep only)
            if (goldId != 0 && y < GameConstants.SeaLevel - 12 && r < 0.008f) return goldId;
            if (ironId != 0 && y < GameConstants.SeaLevel - 4  && r < 0.033f) return ironId;
            if (coalId != 0 && r < 0.075f) return coalId;
            return 0;
        }

        private static uint Hash(uint x, uint y, uint z, uint seed)
        {
            unchecked
            {
                uint h = seed;
                h ^= x * 0x9E3779B1u; h = (h << 13) | (h >> 19);
                h ^= y * 0x85EBCA77u; h = (h << 17) | (h >> 15);
                h ^= z * 0xC2B2AE3Du; h = (h << 11) | (h >> 21);
                h *= 0x27D4EB2Fu; h ^= h >> 15;
                return h;
            }
        }
    }
}
