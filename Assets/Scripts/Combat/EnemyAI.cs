// EnemyAI.cs — basic FSM for the Dummy (M10).
//
// States: Idle → Chase → Attack → Cooldown → re-evaluate.
// NavMeshAgent owns position/rotation while moving; EnemyAI takes
// over rotation while stopped (Cooldown / Attack swing) so the
// next attack lands toward the player.
//
// Single-direction dependency: EnemyAI READS from PlayerHUD-style
// tag-found player Transform; READS animator state for "is in Hit"
// suspension; WRITES MoveSpeed (Float) and Attack (Trigger). Does
// NOT subscribe to PlayerDeath — chasing a dead player is harmless
// in M10 (no damage applied).
//
// Single-writer-per-Animator-parameter invariant:
//   Hit       — EnemyHitReaction (M4-A)
//   Death     — EnemyDeath       (M4-B)
//   MoveSpeed — EnemyAI          (M10)
//   Attack    — EnemyAI          (M10)
// Disjoint sets; no parameter has two writers.

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace LevelGen.Combat
{
    /// <summary>
    /// Per-frame FSM AI for a single-target enemy. Pursues the
    /// Player-tagged GameObject within <see cref="_detectionRange"/>,
    /// closes to <see cref="_attackRange"/>, swings on cooldown,
    /// returns to Idle if the Player escapes <see cref="_leashRange"/>.
    /// Attack swing fires the Animator's Attack trigger; the clip's
    /// own AnimationEvents (OnHitboxOpen / OnHitboxClose) are absorbed
    /// by <see cref="EnemyAnimationEventAbsorber"/> on the Animator's
    /// GameObject — M10 ships no damage application to player.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [DisallowMultipleComponent]
    public class EnemyAI : MonoBehaviour
    {
        public enum State { Idle, Chase, Attack, Cooldown }

        [Header("Refs (auto-resolved on Reset)")]
        [Tooltip("Animator on the visible model child. Auto-set on Reset() to a child Animator.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Tag used to find the Player. Default 'Player'. Re-resolved via " +
                 "GameObject.FindGameObjectWithTag at Awake (mirrors PlayerHUD pattern).")]
        [SerializeField] private string _playerTag = "Player";

        [Header("Ranges")]
        [Tooltip("Distance at which Idle → Chase fires. Default 8m.")]
        [SerializeField] private float _detectionRange = 8.0f;

        [Tooltip("Distance at which Chase → Attack fires. Default 1.8m.")]
        [SerializeField] private float _attackRange = 1.8f;

        [Tooltip("Distance at which Chase → Idle fires (player escaped). Default 15m.")]
        [SerializeField] private float _leashRange = 15.0f;

        [Header("Movement")]
        [Tooltip("NavMeshAgent.speed during Chase. Default 2.5 m/s.")]
        [SerializeField] private float _chaseSpeed = 2.5f;

        [Tooltip("NavMeshAgent.stoppingDistance during Chase. Slightly less " +
                 "than _attackRange so the agent doesn't oscillate at the boundary. " +
                 "Default 1.5m.")]
        [SerializeField] private float _stoppingDistance = 1.5f;

        [Tooltip("Degrees/sec for manual Quaternion.RotateTowards while agent is " +
                 "stopped (Cooldown + Attack). Default 540°/s.")]
        [SerializeField] private float _turnSpeed = 540f;

        [Header("Combat")]
        [Tooltip("Seconds between attacks (measured end-of-attack-anim → " +
                 "start-of-next-attack-eligible). Default 1.5s.")]
        [SerializeField] private float _attackCooldown = 1.5f;

        // ── Cached refs / state ─────────────────────────────────────────────
        private NavMeshAgent          _agent;
        private CharacterStatsRuntime _stats;
        private Transform             _player;
        private State                 _state = State.Idle;
        private float                 _attackCompletedAt = -999f;
        private Coroutine             _attackCoroutine;

        private static readonly int HashMoveSpeed       = Animator.StringToHash("MoveSpeed");
        private static readonly int HashAttackParam     = Animator.StringToHash("Attack");
        private static readonly int HashHitState        = Animator.StringToHash("Hit");
        private static readonly int HashAttackState     = Animator.StringToHash("Attack");

        /// <summary>Read-only FSM state for inspector / validators.</summary>
        public State CurrentState => _state;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Reset()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<CharacterStatsRuntime>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            // Initial agent setup. Inspector defaults on the prefab also
            // populate these, but writing them here keeps the runtime
            // single source of truth.
            _agent.speed            = _chaseSpeed;
            _agent.stoppingDistance = _stoppingDistance;
            _agent.isStopped        = true;
        }

        private void Start()
        {
            TryFindPlayer();
            if (_player == null) StartCoroutine(WaitForPlayer());
        }

        private void TryFindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag(_playerTag);
            if (go != null) _player = go.transform;
        }

        private IEnumerator WaitForPlayer()
        {
            var wait = new WaitForSeconds(0.5f);
            while (_player == null)
            {
                yield return wait;
                TryFindPlayer();
            }
        }

        // ── Per-frame ───────────────────────────────────────────────────────

        private void Update()
        {
            // Death is terminal — EnemyDeath owns the cleanup, FSM
            // permanently early-returns once IsDead is true.
            if (_stats != null && _stats.IsDead) return;
            if (_player == null) return;

            // Hit reaction (M4-A) owns the moment. Suspend FSM tick;
            // resume next frame if Hit clip transitioned out.
            if (IsInHitState()) return;

            float distance = Vector3.Distance(transform.position, _player.position);

            switch (_state)
            {
                case State.Idle:     TickIdle(distance);     break;
                case State.Chase:    TickChase(distance);    break;
                case State.Attack:   /* coroutine owns */    break;
                case State.Cooldown: TickCooldown(distance); break;
            }

            // Drive locomotion blend tree. agent.velocity.magnitude /
            // chaseSpeed maps to [0,1] for the 1D blend (Idle@0 → MoveFWD@1).
            // Stopped agent (Cooldown / Attack) reports 0 → blend rests on Idle.
            if (_animator != null)
            {
                float speed01 = _agent.isStopped
                    ? 0f
                    : _agent.velocity.magnitude / Mathf.Max(0.01f, _chaseSpeed);
                _animator.SetFloat(HashMoveSpeed, Mathf.Clamp01(speed01));
            }
        }

        private bool IsInHitState()
        {
            if (_animator == null) return false;
            var info = _animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == HashHitState;
        }

        // ── State ticks ─────────────────────────────────────────────────────

        private void TickIdle(float distance)
        {
            if (distance <= _detectionRange) EnterChase();
        }

        private void TickChase(float distance)
        {
            if (distance > _leashRange) { EnterIdle(); return; }
            if (distance <= _attackRange && Time.time - _attackCompletedAt >= _attackCooldown)
            {
                EnterAttack();
                return;
            }
            // Per-frame SetDestination is cheap (agent caches and only
            // re-paths when the destination changes meaningfully).
            _agent.SetDestination(_player.position);
        }

        private void TickCooldown(float distance)
        {
            // Face the player during cooldown so the next attack lands.
            FacePlayer();

            if (Time.time - _attackCompletedAt >= _attackCooldown)
            {
                if      (distance <= _attackRange)    EnterAttack();
                else if (distance <= _detectionRange) EnterChase();
                else                                  EnterIdle();
            }
        }

        private void FacePlayer()
        {
            if (_player == null) return;
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(toPlayer.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, _turnSpeed * Time.deltaTime);
        }

        // ── Transitions ─────────────────────────────────────────────────────

        private void EnterIdle()
        {
            _state = State.Idle;
            _agent.isStopped = true;
            if (_agent.hasPath) _agent.ResetPath();
        }

        private void EnterChase()
        {
            _state = State.Chase;
            _agent.isStopped        = false;
            _agent.speed            = _chaseSpeed;
            _agent.stoppingDistance = _stoppingDistance;
        }

        private void EnterAttack()
        {
            _state = State.Attack;
            _agent.isStopped = true;
            _agent.velocity  = Vector3.zero;

            if (_animator != null) _animator.SetTrigger(HashAttackParam);

            if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
            _attackCoroutine = StartCoroutine(AttackCoroutine());
        }

        private IEnumerator AttackCoroutine()
        {
            // Wait one frame so AnyState→Attack actually transitions
            // (SetTrigger is queued; the transition fires on the next
            // Animator update).
            yield return null;

            // Watch normalizedTime; matches the controller's 0.92 exit-time.
            const float exitNT = 0.92f;
            while (true)
            {
                if (_animator == null) break;
                if (_stats != null && _stats.IsDead) break;
                var info = _animator.GetCurrentAnimatorStateInfo(0);

                // Bail early if the state changed unexpectedly (Hit, Death).
                // M4-A AnyState→Hit and M4-B AnyState→Death both have priority;
                // breaking out here lets EnterCooldown still run so the FSM
                // doesn't deadlock waiting for an Attack state we never reach.
                if (info.shortNameHash != HashAttackState) break;
                if (info.normalizedTime >= exitNT) break;

                // Manual face-during-swing — agent is stopped so its auto-
                // rotation doesn't apply.
                FacePlayer();
                yield return null;
            }

            _attackCompletedAt = Time.time;
            _state = State.Cooldown;
        }
    }
}
