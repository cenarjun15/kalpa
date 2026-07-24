// ============================================================================
// WorldSession.cs  (Phase 8 — streaming save routing)
// ----------------------------------------------------------------------------
// SaveNow() now delegates to ChunkManager.SaveWorld(), which saves modified
// chunks (region/*.chunk) plus a lightweight header (seed + player pose).
// The Phase 4 monolithic WorldSaveIO.Save is no longer used for streaming.
// ============================================================================

using System;
using Kalpa.Player;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.SaveSystem
{
    [DefaultExecutionOrder(-500)]
    public sealed class WorldSession : MonoBehaviour
    {
        public static WorldSession Instance { get; private set; }

        public string WorldName { get; private set; } = "MyWorld";
        public int Seed { get; private set; } = 42;
        public bool LoadedFromDisk { get; private set; }

        public event Action Saved;
        public event Action Loaded;

        [Header("Auto-save")]
        [SerializeField, Min(0f)] private float autoSaveIntervalSeconds = 60f;
        [SerializeField] private bool saveOnFocusLossAndQuit = true;

        [Header("Manual Save Hotkey")]
        [SerializeField] private bool ctrlSSaves = true;

        private float timeSinceLastSave;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            timeSinceLastSave += Time.unscaledDeltaTime;

            if (autoSaveIntervalSeconds > 0f && timeSinceLastSave >= autoSaveIntervalSeconds)
                SaveNow("auto-save");

            if (ctrlSSaves && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                           && Input.GetKeyDown(KeyCode.S))
                SaveNow("Ctrl+S");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && saveOnFocusLossAndQuit) SaveNow("focus lost");
        }

        private void OnApplicationQuit()
        {
            if (saveOnFocusLossAndQuit) SaveNow("quitting");
        }

        public void BeginNewWorld(string worldName, int seed)
        {
            WorldName = string.IsNullOrWhiteSpace(worldName) ? "MyWorld" : worldName;
            Seed = seed;
            LoadedFromDisk = false;
            timeSinceLastSave = 0f;
            Debug.Log($"[WorldSession] New world '{WorldName}' — seed {seed}.");
        }

        public void BeginLoadedWorld(WorldSaveHeader header)
        {
            WorldName = header.WorldName;
            Seed = header.Seed;
            LoadedFromDisk = true;
            timeSinceLastSave = 0f;
            Loaded?.Invoke();
            Debug.Log($"[WorldSession] Loaded world '{WorldName}' — seed {header.Seed}.");
        }

        /// <summary>Save via the streaming ChunkManager (modified chunks + header).</summary>
        public void SaveNow(string reason = "manual")
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            if (manager == null) return;

            Vector3 playerPos = Vector3.zero;
            float yaw = 0f, pitch = 0f;

            var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                playerPos = pc.transform.position;
                yaw = pc.transform.eulerAngles.y;
                var cam = pc.GetComponentInChildren<Camera>();
                if (cam != null) pitch = NormalizePitch(cam.transform.localEulerAngles.x);
            }

            try
            {
                manager.SaveWorld(playerPos, yaw, pitch);
                timeSinceLastSave = 0f;
                Saved?.Invoke();
                Debug.Log($"[WorldSession] Saved '{WorldName}' ({reason}).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldSession] Save failed: {e.Message}\n{e}");
            }
        }

        private static float NormalizePitch(float x)
        {
            if (x > 180f) x -= 360f;
            return x;
        }
    }
}
