// ============================================================================
// ChunkMeshBuilder.cs  (Phase 11 — three submeshes: opaque/transparent/cutout)
// ----------------------------------------------------------------------------
// submesh 0 = opaque       (AtlasMaterial, atlas UVs)
// submesh 1 = transparent  (TransparentAtlasMaterial, atlas UVs)
// submesh 2 = cutout        (CutoutMaterial, FULL 0..1 UVs → leaf texture)
//
// Cutout faces use full-tile UVs because the cutout material samples the leaf
// texture directly (it needs the per-pixel alpha the atlas can't store).
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    public struct ChunkMeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[] OpaqueTriangles;
        public int[] TransparentTriangles;
        public int[] CutoutTriangles;

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            if (Vertices == null || Vertices.Length == 0) { mesh.subMeshCount = 0; return; }

            mesh.indexFormat = Vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, UVs);

            mesh.subMeshCount = 3;
            mesh.SetTriangles(OpaqueTriangles ?? System.Array.Empty<int>(), 0);
            mesh.SetTriangles(TransparentTriangles ?? System.Array.Empty<int>(), 1);
            mesh.SetTriangles(CutoutTriangles ?? System.Array.Empty<int>(), 2);

            mesh.RecalculateBounds();
        }
    }

    public sealed class ChunkMeshBuilder
    {
        private const int Size = GameConstants.ChunkSize;
        private const int Height = GameConstants.ChunkHeight;

        private static readonly (int dx, int dy, int dz)[] FaceOffsets =
        {
            ( 1, 0, 0), (-1, 0, 0), ( 0, 1, 0), ( 0, -1, 0), ( 0, 0, 1), ( 0, 0, -1),
        };

        private static readonly Vector3[,] FaceVertices = new Vector3[6, 4]
        {
            { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
            { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
            { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
        };

        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        private static readonly Vector2[] FaceUVs =
        {
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)
        };

        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Vector3> normals  = new List<Vector3>(4096);
        private readonly List<Vector2> uvs       = new List<Vector2>(4096);
        private readonly List<int> opaqueTris      = new List<int>(6144);
        private readonly List<int> transparentTris = new List<int>(1024);
        private readonly List<int> cutoutTris      = new List<int>(2048);

        public ChunkMeshData Build(Chunk chunk, VoxelWorld world,
                                   BlockRegistry registry, BlockTextureAtlas atlas)
        {
            vertices.Clear(); normals.Clear(); uvs.Clear();
            opaqueTris.Clear(); transparentTris.Clear(); cutoutTris.Clear();

            int originX = chunk.Coordinate.WorldOriginX;
            int originZ = chunk.Coordinate.WorldOriginZ;

            for (int y = 0; y < Height; y++)
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                byte id = chunk.GetBlockLocal(x, y, z);
                if (id == GameConstants.AirBlockId) continue;

                var data = registry.GetById(id);
                if (data == null || !data.IsSolid) continue;

                int worldX = originX + x;
                int worldZ = originZ + z;
                var uvRect = atlas.GetUV(id);

                RenderKind kind = data.IsCutout ? RenderKind.Cutout
                                 : data.IsTransparent ? RenderKind.Transparent
                                 : RenderKind.Opaque;

                for (int f = 0; f < 6; f++)
                {
                    var (dx, dy, dz) = FaceOffsets[f];
                    byte neighbourId = GetBlockForCulling(chunk, world,
                                                         x + dx, y + dy, z + dz,
                                                         worldX + dx, worldZ + dz);

                    if (ShouldRenderFace(id, neighbourId, registry, kind))
                        AddFace(x, y, z, f, uvRect, kind);
                }
            }

            return new ChunkMeshData
            {
                Vertices             = vertices.ToArray(),
                Normals              = normals.ToArray(),
                UVs                  = uvs.ToArray(),
                OpaqueTriangles      = opaqueTris.ToArray(),
                TransparentTriangles = transparentTris.ToArray(),
                CutoutTriangles      = cutoutTris.ToArray(),
            };
        }

        private enum RenderKind { Opaque, Transparent, Cutout }

        private static byte GetBlockForCulling(Chunk chunk, VoxelWorld world,
                                               int lx, int ly, int lz, int wx, int wz)
        {
            if (lx >= 0 && lx < Size && lz >= 0 && lz < Size && ly >= 0 && ly < Height)
                return chunk.GetBlockLocal(lx, ly, lz);
            return world.GetBlock(wx, ly, wz);
        }

        private static bool ShouldRenderFace(byte currentId, byte neighbourId,
                                             BlockRegistry registry, RenderKind kind)
        {
            if (neighbourId == GameConstants.AirBlockId) return true;

            var neighbour = registry.GetById(neighbourId);
            if (neighbour == null) return true;

            // Cutout foliage: render every face touching air OR a non-same block,
            // so the canopy shows internal leafy structure. Cull only between the
            // same leaf id to avoid overdraw in dense clumps.
            if (kind == RenderKind.Cutout)
                return neighbourId != currentId;

            // Transparent (glass/water).
            if (neighbour.IsTransparent || neighbour.IsCutout || !neighbour.IsSolid)
            {
                if (currentId == neighbourId) return false;
                return true;
            }

            // Opaque solid neighbour → cull.
            return false;
        }

        private void AddFace(int x, int y, int z, int faceIndex, Rect uvRect, RenderKind kind)
        {
            var origin = new Vector3(x, y, z);
            var normal = FaceNormals[faceIndex];
            int start = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(FaceVertices[faceIndex, i] + origin);
                normals.Add(normal);

                var localUV = FaceUVs[i];
                if (kind == RenderKind.Cutout)
                {
                    // Cutout samples the leaf texture directly → full 0..1 UV.
                    uvs.Add(localUV);
                }
                else
                {
                    // Opaque/transparent sample the shared atlas → map into the tile rect.
                    uvs.Add(new Vector2(
                        uvRect.xMin + localUV.x * uvRect.width,
                        uvRect.yMin + localUV.y * uvRect.height));
                }
            }

            List<int> tris = kind == RenderKind.Cutout ? cutoutTris
                           : kind == RenderKind.Transparent ? transparentTris
                           : opaqueTris;

            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }
    }
}
