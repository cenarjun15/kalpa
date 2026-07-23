// ============================================================================
// WorldSaveData.cs
// ----------------------------------------------------------------------------
// Plain-old-data structures describing a saved world.
// Kept deliberately simple so the on-disk format is easy to reason about and
// forward-compatible via a version field.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Kalpa.SaveSystem
{
    /// <summary>
    /// Top-level metadata for a saved world.
    /// Written as JSON alongside the binary chunk data (see WorldSaveIO).
    /// </summary>
    [Serializable]
    public sealed class WorldSaveHeader
    {
        /// <summary>Save format version. Bump when the binary layout changes.</summary>
        public int FormatVersion;

        /// <summary>Semver of the game that produced this save.</summary>
        public string GameVersion;

        /// <summary>World display name (user-facing).</summary>
        public string WorldName;

        /// <summary>RNG seed used for procedural generation.</summary>
        public int Seed;

        /// <summary>UTC timestamp of last save, ISO-8601.</summary>
        public string LastSavedUtc;

        /// <summary>Player spawn / last-known position.</summary>
        public float PlayerX;
        public float PlayerY;
        public float PlayerZ;
        public float PlayerYaw;
        public float PlayerPitch;

        /// <summary>Which chunks are in this save (parallel to chunk files).</summary>
        public List<ChunkIndexEntry> Chunks = new List<ChunkIndexEntry>();
    }

    /// <summary>One entry per saved chunk. Used to build the load manifest.</summary>
    [Serializable]
    public sealed class ChunkIndexEntry
    {
        public int X;
        public int Z;
        public int ByteOffset;  // offset into the .chunks binary blob
        public int ByteLength;  // length of this chunk's data
    }
}
