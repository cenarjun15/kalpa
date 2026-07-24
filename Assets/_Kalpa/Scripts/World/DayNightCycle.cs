// ============================================================================
// DayNightCycle.cs
// ----------------------------------------------------------------------------
// Drives a full day/night cycle entirely from code — no gradient assets or
// lighting-window setup required.
//
// Each frame it:
//   * Advances a normalized time-of-day value t in [0,1)  (0 = midnight,
//     0.25 = sunrise, 0.5 = noon, 0.75 = sunset).
//   * Rotates the sun directional light accordingly.
//   * Interpolates sky colour, fog colour, ambient colour, and sun colour
//     between hand-picked key colours for midnight / dawn / noon / dusk.
//   * Applies everything to the camera background, RenderSettings, and sun.
//
// This REPLACES the static SkyController from Phase 4B. Remove SkyController
// from the scene and use this instead (setup notes at bottom of chat message).
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace Kalpa.World
{
    /// <summary>
    /// Real-time day/night cycle. Attach to a GameObject in the MainScene.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class DayNightCycle : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Time configuration
        // --------------------------------------------------------------------

        [Header("Time")]

        [Tooltip("Length of a full day/night cycle in real seconds.")]
        [SerializeField, Min(10f)] private float dayLengthSeconds = 600f; // 10 min

        [Tooltip("Time of day at start. 0=midnight, 0.25=sunrise, 0.5=noon, 0.75=sunset.")]
        [SerializeField, Range(0f, 1f)] private float startTime01 = 0.30f; // early morning

        [Tooltip("If true, time does not advance (useful for screenshots).")]
        [SerializeField] private bool freezeTime = false;

        [Header("Debug controls")]
        [Tooltip("Hold this key to fast-forward time (10× speed).")]
        [SerializeField] private KeyCode fastForwardKey = KeyCode.T;

        [Tooltip("Show a small clock + time-of-day label on screen.")]
        [SerializeField] private bool showClock = true;

        // --------------------------------------------------------------------
        // Fog
        // --------------------------------------------------------------------

        [Header("Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private float fogStart = 40f;
        [SerializeField] private float fogEnd = 110f;

        // --------------------------------------------------------------------
        // Key colours (interpolated across the day)
        // --------------------------------------------------------------------

        [Header("Sky colours")]
        [SerializeField] private Color skyMidnight = new Color(0.03f, 0.04f, 0.09f);
        [SerializeField] private Color skyDawnDusk = new Color(0.85f, 0.50f, 0.35f);
        [SerializeField] private Color skyNoon     = new Color(0.53f, 0.75f, 0.92f);

        [Header("Sun")]
        [SerializeField] private Color sunDay      = new Color(1.00f, 0.96f, 0.86f);
        [SerializeField] private Color sunDawnDusk = new Color(1.00f, 0.70f, 0.45f);
        [SerializeField, Range(0f, 3f)] private float sunMaxIntensity = 1.25f;
        [SerializeField, Range(0f, 1f)] private float moonMinIntensity = 0.06f;

        [Header("Ambient")]
        [SerializeField, Range(0f, 1f)] private float ambientDay = 0.55f;
        [SerializeField, Range(0f, 1f)] private float ambientNight = 0.10f;

        // --------------------------------------------------------------------
        // State
        // --------------------------------------------------------------------

        private float time01;
        private Light sun;

        /// <summary>Current normalized time of day, 0..1.</summary>
        public float TimeOfDay01 => time01;

        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Awake()
        {
            time01 = Mathf.Repeat(startTime01, 1f);
            EnsureSun();

            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.ambientMode = AmbientMode.Flat;

            ApplyVisuals();
        }

        private void Update()
        {
            if (!freezeTime)
            {
                float speed = 1f;
                if (fastForwardKey != KeyCode.None && Input.GetKey(fastForwardKey))
                    speed = 10f;

                time01 += (Time.deltaTime / dayLengthSeconds) * speed;
                time01 = Mathf.Repeat(time01, 1f);
            }

            ApplyVisuals();
        }

        // --------------------------------------------------------------------
        // Sun setup
        // --------------------------------------------------------------------

        private void EnsureSun()
        {
            // Reuse an existing directional light if one is present.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }

            if (sun == null)
            {
                var go = new GameObject("Sun");
                go.transform.SetParent(transform, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.shadows = LightShadows.Soft;
        }

        // --------------------------------------------------------------------
        // The heart of the system — map time → all visual properties.
        // --------------------------------------------------------------------

        private void ApplyVisuals()
        {
            // 1. Sun rotation.
            //    At t=0.25 (sunrise) the sun should be at the eastern horizon.
            //    At t=0.50 (noon) it should be overhead.
            //    We rotate around X: elevation = sin over the day.
            float sunAngle = (time01 * 360f) - 90f; // 0.25 -> 0° (horizon), 0.5 -> 90° (up)
            sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

            // 2. Sun elevation factor: 1 at noon, 0 at horizon, negative at night.
            float elevation = Mathf.Sin(time01 * Mathf.PI * 2f - Mathf.PI * 0.5f);
            // Remap so day (elevation>0) drives brightness; clamp night to 0.
            float dayFactor = Mathf.Clamp01(elevation);            // 0 at/below horizon, 1 at noon
            float twilight  = Mathf.Clamp01(1f - Mathf.Abs(elevation) * 3f); // peak near horizon

            // 3. Sky colour: blend midnight → dawn/dusk → noon.
            Color sky;
            if (elevation <= 0f)
            {
                // Night to twilight.
                sky = Color.Lerp(skyMidnight, skyDawnDusk, twilight);
            }
            else
            {
                // Twilight to full day.
                sky = Color.Lerp(skyDawnDusk, skyNoon, dayFactor);
            }

            // 4. Apply sky to all cameras + fog.
            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = sky;
            }
            RenderSettings.fogColor = sky;

            // 5. Sun colour + intensity.
            sun.color = Color.Lerp(sunDawnDusk, sunDay, dayFactor);
            float intensity = Mathf.Lerp(moonMinIntensity, sunMaxIntensity, dayFactor);
            sun.intensity = intensity;

            // 6. Ambient light.
            float ambient = Mathf.Lerp(ambientNight, ambientDay, dayFactor);
            RenderSettings.ambientLight = sky * ambient;
        }

        // --------------------------------------------------------------------
        // Optional on-screen clock
        // --------------------------------------------------------------------

        private void OnGUI()
        {
            if (!showClock) return;

            // Convert t to a 24h clock. t=0 -> 00:00, t=0.5 -> 12:00.
            float hoursFloat = time01 * 24f;
            int hours = Mathf.FloorToInt(hoursFloat);
            int minutes = Mathf.FloorToInt((hoursFloat - hours) * 60f);

            string phase = PhaseName(time01);
            string label = $"🕐 {hours:D2}:{minutes:D2}  ({phase})";

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
            };

            var rect = new Rect(10, Screen.height - 34, 260, 24);
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(rect.x + 6, rect.y, rect.width - 6, rect.height), label, style);
        }

        private static string PhaseName(float t)
        {
            if (t < 0.20f || t >= 0.80f) return "Night";
            if (t < 0.30f) return "Dawn";
            if (t < 0.70f) return "Day";
            return "Dusk";
        }

        // --------------------------------------------------------------------
        // Public API (useful for future features — e.g. mobs spawn at night)
        // --------------------------------------------------------------------

        public bool IsNight => time01 < 0.22f || time01 >= 0.78f;

        public void SetTime(float t) => time01 = Mathf.Repeat(t, 1f);
    }
}
