using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Attach to every room and hall prefab.
    /// Describes what kind of piece this is, its physical bounds,
    /// and the list of exit sockets on it.
    /// </summary>
    public class RoomPiece : MonoBehaviour
    {
        // ── Piece type ────────────────────────────────────────────────
        public enum PieceType { StartRoom, Room, Hall, DeadEnd }

        [Tooltip("What kind of piece this is. Affects generator weighting.")]
        public PieceType pieceType = PieceType.Room;

        // ── Bounds ────────────────────────────────────────────────────
        [Tooltip("Half-extents of this piece's bounding box. " +
                 "Used for overlap detection before placement.")]
        public Vector3 boundsSize = new Vector3(5f, 3f, 5f);

        [Tooltip("Local offset of the bounds center from this object's pivot.")]
        public Vector3 boundsOffset = Vector3.zero;

        // ── Exits ─────────────────────────────────────────────────────
        /// <summary>
        /// All ExitPoint components that are children of this piece.
        /// Populated automatically on Awake — no manual assignment needed.
        /// </summary>
        [HideInInspector] public List<ExitPoint> exits = new List<ExitPoint>();

        // ── Runtime state ─────────────────────────────────────────────
        /// <summary>
        /// Set by the generator once this piece is placed in the world.
        /// </summary>
        [HideInInspector] public bool isPlaced = false;

        /// <summary>
        /// Depth in the generation tree. Start room = 0.
        /// Useful for difficulty scaling per depth.
        /// </summary>
        [HideInInspector] public int generationDepth = 0;

        // ── Unity messages ────────────────────────────────────────────
        private void Awake()
        {
            RefreshExits();
        }

        /// <summary>
        /// Finds and caches all ExitPoint children.
        /// Safe to call multiple times — clears and rebuilds the list.
        /// </summary>
        public void RefreshExits()
        {
            exits.Clear();
            exits.AddRange(GetComponentsInChildren<ExitPoint>());
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns world-space bounds for this piece at its current position.
        /// </summary>
        public Bounds GetWorldBounds()
        {
            return new Bounds(transform.position + boundsOffset, boundsSize * 2f);
        }

        /// <summary>
        /// Returns all exits that are not yet connected or sealed.
        /// These are the ones the generator can still build from.
        /// </summary>
        public List<ExitPoint> GetOpenExits()
        {
            var open = new List<ExitPoint>();
            foreach (var exit in exits)
            {
                if (!exit.isConnected && !exit.isSealed)
                    open.Add(exit);
            }
            return open;
        }

        /// <summary>
        /// Returns the first exit facing the given direction, or null if none found.
        /// Used when connecting a new piece to match socket directions.
        /// </summary>
        public ExitPoint GetExitFacing(ExitPoint.Direction direction)
        {
            foreach (var exit in exits)
            {
                if (exit.exitDirection == direction && !exit.isConnected && !exit.isSealed)
                    return exit;
            }
            return null;
        }

        // ── Editor visualisation ──────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Show the bounding box so you can size it correctly in editor
            Gizmos.color = isPlaced
                ? new Color(0f, 1f, 0f, 0.15f)
                : new Color(1f, 1f, 0f, 0.15f);

            Gizmos.DrawCube(transform.position + boundsOffset, boundsSize * 2f);

            Gizmos.color = isPlaced
                ? new Color(0f, 1f, 0f, 0.6f)
                : new Color(1f, 1f, 0f, 0.6f);

            Gizmos.DrawWireCube(transform.position + boundsOffset, boundsSize * 2f);
        }
#endif
    }
}
