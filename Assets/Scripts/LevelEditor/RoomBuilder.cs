using System.Collections.Generic;
using LevelGen;
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
    /// <para><b>Current scope (Phase 3):</b>
    /// <list type="bullet">
    /// <item>Rectangle shape only.</item>
    /// <item>Square tile type only; all others are skipped.</item>
    /// <item>Tier 0 cells only.</item>
    /// <item>One inspector-assigned prefab per category (floor, wall, corner,
    ///       halfWallL, halfWallR) — no catalogue-based selection.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Deferred work:</b>
    /// <list type="bullet">
    /// <item>Custom shapes (Diamond, Circle) and triangle/angle-wall tile support.</item>
    /// <item>Catalogue-based prefab selection (per-tile-type prefab pools).</item>
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
        [SerializeField, Tooltip("Prefab used for every Square floor cell.")]
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

        // ── Doors ─────────────────────────────────────────────────────────────

        [Header("Doors")]
        [Range(0, 4)]
        [SerializeField, Tooltip("Auto-places doorway openings in order: N, S, E, W. Middle cell of each wall. 0 = solid room.")]
        public int doorCount = 0;

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
            // Guard: all three core prefabs required.
            if (floorPrefab == null || wallPrefab == null || cornerPrefab == null)
            {
                Debug.LogError("[RoomBuilder] Build aborted — floorPrefab, wallPrefab, and cornerPrefab are all required.");
                return;
            }

            // Guard: Half mode requires both half-wall prefabs.
            if (cornerArmLength == CornerArmLength.Half &&
                (halfWallLPrefab == null || halfWallRPrefab == null))
            {
                Debug.LogError("[RoomBuilder] Build aborted — halfWallLPrefab and halfWallRPrefab are required when cornerArmLength is Half.");
                return;
            }

            DestroyRoot();

            CellMap map = ShapeStamp.Rectangle(rectangleWidth, rectangleDepth);
            ApplyAutoDoorways(map);
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
            // Pivot shift applied in world space (Square floors at identity rotation, tier 0).
            for (int i = 0; i < result.floors.Count; i++)
            {
                FloorPlacement floor = result.floors[i];
                if (floor.tileType != TileType.Square) continue;

                GameObject go = InstantiatePiece(floorPrefab, floorsGroup);
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

            PopulateRoomPiece(map);
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

        // Direction order for auto-doorways. requiresWidth=true means N/S (x varies, width must be >=3);
        // false means E/W (z varies, depth must be >=3).
        private struct DoorSlot
        {
            public CellEdge edge;
            public bool     requiresWidth;
        }

        private static readonly DoorSlot[] s_DoorOrder = new DoorSlot[]
        {
            new DoorSlot { edge = CellEdge.North, requiresWidth = true  },
            new DoorSlot { edge = CellEdge.South, requiresWidth = true  },
            new DoorSlot { edge = CellEdge.East,  requiresWidth = false },
            new DoorSlot { edge = CellEdge.West,  requiresWidth = false },
        };

        private void ApplyAutoDoorways(CellMap map)
        {
            int w = map.Width;
            int d = map.Depth;

            for (int i = 0; i < Mathf.Min(doorCount, 4); i++)
            {
                DoorSlot slot = s_DoorOrder[i];
                CellEdge edge = slot.edge;

                if (slot.requiresWidth && w < 3)
                {
                    Debug.LogWarning($"[RoomBuilder] doorCount {i + 1}: room too small in {edge} direction (width={w} < 3), skipping door.");
                    continue;
                }
                if (!slot.requiresWidth && d < 3)
                {
                    Debug.LogWarning($"[RoomBuilder] doorCount {i + 1}: room too small in {edge} direction (depth={d} < 3), skipping door.");
                    continue;
                }

                int x = edge == CellEdge.East  ? w - 1 :
                        edge == CellEdge.West  ? 0      : w / 2;
                int z = edge == CellEdge.North ? d - 1 :
                        edge == CellEdge.South ? 0      : d / 2;

                map.AddDoorway(x, z, edge);
            }
        }

        private static Vector3 WallPivotShift(WallPivotPosition pivot, WallKind kind)
        {
            float half = CellMap.CellSize * 0.5f; // 2f

            // HalfL: mirror pivot authoring, always EndX-equivalent (-2 on local X).
            if (kind == WallKind.HalfL) return new Vector3(-half, 0f, 0f);

            // Straight/HalfR: use user's wallPivot setting.
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

        // ── RoomPiece + ExitPoint stamping ────────────────────────────────────

        // Per-edge data for ExitPoint placement.
        // Indexed by (int)CellEdge: North=0, East=1, South=2, West=3.
        private struct ExitEdgeData
        {
            public Vector3             posOffset;   // from cell center in solver world-space
            public Vector3             outwardDir;  // unit vector away from room interior
            public ExitPoint.Direction exitDir;
        }

        private static readonly ExitEdgeData[] s_ExitEdgeData = new ExitEdgeData[]
        {
            // North (CellEdge 0): +Z face
            new ExitEdgeData { posOffset = new Vector3(0f,                      0f, CellMap.CellSize * 0.5f),  outwardDir = Vector3.forward, exitDir = ExitPoint.Direction.North },
            // East  (CellEdge 1): +X face
            new ExitEdgeData { posOffset = new Vector3(CellMap.CellSize * 0.5f, 0f, 0f),                       outwardDir = Vector3.right,   exitDir = ExitPoint.Direction.East  },
            // South (CellEdge 2): -Z face
            new ExitEdgeData { posOffset = new Vector3(0f,                      0f, -CellMap.CellSize * 0.5f), outwardDir = Vector3.back,    exitDir = ExitPoint.Direction.South },
            // West  (CellEdge 3): -X face
            new ExitEdgeData { posOffset = new Vector3(-CellMap.CellSize * 0.5f, 0f, 0f),                      outwardDir = Vector3.left,    exitDir = ExitPoint.Direction.West  },
        };

        /// <summary>
        /// Stamps a <see cref="RoomPiece"/> component (with bounds) on this GameObject and
        /// spawns one <see cref="ExitPoint"/> child per doorway recorded on <paramref name="map"/>.
        /// Idempotent — stale V2_ExitPoint_ children from a prior build are destroyed first.
        /// </summary>
        public void PopulateRoomPiece(CellMap map)
        {
            // ── RoomPiece bounds ─────────────────────────────────────────────
            var piece = GetComponent<RoomPiece>() ?? gameObject.AddComponent<RoomPiece>();

            float width   = map.Width  * CellMap.CellSize;
            float depth   = map.Depth  * CellMap.CellSize;
            int   maxTier = map.GetMaxTierUsed();
            float height  = (maxTier + 1) * CellMap.TierHeight;

            // boundsSize = half-extents (RoomPiece convention: GetWorldBounds uses size*2).
            // Room geometry is XZ-centered at world origin, so boundsOffset.x/z = 0.
            // Y is floor-anchored: center sits at height/2 above Y=0.
            piece.boundsSize   = new Vector3(width * 0.5f, height * 0.5f, depth * 0.5f);
            piece.boundsOffset = new Vector3(0f, height * 0.5f, 0f);

            Debug.Log($"[RoomPiece] size=({width}, {height}, {depth}), center=(0, {height * 0.5f}, 0), maxTier={maxTier}");

            // ── Remove stale ExitPoints ──────────────────────────────────────
            var stale = new List<GameObject>();
            foreach (Transform child in transform)
                if (child.name.StartsWith("V2_ExitPoint_"))
                    stale.Add(child.gameObject);

            foreach (var go in stale)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(go);
                else
#endif
                    Destroy(go);
            }

            // ── Spawn ExitPoints ─────────────────────────────────────────────
            foreach (var (x, z, edge) in map.AllDoorways())
            {
                ExitEdgeData info = s_ExitEdgeData[(int)edge];

                Vector3 localPos = map.CellCenterWorld(x, z) + info.posOffset;

                var child = new GameObject($"V2_ExitPoint_{edge}_{x}_{z}");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = localPos;
                child.transform.localRotation = Quaternion.LookRotation(info.outwardDir, Vector3.up);

                var ep = child.AddComponent<ExitPoint>();
                ep.exitDirection = info.exitDir;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RegisterCreatedObjectUndo(child, "Build Room");
#endif
            }

            piece.RefreshExits();
        }

        // ── Prefab save ───────────────────────────────────────────────────────

        [ContextMenu("Save as RoomPiece Prefab")]
        private void SaveAsRoomPiecePrefab()
        {
#if UNITY_EDITOR
            string defaultName = $"V2_RoomPiece_{rectangleWidth}x{rectangleDepth}.prefab";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save RoomPiece Prefab", defaultName, "prefab", "Choose prefab save location");
            if (string.IsNullOrEmpty(path)) return;
            PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            Debug.Log($"[RoomBuilder] Saved prefab to {path}");
#endif
        }
    }
}
