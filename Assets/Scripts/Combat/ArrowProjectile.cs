// ArrowProjectile.cs — M22 ranged projectile (spec §8, BP_Arrow).
//
// Rigidbody projectile fired by RangedCombat. Launches at a fixed speed, arcs
// under reduced gravity (0.4×), rotates to follow its velocity, deals damage to
// the first CharacterStatsRuntime it hits, ignores its own shooter, and
// self-destroys on impact or after its lifespan.

using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Combat
{
    /// <summary>
    /// A launched arrow / bolt. Call <see cref="Initialize"/> immediately after
    /// instantiation to set velocity, shooter (for self-hit ignore), and damage.
    /// Values default to the UE5-parity constants in
    /// <see cref="LevelGen.Player.UnrealUnits"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowProjectile : MonoBehaviour
    {
        [Header("Flight (UE5 parity — see UnrealUnits)")]
        [Tooltip("Fraction of world gravity applied to the arrow. UE5 0.4 → a gentle arc.")]
        [SerializeField] private float _gravityScale = LevelGen.Player.UnrealUnits.ArrowGravityScale;

        [Tooltip("Seconds before the arrow self-destroys if it never hits anything. UE5 5 s.")]
        [SerializeField] private float _lifespan = LevelGen.Player.UnrealUnits.ArrowLifespan;

        [Tooltip("Rotate the arrow each physics step to point along its velocity.")]
        [SerializeField] private bool _rotateToVelocity = true;

        private Rigidbody _rb;
        private GameObject _shooter;
        private int _damage = LevelGen.Player.UnrealUnits.ArrowDamage;
        private bool _consumed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;                 // custom 0.4× gravity in FixedUpdate
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 60 m/s → avoid tunneling
        }

        /// <summary>
        /// Arms the arrow: sets launch velocity (direction × speed), records the
        /// shooter so the arrow ignores its own colliders, and sets the on-hit
        /// damage. Also schedules the lifespan self-destruct.
        /// </summary>
        /// <param name="velocity">World launch velocity (m/s). Magnitude is the speed.</param>
        /// <param name="shooter">The firing actor; its colliders are ignored.</param>
        /// <param name="damage">Damage applied to the first target hit.</param>
        public void Initialize(Vector3 velocity, GameObject shooter, int damage)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _shooter = shooter;
            _damage = damage;

            _rb.useGravity = false;
            _rb.linearVelocity = velocity;          // Unity 6.x: linearVelocity (velocity deprecated)
            if (_rotateToVelocity && velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(velocity);

            // Ignore the shooter's solid colliders (self-hit; trigger self-hits
            // are additionally caught by the root check in HandleHit).
            if (shooter != null)
            {
                var mine = GetComponentsInChildren<Collider>();
                foreach (var sc in shooter.GetComponentsInChildren<Collider>())
                {
                    if (sc == null) continue;
                    foreach (var mc in mine)
                        if (mc != null) Physics.IgnoreCollision(mc, sc, true);
                }
            }

            Destroy(gameObject, _lifespan);
        }

        private void FixedUpdate()
        {
            _rb.AddForce(Physics.gravity * _gravityScale, ForceMode.Acceleration);
            if (_rotateToVelocity && _rb.linearVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_rb.linearVelocity);
        }

        private void OnCollisionEnter(Collision collision) => HandleHit(collision.collider);
        private void OnTriggerEnter(Collider other) => HandleHit(other);

        private void HandleHit(Collider other)
        {
            if (_consumed) return;
            // Self-hit ignore (covers trigger colliders that Physics.IgnoreCollision can't).
            if (_shooter != null && other.transform.root == _shooter.transform.root) return;

            _consumed = true;

            var targetable = other.GetComponentInParent<Targetable>();
            var stats = targetable != null
                ? targetable.GetComponent<CharacterStatsRuntime>()
                : other.GetComponentInParent<CharacterStatsRuntime>();

            if (stats != null && !stats.IsDead)
            {
                stats.ApplyDamage(_damage);
                if (targetable != null)
                {
                    Vector3 hitPoint = other.ClosestPoint(transform.position);
                    targetable.RaiseHit(hitPoint, _damage);
                }
            }

            Destroy(gameObject);
        }
    }
}
