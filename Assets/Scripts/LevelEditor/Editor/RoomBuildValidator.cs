using LevelGen;
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Read-only validator for RoomBuilder output. Run via:
    ///   LevelGen ▶ Validate ▶ Validate Room Build
    ///
    /// Select either the RoomBuilder GameObject or the MOD_Room subtree it
    /// produced, then invoke the menu item. The validator inspects the
    /// Collision group's children and (when the room is a Starter) the
    /// PlayerSpawn marker, logging PASS / FAIL per check.
    /// </summary>
    public static class RoomBuildValidator
    {
        private const float WallHeight     = 6f;
        private const float FloorThickness = 0.1f;
        private const float CornerSize     = 0.5f;

        [MenuItem("LevelGen/Validate/Validate Room Build")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;
            int info = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[RoomBuildValidator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[RoomBuildValidator] FAIL — {label}: {detail}"); }
            }

            void InfoLog(string label, string detail)
            {
                info++;
                Debug.Log($"[RoomBuildValidator] INFO — {label}: {detail}");
            }

            // ── Resolve the MOD_Room subject ─────────────────────────────────
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                Debug.LogError("[RoomBuildValidator] FAIL — no GameObject selected. Select a MOD_Room or its parent (RoomBuilder) and re-run.");
                return;
            }

            GameObject  modRoom = null;
            RoomBuilder builder = sel.GetComponent<RoomBuilder>();

            if (builder != null)
            {
                var t = builder.transform.Find("MOD_Room");
                if (t == null)
                {
                    Debug.LogError("[RoomBuildValidator] FAIL — selected RoomBuilder has no MOD_Room child. Build first.");
                    return;
                }
                modRoom = t.gameObject;
            }
            else if (sel.name == "MOD_Room" || sel.GetComponent<RoomPiece>() != null)
            {
                modRoom = sel;
                builder = sel.GetComponentInParent<RoomBuilder>();
            }
            else
            {
                Debug.LogError($"[RoomBuildValidator] FAIL — selected '{sel.name}' is neither a RoomBuilder nor a MOD_Room.");
                return;
            }

            Debug.Log($"[RoomBuildValidator] Validating '{modRoom.name}'.");

            // ── Collision group ──────────────────────────────────────────────
            var coll  = modRoom.transform.Find("Collision");
            var floor = coll != null ? coll.Find("Floor_Collider") : null;

            Check("Collision group exists under MOD_Room",
                coll != null,
                coll != null ? "found" : "missing");

            if (coll != null)
            {
                // Floor collider — exactly one, non-trigger, top ≈ Y=0.
                int floorCount = 0;
                for (int i = 0; i < coll.childCount; i++)
                    if (coll.GetChild(i).name == "Floor_Collider")
                        floorCount++;
                Check("Exactly one Floor_Collider", floorCount == 1, $"count = {floorCount}");

                if (floor != null)
                {
                    var fbox = floor.GetComponent<BoxCollider>();
                    Check("Floor_Collider has non-trigger BoxCollider",
                        fbox != null && !fbox.isTrigger,
                        fbox == null ? "no BoxCollider component"
                                     : (fbox.isTrigger ? "isTrigger=true" : "ok"));

                    if (fbox != null)
                    {
                        var b = fbox.bounds;
                        Check("Floor top surface ≈ Y=0",
                            Mathf.Abs(b.max.y - 0f) < 0.01f,
                            $"max.y = {b.max.y:F4}");
                        Check($"Floor bottom surface ≈ Y=-{FloorThickness}",
                            Mathf.Abs(b.min.y - (-FloorThickness)) < 0.01f,
                            $"min.y = {b.min.y:F4}");
                    }
                }

                // Wall colliders — count per edge, cross-reference against doorCount.
                // Expected total = 4 + doorCount (each doorway splits a run, +1).
                int wallN = 0, wallS = 0, wallE = 0, wallW = 0;
                int wallBad = 0;
                for (int i = 0; i < coll.childCount; i++)
                {
                    var child = coll.GetChild(i);
                    if (!child.name.StartsWith("Wall_Collider_")) continue;

                    if      (child.name.StartsWith("Wall_Collider_N_")) wallN++;
                    else if (child.name.StartsWith("Wall_Collider_S_")) wallS++;
                    else if (child.name.StartsWith("Wall_Collider_E_")) wallE++;
                    else if (child.name.StartsWith("Wall_Collider_W_")) wallW++;

                    var box = child.GetComponent<BoxCollider>();
                    if (box == null || box.isTrigger || !Mathf.Approximately(box.size.y, WallHeight))
                        wallBad++;
                }
                int totalWalls = wallN + wallS + wallE + wallW;

                if (builder != null)
                {
                    int doorCount = Mathf.Clamp(builder.doorCount, 0, 4);
                    int expected  = 4 + doorCount;
                    Check($"Wall collider count == 4 + doorCount ({expected})",
                        totalWalls == expected,
                        $"actual = {totalWalls} (N={wallN} S={wallS} E={wallE} W={wallW}, doorCount={doorCount})");
                }
                else
                {
                    InfoLog("Wall count cross-check",
                        "no RoomBuilder ancestor — falling back to per-edge ≥1 check");
                    Check("Each edge has ≥1 wall collider",
                        wallN >= 1 && wallS >= 1 && wallE >= 1 && wallW >= 1,
                        $"N={wallN} S={wallS} E={wallE} W={wallW}");
                }
                Check($"All wall colliders are non-trigger BoxCollider with size.y == {WallHeight}",
                    wallBad == 0,
                    $"bad = {wallBad} of {totalWalls}");

                // Corner colliders — exactly 4 (NW/NE/SW/SE), correct sizing.
                string[] cornerNames = { "Corner_Collider_NW", "Corner_Collider_NE", "Corner_Collider_SW", "Corner_Collider_SE" };
                int cornersFound = 0;
                int cornerBad    = 0;
                foreach (var name in cornerNames)
                {
                    var ct = coll.Find(name);
                    if (ct == null) continue;
                    cornersFound++;
                    var box = ct.GetComponent<BoxCollider>();
                    if (box == null || box.isTrigger)
                    {
                        cornerBad++;
                        continue;
                    }
                    if (!Mathf.Approximately(box.size.x, CornerSize) ||
                        !Mathf.Approximately(box.size.y, WallHeight) ||
                        !Mathf.Approximately(box.size.z, CornerSize))
                        cornerBad++;
                }
                Check("Exactly 4 corner colliders (NW/NE/SW/SE)",
                    cornersFound == 4,
                    $"found = {cornersFound}");
                Check($"All corners non-trigger BoxCollider with size ({CornerSize}, {WallHeight}, {CornerSize})",
                    cornerBad == 0,
                    $"bad = {cornerBad} of {cornersFound}");

                // No visual leakage into the Collision group.
                var meshFilters   = coll.GetComponentsInChildren<MeshFilter>(true);
                var meshRenderers = coll.GetComponentsInChildren<MeshRenderer>(true);
                int leakCount = meshFilters.Length + meshRenderers.Length;
                Check("No MeshRenderer / MeshFilter under Collision group",
                    leakCount == 0,
                    leakCount == 0
                        ? "clean"
                        : $"{meshFilters.Length} MeshFilter(s) + {meshRenderers.Length} MeshRenderer(s)");

                // Floor-alignment regression check (catches Bug 1 — collision
                // anchored at NW corner instead of centered extents). Computes
                // the union of all visible Floor_* MeshRenderer bounds under
                // 'Floors/' and compares to Floor_Collider's bounds. 0.5-unit
                // tolerance is coarse enough for any reasonable floor pivot.
                var floorsGroup = modRoom.transform.Find("Floors");
                var fboxAlign   = floor != null ? floor.GetComponent<BoxCollider>() : null;
                if (floorsGroup != null && fboxAlign != null)
                {
                    var floorMeshes = floorsGroup.GetComponentsInChildren<MeshRenderer>();
                    if (floorMeshes.Length == 0)
                    {
                        InfoLog("Floor alignment", "no MeshRenderers under Floors/ — skipping alignment check");
                    }
                    else
                    {
                        Bounds visBounds = floorMeshes[0].bounds;
                        for (int i = 1; i < floorMeshes.Length; i++)
                            visBounds.Encapsulate(floorMeshes[i].bounds);

                        Bounds colBounds = fboxAlign.bounds;
                        float dCenterX = Mathf.Abs(colBounds.center.x - visBounds.center.x);
                        float dCenterZ = Mathf.Abs(colBounds.center.z - visBounds.center.z);
                        float dExtX    = Mathf.Abs(colBounds.extents.x - visBounds.extents.x);
                        float dExtZ    = Mathf.Abs(colBounds.extents.z - visBounds.extents.z);

                        bool aligned = dCenterX < 0.5f && dCenterZ < 0.5f
                                    && dExtX    < 0.5f && dExtZ    < 0.5f;
                        Check("Floor_Collider aligned with visible Floors footprint (within 0.5)",
                            aligned,
                            aligned
                                ? $"visible center {visBounds.center} ext {visBounds.extents}; collider matches within tol."
                                : $"OFFSET — visible center {visBounds.center} ext {visBounds.extents}, " +
                                  $"collider center {colBounds.center} ext {colBounds.extents}. " +
                                  "This indicates a coordinate origin bug — see RoomBuilder collision constants and centered-extent math.");
                    }
                }
            }

            // ── PlayerSpawn marker ───────────────────────────────────────────
            //
            // Only validate spawn checks when we can determine whether the
            // room is a Starter. Resolution order:
            //   1. RoomBuilder ancestor: pieceType + roomCategory.
            //   2. RoomPiece on MOD_Room: pieceType + categoryName == "Starter".
            //   3. Neither known → INFO and skip.
            bool? isStarter = null;
            string starterSource = null;

            if (builder != null)
            {
                isStarter = builder.pieceType == PieceType.Room
                         && builder.roomCategory == RoomCategory.Starter;
                starterSource = "RoomBuilder";
            }
            else
            {
                var piece = modRoom.GetComponent<RoomPiece>();
                if (piece != null && !string.IsNullOrEmpty(piece.categoryName))
                {
                    isStarter = piece.pieceType == RoomPiece.PieceType.Room
                             && piece.categoryName == "Starter";
                    starterSource = "RoomPiece.categoryName";
                }
            }

            var spawnT = modRoom.transform.Find("PlayerSpawn");

            if (isStarter == true)
            {
                Debug.Log($"[RoomBuildValidator] (room is Starter — verified via {starterSource})");

                Check("PlayerSpawn child exists under MOD_Room",
                    spawnT != null,
                    spawnT != null ? "found" : "missing");

                if (spawnT != null)
                {
                    Check("PlayerSpawn has a PlayerSpawnPoint component",
                        spawnT.GetComponent<PlayerSpawnPoint>() != null,
                        spawnT.GetComponent<PlayerSpawnPoint>() != null ? "found" : "missing");

                    // Expected center: Floor_Collider's localPosition.xz with Y=0.
                    if (floor != null)
                    {
                        Vector3 expected = new Vector3(floor.localPosition.x, 0f, floor.localPosition.z);
                        Vector3 actual   = spawnT.localPosition;
                        bool centerOk = Mathf.Abs(actual.x - expected.x) < 0.01f
                                     && Mathf.Abs(actual.y - expected.y) < 0.01f
                                     && Mathf.Abs(actual.z - expected.z) < 0.01f;
                        Check("PlayerSpawn localPosition at room center",
                            centerOk,
                            $"expected {expected}, actual {actual}");
                    }
                    else
                    {
                        InfoLog("PlayerSpawn position",
                            "Floor_Collider missing — cannot derive expected center; skipping XZ check.");
                    }

                    Quaternion id  = Quaternion.identity;
                    Quaternion act = spawnT.localRotation;
                    bool rotOk = Mathf.Abs(act.x - id.x) < 0.01f
                              && Mathf.Abs(act.y - id.y) < 0.01f
                              && Mathf.Abs(act.z - id.z) < 0.01f
                              && Mathf.Abs(act.w - id.w) < 0.01f;
                    Check("PlayerSpawn localRotation == identity",
                        rotOk,
                        $"actual = {act}");
                }
            }
            else if (isStarter == false)
            {
                Debug.Log($"[RoomBuildValidator] (room is NOT Starter — verified via {starterSource})");
                Check("No PlayerSpawn child under non-Starter room",
                    spawnT == null,
                    spawnT == null ? "absent (correct)" : $"found unexpectedly: '{spawnT.name}'");
            }
            else
            {
                InfoLog("Spawn checks",
                    "could not determine room category (no RoomBuilder ancestor, RoomPiece.categoryName empty). Skipping spawn checks.");
            }

            Debug.Log($"[RoomBuildValidator] SUMMARY — {pass} PASS / {fail} FAIL / {info} INFO.");
        }
    }
}
