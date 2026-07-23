// ============================================================================
// PlayerController.cs
// ----------------------------------------------------------------------------
// Ties everything together for the player character.
//
// Responsibilities:
//   * Read input (PlayerInput)
//   * Apply movement + jump + physics (PlayerPhysics)
//   * Aim the first-person camera (mouse look)
//   * Cast rays into the world (VoxelRaycaster)
//   * Break / place blocks and show the highlight
//   * Track which block type is selected via the hotbar
// ============================================================================

using System.Collections.Generic;
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
        [Tooltip("The first-person camera (usually a child of this GameObject).")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Optional block highlight — created automatically if left null.")]
        [SerializeField] private BlockHighlight highlight;

        [Header("Look")]
        [Range(0.05f, 5f)]
        [SerializeField] private float mouseSensitivity = 1.5f;

        [Header("Spawn")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 70f, 0f);

        [Header("Hotbar (auto-populated from BlockRegistry if empty)")]
        [Tooltip("Internal block names that appear in the hotbar, in order.")]
        [SerializeField] private string[] hotbarBlocks =
        {
            "kalpa:grass", "kalpa:dirt", "kalpa:stone", "kalpa:sandalwood"
        };

        // --------------------------------------------------------------------
        // State
        // --------------------------------------------------------------------

        private PlayerInput input;
        private PlayerPhysics physics;
        private VoxelRaycaster raycaster;

        private Vector3 velocity;
        private float pitch;           // camera up/down angle
        private float yaw;             // body left/right angle

        private byte[] hotbarBlockIds = new byte[0];
        private int selectedSlot;

        // --------------------------------------------------------------------
        // Public read-only accessors (used by HUD)
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

            // Auto-find the camera if not assigned.
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

            // Auto-create the highlight if not assigned.
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
            {
                Debug.LogWarning("[PlayerController] Hotbar is empty — no matching BlockData found.");
            }
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
            // Horizontal move — build a vector in body-space, then rotate to world-space.
            Vector3 wish = new Vector3(s.MoveRight, 0f, s.MoveForward);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            wish = Quaternion.Euler(0f, yaw, 0f) * wish;

            float speed = s.Sprint ? GameConstants.PlayerSprintSpeed : GameConstants.PlayerWalkSpeed;
            velocity.x = wish.x * speed;
            velocity.z = wish.z * speed;

            // Jump.
            if (s.Jump && physics.IsGrounded)
                velocity.y = GameConstants.PlayerJumpForce;

            // Step physics.
            Vector3 pos = transform.position;
            physics.Step(ref pos, ref velocity, Time.deltaTime);
            transform.position = pos;

            // Safety net: if the player fell out of the world, respawn.
            if (pos.y < -20f)
            {
                transform.position = spawnPosition;
                velocity = Vector3.zero;
            }
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
                    BreakBlock(hit.Position);
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

        private void BreakBlock(BlockPosition pos)
        {
            var gm = GameManager.Instance;
            gm.World.SetBlock(pos, GameConstants.AirBlockId);
            MarkChunkDirty(pos, gm);
        }

        private void PlaceBlock(BlockPosition pos)
        {
            byte id = SelectedBlockId;
            if (id == GameConstants.AirBlockId) return;

            // Don't place inside the player.
            if (WouldOverlapPlayer(pos)) return;

            var gm = GameManager.Instance;
            gm.World.SetBlock(pos, id);
            MarkChunkDirty(pos, gm);
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

            // Block occupies (pos, pos+1) on all axes.
            return pMaxX > pos.X && pMinX < pos.X + 1
                && pMaxY > pos.Y && pMinY < pos.Y + 1
                && pMaxZ > pos.Z && pMinZ < pos.Z + 1;
        }

        /// <summary>
        /// Rebuild the chunk containing <paramref name="pos"/>, and any neighbouring
        /// chunk if the modification touched a chunk border (for correct face culling).
        /// </summary>
        private void MarkChunkDirty(BlockPosition pos, GameManager gm)
        {
            var manager = FindFirstObjectByType<ChunkManager>();
            if (manager == null) return;

            RebuildChunkAt(pos.X, pos.Z, manager);

            // If we're on a border, neighbouring chunks also need to redraw
            // because their edge faces just changed visibility.
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
