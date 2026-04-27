using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LevelEditor
{
    /// <summary>
    /// MonoBehaviour that visualises <see cref="EdgeSolver"/> output as Gizmos in
    /// the Scene view. Select this GameObject to see floor, wall, and corner
    /// placements for a rectangle of the configured size.
    ///
    /// <para>All drawing is offset by <see cref="Transform.position"/> so the
    /// object can be freely moved around the scene.</para>
    ///
    /// <para>Add this component via
    /// <b>LevelEditor → Tests → Create Gizmo Preview in Scene</b>.</para>
    /// </summary>
    public class EdgeSolverGizmoPreview : MonoBehaviour
    {
        /// <summary>One doorway mark to apply to the preview cell map before solving.</summary>
        [Serializable]
        public struct DoorwayEntry
        {
            [Tooltip("Grid X coordinate of the cell whose edge is a doorway.")]
            public int x;
            [Tooltip("Grid Z coordinate of the cell whose edge is a doorway.")]
            public int z;
            [Tooltip("Which edge of the cell is open (no wall emitted).")]
            public CellEdge edge;
        }

        [SerializeField, Tooltip("Width of the test rectangle in cells (ShapeStamp.Rectangle first argument).")]
        private int rectangleWidth = 5;

        [SerializeField, Tooltip("Depth of the test rectangle in cells (ShapeStamp.Rectangle second argument).")]
        private int rectangleDepth = 3;

        [SerializeField, Tooltip("Cell edges to mark as doorways before solving. Marked edges suppress wall emission and are drawn in magenta.")]
        public List<DoorwayEntry> doorways = new List<DoorwayEntry>();

        [SerializeField, Tooltip("Draw semi-transparent blue cubes for each floor placement.")]
        private bool drawFloors = true;

        [SerializeField, Tooltip("Draw yellow wire-box walls with a green +Z arrow showing the inward direction.")]
        private bool drawWalls = true;

        [SerializeField, Tooltip("Draw red wire-pillar corners with an orange +Z bisector arrow.")]
        private bool drawCorners = true;

        [SerializeField, Tooltip("Draw magenta lines along each marked doorway edge.")]
        private bool drawDoorways = true;

        [SerializeField, Tooltip("Draw grid-coord labels above each placement (editor-only, uses Handles.Label).")]
        private bool drawLabels = true;

        [SerializeField, Tooltip("Length of the directional arrow drawn from each wall and corner centre.")]
        private float arrowLength = 1.5f;

        private void OnDrawGizmosSelected()
        {
            // Always draw a white sphere at the anchor so it's obvious where the
            // solver-space origin sits in world space.
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            CellMap map = ShapeStamp.Rectangle(rectangleWidth, rectangleDepth);

            for (int i = 0; i < doorways.Count; i++)
                map.AddDoorway(doorways[i].x, doorways[i].z, doorways[i].edge);

            SolveResult result = EdgeSolver.Solve(map);

            DrawFloors(result);
            DrawWalls(result);
            DrawCorners(result);
            DrawDoorways(map);
        }

        // ------------------------------------------------------------------ floors

        private void DrawFloors(SolveResult result)
        {
            if (!drawFloors) return;

            Gizmos.color = new Color(0.3f, 0.6f, 1.0f, 0.35f);
            var size = new Vector3(CellMap.CellSize * 0.9f, 0.1f, CellMap.CellSize * 0.9f);

            for (int i = 0; i < result.floors.Count; i++)
            {
                FloorPlacement floor = result.floors[i];
                Vector3        pos   = ToWorld(floor.worldPosition);

                Gizmos.DrawCube(pos, size);

#if UNITY_EDITOR
                if (drawLabels)
                    Handles.Label(pos + Vector3.up * 0.2f,
                        $"({floor.gridCoord.x},{floor.gridCoord.y})");
#endif
            }
        }

        // ------------------------------------------------------------------ walls

        private void DrawWalls(SolveResult result)
        {
            if (!drawWalls) return;

            var boxSize = new Vector3(CellMap.CellSize * 0.9f, CellMap.TierHeight, 0.2f);

            for (int i = 0; i < result.walls.Count; i++)
            {
                WallPlacement wall    = result.walls[i];
                Vector3       worldPos = ToWorld(wall.worldPosition);

                // Rotated wire box representing the wall face.
                Gizmos.color  = Color.yellow;
                Gizmos.matrix = Matrix4x4.TRS(worldPos, wall.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
                Gizmos.matrix = Matrix4x4.identity;

                // Green arrow: +Z points INTO the room interior.
                Gizmos.color = Color.green;
                Vector3 arrowTip = worldPos + (wall.rotation * Vector3.forward) * arrowLength;
                Gizmos.DrawLine(worldPos, arrowTip);
                Gizmos.DrawSphere(arrowTip, 0.15f);

#if UNITY_EDITOR
                if (drawLabels)
                    Handles.Label(
                        worldPos + Vector3.up * (CellMap.TierHeight + 0.3f),
                        $"{wall.edge} ({wall.gridCoord.x},{wall.gridCoord.y})");
#endif
            }
        }

        // ------------------------------------------------------------------ corners

        private void DrawCorners(SolveResult result)
        {
            if (!drawCorners) return;

            var boxSize = new Vector3(0.4f, CellMap.TierHeight, 0.4f);

            for (int i = 0; i < result.corners.Count; i++)
            {
                CornerPlacement corner   = result.corners[i];
                Vector3         worldPos = ToWorld(corner.worldPosition);

                // Rotated wire pillar at the corner vertex.
                Gizmos.color  = Color.red;
                Gizmos.matrix = Matrix4x4.TRS(worldPos, corner.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
                Gizmos.matrix = Matrix4x4.identity;

                // Orange arrow: +Z bisects the two wall faces, pointing into the room.
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Vector3 arrowTip = worldPos + (corner.rotation * Vector3.forward) * arrowLength;
                Gizmos.DrawLine(worldPos, arrowTip);
                Gizmos.DrawSphere(arrowTip, 0.2f);

#if UNITY_EDITOR
                if (drawLabels)
                    Handles.Label(
                        worldPos + Vector3.up * (CellMap.TierHeight + 0.3f),
                        $"corner ({corner.gridCoord.x},{corner.gridCoord.y})");
#endif
            }
        }

        // ------------------------------------------------------------------ doorways

        private void DrawDoorways(CellMap map)
        {
            if (!drawDoorways) return;

            float half = CellMap.CellSize * 0.5f;

            Gizmos.color = Color.magenta;
            foreach (var (x, z, edge) in map.AllDoorways())
            {
                Vector3 center = ToWorld(map.CellCenterWorld(x, z));

                Vector3 a, b;
                switch (edge)
                {
                    case CellEdge.North:
                        a = center + new Vector3(-half, 0f,  half);
                        b = center + new Vector3( half, 0f,  half);
                        break;
                    case CellEdge.South:
                        a = center + new Vector3(-half, 0f, -half);
                        b = center + new Vector3( half, 0f, -half);
                        break;
                    case CellEdge.East:
                        a = center + new Vector3(half, 0f, -half);
                        b = center + new Vector3(half, 0f,  half);
                        break;
                    default: // West
                        a = center + new Vector3(-half, 0f, -half);
                        b = center + new Vector3(-half, 0f,  half);
                        break;
                }

                Gizmos.DrawLine(a, b);
                Gizmos.DrawSphere((a + b) * 0.5f, 0.15f);
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Converts a solver-local position to world space by offsetting by
        /// <see cref="Transform.position"/>. All gizmo drawing must go through this
        /// so the preview can be repositioned freely in the scene.
        /// </summary>
        private Vector3 ToWorld(Vector3 localPos) => transform.position + localPos;
    }
}
