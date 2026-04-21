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
        // ── Phase 1: manual AddDoorway ────────────────────────────────────────

        [MenuItem("LevelEditor/Tests/Doorway: Manual 5x3 with 2 Doorways")]
        private static void RunManualDoorwayTest()
        {
            CellMap     baseMap    = ShapeStamp.Rectangle(5, 3);
            SolveResult baseResult = EdgeSolver.Solve(baseMap);

            // Manually stamp south edge of cell (2,0) and north edge of cell (2,2).
            CellMap doorMap = ShapeStamp.Rectangle(5, 3);
            doorMap.AddDoorway(2, 0, CellEdge.South);
            doorMap.AddDoorway(2, 2, CellEdge.North);
            SolveResult doorResult = EdgeSolver.Solve(doorMap);

            int diff = baseResult.walls.Count - doorResult.walls.Count;

            Debug.Log(
                $"[Doorway_Test] [Manual] Baseline   : {baseResult}\n" +
                $"[Doorway_Test] [Manual] +2 Doorways: {doorResult}\n" +
                $"[Doorway_Test] [Manual] Wall diff   : {diff} (expected 2)");

            Log(diff == 2, "Manual stamp");
        }

        // ── Phase 2: doorCount equivalent ────────────────────────────────────

        /// <summary>
        /// Replicates what RoomBuilder.ApplyAutoDoorways does for doorCount=2 on a
        /// 5×3 map: North at (2,2) and South at (2,0). Proves the math matches the
        /// manual authoring path.
        /// </summary>
        [MenuItem("LevelEditor/Tests/Doorway: doorCount=2 equivalent on 5x3")]
        private static void RunDoorCountEquivalentTest()
        {
            int w = 5, d = 3;

            CellMap     baseMap    = ShapeStamp.Rectangle(w, d);
            SolveResult baseResult = EdgeSolver.Solve(baseMap);

            // doorCount=2 → order[0]=North, order[1]=South (mirrors ApplyAutoDoorways).
            CellMap doorMap = ShapeStamp.Rectangle(w, d);
            doorMap.AddDoorway(w / 2,     d - 1, CellEdge.North); // (2, 2)
            doorMap.AddDoorway(w / 2,     0,     CellEdge.South); // (2, 0)
            SolveResult doorResult = EdgeSolver.Solve(doorMap);

            int diff = baseResult.walls.Count - doorResult.walls.Count;

            Debug.Log(
                $"[Doorway_Test] [doorCount=2] Baseline  : {baseResult}\n" +
                $"[Doorway_Test] [doorCount=2] +2 Doorways: {doorResult}\n" +
                $"[Doorway_Test] [doorCount=2] Wall diff  : {diff} (expected 2)");

            Log(diff == 2, "doorCount=2 equivalent");
        }

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
