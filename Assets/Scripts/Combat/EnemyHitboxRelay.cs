// EnemyHitboxRelay.cs — bridge from a child trigger collider to EnemyCombat.
//
// Symmetric to HitboxRelay.cs (Player). Lives on the same GameObject as
// the trigger collider (the EnemyWeaponHitbox child), forwards
// OnTriggerEnter to the parent prefab root's EnemyCombat. No state.

using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Forwards trigger-enter events from an enemy weapon hitbox up to
    /// the prefab root's <see cref="EnemyCombat"/>. The relay exists
    /// because OnTriggerEnter must be on the same GameObject as the
    /// collider, but the combat state lives on the prefab root.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHitboxRelay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("EnemyCombat on the prefab root. Auto-resolved on Reset.")]
        private EnemyCombat _combat;

        public EnemyCombat Combat => _combat;

        private void Reset()
        {
            _combat = GetComponentInParent<EnemyCombat>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_combat == null) return;
            _combat.NotifyHitboxTriggered(other);
        }
    }
}
