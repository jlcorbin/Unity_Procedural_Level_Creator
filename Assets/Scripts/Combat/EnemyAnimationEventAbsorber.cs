// EnemyAnimationEventAbsorber.cs — M10 no-op stub.
//
// Sits on the Animator's GameObject (the MaleCharacterPBR child).
// Absorbs OnHitboxOpen / OnHitboxClose AnimationEvents that fire
// from Attack01_SwordAndShiled (and any future shared-attack
// clips). Without a receiver on the Animator's GameObject, Unity
// spams "AnimationEvent has no receiver" warnings every swing
// (M4-A lesson — Unity dispatches AnimationEvents to the
// Animator's GameObject only, no parent walk).
//
// M11 will REPLACE this component with EnemyCombat, which will
// consume these events to fire an enemy weapon hitbox and apply
// player damage. Until then this component exists only to
// suppress warnings — its bodies are intentionally empty.

using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// No-op receiver for OnHitboxOpen / OnHitboxClose
    /// AnimationEvents on enemy attack clips. Replaced by
    /// EnemyCombat in M11. Method names MUST exactly match the
    /// player-side endpoint names (PlayerCombat.OnHitboxOpen /
    /// OnHitboxClose) since the same clips fire the same events
    /// on either rig.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyAnimationEventAbsorber : MonoBehaviour
    {
        // Public because Unity AnimationEvents only invoke public methods.
        public void OnHitboxOpen()  { /* M10: no-op. M11 will fire enemy weapon hitbox. */ }
        public void OnHitboxClose() { /* M10: no-op. */ }
    }
}
