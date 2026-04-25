#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen;

namespace LevelEditor
{
    /// <summary>
    /// Smoke-tests for RoomBuilder's MOD_Room architecture:
    /// RoomPiece and ExitPoints must live on the MOD_Room child, not on the RoomBuilder itself.
    /// </summary>
    public static class RoomPiece_Test
    {
        [MenuItem("LevelEditor/Tests/RoomPiece: 5x3 with 3 Doorways")]
        private static void RunRoomPieceTest()
        {
            // ── Setup ────────────────────────────────────────────────────────
            var go      = new GameObject("_RoomPieceTest_Temp");
            var builder = go.AddComponent<RoomBuilder>();

            int w = 5, d = 3;
            CellMap map = ShapeStamp.Rectangle(w, d);
            map.AddDoorway(w / 2, d - 1, CellEdge.North); // (2,2) North
            map.AddDoorway(w / 2, 0,     CellEdge.South); // (2,0) South
            map.AddDoorway(w - 1, d / 2, CellEdge.East);  // (4,1) East

            builder.PopulateRoomPiece(map);

            // ── Check 0 — RoomPiece NOT on the RoomBuilder itself ────────────
            var builderPiece = go.GetComponent<RoomPiece>();
            LogCheck(builderPiece == null, "RoomPiece is NOT on the RoomBuilder GameObject");

            // ── Check 1 — MOD_Room child exists ─────────────────────────────
            Transform modRoomTf = go.transform.Find("MOD_Room");
            LogCheck(modRoomTf != null, "MOD_Room child exists under RoomBuilder");

            if (modRoomTf != null)
            {
                // ── Check 2 — RoomPiece on MOD_Room ─────────────────────────
                var piece = modRoomTf.GetComponent<RoomPiece>();
                LogCheck(piece != null, "RoomPiece component exists on MOD_Room");

                if (piece != null)
                {
                    // ── Check 3 — Bounds size ────────────────────────────────
                    float expectedW = w * CellMap.CellSize;
                    float expectedD = d * CellMap.CellSize;
                    float expectedH = (map.GetMaxTierUsed() + 1) * CellMap.TierHeight;

                    bool boundsOk =
                        Mathf.Approximately(piece.boundsSize.x * 2f, expectedW) &&
                        Mathf.Approximately(piece.boundsSize.y * 2f, expectedH) &&
                        Mathf.Approximately(piece.boundsSize.z * 2f, expectedD);

                    LogCheck(boundsOk,
                        $"boundsSize*2 = ({piece.boundsSize.x * 2f}, {piece.boundsSize.y * 2f}, {piece.boundsSize.z * 2f})" +
                        $"  expected ({expectedW}, {expectedH}, {expectedD})");

                    // ── Check 4 — ExitPoint count on MOD_Room ────────────────
                    int epCount = 0;
                    foreach (Transform child in modRoomTf)
                        if (child.name.StartsWith("V2_ExitPoint_") &&
                            child.GetComponent<ExitPoint>() != null)
                            epCount++;

                    LogCheck(epCount == 3,
                        $"ExitPoint child count on MOD_Room = {epCount}  expected 3");

                    // ── Check 5 — No ExitPoints directly on RoomBuilder ──────
                    int builderEpCount = 0;
                    foreach (Transform child in go.transform)
                        if (child.name.StartsWith("V2_ExitPoint_"))
                            builderEpCount++;

                    LogCheck(builderEpCount == 0,
                        $"No V2_ExitPoint_* directly on RoomBuilder (found {builderEpCount})");

                    // ── Check 6 — Forward vectors ────────────────────────────
                    CheckExitForward(modRoomTf.gameObject, "V2_ExitPoint_North", Vector3.forward);
                    CheckExitForward(modRoomTf.gameObject, "V2_ExitPoint_South", Vector3.back);
                    CheckExitForward(modRoomTf.gameObject, "V2_ExitPoint_East",  Vector3.right);
                }
            }

            // ── Cleanup ──────────────────────────────────────────────────────
            Object.DestroyImmediate(go);
        }

        [MenuItem("LevelEditor/Tests/Save: MOD_Room Save+Clear Roundtrip")]
        private static void RunSaveRoundtripTest()
        {
            const string testFolder = "Assets/Prefabs/Rooms/Small";
            const string testPath   = "Assets/Prefabs/Rooms/Small/__test_save_roundtrip.prefab";

            // ── Setup ────────────────────────────────────────────────────────
            var go      = new GameObject("_SaveRoundtripTest_Temp");
            var builder = go.AddComponent<RoomBuilder>();

            int w = 5, d = 3;
            CellMap map = ShapeStamp.Rectangle(w, d);
            map.AddDoorway(w / 2, 0, CellEdge.South); // 1 doorway

            builder.PopulateRoomPiece(map);

            Transform modRoomTf = go.transform.Find("MOD_Room");
            LogCheck(modRoomTf != null, "MOD_Room child exists before save");
            if (modRoomTf == null) { Object.DestroyImmediate(go); return; }

            // ── Save ─────────────────────────────────────────────────────────
            int childCountBefore = modRoomTf.childCount;
            LogCheck(childCountBefore > 0,
                $"MOD_Room has {childCountBefore} children before save (expected > 0)");

            builder.EnsureFolderExists(testFolder);
            builder.SaveRoomRootAsPrefab(testPath, "__test_save_roundtrip");
            AssetDatabase.Refresh();

            // ── Verify saved prefab ───────────────────────────────────────────
            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(testPath);
            LogCheck(savedPrefab != null, $"Prefab loaded from {testPath}");

            if (savedPrefab != null)
            {
                var prefabPiece = savedPrefab.GetComponent<RoomPiece>();
                LogCheck(prefabPiece != null, "Saved prefab root has a RoomPiece component");

                LogCheck(savedPrefab.transform.childCount == childCountBefore,
                    $"Saved prefab child count = {savedPrefab.transform.childCount}  expected {childCountBefore}");
            }

            // ── Delete test asset ─────────────────────────────────────────────
            AssetDatabase.DeleteAsset(testPath);
            AssetDatabase.Refresh();

            // ── Clear ─────────────────────────────────────────────────────────
            builder.Clear();

            modRoomTf = go.transform.Find("MOD_Room");
            LogCheck(modRoomTf != null, "MOD_Room still exists after Clear (empty child)");

            if (modRoomTf != null)
            {
                LogCheck(modRoomTf.childCount == 0,
                    $"MOD_Room has 0 children after Clear (found {modRoomTf.childCount})");

                var pieceAfterClear = modRoomTf.GetComponent<RoomPiece>();
                LogCheck(pieceAfterClear == null, "MOD_Room has no RoomPiece after Clear");
            }

            // ── Cleanup ───────────────────────────────────────────────────────
            Object.DestroyImmediate(go);
            Debug.Log("[RoomPiece_Test] Save+Clear roundtrip done.");
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
            if (pass) Debug.Log($"[RoomPiece_Test] PASS — {detail}");
            else       Debug.LogWarning($"[RoomPiece_Test] FAIL — {detail}");
        }
    }
}
#endif
