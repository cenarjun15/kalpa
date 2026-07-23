// ============================================================================
// GameManager.cs  (Phase 5A update — atlas support)
// ----------------------------------------------------------------------------
// Now also owns BlockTextureAtlas. Boot order matters:
//   1. BlockRegistry loads BlockData assets (with their textures).
//   2. VoxelWorld created.
//   3. BlockTextureAtlas built from registry.
//   4. BlockMaterialCache builds an atlas-material using the atlas texture.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.Core
{
    /// <summary>
    /// Top-level game controller. Placed once per gameplay scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Singleton
        // --------------------------------------------------------------------

        public static GameManager Instance { get; private set; }

        // --------------------------------------------------------------------
        // Systems
        // --------------------------------------------------------------------

        public BlockRegistry      BlockRegistry { get; private set; }
        public VoxelWorld         World         { get; private set; }
        public BlockMaterialCache MaterialCache { get; private set; }
        public BlockTextureAtlas  TextureAtlas  { get; private set; }

        // --------------------------------------------------------------------
        // Inspector
        // --------------------------------------------------------------------

        [Header("Startup")]
        [SerializeField] private bool verboseLogging = true;
        [SerializeField] private bool dontDestroyOnLoad = true;

        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance detected — destroying self.");
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

        // --------------------------------------------------------------------
        // Bootstrap
        // --------------------------------------------------------------------

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
            Log("BlockMaterialCache ready.");

            Log("Boot complete.");
        }

        // --------------------------------------------------------------------
        // Logging
        // --------------------------------------------------------------------

        private void Log(string message)
        {
            if (verboseLogging) Debug.Log($"[GameManager] {message}");
        }
    }
}
