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
using LevelGen.Items;

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
    [RequireComponent(typeof(CharacterStatsRuntime))]
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
        [Tooltip("Fallback damage when no weapon is equipped in the Melee slot (unarmed swing). M22: default 20 matches UE5 melee damage (UnrealUnits.MeleeDamage). Equipped weapons still use their ItemData.Damage.")]
        [SerializeField] private int _fallbackDamage = UnrealUnits.MeleeDamage;

        // ── Runtime hitbox (assigned by PlayerEquipmentVisuals on equip) ────
        // Not a SerializeField — the weapon WorldPrefab acts as its own hitbox
        // and is handed here by PlayerEquipmentVisuals after it instantiates the
        // prefab under weapon_r. Will be null when the player is unarmed.
        private Collider _hitbox;

        /// <summary>
        /// The active trigger collider on the currently-equipped weapon prefab.
        /// Assigned at runtime by <see cref="PlayerEquipmentVisuals"/> when a
        /// weapon is equipped or unequipped. <c>null</c> when unarmed —
        /// <see cref="OnHitboxOpen"/> will log a warning and no-op in that state.
        /// </summary>
        public Collider Hitbox
        {
            get => _hitbox;
            set => _hitbox = value;
        }

        // ── Cached refs / state ─────────────────────────────────────────────

        private PlayerInputReader _input;
        private PlayerAnimator _animator;
        private CharacterStatsRuntime _stats;
        private StanceController _stance;   // M22: optional — owns the stance int + gates ranged LMB
        private bool _attackBuffered;

        // Set to a positive integer to override the next swing's damage
        // for ONE successful hitbox-target hit, then auto-cleared. Used
        // by Interactables (e.g. AssassinateInteractable) to deliver an
        // unusual hit through the existing combo + hitbox path. NOT for
        // general damage tuning.
        private int _nextHitDamageOverride = -1;

        // Targets struck during the current swing — cleared on OnHitboxOpen.
        // Prevents double-hits when the collider re-enters the same trigger.
        private readonly HashSet<Targetable> _currentAttackHitList = new HashSet<Targetable>();

        private static readonly int AttackStateHash = Animator.StringToHash("Attack");
        private static readonly int Attack02StateHash = Animator.StringToHash("Attack02");
        private static readonly int Attack03StateHash = Animator.StringToHash("Attack03");
        private static readonly int HitStateHash = Animator.StringToHash("Hit");
        private static readonly int Attack_OHSStateHash = Animator.StringToHash("Attack_OHS");
        private static readonly int Attack02_OHSStateHash = Animator.StringToHash("Attack02_OHS");
        private static readonly int Attack03_OHSStateHash = Animator.StringToHash("Attack03_OHS");
        private static readonly int Attack_THSStateHash = Animator.StringToHash("Attack_THS");
        private static readonly int Attack02_THSStateHash = Animator.StringToHash("Attack02_THS");
        private static readonly int Attack03_THSStateHash = Animator.StringToHash("Attack03_THS");
        private static readonly int Attack_SpearStateHash = Animator.StringToHash("Attack_Spear");
        private static readonly int Attack02_SpearStateHash = Animator.StringToHash("Attack02_Spear");
        private static readonly int Attack03_SpearStateHash = Animator.StringToHash("Attack03_Spear");
        private static readonly int Attack_UnarmedStateHash = Animator.StringToHash("Attack_Unarmed");
        private static readonly int Attack02_UnarmedStateHash = Animator.StringToHash("Attack02_Unarmed");
        private static readonly int Attack03_UnarmedStateHash = Animator.StringToHash("Attack03_Unarmed");

        // ── M22: stance-agnostic combo recognition via Animator state TAGS ──
        // Each per-stance attack state carries an Attack1 / Attack2 / Attack3 tag
        // (authored in the Animator, P10). Any tagged state combos, so adding a
        // new stance's attack chain needs no code change. The shipped states are
        // ALSO matched by hash below, so nothing regresses before the tags exist.
        private static readonly int Attack1Tag = Animator.StringToHash("Attack1");
        private static readonly int Attack2Tag = Animator.StringToHash("Attack2");
        private static readonly int Attack3Tag = Animator.StringToHash("Attack3");

        private static readonly HashSet<int> Hit1Hashes = new HashSet<int>
        {
            AttackStateHash, Attack_OHSStateHash, Attack_THSStateHash,
            Attack_SpearStateHash, Attack_UnarmedStateHash
        };
        private static readonly HashSet<int> Hit2Hashes = new HashSet<int>
        {
            Attack02StateHash, Attack02_OHSStateHash, Attack02_THSStateHash,
            Attack02_SpearStateHash, Attack02_UnarmedStateHash
        };
        private static readonly HashSet<int> Hit3Hashes = new HashSet<int>
        {
            Attack03StateHash, Attack03_OHSStateHash, Attack03_THSStateHash,
            Attack03_SpearStateHash, Attack03_UnarmedStateHash
        };

        /// <summary>First-hit attack state (any stance): tagged Attack1 or a known first-hit hash.</summary>
        private static bool IsAttack1(AnimatorStateInfo i) => i.tagHash == Attack1Tag || Hit1Hashes.Contains(i.shortNameHash);
        /// <summary>Second-hit attack state (any stance).</summary>
        private static bool IsAttack2(AnimatorStateInfo i) => i.tagHash == Attack2Tag || Hit2Hashes.Contains(i.shortNameHash);
        /// <summary>Third-hit / finisher attack state (any stance) — combo cap.</summary>
        private static bool IsAttack3(AnimatorStateInfo i) => i.tagHash == Attack3Tag || Hit3Hashes.Contains(i.shortNameHash);

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
            // M22: generalize the base-Attack lock to the first hit of every
            // stance's chain (plus Hit). Movement stays free during Attack2/3
            // (M21 "unrestricted during combo"). The out-blend to Idle still
            // reports locked until the transition completes.
            => IsAttack1(info) || info.shortNameHash == HitStateHash;

        /// <summary>
        /// Public alias of <see cref="IsActionLocked"/> for outside callers
        /// (Interactables) to query whether the player can accept a new
        /// action. Same state-hash check semantics.
        /// </summary>
        public bool IsBusy => IsActionLocked;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _animator = GetComponent<PlayerAnimator>();
            // Null-tolerant — PlayerCombat does not [RequireComponent]
            // CharacterStatsRuntime (matches EnemyHitReaction's
            // null-guarded pattern).
            _stats = GetComponent<CharacterStatsRuntime>();
            // M22: optional — owns the stance int and gates ranged LMB. Null in
            // legacy scenes without the stance system (falls back to per-swing
            // WeaponTypeResolver in OnAttackPressed).
            _stance = GetComponent<StanceController>();
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
            if (!IsAttack1(info) && !IsAttack2(info)) return;

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
            // M22: ranged stances (wand/bow) use charge-and-release, not the
            // melee combo — RangedCombat owns the LMB press/release there.
            if (_stance != null && _stance.IsRanged) return;

            var anim = AnimatorComponent;
            if (anim == null) return;

            // Drop input during state transitions — wait for stable state.
            if (anim.IsInTransition(0)) return;

            var info = anim.GetCurrentAnimatorStateInfo(0);

            if (info.shortNameHash == HitStateHash)
            {
                // No canceling out of stagger.
                return;
            }

            // Combo cap: the third hit is the finisher (heavy hits 4/5 dropped
            // per Jason's decision). Drop deliberately rather than depending on
            // the Animator graph having no outgoing Attack-trigger transition.
            if (IsAttack3(info)) return;

            bool inActiveAttack = IsAttack1(info) || IsAttack2(info);

            if (!inActiveAttack)
            {
                // Idle / Locomotion / Sprint — fire immediately. The stance's
                // int is owned by StanceController (M22), which already set it
                // on the last stance change. Only resolve + write the legacy
                // WeaponType here as a fallback for scenes with no StanceController.
                if (_stance == null)
                {
                    var inv = PlayerInventory.Instance;
                    var meleeItem = inv != null ? inv.GetEquipped(EquipSlot.Melee) : null;
                    var offHandItem = inv != null ? inv.GetEquipped(EquipSlot.OffHand) : null;
                    _animator.SetWeaponType(WeaponTypeResolver.Resolve(meleeItem, offHandItem));
                }
                _animator.SetAttackTrigger();
                _attackBuffered = false;
                return;
            }

            // Currently in a first- or second-hit attack. Decide based on combo
            // window position.
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

        /// <summary>
        /// Forcibly drops any in-flight attack state. Clears the
        /// buffered next-combo press, empties the per-swing hit list,
        /// and disables the weapon hitbox. Does NOT touch the Animator
        /// directly — single-writer-per-parameter invariant preserved.
        /// The visual interruption is expected to come from a higher-
        /// priority Animator transition (e.g. AnyState → RollFWD from
        /// PlayerDodge) so the Attack clip blends out as the new state
        /// blends in. Idempotent. Used by PlayerDodge (M12).
        /// </summary>
        public void CancelAttack()
        {
            _attackBuffered = false;
            _currentAttackHitList.Clear();
            if (_hitbox != null) _hitbox.enabled = false;
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
            if (_hitbox == null)
            {
                // Expected when the player is unarmed — no weapon prefab is
                // instantiated under weapon_r, so no hitbox collider has been
                // handed to us by PlayerEquipmentVisuals. Log a warning (not an
                // error) and return; this is not a misconfiguration.
                Debug.LogWarning("[PlayerCombat] OnHitboxOpen fired but no hitbox is assigned — no weapon equipped.", this);
                return;
            }
            _currentAttackHitList.Clear();
            _hitbox.enabled = true;
        }

        /// <summary>
        /// Animation-event callback. Disables the hitbox collider — no more
        /// damage frames for this swing. Silent no-op if hitbox is null
        /// (the open-side already logged).
        /// </summary>
        public void OnHitboxClose()
        {
            if (_hitbox == null) return;
            _hitbox.enabled = false;
        }

        // ── Hitbox routing (called from HitboxRelay.OnTriggerEnter) ────────

        /// <summary>
        /// Called by <see cref="HitboxRelay"/> when the weapon's trigger
        /// collider enters another collider. Resolves the hit to a
        /// <see cref="Targetable"/> + <see cref="CharacterStatsRuntime"/>
        /// pair, applies damage (from equipped Melee weapon or
        /// <see cref="_fallbackDamage"/> if unarmed) once per attack, and
        /// records the target so the same swing can't double-hit.
        /// </summary>
        public void NotifyHitboxTriggered(Collider other)
        {
            // Self-hit guard. Player's own Targetable lives on the root
            // (M11), and the player's CharacterController on the root
            // would otherwise be detected by the weapon hitbox swing.
            // Mirrors EnemyCombat's CompareTag("Player") friendly-fire
            // pattern in reverse: PlayerCombat must NOT hit Players.
            if (other.gameObject == gameObject) return;
            if (other.CompareTag("Player")) return;

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

            // Don't damage corpses. EnemyDeath disables the deathCollider
            // + Targetable on HP→0, so OnTriggerEnter shouldn't normally
            // fire on a dead target — but defense-in-depth in case the
            // wiring is incomplete or the collider re-enables. Mirrors
            // EnemyCombat's IsDead guard (M11).
            if (stats.IsDead) return;

            // Per-swing damage override (single-shot, set by Interactables
            // via SetNextHitDamageOverride). Consumed AFTER stats / hit-list
            // checks so the warning + already-hit branches above leave the
            // override intact for the next eligible swing.
            // M17: pull base damage from equipped Melee weapon; fall back to
            // unarmed _fallbackDamage when no weapon is in the Melee slot.
            var inv = PlayerInventory.Instance;
            ItemData melee = inv != null ? inv.GetEquipped(EquipSlot.Melee) : null;
            int dmg = melee != null ? melee.Damage : _fallbackDamage;
            bool wasOverride = _nextHitDamageOverride > 0;
            if (wasOverride)
            {
                dmg = _nextHitDamageOverride;
                _nextHitDamageOverride = -1;
            }

            stats.ApplyDamage(dmg);
            _currentAttackHitList.Add(targetable);

            Vector3 hitPoint = _hitbox != null
                ? other.ClosestPoint(_hitbox.bounds.center)
                : other.bounds.center;
            targetable.RaiseHit(hitPoint, dmg);

            Debug.Log($"[PlayerCombat] Hit {targetable.name} for {dmg}" +
                      (wasOverride ? " (override)" : "") +
                      $" (HP now {stats.CurrentHP}/{stats.MaxHP}).");
        }
    }
}
