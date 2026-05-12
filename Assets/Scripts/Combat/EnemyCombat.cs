// EnemyCombat.cs — symmetric to PlayerCombat's hitbox+damage path (M11).
//
// Replaces M10's no-op EnemyAnimationEventAbsorber. Owns:
//   - The enemy weapon hitbox enable/disable (driven by Attack-clip
//     AnimationEvents OnHitboxOpen / OnHitboxClose).
//   - The per-swing hit list (prevents double-damage when capsules
//     drift in/out of the trigger arc).
//   - Damage application + hit-event raise on the target's Targetable.
//
// Per-enemy script — Q1 locked the verbatim-mirror approach over
// generalization. Future enemies copy the pattern. Generalization
// (shared Combat base) waits for the third duplicate.
//
// Single-direction dependency: EnemyCombat reads animation events +
// hitbox triggers; writes to nothing player-side except
// targetable.RaiseHit and stats.ApplyDamage. The Player receives
// the hit via its own Targetable.OnHit (M8) and reacts via
// PlayerHitReaction → PlayerCombat → PlayerAnimator. One direction.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Routes enemy attack-clip animation events into a child trigger
    /// hitbox, then routes hitbox intersections into damage on the
    /// hit target's <see cref="CharacterStatsRuntime"/> +
    /// <see cref="Targetable"/>. Mirrors
    /// <c>LevelGen.Player.PlayerCombat</c>'s hitbox path. Friendly-fire
    /// is hard-blocked: only Player-tagged targets receive damage.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [DisallowMultipleComponent]
    public class EnemyCombat : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("HP subtracted per successful hit. Hardcoded for now; " +
                 "WeaponStats SO with per-weapon values is a follow-up. " +
                 "Mirror of PlayerCombat.attackDamage default = 10.")]
        [SerializeField] private int _attackDamage = 10;

        [Tooltip("Duration in seconds the player is invulnerable after taking a hit from this enemy.")]
        [SerializeField] private float _iFrameDuration = 0.5f;

        [Header("Refs")]
        [Tooltip("Trigger collider on the enemy weapon. Enabled during the " +
                 "active frames of an attack via OnHitboxOpen / OnHitboxClose " +
                 "AnimationEvents. Default disabled.")]
        [SerializeField] private Collider _hitbox;

        // Targets struck during the current swing — cleared on OnHitboxOpen.
        // Prevents double-hits when the collider re-enters the same trigger
        // (capsules can drift in/out of the BoxCollider arc).
        private readonly HashSet<Targetable> _currentAttackHitList = new HashSet<Targetable>();

        /// <summary>
        /// M13-EnemyBase entry point. Pushes EnemyData-driven damage value
        /// into this script's <c>_attackDamage</c> SerializeField. Called
        /// by <see cref="LevelGen.Enemy.EnemyBase"/> at Awake. Cast from
        /// float (EnemyData) → int (EnemyCombat); fractional damage is a
        /// future-scope concern.
        /// </summary>
        public void InitFromEnemyData(EnemyData data)
        {
            if (data == null) return;
            _attackDamage = Mathf.RoundToInt(data.attackDamage);
        }

        // ── AnimationEvent endpoints (called via EnemyAnimationEventForwarder) ─

        /// <summary>
        /// Animation-event callback. Clears the per-attack hit list and
        /// enables the hitbox collider so subsequent OnTriggerEnter calls
        /// can deal damage. Public because the Forwarder relays to it
        /// from the Animator's GameObject.
        /// </summary>
        public void OnHitboxOpen()
        {
            if (_hitbox == null)
            {
                Debug.LogError("[EnemyCombat] OnHitboxOpen fired but '_hitbox' is unassigned. " +
                               "Re-run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'.", this);
                return;
            }
            _currentAttackHitList.Clear();
            _hitbox.enabled = true;
        }

        /// <summary>
        /// Animation-event callback. Disables the hitbox collider — no more
        /// damage frames for this swing. Belt-and-suspenders clears the
        /// hit list too (OnHitboxOpen also clears at the start of the
        /// next swing).
        /// </summary>
        public void OnHitboxClose()
        {
            if (_hitbox == null) return;
            _hitbox.enabled = false;
            _currentAttackHitList.Clear();
        }

        // ── Hitbox routing (called from EnemyHitboxRelay.OnTriggerEnter) ───

        /// <summary>
        /// Called by <see cref="EnemyHitboxRelay"/> when the weapon's
        /// trigger collider enters another collider. Resolves the hit
        /// to a <see cref="Targetable"/> + <see cref="CharacterStatsRuntime"/>
        /// pair, applies <see cref="_attackDamage"/> once per attack, and
        /// records the target so the same swing can't double-hit.
        /// Friendly-fire blocked: only Player-tagged targets receive damage.
        /// </summary>
        public void NotifyHitboxTriggered(Collider other)
        {
            if (other == null) return;

            var stats = other.GetComponentInParent<CharacterStatsRuntime>();
            if (stats == null) return;

            // IsDead guard — Death is terminal in M5/M4-B. Without this,
            // a dead Player would still receive damage events (no real
            // harm — HP clamps at 0) but PlayerHitReaction would re-fire
            // GetHit01, interrupting Die01. Stops corpse-flinch.
            if (stats.IsDead) return;

            var targetable = other.GetComponentInParent<Targetable>();
            if (targetable == null)
            {
                Debug.LogWarning($"[EnemyCombat] Hit {other.name} with stats but no Targetable; skipping. " +
                                 "Misconfiguration: CharacterStatsRuntime + Targetable should live on the same root.",
                                 stats);
                return;
            }

            if (_currentAttackHitList.Contains(targetable)) return;

            // Friendly-fire guard. Without this, two enemies in melee range
            // would damage each other on swings (their own CapsuleColliders
            // carry CharacterStatsRuntime + Targetable too). M11 hard-codes
            // "Player-tagged targets only"; future M-Factions milestone
            // replaces this with team / faction IDs.
            if (!stats.CompareTag("Player")) return;

            int dmg = _attackDamage;
            Vector3 hitPoint = (_hitbox != null)
                ? other.ClosestPoint(_hitbox.bounds.center)
                : other.bounds.center;

            // Requires player root or hitbox ancestor to be tagged "Player"
            stats.ApplyDamage(dmg);

            // Post-hit i-frame window (M11.1). Grant a brief invulnerability
            // window so rapid consecutive swings (or multiple enemies in melee)
            // can't stack-damage the player every single frame. Only start the
            // coroutine if the target survived (IsDead → no point granting
            // invuln on a fresh corpse; invuln state would linger until the
            // coroutine resolves 0.5s later and may suppress a Death trigger).
            if (!stats.IsDead && _iFrameDuration > 0f)
                StartCoroutine(GrantIFrames(stats));

            targetable.RaiseHit(hitPoint, dmg);
            _currentAttackHitList.Add(targetable);

            Debug.Log($"[EnemyCombat] {gameObject.name} hit {targetable.name} for {dmg} " +
                      $"(HP now {stats.CurrentHP}/{stats.MaxHP}).");
        }

        // ── I-frame coroutine ─────────────────────────────────────────────────

        /// <summary>
        /// Grants the target a brief invulnerability window after being hit.
        /// Starts after <see cref="ApplyDamage"/> returns (so damage this swing
        /// already applied); ends after <see cref="_iFrameDuration"/> seconds.
        /// Guarded against a null stats ref (defensive; callers already check)
        /// and against re-granting invuln to a target that died during the window.
        /// </summary>
        private IEnumerator GrantIFrames(CharacterStatsRuntime stats)
        {
            if (stats == null) yield break;
            stats.SetInvulnerable(true);
            yield return new WaitForSeconds(_iFrameDuration);
            // Don't clear invuln on a dead target — EnemyDeath / PlayerDeath
            // already cleaned up; lingering SetInvulnerable(false) is harmless
            // but tidy to skip.
            if (stats != null && !stats.IsDead)
                stats.SetInvulnerable(false);
        }
    }
}
