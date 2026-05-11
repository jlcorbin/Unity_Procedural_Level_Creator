// EnemyData.cs — per-enemy ScriptableObject template (M13-EnemyBase).
//
// Replaces hardcoded SerializeField defaults on EnemyAI / EnemyCombat /
// CharacterStatsRuntime for enemy archetypes. One asset per enemy type
// (Grunt, Brute, Archer, etc.); EnemyBase reads it at Awake and pushes
// values into the relevant components.
//
// Mirrors CharacterStats's pattern (ScriptableObject + CreateAssetMenu +
// OnValidate clamps) but is enemy-specific — stats covering AI ranges,
// movement, attack damage. CharacterStats remains the canonical
// HP/Stamina template for the player.
//
// Single authoring path: EnemyBase reads this and calls
// InitFromEnemyData on each consumer. Consumers (EnemyAI / EnemyCombat /
// CharacterStatsRuntime) never read EnemyData directly — they receive
// the values from EnemyBase to preserve a single-direction dependency.

using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Reusable per-enemy stats template. HP, attack damage, defense,
    /// movement speed, AI ranges, attack cooldown. Read by
    /// <see cref="LevelGen.Enemy.EnemyBase"/> at Awake and pushed into
    /// the runtime components on the enemy root.
    /// </summary>
    /// <remarks>
    /// One asset per enemy type. Duplicate the master template, tweak
    /// values, drop into Enemy_*.prefab via EnemyBase._data.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "LevelGen/Combat/Enemy Data",
        fileName = "EnemyData_New",
        order    = 110)]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Inspector label / future UI source.")]
        public string enemyName = "Enemy";

        [Header("Stats")]
        [Tooltip("Max health. Current health lives on CharacterStatsRuntime.")]
        public int maxHP = 100;

        [Tooltip("Damage applied per successful weapon-hitbox hit. " +
                 "Float for future tuning; cast to int on consumption.")]
        public float attackDamage = 10f;

        [Tooltip("Reserved for future damage-mitigation logic. Not yet consumed.")]
        public float defense = 5f;

        [Header("Movement")]
        [Tooltip("NavMeshAgent.speed during Chase. m/s.")]
        public float moveSpeed = 3.5f;

        [Tooltip("Reserved for future rotation tuning. Not yet consumed by EnemyAI " +
                 "(EnemyAI keeps its own _turnSpeed default for face-during-cooldown).")]
        public float rotationSpeed = 10f;

        [Header("AI Ranges")]
        [Tooltip("Distance at which Idle → Chase fires.")]
        public float detectionRange = 6f;

        [Tooltip("Distance at which Chase → Attack fires. Must be < detectionRange.")]
        public float attackRange = 1.3f;

        [Tooltip("Distance at which Chase → Idle fires (player escaped). Must be > detectionRange.")]
        public float leashRange = 10f;

        [Tooltip("NavMeshAgent.stoppingDistance during Chase. Slightly less than " +
                 "attackRange so the agent doesn't oscillate at the boundary.")]
        public float stoppingDistance = 1.0f;

        [Header("Timing")]
        [Tooltip("Seconds between attacks (measured end-of-attack-anim → " +
                 "start-of-next-attack-eligible).")]
        public float attackCooldown = 1.5f;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (maxHP < 1)
            {
                Debug.LogWarning($"[EnemyData] '{name}' maxHP was {maxHP}; clamped to 1.");
                maxHP = 1;
            }
            if (attackDamage < 0f)
            {
                Debug.LogWarning($"[EnemyData] '{name}' attackDamage was negative; clamped to 0.");
                attackDamage = 0f;
            }
            if (defense < 0f) defense = 0f;

            if (moveSpeed < 0f)        moveSpeed = 0f;
            if (rotationSpeed < 0f)    rotationSpeed = 0f;
            if (attackCooldown < 0f)   attackCooldown = 0f;
            if (stoppingDistance < 0f) stoppingDistance = 0f;
            if (attackRange < 0f)      attackRange = 0f;
            if (detectionRange < 0f)   detectionRange = 0f;
            if (leashRange < 0f)       leashRange = 0f;

            // Range ordering: attackRange < detectionRange < leashRange.
            // Clamp without auto-fixing — flag the misorder so the author
            // notices and intentionally chooses values.
            if (detectionRange <= attackRange)
            {
                Debug.LogWarning($"[EnemyData] '{name}' detectionRange ({detectionRange}) " +
                                 $"must be > attackRange ({attackRange}); bumping detectionRange.");
                detectionRange = attackRange + 0.1f;
            }
            if (leashRange <= detectionRange)
            {
                Debug.LogWarning($"[EnemyData] '{name}' leashRange ({leashRange}) " +
                                 $"must be > detectionRange ({detectionRange}); bumping leashRange.");
                leashRange = detectionRange + 0.1f;
            }
        }
#endif
    }
}
