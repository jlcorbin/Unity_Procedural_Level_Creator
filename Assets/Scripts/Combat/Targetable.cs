using System;
using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Marker + hit-event publisher. Anything attackable carries this.
    /// Damage application lives elsewhere (CharacterStatsRuntime); this
    /// script's only job is identity, AimPoint resolution, and hit
    /// notification.
    /// </summary>
    /// <remarks>
    /// <see cref="AimPoint"/> defaults to this transform but can be
    /// overridden by adding a child named exactly "AimPoint" (case-
    /// sensitive). Future targeting systems aim at this transform rather
    /// than the GameObject's pivot.
    ///
    /// <see cref="OnHit"/> is raised by the damage-application site
    /// (PlayerCombat) after damage has been applied. Subscribers should
    /// treat the event as a notification only — they receive the hit
    /// point as payload but should not reach back into Targetable for
    /// state.
    /// </remarks>
    [DisallowMultipleComponent]
    public class Targetable : MonoBehaviour
    {
        /// <summary>
        /// Raised when this target is hit. Payload is the world-space
        /// point on the target's collider that received the hit (used
        /// downstream for VFX, knockback direction, floating numbers).
        /// </summary>
        public event Action<Vector3> OnHit;

        public Transform AimPoint { get; private set; }

        void Awake()
        {
            var child = transform.Find("AimPoint");
            AimPoint = child != null ? child : transform;
        }

        /// <summary>
        /// Called by the damage-application site (PlayerCombat or future
        /// equivalent) AFTER damage has been applied. Pure pass-through
        /// to subscribers — Targetable holds no hit state.
        /// </summary>
        public void RaiseHit(Vector3 hitPoint)
        {
            OnHit?.Invoke(hitPoint);
        }
    }
}
