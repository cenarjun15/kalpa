// ============================================================================
// BlockPicker.cs  (v2 — real texture thumbnails)
// ----------------------------------------------------------------------------
// The palette now draws each block's ACTUAL texture (its atlas tile) as the
// swatch, instead of a flat debug colour. Cutout blocks (leaves) draw the
// cutout leaf texture; everything else draws its atlas sub-rect via
// GUI.DrawTextureWithTexCoords.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using Kalpa.Player;
using UnityEngine;

namespace Kalpa.UI
{
    public sealed class BlockPicker : MonoBehaviour
    {
        [Header("Open/Close")]
        [SerializeField] private KeyCode toggleKey = KeyCode.E;

        [Header("Layout")]
        [SerializeField] private int columns = 6;
        [SerializeField] private int cellSize = 78;
        [SerializeField] private int cellPadding = 8;

        private bool open;
        public bool IsOpen => open;

        private Vector2 scroll;
        private readonly List<BlockData> palette = new List<BlockData>();
        private PlayerController player;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                open = !open;
                SetCursor(open);
                if (open) RefreshPalette();
            }
            if (open && Input.GetKeyDown(KeyCode.Escape))
            {
                open = false;
                SetCursor(false);
            }
        }

        private void SetCursor(bool visible)
        {
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }

        private void RefreshPalette()
        {
            palette.Clear();
            var gm = GameManager.Instance;
            if (gm == null) return;

            for (int id = 1; id < GameConstants.MaxBlockTypes; id++)
            {
                var data = gm.BlockRegistry.GetById((byte)id);
                if (data != null) palette.Add(data);
            }

            if (player == null)
                player = Object.FindFirstObjectByType<PlayerController>();
        }

        private void OnGUI()
        {
            if (!open) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            var atlasTex = gm.TextureAtlas != null ? gm.TextureAtlas.Atlas : null;
            var cutoutTex = gm.MaterialCache != null && gm.MaterialCache.CutoutMaterial != null
                ? gm.MaterialCache.CutoutMaterial.mainTexture as Texture2D
                : null;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            int panelW = columns * (cellSize + cellPadding) + cellPadding + 24;
            int panelH = Mathf.Min(Screen.height - 120, 620);
            var panel = new Rect((Screen.width - panelW) / 2, (Screen.height - panelH) / 2, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white },
            };
            GUI.Label(new Rect(panel.x, panel.y + 12, panel.width, 28), "Select a Block", title);

            var hint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) },
            };
            GUI.Label(new Rect(panel.x, panel.y + 40, panel.width, 18),
                      $"Press {toggleKey} or ESC to close", hint);

            var viewRect = new Rect(panel.x + 12, panel.y + 66, panel.width - 24, panel.height - 78);
            int rows = Mathf.CeilToInt(palette.Count / (float)columns);
            int contentH = rows * (cellSize + cellPadding) + cellPadding;
            var contentRect = new Rect(0, 0, viewRect.width - 20, contentH);

            scroll = GUI.BeginScrollView(viewRect, scroll, contentRect);

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 8, alignment = TextAnchor.LowerCenter,
                normal = { textColor = Color.white },
                wordWrap = false,   // FIX: no wrap → long names no longer clip to "ne"
                clipping = TextClipping.Clip,
            };

            for (int i = 0; i < palette.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                var cell = new Rect(
                    cellPadding + col * (cellSize + cellPadding),
                    cellPadding + row * (cellSize + cellPadding),
                    cellSize, cellSize);

                var data = palette[i];

                // Cell background.
                GUI.color = new Color(0f, 0f, 0f, 0.4f);
                GUI.DrawTexture(cell, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var swatch = new Rect(cell.x + 8, cell.y + 8, cell.width - 16, cell.height - 26);

                // Draw the real texture thumbnail.
                if (data.IsCutout && cutoutTex != null)
                {
                    // Leaves: draw full cutout texture.
                    GUI.DrawTexture(swatch, cutoutTex, ScaleMode.StretchToFill, true);
                }
                else if (atlasTex != null)
                {
                    // Draw the block's atlas sub-rect.
                    Rect uv = gm.TextureAtlas.GetUV(data.Id);
                    // Apply debug-colour tint so glass/water still read as tinted.
                    GUI.color = data.DebugColor.a > 0.05f ? data.DebugColor : Color.white;
                    GUI.DrawTextureWithTexCoords(swatch, atlasTex, uv, true);
                    GUI.color = Color.white;
                }
                else
                {
                    // Fallback: flat colour.
                    GUI.color = data.DebugColor.a > 0.05f
                        ? new Color(data.DebugColor.r, data.DebugColor.g, data.DebugColor.b, 1f)
                        : Color.gray;
                    GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                GUI.Label(new Rect(cell.x + 1, cell.y + cell.height - 18, cell.width - 2, 16),
                          data.DisplayName, nameStyle);

                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                {
                    if (player != null) player.SetSelectedBlockById(data.Id);
                    open = false;
                    SetCursor(false);
                }
            }

            GUI.EndScrollView();
            GUI.color = prev;
        }
    }
}
