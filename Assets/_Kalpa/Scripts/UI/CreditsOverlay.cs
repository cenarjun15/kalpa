// ============================================================================
// CreditsOverlay.cs
// ----------------------------------------------------------------------------
// Shows author + copyright text on the main menu (and optionally a small
// watermark in-game). Attach to a GameObject in the MainMenu scene, and
// optionally one in the MainScene too.
// ============================================================================

using Kalpa.Core;
using UnityEngine;

namespace Kalpa.UI
{
    public sealed class CreditsOverlay : MonoBehaviour
    {
        public enum Mode { MainMenu, InGameWatermark }

        [SerializeField] private Mode mode = Mode.MainMenu;
        [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.75f);

        private void OnGUI()
        {
            if (mode == Mode.MainMenu) DrawMainMenuCredits();
            else DrawInGameWatermark();
        }

        private void DrawMainMenuCredits()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = textColor },
            };

            // Bottom-centre credit line.
            var rect = new Rect(0, Screen.height - 52, Screen.width, 22);
            GUI.Label(rect, $"Created by {GameConstants.Author}", style);

            var copyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = new Color(textColor.r, textColor.g, textColor.b, 0.55f) },
            };
            var rect2 = new Rect(0, Screen.height - 30, Screen.width, 20);
            GUI.Label(rect2, GameConstants.Copyright, copyStyle);
        }

        private void DrawInGameWatermark()
        {
            // Subtle bottom-right watermark during gameplay.
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.LowerRight,
                normal = { textColor = new Color(1f, 1f, 1f, 0.35f) },
            };
            var rect = new Rect(0, Screen.height - 22, Screen.width - 10, 18);
            GUI.Label(rect, $"Kalpa — {GameConstants.Author}", style);
        }
    }
}
