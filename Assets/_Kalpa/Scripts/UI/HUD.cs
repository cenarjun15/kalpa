// ============================================================================
// HUD.cs
// ----------------------------------------------------------------------------
// Minimalist IMGUI-based on-screen UI:
//   * Crosshair in the centre
//   * Hotbar at the bottom showing selected block
//   * Small stats block (FPS, position) at the top-left
// IMGUI is chosen deliberately: zero scene setup, no prefabs, no Canvas noise.
// Phase 4+ can replace this with a proper UGUI/UI Toolkit implementation.
// ============================================================================

using Kalpa.Core;
using Kalpa.Player;
using UnityEngine;

namespace Kalpa.UI
{
    /// <summary>
    /// Simple on-screen HUD. Attach to any GameObject in the scene.
    /// </summary>
    public sealed class HUD : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        [Header("Style")]
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private int crosshairSize = 12;
        [SerializeField] private int hotbarSlotSize = 56;

        // --------------------------------------------------------------------
        // FPS tracking (simple exponential average).
        // --------------------------------------------------------------------

        private float fpsAvg;
        private const float FpsSmoothing = 0.9f;

        private void Update()
        {
            float instantFps = 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-5f);
            fpsAvg = FpsSmoothing * fpsAvg + (1f - FpsSmoothing) * instantFps;

            if (player == null) player = FindFirstObjectByType<PlayerController>();
        }

        // --------------------------------------------------------------------
        // OnGUI runs every frame — IMGUI is retained-mode-free.
        // --------------------------------------------------------------------

        private void OnGUI()
        {
            DrawCrosshair();
            DrawStats();
            DrawHotbar();
        }

        // --------------------------------------------------------------------
        // Crosshair — a simple cross drawn as two coloured rects.
        // --------------------------------------------------------------------

        private void DrawCrosshair()
        {
            var prev = GUI.color;
            GUI.color = crosshairColor;

            int cx = Screen.width / 2;
            int cy = Screen.height / 2;
            int s = crosshairSize;
            int t = 2;

            GUI.DrawTexture(new Rect(cx - s / 2, cy - t / 2, s, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - t / 2, cy - s / 2, t, s), Texture2D.whiteTexture);

            GUI.color = prev;
        }

        // --------------------------------------------------------------------
        // Stats — top-left overlay.
        // --------------------------------------------------------------------

        private void DrawStats()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 13,
            };

            var box = new Rect(10, 10, 240, 82);
            GUI.Box(box, GUIContent.none);

            string pos = player != null
                ? $"{player.transform.position.x:F1}, {player.transform.position.y:F1}, {player.transform.position.z:F1}"
                : "-";

            GUI.Label(new Rect(box.x + 8,  box.y + 6,  box.width - 16, 20),
                      $"Kalpa v{GameConstants.GameVersion}", style);
            GUI.Label(new Rect(box.x + 8,  box.y + 26, box.width - 16, 20),
                      $"FPS: {fpsAvg:F0}", style);
            GUI.Label(new Rect(box.x + 8,  box.y + 46, box.width - 16, 20),
                      $"Pos: {pos}", style);
        }

        // --------------------------------------------------------------------
        // Hotbar — bottom-centre.
        // --------------------------------------------------------------------

        private void DrawHotbar()
        {
            if (player == null) return;
            var slots = player.HotbarBlockIds;
            if (slots == null || slots.Count == 0) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            int slotSize = hotbarSlotSize;
            int gap = 6;
            int totalWidth = slots.Count * (slotSize + gap) - gap;
            int startX = (Screen.width - totalWidth) / 2;
            int y = Screen.height - slotSize - 20;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                fontSize = 11,
                alignment = TextAnchor.LowerCenter,
            };

            for (int i = 0; i < slots.Count; i++)
            {
                var rect = new Rect(startX + i * (slotSize + gap), y, slotSize, slotSize);

                var data = gm.BlockRegistry.GetById(slots[i]);
                bool selected = (i == player.SelectedSlot);

                // Background
                var prevColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                // Coloured block preview
                GUI.color = data != null ? data.DebugColor : Color.magenta;
                var inner = new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 22);
                GUI.DrawTexture(inner, Texture2D.whiteTexture);

                // Border (yellow if selected, dark grey otherwise)
                GUI.color = selected ? Color.yellow : new Color(0.2f, 0.2f, 0.2f, 1f);
                DrawRectBorder(rect, selected ? 3 : 2);

                // Number label
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x, rect.y + rect.height - 14, rect.width, 14),
                          (i + 1).ToString(), labelStyle);

                GUI.color = prevColor;
            }
        }

        private static void DrawRectBorder(Rect rect, int width)
        {
            var tex = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, width), tex);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - width, rect.width, width), tex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), tex);
            GUI.DrawTexture(new Rect(rect.x + rect.width - width, rect.y, width, rect.height), tex);
        }
    }
}
