// ============================================================================
// VoxelRaycaster.cs
// ----------------------------------------------------------------------------
// Amanatides & Woo (1987) fast voxel traversal.
// Given an origin + direction + max distance, marches through the world one
// voxel at a time until it hits a solid block. Also reports the face that was
// hit so we know where to place a new block.
// ============================================================================

using Kalpa.Core;
using Kalpa.Utils;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.Player
{
    /// <summary>
    /// Result of a voxel raycast.
    /// Hit = true means Position holds the hit block, NormalFace holds the face.
    /// </summary>
    public struct VoxelHit
    {
        public bool Hit;
        public BlockPosition Position;     // block that was hit
        public BlockPosition NormalFace;   // direction of the face (unit vector as ints)
        public byte BlockId;
    }

    /// <summary>
    /// Fast digital-differential-analyzer-style voxel raycaster.
    /// Not a MonoBehaviour — used by PlayerController.
    /// </summary>
    public sealed class VoxelRaycaster
    {
        private readonly VoxelWorld world;

        public VoxelRaycaster(VoxelWorld world)
        {
            this.world = world;
        }

        /// <summary>
        /// Cast a ray. Returns the first non-air block hit within <paramref name="maxDistance"/>.
        /// </summary>
        public VoxelHit Cast(Vector3 origin, Vector3 direction, float maxDistance)
        {
            direction = direction.normalized;

            // Current voxel we're stepping through.
            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int z = Mathf.FloorToInt(origin.z);

            // Step direction (+1 or -1) per axis.
            int stepX = direction.x > 0 ? 1 : -1;
            int stepY = direction.y > 0 ? 1 : -1;
            int stepZ = direction.z > 0 ? 1 : -1;

            // Distance along ray to next voxel boundary on each axis.
            float tDeltaX = direction.x != 0 ? Mathf.Abs(1f / direction.x) : float.PositiveInfinity;
            float tDeltaY = direction.y != 0 ? Mathf.Abs(1f / direction.y) : float.PositiveInfinity;
            float tDeltaZ = direction.z != 0 ? Mathf.Abs(1f / direction.z) : float.PositiveInfinity;

            float tMaxX = direction.x != 0
                ? ((stepX > 0 ? (x + 1) : x) - origin.x) / direction.x
                : float.PositiveInfinity;
            float tMaxY = direction.y != 0
                ? ((stepY > 0 ? (y + 1) : y) - origin.y) / direction.y
                : float.PositiveInfinity;
            float tMaxZ = direction.z != 0
                ? ((stepZ > 0 ? (z + 1) : z) - origin.z) / direction.z
                : float.PositiveInfinity;

            // Which axis we last stepped through — that tells us the hit face.
            int lastAxis = -1;

            // Check the starting voxel first (in case origin is already inside one).
            byte startId = world.GetBlock(x, y, z);
            if (startId != GameConstants.AirBlockId)
            {
                return new VoxelHit
                {
                    Hit = true,
                    Position = new BlockPosition(x, y, z),
                    NormalFace = BlockPosition.Zero,
                    BlockId = startId,
                };
            }

            // Iterate — but bound iterations to avoid infinite loops on math edge cases.
            int maxIterations = Mathf.CeilToInt(maxDistance) * 3 + 10;

            for (int i = 0; i < maxIterations; i++)
            {
                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    if (tMaxX > maxDistance) return default;
                    x += stepX;
                    tMaxX += tDeltaX;
                    lastAxis = 0;
                }
                else if (tMaxY < tMaxZ)
                {
                    if (tMaxY > maxDistance) return default;
                    y += stepY;
                    tMaxY += tDeltaY;
                    lastAxis = 1;
                }
                else
                {
                    if (tMaxZ > maxDistance) return default;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                    lastAxis = 2;
                }

                byte id = world.GetBlock(x, y, z);
                if (id != GameConstants.AirBlockId)
                {
                    // Compute the face normal — opposite of the axis we just stepped along.
                    BlockPosition normal = BlockPosition.Zero;
                    if (lastAxis == 0) normal = new BlockPosition(-stepX, 0, 0);
                    else if (lastAxis == 1) normal = new BlockPosition(0, -stepY, 0);
                    else normal = new BlockPosition(0, 0, -stepZ);

                    return new VoxelHit
                    {
                        Hit = true,
                        Position = new BlockPosition(x, y, z),
                        NormalFace = normal,
                        BlockId = id,
                    };
                }
            }

            return default;
        }
    }
}
