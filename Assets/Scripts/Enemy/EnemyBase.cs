// EnemyBase.cs — M13-EnemyBase: root manifest for enemy archetypes.
//
// Enemy equivalent of PlayerHero. Declares the full required component
// set via [RequireComponent] and caches SerializeField refs to each.
// No gameplay logic — pure wiring contract + the canonical EnemyData
// push-down at Awake.
//
// Spec deviations from the M13-EnemyBase locked list:
// 1. Animator (Unity built-in) is NOT in the RequireComponent chain
//    because the Animator lives on the MaleCharacterPBR child by design
//    (FBX-mounted humanoid rig). EnemyAI / EnemyHitReaction / EnemyDeath
//    resolve it via GetComponentInChildren. Forcing [RequireComponent(
//    Animator)] on the root would auto-add a second, controller-less
//    Animator that conflicts with the child rig (same lesson as PlayerHero).
// 2. Execution order set to -50 so EnemyBase.Awake runs BEFORE
//    CharacterStatsRuntime.Awake / EnemyAI.Awake (both at default 0).
//    The push-down (InitFromEnemyData on each consumer) must complete
//    before consumers run their own Awake — otherwise the EnemyData
//    values would be overwritten by the consumer's own defaults.

using UnityEngine;
using UnityEngine.AI;
using LevelGen.Combat;

namespace LevelGen.Enemy
{
    /// <summary>
    /// Manifest component on the root of every enemy archetype prefab.
    /// Declares the full required component set via [RequireComponent]
    /// and exposes each as a SerializeField + public read-only property.
    /// Owns NO gameplay state, NO Update logic, NO Animator writes —
    /// it is solely a wiring contract.
    /// </summary>
    /// <remarks>
    /// The one piece of behavior here is the Awake push-down: EnemyBase
    /// reads its <c>_data</c> EnemyData asset and pushes the relevant
    /// values into <see cref="CharacterStatsRuntime.InitFromEnemyData"/>,
    /// <see cref="EnemyAI.InitFromEnemyData"/>,
    /// <see cref="EnemyCombat.InitFromEnemyData"/>, and onto the
    /// <see cref="NavMeshAgent"/>. Execution order -50 ensures this
    /// happens before consumers' own Awake runs.
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [RequireComponent(typeof(Targetable))]
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(EnemyCombat))]
    [RequireComponent(typeof(EnemyHitReaction))]
    [RequireComponent(typeof(EnemyDeath))]
    [DisallowMultipleComponent]
    public class EnemyBase : MonoBehaviour
    {
        // ── Data ────────────────────────────────────────────────────────────
        [Header("Data")]
        [Tooltip("Per-enemy stats template. Drives maxHP, attackDamage, " +
                 "AI ranges, movement speed, attack cooldown. " +
                 "Pushed into consumers in Awake via InitFromEnemyData.")]
        [SerializeField] private EnemyData _data;

        // ── Components (auto-wired by EnemyBaseBuilder) ─────────────────────
        [Header("Components")]
        [SerializeField] private CharacterStatsRuntime _stats;
        [SerializeField] private Targetable            _targetable;
        [SerializeField] private EnemyAI               _ai;
        [SerializeField] private EnemyCombat           _combat;
        [SerializeField] private EnemyHitReaction      _hitReaction;
        [SerializeField] private EnemyDeath            _death;

        [Tooltip("Animator on the visible model child (MaleCharacterPBR or " +
                 "equivalent). Not [RequireComponent] because it lives on a " +
                 "child by design; resolved by EnemyBaseBuilder at build time.")]
        [SerializeField] private Animator              _animator;

        [SerializeField] private NavMeshAgent          _agent;
        [SerializeField] private CapsuleCollider       _capsule;

        // ── Public read API ─────────────────────────────────────────────────
        public EnemyData              Data        => _data;
        public CharacterStatsRuntime  Stats       => _stats;
        public Targetable             Targetable  => _targetable;
        public EnemyAI                AI          => _ai;
        public EnemyCombat            Combat      => _combat;
        public EnemyHitReaction       HitReaction => _hitReaction;
        public EnemyDeath             Death       => _death;
        public Animator               Animator    => _animator;
        public NavMeshAgent           Agent       => _agent;
        public CapsuleCollider        Capsule     => _capsule;

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Belt-and-suspenders Awake: re-resolves any null SerializeField
        /// via GetComponent, then pushes EnemyData values into consumers
        /// BEFORE their own Awake runs (this script is at execution order
        /// -50; consumers default to 0).
        /// </summary>
        private void Awake()
        {
            // Self-heal null refs — builder is canonical, this is safety net.
            if (_stats       == null) _stats       = GetComponent<CharacterStatsRuntime>();
            if (_targetable  == null) _targetable  = GetComponent<Targetable>();
            if (_ai          == null) _ai          = GetComponent<EnemyAI>();
            if (_combat      == null) _combat      = GetComponent<EnemyCombat>();
            if (_hitReaction == null) _hitReaction = GetComponent<EnemyHitReaction>();
            if (_death       == null) _death       = GetComponent<EnemyDeath>();
            if (_agent       == null) _agent       = GetComponent<NavMeshAgent>();
            if (_capsule     == null) _capsule     = GetComponent<CapsuleCollider>();
            if (_animator    == null) _animator    = GetComponentInChildren<Animator>();

            // Push EnemyData into consumers. Each InitFromEnemyData is a
            // no-op if data is null — they keep their SerializeField
            // defaults in that case (validator flags _data null).
            if (_data == null)
            {
                Debug.LogError($"[EnemyBase] '{name}' has no EnemyData assigned. " +
                               $"Enemy will fall back to component defaults.", this);
                return;
            }

            if (_stats  != null) _stats.InitFromEnemyData(_data);
            if (_ai     != null) _ai.InitFromEnemyData(_data);
            if (_combat != null) _combat.InitFromEnemyData(_data);
            if (_agent  != null)
            {
                _agent.speed            = _data.moveSpeed;
                _agent.stoppingDistance = _data.stoppingDistance;
            }
        }
    }
}
