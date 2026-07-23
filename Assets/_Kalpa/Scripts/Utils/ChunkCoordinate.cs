// ============================================================================
// ChunkCoordinate.cs
// ----------------------------------------------------------------------------
// Immutable (chunkX, chunkZ) coordinate identifying a chunk in the world.
// A chunk covers ChunkSize × ChunkHeight × ChunkSize blocks.
// Used as a Dictionary key, so hashing must be fast and well-distributed.
// ============================================================================

using System;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.Utils
{
    /// <summary>
    /// Identifies a chunk column in the world.
    /// Y is not part of the coordinate because chunks span the full world height.
    /// </summary>
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public readonly int X;
        public readonly int Z;

        public ChunkCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        // --------------------------------------------------------------------
        // Conversions between block-space and chunk-space
        // --------------------------------------------------------------------

        /// <summary>
        /// Get the chunk that contains a given world-space block position.
        /// Uses floor division so negative coordinates work correctly.
        /// </summary>
        public static ChunkCoordinate FromWorldXZ(int worldX, int worldZ)
        {
            int size = GameConstants.ChunkSize;
            return new ChunkCoordinate(
                Mathf.FloorToInt((float)worldX / size),
                Mathf.FloorToInt((float)worldZ / size));
        }

        /// <summary>World-space block X where this chunk starts.</summary>
        public int WorldOriginX => X * GameConstants.ChunkSize;

        /// <summary>World-space block Z where this chunk starts.</summary>
        public int WorldOriginZ => Z * GameConstants.ChunkSize;

        // --------------------------------------------------------------------
        // Equality & hashing
        // --------------------------------------------------------------------

        public bool Equals(ChunkCoordinate other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is ChunkCoordinate c && Equals(c);

        public override int GetHashCode()
        {
            unchecked
            {
                // Szudzik pairing-style combine — good spread for small ints.
                return (X * 73856093) ^ (Z * 19349663);
            }
        }

        public static bool operator ==(ChunkCoordinate a, ChunkCoordinate b) => a.Equals(b);
        public static bool operator !=(ChunkCoordinate a, ChunkCoordinate b) => !a.Equals(b);

        public override string ToString() => $"Chunk({X}, {Z})";
    }
}
