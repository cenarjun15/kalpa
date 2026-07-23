// ============================================================================
// PauseMenu.cs  (Phase 5B update — volume sliders)
// ----------------------------------------------------------------------------
// Adds Master/SFX/Ambient volume sliders. Values live in AudioSystem +
// PlayerPrefs, so they persist across sessions.
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
        [SerializeField] private int panelWidth = 380;
        [SerializeField] private int panelHeight = 460;

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

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

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

            DrawVolumeSliders();
            GUILayout.Space(12);

            if (GUILayout.Button("▶ Resume", GUILayout.Height(38)))
            {
                open = false;
                SetCursor(false);
            }
            GUILayout.Space(6);

            if (GUILayout.Button("💾 Save", GUILayout.Height(38)))
            {
                WorldSession.Instance?.SaveNow("pause menu");
            }
            GUILayout.Space(6);

            if (GUILayout.Button("🏠 Save & Quit to Menu", GUILayout.Height(38)))
            {
                WorldSession.Instance?.SaveNow("quit to menu");
                LoadMainMenu();
            }
            GUILayout.Space(6);

            if (GUILayout.Button("✖ Quit to Desktop", GUILayout.Height(38)))
            {
                WorldSession.Instance?.SaveNow("quit to desktop");
                Quit();
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
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 1f) },
            };

            float master = audio.MasterVolume;
            float sfx = audio.SfxVolume;
            float ambient = audio.AmbientVolume;

            GUILayout.Label($"🔊 Master  ({Mathf.RoundToInt(master * 100)}%)", labelStyle);
            float newMaster = GUILayout.HorizontalSlider(master, 0f, 1f);
            if (!Mathf.Approximately(newMaster, master)) audio.SetMasterVolume(newMaster);

            GUILayout.Space(4);
            GUILayout.Label($"💥 SFX  ({Mathf.RoundToInt(sfx * 100)}%)", labelStyle);
            float newSfx = GUILayout.HorizontalSlider(sfx, 0f, 1f);
            if (!Mathf.Approximately(newSfx, sfx)) audio.SetSfxVolume(newSfx);

            GUILayout.Space(4);
            GUILayout.Label($"🌬 Ambient  ({Mathf.RoundToInt(ambient * 100)}%)", labelStyle);
            float newAmbient = GUILayout.HorizontalSlider(ambient, 0f, 1f);
            if (!Mathf.Approximately(newAmbient, ambient)) audio.SetAmbientVolume(newAmbient);
        }

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