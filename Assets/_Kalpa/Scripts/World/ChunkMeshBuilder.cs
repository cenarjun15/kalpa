// ============================================================================
// ChunkMeshBuilder.cs
// ----------------------------------------------------------------------------
// Turns a Chunk into a Unity Mesh with:
//   * Face culling — a face is emitted only if its neighbour is air/transparent.
//   * Submeshes per block type — one material per submesh, so each block gets
//     its own colour without needing per-vertex colours or texture atlases.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using Kalpa.Utils;
using UnityEngine;

namespace Kalpa.World
{
    /// <summary>Result of a mesh build. Apply to a Unity Mesh via <see cref="ApplyTo"/>.</summary>
    public struct ChunkMeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[][] SubmeshTriangles; // one int[] per submesh
        public byte[] SubmeshBlockIds;   // parallel to SubmeshTriangles — the block ID that submesh represents

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            if (Vertices == null || Vertices.Length == 0) return;

            // Use 32-bit indices for chunks with many faces.
            mesh.indexFormat = Vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, UVs);
            mesh.subMeshCount = SubmeshTriangles.Length;
            for (int i = 0; i < SubmeshTriangles.Length; i++)
                mesh.SetTriangles(SubmeshTriangles[i], i);

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

        // The 6 face directions.
        private enum Face { Right = 0, Left = 1, Top = 2, Bottom = 3, Forward = 4, Back = 5 }

        // Offsets to the neighbour block for each face.
        private static readonly (int dx, int dy, int dz)[] FaceOffsets = new (int, int, int)[]
        {
            ( 1,  0,  0), // Right  (+X)
            (-1,  0,  0), // Left   (-X)
            ( 0,  1,  0), // Top    (+Y)
            ( 0, -1,  0), // Bottom (-Y)
            ( 0,  0,  1), // Forward(+Z)
            ( 0,  0, -1), // Back   (-Z)
        };

        // The 4 corner vertices of each face (in local block space, block at 0,0,0
        // occupies the cube from (0,0,0) to (1,1,1)). Wound counter-clockwise
        // when viewed from OUTSIDE the block so normals point outward.
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

        private static readonly Vector2[] FaceUVs = new Vector2[4]
        {
            new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0)
        };

        // --------------------------------------------------------------------
        // Reusable buffers (kept between builds to reduce GC pressure)
        // --------------------------------------------------------------------

        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Vector3> normals  = new List<Vector3>(4096);
        private readonly List<Vector2> uvs      = new List<Vector2>(4096);

        // block ID → triangle-index list for that submesh
        private readonly Dictionary<byte, List<int>> triangleLists = new Dictionary<byte, List<int>>();

        // --------------------------------------------------------------------
        // Build
        // --------------------------------------------------------------------

        /// <summary>
        /// Build mesh data for a chunk.
        /// Reads neighbour blocks from <paramref name="world"/> so cross-chunk borders
        /// are culled correctly against adjacent loaded chunks.
        /// </summary>
        public ChunkMeshData Build(Chunk chunk, VoxelWorld world, BlockRegistry registry)
        {
            // Clear buffers for reuse.
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            foreach (var list in triangleLists.Values) list.Clear();

            int originX = chunk.Coordinate.WorldOriginX;
            int originZ = chunk.Coordinate.WorldOriginZ;

            // Iterate every block in the chunk.
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

                // Check each of the 6 faces.
                for (int f = 0; f < 6; f++)
                {
                    var (dx, dy, dz) = FaceOffsets[f];
                    byte neighbourId = GetBlockForCulling(chunk, world, x + dx, y + dy, z + dz,
                                                         worldX + dx, worldZ + dz);

                    if (ShouldRenderFace(neighbourId, registry))
                    {
                        AddFace(x, y, z, f, id);
                    }
                }
            }

            return Finalise();
        }

        // --------------------------------------------------------------------
        // Neighbour lookup — prefer local chunk (fast path), fall back to world.
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
        // Face emission
        // --------------------------------------------------------------------

        private void AddFace(int x, int y, int z, int faceIndex, byte blockId)
        {
            var origin = new Vector3(x, y, z);
            var normal = FaceNormals[faceIndex];

            int start = vertices.Count;

            for (int i = 0; i < 4; i++)
            {
                vertices.Add(FaceVertices[faceIndex, i] + origin);
                normals.Add(normal);
                uvs.Add(FaceUVs[i]);
            }

            var tris = GetTriangleList(blockId);
            tris.Add(start);
            tris.Add(start + 1);
            tris.Add(start + 2);
            tris.Add(start);
            tris.Add(start + 2);
            tris.Add(start + 3);
        }

        private List<int> GetTriangleList(byte blockId)
        {
            if (!triangleLists.TryGetValue(blockId, out var list))
            {
                list = new List<int>(1024);
                triangleLists[blockId] = list;
            }
            return list;
        }

        // --------------------------------------------------------------------
        // Package result
        // --------------------------------------------------------------------

        private ChunkMeshData Finalise()
        {
            // Collect submeshes that actually had triangles this build.
            var activeIds = new List<byte>(triangleLists.Count);
            foreach (var kv in triangleLists)
                if (kv.Value.Count > 0) activeIds.Add(kv.Key);

            var submeshTris = new int[activeIds.Count][];
            var submeshIds  = new byte[activeIds.Count];
            for (int i = 0; i < activeIds.Count; i++)
            {
                submeshIds[i]  = activeIds[i];
                submeshTris[i] = triangleLists[activeIds[i]].ToArray();
            }

            return new ChunkMeshData
            {
                Vertices          = vertices.ToArray(),
                Normals           = normals.ToArray(),
                UVs               = uvs.ToArray(),
                SubmeshTriangles  = submeshTris,
                SubmeshBlockIds   = submeshIds,
            };
        }
    }
}
