// ============================================================================
// PlayerInput.cs  (Phase 10 — scroll-wheel hotbar)
// ----------------------------------------------------------------------------
// Adds HotbarScroll: +1 / -1 when the mouse wheel moves, so the player can
// cycle hotbar slots with the wheel in addition to number keys.
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
            public int   HotbarSelection; // 1..N direct select, 0 = none
            public int   HotbarScroll;    // -1, 0, +1
        }

        public InputState State { get; private set; }

        [Header("Cursor Lock")]
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
            bool paused = pauseMenu != null && pauseMenu.IsOpen;
            if (paused)
            {
                cursorLocked = false;
                State = default;
                return;
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
            if (scroll > 0.01f) return -1; // wheel up → previous slot
            if (scroll < -0.01f) return 1; // wheel down → next slot
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
