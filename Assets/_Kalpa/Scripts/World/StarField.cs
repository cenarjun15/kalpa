// ============================================================================
// StarField.cs  (Phase 10 — fog-immune fix)
// ----------------------------------------------------------------------------
// Procedural night sky: a dome of stars + a moon that follows the camera and
// fades in/out with the DayNightCycle.
//
// IMPORTANT FIX vs first version:
//   * Uses the "Sprites/Default" shader, which ignores scene fog and lighting.
//     The previous URP/Unlit material was being fully occluded by distance fog
//     (star dome radius 400 >> fogEnd ~110), so stars rendered as fog colour
//     and were invisible.
//   * More robust camera acquisition (tags PlayerCamera as MainCamera).
// ============================================================================

using UnityEngine;

namespace Kalpa.World
{
    [DefaultExecutionOrder(-700)]
    public sealed class StarField : MonoBehaviour
    {
        [Header("Stars")]
        [SerializeField, Range(100, 2000)] private int starCount = 800;
        [SerializeField] private float domeRadius = 300f;
        [SerializeField, Range(0f, 4f)] private float starSize = 1.6f;

        [Header("Moon")]
        [SerializeField] private float moonSize = 45f;
        [SerializeField] private Color moonColor = new Color(0.95f, 0.95f, 0.85f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logNightFactor = false;

        private DayNightCycle cycle;
        private Camera cam;

        private GameObject starObj;
        private Material starMat;
        private MeshRenderer starRenderer;

        private GameObject moonObj;
        private Material moonMat;
        private MeshRenderer moonRenderer;

        private void Start()
        {
            cycle = Object.FindFirstObjectByType<DayNightCycle>();
            AcquireCamera();

            BuildStars();
            BuildMoon();
        }

        private void AcquireCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var pc = Object.FindFirstObjectByType<Kalpa.Player.PlayerController>();
                if (pc != null)
                {
                    cam = pc.GetComponentInChildren<Camera>();
                    // Ensure Camera.main works elsewhere too.
                    if (cam != null && cam.gameObject.tag != "MainCamera")
                        cam.gameObject.tag = "MainCamera";
                }
            }
        }

        // --------------------------------------------------------------------
        // Fog-immune material
        // --------------------------------------------------------------------

        private static Material MakeSkyMaterial(string name)
        {
            // Sprites/Default ignores fog + lighting — perfect for sky elements.
            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("UI/Default")
                      ?? Shader.Find("Unlit/Transparent");
            var mat = new Material(shader) { name = name };
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        // --------------------------------------------------------------------
        // Star dome
        // --------------------------------------------------------------------

        private void BuildStars()
        {
            starObj = new GameObject("Stars");
            starObj.transform.SetParent(transform, false);

            var mf = starObj.AddComponent<MeshFilter>();
            starRenderer = starObj.AddComponent<MeshRenderer>();
            starRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;

            var mesh = new Mesh { name = "StarMesh" };
            var verts = new Vector3[starCount * 4];
            var tris  = new int[starCount * 6];
            var cols  = new Color[starCount * 4];
            var uvs   = new Vector2[starCount * 4];

            var rng = new System.Random(12345);
            for (int i = 0; i < starCount; i++)
            {
                float u = (float)rng.NextDouble();
                float v = (float)rng.NextDouble() * 0.5f;
                float theta = u * Mathf.PI * 2f;
                float phi = Mathf.Acos(1f - v);
                Vector3 dir = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta));
                Vector3 center = dir * domeRadius;

                float s = starSize * (0.5f + (float)rng.NextDouble());
                Vector3 right = Vector3.Cross(dir, Vector3.up).normalized * s;
                Vector3 up = Vector3.Cross(right, dir).normalized * s;

                int vb = i * 4;
                verts[vb + 0] = center - right - up;
                verts[vb + 1] = center - right + up;
                verts[vb + 2] = center + right + up;
                verts[vb + 3] = center + right - up;

                float bright = 0.7f + (float)rng.NextDouble() * 0.3f;
                var c = new Color(bright, bright, bright, 1f);
                cols[vb + 0] = cols[vb + 1] = cols[vb + 2] = cols[vb + 3] = c;

                uvs[vb + 0] = new Vector2(0, 0);
                uvs[vb + 1] = new Vector2(0, 1);
                uvs[vb + 2] = new Vector2(1, 1);
                uvs[vb + 3] = new Vector2(1, 0);

                int tb = i * 6;
                tris[tb + 0] = vb + 0; tris[tb + 1] = vb + 1; tris[tb + 2] = vb + 2;
                tris[tb + 3] = vb + 0; tris[tb + 4] = vb + 2; tris[tb + 5] = vb + 3;
            }

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            starMat = MakeSkyMaterial("StarMat");
            starRenderer.sharedMaterial = starMat;
        }

        // --------------------------------------------------------------------
        // Moon
        // --------------------------------------------------------------------

        private void BuildMoon()
        {
            moonObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            moonObj.name = "Moon";
            var col = moonObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            moonObj.transform.SetParent(transform, false);
            moonObj.transform.localScale = Vector3.one * moonSize;

            moonRenderer = moonObj.GetComponent<MeshRenderer>();
            moonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonRenderer.receiveShadows = false;

            moonMat = MakeSkyMaterial("MoonMat");
            moonMat.mainTexture = MakeMoonTexture();
            moonRenderer.sharedMaterial = moonMat;
        }

        private Texture2D MakeMoonTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size / 2f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));
                px[y * size + x] = new Color(1f, 1f, 0.95f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // --------------------------------------------------------------------
        // Follow camera + fade with night
        // --------------------------------------------------------------------

        private void LateUpdate()
        {
            if (cam == null) { AcquireCamera(); if (cam == null) return; }

            transform.position = cam.transform.position;

            float night = ComputeNightFactor();
            if (logNightFactor) Debug.Log($"[StarField] nightFactor={night:F2}");

            // Stars.
            if (starMat != null)
            {
                bool visible = night > 0.01f;
                starObj.SetActive(visible);
                if (visible)
                {
                    var c = new Color(1f, 1f, 1f, night);
                    if (starMat.HasProperty("_Color")) starMat.SetColor("_Color", c);
                }
            }

            // Moon.
            if (moonObj != null)
            {
                bool visible = night > 0.01f;
                moonObj.SetActive(visible);
                if (visible)
                {
                    float t = cycle != null ? cycle.TimeOfDay01 : 0f;
                    float moonAngle = (t * 360f) + 90f;
                    Vector3 moonDir = Quaternion.Euler(moonAngle, 200f, 0f) * Vector3.forward;
                    moonObj.transform.position = cam.transform.position + moonDir * (domeRadius * 0.9f);
                    moonObj.transform.rotation = Quaternion.LookRotation(
                        moonObj.transform.position - cam.transform.position);

                    var mc = moonColor; mc.a = night;
                    if (moonMat.HasProperty("_Color")) moonMat.SetColor("_Color", mc);
                }
            }
        }

        private float ComputeNightFactor()
        {
            if (cycle == null) return 0f;
            float t = cycle.TimeOfDay01;
            float night = 0f;
            if (t < 0.25f)      night = Mathf.InverseLerp(0.25f, 0.10f, t);
            else if (t > 0.75f) night = Mathf.InverseLerp(0.75f, 0.90f, t);
            return Mathf.Clamp01(night);
        }
    }
}
