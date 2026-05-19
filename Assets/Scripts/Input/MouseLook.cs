// Cursor-state controller. Despite the legacy filename, this does NOT rotate
// the camera or read mouse delta — PlayerInput owns Look input. This script
// only manages Cursor.lockState and Cursor.visible.
//
// Behavior:
//   Awake / Play start  → lock + hide
//   Escape pressed      → unlock + show
//   Click in Game view  → lock + hide (re-engage)
//   Focus lost (Alt-Tab)→ unlock + show
//   Focus regained      → stay unlocked, wait for click
//   OnDisable / Destroy → unlock + show (clean teardown)

using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelGen.Input
{
    /// <summary>
    /// Locks and hides the cursor while the Game view is focused. Escape
    /// releases; clicking back in re-engages; focus loss releases. Single
    /// purpose — does not read look input.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MouseLook : MonoBehaviour
    {
        /// <summary>True when the cursor is currently locked and hidden.</summary>
        public bool IsLocked { get; private set; }

        private void OnEnable()
        {
            Application.focusChanged += OnFocusChanged;
            Lock();
        }

        private void OnDisable()
        {
            Application.focusChanged -= OnFocusChanged;
            Unlock();
        }

        private void OnDestroy()
        {
            Unlock();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Unlock();
                return;
            }

            if (!IsLocked)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    Lock();
                }
            }
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus) Unlock();
        }

        /// <summary>
        /// Set to true by <see cref="Lock"/> each time the cursor is locked.
        /// <c>PlayerInputReader.OnLook</c> reads and clears this flag to
        /// suppress the OS cursor-warp delta that the Input System reports
        /// as a mouse-delta event on the frame the lock engages.
        /// </summary>
        public static bool SuppressLookThisFrame;

        private void Lock()
        {
            SuppressLookThisFrame = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            IsLocked = true;
        }

        private void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsLocked = false;
        }
    }
}
