// EnemyAnimationEventForwarder.cs — REPLACES the M10 EnemyAnimationEventAbsorber.
//
// Sits on the Animator's GameObject (the MaleCharacterPBR child) and
// forwards Attack-clip AnimationEvents to EnemyCombat on the parent.
// Symmetric to AnimationEventForwarder.cs (Player). M10 had a no-op
// stub here just to suppress "no receiver" warnings; M11 makes it
// route the events to real damage logic.
//
// Why this exists: Unity dispatches AnimationEvents via SendMessage to
// the Animator's own GameObject — it does NOT walk the hierarchy. Our
// Animator lives on the MaleCharacterPBR child, but EnemyCombat lives
// on the Dummy prefab root.

using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Forwards weapon-hitbox AnimationEvents from the Animator's GameObject
    /// up to <see cref="EnemyCombat"/> on the prefab root. Method names
    /// must match the AnimationEvent function names exactly
    /// (<c>OnHitboxOpen</c>, <c>OnHitboxClose</c>) — same names the player
    /// rig uses, since the Attack01 clip is shared.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAnimationEventForwarder : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("EnemyCombat on the prefab root. Auto-resolved on Reset.")]
        private EnemyCombat _combat;

        public EnemyCombat Combat => _combat;

        private void Reset()
        {
            _combat = GetComponentInParent<EnemyCombat>();
        }

        public void OnHitboxOpen()
        {
            if (_combat != null) _combat.OnHitboxOpen();
        }

        public void OnHitboxClose()
        {
            if (_combat != null) _combat.OnHitboxClose();
        }
    }
}
