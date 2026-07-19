// RangedCombat.cs — M22 ranged charge-and-release (spec §8).
//
// Owns LMB in ranged stances (MagicWand 6, BowAndArrow 7). Press begins a
// (visual-only) charge; release fires one shot: plays the shot motion (reusing
// the Attack trigger) and spawns an ArrowProjectile aimed at the locked target,
// or — free aim — at whatever the camera-center ray hits (so the shot converges
// on the crosshair despite the over-the-shoulder camera offset).
//
// Single-direction dependency: subscribes to PlayerInputReader's AttackPressed /
// AttackReleased, reads StanceController.IsRanged, and writes only through
// PlayerAnimator's public API + an instantiated projectile. Coexists with
// PlayerCombat, which early-returns on AttackPressed while the stance is ranged.

using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Charge-and-release ranged attack for wand / bow stances. Inactive while
    /// the active stance is melee (PlayerCombat owns LMB there).
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    [RequireComponent(typeof(StanceController))]
    public class RangedCombat : MonoBehaviour
    {
        [Header("Projectile")]
        [Tooltip("Arrow/bolt prefab with an ArrowProjectile + Rigidbody + collider. Built via LevelGen ▶ Player ▶ Build Arrow Prefab.")]
        [SerializeField] private GameObject _arrowPrefab;

        [Tooltip("Muzzle / nock point the projectile spawns from (the bow's NockArrow point). If null, a point in front of the chest is used.")]
        [SerializeField] private Transform _muzzle;

        [Tooltip("Damage per shot. UE5 arrow damage 30 (UnrealUnits.ArrowDamage).")]
        [SerializeField] private int _damage = UnrealUnits.ArrowDamage;

        [Tooltip("Launch speed in m/s. UE5 6000 cm/s → 60 (UnrealUnits.ArrowSpeed).")]
        [SerializeField] private float _projectileSpeed = UnrealUnits.ArrowSpeed;

        [Header("Aim")]
        [Tooltip("Layers the free-aim camera ray can hit. Leave as Everything — the shooter is skipped by hierarchy, so no dedicated Player layer is needed.")]
        [SerializeField] private LayerMask _aimMask = ~0;

        [Tooltip("Height above a locked target's pivot to aim at (aim for the body, not the feet).")]
        [SerializeField] private float _targetAimHeight = 1.0f;

        [Header("Release timing")]
        [Tooltip("Normalized time within the shot clip at which the arrow leaves the bow/wand. 0.45 = 45% through the shot animation.")]
        [SerializeField, Range(0f, 1f)] private float _releaseNormalizedTime = 0.45f;

        [Tooltip("Safety fallback: if the tagged shot state isn't detected within this many seconds of release, the arrow fires anyway so a shot is never lost.")]
        [SerializeField] private float _maxReleaseDelay = 0.6f;

        // ── Cached refs / state ─────────────────────────────────────────────
        private PlayerInputReader _input;
        private PlayerAnimator _anim;
        private StanceController _stance;
        private CharacterStatsRuntime _stats;   // optional — dead-guard
        private Camera _cam;
        private bool _isCharging;

        // Deferred fire: the shot animation plays on release, but the arrow
        // leaves at _releaseNormalizedTime of the tagged shot state (frame-accurate).
        private bool _firePending;
        private float _fireDeadline;
        private static readonly int RangedShotTag = Animator.StringToHash("RangedShot");
        private static readonly int HitStateHash  = Animator.StringToHash("Hit");

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _anim = GetComponent<PlayerAnimator>();
            _stance = GetComponent<StanceController>();
            _stats = GetComponent<CharacterStatsRuntime>();
        }

        private void OnEnable()
        {
            if (_input != null)
            {
                _input.AttackPressed += OnAttackPressed;
                _input.AttackReleased += OnAttackReleased;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.AttackPressed -= OnAttackPressed;
                _input.AttackReleased -= OnAttackReleased;
            }
        }

        // ── Input handlers ──────────────────────────────────────────────────

        private void OnAttackPressed()
        {
            if (_stance == null || !_stance.IsRanged) return;
            if (_stats != null && _stats.IsDead) return;
            // No distinct held-draw pose yet (spec §8 limitation) — charge is a
            // flag only. A per-stance draw clip can be triggered here later.
            _isCharging = true;
        }

        private void OnAttackReleased()
        {
            if (!_isCharging) return;
            _isCharging = false;
            if (_stance == null || !_stance.IsRanged) return;   // stance may have changed mid-charge
            if (_stats != null && _stats.IsDead) return;

            // Play the shot motion now; the arrow spawns later, at the release
            // frame (see Update). In ranged stances the Attack state IS the fire
            // animation (spec §8). PlayerAnimator is the sole Animator writer.
            _anim?.SetAttackTrigger();
            _firePending = true;
            _fireDeadline = Time.time + _maxReleaseDelay;
        }

        private void Update()
        {
            if (!_firePending) return;

            if (_stats != null && _stats.IsDead) { _firePending = false; return; }

            var anim = _anim != null ? _anim.Animator : null;
            if (anim == null) { _firePending = false; return; }

            var info = anim.GetCurrentAnimatorStateInfo(0);

            // Interrupted by a hit → cancel the pending shot.
            if (info.shortNameHash == HitStateHash) { _firePending = false; return; }

            // Frame-accurate: fire once the tagged shot state reaches the release point.
            if (info.tagHash == RangedShotTag && (info.normalizedTime % 1f) >= _releaseNormalizedTime)
            {
                FireArrow();
                _firePending = false;
                return;
            }

            // Safety net: never lose a shot if the state isn't tagged / not detected.
            if (Time.time >= _fireDeadline)
            {
                FireArrow();
                _firePending = false;
            }
        }

        // ── Fire ────────────────────────────────────────────────────────────

        private void FireArrow()
        {
            Vector3 muzzlePos = MuzzlePosition();
            Vector3 aimDir = ComputeAimDirection(muzzlePos);

            if (_arrowPrefab == null)
            {
                Debug.LogWarning("[RangedCombat] No arrow prefab assigned — nothing spawned. " +
                                 "Run LevelGen ▶ Player ▶ Build Arrow Prefab and wire it.", this);
                return;
            }

            var go = Instantiate(_arrowPrefab, muzzlePos, Quaternion.LookRotation(aimDir));
            var proj = go.GetComponent<ArrowProjectile>();
            if (proj != null)
                proj.Initialize(aimDir * _projectileSpeed, gameObject, _damage);
            else
                Debug.LogWarning("[RangedCombat] Arrow prefab has no ArrowProjectile component.", go);
        }

        private Vector3 MuzzlePosition()
        {
            if (_muzzle != null) return _muzzle.position;
            // Fallback: a point in front of the chest so free flight looks sane
            // even before the bow's NockArrow point is wired.
            return transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
        }

        /// <summary>
        /// Spec §8 aim: if locked, aim straight at the locked target's body;
        /// otherwise cast the camera-center ray forward and aim at the hit point
        /// (or the far point on a miss) so the shot converges on the crosshair.
        /// </summary>
        private Vector3 ComputeAimDirection(Vector3 muzzlePos)
        {
            var lock_ = TargetLock.Instance;
            if (lock_ != null && lock_.IsLocked && lock_.LockedTarget != null)
            {
                Vector3 targetPoint = lock_.LockedTarget.transform.position
                                    + Vector3.up * _targetAimHeight;
                Vector3 d = targetPoint - muzzlePos;
                return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
            }

            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
            {
                Ray ray = _cam.ScreenPointToRay(
                    new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

                // Nearest hit that ISN'T the shooter (skipped by hierarchy, so no
                // Player layer is required). Falls back to the far point on a miss.
                Vector3 aimPoint = ray.GetPoint(UnrealUnits.FreeAimRayLength);
                var hits = Physics.RaycastAll(ray, UnrealUnits.FreeAimRayLength,
                    _aimMask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                foreach (var h in hits)
                {
                    if (h.transform.root == transform.root) continue; // ignore the shooter
                    if (h.distance < nearest)
                    {
                        nearest = h.distance;
                        aimPoint = h.point;
                    }
                }

                Vector3 d = aimPoint - muzzlePos;
                return d.sqrMagnitude > 0.0001f ? d.normalized : _cam.transform.forward;
            }

            return transform.forward;
        }
    }
}
