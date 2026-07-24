// ============================================================================
// ChunkStreamer.cs
// ----------------------------------------------------------------------------
// Dynamically loads chunks around the player and unloads distant ones, giving
// an effectively infinite world.
//
// Each frame (throttled):
//   1. Determine the player's current chunk.
//   2. Build the set of chunk coords that SHOULD be loaded (a square of
//      radius LoadRadius around the player).
//   3. Queue any missing chunks for loading.
//   4. Queue any loaded chunks outside UnloadRadius for unloading.
//   5. Process a bounded number of load/unload ops per frame (budgeted) so
//      there's no frame hitch when crossing chunk borders.
//
// Loading a chunk:
//   * If ChunkStore has a modified version on disk → load that.
//   * Else → generate terrain (+ trees) procedurally from the seed.
//
// Unloading a chunk:
//   * If chunk.IsModified → save via ChunkStore first.
//   * Then remove from world (fires ChunkRemoved → renderer destroyed).
// ============================================================================

using System.Collections.Generic;
using Kalpa.Core;
using Kalpa.SaveSystem;
using Kalpa.Utils;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>
    /// Streams chunks in/out around a target transform (the player).
    /// Driven by ChunkManager after the world systems are ready.
    /// </summary>
    public sealed class ChunkStreamer
    {
        // --------------------------------------------------------------------
        // Config
        // --------------------------------------------------------------------

        public int LoadRadius   { get; set; } = 5;   // chunks loaded around player
        public int UnloadRadius { get; set; } = 7;   // chunks beyond this are unloaded
        public int OpsPerFrame  { get; set; } = 2;   // load/unload actions per frame

        // --------------------------------------------------------------------
        // Dependencies
        // --------------------------------------------------------------------

        private readonly VoxelWorld world;
        private readonly TerrainGenerator terrain;
        private readonly TreeGenerator trees;   // may be null if trees disabled
        private readonly ChunkStore store;
        private readonly Transform player;

        // --------------------------------------------------------------------
        // Work queues
        // --------------------------------------------------------------------

        private readonly Queue<ChunkCoordinate> loadQueue = new Queue<ChunkCoordinate>();
        private readonly HashSet<ChunkCoordinate> queuedForLoad = new HashSet<ChunkCoordinate>();
        private readonly List<ChunkCoordinate> unloadScratch = new List<ChunkCoordinate>();

        private ChunkCoordinate lastPlayerChunk;
        private bool hasLastChunk;

        public ChunkStreamer(VoxelWorld world, TerrainGenerator terrain,
                             TreeGenerator trees, ChunkStore store, Transform player)
        {
            this.world = world;
            this.terrain = terrain;
            this.trees = trees;
            this.store = store;
            this.player = player;
        }

        // --------------------------------------------------------------------
        // Per-frame tick — call from a MonoBehaviour.Update.
        // --------------------------------------------------------------------

        public void Tick()
        {
            if (player == null) return;

            var playerChunk = ChunkCoordinate.FromWorldXZ(
                Mathf.FloorToInt(player.position.x),
                Mathf.FloorToInt(player.position.z));

            // Only recompute desired-set when the player crosses into a new chunk.
            if (!hasLastChunk || !playerChunk.Equals(lastPlayerChunk))
            {
                lastPlayerChunk = playerChunk;
                hasLastChunk = true;
                RefreshQueues(playerChunk);
            }

            ProcessQueues();
        }

        // --------------------------------------------------------------------
        // Desired-set computation
        // --------------------------------------------------------------------

        private void RefreshQueues(ChunkCoordinate center)
        {
            // 1. Enqueue missing chunks within LoadRadius, nearest-first.
            var wanted = new List<ChunkCoordinate>();
            for (int dx = -LoadRadius; dx <= LoadRadius; dx++)
            for (int dz = -LoadRadius; dz <= LoadRadius; dz++)
            {
                var c = new ChunkCoordinate(center.X + dx, center.Z + dz);
                if (!world.HasChunk(c) && !queuedForLoad.Contains(c))
                    wanted.Add(c);
            }

            // Sort nearest-first so the world fills in around the player.
            wanted.Sort((a, b) =>
                ManhattanTo(center, a).CompareTo(ManhattanTo(center, b)));

            foreach (var c in wanted)
            {
                loadQueue.Enqueue(c);
                queuedForLoad.Add(c);
            }

            // 2. Unload chunks beyond UnloadRadius.
            unloadScratch.Clear();
            foreach (var chunk in world.AllChunks)
            {
                var c = chunk.Coordinate;
                int dist = Mathf.Max(Mathf.Abs(c.X - center.X), Mathf.Abs(c.Z - center.Z));
                if (dist > UnloadRadius)
                    unloadScratch.Add(c);
            }

            foreach (var c in unloadScratch)
                UnloadChunk(c);
        }

        private static int ManhattanTo(ChunkCoordinate a, ChunkCoordinate b)
            => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Z - b.Z);

        // --------------------------------------------------------------------
        // Bounded processing
        // --------------------------------------------------------------------

        private void ProcessQueues()
        {
            int budget = OpsPerFrame;
            while (budget-- > 0 && loadQueue.Count > 0)
            {
                var c = loadQueue.Dequeue();
                queuedForLoad.Remove(c);
                if (!world.HasChunk(c))
                    LoadChunk(c);
            }
        }

        // --------------------------------------------------------------------
        // Load / unload
        // --------------------------------------------------------------------

        private void LoadChunk(ChunkCoordinate coord)
        {
            Chunk chunk;

            // Prefer a saved (player-modified) chunk from disk.
            if (store != null && store.Exists(coord))
            {
                chunk = store.Load(coord);
                if (chunk == null)
                    chunk = GenerateChunk(coord);
            }
            else
            {
                chunk = GenerateChunk(coord);
            }

            world.AddChunk(chunk); // fires ChunkAdded → renderer created + will rebuild
        }

        private Chunk GenerateChunk(ChunkCoordinate coord)
        {
            var chunk = new Chunk(coord);
            terrain.Generate(chunk);
            // Trees are generated per-chunk; canopies that spill into not-yet-loaded
            // neighbours are simply skipped (world.GetBlock returns air there).
            trees?.Generate(chunk, world);
            return chunk;
        }

        private void UnloadChunk(ChunkCoordinate coord)
        {
            var chunk = world.GetChunk(coord);
            if (chunk == null) return;

            if (chunk.IsModified && store != null)
                store.Save(chunk);

            world.RemoveChunk(coord); // fires ChunkRemoved → renderer destroyed
        }

        // --------------------------------------------------------------------
        // Save everything currently loaded + modified (called on quit / manual save)
        // --------------------------------------------------------------------

        public void SaveAllModified()
        {
            if (store == null) return;
            foreach (var chunk in world.AllChunks)
                if (chunk.IsModified)
                    store.Save(chunk);
        }
    }
}
