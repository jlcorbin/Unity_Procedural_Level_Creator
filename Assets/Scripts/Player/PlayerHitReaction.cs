// PlayerHitReaction.cs — symmetric to EnemyHitReaction (M11).
//
// Subscribes to its own Targetable.OnHit, calls PlayerCombat.TakeHit().
// PlayerCombat owns the Animator parameter writes (single-writer-per-
// parameter invariant); this script is a pure consumer of the OnHit
// event that delegates to PlayerCombat for the response.
//
// Q4 (M11 prompt): NO stagger window. Every hit triggers a flinch.
// Souls-like stunlock behavior. With one Dummy on a 1.5s cooldown
// stunlock isn't a problem; revisit if multi-enemy playtest demands it.
//
// Single-direction wiring: Targetable raises OnHit → PlayerHitReaction
// reads → PlayerCombat.TakeHit → PlayerAnimator.SetHitTrigger →
// Animator transitions to Hit. PlayerHitReaction never writes to
// Animator parameters directly.

using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Bridge from <see cref="Targetable.OnHit"/> on the Player to the
    /// existing M2-B Hit reaction flow via <see cref="PlayerCombat.TakeHit"/>.
    /// Mirror of <see cref="EnemyHitReaction"/> minus the stagger
    /// window (Q4 — Player flinches on every hit).
    /// </summary>
    [RequireComponent(typeof(Targetable))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [DisallowMultipleComponent]
    public class PlayerHitReaction : MonoBehaviour
    {
        private Targetable             _targetable;
        private PlayerCombat           _combat;
        private CharacterStatsRuntime  _stats;

        private void Awake()
        {
            _targetable = GetComponent<Targetable>();
            _combat     = GetComponent<PlayerCombat>();
            _stats      = GetComponent<CharacterStatsRuntime>();
        }

        private void OnEnable()
        {
            if (_targetable != null) _targetable.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (_targetable != null) _targetable.OnHit -= HandleHit;
        }

        private void HandleHit(Vector3 hitPoint, float damage)
        {
            // Defense in depth: PlayerCombat.TakeHit already early-returns
            // on _stats.IsDead (M5), but a dead player should never flinch
            // under any path. Belt-and-suspenders against a future TakeHit
            // refactor that drops the IsDead guard.
            if (_stats != null && _stats.IsDead) return;

            // damage param ignored — flinch is binary, doesn't scale with
            // damage value. Same as EnemyHitReaction.
            if (_combat != null) _combat.TakeHit();
        }
    }
}
