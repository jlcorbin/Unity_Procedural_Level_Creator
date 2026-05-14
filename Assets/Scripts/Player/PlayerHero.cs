// PlayerHero.cs — M12-R refactor: single root manifest for the player.
//
// PlayerHero is purely a wiring registry: it declares every required
// component on the Player_Hero prefab root via [RequireComponent] and
// caches a SerializeField reference to each. No gameplay logic lives
// here; every script in the player domain still owns its own concern.
//
// Spec notes — deviations from the M12-R locked list:
// 1. `Animator` (Unity built-in) is NOT in the RequireComponent chain
//    because the Animator component lives on the `MaleCharacterPBR`
//    child by design (FBX-mounted humanoid rig). PlayerAnimator
//    resolves it via GetComponentInChildren. Forcing [RequireComponent(
//    Animator)] on the root would auto-add a SECOND, controller-less
//    Animator and conflict with the child rig.
// 2. `UnityEngine.InputSystem.PlayerInput` is ADDED to the chain
//    (not in the locked list) because PlayerInputReader receives its
//    callbacks via PlayerInput's UnityEvent dispatch. Without this
//    component the player has no input.
// 3. PlayerController <-> PlayerStamina would form a circular
//    [RequireComponent] cycle (PlayerStamina already requires
//    PlayerController). PlayerHero declares both at the root so the
//    contract holds, but the per-script chain only goes one way.

using UnityEngine;
using UnityEngine.InputSystem;
using LevelGen.Combat;
using LevelGen.Input;
using LevelGen; // TargetLock lives in the root LevelGen namespace (M15)

namespace LevelGen.Player
{
    /// <summary>
    /// Manifest component on the Player_Hero prefab root. Declares the
    /// full required component set via [RequireComponent] and exposes
    /// each as a SerializeField reference + public read-only property.
    /// Owns NO gameplay state, NO Update logic, NO Animator writes —
    /// it is solely a wiring contract and a lookup table for external
    /// systems (enemy AI, UI, future game manager) that need to query
    /// the player.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [RequireComponent(typeof(Targetable))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerStamina))]
    [RequireComponent(typeof(PlayerDodge))]
    [RequireComponent(typeof(PlayerHitReaction))]
    [RequireComponent(typeof(PlayerDeath))]
    [RequireComponent(typeof(PlayerInteractor))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(MouseLook))]
    [RequireComponent(typeof(TargetLock))]
    [DisallowMultipleComponent]
    public class PlayerHero : MonoBehaviour
    {
        // ── Core ────────────────────────────────────────────────────────────
        [Header("Core")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private CharacterStatsRuntime _stats;
        [SerializeField] private Targetable _targetable;

        // ── Input ───────────────────────────────────────────────────────────
        [Header("Input")]
        [SerializeField] private UnityEngine.InputSystem.PlayerInput _playerInput;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private MouseLook _mouseLook;

        // ── Movement & Animation ────────────────────────────────────────────
        [Header("Movement & Animation")]
        [SerializeField] private PlayerController _controller;
        [SerializeField] private PlayerAnimator _animator;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private PlayerDodge _dodge;

        // ── Combat ──────────────────────────────────────────────────────────
        [Header("Combat")]
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private PlayerHitReaction _hitReaction;
        [SerializeField] private PlayerDeath _death;

        // ── Interaction ─────────────────────────────────────────────────────
        [Header("Interaction")]
        [SerializeField] private PlayerInteractor _interactor;

        // ── Target Lock (M15) ───────────────────────────────────────────────
        [Header("Target Lock")]
        [SerializeField] private TargetLock _targetLock;

        // ── Public read API ─────────────────────────────────────────────────
        public CharacterController CharacterController => _characterController;
        public CharacterStatsRuntime Stats             => _stats;
        public Targetable Targetable                   => _targetable;
        public UnityEngine.InputSystem.PlayerInput PlayerInput => _playerInput;
        public PlayerInputReader Input                 => _input;
        public MouseLook MouseLook                     => _mouseLook;
        public PlayerController Controller             => _controller;
        public PlayerAnimator Animator                 => _animator;
        public PlayerStamina Stamina                   => _stamina;
        public PlayerDodge Dodge                       => _dodge;
        public PlayerCombat Combat                     => _combat;
        public PlayerHitReaction HitReaction           => _hitReaction;
        public PlayerDeath Death                       => _death;
        public PlayerInteractor Interactor             => _interactor;
        public TargetLock TargetLock                   => _targetLock;

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Belt-and-suspenders Awake: re-resolves any null SerializeField
        /// via GetComponent. Builder is the canonical wiring path; this
        /// is the safety net for cases where a component was added at
        /// runtime, or a script reload cleared a ref.
        /// </summary>
        private void Awake()
        {
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_stats               == null) _stats               = GetComponent<CharacterStatsRuntime>();
            if (_targetable          == null) _targetable          = GetComponent<Targetable>();
            if (_playerInput         == null) _playerInput         = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (_input               == null) _input               = GetComponent<PlayerInputReader>();
            if (_mouseLook           == null) _mouseLook           = GetComponent<MouseLook>();
            if (_controller          == null) _controller          = GetComponent<PlayerController>();
            if (_animator            == null) _animator            = GetComponent<PlayerAnimator>();
            if (_stamina             == null) _stamina             = GetComponent<PlayerStamina>();
            if (_dodge               == null) _dodge               = GetComponent<PlayerDodge>();
            if (_combat              == null) _combat              = GetComponent<PlayerCombat>();
            if (_hitReaction         == null) _hitReaction         = GetComponent<PlayerHitReaction>();
            if (_death               == null) _death               = GetComponent<PlayerDeath>();
            if (_interactor          == null) _interactor          = GetComponent<PlayerInteractor>();
            if (_targetLock          == null) _targetLock          = GetComponent<TargetLock>();

            // M15: inject the input reader so TargetLock can subscribe to
            // OnLockOnPerformed. PlayerHero owns this call site; TargetLock
            // owns the subscription lifecycle (OnDisable unsubs).
            if (_targetLock != null && _input != null)
                _targetLock.InitFromPlayerHero(_input);
        }
    }
}
