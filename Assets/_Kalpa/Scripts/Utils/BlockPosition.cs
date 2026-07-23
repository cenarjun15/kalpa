// ============================================================================
// BlockPosition.cs
// ----------------------------------------------------------------------------
// Immutable integer 3D coordinate used to identify block positions in the world.
// Using our own struct (instead of Unity's Vector3Int) gives us more control
// over hashing and equality, which matters when we use it as a Dictionary key.
// ============================================================================

using System;

namespace Kalpa.Utils
{
    /// <summary>
    /// Immutable integer 3D coordinate representing a block position.
    /// Value type — no GC pressure when used as a dictionary key.
    /// </summary>
    public readonly struct BlockPosition : IEquatable<BlockPosition>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public BlockPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // --------------------------------------------------------------------
        // Common positions
        // --------------------------------------------------------------------

        public static BlockPosition Zero => new BlockPosition(0, 0, 0);
        public static BlockPosition Up => new BlockPosition(0, 1, 0);
        public static BlockPosition Down => new BlockPosition(0, -1, 0);
        public static BlockPosition Left => new BlockPosition(-1, 0, 0);
        public static BlockPosition Right => new BlockPosition(1, 0, 0);
        public static BlockPosition Forward => new BlockPosition(0, 0, 1);
        public static BlockPosition Back => new BlockPosition(0, 0, -1);

        // --------------------------------------------------------------------
        // Equality & Hashing (critical for Dictionary performance)
        // --------------------------------------------------------------------

        public bool Equals(BlockPosition other) => X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) => obj is BlockPosition other && Equals(other);

        public override int GetHashCode()
        {
            // Cantor-pair-style hash — good distribution for typical block coordinates.
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Y;
                hash = hash * 31 + Z;
                return hash;
            }
        }

        public static bool operator ==(BlockPosition a, BlockPosition b) => a.Equals(b);
        public static bool operator !=(BlockPosition a, BlockPosition b) => !a.Equals(b);

        // --------------------------------------------------------------------
        // Arithmetic
        // --------------------------------------------------------------------

        public static BlockPosition operator +(BlockPosition a, BlockPosition b)
            => new BlockPosition(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static BlockPosition operator -(BlockPosition a, BlockPosition b)
            => new BlockPosition(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        // --------------------------------------------------------------------
        // Interop with Unity's Vector3 / Vector3Int
        // --------------------------------------------------------------------

        public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(X, Y, Z);
        public UnityEngine.Vector3Int ToVector3Int() => new UnityEngine.Vector3Int(X, Y, Z);

        public static BlockPosition FromVector3(UnityEngine.Vector3 v)
            => new BlockPosition(
                UnityEngine.Mathf.FloorToInt(v.x),
                UnityEngine.Mathf.FloorToInt(v.y),
                UnityEngine.Mathf.FloorToInt(v.z));

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
