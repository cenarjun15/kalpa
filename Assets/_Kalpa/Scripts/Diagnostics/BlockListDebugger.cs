// ============================================================================
// BlockListDebugger.cs
// ----------------------------------------------------------------------------
// One-time diagnostic: on Start, logs every registered block's Id, DisplayName,
// and InternalName. Use this to find the mysterious "ne" entry.
// Attach to any GameObject, press Play, read the Console, then delete it.
// ============================================================================

using Kalpa.Core;
using UnityEngine;

namespace Kalpa.Diagnostics
{
    public sealed class BlockListDebugger : MonoBehaviour
    {
        private void Start() => Invoke(nameof(Dump), 0.5f);

        private void Dump()
        {
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[BlockList] No GameManager."); return; }

            Debug.Log("========== REGISTERED BLOCKS ==========");
            for (int id = 1; id < GameConstants.MaxBlockTypes; id++)
            {
                var d = gm.BlockRegistry.GetById((byte)id);
                if (d == null) continue;
                Debug.Log($"[BlockList] id={id}  display='{d.DisplayName}'  internal='{d.InternalName}'  assetName='{d.name}'");
            }
            Debug.Log("=======================================");
        }
    }
}
