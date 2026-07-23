// ============================================================================
// ChunkManager.cs  (Phase 5A update — passes atlas into ChunkRenderer)
// ----------------------------------------------------------------------------
// Same responsibilities as Phase 4A, plus: hands the atlas to each renderer.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using Kalpa.Core;
using Kalpa.SaveSystem;
using Kalpa.Utils;
using UnityEngine;

namespace Kalpa.World
{
    public sealed class ChunkManager : MonoBehaviour
    {
        [Header("World Setup")]

        [SerializeField, Range(1, 8)] private int worldRadiusChunks = 3;
        [SerializeField] private int fallbackSeed = 42;
        [SerializeField] private string fallbackWorldName = "MyWorld";

        private readonly ChunkMeshBuilder meshBuilder = new ChunkMeshBuilder();
        private readonly Dictionary<ChunkCoordinate, ChunkRenderer> renderers
            = new Dictionary<ChunkCoordinate, ChunkRenderer>();

        private VoxelWorld world;

        private void Start()
        {
            StartCoroutine(BootstrapWorld());
        }

        private void OnDestroy()
        {
            if (world != null) world.ChunkAdded -= OnChunkAdded;
        }

        private IEnumerator BootstrapWorld()
        {
            yield return null;

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[ChunkManager] No GameManager in scene!");
                yield break;
            }
            if (gm.BlockRegistry.Count == 0)
            {
                Debug.LogError("[ChunkManager] BlockRegistry is empty — no BlockData assets found.");
                yield break;
            }

            world = gm.World;
            world.ChunkAdded += OnChunkAdded;

            var session = WorldSession.Instance;
            string worldName;
            int seed;

            if (session != null)
            {
                worldName = session.WorldName;
                seed = session.Seed;
            }
            else
            {
                worldName = fallbackWorldName;
                seed = fallbackSeed;
                Debug.LogWarning("[ChunkManager] No WorldSession in scene; using inspector fallbacks.");
            }

            bool loadSucceeded = false;
            WorldSaveHeader loadedHeader = null;

            if (WorldSaveIO.WorldExists(worldName))
            {
                Debug.Log($"[ChunkManager] Loading saved world '{worldName}'…");
                try
                {
                    loadedHeader = WorldSaveIO.Load(worldName, world);
                    loadSucceeded = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ChunkManager] Load failed: {e.Message}. Generating fresh world instead.");
                    loadSucceeded = false;
                }
            }

            if (loadSucceeded)
            {
                if (session != null) session.BeginLoadedWorld(loadedHeader);
                foreach (var kv in renderers) kv.Value.Rebuild();
                ApplyPlayerPose(loadedHeader);
            }
            else
            {
                yield return GenerateFreshWorld(gm, seed);
            }

            Debug.Log($"[ChunkManager] World ready — {world.ChunkCount} chunks in world.");
        }

        private IEnumerator GenerateFreshWorld(GameManager gm, int seed)
        {
            var terrain = new TerrainGenerator(seed, gm.BlockRegistry);

            for (int cx = -worldRadiusChunks; cx <= worldRadiusChunks; cx++)
            for (int cz = -worldRadiusChunks; cz <= worldRadiusChunks; cz++)
            {
                var coord = new ChunkCoordinate(cx, cz);
                var chunk = new Chunk(coord);
                terrain.Generate(chunk);
                world.AddChunk(chunk);
            }

            yield return null;

            foreach (var kv in renderers) kv.Value.Rebuild();
        }

        private void ApplyPlayerPose(WorldSaveHeader header)
        {
            var pc = Object.FindFirstObjectByType<Player.PlayerController>();
            if (pc == null) return;

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

        private void OnChunkAdded(Chunk chunk)
        {
            if (renderers.ContainsKey(chunk.Coordinate)) return;

            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<ChunkRenderer>();
            var gm = GameManager.Instance;
            renderer.Initialise(chunk, world, gm.BlockRegistry, gm.MaterialCache,
                                meshBuilder, gm.TextureAtlas);

            renderers[chunk.Coordinate] = renderer;
        }

        public ChunkRenderer GetRenderer(ChunkCoordinate coord)
            => renderers.TryGetValue(coord, out var r) ? r : null;

        public int RendererCount => renderers.Count;
    }
}
