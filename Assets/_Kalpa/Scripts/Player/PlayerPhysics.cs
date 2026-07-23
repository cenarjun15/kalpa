// ============================================================================
// PlayerPhysics.cs
// ----------------------------------------------------------------------------
// Axis-aligned bounding box (AABB) collision against the voxel world.
// Runs independently of Unity's Rigidbody system — voxel games need custom
// physics because generic PhysX colliders per block would be catastrophically
// slow.
// ============================================================================

using Kalpa.Core;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.Player
{
    /// <summary>
    /// Handles collision + gravity for a player represented as an axis-aligned box.
    /// Not a MonoBehaviour — driven by PlayerController each frame.
    /// </summary>
    public sealed class PlayerPhysics
    {
        private readonly VoxelWorld world;

        /// <summary>True if the player was on solid ground after the last step.</summary>
        public bool IsGrounded { get; private set; }

        public PlayerPhysics(VoxelWorld world)
        {
            this.world = world;
        }

        // --------------------------------------------------------------------
        // Player AABB
        //   Position is FEET position (not centre).
        //   Box extends: X ± halfWidth, Y = [0, height], Z ± halfWidth.
        // --------------------------------------------------------------------

        private const float HalfWidth = GameConstants.PlayerWidth * 0.5f;

        /// <summary>
        /// Step the player. Applies velocity on each axis with sweep-and-slide
        /// collision — X first, then Z, then Y — so you don't tunnel through blocks.
        /// </summary>
        public void Step(ref Vector3 position, ref Vector3 velocity, float deltaTime)
        {
            // Apply gravity to Y velocity.
            velocity.y -= GameConstants.Gravity * deltaTime;
            if (velocity.y < -GameConstants.TerminalVelocity)
                velocity.y = -GameConstants.TerminalVelocity;

            // Movement this frame.
            Vector3 delta = velocity * deltaTime;

            // Move X.
            Vector3 attempt = position + new Vector3(delta.x, 0, 0);
            if (!IntersectsWorld(attempt))
                position.x = attempt.x;
            else
                velocity.x = 0f;

            // Move Z.
            attempt = position + new Vector3(0, 0, delta.z);
            if (!IntersectsWorld(attempt))
                position.z = attempt.z;
            else
                velocity.z = 0f;

            // Move Y. Groundedness derived here.
            attempt = position + new Vector3(0, delta.y, 0);
            if (!IntersectsWorld(attempt))
            {
                position.y = attempt.y;
                IsGrounded = false;
            }
            else
            {
                if (velocity.y < 0f) IsGrounded = true;
                velocity.y = 0f;
            }
        }

        // --------------------------------------------------------------------
        // Collision check — does the player's AABB intersect any solid block?
        // --------------------------------------------------------------------

        private bool IntersectsWorld(Vector3 feetPos)
        {
            float minX = feetPos.x - HalfWidth;
            float maxX = feetPos.x + HalfWidth;
            float minY = feetPos.y;
            float maxY = feetPos.y + GameConstants.PlayerHeight;
            float minZ = feetPos.z - HalfWidth;
            float maxZ = feetPos.z + HalfWidth;

            int x0 = Mathf.FloorToInt(minX);
            int x1 = Mathf.FloorToInt(maxX - 0.0001f);
            int y0 = Mathf.FloorToInt(minY);
            int y1 = Mathf.FloorToInt(maxY - 0.0001f);
            int z0 = Mathf.FloorToInt(minZ);
            int z1 = Mathf.FloorToInt(maxZ - 0.0001f);

            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                byte id = world.GetBlock(x, y, z);
                if (id != GameConstants.AirBlockId)
                {
                    // Phase 3 assumes every non-air block is solid.
                    // Phase 4+ can query BlockRegistry for IsSolid per block.
                    return true;
                }
            }

            return false;
        }
    }
}
