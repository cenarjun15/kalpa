// ============================================================================
// ChunkRenderer.cs
// ----------------------------------------------------------------------------
// One ChunkRenderer per loaded chunk. Owns the MeshFilter / MeshRenderer, and
// rebuilds the mesh whenever the chunk is dirty.
// ============================================================================

using Kalpa.Blocks;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>
    /// Renders a single chunk. Positioned at the chunk's world origin.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ChunkRenderer : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Dependencies (injected via Initialise)
        // --------------------------------------------------------------------

        private Chunk chunk;
        private VoxelWorld world;
        private BlockRegistry registry;
        private BlockMaterialCache materials;
        private ChunkMeshBuilder builder;

        // --------------------------------------------------------------------
        // Unity components
        // --------------------------------------------------------------------

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        // --------------------------------------------------------------------
        // Public API
        // --------------------------------------------------------------------

        /// <summary>
        /// Set up this renderer for a specific chunk. Positions the GameObject at
        /// the chunk's world origin.
        /// </summary>
        public void Initialise(Chunk chunk, VoxelWorld world, BlockRegistry registry,
                               BlockMaterialCache materials, ChunkMeshBuilder builder)
        {
            this.chunk     = chunk;
            this.world     = world;
            this.registry  = registry;
            this.materials = materials;
            this.builder   = builder;

            meshFilter   = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            mesh = new Mesh { name = $"ChunkMesh_{chunk.Coordinate.X}_{chunk.Coordinate.Z}" };
            meshFilter.sharedMesh = mesh;

            transform.position = new Vector3(chunk.Coordinate.WorldOriginX, 0f,
                                             chunk.Coordinate.WorldOriginZ);
            name = $"Chunk_{chunk.Coordinate.X}_{chunk.Coordinate.Z}";
        }

        /// <summary>
        /// Rebuild the mesh from current chunk data.
        /// Cheap to call — internal builders reuse buffers.
        /// </summary>
        public void Rebuild()
        {
            if (chunk == null) return;

            var meshData = builder.Build(chunk, world, registry);
            meshData.ApplyTo(mesh);

            // Update materials array to match submeshes.
            var mats = new Material[meshData.SubmeshBlockIds.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var blockData = registry.GetById(meshData.SubmeshBlockIds[i]);
                mats[i] = materials.GetMaterial(blockData);
            }
            meshRenderer.sharedMaterials = mats;

            chunk.IsDirty = false;
        }

        /// <summary>Rebuild if the chunk is currently marked dirty.</summary>
        public void RebuildIfDirty()
        {
            if (chunk != null && chunk.IsDirty) Rebuild();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
