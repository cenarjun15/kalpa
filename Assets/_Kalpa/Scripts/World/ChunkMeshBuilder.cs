// ============================================================================
// ChunkMeshBuilder.cs  (Phase 5A update — atlas UVs)
// ----------------------------------------------------------------------------
// Now:
//   * All triangles go into ONE submesh (no per-block-type submeshing).
//   * UVs are sampled from BlockTextureAtlas — each face's 4 UVs point at the
//     block's tile in the atlas.
// One material + one submesh = one draw call per chunk. Massive perf win.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>Result of a mesh build.</summary>
    public struct ChunkMeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[]     Triangles; // single submesh now

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            if (Vertices == null || Vertices.Length == 0) return;

            mesh.indexFormat = Vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, UVs);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// Reusable, allocation-conscious builder. Not thread-safe — one instance per thread.
    /// </summary>
    public sealed class ChunkMeshBuilder
    {
        // --------------------------------------------------------------------
        // Constants
        // --------------------------------------------------------------------

        private const int Size = GameConstants.ChunkSize;
        private const int Height = GameConstants.ChunkHeight;

        // Face directions.
        private enum Face { Right = 0, Left = 1, Top = 2, Bottom = 3, Forward = 4, Back = 5 }

        private static readonly (int dx, int dy, int dz)[] FaceOffsets = new (int, int, int)[]
        {
            ( 1,  0,  0), // Right
            (-1,  0,  0), // Left
            ( 0,  1,  0), // Top
            ( 0, -1,  0), // Bottom
            ( 0,  0,  1), // Forward
            ( 0,  0, -1), // Back
        };

        // Vertices per face, counter-clockwise viewed from outside.
        private static readonly Vector3[,] FaceVertices = new Vector3[6, 4]
        {
            // Right (+X)
            { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            // Left (-X)
            { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
            // Top (+Y)
            { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            // Bottom (-Y)
            { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
            // Forward (+Z)
            { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            // Back (-Z)
            { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
        };

        private static readonly Vector3[] FaceNormals = new Vector3[6]
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
        };

        // UV corner ORDER, in the same winding as FaceVertices (0,0)->(0,1)->(1,1)->(1,0)
        // is mapped to whichever atlas rect a block occupies.
        private static readonly Vector2[] FaceUVs = new Vector2[4]
        {
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)
        };

        // --------------------------------------------------------------------
        // Reusable buffers
        // --------------------------------------------------------------------

        private readonly List<Vector3> vertices  = new List<Vector3>(4096);
        private readonly List<Vector3> normals   = new List<Vector3>(4096);
        private readonly List<Vector2> uvs       = new List<Vector2>(4096);
        private readonly List<int>     triangles = new List<int>(6144);

        // --------------------------------------------------------------------
        // Build
        // --------------------------------------------------------------------

        public ChunkMeshData Build(Chunk chunk, VoxelWorld world,
                                   BlockRegistry registry, BlockTextureAtlas atlas)
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();

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

                for (int f = 0; f < 6; f++)
                {
                    var (dx, dy, dz) = FaceOffsets[f];
                    byte neighbourId = GetBlockForCulling(chunk, world,
                                                         x + dx, y + dy, z + dz,
                                                         worldX + dx, worldZ + dz);

                    if (ShouldRenderFace(neighbourId, registry))
                    {
                        AddFace(x, y, z, f, uvRect);
                    }
                }
            }

            return new ChunkMeshData
            {
                Vertices  = vertices.ToArray(),
                Normals   = normals.ToArray(),
                UVs       = uvs.ToArray(),
                Triangles = triangles.ToArray(),
            };
        }

        // --------------------------------------------------------------------
        // Neighbour lookup
        // --------------------------------------------------------------------

        private static byte GetBlockForCulling(Chunk chunk, VoxelWorld world,
                                               int lx, int ly, int lz,
                                               int wx, int wz)
        {
            if (lx >= 0 && lx < Size && lz >= 0 && lz < Size && ly >= 0 && ly < Height)
                return chunk.GetBlockLocal(lx, ly, lz);
            return world.GetBlock(wx, ly, wz);
        }

        private static bool ShouldRenderFace(byte neighbourId, BlockRegistry registry)
        {
            if (neighbourId == GameConstants.AirBlockId) return true;
            var data = registry.GetById(neighbourId);
            return data == null || data.IsTransparent || !data.IsSolid;
        }

        // --------------------------------------------------------------------
        // Face emission — writes 4 verts + 6 triangle indices, with atlas UVs.
        // --------------------------------------------------------------------

        private void AddFace(int x, int y, int z, int faceIndex, Rect uvRect)
        {
            var origin = new Vector3(x, y, z);
            var normal = FaceNormals[faceIndex];

            int start = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(FaceVertices[faceIndex, i] + origin);
                normals.Add(normal);

                // Map the face's corner UV (0/1 in each axis) into the atlas rect.
                var localUV = FaceUVs[i];
                uvs.Add(new Vector2(
                    uvRect.xMin + localUV.x * uvRect.width,
                    uvRect.yMin + localUV.y * uvRect.height));
            }

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
