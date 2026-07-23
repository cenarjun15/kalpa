// ============================================================================
// TerrainGenerator.cs
// ----------------------------------------------------------------------------
// Procedurally fills a chunk with terrain using layered Perlin noise.
// Layer 1: broad hills (low frequency)
// Layer 2: small bumps  (high frequency)
// Combined and shifted around sea level.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>
    /// Simple layered-noise terrain generator.
    /// Deterministic for a given seed — a chunk always regenerates identically.
    /// </summary>
    public sealed class TerrainGenerator
    {
        // Cached block IDs (looked up once at construction).
        private readonly byte grassId;
        private readonly byte dirtId;
        private readonly byte stoneId;

        private readonly float noiseOffset;

        // Noise tuning.
        private const float LowFreq = 0.015f;
        private const float HighFreq = 0.08f;
        private const int TerrainAmplitude = 24;
        private const int DirtLayerDepth = 3;

        public TerrainGenerator(int seed, BlockRegistry registry)
        {
            grassId = registry.GetByName("kalpa:grass")?.Id ?? 1;
            dirtId  = registry.GetByName("kalpa:dirt")?.Id  ?? 2;
            stoneId = registry.GetByName("kalpa:stone")?.Id ?? 3;

            // Convert seed to a stable noise offset that keeps sampling in a
            // safe positive range for Mathf.PerlinNoise (which repeats at 256).
            System.Random rng = new System.Random(seed);
            noiseOffset = (float)(rng.NextDouble() * 10_000);
        }

        /// <summary>Fill the chunk with terrain in place.</summary>
        public void Generate(Chunk chunk)
        {
            int originX = chunk.Coordinate.WorldOriginX;
            int originZ = chunk.Coordinate.WorldOriginZ;

            for (int lx = 0; lx < GameConstants.ChunkSize; lx++)
            for (int lz = 0; lz < GameConstants.ChunkSize; lz++)
            {
                int wx = originX + lx;
                int wz = originZ + lz;

                int height = GetHeightAt(wx, wz);

                for (int y = 0; y <= height && y < GameConstants.ChunkHeight; y++)
                {
                    byte id;
                    if (y == height) id = grassId;
                    else if (y >= height - DirtLayerDepth) id = dirtId;
                    else id = stoneId;

                    chunk.SetBlockLocal(lx, y, lz, id);
                }
            }
        }

        /// <summary>Compute the surface height at world (x, z).</summary>
        private int GetHeightAt(int x, int z)
        {
            float fx = x + noiseOffset;
            float fz = z + noiseOffset;

            // Two layers of Perlin noise combined for interesting terrain.
            float low  = Mathf.PerlinNoise(fx * LowFreq,  fz * LowFreq);
            float high = Mathf.PerlinNoise(fx * HighFreq, fz * HighFreq) * 0.3f;

            float combined = Mathf.Clamp01((low + high) / 1.3f);

            int height = GameConstants.SeaLevel + Mathf.RoundToInt((combined - 0.5f) * TerrainAmplitude);
            return Mathf.Clamp(height, 1, GameConstants.ChunkHeight - 2);
        }
    }
}
