// TargetLock.cs — M15. Sphere-cast target acquisition and lock-on.
//
// Right Mouse Button toggles lock-on. When locked, the player body faces the
// target each frame (strafe model) and a LockIndicator billboard floats above
// the enemy. Lock auto-clears if the target dies or moves beyond _breakRange.
//
// PlayerHero.Awake wires this component by calling InitFromPlayerHero(inputReader).
// TargetLock.InitFromPlayerHero subscribes to OnLockOnPerformed; no inspector
// wiring is needed for the input event.

using UnityEngine;
using LevelGen.Combat;

namespace LevelGen
{
    /// <summary>
    /// Manages target lock-on acquisition, hold, and release for the player.
    /// Right Mouse Button (or any binding on the LockOn action) toggles the
    /// locked state. While locked, <see cref="IsLocked"/> is true and
    /// <see cref="LockedTarget"/> holds the current enemy's
    /// <see cref="Targetable"/>. PlayerController reads these each frame to
    /// rotate toward the target and translate WASD relative to that facing.
    /// </summary>
    /// <remarks>
    /// Singleton scope is per-scene (no DontDestroyOnLoad). Duplicate
    /// instances log a warning and self-destroy (NOT the GameObject). Safe
    /// to run alongside all other player components — no Animator writes,
    /// no CharacterController writes.
    /// </remarks>
    [DefaultExecutionOrder(-40)]   // after EnemyBase (-50), before default (0)
    [DisallowMultipleComponent]
    public class TargetLock : MonoBehaviour
    {
        // ── SerializeFields ──────────────────────────────────────────────────

        [SerializeField]
        [Tooltip("Maximum distance at which a target can be acquired and held. " +
                 "Exceeding _breakRange auto-unlocks.")]
        private float _lockRange = 20f;

        [SerializeField]
        [Tooltip("Distance beyond which the lock is automatically released. " +
                 "Should be greater than _lockRange to create a hysteresis band.")]
        private float _breakRange = 25f;

        [SerializeField]
        [Tooltip("Radius of the sphere used in Physics.SphereCastAll during " +
                 "target acquisition. Wider = easier to acquire off-center targets.")]
        private float _sphereCastRadius = 3f;

        [SerializeField]
        [Tooltip("Layer mask for the sphere cast. Set to the layer(s) carrying " +
                 "enemy colliders so the cast ignores terrain and props.")]
        private LayerMask _targetLayer;

        [SerializeField]
        [Tooltip("Height above the player root used as the sphere cast origin, " +
                 "to cast from roughly eye level.")]
        private float _eyeHeight = 1.2f;

        [SerializeField]
        [Tooltip("Optional indicator prefab override. If null (default), " +
                 "TargetLock creates a fresh GameObject and adds LockIndicator " +
                 "via AddComponent. Leave null unless you need a pre-authored prefab.")]
        private GameObject _indicatorPrefab;

        // ── Private state ────────────────────────────────────────────────────
        private Player.PlayerInputReader _inputReader;
        private LockIndicator _indicator;
        private Camera _cam;

        // ── Singleton ────────────────────────────────────────────────────────

        /// <summary>Per-scene singleton. Set in Awake; cleared on Destroy.</summary>
        public static TargetLock Instance { get; private set; }

        // ── Public read API ──────────────────────────────────────────────────

        /// <summary>Whether a target is currently locked.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>The currently locked <see cref="Targetable"/>, or null when unlocked.</summary>
        public Targetable LockedTarget { get; private set; }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[TargetLock] Duplicate instance detected on " +
                                 $"'{name}'. Destroying the duplicate component " +
                                 "(NOT the GameObject).", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDisable()
        {
            if (_inputReader != null)
                _inputReader.OnLockOnPerformed -= HandleLockOnInput;

            // Clean up any active lock so the indicator doesn't linger.
            if (IsLocked)
                Unlock();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!IsLocked || LockedTarget == null) return;

            // Auto-unlock when the target wanders beyond the break range.
            if (Vector3.Distance(transform.position, LockedTarget.transform.position) > _breakRange)
                Unlock();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="LevelGen.Player.PlayerHero"/>.Awake to inject
        /// the input reader dependency. Subscribes to
        /// <see cref="LevelGen.Player.PlayerInputReader.OnLockOnPerformed"/>.
        /// </summary>
        /// <param name="inputReader">The PlayerInputReader on the same prefab root.</param>
        public void InitFromPlayerHero(Player.PlayerInputReader inputReader)
        {
            _inputReader = inputReader;
            if (_inputReader != null)
                _inputReader.OnLockOnPerformed += HandleLockOnInput;
        }

        // ── Input handler ────────────────────────────────────────────────────

        private void HandleLockOnInput()
        {
            if (IsLocked)
                Unlock();
            else
                TryAcquire();
        }

        // ── Acquire / Lock / Unlock ──────────────────────────────────────────

        /// <summary>
        /// Performs a sphere cast from player eye-level in the camera's forward
        /// direction. Collects all <see cref="Targetable"/> candidates within
        /// <see cref="_lockRange"/>, skips dead ones, and locks onto the one
        /// with the smallest angle from camera forward.
        /// </summary>
        private void TryAcquire()
        {
            // Lazy camera cache — same pattern as EnemyHealthBar / LockIndicator.
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            Vector3 origin    = transform.position + Vector3.up * _eyeHeight;
            Vector3 direction = _cam.transform.forward;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, _sphereCastRadius, direction, _lockRange, _targetLayer);

            Targetable bestCandidate  = null;
            float      bestAngle      = float.MaxValue;

            foreach (var hit in hits)
            {
                // Walk up the hierarchy to find a Targetable (collider may be
                // on a child, Targetable lives on the root).
                var targetable = hit.collider.GetComponentInParent<Targetable>();
                if (targetable == null) continue;

                // Skip dead targets — locking onto a corpse is not useful.
                var stats = targetable.GetComponent<CharacterStatsRuntime>();
                if (stats != null && stats.IsDead) continue;

                // Prefer the candidate with the smallest angle from camera forward.
                Vector3 toCandidate = targetable.transform.position - _cam.transform.position;
                float angle = Vector3.Angle(_cam.transform.forward, toCandidate);
                if (angle < bestAngle)
                {
                    bestAngle     = angle;
                    bestCandidate = targetable;
                }
            }

            if (bestCandidate != null)
                Lock(bestCandidate);
        }

        /// <summary>
        /// Locks onto <paramref name="target"/>: subscribes to its death event,
        /// spawns the lock indicator, and forces the health bar visible.
        /// </summary>
        private void Lock(Targetable target)
        {
            IsLocked    = true;
            LockedTarget = target;

            // Subscribe to the CharacterStatsRuntime.OnDied event so we
            // auto-unlock when the target's HP hits zero.
            var stats = target.GetComponent<CharacterStatsRuntime>();
            if (stats != null)
                stats.OnDied += OnTargetDied;

            // Spawn indicator. Per spec: create a fresh GameObject and use
            // AddComponent so there is no prefab dependency (the _indicatorPrefab
            // SerializeField is a convenience override but defaults to null).
            GameObject indicatorGo;
            if (_indicatorPrefab != null)
            {
                indicatorGo = Instantiate(_indicatorPrefab);
            }
            else
            {
                indicatorGo = new GameObject("LockIndicator");
                indicatorGo.AddComponent<MeshFilter>();
                indicatorGo.AddComponent<MeshRenderer>();
                _indicator = indicatorGo.AddComponent<LockIndicator>();
            }

            if (_indicator == null)
                _indicator = indicatorGo.GetComponent<LockIndicator>();

            _indicator.Init(target.transform);

            // Force the enemy's health bar visible while locked.
            target.GetComponent<EnemyHealthBar>()?.SetVisible(true);
        }

        /// <summary>
        /// Releases the current lock: unsubscribes from the target's death
        /// event, destroys the indicator, hides the health bar (unless the
        /// target has already died — EnemyHealthBar hides itself on death).
        /// </summary>
        private void Unlock()
        {
            if (LockedTarget != null)
            {
                // Unsubscribe from the CharacterStatsRuntime.OnDied event.
                var stats = LockedTarget.GetComponent<CharacterStatsRuntime>();
                if (stats != null)
                    stats.OnDied -= OnTargetDied;

                // Hide health bar only if the target is still alive;
                // EnemyHealthBar already hides itself on death so calling
                // SetVisible(false) on a dead target would double-hide and
                // could race with the despawn sequence.
                if (stats == null || !stats.IsDead)
                    LockedTarget.GetComponent<EnemyHealthBar>()?.SetVisible(false);
            }

            // Destroy the indicator GameObject (owns LockIndicator + mesh).
            if (_indicator != null)
            {
                Destroy(_indicator.gameObject);
                _indicator = null;
            }

            IsLocked     = false;
            LockedTarget = null;
        }

        /// <summary>
        /// Callback for <see cref="CharacterStatsRuntime.OnDied"/>.
        /// Auto-unlocks when the locked target's HP reaches zero.
        /// Internal null-check prevents double-unsub if called multiple times.
        /// </summary>
        private void OnTargetDied(CharacterStatsRuntime stats)
        {
            Unlock();
        }
    }
}
