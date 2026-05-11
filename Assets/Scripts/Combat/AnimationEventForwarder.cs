// AnimationEventForwarder.cs — sits on the Animator's GameObject and
// forwards Attack-clip AnimationEvents to PlayerCombat on the parent.
//
// Why this exists: Unity dispatches AnimationEvents via SendMessage to
// the Animator's own GameObject — it does NOT walk the hierarchy. Our
// Animator lives on the MaleCharacterPBR child, but PlayerCombat lives
// on the Player_Hero root. Without this forwarder, AnimationEvents
// log "no receiver! Are you missing a component?" for every fire.

using UnityEngine;
using LevelGen.Player;

namespace LevelGen.Combat
{
    /// <summary>
    /// Forwards weapon-hitbox AnimationEvents from the Animator's GameObject
    /// up to <see cref="PlayerCombat"/> on the prefab root. Method names
    /// must match the AnimationEvent function names exactly
    /// (<c>OnHitboxOpen</c>, <c>OnHitboxClose</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimationEventForwarder : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("PlayerCombat on the prefab root. Auto-resolved on Reset.")]
        private PlayerCombat combat;

        public PlayerCombat Combat => combat;

        private void Reset()
        {
            combat = GetComponentInParent<PlayerCombat>();
        }

        public void OnHitboxOpen()
        {
            if (combat != null) combat.OnHitboxOpen();
        }

        public void OnHitboxClose()
        {
            if (combat != null) combat.OnHitboxClose();
        }
    }
}
