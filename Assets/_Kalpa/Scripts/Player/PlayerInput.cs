// ============================================================================
// PlayerInput.cs  (Mobile update — merges touch + keyboard/mouse)
// ----------------------------------------------------------------------------
// When TouchControls.Active is true, movement/look/actions come from the
// on-screen touch UI. Otherwise, keyboard + mouse as before. Both paths feed
// the same InputState, so all downstream code is unchanged.
// ============================================================================

using Kalpa.UI;
using UnityEngine;

namespace Kalpa.Player
{
    public sealed class PlayerInput : MonoBehaviour
    {
        public struct InputState
        {
            public float MoveForward;
            public float MoveRight;
            public bool  Jump;
            public bool  Sprint;
            public float MouseDeltaX;
            public float MouseDeltaY;
            public bool  BreakPressed;
            public bool  PlacePressed;
            public int   HotbarSelection;
            public int   HotbarScroll;
            public bool  OpenPicker;    // touch "BLOCKS" button
        }

        public InputState State { get; private set; }

        [Header("Cursor Lock (desktop only)")]
        [SerializeField] private bool autoLockOnClick = true;

        private bool cursorLocked;
        private bool wasMenuOpen;
        private PauseMenu pauseMenu;
        private BlockPicker blockPicker;
        private TouchControls touch;

        private void Start()
        {
            pauseMenu = Object.FindFirstObjectByType<PauseMenu>();
            blockPicker = Object.FindFirstObjectByType<BlockPicker>();
            touch = TouchControls.Instance ?? Object.FindFirstObjectByType<TouchControls>();

            if (touch == null || !touch.Active)
                SetCursorLocked(true);
        }

        private void Update()
        {
            bool menuOpen = (pauseMenu != null && pauseMenu.IsOpen)
                         || (blockPicker != null && blockPicker.IsOpen);

            if (menuOpen)
            {
                wasMenuOpen = true;
                cursorLocked = false;
                State = default;
                return;
            }

            bool touchActive = touch != null && touch.Active;

            if (!touchActive && wasMenuOpen)
            {
                wasMenuOpen = false;
                SetCursorLocked(true);
            }
            else if (wasMenuOpen)
            {
                wasMenuOpen = false;
            }

            State = touchActive ? BuildTouchState() : BuildDesktopState();
        }

        // --------------------------------------------------------------------
        // Touch input path
        // --------------------------------------------------------------------

        private InputState BuildTouchState()
        {
            var t = touch;
            return new InputState
            {
                MoveForward     = t.Move.y,
                MoveRight       = t.Move.x,
                Jump            = t.JumpHeld,
                Sprint          = t.SprintOn,
                MouseDeltaX     = t.Look.x,
                MouseDeltaY     = t.Look.y,
                BreakPressed    = t.BreakPressedThisFrame,
                PlacePressed    = t.PlacePressedThisFrame,
                HotbarSelection = 0,
                HotbarScroll    = 0,
                OpenPicker      = t.PickerPressedThisFrame,
            };
        }

        // --------------------------------------------------------------------
        // Desktop input path
        // --------------------------------------------------------------------

        private InputState BuildDesktopState()
        {
            HandleCursorLock();

            return new InputState
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
                HotbarScroll    = ReadScroll(),
                OpenPicker      = false,
            };
        }

        private int ReadHotbarKey()
        {
            for (int i = 1; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i)) return i;
            return 0;
        }

        private int ReadScroll()
        {
            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (scroll > 0.01f) return -1;
            if (scroll < -0.01f) return 1;
            return 0;
        }

        private void HandleCursorLock()
        {
            if (autoLockOnClick && Input.GetMouseButtonDown(0) && !cursorLocked)
                SetCursorLocked(true);
        }

        private void SetCursorLocked(bool locked)
        {
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible   = !locked;
        }
    }
}
