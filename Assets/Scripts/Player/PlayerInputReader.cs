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

        /// <summary>
        /// Raised once per Attack button RELEASE (button-up edge, ctx.canceled).
        /// M22: consumed by the ranged combat path (charge on press, fire on
        /// release, spec §8). Melee ignores it.
        /// </summary>
        public event System.Action AttackReleased;

        /// <summary>
        /// Raised once per SwitchStance button press (button-down edge,
        /// ctx.performed). M22: consumed by the DEV-ONLY stance cycler
        /// (<c>StanceDevCycler</c>) to cycle <c>(stance + 1) % 8</c>. The binding
        /// (Q) and this event are development affordances only — see
        /// <see cref="StanceController"/>.
        /// </summary>
        public event System.Action SwitchStancePressed;

        // ── Robust direct action subscription (M22) ──────────────────────────
        // LockOn (RMB) and SwitchStance (Q) were not reaching their UnityEvent
        // endpoints even with correct-looking inspector wiring (the fragile
        // per-action UnityEvent failure — see the M5 input lesson). We subscribe
        // to those two actions DIRECTLY off the sibling PlayerInput's asset, which
        // bypasses the UnityEvent layer and needs no inspector wiring. When the
        // direct path is live it OWNS the event; the matching UnityEvent endpoint
        // below no-ops so a fixed wiring can't double-fire a toggle.
        private UnityEngine.InputSystem.PlayerInput _playerInput;
        private InputAction _lockOnAction;
        private InputAction _switchStanceAction;
        private bool _directLockOn;
        private bool _directSwitchStance;

        private void Awake()
        {
            _playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        }

        private void OnEnable()
        {
            var map = (_playerInput != null && _playerInput.actions != null)
                ? _playerInput.actions.FindActionMap("Player", false)
                : null;
            if (map == null) return;

            _lockOnAction = map.FindAction("LockOn", false);
            if (_lockOnAction != null) { _lockOnAction.performed += OnLockOnAction; _directLockOn = true; }

            _switchStanceAction = map.FindAction("SwitchStance", false);
            if (_switchStanceAction != null) { _switchStanceAction.performed += OnSwitchStanceAction; _directSwitchStance = true; }
        }

        private void OnDisable()
        {
            if (_lockOnAction != null) _lockOnAction.performed -= OnLockOnAction;
            if (_switchStanceAction != null) _switchStanceAction.performed -= OnSwitchStanceAction;
            _directLockOn = _directSwitchStance = false;
        }

        private void OnLockOnAction(InputAction.CallbackContext ctx) => OnLockOnPerformed?.Invoke();
        private void OnSwitchStanceAction(InputAction.CallbackContext ctx) => SwitchStancePressed?.Invoke();

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
            else if (ctx.canceled) AttackReleased?.Invoke();  // M22: ranged charge-release
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
            if (_directLockOn) return;   // direct subscription owns this action
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

        /// <summary>
        /// SwitchStance action endpoint (DEV-ONLY). Raises
        /// <see cref="SwitchStancePressed"/> on the performed phase. Consumed by
        /// <c>StanceDevCycler</c>. Until the Q binding is added to the input asset
        /// (P8), this simply never fires — safe to ship ahead of the binding.
        /// </summary>
        public void OnSwitchStance(InputAction.CallbackContext ctx)
        {
            if (_directSwitchStance) return;   // direct subscription owns this action
            if (ctx.performed) SwitchStancePressed?.Invoke();
        }
    }
}
