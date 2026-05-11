// HitboxRelay.cs — bridge from a child trigger collider to PlayerCombat.
//
// Lives on the same GameObject as the trigger collider (the WeaponHitbox
// child of Player_Hero). Forwards OnTriggerEnter to the parent
// prefab root's PlayerCombat. No state, no Update — pure relay.

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

        public PlayerCombat Combat => combat;

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
