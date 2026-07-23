// ============================================================================
// MainMenuController.cs
// ----------------------------------------------------------------------------
// Simple IMGUI main menu (no Canvas / UGUI setup required).
// Provides: New World, Load World, Continue, Quit.
//
// On selection, it configures WorldSession (via a small persistent bootstrap
// object) and loads the MainScene where the world actually spawns.
// ============================================================================

using System.Collections.Generic;
using Kalpa.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kalpa.UI
{
    /// <summary>
    /// Main menu — attach to a GameObject in the MainMenu scene.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Names (must match Build Settings)")]
        [Tooltip("The main gameplay scene to load when a world is selected.")]
        [SerializeField] private string mainSceneName = "MainScene";

        [Header("Layout")]
        [SerializeField] private int panelWidth = 380;
        [SerializeField] private int panelHeight = 480;

        // --------------------------------------------------------------------
        // UI state
        // --------------------------------------------------------------------

        private enum View { Root, NewWorld, LoadWorld }
        private View view = View.Root;

        // New-world form state.
        private string newWorldName = "MyWorld";
        private string newWorldSeed = "42";

        // Load-world state.
        private string[] cachedWorldList;
        private Vector2 loadScroll;

        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Start()
        {
            // Menu is fully mouse-driven; make sure the cursor is visible.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RefreshWorldList();
        }

        private void RefreshWorldList() => cachedWorldList = WorldSaveIO.ListWorlds();

        // --------------------------------------------------------------------
        // IMGUI
        // --------------------------------------------------------------------

        private void OnGUI()
        {
            var panel = new Rect(
                (Screen.width - panelWidth) / 2,
                (Screen.height - panelHeight) / 2,
                panelWidth, panelHeight);

            DrawBackdrop();
            GUI.Box(panel, GUIContent.none);

            // Title.
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(panel.x, panel.y + 20, panel.width, 44), "KALPA", titleStyle);

            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f, 1f) },
            };
            GUI.Label(new Rect(panel.x, panel.y + 62, panel.width, 22),
                      "A voxel sandbox — v" + Kalpa.Core.GameConstants.GameVersion,
                      subtitleStyle);

            // Contents.
            var contentArea = new Rect(panel.x + 24, panel.y + 96,
                                       panel.width - 48, panel.height - 116);
            GUILayout.BeginArea(contentArea);
            switch (view)
            {
                case View.Root:      DrawRootMenu(); break;
                case View.NewWorld:  DrawNewWorldForm(); break;
                case View.LoadWorld: DrawLoadWorldList(); break;
            }
            GUILayout.EndArea();
        }

        // Subtle dark backdrop so the menu reads against any background.
        private void DrawBackdrop()
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // --------------------------------------------------------------------
        // Root menu
        // --------------------------------------------------------------------

        private void DrawRootMenu()
        {
            GUILayout.Space(20);

            if (BigButton("▶ New World")) view = View.NewWorld;
            GUILayout.Space(10);

            bool anySaves = cachedWorldList != null && cachedWorldList.Length > 0;

            GUI.enabled = anySaves;
            if (BigButton("📂 Load World")) { RefreshWorldList(); view = View.LoadWorld; }
            GUILayout.Space(10);

            if (BigButton("⏩ Continue Last") && anySaves)
            {
                // Continue = most-recently-modified save.
                var latest = FindMostRecentWorld();
                if (!string.IsNullOrEmpty(latest)) LoadExistingWorld(latest);
            }
            GUI.enabled = true;

            GUILayout.Space(30);

            if (BigButton("✖ Quit"))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        // --------------------------------------------------------------------
        // New world form
        // --------------------------------------------------------------------

        private void DrawNewWorldForm()
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white },
            };
            var fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 14 };

            GUILayout.Label("World name", labelStyle);
            newWorldName = GUILayout.TextField(newWorldName, 40, fieldStyle, GUILayout.Height(26));
            GUILayout.Space(12);

            GUILayout.Label("Seed (numbers only)", labelStyle);
            newWorldSeed = GUILayout.TextField(newWorldSeed, 10, fieldStyle, GUILayout.Height(26));
            GUILayout.Space(8);

            if (GUILayout.Button("🎲 Random seed", GUILayout.Height(26)))
                newWorldSeed = new System.Random().Next(1, int.MaxValue).ToString();

            GUILayout.Space(24);

            GUI.enabled = IsNewWorldFormValid();
            if (BigButton("Create world")) CreateNewWorld();
            GUI.enabled = true;

            GUILayout.Space(10);

            if (BigButton("← Back")) view = View.Root;
        }

        private bool IsNewWorldFormValid()
        {
            if (string.IsNullOrWhiteSpace(newWorldName)) return false;
            if (!int.TryParse(newWorldSeed, out _)) return false;
            return true;
        }

        private void CreateNewWorld()
        {
            // If a save with this name already exists, delete it so "New World"
            // really is a new world. (Matches most players' expectation.)
            if (WorldSaveIO.WorldExists(newWorldName))
            {
                var folder = WorldSaveIO.WorldFolder(newWorldName);
                try { System.IO.Directory.Delete(folder, true); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[MainMenu] Could not delete existing world '{newWorldName}': {e.Message}");
                }
            }

            int seed = int.Parse(newWorldSeed);
            SessionBootstrap.QueueForNextScene(newWorldName, seed);
            LoadMainScene();
        }

        // --------------------------------------------------------------------
        // Load-world list
        // --------------------------------------------------------------------

        private void DrawLoadWorldList()
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            GUILayout.Label("Choose a world:", labelStyle);
            GUILayout.Space(6);

            loadScroll = GUILayout.BeginScrollView(loadScroll, GUILayout.Height(240));

            if (cachedWorldList == null || cachedWorldList.Length == 0)
            {
                GUILayout.Label("(no saved worlds)", GUI.skin.label);
            }
            else
            {
                foreach (var w in cachedWorldList)
                {
                    if (GUILayout.Button($"📁  {w}", GUILayout.Height(30)))
                    {
                        LoadExistingWorld(w);
                        break;
                    }
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(14);
            if (BigButton("← Back")) view = View.Root;
        }

        // --------------------------------------------------------------------
        // Actions
        // --------------------------------------------------------------------

        private void LoadExistingWorld(string worldName)
        {
            // Seed doesn't matter for load — save file overrides it. Use 0 as placeholder.
            SessionBootstrap.QueueForNextScene(worldName, 0);
            LoadMainScene();
        }

        private void LoadMainScene()
        {
            if (Application.CanStreamedLevelBeLoaded(mainSceneName))
            {
                SceneManager.LoadScene(mainSceneName);
            }
            else
            {
                Debug.LogError($"[MainMenu] Scene '{mainSceneName}' is not in Build Settings!");
            }
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private string FindMostRecentWorld()
        {
            if (cachedWorldList == null || cachedWorldList.Length == 0) return null;

            string best = null;
            System.DateTime bestTime = System.DateTime.MinValue;

            foreach (var name in cachedWorldList)
            {
                var path = WorldSaveIO.HeaderPath(name);
                if (!System.IO.File.Exists(path)) continue;
                var t = System.IO.File.GetLastWriteTimeUtc(path);
                if (t > bestTime) { bestTime = t; best = name; }
            }
            return best;
        }

        private static bool BigButton(string label)
        {
            return GUILayout.Button(label, GUILayout.Height(38));
        }
    }
}
