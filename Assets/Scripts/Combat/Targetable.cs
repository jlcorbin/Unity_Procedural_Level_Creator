using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Marker component. Adding it to a GameObject opts that object in for
    /// future enemy AI / target-lock / damage-application systems. No
    /// behavior is shipped on it now beyond existence and AimPoint
    /// resolution — it's a discovery hook.
    /// </summary>
    /// <remarks>
    /// <see cref="AimPoint"/> defaults to this transform but can be
    /// overridden by adding a child named exactly "AimPoint" (case-
    /// sensitive). Future targeting systems aim at this transform rather
    /// than the GameObject's pivot, letting authors place a chest- or
    /// head-height target without making the override mandatory.
    /// </remarks>
    [DisallowMultipleComponent]
    public class Targetable : MonoBehaviour
    {
        public Transform AimPoint { get; private set; }

        void Awake()
        {
            var child = transform.Find("AimPoint");
            AimPoint = child != null ? child : transform;
        }
    }
}
