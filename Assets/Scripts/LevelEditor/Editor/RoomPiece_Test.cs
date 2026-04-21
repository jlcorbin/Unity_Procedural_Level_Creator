#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen;

namespace LevelEditor
{
    /// <summary>
    /// Smoke-test that verifies RoomPiece bounds and ExitPoint children are
    /// correctly stamped by RoomBuilder.PopulateRoomPiece for a 5×3 room
    /// with doorCount = 3 (N + S + E).
    /// </summary>
    public static class RoomPiece_Test
    {
        [MenuItem("LevelEditor/Tests/RoomPiece: 5x3 with 3 Doorways")]
        private static void RunRoomPieceTest()
        {
            // ── Setup ────────────────────────────────────────────────────────
            // Create a temporary RoomBuilder GameObject for the test.
            var go      = new GameObject("_RoomPieceTest_Temp");
            var builder = go.AddComponent<RoomBuilder>();

            // Build the map that doorCount=3 on 5×3 would produce:
            //   North at (2, 2), South at (2, 0), East at (4, 1).
            int w = 5, d = 3;
            CellMap map = ShapeStamp.Rectangle(w, d);
            map.AddDoorway(w / 2,     d - 1, CellEdge.North); // (2, 2) North
            map.AddDoorway(w / 2,     0,     CellEdge.South); // (2, 0) South
            map.AddDoorway(w - 1,     d / 2, CellEdge.East);  // (4, 1) East

            builder.PopulateRoomPiece(map);

            // ── Check 1 — RoomPiece exists ───────────────────────────────────
            var piece = go.GetComponent<RoomPiece>();
            LogCheck(piece != null, "RoomPiece component exists on RoomBuilder GameObject");

            if (piece != null)
            {
                // ── Check 2 — Bounds size ────────────────────────────────────
                // boundsSize holds half-extents; full size = boundsSize * 2.
                float expectedW = w * CellMap.CellSize;          // 20
                float expectedD = d * CellMap.CellSize;          // 12
                float expectedH = CellMap.MaxTiers * CellMap.TierHeight; // 18

                bool boundsOk =
                    Mathf.Approximately(piece.boundsSize.x * 2f, expectedW) &&
                    Mathf.Approximately(piece.boundsSize.y * 2f, expectedH) &&
                    Mathf.Approximately(piece.boundsSize.z * 2f, expectedD);

                LogCheck(boundsOk,
                    $"boundsSize*2 = ({piece.boundsSize.x * 2f}, {piece.boundsSize.y * 2f}, {piece.boundsSize.z * 2f})" +
                    $"  expected ({expectedW}, {expectedH}, {expectedD})");

                // ── Check 3 — ExitPoint count ────────────────────────────────
                int epCount = 0;
                foreach (Transform child in go.transform)
                    if (child.name.StartsWith("V2_ExitPoint_") &&
                        child.GetComponent<ExitPoint>() != null)
                        epCount++;

                LogCheck(epCount == 3,
                    $"ExitPoint child count = {epCount}  expected 3");

                // ── Check 4 — Forward vectors ────────────────────────────────
                CheckExitForward(go, "V2_ExitPoint_North", Vector3.forward);
                CheckExitForward(go, "V2_ExitPoint_South", Vector3.back);
                CheckExitForward(go, "V2_ExitPoint_East",  Vector3.right);
            }

            // ── Cleanup ──────────────────────────────────────────────────────
            Object.DestroyImmediate(go);
        }

        private static void CheckExitForward(GameObject root, string namePrefix, Vector3 expectedForward)
        {
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith(namePrefix)) continue;
                bool ok = Vector3.Dot(child.forward, expectedForward) > 0.99f;
                LogCheck(ok,
                    $"{child.name} forward = {child.forward:F2}  expected {expectedForward}");
                return;
            }
            LogCheck(false, $"No child found with prefix '{namePrefix}'");
        }

        private static void LogCheck(bool pass, string detail)
        {
            if (pass)
                Debug.Log($"[RoomPiece_Test] PASS — {detail}");
            else
                Debug.LogWarning($"[RoomPiece_Test] FAIL — {detail}");
        }
    }
}
#endif
