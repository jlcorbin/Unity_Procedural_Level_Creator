// PlayerCombat.cs
// Translates combat input intent into Animator trigger writes. Owns the
// buffered-combo state machine: presses outside the combo window are
// dropped; presses inside the window are buffered and re-fired near the
// end of the current swing. TakeHit() is the public damage entry point.
//
// Single-direction dependency: subscribes to PlayerInputReader's
// AttackPressed event and writes via PlayerAnimator's public API only.

using System.Collections.Generic;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Translates combat input intent into Animator trigger writes. Implements
    /// a window-gated 3-hit buffered combo: a press during a swing's combo
    /// window is buffered and consumed near the swing's end via the
    /// <c>ComboNext</c> trigger, routing Attack → Attack02 → Attack03 in the
    /// Animator graph. Presses outside the window or during Hit are dropped.
    /// Attack03 is the finisher — presses during Attack03 are explicitly
    /// dropped to enforce the combo cap.
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

        [Header("Damage")]
        [Tooltip("HP subtracted per successful hit. Hardcoded for now; " +
                 "WeaponStats SO with per-weapon values is a follow-up.")]
        [SerializeField] private int attackDamage = 10;

        [Tooltip("Trigger collider on the weapon. Enabled during the active " +
                 "frames of an attack via OnHitboxOpen / OnHitboxClose " +
                 "AnimationEvents. Default disabled.")]
        [SerializeField] private Collider hitbox;

        // ── Cached refs / state ─────────────────────────────────────────────

        private PlayerInputReader     _input;
        private PlayerAnimator        _animator;
        private CharacterStatsRuntime _stats;
        private bool                  _attackBuffered;

        // Set to a positive integer to override the next swing's damage
        // for ONE successful hitbox-target hit, then auto-cleared. Used
        // by Interactables (e.g. AssassinateInteractable) to deliver an
        // unusual hit through the existing combo + hitbox path. NOT for
        // general damage tuning.
        private int _nextHitDamageOverride = -1;

        // Targets struck during the current swing — cleared on OnHitboxOpen.
        // Prevents double-hits when the collider re-enters the same trigger.
        private readonly HashSet<Targetable> _currentAttackHitList = new HashSet<Targetable>();

        private static readonly int AttackStateHash   = Animator.StringToHash("Attack");
        private static readonly int Attack02StateHash = Animator.StringToHash("Attack02");
        private static readonly int Attack03StateHash = Animator.StringToHash("Attack03");
        private static readonly int HitStateHash      = Animator.StringToHash("Hit");

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

        /// <summary>
        /// Public alias of <see cref="IsActionLocked"/> for outside callers
        /// (Interactables) to query whether the player can accept a new
        /// action. Same state-hash check semantics.
        /// </summary>
        public bool IsBusy => IsActionLocked;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _input    = GetComponent<PlayerInputReader>();
            _animator = GetComponent<PlayerAnimator>();
            // Null-tolerant — PlayerCombat does not [RequireComponent]
            // CharacterStatsRuntime (matches EnemyHitReaction's
            // null-guarded pattern).
            _stats    = GetComponent<CharacterStatsRuntime>();
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
            int hash = info.shortNameHash;
            if (hash != AttackStateHash && hash != Attack02StateHash) return;

            float n = info.normalizedTime % 1.0f;
            if (n >= bufferConsumeAt)
            {
                _animator.SetComboNext();
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

            // Combo cap: Attack03 is the finisher. Drop deliberately rather
            // than depending on the Animator graph having no outgoing
            // Attack-trigger transition from Attack03.
            if (hash == Attack03StateHash) return;

            bool inActiveAttack = hash == AttackStateHash || hash == Attack02StateHash;

            if (!inActiveAttack)
            {
                // Idle / Locomotion / Sprint — fire immediately.
                _animator.SetAttackTrigger();
                _attackBuffered = false;
                return;
            }

            // Currently in Attack01 or Attack02. Decide based on combo window position.
            float n = info.normalizedTime % 1.0f;
            if (n >= comboWindowOpen && n < comboWindowClose)
                _attackBuffered = true;
            // else: too early or too late — drop input.
        }

        // ── Public hooks for Interactables ──────────────────────────────────

        /// <summary>
        /// Sets a one-shot damage override for the next successful hitbox-
        /// target intersection. Auto-cleared inside
        /// <see cref="NotifyHitboxTriggered"/> after one consumption.
        /// Used by <c>AssassinateInteractable</c> (M6) to deliver an
        /// unusual hit through the existing combo + hitbox path.
        /// </summary>
        public void SetNextHitDamageOverride(int damage)
        {
            _nextHitDamageOverride = damage;
        }

        /// <summary>
        /// Public delegate of the private <c>OnAttackPressed</c> input
        /// handler — runs the same routing as a manual LMB press.
        /// Used by Interactables that fire the player's normal Attack
        /// path (e.g. AssassinateInteractable). The buffered-combo
        /// machine handles state.
        /// </summary>
        public void RequestAttack()
        {
            OnAttackPressed();
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
            // Hit-after-Death guard — belt-and-suspenders against
            // same-frame OnHit/OnDied ordering. PlayerDeath also disables
            // this component on the OnDied path, but a hit landing in the
            // same frame as HP→0 could otherwise queue a flinch right
            // before Death plays.
            if (_stats != null && _stats.IsDead) return;

            if (_animator == null) return;
            _animator.SetHitTrigger();
            _attackBuffered = false;
        }

        // ── AnimationEvent endpoints (called from Attack clips) ─────────────

        /// <summary>
        /// Animation-event callback. Clears the per-attack hit list and
        /// enables the hitbox collider so subsequent OnTriggerEnter calls
        /// can deal damage. Public because Unity's AnimationEvent system
        /// only invokes public methods. Walks the hierarchy from the
        /// Animator GameObject up to here.
        /// </summary>
        public void OnHitboxOpen()
        {
            if (hitbox == null)
            {
                Debug.LogError("[PlayerCombat] OnHitboxOpen fired but 'hitbox' is unassigned. " +
                               "Run 'LevelGen ▶ Combat ▶ Add Weapon Hitbox to Player_MaleHero'.", this);
                return;
            }
            _currentAttackHitList.Clear();
            hitbox.enabled = true;
        }

        /// <summary>
        /// Animation-event callback. Disables the hitbox collider — no more
        /// damage frames for this swing. Silent no-op if hitbox is null
        /// (the open-side already logged).
        /// </summary>
        public void OnHitboxClose()
        {
            if (hitbox == null) return;
            hitbox.enabled = false;
        }

        // ── Hitbox routing (called from HitboxRelay.OnTriggerEnter) ────────

        /// <summary>
        /// Called by <see cref="HitboxRelay"/> when the weapon's trigger
        /// collider enters another collider. Resolves the hit to a
        /// <see cref="Targetable"/> + <see cref="CharacterStatsRuntime"/>
        /// pair, applies <see cref="attackDamage"/> once per attack, and
        /// records the target so the same swing can't double-hit.
        /// </summary>
        public void NotifyHitboxTriggered(Collider other)
        {
            var targetable = other.GetComponentInParent<Targetable>();
            if (targetable == null) return;

            if (_currentAttackHitList.Contains(targetable)) return;

            var stats = targetable.GetComponent<CharacterStatsRuntime>();
            if (stats == null)
            {
                Debug.LogWarning($"[PlayerCombat] Targetable '{targetable.name}' hit but has no " +
                                 "CharacterStatsRuntime — recording as hit anyway to prevent " +
                                 "spam. Misconfiguration: Targetable + CharacterStatsRuntime " +
                                 "should live on the same GameObject.", targetable);
                _currentAttackHitList.Add(targetable);
                return;
            }

            // Per-swing damage override (single-shot, set by Interactables
            // via SetNextHitDamageOverride). Consumed AFTER stats / hit-list
            // checks so the warning + already-hit branches above leave the
            // override intact for the next eligible swing.
            int dmg = _nextHitDamageOverride > 0 ? _nextHitDamageOverride : attackDamage;
            bool wasOverride = _nextHitDamageOverride > 0;
            if (wasOverride) _nextHitDamageOverride = -1;

            stats.ApplyDamage(dmg);
            _currentAttackHitList.Add(targetable);

            Vector3 hitPoint = hitbox != null
                ? other.ClosestPoint(hitbox.bounds.center)
                : other.bounds.center;
            targetable.RaiseHit(hitPoint, dmg);

            Debug.Log($"[PlayerCombat] Hit {targetable.name} for {dmg}" +
                      (wasOverride ? " (override)" : "") +
                      $" (HP now {stats.CurrentHP}/{stats.MaxHP}).");
        }
    }
}
