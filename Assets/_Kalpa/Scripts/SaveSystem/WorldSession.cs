// ============================================================================
// WorldSession.cs
// ----------------------------------------------------------------------------
// Represents the currently active world session.
// Owns: world name, seed, "is loaded from disk" flag, auto-save timer.
//
// Anyone who needs to save or trigger auto-save calls into this MonoBehaviour
// rather than talking to WorldSaveIO directly. This centralises policy
// decisions (when to save, how often, where the player is, etc.) in one place.
// ============================================================================

using System;
using Kalpa.Core;
using Kalpa.Player;
using UnityEngine;

namespace Kalpa.SaveSystem
{
    /// <summary>
    /// Runtime state of the current world session.
    /// Placed once in the scene alongside GameManager.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class WorldSession : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Singleton (only one active world at a time)
        // --------------------------------------------------------------------

        public static WorldSession Instance { get; private set; }

        // --------------------------------------------------------------------
        // Session state
        // --------------------------------------------------------------------

        /// <summary>Name of the world currently loaded. Used as the folder name.</summary>
        public string WorldName { get; private set; } = "MyWorld";

        /// <summary>RNG seed the current world was generated with.</summary>
        public int Seed { get; private set; } = 42;

        /// <summary>True if this session was restored from disk (rather than freshly generated).</summary>
        public bool LoadedFromDisk { get; private set; }

        /// <summary>Fired after a successful save.</summary>
        public event Action Saved;

        /// <summary>Fired after a successful load.</summary>
        public event Action Loaded;

        // --------------------------------------------------------------------
        // Auto-save
        // --------------------------------------------------------------------

        [Header("Auto-save")]
        [Tooltip("Save automatically every N seconds. 0 = disabled.")]
        [SerializeField, Min(0f)] private float autoSaveIntervalSeconds = 60f;

        [Tooltip("Save automatically when the game loses focus or quits.")]
        [SerializeField] private bool saveOnFocusLossAndQuit = true;

        [Header("Manual Save Hotkey")]
        [Tooltip("Also allow Ctrl+S to save manually.")]
        [SerializeField] private bool ctrlSSaves = true;

        private float timeSinceLastSave;

        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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
            {
                SaveNow("auto-save");
            }

            if (ctrlSSaves && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                           && Input.GetKeyDown(KeyCode.S))
            {
                SaveNow("Ctrl+S");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && saveOnFocusLossAndQuit) SaveNow("focus lost");
        }

        private void OnApplicationQuit()
        {
            if (saveOnFocusLossAndQuit) SaveNow("quitting");
        }

        // --------------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------------

        /// <summary>Configure the session for a NEW world (about to be generated).</summary>
        public void BeginNewWorld(string worldName, int seed)
        {
            WorldName = string.IsNullOrWhiteSpace(worldName) ? "MyWorld" : worldName;
            Seed = seed;
            LoadedFromDisk = false;
            timeSinceLastSave = 0f;
            Debug.Log($"[WorldSession] New world '{WorldName}' — seed {seed}.");
        }

        /// <summary>Configure the session for a LOADED world (already deserialised).</summary>
        public void BeginLoadedWorld(WorldSaveHeader header)
        {
            WorldName = header.WorldName;
            Seed = header.Seed;
            LoadedFromDisk = true;
            timeSinceLastSave = 0f;
            Loaded?.Invoke();
            Debug.Log($"[WorldSession] Loaded world '{WorldName}' — seed {header.Seed}.");
        }

        /// <summary>Save the world now. Silent no-op if GameManager is missing.</summary>
        public void SaveNow(string reason = "manual")
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.World == null) return;

            Vector3 playerPos = Vector3.zero;
            float yaw = 0f, pitch = 0f;

            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                playerPos = pc.transform.position;
                yaw = pc.transform.eulerAngles.y;
                var cam = pc.GetComponentInChildren<Camera>();
                if (cam != null) pitch = NormalizePitch(cam.transform.localEulerAngles.x);
            }

            try
            {
                WorldSaveIO.Save(WorldName, Seed, gm.World, playerPos, yaw, pitch);
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
