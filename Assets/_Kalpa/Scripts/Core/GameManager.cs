// ============================================================================
// GameManager.cs  (Phase 11 — builds cutout leaf material)
// ----------------------------------------------------------------------------
// Boot order:
//   1. BlockRegistry
//   2. VoxelWorld
//   3. BlockTextureAtlas (opaque + transparent share this)
//   4. Generate leaf cutout texture → BlockMaterialCache builds all 3 materials
// ============================================================================

using Kalpa.Blocks;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public BlockRegistry      BlockRegistry { get; private set; }
        public VoxelWorld         World         { get; private set; }
        public BlockMaterialCache MaterialCache { get; private set; }
        public BlockTextureAtlas  TextureAtlas  { get; private set; }

        [Header("Startup")]
        [SerializeField] private bool verboseLogging = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Foliage")]
        [Tooltip("Seed for the procedurally-generated leaf cutout texture.")]
        [SerializeField] private int leafTextureSeed = 1337;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance — destroying self.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            InitialiseSystems();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitialiseSystems()
        {
            Log($"Booting {GameConstants.GameName} v{GameConstants.GameVersion} …");

            BlockRegistry = new BlockRegistry();
            int blockCount = BlockRegistry.LoadFromResources();
            Log($"BlockRegistry ready — {blockCount} block types.");

            World = new VoxelWorld(BlockRegistry);
            Log("VoxelWorld ready — empty.");

            TextureAtlas = new BlockTextureAtlas();
            TextureAtlas.Build(BlockRegistry);
            Log("BlockTextureAtlas built.");

            MaterialCache = new BlockMaterialCache();
            MaterialCache.BuildAtlasMaterial(TextureAtlas.Atlas);

            // Generate leaf cutout texture + build the cutout material.
            var leafTex = LeafTextureGenerator.Generate(64, leafTextureSeed);
            MaterialCache.BuildCutoutMaterial(leafTex);
            Log("BlockMaterialCache ready (opaque + transparent + cutout).");

            Log("Boot complete.");
        }

        private void Log(string message)
        {
            if (verboseLogging) Debug.Log($"[GameManager] {message}");
        }
    }
}
