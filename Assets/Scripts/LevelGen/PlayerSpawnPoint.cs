using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Marks the world position where the player should spawn when this room
    /// is the starter of a level. Exactly one per starter room. The transform
    /// IS the spawn pose — position + rotation come directly from it.
    ///
    /// Auto-placed by RoomBuilder.Build() when roomCategory == Starter.
    /// PlayerSpawner (or any future spawn-handling code) can locate this via:
    ///     UnityEngine.Object.FindAnyObjectByType&lt;PlayerSpawnPoint&gt;()?.transform
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        // Intentionally fieldless. The transform encodes position + rotation.

#if UNITY_EDITOR
        // Gizmo: green capsule + facing arrow at this transform's pose.
        // Color matches the convention used by SpawnPoint.cs (room-presence
        // markers): a saturated, distinguishable green that reads against
        // both light and dark floors.

        private static readonly Color GizmoColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);

        private void OnDrawGizmos()
        {
            DrawGizmo(filled: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(filled: true);
        }

        private void DrawGizmo(bool filled)
        {
            // Capsule approximation: top sphere, bottom sphere, connecting line.
            // Player rig matches CharacterController dims (height=1.8, radius=0.3).
            const float capHeight = 1.8f;
            const float capRadius = 0.3f;

            Vector3 basePos   = transform.position;
            Vector3 topSphere = basePos + transform.up * (capHeight - capRadius);
            Vector3 botSphere = basePos + transform.up * capRadius;

            Gizmos.color = GizmoColor;

            if (filled)
            {
                Gizmos.DrawSphere(topSphere, capRadius);
                Gizmos.DrawSphere(botSphere, capRadius);
            }
            else
            {
                Gizmos.DrawWireSphere(topSphere, capRadius);
                Gizmos.DrawWireSphere(botSphere, capRadius);
            }

            // Connecting line down the capsule axis
            Gizmos.DrawLine(topSphere, botSphere);

            // Facing arrow: from base, forward 0.8m, with two short fletching lines
            Vector3 arrowStart = basePos + transform.up * 1.0f;
            Vector3 arrowTip   = arrowStart + transform.forward * 0.8f;
            Gizmos.DrawLine(arrowStart, arrowTip);

            // Arrowhead
            Vector3 left  = arrowTip + Quaternion.Euler(0f, 150f, 0f) * (transform.forward * 0.2f);
            Vector3 right = arrowTip + Quaternion.Euler(0f, -150f, 0f) * (transform.forward * 0.2f);
            Gizmos.DrawLine(arrowTip, left);
            Gizmos.DrawLine(arrowTip, right);

#if UNITY_EDITOR
            UnityEditor.Handles.color = GizmoColor;
            UnityEditor.Handles.Label(basePos + transform.up * 2.2f, "Player Spawn");
#endif
        }
#endif
    }
}
