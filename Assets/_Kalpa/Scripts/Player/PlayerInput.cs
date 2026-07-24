// ============================================================================
// PlayerInput.cs  (Phase 12d — auto re-lock cursor when a menu closes)
// ----------------------------------------------------------------------------
// FIX: previously, after closing the BlockPicker/PauseMenu you had to click the
// screen before mouse-look resumed. Now the cursor re-locks automatically the
// frame a menu closes, so look control returns immediately.
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
        }

        public InputState State { get; private set; }

        [Header("Cursor Lock")]
        [SerializeField] private bool autoLockOnClick = true;

        private bool cursorLocked;
        private bool wasMenuOpen;         // tracks menu state across frames
        private PauseMenu pauseMenu;
        private BlockPicker blockPicker;

        private void Start()
        {
            pauseMenu = Object.FindFirstObjectByType<PauseMenu>();
            blockPicker = Object.FindFirstObjectByType<BlockPicker>();
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

            // A menu JUST closed this frame → re-lock immediately so look works.
            if (wasMenuOpen)
            {
                wasMenuOpen = false;
                SetCursorLocked(true);
            }

            HandleCursorLock();

            State = new InputState
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
