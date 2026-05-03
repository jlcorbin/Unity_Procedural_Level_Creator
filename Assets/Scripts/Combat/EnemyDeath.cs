// EnemyDeath.cs — sole owner of the death sequence on an enemy.
//
// Subscribes to CharacterStatsRuntime.OnDied (single-fire, raised
// inside ApplyDamage when HP transitions from >0 to <=0). On
// invocation: disables Targetable + Collider + EnemyHitReaction
// so the corpse stops being attackable, fires the Animator's
// Death trigger, schedules Destroy(gameObject, despawnDelay).
//
// Coexists with EnemyHitReaction on the same Animator. Each owns
// exactly one Animator parameter (Hit vs Death) — single-writer-
// per-parameter invariant preserved.

using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Sole owner of the death sequence on an enemy GameObject.
    /// Subscribes to <see cref="CharacterStatsRuntime.OnDied"/>,
    /// fires the Animator's Death trigger, disables Targetable +
    /// Collider + EnemyHitReaction so the corpse stops being
    /// attackable, and schedules a Destroy after
    /// <see cref="despawnDelay"/> seconds.
    /// Sole writer to the Animator's Death parameter.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [RequireComponent(typeof(Targetable))]
    [DisallowMultipleComponent]
    public class EnemyDeath : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Animator that will receive the Death trigger. " +
                 "Auto-resolved on Reset() to a child Animator.")]
        [SerializeField] private Animator animator;

        [Tooltip("Collider to disable on death. Auto-resolved on " +
                 "Reset() to the first Collider on this GameObject " +
                 "or its children.")]
        [SerializeField] private Collider deathCollider;

        [Tooltip("EnemyHitReaction to disable on death. " +
                 "Auto-resolved on Reset() to the sibling component.")]
        [SerializeField] private EnemyHitReaction hitReaction;

        [Header("Despawn")]
        [Tooltip("Seconds after death before the GameObject is " +
                 "destroyed. Set <= 0 to keep the corpse forever.")]
        [SerializeField] private float despawnDelay = 5f;

        private CharacterStatsRuntime _stats;
        private Targetable            _targetable;
        private bool                  _hasFired;

        private static readonly int DeathTriggerHash = Animator.StringToHash("Death");

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            deathCollider = GetComponent<Collider>();
            if (deathCollider == null)
                deathCollider = GetComponentInChildren<Collider>();
            hitReaction = GetComponent<EnemyHitReaction>();
        }

        private void Awake()
        {
            _stats      = GetComponent<CharacterStatsRuntime>();
            _targetable = GetComponent<Targetable>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (deathCollider == null)
            {
                deathCollider = GetComponent<Collider>();
                if (deathCollider == null)
                    deathCollider = GetComponentInChildren<Collider>();
            }
            if (hitReaction == null)
                hitReaction = GetComponent<EnemyHitReaction>();
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

            // 1. Stop being a target. Future hitbox triggers find no
            //    Targetable on the parent and skip the corpse.
            if (_targetable != null) _targetable.enabled = false;

            // 2. Stop blocking. Player can walk through.
            if (deathCollider != null) deathCollider.enabled = false;

            // 3. Stop reacting to hits visually. Belt-and-suspenders —
            //    EnemyHitReaction also has an IsDead guard, but disabling
            //    the component releases the OnHit subscription cleanly.
            if (hitReaction != null) hitReaction.enabled = false;

            // 4. Play Death.
            if (animator != null) animator.SetTrigger(DeathTriggerHash);

            // 5. Schedule despawn. <=0 means keep the corpse indefinitely
            //    (test scene convenience — not a gameplay default).
            if (despawnDelay > 0f) Destroy(gameObject, despawnDelay);
        }
    }
}
