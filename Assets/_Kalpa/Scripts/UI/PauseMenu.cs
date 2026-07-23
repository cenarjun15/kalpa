// ============================================================================
// PauseMenu.cs
// ----------------------------------------------------------------------------
// Press ESC (once) in-game to open a small pause menu.
// Options: Resume, Save, Save & Quit to Menu, Quit to Desktop.
//
// Deliberately does NOT set Time.timeScale = 0 — physics still runs, so gravity
// and animations are unaffected while the menu is open. The player controller
// itself simply ignores input while the cursor is unlocked.
// ============================================================================

using Kalpa.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kalpa.UI
{
    /// <summary>
    /// In-game pause menu. Attach to any GameObject in the MainScene.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("Scene names (must match Build Settings)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Layout")]
        [SerializeField] private int panelWidth = 320;
        [SerializeField] private int panelHeight = 300;

        private bool open;

        // --------------------------------------------------------------------
        // Input
        // --------------------------------------------------------------------

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                open = !open;
                SetCursor(open);
            }
        }

        private void SetCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }

        // --------------------------------------------------------------------
        // Public accessor — PlayerInput checks this to freeze input while open
        // --------------------------------------------------------------------

        public bool IsOpen => open;

        // --------------------------------------------------------------------
        // IMGUI
        // --------------------------------------------------------------------

        private void OnGUI()
        {
            if (!open) return;

            // Backdrop.
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            // Panel.
            var panel = new Rect(
                (Screen.width - panelWidth) / 2,
                (Screen.height - panelHeight) / 2,
                panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(panel.x, panel.y + 18, panel.width, 32), "Paused", title);

            var contentArea = new Rect(panel.x + 24, panel.y + 66,
                                       panel.width - 48, panel.height - 84);
            GUILayout.BeginArea(contentArea);

            if (GUILayout.Button("▶ Resume", GUILayout.Height(38)))
            {
                open = false;
                SetCursor(false);
            }
            GUILayout.Space(8);

            if (GUILayout.Button("💾 Save", GUILayout.Height(38)))
            {
                WorldSession.Instance?.SaveNow("pause menu");
            }
            GUILayout.Space(8);

            if (GUILayout.Button("🏠 Save & Quit to Menu", GUILayout.Height(38)))
            {
                WorldSession.Instance?.SaveNow("quit to menu");
                LoadMainMenu();
            }
            GUILayout.Space(8);

            if (GUILayout.Button("✖ Quit to Desktop", GUILayout.Height(38)))
            {
                WorldSession.Instance?.SaveNow("quit to desktop");
                Quit();
            }

            GUILayout.EndArea();
        }

        // --------------------------------------------------------------------
        // Actions
        // --------------------------------------------------------------------

        private void LoadMainMenu()
        {
            if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Debug.LogError($"[PauseMenu] Scene '{mainMenuSceneName}' is not in Build Settings!");
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
