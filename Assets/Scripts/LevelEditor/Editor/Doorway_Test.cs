#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Editor menu items that smoke-test doorway suppression — both the manual
    /// authoring path (phase 1) and the RoomBuilder doorCount path (phase 2).
    /// </summary>
    public static class Doorway_Test
    {
        // ── Combined: both paths, no interference ─────────────────────────────

        /// <summary>
        /// Proves manual AddDoorway and doorCount-equivalent stamps are additive (union).
        /// Uses manual stamp on East + doorCount-equivalent North/South = 3 total gaps.
        /// Also verifies stamping the same edge twice is idempotent (still 1 gap).
        /// </summary>
        [MenuItem("LevelEditor/Tests/Doorway: Combined paths on 5x3")]
        private static void RunCombinedTest()
        {
            int w = 5, d = 3;

            CellMap     baseMap    = ShapeStamp.Rectangle(w, d);
            SolveResult baseResult = EdgeSolver.Solve(baseMap);

            // Union of manual East door + doorCount-equivalent N + S = 3 gaps.
            CellMap combinedMap = ShapeStamp.Rectangle(w, d);
            combinedMap.AddDoorway(w - 1, d / 2, CellEdge.East); // manual East
            combinedMap.AddDoorway(w / 2, d - 1, CellEdge.North); // doorCount N
            combinedMap.AddDoorway(w / 2, 0,     CellEdge.South); // doorCount S
            SolveResult combinedResult = EdgeSolver.Solve(combinedMap);

            // Idempotency: adding the same East doorway again should not open a second gap.
            combinedMap.AddDoorway(w - 1, d / 2, CellEdge.East);
            SolveResult idempotentResult = EdgeSolver.Solve(combinedMap);

            int diff3 = baseResult.walls.Count - combinedResult.walls.Count;
            int diffSame = combinedResult.walls.Count - idempotentResult.walls.Count;

            Debug.Log(
                $"[Doorway_Test] [Combined] Baseline  : {baseResult}\n" +
                $"[Doorway_Test] [Combined] 3 Doorways: {combinedResult}\n" +
                $"[Doorway_Test] [Combined] Wall diff  : {diff3} (expected 3)\n" +
                $"[Doorway_Test] [Combined] Duplicate stamp diff: {diffSame} (expected 0)");

            Log(diff3 == 3 && diffSame == 0, "Combined paths");
        }

        private static void Log(bool pass, string label)
        {
            if (pass)
                Debug.Log($"[Doorway_Test] PASS — {label}.");
            else
                Debug.LogWarning($"[Doorway_Test] FAIL — {label}. Check doorway logic.");
        }
    }
}
#endif
