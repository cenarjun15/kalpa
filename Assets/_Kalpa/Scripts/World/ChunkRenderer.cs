// ============================================================================
// ChunkRenderer.cs  (Phase 9 — two materials: opaque + transparent)
// ----------------------------------------------------------------------------
// The chunk mesh now has two submeshes (0 opaque, 1 transparent), so the
// MeshRenderer gets two materials in matching order.
// ============================================================================

using Kalpa.Blocks;
using UnityEngine;

namespace Kalpa.World
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ChunkRenderer : MonoBehaviour
    {
        private Chunk chunk;
        private VoxelWorld world;
        private BlockRegistry registry;
        private BlockMaterialCache materials;
        private ChunkMeshBuilder builder;
        private BlockTextureAtlas atlas;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        public void Initialise(Chunk chunk, VoxelWorld world, BlockRegistry registry,
                               BlockMaterialCache materials, ChunkMeshBuilder builder,
                               BlockTextureAtlas atlas)
        {
            this.chunk     = chunk;
            this.world     = world;
            this.registry  = registry;
            this.materials = materials;
            this.builder   = builder;
            this.atlas     = atlas;

            meshFilter   = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            mesh = new Mesh { name = $"ChunkMesh_{chunk.Coordinate.X}_{chunk.Coordinate.Z}" };
            meshFilter.sharedMesh = mesh;

            // Two materials matching the two submeshes.
            meshRenderer.sharedMaterials = new[]
            {
                materials.AtlasMaterial,             // submesh 0 — opaque
                materials.TransparentAtlasMaterial,  // submesh 1 — transparent
            };

            transform.position = new Vector3(chunk.Coordinate.WorldOriginX, 0f,
                                             chunk.Coordinate.WorldOriginZ);
            name = $"Chunk_{chunk.Coordinate.X}_{chunk.Coordinate.Z}";
        }

        public void Rebuild()
        {
            if (chunk == null) return;

            var meshData = builder.Build(chunk, world, registry, atlas);
            meshData.ApplyTo(mesh);

            chunk.IsDirty = false;
        }

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
