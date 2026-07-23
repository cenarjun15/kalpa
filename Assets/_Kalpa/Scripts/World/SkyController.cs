// ============================================================================
// SkyController.cs
// ----------------------------------------------------------------------------
// Sets up sky colour, fog, ambient lighting, and a warmer directional "sun"
// entirely from code. No skybox material or lighting settings required.
//
// Why do it in code?
//   * No dependency on the Lighting window (which behaves differently across
//     Unity versions and render pipelines).
//   * All tuning lives in one file — designers can iterate on colour without
//     hunting through project settings.
//   * Guaranteed identical look in editor and standalone builds.
// ============================================================================

using UnityEngine;
using UnityEngine.Rendering;

namespace Kalpa.World
{
    /// <summary>
    /// Applies sky/fog/ambient/sun settings on Awake.
    /// Attach to any GameObject in the scene.
    /// </summary>
    [DefaultExecutionOrder(-800)] // run before rendering starts
    public sealed class SkyController : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Inspector
        // --------------------------------------------------------------------

        [Header("Sky")]

        [Tooltip("Solid-colour sky. Camera clears to this colour every frame.")]
        [SerializeField] private Color skyColor = new Color(0.53f, 0.75f, 0.92f, 1f); // warm blue

        [Tooltip("Match fog colour to sky colour so the horizon blends seamlessly.")]
        [SerializeField] private bool syncFogWithSky = true;

        [Header("Fog")]

        [SerializeField] private bool enableFog = true;
        [SerializeField] private FogMode fogMode = FogMode.Linear;
        [SerializeField] private Color fogColor = new Color(0.53f, 0.75f, 0.92f, 1f);

        [Tooltip("Fog starts at this distance (Linear mode only).")]
        [SerializeField] private float fogStart = 32f;

        [Tooltip("Fog fully occludes at this distance (Linear mode only).")]
        [SerializeField] private float fogEnd = 96f;

        [Header("Ambient light")]

        [Tooltip("Slight warm tint keeps shadowed sides from going pure grey.")]
        [SerializeField] private Color ambientColor = new Color(0.55f, 0.55f, 0.58f, 1f);

        [Tooltip("Ambient light multiplier — small so shadows still read as shadows.")]
        [SerializeField, Range(0f, 2f)] private float ambientIntensity = 0.55f;

        [Header("Sun")]

        [Tooltip("If assigned, tunes this Light to warm sunlight. If null, one is created.")]
        [SerializeField] private Light sunLight;

        [SerializeField] private Color sunColor = new Color(1f, 0.96f, 0.86f, 1f); // pale gold
        [SerializeField, Range(0f, 3f)] private float sunIntensity = 1.2f;
        [SerializeField] private Vector3 sunRotationEuler = new Vector3(45f, 30f, 0f);

        // --------------------------------------------------------------------
        // Apply on Awake so first frame already looks right.
        // --------------------------------------------------------------------

        private void Awake()
        {
            ApplyCameraBackground();
            ApplyFog();
            ApplyAmbient();
            ApplySun();
        }

        // Re-apply if the inspector is edited during play (nice for tuning).
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ApplyCameraBackground();
            ApplyFog();
            ApplyAmbient();
            ApplySun();
        }

        // --------------------------------------------------------------------
        // Camera background
        // --------------------------------------------------------------------

        private void ApplyCameraBackground()
        {
            // Apply to every camera in the scene (typically just Player + editor cam).
            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColor;
            }
        }

        // --------------------------------------------------------------------
        // Fog
        // --------------------------------------------------------------------

        private void ApplyFog()
        {
            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = syncFogWithSky ? skyColor : fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        // --------------------------------------------------------------------
        // Ambient
        // --------------------------------------------------------------------

        private void ApplyAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor * ambientIntensity;
        }

        // --------------------------------------------------------------------
        // Sun
        // --------------------------------------------------------------------

        private void ApplySun()
        {
            if (sunLight == null)
            {
                // Try to find an existing directional light first.
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional) { sunLight = l; break; }
                }

                // Nothing found — create one.
                if (sunLight == null)
                {
                    var go = new GameObject("Sun");
                    go.transform.SetParent(transform, false);
                    sunLight = go.AddComponent<Light>();
                    sunLight.type = LightType.Directional;
                }
            }

            sunLight.color = sunColor;
            sunLight.intensity = sunIntensity;
            sunLight.transform.rotation = Quaternion.Euler(sunRotationEuler);
            sunLight.shadows = LightShadows.Soft;
        }
    }
}
