// ============================================================================
// PlayerController.cs  (Phase 5B update — audio hooks)
// ----------------------------------------------------------------------------
// Adds sound triggers:
//   * Footsteps while walking on ground (rate scales with speed).
//   * Jump sound when leaving the ground.
//   * Break / place sounds when modifying the world.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Audio;
using Kalpa.Blocks;
using Kalpa.Core;
using Kalpa.Utils;
using Kalpa.World;
using UnityEngine;

namespace Kalpa.Player
{
    /// <summary>
    /// Root MonoBehaviour for the player. Attach to a "Player" GameObject in scene.
    /// Requires a child camera named "PlayerCamera" (or assign it manually).
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Inspector
        // --------------------------------------------------------------------

        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private BlockHighlight highlight;

        [Header("Look")]
        [Range(0.05f, 5f)]
        [SerializeField] private float mouseSensitivity = 1.5f;

        [Header("Spawn")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 70f, 0f);

        [Header("Hotbar (auto-populated from BlockRegistry if empty)")]
        [SerializeField] private string[] hotbarBlocks =
        {
            "kalpa:grass", "kalpa:dirt", "kalpa:stone", "kalpa:sandalwood",
            "kalpa:sand", "kalpa:marble"
        };

        [Header("Footsteps")]
        [Tooltip("Time between footstep sounds while walking.")]
        [SerializeField, Range(0.15f, 0.8f)] private float stepIntervalWalk = 0.45f;
        [Tooltip("Time between footstep sounds while sprinting.")]
        [SerializeField, Range(0.15f, 0.8f)] private float stepIntervalSprint = 0.30f;

        // --------------------------------------------------------------------
        // State
        // --------------------------------------------------------------------

        private PlayerInput input;
        private PlayerPhysics physics;
        private VoxelRaycaster raycaster;

        private Vector3 velocity;
        private float pitch;
        private float yaw;

        private byte[] hotbarBlockIds = new byte[0];
        private int selectedSlot;

        // Audio state.
        private float stepTimer;
        private bool wasGrounded;

        // --------------------------------------------------------------------
        // Public accessors
        // --------------------------------------------------------------------

        public int SelectedSlot => selectedSlot;
        public IReadOnlyList<byte> HotbarBlockIds => hotbarBlockIds;
        public byte SelectedBlockId
            => hotbarBlockIds.Length == 0
                ? GameConstants.AirBlockId
                : hotbarBlockIds[selectedSlot];

        // --------------------------------------------------------------------
        // Bootstrap
        // --------------------------------------------------------------------

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[PlayerController] No GameManager — did you forget to add it to the scene?");
                enabled = false;
                return;
            }

            input     = GetComponent<PlayerInput>() ?? gameObject.AddComponent<PlayerInput>();
            physics   = new PlayerPhysics(gm.World);
            raycaster = new VoxelRaycaster(gm.World);

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
                    Debug.LogError("[PlayerController] No child camera found. Please parent a Camera to this GameObject.");
                    enabled = false;
                    return;
                }
            }

            if (highlight == null)
            {
                var go = new GameObject("BlockHighlight");
                highlight = go.AddComponent<BlockHighlight>();
            }

            BuildHotbar(gm.BlockRegistry);

            transform.position = spawnPosition;
            velocity = Vector3.zero;
            yaw = transform.eulerAngles.y;
            pitch = 0f;
        }

        private void BuildHotbar(BlockRegistry registry)
        {
            var list = new List<byte>();
            foreach (var name in hotbarBlocks)
            {
                var data = registry.GetByName(name);
                if (data != null) list.Add(data.Id);
            }
            hotbarBlockIds = list.ToArray();

            if (hotbarBlockIds.Length == 0)
                Debug.LogWarning("[PlayerController] Hotbar is empty — no matching BlockData found.");
        }

        // --------------------------------------------------------------------
        // Main loop
        // --------------------------------------------------------------------

        private void Update()
        {
            if (input == null) return;
            var s = input.State;

            HandleLook(s);
            HandleMovement(s);
            HandleFootsteps(s);
            HandleHotbar(s);
            HandleTargeting(s);
        }

        // --------------------------------------------------------------------
        // Look
        // --------------------------------------------------------------------

        private void HandleLook(PlayerInput.InputState s)
        {
            yaw   += s.MouseDeltaX * mouseSensitivity * 2f;
            pitch -= s.MouseDeltaY * mouseSensitivity * 2f;
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // --------------------------------------------------------------------
        // Movement
        // --------------------------------------------------------------------

        private void HandleMovement(PlayerInput.InputState s)
        {
            Vector3 wish = new Vector3(s.MoveRight, 0f, s.MoveForward);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            wish = Quaternion.Euler(0f, yaw, 0f) * wish;

            float speed = s.Sprint ? GameConstants.PlayerSprintSpeed : GameConstants.PlayerWalkSpeed;
            velocity.x = wish.x * speed;
            velocity.z = wish.z * speed;

            if (s.Jump && physics.IsGrounded)
            {
                velocity.y = GameConstants.PlayerJumpForce;
                AudioSystem.Instance?.PlayJump();
            }

            Vector3 pos = transform.position;
            physics.Step(ref pos, ref velocity, Time.deltaTime);
            transform.position = pos;

            // Track ground-transition for audio.
            wasGrounded = physics.IsGrounded;

            if (pos.y < -20f)
            {
                transform.position = spawnPosition;
                velocity = Vector3.zero;
            }
        }

        // --------------------------------------------------------------------
        // Footsteps
        // --------------------------------------------------------------------

        private void HandleFootsteps(PlayerInput.InputState s)
        {
            if (!physics.IsGrounded)
            {
                stepTimer = 0f;
                return;
            }

            bool moving = Mathf.Abs(s.MoveForward) > 0.1f || Mathf.Abs(s.MoveRight) > 0.1f;
            if (!moving)
            {
                stepTimer = 0f;
                return;
            }

            float interval = s.Sprint ? stepIntervalSprint : stepIntervalWalk;
            stepTimer += Time.deltaTime;
            if (stepTimer >= interval)
            {
                stepTimer = 0f;
                AudioSystem.Instance?.PlayFootstep(GetGroundCategory());
            }
        }

        /// <summary>Look at the block one below the player and classify it.</summary>
        private BlockCategory GetGroundCategory()
        {
            var gm = GameManager.Instance;
            if (gm == null) return BlockCategory.Natural;

            int gx = Mathf.FloorToInt(transform.position.x);
            int gy = Mathf.FloorToInt(transform.position.y - 0.1f);
            int gz = Mathf.FloorToInt(transform.position.z);
            byte id = gm.World.GetBlock(gx, gy, gz);
            var data = gm.BlockRegistry.GetById(id);
            return data != null ? data.Category : BlockCategory.Natural;
        }

        // --------------------------------------------------------------------
        // Hotbar
        // --------------------------------------------------------------------

        private void HandleHotbar(PlayerInput.InputState s)
        {
            if (s.HotbarSelection > 0 && hotbarBlockIds.Length > 0)
            {
                int idx = Mathf.Clamp(s.HotbarSelection - 1, 0, hotbarBlockIds.Length - 1);
                selectedSlot = idx;
            }
        }

        // --------------------------------------------------------------------
        // Targeting: raycast, highlight, break, place
        // --------------------------------------------------------------------

        private void HandleTargeting(PlayerInput.InputState s)
        {
            Vector3 eyePos = playerCamera.transform.position;
            Vector3 lookDir = playerCamera.transform.forward;

            var hit = raycaster.Cast(eyePos, lookDir, GameConstants.PlayerReach);

            if (hit.Hit)
            {
                highlight.ShowAt(new Vector3(hit.Position.X, hit.Position.Y, hit.Position.Z));

                if (s.BreakPressed)
                {
                    BreakBlock(hit.Position, hit.BlockId);
                }
                else if (s.PlacePressed)
                {
                    var placePos = hit.Position + hit.NormalFace;
                    PlaceBlock(placePos);
                }
            }
            else
            {
                highlight.Hide();
            }
        }

        private void BreakBlock(BlockPosition pos, byte oldBlockId)
        {
            var gm = GameManager.Instance;
            var brokenData = gm.BlockRegistry.GetById(oldBlockId);
            AudioSystem.Instance?.PlayBreak(brokenData);

            gm.World.SetBlock(pos, GameConstants.AirBlockId);
            MarkChunkDirty(pos, gm);
        }

        private void PlaceBlock(BlockPosition pos)
        {
            byte id = SelectedBlockId;
            if (id == GameConstants.AirBlockId) return;
            if (WouldOverlapPlayer(pos)) return;

            var gm = GameManager.Instance;
            gm.World.SetBlock(pos, id);
            MarkChunkDirty(pos, gm);

            var placedData = gm.BlockRegistry.GetById(id);
            AudioSystem.Instance?.PlayPlace(placedData);
        }

        private bool WouldOverlapPlayer(BlockPosition pos)
        {
            const float half = GameConstants.PlayerWidth * 0.5f;

            float pMinX = transform.position.x - half;
            float pMaxX = transform.position.x + half;
            float pMinY = transform.position.y;
            float pMaxY = transform.position.y + GameConstants.PlayerHeight;
            float pMinZ = transform.position.z - half;
            float pMaxZ = transform.position.z + half;

            return pMaxX > pos.X && pMinX < pos.X + 1
                && pMaxY > pos.Y && pMinY < pos.Y + 1
                && pMaxZ > pos.Z && pMinZ < pos.Z + 1;
        }

        private void MarkChunkDirty(BlockPosition pos, GameManager gm)
        {
            var manager = Object.FindFirstObjectByType<ChunkManager>();
            if (manager == null) return;

            RebuildChunkAt(pos.X, pos.Z, manager);

            int size = GameConstants.ChunkSize;
            int localX = ((pos.X % size) + size) % size;
            int localZ = ((pos.Z % size) + size) % size;

            if (localX == 0)          RebuildChunkAt(pos.X - 1, pos.Z, manager);
            if (localX == size - 1)   RebuildChunkAt(pos.X + 1, pos.Z, manager);
            if (localZ == 0)          RebuildChunkAt(pos.X, pos.Z - 1, manager);
            if (localZ == size - 1)   RebuildChunkAt(pos.X, pos.Z + 1, manager);
        }

        private static void RebuildChunkAt(int worldX, int worldZ, ChunkManager manager)
        {
            var coord = ChunkCoordinate.FromWorldXZ(worldX, worldZ);
            var renderer = manager.GetRenderer(coord);
            if (renderer != null) renderer.Rebuild();
        }
    }
}
