// ============================================================================
// ChunkStore.cs
// ----------------------------------------------------------------------------
// Per-chunk disk persistence for streaming worlds.
//
// Unlike the Phase 4 monolithic world save (one big chunks.bin), streaming needs
// to save/load INDIVIDUAL chunks on demand as the player moves. Each modified
// chunk is written as its own small file:
//
//   <persistentData>/Saves/<WorldName>/region/<x>_<z>.chunk
//
// Only chunks the player actually MODIFIED are stored. Untouched procedural
// chunks are never written — they regenerate identically from the seed, saving
// enormous disk space for large explored areas.
// ============================================================================

using System;
using System.IO;
using Kalpa.Core;
using Kalpa.Utils;
using UnityEngine;

namespace Kalpa.SaveSystem
{
    /// <summary>
    /// Reads and writes individual chunks to disk for a given world.
    /// </summary>
    public sealed class ChunkStore
    {
        private readonly string regionFolder;

        public ChunkStore(string worldName)
        {
            regionFolder = Path.Combine(WorldSaveIO.WorldFolder(worldName), "region");
            Directory.CreateDirectory(regionFolder);
        }

        private string PathFor(ChunkCoordinate c)
            => Path.Combine(regionFolder, $"{c.X}_{c.Z}.chunk");

        /// <summary>True if a modified chunk exists on disk for this coordinate.</summary>
        public bool Exists(ChunkCoordinate c) => File.Exists(PathFor(c));

        // --------------------------------------------------------------------
        // Save
        // --------------------------------------------------------------------

        /// <summary>
        /// Write a chunk's block data to its own file.
        /// Layout: [blocks: ChunkSize*ChunkSize*ChunkHeight bytes]  (coord is in filename)
        /// </summary>
        public void Save(Kalpa.World.Chunk chunk)
        {
            var path = PathFor(chunk.Coordinate);
            var tmp = path + ".tmp";

            try
            {
                File.WriteAllBytes(tmp, chunk.RawBlocks);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ChunkStore] Failed to save chunk {chunk.Coordinate}: {e.Message}");
            }
        }

        // --------------------------------------------------------------------
        // Load
        // --------------------------------------------------------------------

        /// <summary>
        /// Load a chunk from disk into a fresh Chunk instance.
        /// Returns null if no file exists (caller should generate procedurally).
        /// </summary>
        public Kalpa.World.Chunk Load(ChunkCoordinate coord)
        {
            var path = PathFor(coord);
            if (!File.Exists(path)) return null;

            int expected = GameConstants.ChunkSize * GameConstants.ChunkSize * GameConstants.ChunkHeight;

            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length != expected)
                {
                    Debug.LogError($"[ChunkStore] Chunk {coord} wrong size ({bytes.Length}, expected {expected}). Ignoring.");
                    return null;
                }

                var chunk = new Kalpa.World.Chunk(coord);
                Buffer.BlockCopy(bytes, 0, chunk.RawBlocks, 0, expected);
                chunk.IsDirty = true;
                chunk.IsModified = true; // it was modified once; keep persisting
                return chunk;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ChunkStore] Failed to load chunk {coord}: {e.Message}");
                return null;
            }
        }
    }
}
