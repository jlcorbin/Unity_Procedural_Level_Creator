using System.Collections.Generic;
using System.IO;
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
    /// Turns a <see cref="SolveResult"/> from <see cref="EdgeSolver"/> into actual scene geometry
    /// under a child <b>MOD_Room</b> GameObject. RoomPiece, ExitPoints, and all mesh prefabs live
    /// on MOD_Room so that saving it captures complete geometry, bounds, and exit data.
    /// The RoomBuilder GameObject is the tool; MOD_Room is the room asset.
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

        // ── Catalogue / Theme ─────────────────────────────────────────────────

        [Header("Catalogue / Theme")]
        [Tooltip("PieceCatalogue containing named prefab bundles (themes). " +
            "Leave empty to use the direct Prefabs slots below for every build.")]
        public PieceCatalogue catalogue;

        [Tooltip("Name of the theme to pull prefabs from. Empty = use direct Prefabs slots as fallback.")]
        public string themeName = "";

        // ── Doors ─────────────────────────────────────────────────────────────

        [Header("Doors")]
        [Range(0, 4)]
        [SerializeField, Tooltip("Auto-places doorway openings in order: N, S, E, W. Middle cell of each wall. 0 = solid room.")]
        public int doorCount = 0;

        // ── Output ────────────────────────────────────────────────────────────

        [Header("Output")]
        [SerializeField, Tooltip("Name of the child GameObject that holds all room geometry and the RoomPiece component.")]
        private string rootName = "MOD_Room";

        // ── Save Classification ───────────────────────────────────────────────

        [Header("Save Classification")]
        [Tooltip("Room or Hall — determines the save sub-folder and the RoomPiece pieceType.")]
        public PieceType pieceType = PieceType.Room;

        [Tooltip("Room sub-category. Only used when PieceType is Room.")]
        public RoomCategory roomCategory = RoomCategory.Small;

        [Tooltip("Hall sub-category. Only used when PieceType is Hall.")]
        public HallCategory hallCategory = HallCategory.Small;

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Destroys any existing MOD_Room child, solves a Rectangle cell-map, and instantiates
        /// floor, wall, and corner prefabs under a fresh MOD_Room child. RoomPiece and ExitPoints
        /// are stamped on MOD_Room so the child contains the complete room asset.
        /// Returns immediately with an error log if any core prefab is unassigned.
        /// </summary>
        public void Build()
        {
            GameObject resolvedFloor  = ResolvePrefab(PieceCatalogue.PieceType.Floor);
            GameObject resolvedWall   = ResolvePrefab(PieceCatalogue.PieceType.Wall);
            GameObject resolvedCorner = ResolvePrefab(PieceCatalogue.PieceType.Corner);

            if (resolvedFloor == null || resolvedWall == null || resolvedCorner == null)
            {
                Debug.LogError("[RoomBuilder] Build aborted — floor, wall, and corner prefabs are required " +
                               "(assign via a Theme on the catalogue, or fill in the direct Prefabs slots).");
                return;
            }

            if (cornerArmLength == CornerArmLength.Half &&
                (halfWallLPrefab == null || halfWallRPrefab == null))
            {
                Debug.LogError("[RoomBuilder] Build aborted — halfWallLPrefab and halfWallRPrefab are required when cornerArmLength is Half.");
                return;
            }

            // One-shot migration: remove legacy components from the builder itself.
            MigrateLegacyRoomPieceFromBuilder();

            // Destroy any existing MOD_Room child, then create a fresh one.
            DestroyChildRoot();
            GameObject root = GetOrCreateRoomRoot();

            CellMap map = ShapeStamp.Rectangle(rectangleWidth, rectangleDepth);
            ApplyAutoDoorways(map);
            SolveResult result = EdgeSolver.Solve(map, cornerArmLength);

            for (int i = 0; i < result.warnings.Count; i++)
                Debug.LogWarning($"[RoomBuilder] Solver warning: {result.warnings[i]}");

            var floorsGroup  = new GameObject("Floors").transform;
            var wallsGroup   = new GameObject("Walls").transform;
            var cornersGroup = new GameObject("Corners").transform;

            floorsGroup.SetParent(root.transform,  false);
            wallsGroup.SetParent(root.transform,   false);
            cornersGroup.SetParent(root.transform, false);

            Vector3 floorShift = FloorPivotShift(floorPivot);

            // ── Floors ────────────────────────────────────────────────────────
            for (int i = 0; i < result.floors.Count; i++)
            {
                FloorPlacement floor = result.floors[i];
                if (floor.tileType != TileType.Square) continue;

                GameObject go = InstantiatePiece(resolvedFloor, floorsGroup);
                go.name                    = $"Floor_{floor.gridCoord.x}_{floor.gridCoord.y}";
                go.transform.localPosition = floor.worldPosition + floorShift;
                go.transform.localRotation = floor.rotation;
            }

            // ── Walls ─────────────────────────────────────────────────────────
            for (int i = 0; i < result.walls.Count; i++)
            {
                WallPlacement wall = result.walls[i];

                GameObject prefab;
                switch (wall.kind)
                {
                    case WallKind.Straight: prefab = resolvedWall;    break;
                    case WallKind.HalfL:    prefab = halfWallLPrefab; break;
                    case WallKind.HalfR:    prefab = halfWallRPrefab; break;
                    default:                continue;
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
                GameObject      go     = InstantiatePiece(resolvedCorner, cornersGroup);
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
        /// Removes the RoomPiece component and all children from the MOD_Room child, leaving
        /// an empty child ready for the next build. Also runs the legacy migration.
        /// </summary>
        public void Clear()
        {
            MigrateLegacyRoomPieceFromBuilder();

            var rootTf = transform.Find(rootName);
            if (rootTf == null)
            {
                Debug.Log($"[RoomBuilder] Nothing to clear — '{rootName}' not found.");
                return;
            }

            GameObject roomRoot = rootTf.gameObject;

            var piece = roomRoot.GetComponent<RoomPiece>();
            if (piece != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.DestroyObjectImmediate(piece);
                else
#endif
                    DestroyImmediate(piece);
            }

            // Destroy all children (mesh groups + ExitPoints) in reverse index order.
            for (int i = roomRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = roomRoot.transform.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.DestroyObjectImmediate(child);
                    continue;
                }
#endif
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            Debug.Log($"[RoomBuilder] Cleared '{rootName}' — meshes, exits, and RoomPiece removed.");
        }

        // ─────────────────────────────────────────────────────────────────────

        // Returns the MOD_Room child, creating it as a child of this transform if absent.
        private GameObject GetOrCreateRoomRoot()
        {
            var existing = transform.Find(rootName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(rootName);
            go.transform.SetParent(transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, "Build Room");
#endif
            return go;
        }

        // Destroys the MOD_Room child (and its entire subtree) with Undo support in the editor.
        private void DestroyChildRoot()
        {
            var existing = transform.Find(rootName);
            if (existing == null) return;

#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(existing.gameObject);
#else
            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
#endif
            Debug.Log($"[RoomBuilder] Removed existing '{rootName}'.");
        }

        // Removes stale RoomPiece and V2_ExitPoint_* children that older code placed directly
        // on the RoomBuilder GameObject. Also destroys any scene-level MOD_Room that is not
        // a child of this builder (created by the pre-child-root architecture).
        private void MigrateLegacyRoomPieceFromBuilder()
        {
            var legacyPiece = GetComponent<RoomPiece>();
            if (legacyPiece != null)
            {
                DestroyImmediate(legacyPiece);
                Debug.Log("[RoomBuilder] Migrated legacy RoomPiece off the builder GameObject.");
            }

            var legacyExits = new List<GameObject>();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("V2_ExitPoint_"))
                    legacyExits.Add(child.gameObject);
            }
            foreach (var go in legacyExits)
                DestroyImmediate(go);
            if (legacyExits.Count > 0)
                Debug.Log($"[RoomBuilder] Migrated {legacyExits.Count} legacy ExitPoint children off the builder GameObject.");

            // Scene-level MOD_Room left over from the old architecture (not a child of this builder).
            var sceneRoot = GameObject.Find(rootName);
            if (sceneRoot != null && sceneRoot.transform.parent != transform)
            {
                DestroyImmediate(sceneRoot);
                Debug.Log($"[RoomBuilder] Destroyed legacy scene-level '{rootName}'.");
            }
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

        // ── Catalogue / theme resolution ──────────────────────────────────────

        /// <summary>
        /// Returns the prefab to use for the given category.
        /// Priority: theme slot → direct inspector slot fallback.
        /// </summary>
        public GameObject ResolvePrefab(PieceCatalogue.PieceType cat)
        {
            if (catalogue != null && !string.IsNullOrEmpty(themeName))
            {
                PieceCatalogue.Theme theme = catalogue.GetTheme(themeName);
                if (theme != null)
                {
                    GameObject themed = cat switch
                    {
                        PieceCatalogue.PieceType.Floor   => theme.floor,
                        PieceCatalogue.PieceType.Wall    => theme.wall,
                        PieceCatalogue.PieceType.Corner  => theme.corner,
                        PieceCatalogue.PieceType.Column  => theme.column,
                        PieceCatalogue.PieceType.Doorway => theme.doorway,
                        _                                => null,
                    };
                    if (themed != null) return themed;
                    Debug.LogWarning($"[RoomBuilder] Theme '{themeName}' has no prefab for {cat}. Falling back to direct slot.");
                }
            }
            return cat switch
            {
                PieceCatalogue.PieceType.Floor  => floorPrefab,
                PieceCatalogue.PieceType.Wall   => wallPrefab,
                PieceCatalogue.PieceType.Corner => cornerPrefab,
                _                               => null,
            };
        }

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
            float half = CellMap.CellSize * 0.5f;
            if (kind == WallKind.HalfL) return new Vector3(-half, 0f, 0f);
            switch (pivot)
            {
                case WallPivotPosition.StartX: return new Vector3( half, 0f, 0f);
                case WallPivotPosition.EndX:   return new Vector3(-half, 0f, 0f);
                default:                       return Vector3.zero;
            }
        }

        private static Vector3 FloorPivotShift(FloorPivotPosition pivot)
        {
            float half = CellMap.CellSize * 0.5f;
            switch (pivot)
            {
                case FloorPivotPosition.PivotNW: return new Vector3( half, 0f, -half);
                case FloorPivotPosition.PivotNE: return new Vector3(-half, 0f, -half);
                case FloorPivotPosition.PivotSW: return new Vector3( half, 0f,  half);
                case FloorPivotPosition.PivotSE: return new Vector3(-half, 0f,  half);
                default:                         return Vector3.zero;
            }
        }

        // ── RoomPiece + ExitPoint stamping ────────────────────────────────────

        private struct ExitEdgeData
        {
            public Vector3             posOffset;
            public Vector3             outwardDir;
            public ExitPoint.Direction exitDir;
        }

        private static readonly ExitEdgeData[] s_ExitEdgeData = new ExitEdgeData[]
        {
            // North (CellEdge 0): +Z face
            new ExitEdgeData { posOffset = new Vector3(0f,                       0f, CellMap.CellSize * 0.5f),  outwardDir = Vector3.forward, exitDir = ExitPoint.Direction.North },
            // East  (CellEdge 1): +X face
            new ExitEdgeData { posOffset = new Vector3(CellMap.CellSize * 0.5f,  0f, 0f),                       outwardDir = Vector3.right,   exitDir = ExitPoint.Direction.East  },
            // South (CellEdge 2): -Z face
            new ExitEdgeData { posOffset = new Vector3(0f,                       0f, -CellMap.CellSize * 0.5f), outwardDir = Vector3.back,    exitDir = ExitPoint.Direction.South },
            // West  (CellEdge 3): -X face
            new ExitEdgeData { posOffset = new Vector3(-CellMap.CellSize * 0.5f, 0f, 0f),                       outwardDir = Vector3.left,    exitDir = ExitPoint.Direction.West  },
        };

        /// <summary>
        /// Stamps a <see cref="RoomPiece"/> component (with bounds) on the <b>MOD_Room</b> child
        /// and spawns one <see cref="ExitPoint"/> child per doorway on <paramref name="map"/>.
        /// Creates MOD_Room if absent. Idempotent — stale V2_ExitPoint_* children are destroyed first.
        /// </summary>
        public void PopulateRoomPiece(CellMap map)
        {
            GameObject roomRoot = GetOrCreateRoomRoot();

            // ── RoomPiece bounds ─────────────────────────────────────────────
            var piece = roomRoot.GetComponent<RoomPiece>() ?? roomRoot.AddComponent<RoomPiece>();

            float width   = map.Width  * CellMap.CellSize;
            float depth   = map.Depth  * CellMap.CellSize;
            int   maxTier = map.GetMaxTierUsed();
            float height  = (maxTier + 1) * CellMap.TierHeight;

            piece.boundsSize   = new Vector3(width * 0.5f, height * 0.5f, depth * 0.5f);
            piece.boundsOffset = new Vector3(0f, height * 0.5f, 0f);

            Debug.Log($"[RoomPiece] size=({width}, {height}, {depth}), center=(0, {height * 0.5f}, 0), maxTier={maxTier}");

            piece.pieceType    = pieceType == PieceType.Room ? RoomPiece.PieceType.Room : RoomPiece.PieceType.Hall;
            piece.categoryName = ResolveCategoryName();

            // ── Remove stale ExitPoints on MOD_Room ──────────────────────────
            var stale = new List<GameObject>();
            foreach (Transform child in roomRoot.transform)
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

            // ── Spawn ExitPoints as children of MOD_Room ─────────────────────
            // Positions are in the solver's origin-centered frame, which equals MOD_Room's
            // local space (MOD_Room sits at (0,0,0) local on the RoomBuilder).
            foreach (var (x, z, edge) in map.AllDoorways())
            {
                ExitEdgeData info     = s_ExitEdgeData[(int)edge];
                Vector3      localPos = map.CellCenterWorld(x, z) + info.posOffset;

                var child = new GameObject($"V2_ExitPoint_{edge}_{x}_{z}");
                child.transform.SetParent(roomRoot.transform, false);
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

        // ── Save folder helpers ───────────────────────────────────────────────

        /// <summary>
        /// Returns the asset-relative folder path for the current Type + Category selection.
        /// Example: "Assets/Prefabs/Rooms/Starter".
        /// </summary>
        public string ResolveSaveFolder()
        {
            string typeFolder = pieceType == PieceType.Room ? "Rooms" : "Halls";
            return $"Assets/Prefabs/{typeFolder}/{ResolveCategoryName()}";
        }

        /// <summary>Returns the display name of the currently selected category ("Starter", "Small", etc).</summary>
        public string ResolveCategoryName()
        {
            return pieceType == PieceType.Room
                ? roomCategory.ToString()
                : hallCategory.ToString();
        }

        // ── Prefab save ───────────────────────────────────────────────────────

        /// <summary>
        /// Saves the MOD_Room child as a prefab into the categorized folder.
        /// Opens a file-save panel starting in that folder; cancels silently if dismissed.
        /// </summary>
        [ContextMenu("Save as RoomPiece")]
        public void SaveAsRoomPiece()
        {
#if UNITY_EDITOR
            string folder = ResolveSaveFolder();
            EnsureFolderExists(folder);

            string defaultName = $"{ResolveCategoryName()}_{rectangleWidth}x{rectangleDepth}";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save RoomPiece", defaultName, "prefab", "", folder);

            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("[RoomBuilder] Save cancelled.");
                return;
            }

            SaveRoomRootAsPrefab(path, Path.GetFileNameWithoutExtension(path));
#endif
        }

        /// <summary>
        /// Saves the MOD_Room child to a user-chosen path.
        /// Kept as a fallback when the categorized folder is not appropriate.
        /// </summary>
        [ContextMenu("Save to Custom Path…")]
        public void SaveToCustomPath()
        {
#if UNITY_EDITOR
            string defaultName = $"V2_RoomPiece_{rectangleWidth}x{rectangleDepth}";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save RoomPiece (Custom Path)", defaultName, "prefab", "");
            if (string.IsNullOrEmpty(path)) return;
            SaveRoomRootAsPrefab(path, Path.GetFileNameWithoutExtension(path));
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Walks <paramref name="assetPath"/> component-by-component and creates any missing folders.
        /// Safe to call when the folder already exists.
        /// </summary>
        public void EnsureFolderExists(string assetPath)
        {
            var    parts   = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                    Debug.Log($"[RoomBuilder] Created folder {next}");
                }
                current = next;
            }
        }

        /// <summary>
        /// Duplicates the MOD_Room child, renames the copy to <paramref name="newName"/>,
        /// saves it as a prefab at <paramref name="path"/>, then destroys the copy.
        /// The scene's MOD_Room is left intact. The duplicate is always destroyed — even if
        /// the save throws.
        /// </summary>
        public void SaveRoomRootAsPrefab(string path, string newName)
        {
            var rootTf = transform.Find(rootName);
            if (rootTf == null)
            {
                Debug.LogError("[RoomBuilder] No room root found. Build the room first.");
                return;
            }

            GameObject dup = Instantiate(rootTf.gameObject);
            dup.name = newName;
            dup.transform.SetParent(null, true);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(dup, path);
                Debug.Log($"[RoomBuilder] Saved '{newName}' to {path}");
            }
            finally
            {
                DestroyImmediate(dup);
            }
        }
#endif
    }
}
