// ============================================================================
// BlockRegistry.cs
// ----------------------------------------------------------------------------
// Central registry of all BlockData assets in the game.
// Provides fast ID → BlockData and name → BlockData lookups.
// Loaded once at startup by GameManager.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.Blocks
{
    /// <summary>
    /// Runtime registry mapping block IDs to their BlockData definitions.
    /// This is a plain C# class (not a MonoBehaviour) — it does not live in
    /// the scene. GameManager owns the single instance.
    /// </summary>
    public sealed class BlockRegistry
    {
        // --------------------------------------------------------------------
        // Storage
        // --------------------------------------------------------------------

        // Array indexed by block ID → O(1) lookup, no boxing.
        private readonly BlockData[] byId = new BlockData[GameConstants.MaxBlockTypes];

        // Dictionary for internalName → BlockData lookup (used by save loader).
        private readonly Dictionary<string, BlockData> byName = new Dictionary<string, BlockData>();

        // --------------------------------------------------------------------
        // Registration
        // --------------------------------------------------------------------

        /// <summary>
        /// Load all BlockData assets found in Resources/Blocks/ and register them.
        /// Returns the number of blocks registered.
        /// </summary>
        public int LoadFromResources()
        {
            var loaded = Resources.LoadAll<BlockData>("Blocks");
            int registered = 0;

            foreach (var block in loaded)
            {
                if (block == null) continue;

                if (block.Id == GameConstants.AirBlockId)
                {
                    Debug.LogError($"[BlockRegistry] Block '{block.name}' uses reserved AIR id 0. Skipped.");
                    continue;
                }

                if (byId[block.Id] != null)
                {
                    Debug.LogError(
                        $"[BlockRegistry] Duplicate block ID {block.Id}: " +
                        $"'{byId[block.Id].name}' vs '{block.name}'. Skipped.");
                    continue;
                }

                if (byName.ContainsKey(block.InternalName))
                {
                    Debug.LogError(
                        $"[BlockRegistry] Duplicate internal name '{block.InternalName}'. Skipped.");
                    continue;
                }

                byId[block.Id] = block;
                byName[block.InternalName] = block;
                registered++;
            }

            Debug.Log($"[BlockRegistry] Registered {registered} block types.");
            return registered;
        }

        // --------------------------------------------------------------------
        // Query
        // --------------------------------------------------------------------

        /// <summary>Fast lookup by ID. Returns null if not registered.</summary>
        public BlockData GetById(byte id) => byId[id];

        /// <summary>Lookup by internal name. Returns null if not found.</summary>
        public BlockData GetByName(string internalName)
            => byName.TryGetValue(internalName, out var data) ? data : null;

        /// <summary>True if the given ID is registered (or is Air).</summary>
        public bool IsRegistered(byte id)
            => id == GameConstants.AirBlockId || byId[id] != null;

        /// <summary>Total count of registered non-air block types.</summary>
        public int Count
        {
            get
            {
                int c = 0;
                for (int i = 1; i < byId.Length; i++)
                    if (byId[i] != null) c++;
                return c;
            }
        }
    }
}
