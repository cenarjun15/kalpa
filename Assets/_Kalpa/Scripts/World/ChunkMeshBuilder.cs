// ============================================================================
// ChunkMeshBuilder.cs  (Phase 9 — opaque + transparent submeshes)
// ----------------------------------------------------------------------------
// Produces a mesh with TWO submeshes:
//   submesh 0 = opaque faces      (rendered with AtlasMaterial)
//   submesh 1 = transparent faces (rendered with TransparentAtlasMaterial)
//
// Face culling now considers transparency:
//   * Opaque block face   → render unless neighbour is an opaque solid block.
//   * Transparent block face → render unless neighbour is the SAME transparent
//     block id (so a body of water/glass looks solid, with no internal grid).
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>Result of a mesh build. Two submeshes: 0 opaque, 1 transparent.</summary>
    public struct ChunkMeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[]     OpaqueTriangles;
        public int[]     TransparentTriangles;

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            if (Vertices == null || Vertices.Length == 0)
            {
                mesh.subMeshCount = 0;
                return;
            }

            mesh.indexFormat = Vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, UVs);

            mesh.subMeshCount = 2;
            mesh.SetTriangles(OpaqueTriangles ?? System.Array.Empty<int>(), 0);
            mesh.SetTriangles(TransparentTriangles ?? System.Array.Empty<int>(), 1);

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

        public ChunkMeshData Build(Chunk chunk, VoxelWorld world,
                                   BlockRegistry registry, BlockTextureAtlas atlas)
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            opaqueTris.Clear();
            transparentTris.Clear();

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

                bool transparent = data.IsTransparent;
                int worldX = originX + x;
                int worldZ = originZ + z;
                var uvRect = atlas.GetUV(id);

                for (int f = 0; f < 6; f++)
                {
                    var (dx, dy, dz) = FaceOffsets[f];
                    byte neighbourId = GetBlockForCulling(chunk, world,
                                                         x + dx, y + dy, z + dz,
                                                         worldX + dx, worldZ + dz);

                    if (ShouldRenderFace(id, neighbourId, registry))
                        AddFace(x, y, z, f, uvRect, transparent);
                }
            }

            return new ChunkMeshData
            {
                Vertices             = vertices.ToArray(),
                Normals              = normals.ToArray(),
                UVs                  = uvs.ToArray(),
                OpaqueTriangles      = opaqueTris.ToArray(),
                TransparentTriangles = transparentTris.ToArray(),
            };
        }

        private static byte GetBlockForCulling(Chunk chunk, VoxelWorld world,
                                               int lx, int ly, int lz, int wx, int wz)
        {
            if (lx >= 0 && lx < Size && lz >= 0 && lz < Size && ly >= 0 && ly < Height)
                return chunk.GetBlockLocal(lx, ly, lz);
            return world.GetBlock(wx, ly, wz);
        }

        /// <summary>Transparency-aware face culling.</summary>
        private static bool ShouldRenderFace(byte currentId, byte neighbourId, BlockRegistry registry)
        {
            if (neighbourId == GameConstants.AirBlockId) return true;

            var neighbour = registry.GetById(neighbourId);
            if (neighbour == null) return true;

            // See-through neighbour (transparent or non-solid).
            if (neighbour.IsTransparent || !neighbour.IsSolid)
            {
                // Cull the shared face between two blocks of the SAME transparent type,
                // so a mass of water/glass renders as one clean surface (no inner grid).
                if (currentId == neighbourId) return false;
                return true;
            }

            // Opaque solid neighbour → cull.
            return false;
        }

        private void AddFace(int x, int y, int z, int faceIndex, Rect uvRect, bool transparent)
        {
            var origin = new Vector3(x, y, z);
            var normal = FaceNormals[faceIndex];
            int start = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(FaceVertices[faceIndex, i] + origin);
                normals.Add(normal);
                var localUV = FaceUVs[i];
                uvs.Add(new Vector2(
                    uvRect.xMin + localUV.x * uvRect.width,
                    uvRect.yMin + localUV.y * uvRect.height));
            }

            var tris = transparent ? transparentTris : opaqueTris;
            tris.Add(start);
            tris.Add(start + 1);
            tris.Add(start + 2);
            tris.Add(start);
            tris.Add(start + 2);
            tris.Add(start + 3);
        }
    }
}
