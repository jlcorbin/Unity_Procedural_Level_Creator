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

using LevelGen.Input;
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

        /// <summary>True while the Sneak action is held. Read by PlayerController and PlayerSneak each frame.</summary>
        public bool IsSneaking { get; private set; }

        /// <summary>
        /// Raised once per Attack button press (button-down edge). Subscribed
        /// to by PlayerCombat. Not raised on hold or release.
        /// </summary>
        public event System.Action AttackPressed;

        /// <summary>
        /// Raised once per Jump button press (button-down edge). Subscribed
        /// to by PlayerController. Not raised on hold or release.
        /// </summary>
        public event System.Action JumpPressed;

        /// <summary>
        /// Raised once per Interact button press (button-down edge).
        /// Subscribed to by PlayerInteractor. Not raised on hold or release.
        /// </summary>
        public event System.Action InteractPressed;

        /// <summary>
        /// Raised once per Dodge button press (button-down edge,
        /// ctx.started). Subscribed to by PlayerDodge. Not raised on
        /// hold or release.
        /// </summary>
        public event System.Action DodgePressed;

        /// <summary>
        /// Raised once per LockOn button press (button-down edge,
        /// ctx.performed). Subscribed to by TargetLock. Toggles lock-on
        /// state — same press acquires or releases a locked target.
        /// Not raised on hold or release.
        /// </summary>
        public event System.Action OnLockOnPerformed;

        /// <summary>
        /// Raised once per ToggleInventory button press (button-down edge,
        /// ctx.performed). Subscribed to by InventoryPanel. Toggles the
        /// inventory panel open / closed.
        /// Not raised on hold or release.
        /// </summary>
        public event System.Action OnToggleInventoryPerformed;

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
            if (MouseLook.SuppressLookThisFrame)
            {
                MouseLook.SuppressLookThisFrame = false;
                return;
            }
            LookInput = ctx.ReadValue<Vector2>();
        }

        /// <summary>
        /// Attack action endpoint. Raises <see cref="AttackPressed"/> on the
        /// performed phase (button-down). Consumed by PlayerCombat.
        /// </summary>
        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) AttackPressed?.Invoke();
        }

        /// <summary>
        /// Interact action endpoint. Raises <see cref="InteractPressed"/>
        /// on the performed phase (button-down). Consumed by
        /// PlayerInteractor.
        /// </summary>
        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) InteractPressed?.Invoke();
        }

        /// <summary>Crouch stub. M1: log on press only.</summary>
        public void OnCrouch(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Debug.Log("[PlayerInputReader] Crouch");
        }

        /// <summary>
        /// Jump action endpoint. Raises <see cref="JumpPressed"/> on the
        /// performed phase (button-down). Consumed by PlayerController.
        /// </summary>
        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) JumpPressed?.Invoke();
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

        /// <summary>
        /// Sneak is hold-to-activate. Updates <see cref="IsSneaking"/> from
        /// the action's phase on every callback so we correctly track press,
        /// hold, and release. True during Started and Performed phases
        /// (key held); false during Canceled (key released).
        /// </summary>
        public void OnSneak(InputAction.CallbackContext ctx)
        {
            IsSneaking = ctx.phase == InputActionPhase.Performed
                      || ctx.phase == InputActionPhase.Started;
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

        /// <summary>
        /// Dodge action endpoint. Raises <see cref="DodgePressed"/> on
        /// the started phase (the literal moment the button goes down)
        /// rather than performed — single press only, immune to any
        /// future Hold/Tap interactions added to the binding.
        /// Consumed by PlayerDodge.
        /// </summary>
        public void OnDodge(InputAction.CallbackContext ctx)
        {
            if (ctx.started) DodgePressed?.Invoke();
        }

        /// <summary>
        /// LockOn action endpoint. Raises <see cref="OnLockOnPerformed"/>
        /// on the performed phase (button-down edge). Consumed by
        /// TargetLock. Toggles lock-on state — same press acquires or
        /// releases a locked target.
        /// </summary>
        public void OnLockOn(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) OnLockOnPerformed?.Invoke();
        }

        /// <summary>
        /// ToggleInventory action endpoint. Raises <see cref="OnToggleInventoryPerformed"/>
        /// on the performed phase (button-down edge). Consumed by
        /// InventoryPanel. Toggles the inventory panel open / closed.
        /// </summary>
        public void OnToggleInventory(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) OnToggleInventoryPerformed?.Invoke();
        }
    }
}
