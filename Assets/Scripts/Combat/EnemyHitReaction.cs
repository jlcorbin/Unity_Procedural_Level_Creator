// EnemyHitReaction.cs — subscribes to a sibling Targetable's OnHit
// event and fires the Animator's Hit trigger. Sole writer to the
// Hit parameter on this enemy's Animator.
//
// Stagger window (script-side): within `staggerWindow` seconds of
// the last fired Hit, additional OnHit calls are swallowed (visual
// only — damage application is upstream and unaffected).

using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Bridge from <see cref="Targetable.OnHit"/> to the Animator's
    /// Hit trigger. Holds a script-side stagger cooldown to prevent
    /// the Hit clip from re-firing during its own playback.
    /// Single-writer-to-Animator invariant: this is the sole script
    /// writing to this enemy's Animator parameters.
    /// </summary>
    [RequireComponent(typeof(Targetable))]
    [DisallowMultipleComponent]
    public class EnemyHitReaction : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator that will receive the Hit trigger. " +
                 "Auto-resolved on Reset() to a child Animator.")]
        [SerializeField] private Animator animator;

        [Header("Stagger")]
        [Tooltip("Minimum seconds between visible Hit reactions. " +
                 "Hits within this window are swallowed visually " +
                 "(damage application is unaffected).")]
        [SerializeField] private float staggerWindow = 0.3f;

        private Targetable             _targetable;
        private CharacterStatsRuntime  _stats;
        private float                  _lastHitTime = -999f;

        private static readonly int HitTriggerHash = Animator.StringToHash("Hit");

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            _targetable = GetComponent<Targetable>();
            _stats      = GetComponent<CharacterStatsRuntime>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (_targetable != null) _targetable.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (_targetable != null) _targetable.OnHit -= HandleHit;
        }

        private void HandleHit(Vector3 hitPoint)
        {
            if (animator == null) return;

            // Same-frame OnHit/OnDied ordering insurance: if HP just
            // crossed 0 in the same ApplyDamage call that raised this
            // OnHit, IsDead is already true. EnemyDeath will disable
            // this component shortly, but a leaked SetTrigger here
            // would queue a Hit-after-Death.
            if (_stats != null && _stats.IsDead) return;

            float now = Time.time;
            if (now - _lastHitTime < staggerWindow) return;

            _lastHitTime = now;
            animator.SetTrigger(HitTriggerHash);
        }
    }
}
