using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Marks a position inside a dressed room where the content generator may place a prop.
    /// Color-coded gizmos make spawn points easy to identify in the Scene view.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────────────

        [Tooltip("Gameplay role of the prop that should go here. " +
                 "PropCatalogue.GetRandom() is filtered by this value.")]
        public PropEntry.SpawnType spawnType = PropEntry.SpawnType.Decoration;

        [Tooltip("Physical size category of the prop expected here. " +
                 "Matches PropEntry.spawnSize for size-appropriate placement.")]
        public PropEntry.SpawnSize spawnSize = PropEntry.SpawnSize.Small;

        /// <summary>
        /// Set by RoomContentGenerator after a prop has been placed here.
        /// Prevents a second pass from double-filling the same point.
        /// </summary>
        [HideInInspector] public bool isOccupied = false;

        // ── Editor visualisation ──────────────────────────────────────────────
#if UNITY_EDITOR
        // Gizmo colors per spawn type:
        // Decoration = blue, Enemy = red, Trap = orange, Chest = yellow
        private static readonly Color[] TypeColors =
        {
            new Color(0.2f, 0.5f, 1.0f, 0.9f), // Decoration — blue
            new Color(1.0f, 0.2f, 0.2f, 0.9f), // Enemy      — red
            new Color(1.0f, 0.55f, 0.1f, 0.9f),// Trap       — orange
            new Color(1.0f, 0.9f, 0.1f, 0.9f), // Chest      — yellow
        };

        private void OnDrawGizmos()
        {
            Gizmos.color = isOccupied
                ? new Color(0.3f, 0.3f, 0.3f, 0.5f)
                : TypeColors[(int)spawnType];

            Gizmos.DrawWireSphere(transform.position, 0.22f);
            Gizmos.DrawRay(transform.position, transform.up * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = TypeColors[(int)spawnType];
            Gizmos.DrawSphere(transform.position, 0.12f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.65f,
                $"{spawnType} ({spawnSize}){(isOccupied ? " [filled]" : "")}");
        }
#endif
    }
}
