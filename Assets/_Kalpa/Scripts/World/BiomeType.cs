// ============================================================================
// BiomeType.cs
// ----------------------------------------------------------------------------
// Enumerates the biomes and holds a small data record describing each one:
// surface block, sub-surface block, tree density, terrain roughness, etc.
// Biomes are chosen by a low-frequency noise field so they form large,
// coherent regions rather than noisy speckle.
// ============================================================================

namespace Kalpa.World
{
    public enum BiomeType
    {
        Plains = 0,
        Forest,
        Desert,
        Snow,
    }

    /// <summary>Per-biome generation parameters.</summary>
    public struct BiomeSettings
    {
        public BiomeType Type;

        public string SurfaceBlock;     // top block
        public string SubSurfaceBlock;  // just below surface
        public string DeepBlock;        // deep fill

        public float HeightScale;       // vertical amplitude multiplier
        public float TreeDensity;       // 0..1
        public bool AllowTrees;

        public static BiomeSettings For(BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Desert:
                    return new BiomeSettings
                    {
                        Type = type,
                        SurfaceBlock = "kalpa:sand",
                        SubSurfaceBlock = "kalpa:sand",
                        DeepBlock = "kalpa:stone",
                        HeightScale = 0.6f,
                        TreeDensity = 0.0f,
                        AllowTrees = false,
                    };

                case BiomeType.Snow:
                    return new BiomeSettings
                    {
                        Type = type,
                        SurfaceBlock = "kalpa:marble",   // stand-in "snow" (white marble)
                        SubSurfaceBlock = "kalpa:dirt",
                        DeepBlock = "kalpa:stone",
                        HeightScale = 1.4f,              // taller, mountainous
                        TreeDensity = 0.06f,
                        AllowTrees = true,
                    };

                case BiomeType.Forest:
                    return new BiomeSettings
                    {
                        Type = type,
                        SurfaceBlock = "kalpa:grass",
                        SubSurfaceBlock = "kalpa:dirt",
                        DeepBlock = "kalpa:stone",
                        HeightScale = 1.0f,
                        TreeDensity = 0.22f,             // dense
                        AllowTrees = true,
                    };

                case BiomeType.Plains:
                default:
                    return new BiomeSettings
                    {
                        Type = BiomeType.Plains,
                        SurfaceBlock = "kalpa:grass",
                        SubSurfaceBlock = "kalpa:dirt",
                        DeepBlock = "kalpa:stone",
                        HeightScale = 0.8f,
                        TreeDensity = 0.05f,             // sparse
                        AllowTrees = true,
                    };
            }
        }
    }
}
