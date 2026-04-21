#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelEditor
{
    /// <summary>
    /// Editor menu items for <see cref="EdgeSolver"/> smoke-testing and scene preview.
    /// </summary>
    public static class EdgeSolver_Test
    {
        private const string PreviewName = "EdgeSolver Gizmo Preview";

        /// <summary>
        /// Runs EdgeSolver on a Rectangle(5,3) and logs the result summary plus
        /// the first entry from each placement list. Expected: 15 floors, 16 walls,
        /// 4 corners, 0 warnings.
        /// </summary>
        [MenuItem("LevelEditor/Tests/Dump EdgeSolver Results")]
        private static void DumpEdgeSolverResults()
        {
            CellMap     map    = ShapeStamp.Rectangle(5, 3);
            SolveResult result = EdgeSolver.Solve(map);

            Debug.Log($"[EdgeSolver] {result}");

            for (int i = 0; i < result.warnings.Count; i++)
                Debug.LogWarning($"[EdgeSolver] Warning: {result.warnings[i]}");

            if (result.floors.Count > 0)
            {
                FloorPlacement f = result.floors[0];
                Debug.Log($"[EdgeSolver] First floor:  {f.worldPosition} at grid ({f.gridCoord.x},{f.gridCoord.y})");
            }

            if (result.walls.Count > 0)
            {
                WallPlacement w = result.walls[0];
                Debug.Log($"[EdgeSolver] First wall:   {w.worldPosition} at grid ({w.gridCoord.x},{w.gridCoord.y}) edge {w.edge}");
            }

            if (result.corners.Count > 0)
            {
                CornerPlacement c = result.corners[0];
                Debug.Log($"[EdgeSolver] First corner: {c.worldPosition} at grid ({c.gridCoord.x},{c.gridCoord.y})");
            }
        }

        /// <summary>
        /// Creates (or re-selects) an <see cref="EdgeSolverGizmoPreview"/> GameObject
        /// in the active scene at world origin. Select the object afterwards to see
        /// floor, wall, and corner gizmos in the Scene view.
        /// </summary>
        [MenuItem("LevelEditor/Tests/Create Gizmo Preview in Scene")]
        private static void CreateGizmoPreview()
        {
            // Re-select if it already exists so the user can just look at it.
            GameObject existing = GameObject.Find(PreviewName);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                Debug.Log($"[EdgeSolver] '{PreviewName}' already exists — selected it.");
                return;
            }

            var go = new GameObject(PreviewName);
            go.AddComponent<EdgeSolverGizmoPreview>();
            Selection.activeGameObject = go;
            Debug.Log($"[EdgeSolver] Created '{PreviewName}' at world origin. Select it to view gizmos.");
        }
    }
}
#endif
