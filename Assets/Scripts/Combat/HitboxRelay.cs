// HitboxRelay.cs — bridge from a child trigger collider to PlayerCombat.
//
// Lives on the same GameObject as the trigger collider. As of M20b this
// is the weapon WorldPrefab itself (instantiated under weapon_r by
// PlayerEquipmentVisuals) rather than a static WeaponHitbox child of
// Player_Hero. The combat reference is null in the prefab asset and is
// wired at runtime by PlayerEquipmentVisuals after instantiation.
// Forwards OnTriggerEnter to PlayerCombat. No state, no Update — pure relay.

using UnityEngine;
using LevelGen.Player;

namespace LevelGen.Combat
{
    /// <summary>
    /// Forwards trigger-enter events from a weapon hitbox collider up to
    /// the prefab root's <see cref="PlayerCombat"/>. The relay exists
    /// because OnTriggerEnter has to be on the same GameObject as the
    /// collider, but the combat state lives on the prefab root.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitboxRelay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("PlayerCombat on the prefab root. Auto-resolved on Reset.")]
        private PlayerCombat combat;

        /// <summary>
        /// The <see cref="PlayerCombat"/> that receives trigger events from this
        /// relay. Auto-resolved on <c>Reset</c> via <c>GetComponentInParent</c>.
        /// Can also be set at runtime by <c>PlayerEquipmentVisuals</c> when a
        /// weapon WorldPrefab is instantiated at runtime (M20b).
        /// </summary>
        public PlayerCombat Combat
        {
            get => combat;
            set => combat = value;
        }

        private void Reset()
        {
            combat = GetComponentInParent<PlayerCombat>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (combat == null) return;
            combat.NotifyHitboxTriggered(other);
        }
    }
}
