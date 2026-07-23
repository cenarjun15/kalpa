// ============================================================================
// PlayerInput.cs  (Phase 4B update)
// ----------------------------------------------------------------------------
// Now also aware of the PauseMenu — when the menu is open, input is zeroed and
// the cursor is left free for menu interaction.
// ============================================================================

using Kalpa.UI;
using UnityEngine;

namespace Kalpa.Player
{
    /// <summary>
    /// Snapshots input each frame into a struct that other systems read.
    /// Nothing else in the game should call Input.* directly.
    /// </summary>
    public sealed class PlayerInput : MonoBehaviour
    {
        // --------------------------------------------------------------------
        // Current input snapshot (public read-only)
        // --------------------------------------------------------------------

        public struct InputState
        {
            public float MoveForward;   // -1..1  (W/S)
            public float MoveRight;     // -1..1  (D/A)
            public bool  Jump;
            public bool  Sprint;
            public float MouseDeltaX;   // pixels
            public float MouseDeltaY;   // pixels

            public bool  BreakPressed;
            public bool  PlacePressed;

            public int   HotbarSelection;
        }

        public InputState State { get; private set; }

        // --------------------------------------------------------------------
        // Cursor lock
        // --------------------------------------------------------------------

        [Header("Cursor Lock")]
        [Tooltip("If true, cursor auto-locks when the game view is focused.")]
        [SerializeField] private bool autoLockOnClick = true;

        private bool cursorLocked;
        private PauseMenu pauseMenu;

        private void Start()
        {
            pauseMenu = Object.FindFirstObjectByType<PauseMenu>();
            SetCursorLocked(true);
        }

        private void Update()
        {
            // If pause menu is open, DON'T lock cursor and DON'T register gameplay input.
            bool paused = pauseMenu != null && pauseMenu.IsOpen;
            if (paused)
            {
                cursorLocked = false;
                State = default; // zero everything
                return;
            }

            HandleCursorLock();

            var s = new InputState
            {
                MoveForward     = Input.GetAxisRaw("Vertical"),
                MoveRight       = Input.GetAxisRaw("Horizontal"),
                Jump            = Input.GetKey(KeyCode.Space),
                Sprint          = Input.GetKey(KeyCode.LeftShift),
                MouseDeltaX     = cursorLocked ? Input.GetAxisRaw("Mouse X") : 0f,
                MouseDeltaY     = cursorLocked ? Input.GetAxisRaw("Mouse Y") : 0f,
                BreakPressed    = cursorLocked && Input.GetMouseButtonDown(0),
                PlacePressed    = cursorLocked && Input.GetMouseButtonDown(1),
                HotbarSelection = ReadHotbarKey(),
            };

            State = s;
        }

        private int ReadHotbarKey()
        {
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i)) return i;
            }
            return 0;
        }

        private void HandleCursorLock()
        {
            // ESC is now handled by PauseMenu, not here.
            if (autoLockOnClick && Input.GetMouseButtonDown(0) && !cursorLocked)
            {
                SetCursorLocked(true);
            }
        }

        private void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }
    }
}
