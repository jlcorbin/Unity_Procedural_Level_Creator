// PlayerCombat.cs
// Translates combat input intent into Animator trigger writes. Owns the
// buffered-combo state machine: presses outside the combo window are
// dropped; presses inside the window are buffered and re-fired near the
// end of the current swing. TakeHit() is the public damage entry point.
//
// Single-direction dependency: subscribes to PlayerInputReader's
// AttackPressed event and writes via PlayerAnimator's public API only.

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Translates combat input intent into Animator trigger writes. Implements
    /// a window-gated buffered combo (M2-B Step 3 foundation): a press during
    /// the swing's middle is held until the next-attack consume threshold and
    /// re-fired then; presses outside the window are dropped. With only one
    /// Attack state in the Animator, a buffered press re-fires Attack01 — the
    /// mechanism is in place for future Attack02+ states to consume the same
    /// flag.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    public class PlayerCombat : MonoBehaviour
    {
        // ── Tunables ────────────────────────────────────────────────────────

        [Header("Combo Window")]
        [Tooltip("Normalized time within Attack clip when next-attack input " +
                 "becomes buffer-eligible. 0.40 = 40% through the swing.")]
        [SerializeField, Range(0f, 1f)] private float comboWindowOpen = 0.40f;

        [Tooltip("Normalized time when the buffer window closes. Presses " +
                 "after this point are dropped (recovery frames).")]
        [SerializeField, Range(0f, 1f)] private float comboWindowClose = 0.80f;

        [Tooltip("Normalized time at which a buffered press fires the next " +
                 "Attack. Should sit just before the Attack→Idle exit time " +
                 "(0.90 in the controller).")]
        [SerializeField, Range(0f, 1f)] private float bufferConsumeAt = 0.85f;

        // ── Cached refs / state ─────────────────────────────────────────────

        private PlayerInputReader _input;
        private PlayerAnimator    _animator;
        private bool              _attackBuffered;

        private static readonly int AttackStateHash = Animator.StringToHash("Attack");
        private static readonly int HitStateHash    = Animator.StringToHash("Hit");

        // Resolved lazily — PlayerAnimator.Awake may run after ours since
        // sibling-Awake order is non-deterministic. Access via this property
        // anywhere it's needed; PlayerAnimator.Animator is a simple field
        // getter so the indirection is free.
        private Animator AnimatorComponent => _animator != null ? _animator.Animator : null;

        /// <summary>
        /// True when the player is currently in (or transitioning into) the
        /// Attack or Hit state. PlayerController reads this to suppress
        /// horizontal translation so the body roots in place during swings
        /// and stagger. Both phases of the in-blend are covered (current OR
        /// next state) to prevent a 0.10 s window of "moving while the swing
        /// is starting." The out-blend (Attack→Idle, Hit→Idle) reports
        /// locked as well — locomotion resumes the frame the transition
        /// completes.
        /// </summary>
        public bool IsActionLocked
        {
            get
            {
                var anim = AnimatorComponent;
                if (anim == null) return false;
                if (IsActionState(anim.GetCurrentAnimatorStateInfo(0))) return true;
                if (anim.IsInTransition(0) && IsActionState(anim.GetNextAnimatorStateInfo(0))) return true;
                return false;
            }
        }

        private static bool IsActionState(AnimatorStateInfo info)
            => info.shortNameHash == AttackStateHash || info.shortNameHash == HitStateHash;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _input    = GetComponent<PlayerInputReader>();
            _animator = GetComponent<PlayerAnimator>();
        }

        private void OnEnable()
        {
            if (_input != null) _input.AttackPressed += OnAttackPressed;
        }

        private void OnDisable()
        {
            if (_input != null) _input.AttackPressed -= OnAttackPressed;
        }

        private void Update()
        {
            if (!_attackBuffered) return;
            var anim = AnimatorComponent;
            if (anim == null) return;
            if (anim.IsInTransition(0)) return;

            var info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != AttackStateHash) return;

            float n = info.normalizedTime % 1.0f;
            if (n >= bufferConsumeAt)
            {
                _animator.SetAttackTrigger();
                _attackBuffered = false;
            }
        }

        // ── Input handler ───────────────────────────────────────────────────

        /// <summary>
        /// Subscribed to <see cref="PlayerInputReader.AttackPressed"/>. Routes
        /// the press based on current Animator state: fires immediately from
        /// Idle/Locomotion/Sprint, buffers within the combo window during
        /// Attack, drops the press during Hit or outside the window.
        /// </summary>
        private void OnAttackPressed()
        {
            var anim = AnimatorComponent;
            if (anim == null) return;

            // Drop input during state transitions — wait for stable state.
            if (anim.IsInTransition(0)) return;

            var info = anim.GetCurrentAnimatorStateInfo(0);
            int hash = info.shortNameHash;

            if (hash == HitStateHash)
            {
                // No canceling out of stagger.
                return;
            }

            if (hash != AttackStateHash)
            {
                // Idle / Locomotion / Sprint — fire immediately.
                _animator.SetAttackTrigger();
                _attackBuffered = false;
                return;
            }

            // Currently in Attack. Decide based on combo window position.
            float n = info.normalizedTime % 1.0f;
            if (n >= comboWindowOpen && n < comboWindowClose)
                _attackBuffered = true;
            // else: too early or too late — drop input.
        }

        // ── Public damage entry point ───────────────────────────────────────

        /// <summary>
        /// External damage entry point. Plays the Hit reaction by firing the
        /// Animator's Hit trigger. Does not apply damage — that's a future
        /// PlayerHealth concern. Clears any buffered attack so a queued combo
        /// doesn't leak past stagger.
        /// </summary>
        [ContextMenu("Take Hit")]
        public void TakeHit()
        {
            if (_animator == null) return;
            _animator.SetHitTrigger();
            _attackBuffered = false;
        }
    }
}
