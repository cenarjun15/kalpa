// ============================================================================
// PauseMenu.cs  (clean rebuild — guaranteed Quit button, no emoji, taller panel)
// ----------------------------------------------------------------------------
// ESC toggles the menu. Buttons: Resume, Save, Save & Quit to Menu, Quit Game.
// Panel is sized to fit all buttons + volume sliders with margin, so nothing
// gets clipped. No emoji (renders cleanly in builds).
// ============================================================================

using Kalpa.Audio;
using Kalpa.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kalpa.UI
{
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("Scene names (must match Build Settings)")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Layout")]
        [SerializeField] private int panelWidth = 400;
        [SerializeField] private int panelHeight = 520;

        private bool open;
        public bool IsOpen => open;

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

        private void OnGUI()
        {
            if (!open) return;

            // Dark backdrop.
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            var panel = new Rect(
                (Screen.width - panelWidth) / 2,
                (Screen.height - panelHeight) / 2,
                panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(panel.x, panel.y + 16, panel.width, 34), "PAUSED", title);

            var area = new Rect(panel.x + 28, panel.y + 64, panel.width - 56, panel.height - 84);
            GUILayout.BeginArea(area);

            DrawVolumeSliders();
            GUILayout.Space(16);

            if (GUILayout.Button("Resume", GUILayout.Height(40)))
            {
                open = false;
                SetCursor(false);
            }
            GUILayout.Space(8);

            if (GUILayout.Button("Save", GUILayout.Height(40)))
                WorldSession.Instance?.SaveNow("pause menu");
            GUILayout.Space(8);

            if (GUILayout.Button("Save & Quit to Menu", GUILayout.Height(40)))
            {
                WorldSession.Instance?.SaveNow("quit to menu");
                LoadMainMenu();
            }
            GUILayout.Space(8);

            // THE EXIT BUTTON.
            if (GUILayout.Button("Quit Game", GUILayout.Height(40)))
            {
                WorldSession.Instance?.SaveNow("quit to desktop");
                QuitGame();
            }

            GUILayout.EndArea();
        }

        private void DrawVolumeSliders()
        {
            var audio = AudioSystem.Instance;
            if (audio == null) return;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) },
            };

            GUILayout.Label($"Master  ({Mathf.RoundToInt(audio.MasterVolume * 100)}%)", labelStyle);
            float m = GUILayout.HorizontalSlider(audio.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(m, audio.MasterVolume)) audio.SetMasterVolume(m);

            GUILayout.Space(4);
            GUILayout.Label($"SFX  ({Mathf.RoundToInt(audio.SfxVolume * 100)}%)", labelStyle);
            float s = GUILayout.HorizontalSlider(audio.SfxVolume, 0f, 1f);
            if (!Mathf.Approximately(s, audio.SfxVolume)) audio.SetSfxVolume(s);

            GUILayout.Space(4);
            GUILayout.Label($"Ambient  ({Mathf.RoundToInt(audio.AmbientVolume * 100)}%)", labelStyle);
            float a = GUILayout.HorizontalSlider(audio.AmbientVolume, 0f, 1f);
            if (!Mathf.Approximately(a, audio.AmbientVolume)) audio.SetAmbientVolume(a);
        }

        private void LoadMainMenu()
        {
            if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName);
            else
                Debug.LogError($"[PauseMenu] Scene '{mainMenuSceneName}' not in Build Settings!");
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
