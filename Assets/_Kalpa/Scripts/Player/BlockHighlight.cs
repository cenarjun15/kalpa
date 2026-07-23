// ============================================================================
// BlockHighlight.cs
// ----------------------------------------------------------------------------
// A wireframe cube that shows which block the player is targeting.
// Auto-creates its own mesh & renderer on Awake — no scene setup needed.
// ============================================================================

using UnityEngine;

namespace Kalpa.Player
{
    /// <summary>
    /// Draws a subtle wireframe outline around the currently-targeted block.
    /// </summary>
    public sealed class BlockHighlight : MonoBehaviour
    {
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private float lineWidth = 0.02f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private void Awake()
        {
            meshFilter   = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = BuildWireCubeMesh(lineWidth);

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", lineColor);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", lineColor);
            meshRenderer.sharedMaterial = mat;

            Hide();
        }

        /// <summary>Move the highlight to a block position (block occupies (pos, pos+1)).</summary>
        public void ShowAt(Vector3 blockOrigin)
        {
            transform.position = blockOrigin;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // --------------------------------------------------------------------
        // Build a 12-edge "fat wire" mesh — thin boxes along each cube edge.
        // Chosen over LineRenderer for consistent line thickness at any distance.
        // --------------------------------------------------------------------

        private static Mesh BuildWireCubeMesh(float thickness)
        {
            var mesh = new Mesh { name = "BlockHighlightWire" };

            // 8 corners of the unit cube.
            var corners = new Vector3[8]
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(1, 0, 1), new Vector3(0, 0, 1),
                new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                new Vector3(1, 1, 1), new Vector3(0, 1, 1),
            };

            // 12 edges as (from, to) index pairs.
            int[,] edges = {
                {0,1},{1,2},{2,3},{3,0},   // bottom loop
                {4,5},{5,6},{6,7},{7,4},   // top loop
                {0,4},{1,5},{2,6},{3,7},   // verticals
            };

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris  = new System.Collections.Generic.List<int>();

            for (int e = 0; e < 12; e++)
            {
                Vector3 a = corners[edges[e, 0]];
                Vector3 b = corners[edges[e, 1]];
                AddEdgeBox(verts, tris, a, b, thickness);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddEdgeBox(System.Collections.Generic.List<Vector3> verts,
                                       System.Collections.Generic.List<int> tris,
                                       Vector3 a, Vector3 b, float thickness)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 up  = Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.forward;
            Vector3 right = Vector3.Cross(dir, up).normalized * thickness * 0.5f;
            Vector3 forward = Vector3.Cross(dir, right).normalized * thickness * 0.5f;

            // 8 corners of the thin box around the edge.
            var box = new Vector3[8]
            {
                a - right - forward, a + right - forward,
                a + right + forward, a - right + forward,
                b - right - forward, b + right - forward,
                b + right + forward, b - right + forward,
            };

            int baseIdx = verts.Count;
            for (int i = 0; i < 8; i++) verts.Add(box[i]);

            // 6 faces × 2 triangles.
            int[] faceIdx = {
                0,1,2, 0,2,3,   // -Z end
                4,6,5, 4,7,6,   // +Z end
                0,4,5, 0,5,1,   // bottom
                3,2,6, 3,6,7,   // top
                0,3,7, 0,7,4,   // -X side
                1,5,6, 1,6,2,   // +X side
            };
            for (int i = 0; i < faceIdx.Length; i++) tris.Add(baseIdx + faceIdx[i]);
        }
    }
}
