using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LevelEditor
{
    /// <summary>
    /// Describes where a wall prefab's pivot sits along the wall's local X axis.
    /// Used by RoomBuilder to shift the pivot so the mesh lands centered on the edge midpoint.
    /// </summary>
    public enum WallPivotPosition
    {
        /// <summary>Pivot at the geometric center of the wall length. No shift applied.</summary>
        Center,
        /// <summary>Pivot at the -X end of the wall; mesh extends +X from pivot. Shift applied: +X * CellSize/2.</summary>
        StartX,
        /// <summary>Pivot at the +X end of the wall; mesh extends -X from pivot. Shift applied: -X * CellSize/2.</summary>
        EndX,
    }

    /// <summary>
    /// Describes where a floor prefab's pivot sits relative to its tile footprint.
    /// Used by RoomBuilder to shift the pivot so the mesh lands centered on the cell.
    /// </summary>
    public enum FloorPivotPosition
    {
        /// <summary>Pivot at the center of the tile footprint. No shift applied.</summary>
        Center,
        /// <summary>Pivot at the NW corner; mesh extends +X and -Z. Shift applied: (+X, 0, -Z) * CellSize/2.</summary>
        PivotNW,
        /// <summary>Pivot at the NE corner; mesh extends -X and -Z. Shift applied: (-X, 0, -Z) * CellSize/2.</summary>
        PivotNE,
        /// <summary>Pivot at the SW corner; mesh extends +X and +Z. Shift applied: (+X, 0, +Z) * CellSize/2.</summary>
        PivotSW,
        /// <summary>Pivot at the SE corner; mesh extends -X and +Z. Shift applied: (-X, 0, +Z) * CellSize/2.</summary>
        PivotSE,
    }

    /// <summary>
    /// Turns a <see cref="SolveResult"/> from <see cref="EdgeSolver"/> into actual
    /// scene geometry under a single root GameObject.
    ///
    /// <para><b>Current scope (Phase 3 / Pass 1):</b>
    /// <list type="bullet">
    /// <item>Rectangle shape only.</item>
    /// <item>Tier 0 cells only.</item>
    /// <item>One inspector-assigned prefab per category (floor, wall, corner) — no
    ///       catalogue-based variant selection.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Deferred work:</b>
    /// <list type="bullet">
    /// <item>Catalogue-based prefab selection (per-tile-type prefab pools), replacing halfWall slots.</item>
    /// <item>Per-tile-type variant selection (triangle floors, angle/concave/convex walls).</item>
    /// <item>Tier stacking (tiers 1 and 2).</item>
    /// <item>RoomPiece bounds stamping.</item>
    /// <item>ExitPoint placement (door workflow).</item>
    /// <item>Inward (concave) corners for L-shapes and notches.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class RoomBuilder : MonoBehaviour
    {
        // ── Shape ────────────────────────────────────────────────────────────

        [Header("Shape")]
        [SerializeField, Tooltip("Room width in cells (min 1).")]
        private int rectangleWidth = 5;

        [SerializeField, Tooltip("Room depth in cells (min 1).")]
        private int rectangleDepth = 3;

        // ── Prefabs ───────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [SerializeField, Tooltip("Prefab used for every floor cell.")]
        private GameObject floorPrefab;

        [SerializeField, Tooltip("Prefab used for every straight wall segment.")]
        private GameObject wallPrefab;

        [SerializeField, Tooltip("Prefab used for every outward corner.")]
        private GameObject cornerPrefab;

        [SerializeField, Tooltip("Half-wall prefab with mesh extending to the LEFT of its pivot (_L variant). " +
            "Used when a Half corner sits at the wall's right (local +X) end.")]
        private GameObject halfWallLPrefab;

        [SerializeField, Tooltip("Half-wall prefab with mesh extending to the RIGHT of its pivot (_R variant). " +
            "Used when a Half corner sits at the wall's left (local -X) end.")]
        private GameObject halfWallRPrefab;

        [SerializeField, Tooltip("Where the assigned wall prefab's pivot sits along its length. " +
            "Use Center for _M_ center-pivoted walls. Use StartX or EndX if the mesh is offset " +
            "from the pivot along the wall's local X axis.")]
        private WallPivotPosition wallPivot = WallPivotPosition.StartX;

        [SerializeField, Tooltip("Where the assigned floor prefab's pivot sits relative to its tile. " +
            "Use Center for center-pivoted floors. Use one of the four corner options if the mesh is " +
            "offset from the pivot.")]
        private FloorPivotPosition floorPivot = FloorPivotPosition.PivotNW;

        [SerializeField, Tooltip("How long the corner prefab's arms are. " +
            "Full: arms are 4 units, corner replaces the 2 adjacent walls. " +
            "Half: arms are 2 units, adjacent walls are replaced with L/R half-wall variants. " +
            "Column: corner is decorative, both adjacent walls remain full.")]
        private CornerArmLength cornerArmLength = CornerArmLength.Full;

        // ── Output ────────────────────────────────────────────────────────────

        [Header("Output")]
        [SerializeField, Tooltip("Name of the root GameObject created by Build. Overwritten on each Build.")]
        private string rootName = "MOD_Room";

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Destroys any existing room root by name, then solves a Rectangle cell-map
        /// and instantiates floor, wall, and corner prefabs under a new root at world
        /// origin. Logs all solver warnings and a completion summary.
        ///
        /// <para>Returns immediately and logs an error if any of the three core prefabs are
        /// unassigned — no partial build occurs.</para>
        /// </summary>
        public void Build()
        {
            // Guard: floor, wall, and corner prefabs must all be assigned.
            bool floorMissing  = floorPrefab  == null;
            bool wallMissing   = wallPrefab   == null;
            bool cornerMissing = cornerPrefab == null;

            if (floorMissing || wallMissing || cornerMissing)
            {
                string missing = string.Join(", ",
                    floorMissing  ? "floorPrefab"  : null,
                    wallMissing   ? "wallPrefab"   : null,
                    cornerMissing ? "cornerPrefab" : null);
                Debug.LogError($"[RoomBuilder] Build aborted — missing prefabs: {missing}");
                return;
            }

            // Warn once if Half mode is selected but one or both half-wall slots are empty.
            if (cornerArmLength == CornerArmLength.Half)
            {
                bool lMissing = halfWallLPrefab == null;
                bool rMissing = halfWallRPrefab == null;
                if (lMissing || rMissing)
                {
                    string missing = string.Join(", ",
                        lMissing ? "halfWallLPrefab" : null,
                        rMissing ? "halfWallRPrefab" : null);
                    Debug.LogWarning($"[RoomBuilder] Half mode: {missing} not assigned — those half-walls will be skipped.");
                }
            }

            DestroyRoot();

            CellMap     map    = ShapeStamp.Rectangle(rectangleWidth, rectangleDepth);
            SolveResult result = EdgeSolver.Solve(map, cornerArmLength);

            for (int i = 0; i < result.warnings.Count; i++)
                Debug.LogWarning($"[RoomBuilder] Solver warning: {result.warnings[i]}");

            // Root at world origin — position/rotation are explicit so the builder's
            // own transform has no effect on room placement.
            var root = new GameObject(rootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(root, "Build Room");
#endif

            var floorsGroup  = new GameObject("Floors").transform;
            var wallsGroup   = new GameObject("Walls").transform;
            var cornersGroup = new GameObject("Corners").transform;

            floorsGroup.SetParent(root.transform,  false);
            wallsGroup.SetParent(root.transform,   false);
            cornersGroup.SetParent(root.transform, false);

            Vector3 floorShift = FloorPivotShift(floorPivot);

            // ── Floors ────────────────────────────────────────────────────────
            // Floor shift is applied in world space — Square floors are always at
            // identity rotation at tier 0, so rotating the shift is unnecessary.
            for (int i = 0; i < result.floors.Count; i++)
            {
                FloorPlacement floor = result.floors[i];
                GameObject     go    = InstantiatePiece(floorPrefab, floorsGroup);
                go.name                    = $"Floor_{floor.gridCoord.x}_{floor.gridCoord.y}";
                go.transform.localPosition = floor.worldPosition + floorShift;
                go.transform.localRotation = floor.rotation;
            }

            // ── Walls ─────────────────────────────────────────────────────────
            // Wall pivot shift is along the wall's own local X, so it is rotated by the
            // wall's rotation before being added to the world-space position.
            // HalfL uses a hardcoded EndX-equivalent shift (mirror authoring vs. HalfR).
            for (int i = 0; i < result.walls.Count; i++)
            {
                WallPlacement wall = result.walls[i];

                GameObject prefab;
                switch (wall.kind)
                {
                    case WallKind.Straight: prefab = wallPrefab;      break;
                    case WallKind.HalfL:    prefab = halfWallLPrefab; break;
                    case WallKind.HalfR:    prefab = halfWallRPrefab; break;
                    default:                continue; // Angle/Concave/Convex deferred
                }

                if (prefab == null) continue;

                Vector3    wallShiftLocal = WallPivotShift(wallPivot, wall.kind);
                GameObject go             = InstantiatePiece(prefab, wallsGroup);
                go.name = wall.kind == WallKind.Straight
                    ? $"Wall_{wall.gridCoord.x}_{wall.gridCoord.y}_{wall.edge}"
                    : $"Wall_{wall.gridCoord.x}_{wall.gridCoord.y}_{wall.edge}_{wall.kind}";
                go.transform.localPosition = wall.worldPosition + (wall.rotation * wallShiftLocal);
                go.transform.localRotation = wall.rotation;
            }

            // ── Corners ───────────────────────────────────────────────────────
            for (int i = 0; i < result.corners.Count; i++)
            {
                CornerPlacement corner = result.corners[i];
                GameObject      go     = InstantiatePiece(cornerPrefab, cornersGroup);
                go.name                    = $"Corner_{corner.gridCoord.x}_{corner.gridCoord.y}";
                go.transform.localPosition = corner.worldPosition;
                go.transform.localRotation = corner.rotation;
            }

            Debug.Log($"[RoomBuilder] Built {result.floors.Count} floors, " +
                      $"{result.walls.Count} walls, {result.corners.Count} corners " +
                      $"under '{rootName}'.");
        }

        /// <summary>
        /// Finds and destroys the room root GameObject by name. Does nothing if not found.
        /// </summary>
        public void Clear()
        {
            int count = DestroyRoot();
            if (count == 0)
                Debug.Log($"[RoomBuilder] Nothing to clear — '{rootName}' not found.");
        }

        // ─────────────────────────────────────────────────────────────────────

        // Finds and destroys the existing root, returning how many objects were removed.
        private int DestroyRoot()
        {
            GameObject existing = GameObject.Find(rootName);
            if (existing == null) return 0;

#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(existing);
#else
            if (Application.isPlaying)
                Destroy(existing);
            else
                DestroyImmediate(existing);
#endif
            Debug.Log($"[RoomBuilder] Removed existing '{rootName}'.");
            return 1;
        }

        // Instantiates a prefab under the given parent. Uses PrefabUtility in the editor
        // so the hierarchy retains its prefab link; plain Instantiate at runtime.
        private GameObject InstantiatePiece(GameObject prefab, Transform parent)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Undo.RegisterCreatedObjectUndo(go, "Build Room");
                return go;
            }
#endif
            return Instantiate(prefab, parent);
        }

        private static Vector3 WallPivotShift(WallPivotPosition pivot, WallKind kind)
        {
            float half = CellMap.CellSize * 0.5f; // 2f

            // HalfL prefabs have mirror pivot authoring vs. HalfR and straight walls:
            // pivot sits at the +X end with mesh extending -X. They always need
            // the EndX-equivalent -2 shift regardless of the user's wallPivot setting.
            if (kind == WallKind.HalfL)
                return new Vector3(-half, 0f, 0f);

            switch (pivot)
            {
                case WallPivotPosition.StartX: return new Vector3( half, 0f, 0f);
                case WallPivotPosition.EndX:   return new Vector3(-half, 0f, 0f);
                default:                       return Vector3.zero; // Center
            }
        }

        private static Vector3 FloorPivotShift(FloorPivotPosition pivot)
        {
            float half = CellMap.CellSize * 0.5f; // 2f
            switch (pivot)
            {
                case FloorPivotPosition.PivotNW: return new Vector3( half, 0f, -half);
                case FloorPivotPosition.PivotNE: return new Vector3(-half, 0f, -half);
                case FloorPivotPosition.PivotSW: return new Vector3( half, 0f,  half);
                case FloorPivotPosition.PivotSE: return new Vector3(-half, 0f,  half);
                default:                         return Vector3.zero; // Center
            }
        }
    }
}
