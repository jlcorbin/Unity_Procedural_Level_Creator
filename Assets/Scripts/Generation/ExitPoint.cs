using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Placed as a child object on every RoomPiece prefab.
    /// Marks a connection socket — position and direction define
    /// where and how another piece attaches.
    /// </summary>
    public class ExitPoint : MonoBehaviour
    {
        // ── Exit direction ────────────────────────────────────────────
        public enum Direction { North, South, East, West, Up, Down }

        /// <summary>
        /// Which direction this exit faces outward from the room.
        /// The connecting piece must have an exit facing the opposite direction.
        /// </summary>
        [Tooltip("Direction this exit faces outward from the room.")]
        public Direction exitDirection = Direction.North;

        /// <summary>
        /// True once another piece has been connected to this exit.
        /// Prevents the generator from trying to fill it again.
        /// </summary>
        [HideInInspector] public bool isConnected = false;

        /// <summary>
        /// True if the generator gave up trying to fill this exit
        /// (e.g. all placements overlapped). A dead-end cap should be placed here.
        /// </summary>
        [HideInInspector] public bool isSealed = false;

        /// <summary>
        /// Reference to the piece that was connected through this exit.
        /// Null until connected.
        /// </summary>
        [HideInInspector] public RoomPiece connectedPiece = null;

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the direction that an incoming piece must face to connect here.
        /// e.g. a North-facing exit connects to a South-facing exit on the next piece.
        /// </summary>
        public Direction OppositeDirection()
        {
            return exitDirection switch
            {
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                Direction.East  => Direction.West,
                Direction.West  => Direction.East,
                Direction.Up    => Direction.Down,
                Direction.Down  => Direction.Up,
                _               => Direction.South
            };
        }

        /// <summary>
        /// Converts this exit's direction enum to a world-space Vector3.
        /// Used by the generator to calculate placement offsets.
        /// </summary>
        public Vector3 DirectionVector()
        {
            return exitDirection switch
            {
                Direction.North => Vector3.forward,
                Direction.South => Vector3.back,
                Direction.East  => Vector3.right,
                Direction.West  => Vector3.left,
                Direction.Up    => Vector3.up,
                Direction.Down  => Vector3.down,
                _               => Vector3.forward
            };
        }

        // ── Editor visualisation ──────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Color-code by state
            if (isConnected)
                Gizmos.color = Color.green;
            else if (isSealed)
                Gizmos.color = Color.red;
            else
                Gizmos.color = Color.yellow;

            // Draw a small sphere at the socket position
            Gizmos.DrawSphere(transform.position, 0.2f);

            // Draw an arrow showing which direction this exit faces
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, DirectionVector() * 1.0f);
        }
#endif
    }
}
