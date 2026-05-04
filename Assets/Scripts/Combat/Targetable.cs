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
    /// point + damage as payload but should not reach back into Targetable
    /// for state.
    ///
    /// <see cref="AnyTargetableHit"/> is a static fan-out that fires
    /// alongside the instance event. Scene-wide subscribers (e.g. the
    /// floating combat-text spawner) subscribe once and receive every
    /// hit on every Targetable, including ones spawned at runtime.
    /// </remarks>
    [DisallowMultipleComponent]
    public class Targetable : MonoBehaviour
    {
        /// <summary>
        /// Raised when this target is hit. Payload is (hit point in
        /// world space, damage value applied). Used downstream for
        /// VFX, knockback direction, hit-reaction triggers.
        /// </summary>
        public event Action<Vector3, float> OnHit;

        /// <summary>
        /// Static fan-out for scene-wide subscribers (combat-text
        /// spawners, kill counters, etc.). Fires every time any
        /// Targetable.RaiseHit is called. Subscribers MUST unsubscribe
        /// in OnDisable / OnDestroy to avoid leaks across scene reloads
        /// (static event lifetime survives domain reloads).
        /// </summary>
        public static event Action<Vector3, float> AnyTargetableHit;

        public Transform AimPoint { get; private set; }

        void Awake()
        {
            var child = transform.Find("AimPoint");
            AimPoint = child != null ? child : transform;
        }

        /// <summary>
        /// Called by the damage-application site (PlayerCombat or future
        /// equivalent) AFTER damage has been applied. Pure pass-through
        /// to subscribers — Targetable holds no hit state. Fires both
        /// the instance event and the static fan-out.
        /// </summary>
        public void RaiseHit(Vector3 hitPoint, float damage)
        {
            OnHit?.Invoke(hitPoint, damage);
            AnyTargetableHit?.Invoke(hitPoint, damage);
        }
    }
}
