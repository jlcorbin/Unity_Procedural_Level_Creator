// PlayerInputReader.cs
// Passive input endpoint. Receives UnityEvents from
// UnityEngine.InputSystem.PlayerInput on the same GameObject and
// exposes the current frame's input as read-only properties.
//
// NOTE: Class is named PlayerInputReader (not PlayerInput) to avoid
// human-side confusion with UnityEngine.InputSystem.PlayerInput,
// which is the Unity component this script receives events from on
// the same prefab root. Compiler-side both classes coexist via
// namespacing, but the inspector would otherwise show two components
// labeled "Player Input".
//
// Wired via UnityEvents in the inspector (Behavior: Invoke Unity
// Events on the PlayerInput component). Method names below match
// the InputSystem_Actions Player map exactly.

using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelGen.Player
{
    /// <summary>
    /// Passive endpoint for input from <c>UnityEngine.InputSystem.PlayerInput</c>
    /// (Behavior: Invoke Unity Events). Stashes per-frame intent into read-only
    /// properties. Owns no movement logic, no Animator logic, no transform
    /// manipulation — the single responsibility is "what is the player asking
    /// for right now".
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        // ── Public read API ──────────────────────────────────────────────────

        /// <summary>Last Move action value. Vector2 in [-1, 1] per axis.</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Last Look action value. Vector2 (camera-relative deltas).</summary>
        public Vector2 LookInput { get; private set; }

        /// <summary>True while the Sprint action is held. Read by PlayerController each frame.</summary>
        public bool IsSprinting { get; private set; }

        // ── UnityEvent endpoints ─────────────────────────────────────────────
        // Wired in the inspector to UnityEngine.InputSystem.PlayerInput's
        // per-action UnityEvents. Value-type actions (Move, Look) read every
        // callback; Button-type stubs gate on ctx.performed so a single press
        // logs once instead of three times (started/performed/canceled).

        /// <summary>Move action endpoint (Vector2). Stores the latest stick / WASD value.</summary>
        public void OnMove(InputAction.CallbackContext ctx)
        {
            MoveInput = ctx.ReadValue<Vector2>();
        }

        /// <summary>Look action endpoint (Vector2). Stores the latest mouse / right-stick delta.</summary>
        public void OnLook(InputAction.CallbackContext ctx)
        {
            LookInput = ctx.ReadValue<Vector2>();
        }

        /// <summary>Attack stub. M1: log on press only.</summary>
        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Attack");
        }

        /// <summary>Interact stub. M1: log on press only.</summary>
        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Interact");
        }

        /// <summary>Crouch stub. M1: log on press only.</summary>
        public void OnCrouch(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Crouch");
        }

        /// <summary>Jump stub. M1: log on press only.</summary>
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Jump");
        }

        /// <summary>
        /// Sprint is hold-to-activate. Updates <see cref="IsSprinting"/> from
        /// the action's button state on every callback phase so we correctly
        /// track press, hold, and release.
        /// </summary>
        public void OnSprint(InputAction.CallbackContext ctx)
        {
            IsSprinting = ctx.ReadValueAsButton();
        }

        /// <summary>Previous stub. M1: log on press only.</summary>
        public void OnPrevious(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Previous");
        }

        /// <summary>Next stub. M1: log on press only.</summary>
        public void OnNext(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Next");
        }
    }
}
