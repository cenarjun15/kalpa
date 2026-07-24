// ============================================================================
// ChunkManager.cs  (Phase 8 — streaming world)
// ----------------------------------------------------------------------------
// No longer generates a fixed grid. Instead:
//   * Reads/creates a lightweight header (seed + player pose).
//   * Force-loads a small area around spawn synchronously so the player has
//     ground beneath them immediately.
//   * Hands ongoing load/unload to ChunkStreamer, ticked every frame.
//   * Creates/destroys ChunkRenderers in response to ChunkAdded / ChunkRemoved.
//   * Rebuilds a chunk's neighbours when it loads so border faces cull correctly.
//
// Persistence for streaming worlds:
//   * header.json  — seed + player pose (written by SaveWorld()).
//   * region/*.chunk — individual modified chunks (written by the streamer).
//   The Phase 4 monolithic chunks.bin is NOT used for streaming worlds.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kalpa.Core;
using Kalpa.SaveSystem;
using Kalpa.Utils;
using UnityEngine;

namespace Kalpa.World
{
    public sealed class ChunkManager : MonoBehaviour
    {
        [Header("Streaming")]
        [SerializeField, Range(2, 10)] private int loadRadius = 5;
        [SerializeField, Range(3, 14)] private int unloadRadius = 7;
        [SerializeField, Range(1, 8)]  private int opsPerFrame = 2;
        [SerializeField, Range(1, 4)]  private int spawnPreloadRadius = 2;

        [Header("World defaults (if no WorldSession present)")]
        [SerializeField] private int fallbackSeed = 42;
        [SerializeField] private string fallbackWorldName = "MyWorld";

        [Header("Trees")]
        [SerializeField] private bool generateTrees = true;
        [SerializeField, Range(0f, 1f)] private float treeDensity = 0.12f;
        [SerializeField, Range(3, 8)] private int treeMinHeight = 4;
        [SerializeField, Range(4, 12)] private int treeMaxHeight = 6;
        [SerializeField, Range(1, 4)] private int treeCanopyRadius = 2;

        // --------------------------------------------------------------------
        // Runtime
        // --------------------------------------------------------------------

        private readonly ChunkMeshBuilder meshBuilder = new ChunkMeshBuilder();
        private readonly Dictionary<ChunkCoordinate, ChunkRenderer> renderers
            = new Dictionary<ChunkCoordinate, ChunkRenderer>();

        private VoxelWorld world;
        private ChunkStreamer streamer;
        private ChunkStore store;
        private string worldName;
        private int seed;
        private bool ready;

        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Start() => StartCoroutine(BootstrapWorld());

        private void OnDestroy()
        {
            if (world != null)
            {
                world.ChunkAdded -= OnChunkAdded;
                world.ChunkRemoved -= OnChunkRemoved;
            }
        }

        private void Update()
        {
            if (ready) streamer.Tick();
        }

        // --------------------------------------------------------------------
        // Bootstrap
        // --------------------------------------------------------------------

        private IEnumerator BootstrapWorld()
        {
            yield return null;

            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[ChunkManager] No GameManager!"); yield break; }
            if (gm.BlockRegistry.Count == 0)
            {
                Debug.LogError("[ChunkManager] BlockRegistry empty — no BlockData assets.");
                yield break;
            }

            world = gm.World;
            world.ChunkAdded += OnChunkAdded;
            world.ChunkRemoved += OnChunkRemoved;

            // Resolve session.
            var session = WorldSession.Instance;
            worldName = session != null ? session.WorldName : fallbackWorldName;
            seed      = session != null ? session.Seed : fallbackSeed;

            // Load header (seed + player pose) if it exists.
            var header = LoadHeader(worldName);
            bool loaded = header != null;
            if (loaded)
            {
                seed = header.Seed;
                if (session != null) session.BeginLoadedWorld(header);
            }
            else if (session != null)
            {
                session.BeginNewWorld(worldName, seed);
            }

            // Build generators + store + streamer.
            store = new ChunkStore(worldName);

            var terrain = new TerrainGenerator(seed, gm.BlockRegistry);
            TreeGenerator trees = null;
            if (generateTrees)
            {
                trees = new TreeGenerator(seed, gm.BlockRegistry, new TreeGenerator.Settings
                {
                    Density = treeDensity,
                    MinTrunkHeight = treeMinHeight,
                    MaxTrunkHeight = Mathf.Max(treeMinHeight, treeMaxHeight),
                    CanopyRadius = treeCanopyRadius,
                }, terrain);
            }

            var player = Object.FindFirstObjectByType<Player.PlayerController>();
            Transform playerTf = player != null ? player.transform : null;

            streamer = new ChunkStreamer(world, terrain, trees, store, playerTf)
            {
                LoadRadius = loadRadius,
                UnloadRadius = unloadRadius,
                OpsPerFrame = opsPerFrame,
            };

            // Position player before preloading so spawn area centres on them.
            if (player != null && loaded)
                ApplyPlayerPose(player, header);

            // Force-load spawn area synchronously so the player lands on solid ground.
            PreloadSpawn(playerTf);

            ready = true;
            Debug.Log($"[ChunkManager] Streaming ready — world '{worldName}', seed {seed}, " +
                      $"{world.ChunkCount} chunks preloaded.");
        }

        // --------------------------------------------------------------------
        // Spawn preload — load a small area immediately (no frame budget).
        // --------------------------------------------------------------------

        private void PreloadSpawn(Transform playerTf)
        {
            int cx = 0, cz = 0;
            if (playerTf != null)
            {
                cx = Mathf.FloorToInt(playerTf.position.x / GameConstants.ChunkSize);
                cz = Mathf.FloorToInt(playerTf.position.z / GameConstants.ChunkSize);
            }

            var gm = GameManager.Instance;
            var terrain = new TerrainGenerator(seed, gm.BlockRegistry);
            TreeGenerator trees = null;
            if (generateTrees)
                trees = new TreeGenerator(seed, gm.BlockRegistry, new TreeGenerator.Settings
                {
                    Density = treeDensity,
                    MinTrunkHeight = treeMinHeight,
                    MaxTrunkHeight = Mathf.Max(treeMinHeight, treeMaxHeight),
                    CanopyRadius = treeCanopyRadius,
                }, terrain);

            for (int dx = -spawnPreloadRadius; dx <= spawnPreloadRadius; dx++)
            for (int dz = -spawnPreloadRadius; dz <= spawnPreloadRadius; dz++)
            {
                var coord = new ChunkCoordinate(cx + dx, cz + dz);
                if (world.HasChunk(coord)) continue;

                Chunk chunk = (store != null && store.Exists(coord))
                    ? store.Load(coord)
                    : null;

                if (chunk == null)
                {
                    chunk = new Chunk(coord);
                    terrain.Generate(chunk);
                    trees?.Generate(chunk, world);
                }
                world.AddChunk(chunk);
            }

            // Rebuild all preloaded renderers now.
            foreach (var kv in renderers) kv.Value.Rebuild();
        }

        // --------------------------------------------------------------------
        // Renderer lifecycle
        // --------------------------------------------------------------------

        private void OnChunkAdded(Chunk chunk)
        {
            if (!renderers.ContainsKey(chunk.Coordinate))
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<ChunkRenderer>();
                var gm = GameManager.Instance;
                renderer.Initialise(chunk, world, gm.BlockRegistry, gm.MaterialCache,
                                    meshBuilder, gm.TextureAtlas);
                renderers[chunk.Coordinate] = renderer;
            }

            // Rebuild this chunk + neighbours (so border faces cull correctly).
            if (ready)
            {
                RebuildAt(chunk.Coordinate);
                RebuildAt(new ChunkCoordinate(chunk.Coordinate.X + 1, chunk.Coordinate.Z));
                RebuildAt(new ChunkCoordinate(chunk.Coordinate.X - 1, chunk.Coordinate.Z));
                RebuildAt(new ChunkCoordinate(chunk.Coordinate.X, chunk.Coordinate.Z + 1));
                RebuildAt(new ChunkCoordinate(chunk.Coordinate.X, chunk.Coordinate.Z - 1));
            }
        }

        private void OnChunkRemoved(Chunk chunk)
        {
            if (renderers.TryGetValue(chunk.Coordinate, out var renderer))
            {
                renderers.Remove(chunk.Coordinate);
                if (renderer != null) Destroy(renderer.gameObject);
            }
        }

        private void RebuildAt(ChunkCoordinate coord)
        {
            if (renderers.TryGetValue(coord, out var r) && r != null)
                r.Rebuild();
        }

        // --------------------------------------------------------------------
        // Public: chunk rebuild used by PlayerController when editing blocks
        // --------------------------------------------------------------------

        public ChunkRenderer GetRenderer(ChunkCoordinate coord)
            => renderers.TryGetValue(coord, out var r) ? r : null;

        public int RendererCount => renderers.Count;

        // --------------------------------------------------------------------
        // Save / header IO (lightweight — seed + player pose only)
        // --------------------------------------------------------------------

        /// <summary>Save modified chunks + header. Called by WorldSession.</summary>
        public void SaveWorld(Vector3 playerPos, float yaw, float pitch)
        {
            if (!ready) return;

            streamer.SaveAllModified();
            SaveHeader(playerPos, yaw, pitch);
            Debug.Log($"[ChunkManager] Streaming world '{worldName}' saved.");
        }

        private void SaveHeader(Vector3 playerPos, float yaw, float pitch)
        {
            var header = new WorldSaveHeader
            {
                FormatVersion = GameConstants.SaveFormatVersion,
                GameVersion = GameConstants.GameVersion,
                WorldName = worldName,
                Seed = seed,
                LastSavedUtc = System.DateTime.UtcNow.ToString("o"),
                PlayerX = playerPos.x,
                PlayerY = playerPos.y,
                PlayerZ = playerPos.z,
                PlayerYaw = yaw,
                PlayerPitch = pitch,
            };

            var path = WorldSaveIO.HeaderPath(worldName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(header, true), Encoding.UTF8);
        }

        private static WorldSaveHeader LoadHeader(string worldName)
        {
            var path = WorldSaveIO.HeaderPath(worldName);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<WorldSaveHeader>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return null; }
        }

        private void ApplyPlayerPose(Player.PlayerController pc, WorldSaveHeader header)
        {
            pc.transform.position = new Vector3(header.PlayerX, header.PlayerY, header.PlayerZ);
            var e = pc.transform.eulerAngles;
            pc.transform.eulerAngles = new Vector3(e.x, header.PlayerYaw, e.z);
            var cam = pc.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                var lr = cam.transform.localEulerAngles;
                cam.transform.localEulerAngles = new Vector3(header.PlayerPitch, lr.y, lr.z);
            }
        }
    }
}
