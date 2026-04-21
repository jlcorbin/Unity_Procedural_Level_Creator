using UnityEngine;
using UnityEditor;
using System.IO;

namespace LevelGen
{
    /// <summary>
    /// Editor utility that creates placeholder room and hall prefabs
    /// using primitive cube meshes with properly positioned ExitPoint children.
    ///
    /// Menu: LevelGen ▶ Create Placeholder Prefabs
    /// </summary>
    public static class PlaceholderPrefabFactory
    {
        // ── Output paths ──────────────────────────────────────────────────────
        private const string RoomsPath = "Assets/Prefabs/Rooms";
        private const string HallsPath = "Assets/Prefabs/Halls";

        // ── Room dimensions (half-extents used by RoomPiece.boundsSize) ───────
        // Room: 10 × 6 × 10  →  half-extents (5, 3, 5)
        private static readonly Vector3 RoomHalfExtents  = new Vector3(5f, 3f, 5f);
        // Hall: 4 × 4 × 10   →  half-extents (2, 2, 5)
        private static readonly Vector3 HallHalfExtents  = new Vector3(2f, 2f, 5f);

        // ── Menu entry ────────────────────────────────────────────────────────
        [MenuItem("LevelGen/Create Placeholder Prefabs")]
        public static void CreateAll()
        {
            EnsureDirectory(RoomsPath);
            EnsureDirectory(HallsPath);

            CreateStartRoom();
            CreateRoom4Way();
            CreateRoomTShape();
            CreateHallStraight();
            CreateHallCorner();
            CreateDeadEnd();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LevelGen] Placeholder prefabs created in " +
                      $"{RoomsPath} and {HallsPath}.");
        }

        // ── Prefab definitions ────────────────────────────────────────────────

        /// <summary>
        /// Start room — 4-way exits (N/S/E/W), type = StartRoom.
        /// Mesh tinted green so it stands out.
        /// </summary>
        private static void CreateStartRoom()
        {
            var root = BuildRoomRoot("StartRoom", RoomPiece.PieceType.StartRoom,
                                     RoomHalfExtents, Color.green);

            AddExit(root, "Exit_North", new Vector3(0, 0,  RoomHalfExtents.z), ExitPoint.Direction.North);
            AddExit(root, "Exit_South", new Vector3(0, 0, -RoomHalfExtents.z), ExitPoint.Direction.South);
            AddExit(root, "Exit_East",  new Vector3( RoomHalfExtents.x, 0, 0), ExitPoint.Direction.East);
            AddExit(root, "Exit_West",  new Vector3(-RoomHalfExtents.x, 0, 0), ExitPoint.Direction.West);

            SavePrefab(root, RoomsPath, "StartRoom");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Standard room — 4-way exits (N/S/E/W), type = Room.
        /// </summary>
        private static void CreateRoom4Way()
        {
            var root = BuildRoomRoot("Room_4Way", RoomPiece.PieceType.Room,
                                     RoomHalfExtents, new Color(0.4f, 0.6f, 1f));

            AddExit(root, "Exit_North", new Vector3(0, 0,  RoomHalfExtents.z), ExitPoint.Direction.North);
            AddExit(root, "Exit_South", new Vector3(0, 0, -RoomHalfExtents.z), ExitPoint.Direction.South);
            AddExit(root, "Exit_East",  new Vector3( RoomHalfExtents.x, 0, 0), ExitPoint.Direction.East);
            AddExit(root, "Exit_West",  new Vector3(-RoomHalfExtents.x, 0, 0), ExitPoint.Direction.West);

            SavePrefab(root, RoomsPath, "Room_4Way");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// T-shaped room — 3 exits (N/E/W), type = Room.
        /// Useful for branching without a return south.
        /// </summary>
        private static void CreateRoomTShape()
        {
            var root = BuildRoomRoot("Room_TShape", RoomPiece.PieceType.Room,
                                     RoomHalfExtents, new Color(0.4f, 0.6f, 1f));

            AddExit(root, "Exit_North", new Vector3(0, 0,  RoomHalfExtents.z), ExitPoint.Direction.North);
            AddExit(root, "Exit_East",  new Vector3( RoomHalfExtents.x, 0, 0), ExitPoint.Direction.East);
            AddExit(root, "Exit_West",  new Vector3(-RoomHalfExtents.x, 0, 0), ExitPoint.Direction.West);

            SavePrefab(root, RoomsPath, "Room_TShape");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Straight hall — exits North and South, type = Hall.
        /// </summary>
        private static void CreateHallStraight()
        {
            var root = BuildRoomRoot("Hall_Straight", RoomPiece.PieceType.Hall,
                                     HallHalfExtents, new Color(0.85f, 0.65f, 0.3f));

            AddExit(root, "Exit_North", new Vector3(0, 0,  HallHalfExtents.z), ExitPoint.Direction.North);
            AddExit(root, "Exit_South", new Vector3(0, 0, -HallHalfExtents.z), ExitPoint.Direction.South);

            SavePrefab(root, HallsPath, "Hall_Straight");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Corner hall — exits North and East, type = Hall.
        /// </summary>
        private static void CreateHallCorner()
        {
            var root = BuildRoomRoot("Hall_Corner", RoomPiece.PieceType.Hall,
                                     HallHalfExtents, new Color(0.85f, 0.65f, 0.3f));

            AddExit(root, "Exit_North", new Vector3(0, 0,  HallHalfExtents.z), ExitPoint.Direction.North);
            AddExit(root, "Exit_East",  new Vector3( HallHalfExtents.x, 0, 0), ExitPoint.Direction.East);

            SavePrefab(root, HallsPath, "Hall_Corner");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Dead-end cap — single South exit, type = DeadEnd.
        /// Placed by the generator to close off open sockets.
        /// </summary>
        private static void CreateDeadEnd()
        {
            // Dead-ends are small — half the room height, half the depth
            var halfExtents = new Vector3(RoomHalfExtents.x, RoomHalfExtents.y,
                                          RoomHalfExtents.z * 0.5f);
            var root = BuildRoomRoot("DeadEnd", RoomPiece.PieceType.DeadEnd,
                                     halfExtents, new Color(0.7f, 0.3f, 0.3f));

            AddExit(root, "Exit_South", new Vector3(0, 0, -halfExtents.z), ExitPoint.Direction.South);

            SavePrefab(root, RoomsPath, "DeadEnd");
            Object.DestroyImmediate(root);
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a root GameObject with:
        ///   • a RoomPiece component configured with the given type and bounds
        ///   • a child "Mesh" cube scaled to fill the bounds (colored for clarity)
        /// </summary>
        private static GameObject BuildRoomRoot(string name,
                                                 RoomPiece.PieceType type,
                                                 Vector3 halfExtents,
                                                 Color meshColor)
        {
            var root = new GameObject(name);
            var piece = root.AddComponent<RoomPiece>();
            piece.pieceType   = type;
            piece.boundsSize  = halfExtents;
            piece.boundsOffset = Vector3.zero;

            // Visual mesh — primitive cube, scaled to full bounds size
            var meshGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshGO.name = "Mesh";
            meshGO.transform.SetParent(root.transform, false);
            meshGO.transform.localScale = halfExtents * 2f;

            // Tint the mesh via a shared material instance so each type is distinct
            var renderer = meshGO.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Create a new material so we don't modify the shared default
                var mat = new Material(renderer.sharedMaterial) { color = meshColor };
                renderer.sharedMaterial = mat;
            }

            // Remove the box collider from the primitive — bounds checks use AABB math
            var col = meshGO.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);

            return root;
        }

        /// <summary>
        /// Adds an ExitPoint child at the given local position facing the given direction.
        /// </summary>
        private static void AddExit(GameObject parent,
                                     string childName,
                                     Vector3 localPosition,
                                     ExitPoint.Direction direction)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;

            var exit = go.AddComponent<ExitPoint>();
            exit.exitDirection = direction;
        }

        /// <summary>
        /// Saves the in-memory GameObject as a prefab asset, then cleans up.
        /// Overwrites any existing prefab at the same path.
        /// </summary>
        private static void SavePrefab(GameObject root, string folder, string assetName)
        {
            string path = $"{folder}/{assetName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[LevelGen] Saved prefab: {path}");
        }

        /// <summary>
        /// Creates the directory (and its .meta) if it does not already exist.
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
