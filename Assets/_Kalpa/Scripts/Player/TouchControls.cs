// ============================================================================
// TouchControls.cs  (v2 — runtime touch detection, WebGL-safe)
// ----------------------------------------------------------------------------
// FIX: v1 activated touch based on compile platform + a Force flag, which made
// desktop WebGL wrongly use the touch path (keyboard/mouse ignored).
//
// v2 detects an ACTUAL touchscreen at runtime via Input.touchSupported, so:
//   * Desktop browser (WebGL) → keyboard/mouse
//   * Mobile browser (WebGL)  → touch UI
//   * Android/iOS build       → touch UI
// No manual toggling needed.
// ============================================================================

using UnityEngine;

namespace Kalpa.Player
{
    public sealed class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }

        [Header("Enable")]
        [Tooltip("Force touch UI on for testing on desktop. Leave OFF for normal use.")]
        [SerializeField] private bool forceTouch = false;

        [Header("Look sensitivity")]
        [SerializeField] private float lookSensitivity = 0.15f;

        [Header("Layout")]
        [SerializeField] private float joystickRadius = 120f;
        [SerializeField] private float buttonSize = 90f;

        public bool Active { get; private set; }
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool SprintOn { get; private set; }
        public bool BreakPressedThisFrame { get; private set; }
        public bool PlacePressedThisFrame { get; private set; }
        public bool PickerPressedThisFrame { get; private set; }

        private int moveTouchId = -1;
        private Vector2 moveOrigin;
        private int lookTouchId = -1;
        private Vector2 lookPrev;

        private void Awake()
        {
            Instance = this;

            if (forceTouch)
            {
                Active = true;
                return;
            }

#if UNITY_ANDROID || UNITY_IOS
            // Native mobile builds → touch on from the start.
            Active = true;
#else
            // Desktop AND desktop/mobile WebGL start with keyboard/mouse.
            // On WebGL we auto-flip to touch the first time a real touch occurs
            // (see Update), so mobile browsers get touch, desktop stays mouse.
            Active = false;
#endif
        }

        // On WebGL, flip to touch mode the first time the user actually touches.
        private void DetectTouchActivation()
        {
            if (Active) return;
            if (Input.touchCount > 0)
                Active = true;
        }

        private void Update()
        {
            DetectTouchActivation(); // WebGL: switch to touch on first touch

            if (!Active) return;

            BreakPressedThisFrame = false;
            PlacePressedThisFrame = false;
            PickerPressedThisFrame = false;
            Look = Vector2.zero;

            ProcessTouches();
        }

        private void ProcessTouches()
        {
            if (Input.touchCount == 0)
            {
                moveTouchId = -1;
                lookTouchId = -1;
                Move = Vector2.zero;
                return;
            }

            float halfW = Screen.width * 0.5f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);

                if (t.phase == TouchPhase.Began)
                {
                    if (t.position.x < halfW && moveTouchId == -1)
                    {
                        moveTouchId = t.fingerId;
                        moveOrigin = t.position;
                    }
                    else if (t.position.x >= halfW && lookTouchId == -1)
                    {
                        lookTouchId = t.fingerId;
                        lookPrev = t.position;
                    }
                }

                if (t.fingerId == moveTouchId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        moveTouchId = -1;
                        Move = Vector2.zero;
                    }
                    else
                    {
                        Vector2 delta = t.position - moveOrigin;
                        delta = Vector2.ClampMagnitude(delta, joystickRadius);
                        Move = delta / joystickRadius;
                    }
                }

                if (t.fingerId == lookTouchId)
                {
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        lookTouchId = -1;
                    else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                    {
                        Vector2 d = t.position - lookPrev;
                        lookPrev = t.position;
                        Look += new Vector2(d.x, d.y) * lookSensitivity;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!Active) return;

            float b = buttonSize;
            float pad = 20f;
            float rightX = Screen.width - b - pad;
            float bottomY = Screen.height - b - pad;

            JumpHeld = GUI.RepeatButton(new Rect(rightX, bottomY, b, b), "JUMP");

            if (GUI.Button(new Rect(rightX, bottomY - b - 12, b, b), "BREAK"))
                BreakPressedThisFrame = true;

            if (GUI.Button(new Rect(rightX - b - 12, bottomY - b - 12, b, b), "PLACE"))
                PlacePressedThisFrame = true;

            string sprintLbl = SprintOn ? "RUN*" : "RUN";
            if (GUI.Button(new Rect(rightX - b - 12, bottomY, b, b), sprintLbl))
                SprintOn = !SprintOn;

            if (GUI.Button(new Rect(Screen.width - b - pad, pad, b, b * 0.6f), "BLOCKS"))
                PickerPressedThisFrame = true;

            if (moveTouchId != -1)
            {
                DrawCircle(moveOrigin, joystickRadius, new Color(1, 1, 1, 0.15f));
                Vector2 knob = moveOrigin + Move * joystickRadius;
                DrawCircle(knob, 30f, new Color(1, 1, 1, 0.4f));
            }
        }

        private static void DrawCircle(Vector2 screenPos, float radius, Color color)
        {
            float y = Screen.height - screenPos.y;
            var rect = new Rect(screenPos.x - radius, y - radius, radius * 2, radius * 2);
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
