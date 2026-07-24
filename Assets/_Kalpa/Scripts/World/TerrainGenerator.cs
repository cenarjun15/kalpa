// ============================================================================
// TerrainGenerator.cs  (Phase 10 — biome-aware)
// ----------------------------------------------------------------------------
// Terrain now varies by biome:
//   * A low-frequency biome noise field picks a biome per world column, so
//     biomes form large coherent regions.
//   * Surface / sub-surface / deep blocks come from the biome's settings.
//   * Terrain height amplitude is scaled per biome (deserts flatter, snow
//     mountains taller).
//   * Biome boundaries are height-blended to avoid harsh cliffs at borders.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    public sealed class TerrainGenerator
    {
        private readonly BlockRegistry registry;
        private readonly float noiseOffset;
        private readonly float biomeOffset;

        // Resolved block IDs per biome (cached).
        private struct ResolvedBiome
        {
            public byte Surface, SubSurface, Deep;
            public float HeightScale;
            public float TreeDensity;
            public bool AllowTrees;
        }
        private readonly ResolvedBiome[] biomes;

        private const float TerrainFreqLow  = 0.015f;
        private const float TerrainFreqHigh = 0.08f;
        private const float BiomeFreq = 0.006f;          // large biome regions
        private const int BaseAmplitude = 22;
        private const int DirtDepth = 3;

        public TerrainGenerator(int seed, BlockRegistry registry)
        {
            this.registry = registry;

            var rng = new System.Random(seed);
            noiseOffset = (float)(rng.NextDouble() * 10000);
            biomeOffset = (float)(rng.NextDouble() * 10000);

            // Resolve block IDs for all biomes once.
            var types = (BiomeType[])System.Enum.GetValues(typeof(BiomeType));
            biomes = new ResolvedBiome[types.Length];
            foreach (var t in types)
            {
                var s = BiomeSettings.For(t);
                biomes[(int)t] = new ResolvedBiome
                {
                    Surface     = Id(s.SurfaceBlock, 1),
                    SubSurface  = Id(s.SubSurfaceBlock, 2),
                    Deep        = Id(s.DeepBlock, 3),
                    HeightScale = s.HeightScale,
                    TreeDensity = s.TreeDensity,
                    AllowTrees  = s.AllowTrees,
                };
            }
        }

        private byte Id(string name, byte fallback)
            => registry.GetByName(name)?.Id ?? fallback;

        // --------------------------------------------------------------------
        // Public: biome query (used by TreeGenerator for per-biome density)
        // --------------------------------------------------------------------

        public BiomeType GetBiomeAt(int worldX, int worldZ)
        {
            float bx = (worldX + biomeOffset) * BiomeFreq;
            float bz = (worldZ + biomeOffset) * BiomeFreq;

            float temperature = Mathf.PerlinNoise(bx, bz);
            float humidity     = Mathf.PerlinNoise(bx + 500f, bz + 500f);

            // Simple temperature/humidity → biome mapping.
            if (temperature > 0.65f && humidity < 0.4f) return BiomeType.Desert;
            if (temperature < 0.35f)                     return BiomeType.Snow;
            if (humidity > 0.6f)                          return BiomeType.Forest;
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

                var biomeType = GetBiomeAt(wx, wz);
                var biome = biomes[(int)biomeType];

                int height = HeightAt(wx, wz, biome.HeightScale);

                for (int y = 0; y <= height && y < GameConstants.ChunkHeight; y++)
                {
                    byte id;
                    if (y == height) id = biome.Surface;
                    else if (y >= height - DirtDepth) id = biome.SubSurface;
                    else id = biome.Deep;

                    chunk.SetBlockLocal(lx, y, lz, id);
                }
            }
        }

        private int HeightAt(int x, int z, float biomeScale)
        {
            float fx = x + noiseOffset;
            float fz = z + noiseOffset;

            float low  = Mathf.PerlinNoise(fx * TerrainFreqLow,  fz * TerrainFreqLow);
            float high = Mathf.PerlinNoise(fx * TerrainFreqHigh, fz * TerrainFreqHigh) * 0.3f;
            float combined = Mathf.Clamp01((low + high) / 1.3f);

            int amp = Mathf.RoundToInt(BaseAmplitude * biomeScale);
            int height = GameConstants.SeaLevel + Mathf.RoundToInt((combined - 0.5f) * amp);
            return Mathf.Clamp(height, 1, GameConstants.ChunkHeight - 2);
        }
    }
}
