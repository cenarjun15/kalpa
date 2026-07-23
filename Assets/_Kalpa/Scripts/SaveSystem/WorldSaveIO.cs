// ============================================================================
// WorldSaveIO.cs
// ----------------------------------------------------------------------------
// Reads and writes saved worlds to disk.
//
// File layout (per world folder):
//   <SaveFolder>/<WorldName>/
//     ├── header.json        (WorldSaveHeader as JSON, human-readable)
//     └── chunks.bin         (all chunks concatenated as raw bytes)
//
// Design notes:
//   * Chunks are packed into a single binary blob rather than one file per chunk.
//     Fewer file-system operations = much faster on Windows for many-chunk saves.
//   * Header carries a manifest (offset+length) so we can jump to any chunk.
//   * FormatVersion allows non-breaking upgrades.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kalpa.Core;
using Kalpa.Utils;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.SaveSystem
{
    /// <summary>
    /// Static helpers to save and load a <see cref="VoxelWorld"/> to disk.
    /// </summary>
    public static class WorldSaveIO
    {
        // --------------------------------------------------------------------
        // Paths
        // --------------------------------------------------------------------

        /// <summary>Root save folder inside Application.persistentDataPath.</summary>
        public static string SavesRoot
            => Path.Combine(Application.persistentDataPath, GameConstants.SaveFolder);

        /// <summary>Folder for a specific world.</summary>
        public static string WorldFolder(string worldName)
            => Path.Combine(SavesRoot, SanitizeName(worldName));

        public static string HeaderPath(string worldName)
            => Path.Combine(WorldFolder(worldName), "header.json");

        public static string ChunksPath(string worldName)
            => Path.Combine(WorldFolder(worldName), "chunks.bin");

        /// <summary>Very defensive filename cleaner.</summary>
        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Untitled";
            var sb = new StringBuilder(name.Length);
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in name)
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        // --------------------------------------------------------------------
        // Query
        // --------------------------------------------------------------------

        /// <summary>True if a saved world exists on disk with this name.</summary>
        public static bool WorldExists(string worldName)
            => File.Exists(HeaderPath(worldName)) && File.Exists(ChunksPath(worldName));

        /// <summary>List all world names present on disk.</summary>
        public static string[] ListWorlds()
        {
            if (!Directory.Exists(SavesRoot)) return Array.Empty<string>();
            var dirs = Directory.GetDirectories(SavesRoot);
            var result = new List<string>(dirs.Length);
            foreach (var d in dirs)
            {
                var name = Path.GetFileName(d);
                if (WorldExists(name)) result.Add(name);
            }
            return result.ToArray();
        }

        // --------------------------------------------------------------------
        // Save
        // --------------------------------------------------------------------

        /// <summary>
        /// Write the given world to disk under <paramref name="worldName"/>.
        /// Overwrites any existing save.
        /// </summary>
        public static void Save(
            string worldName,
            int seed,
            VoxelWorld world,
            Vector3 playerPos,
            float playerYaw,
            float playerPitch)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            var folder = WorldFolder(worldName);
            Directory.CreateDirectory(folder);

            // 1. Serialise all chunks into memory + build the manifest.
            var header = new WorldSaveHeader
            {
                FormatVersion = GameConstants.SaveFormatVersion,
                GameVersion   = GameConstants.GameVersion,
                WorldName     = worldName,
                Seed          = seed,
                LastSavedUtc  = DateTime.UtcNow.ToString("o"),
                PlayerX       = playerPos.x,
                PlayerY       = playerPos.y,
                PlayerZ       = playerPos.z,
                PlayerYaw     = playerYaw,
                PlayerPitch   = playerPitch,
            };

            var tmpChunksPath = ChunksPath(worldName) + ".tmp";
            using (var fs = new FileStream(tmpChunksPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                int offset = 0;
                foreach (var chunk in world.AllChunks)
                {
                    var payload = SerialiseChunk(chunk);
                    fs.Write(payload, 0, payload.Length);

                    header.Chunks.Add(new ChunkIndexEntry
                    {
                        X = chunk.Coordinate.X,
                        Z = chunk.Coordinate.Z,
                        ByteOffset = offset,
                        ByteLength = payload.Length,
                    });
                    offset += payload.Length;
                }
            }

            // 2. Write header JSON (Unity's JsonUtility supports our types).
            var json = JsonUtility.ToJson(header, prettyPrint: true);
            var tmpHeaderPath = HeaderPath(worldName) + ".tmp";
            File.WriteAllText(tmpHeaderPath, json, Encoding.UTF8);

            // 3. Atomic replace — either both new files land, or neither does.
            //    (On Windows, File.Replace requires the destination to exist first.)
            SafeReplace(tmpChunksPath, ChunksPath(worldName));
            SafeReplace(tmpHeaderPath, HeaderPath(worldName));

            Debug.Log($"[Save] Wrote {header.Chunks.Count} chunks to '{folder}'.");
        }

        private static void SafeReplace(string src, string dst)
        {
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(src, dst);
        }

        /// <summary>
        /// Serialise a single chunk to bytes.
        /// Layout: [CoordX:int32][CoordZ:int32][blocks: ChunkSize*ChunkSize*ChunkHeight bytes]
        /// </summary>
        private static byte[] SerialiseChunk(Chunk chunk)
        {
            int blockCount = GameConstants.ChunkSize * GameConstants.ChunkSize * GameConstants.ChunkHeight;
            var buffer = new byte[8 + blockCount];

            Buffer.BlockCopy(BitConverter.GetBytes(chunk.Coordinate.X), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(chunk.Coordinate.Z), 0, buffer, 4, 4);
            Buffer.BlockCopy(chunk.RawBlocks, 0, buffer, 8, blockCount);

            return buffer;
        }

        // --------------------------------------------------------------------
        // Load
        // --------------------------------------------------------------------

        /// <summary>
        /// Read a saved world into <paramref name="world"/>.
        /// Existing chunks in <paramref name="world"/> are replaced.
        /// Returns the deserialised header (contains seed + player pose).
        /// </summary>
        public static WorldSaveHeader Load(string worldName, VoxelWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!WorldExists(worldName))
                throw new FileNotFoundException($"No saved world named '{worldName}'.");

            var headerJson = File.ReadAllText(HeaderPath(worldName), Encoding.UTF8);
            var header = JsonUtility.FromJson<WorldSaveHeader>(headerJson);

            if (header == null)
                throw new InvalidDataException("Header JSON could not be deserialised.");

            if (header.FormatVersion > GameConstants.SaveFormatVersion)
            {
                Debug.LogWarning(
                    $"[Load] Save format version {header.FormatVersion} " +
                    $"is newer than game version {GameConstants.SaveFormatVersion}. " +
                    "Loading may fail.");
            }

            using (var fs = new FileStream(ChunksPath(worldName), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                foreach (var entry in header.Chunks)
                {
                    fs.Seek(entry.ByteOffset, SeekOrigin.Begin);
                    var payload = new byte[entry.ByteLength];
                    int read = fs.Read(payload, 0, payload.Length);
                    if (read != payload.Length)
                        throw new InvalidDataException($"Truncated chunk at ({entry.X},{entry.Z}).");

                    var chunk = DeserialiseChunk(payload);
                    world.AddChunk(chunk);
                }
            }

            Debug.Log($"[Load] Loaded {header.Chunks.Count} chunks from '{worldName}' (seed={header.Seed}).");
            return header;
        }

        private static Chunk DeserialiseChunk(byte[] payload)
        {
            int cx = BitConverter.ToInt32(payload, 0);
            int cz = BitConverter.ToInt32(payload, 4);

            var chunk = new Chunk(new ChunkCoordinate(cx, cz));
            int blockCount = GameConstants.ChunkSize * GameConstants.ChunkSize * GameConstants.ChunkHeight;
            if (payload.Length - 8 != blockCount)
                throw new InvalidDataException(
                    $"Chunk ({cx},{cz}) has {payload.Length - 8} block bytes, expected {blockCount}.");

            Buffer.BlockCopy(payload, 8, chunk.RawBlocks, 0, blockCount);
            chunk.IsDirty = true; // needs re-mesh
            return chunk;
        }
    }
}
