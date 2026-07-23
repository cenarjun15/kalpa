// ============================================================================
// SaveIndicator.cs
// ----------------------------------------------------------------------------
// Tiny bit of on-screen feedback: shows a fading "Saved" label whenever
// WorldSession fires its Saved event.
// ============================================================================

using Kalpa.SaveSystem;
using UnityEngine;

namespace Kalpa.UI
{
    /// <summary>Draws a short-lived "Saved" label near the top-right of the screen.</summary>
    public sealed class SaveIndicator : MonoBehaviour
    {
        [SerializeField] private float showDurationSeconds = 1.6f;

        private float timeRemaining;

        private void Start()
        {
            if (WorldSession.Instance != null)
            {
                WorldSession.Instance.Saved += OnSaved;
            }
        }

        private void OnDestroy()
        {
            if (WorldSession.Instance != null)
            {
                WorldSession.Instance.Saved -= OnSaved;
            }
        }

        private void OnSaved()
        {
            timeRemaining = showDurationSeconds;
        }

        private void Update()
        {
            if (timeRemaining > 0f)
                timeRemaining -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (timeRemaining <= 0f) return;

            float alpha = Mathf.Clamp01(timeRemaining / showDurationSeconds);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, alpha) },
            };

            const int w = 140, h = 34;
            var box = new Rect(Screen.width - w - 16, 16, w, h);

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f * alpha);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(box, "✓ Saved", style);
        }
    }
}
