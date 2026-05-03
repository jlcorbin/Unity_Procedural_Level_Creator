// PlayerDeath.cs — sole owner of the Player's death sequence.
//
// Subscribes to CharacterStatsRuntime.OnDied (raised inside
// ApplyDamage when HP transitions from >0 to <=0). On invocation:
// disables PlayerController + PlayerCombat (input still flows but
// no subscribers act on it), calls PlayerAnimator.SetDeathTrigger,
// and fires its own OnPlayerDied event for UI to subscribe to.
//
// Mirrors EnemyDeath but uses Player conventions:
//   - PlayerAnimator is the sole writer to Animator parameters,
//     so this script calls SetDeathTrigger() rather than
//     animator.SetTrigger directly.
//   - Player corpse stays in scene — no Destroy. Restart routes
//     through PlayerDeathOverlay (scene reload).

using System;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Sole owner of the Player's death sequence. Subscribes to
    /// <see cref="CharacterStatsRuntime.OnDied"/>; on first fire
    /// disables <see cref="PlayerController"/> + <see cref="PlayerCombat"/>,
    /// triggers the Animator's Death state via
    /// <see cref="PlayerAnimator.SetDeathTrigger"/>, and raises
    /// <see cref="OnPlayerDied"/> for UI subscribers.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [DisallowMultipleComponent]
    public class PlayerDeath : MonoBehaviour
    {
        [Header("References (auto-resolved on Reset/Awake)")]
        [Tooltip("Animator-writer component. Death trigger is fired via " +
                 "SetDeathTrigger() (single-writer-per-Animator-parameter " +
                 "invariant preserved).")]
        [SerializeField] private PlayerAnimator    _animator;

        [Tooltip("PlayerController — disabled on death so movement input " +
                 "is silently ignored even though PlayerInputReader keeps " +
                 "raising events.")]
        [SerializeField] private PlayerController  _controller;

        [Tooltip("PlayerCombat — disabled on death so attack input is " +
                 "silently ignored. The Hit-after-Death guard inside " +
                 "TakeHit is belt-and-suspenders for the same-frame case.")]
        [SerializeField] private PlayerCombat      _combat;

        private CharacterStatsRuntime _stats;
        private bool                  _hasFired;

        /// <summary>
        /// Raised AFTER the death cleanup runs (subscribers see the
        /// post-cleanup state — controllers disabled, Death trigger
        /// queued). Single-fire — guarded by `_hasFired`.
        /// </summary>
        public event Action<PlayerDeath> OnPlayerDied;

        /// <summary>True once HandleDied has fired its cleanup sequence.</summary>
        public bool HasFired => _hasFired;

        private void Reset()
        {
            _animator   = GetComponent<PlayerAnimator>();
            _controller = GetComponent<PlayerController>();
            _combat     = GetComponent<PlayerCombat>();
        }

        private void Awake()
        {
            _stats = GetComponent<CharacterStatsRuntime>();
            if (_animator   == null) _animator   = GetComponent<PlayerAnimator>();
            if (_controller == null) _controller = GetComponent<PlayerController>();
            if (_combat     == null) _combat     = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            if (_stats != null) _stats.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.OnDied -= HandleDied;
        }

        private void HandleDied(CharacterStatsRuntime _)
        {
            if (_hasFired) return;
            _hasFired = true;

            // 1. Disable controllers FIRST so any input arriving in the
            //    same frame as this event is dropped before the
            //    components see it.
            if (_controller != null) _controller.enabled = false;
            if (_combat     != null) _combat.enabled     = false;

            // 2. Queue Death trigger via the animator-writer (preserves
            //    single-writer-per-Animator-parameter invariant).
            if (_animator != null) _animator.SetDeathTrigger();

            // 3. Fire public event LAST so subscribers (overlay) see the
            //    post-cleanup state with the trigger already queued.
            OnPlayerDied?.Invoke(this);
        }
    }
}
