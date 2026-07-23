// ============================================================================
// GameManager.cs  (Phase 2 update)
// ----------------------------------------------------------------------------
// Now owns BlockMaterialCache in addition to BlockRegistry and VoxelWorld.
// The Phase 1 diagnostic block-log listener has been removed — you'll see the
// world visually now.
// ============================================================================

using Kalpa.Blocks;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.Core
{
    /// <summary>
    /// Top-level game controller. Placed once in the MainScene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Singleton access
        // --------------------------------------------------------------------

        public static GameManager Instance { get; private set; }

        // --------------------------------------------------------------------
        // Systems
        // --------------------------------------------------------------------

        public BlockRegistry     BlockRegistry { get; private set; }
        public VoxelWorld        World         { get; private set; }
        public BlockMaterialCache MaterialCache { get; private set; }

        // --------------------------------------------------------------------
        // Inspector-configurable
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

            MaterialCache = new BlockMaterialCache();
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
