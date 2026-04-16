using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Static utility for checking whether a proposed piece placement
    /// would overlap any already-placed pieces.
    ///
    /// Two strategies available:
    ///   1. AABB check against a tracked list of placed pieces (fast, no physics)
    ///   2. Physics.OverlapBox check (accurate, requires colliders on pieces)
    ///
    /// The generator uses strategy 1 by default. Switch to strategy 2
    /// if your pieces have irregular shapes that need collider-accurate checks.
    /// </summary>
    public static class BoundsChecker
    {
        // ── Strategy 1: AABB list check ───────────────────────────────

        /// <summary>
        /// Checks whether a proposed bounds overlaps any bounds in the
        /// already-placed list. Uses a small shrink factor so pieces that
        /// share a wall edge (exactly touching) are not counted as overlapping.
        /// </summary>
        /// <param name="proposed">World-space bounds of the piece to place.</param>
        /// <param name="placedPieces">All pieces already in the scene.</param>
        /// <param name="overlapTolerance">
        ///     How much to shrink the proposed bounds before testing.
        ///     Prevents false positives from floating-point edge contact.
        ///     Default 0.1 units.
        /// </param>
        public static bool Overlaps(
            Bounds proposed,
            List<RoomPiece> placedPieces,
            float overlapTolerance = 0.1f)
        {
            // Shrink slightly so touching walls don't count as overlapping
            Bounds shrunk = proposed;
            shrunk.Expand(-overlapTolerance);

            foreach (var piece in placedPieces)
            {
                if (piece == null) continue;

                Bounds existing = piece.GetWorldBounds();
                if (shrunk.Intersects(existing))
                    return true;
            }
            return false;
        }

        // ── Strategy 2: Physics overlap check ────────────────────────

        /// <summary>
        /// Uses Physics.OverlapBox to detect any colliders inside the
        /// proposed placement area. More accurate for non-rectangular pieces.
        ///
        /// Requires pieces to have colliders on a dedicated layer.
        /// </summary>
        /// <param name="center">World-space center of the proposed placement.</param>
        /// <param name="halfExtents">Half-size of the proposed piece.</param>
        /// <param name="rotation">Rotation of the proposed piece.</param>
        /// <param name="layerMask">Layer mask for the room/hall geometry layer.</param>
        public static bool OverlapsPhysics(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rotation,
            LayerMask layerMask)
        {
            // Shrink slightly for the same tolerance reason
            Vector3 shrunk = halfExtents - Vector3.one * 0.1f;
            shrunk = Vector3.Max(shrunk, Vector3.one * 0.01f); // clamp to positive

            Collider[] hits = Physics.OverlapBox(center, shrunk, rotation, layerMask);
            return hits.Length > 0;
        }

        // ── Placement math ────────────────────────────────────────────

        /// <summary>
        /// Calculates the world position a new piece should be placed at
        /// so that its entry exit aligns exactly with the source exit.
        ///
        /// How it works:
        ///   - The source exit has a world position.
        ///   - The new piece has an entry exit at a local offset from its pivot.
        ///   - We want: newPiece.pivot + entryLocalOffset == sourceExit.worldPos
        ///   - So: newPiece.pivot = sourceExit.worldPos - entryLocalOffset
        /// </summary>
        /// <param name="sourceExit">The open exit on the already-placed piece.</param>
        /// <param name="newPiecePrefab">The prefab being placed.</param>
        /// <param name="entryExit">The exit on the new piece that connects to sourceExit.</param>
        public static Vector3 CalculatePlacementPosition(
            ExitPoint sourceExit,
            RoomPiece newPiecePrefab,
            ExitPoint entryExit)
        {
            // World position of the source socket
            Vector3 sourcePos = sourceExit.transform.position;

            // Local offset from the new piece's pivot to its entry exit
            Vector3 entryLocalOffset = entryExit.transform.localPosition;

            // Place the new piece so its entry exit lands on the source exit
            return sourcePos - entryLocalOffset;
        }

        /// <summary>
        /// Calculates the rotation a new piece needs so its entry exit
        /// faces directly into the source exit (opposite directions).
        ///
        /// For simple axis-aligned pieces this is usually a 180-degree Y rotation.
        /// </summary>
        /// <param name="sourceExit">The open exit on the already-placed piece.</param>
        /// <param name="entryExit">The exit on the new piece that connects to sourceExit.</param>
        public static Quaternion CalculatePlacementRotation(
            ExitPoint sourceExit,
            ExitPoint entryExit)
        {
            // The new piece needs to rotate so entryExit faces opposite to sourceExit
            Vector3 sourceForward = sourceExit.DirectionVector();
            Vector3 entryForward  = entryExit.DirectionVector();

            // Rotation that takes entryForward → -sourceForward (facing inward)
            if (entryForward == -sourceForward)
                return Quaternion.identity; // already aligned

            return Quaternion.FromToRotation(entryForward, -sourceForward);
        }
    }
}
