// ============================================================================
// SessionBootstrap.cs
// ----------------------------------------------------------------------------
// Small persistent shim that ferries "which world to load" from the MainMenu
// scene into the MainScene.
//
// Flow:
//   1. Main menu calls SessionBootstrap.QueueForNextScene("MyWorld", 42).
//   2. That call spawns a DontDestroyOnLoad GameObject holding a
//      SessionBootstrap component.
//   3. Main scene loads. On Awake, SessionBootstrap finds the WorldSession
//      that was placed in the main scene and pushes the queued values into it,
//      then destroys itself.
//
// This is cleaner than a static field: still testable, still garbage-collected,
// still visible in the Hierarchy while it exists.
// ============================================================================

using Kalpa.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kalpa.UI
{
    /// <summary>Ferries world name + seed across a scene load.</summary>
    public sealed class SessionBootstrap : MonoBehaviour
    {
        private static SessionBootstrap current;

        [SerializeField] private string queuedWorldName;
        [SerializeField] private int queuedSeed;

        // --------------------------------------------------------------------
        // Static entry point — called from the main menu.
        // --------------------------------------------------------------------

        public static void QueueForNextScene(string worldName, int seed)
        {
            // If a previous bootstrap is still around (edge case: player went
            // back to main menu without quitting), reuse it.
            if (current == null)
            {
                var go = new GameObject("~SessionBootstrap");
                DontDestroyOnLoad(go);
                current = go.AddComponent<SessionBootstrap>();
            }

            current.queuedWorldName = worldName;
            current.queuedSeed = seed;
        }

        // --------------------------------------------------------------------
        // Apply on scene load
        // --------------------------------------------------------------------

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Only care about the main scene.
            if (scene.name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            var session = WorldSession.Instance;
            if (session == null)
            {
                // Might not have run its Awake yet — defer one frame.
                StartCoroutine(ApplyNextFrame());
            }
            else
            {
                ApplyToSession(session);
                Destroy(gameObject);
                current = null;
            }
        }

        private System.Collections.IEnumerator ApplyNextFrame()
        {
            yield return null;
            var session = WorldSession.Instance;
            if (session != null) ApplyToSession(session);
            Destroy(gameObject);
            current = null;
        }

        private void ApplyToSession(WorldSession session)
        {
            // If a saved world exists on disk, treat this as "load"; otherwise
            // treat it as "new world with this seed". WorldSession's own
            // BeginLoadedWorld is called by ChunkManager after the load succeeds,
            // so we just push the intent here.
            session.BeginNewWorld(queuedWorldName, queuedSeed);
        }
    }
}
