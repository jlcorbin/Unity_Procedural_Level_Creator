using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Static utility that constructs a room from modular pieces on a snap grid.
    ///
    /// Grid rules:
    /// - <see cref="SNAP_UNIT"/> = 2 Unity units per tile.
    /// - Room pivot is at floor level (y = 0). Geometry extends upward.
    /// - Size presets: Small = 4×4 (2 tiles), Medium = 8×8 (4 tiles), Large = 12×12 (6 tiles).
    /// - <see cref="WALL_HEIGHT"/> = 6 units. Bounds half-extent Y = 3 always.
    /// - Bounds offset: X = 0, Y = <see cref="HALF_HEIGHT"/> (= 3), Z = 0.
    ///
    /// Wall/doorway prefab convention:
    /// - Prefab forward (+Z) faces into the room interior.
    /// - North wall y = 180°, South y = 0°, East y = 270°, West y = 90°.
    ///
    /// Doorway placement:
    /// - One center tile per exit wall: tile index = (gridSize - 1) / 2.
    /// - Doorway slot is marked occupied before walls are placed — no overlap.
    ///
    /// Direction reference (Unity axis convention):
    ///   North = +Z   South = −Z   East = +X   West = −X
    /// </summary>
    public static class RoomBuilder
    {
        // ── Constants ─────────────────────────────────────────────────────────

        /// <summary>Size of one grid tile in Unity units. All piece prefabs should match this.</summary>
        public const float SNAP_UNIT = 2f;

        /// <summary>Total wall height in Unity units (matches Fantastic Dungeon Pack tall walls).</summary>
        public const float WALL_HEIGHT = 6f;

        /// <summary>Half-height used for boundsSize.Y and boundsOffset.Y. Always WALL_HEIGHT / 2 = 3.</summary>
        public const float HALF_HEIGHT = WALL_HEIGHT * 0.5f;   // = 3f

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Constructs a room under <paramref name="root"/> according to the given definition
        /// and piece catalogue. Clears any existing children of root first.
        ///
        /// After this call, root will have:
        ///   • A <see cref="RoomPiece"/> component with correct boundsSize and boundsOffset.
        ///   • Child GameObjects for floor, wall, corner, ceiling, and doorway pieces.
        ///   • Child <see cref="ExitPoint"/> GameObjects at each active doorway exit.
        /// </summary>
        /// <param name="def">Room layout definition (size, exits, ceiling toggle).</param>
        /// <param name="cat">Piece catalogue to sample prefabs from.</param>
        /// <param name="root">Transform to build under (pivot becomes room floor centre).</param>
        /// <param name="rng">Seeded RNG for deterministic piece selection.</param>
        public static void BuildRoom(RoomDefinition def, PieceCatalogue cat,
                                      Transform root, System.Random rng)
        {
            ClearChildren(root);

            int   gridW = GetGridWidth(def);
            int   gridD = GetGridDepth(def);
            float halfW = gridW * SNAP_UNIT * 0.5f;
            float halfD = gridD * SNAP_UNIT * 0.5f;

            // ── RoomPiece bounds ──────────────────────────────────────────────
            var piece = root.GetComponent<RoomPiece>() ?? root.gameObject.AddComponent<RoomPiece>();
            ApplyBounds(piece, gridW, gridD);

            // ── Floor ─────────────────────────────────────────────────────────
            // Grid offset formula: tile i of count tiles sits at TileCenter(i, count)
            //   = (-count + 1 + 2*i) * SNAP_UNIT * 0.5
            // For count=4: centres at -3, -1, +1, +3  (spans -4 to +4 with SNAP_UNIT=2)
            // This places each tile edge-to-edge, not edge-at-centre.
            // ── Do NOT start at -halfWidth ── that produces the wrong offset. ──
            //
            // Doorway index: (gridSize - 1) / 2
            //   Even count-4 → index 1 → position -1  (left-of-centre tile)
            //   Odd  count-3 → index 1 → position  0  (true centre tile)
            // HashSet guards against any accidental double-placement of the same slot.
            var floorSlots = new HashSet<Vector2Int>();
            for (int ix = 0; ix < gridW; ix++)
            for (int iz = 0; iz < gridD; iz++)
            {
                var slot = new Vector2Int(ix, iz);
                if (!floorSlots.Add(slot)) continue;   // Add returns false if already present

                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Floor, root,
                    new Vector3(TileCenter(ix, gridW), 0f, TileCenter(iz, gridD)),
                    Quaternion.identity, rng, $"Floor_{ix},{iz}");
            }

            // ── Walls (with single-center-tile doorway substitution) ──────────
            // Doorway tile index = (gridSize - 1) / 2 (left-of-centre for even, true centre for odd).
            // Each wall method marks the doorway slot BEFORE filling remaining wall slots
            // so the two types never overlap at the same position.
            PlaceNorthWall(def, cat, root, gridW, halfD, rng);
            PlaceSouthWall(def, cat, root, gridW, halfD, rng);
            PlaceEastWall (def, cat, root, gridD, halfW, rng);
            PlaceWestWall (def, cat, root, gridD, halfW, rng);

            // ── Corners ───────────────────────────────────────────────────────
            // Exactly 4 corners, one per outer corner, rotated to face inward diagonally.
            PlaceCorners(cat, root, halfW, halfD, rng);

            // ── Ceiling (optional) ────────────────────────────────────────────
            if (def.includeCeiling)
            {
                for (int ix = 0; ix < gridW; ix++)
                for (int iz = 0; iz < gridD; iz++)
                {
                    PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Ceiling, root,
                        new Vector3(TileCenter(ix, gridW), WALL_HEIGHT, TileCenter(iz, gridD)),
                        Quaternion.Euler(180f, 0f, 0f), rng, $"Ceiling_{ix},{iz}");
                }
            }

            // ── ExitPoints ────────────────────────────────────────────────────
            CreateExitPoints(def, root, halfW, halfD);

            piece.RefreshExits();
        }

        /// <summary>
        /// Recalculates and applies boundsSize and boundsOffset on the <see cref="RoomPiece"/>
        /// attached to <paramref name="root"/> without rebuilding any geometry.
        ///
        /// Correct formula (floor-level pivot, wall height = 6):
        ///   boundsSize   = (halfW, 3, halfD)
        ///   boundsOffset = (0, 3, 0)
        /// </summary>
        public static void RecalculateBounds(RoomDefinition def, Transform root)
        {
            var piece = root.GetComponent<RoomPiece>();
            if (piece == null)
            {
                Debug.LogWarning("[RoomBuilder] RecalculateBounds: no RoomPiece on root.");
                return;
            }
            ApplyBounds(piece, GetGridWidth(def), GetGridDepth(def));
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(piece);
#endif
        }

        // ── Grid helpers ──────────────────────────────────────────────────────

        /// <summary>Returns the number of grid tiles along the X (width) axis.</summary>
        public static int GetGridWidth(RoomDefinition def)
        {
            return def.sizePreset switch
            {
                RoomDefinition.SizePreset.Small  => 2,
                RoomDefinition.SizePreset.Medium => 4,
                RoomDefinition.SizePreset.Large  => 6,
                RoomDefinition.SizePreset.Custom => Mathf.Max(1, def.customWidth),
                _                                => 4,
            };
        }

        /// <summary>Returns the number of grid tiles along the Z (depth) axis.</summary>
        public static int GetGridDepth(RoomDefinition def)
        {
            return def.sizePreset switch
            {
                RoomDefinition.SizePreset.Small  => 2,
                RoomDefinition.SizePreset.Medium => 4,
                RoomDefinition.SizePreset.Large  => 6,
                RoomDefinition.SizePreset.Custom => Mathf.Max(1, def.customDepth),
                _                                => 4,
            };
        }

        // ── Private: bounds ───────────────────────────────────────────────────

        private static void ApplyBounds(RoomPiece piece, int gridW, int gridD)
        {
            float halfW = gridW * SNAP_UNIT * 0.5f;
            float halfD = gridD * SNAP_UNIT * 0.5f;

            // boundsSize stores half-extents. Y = HALF_HEIGHT = 3 always (wall height 6).
            piece.boundsSize = new Vector3(halfW, HALF_HEIGHT, halfD);

            // boundsOffset shifts the AABB center upward so it wraps the mesh,
            // not buried at the floor pivot. X and Z are always 0 for _M_ pieces.
            piece.boundsOffset = new Vector3(0f, HALF_HEIGHT, 0f);
        }

        // ── Private: wall placement ───────────────────────────────────────────

        // North wall — z = +halfD, pieces face inward (South) → y rot 180°
        private static void PlaceNorthWall(RoomDefinition def, PieceCatalogue cat,
                                            Transform root, int gridW, float halfD,
                                            System.Random rng)
        {
            var rot     = Quaternion.Euler(0f, 180f, 0f);
            int doorIdx = (gridW - 1) / 2;                     // left-of-centre for even, centre for odd
            var occupied = new HashSet<int>();

            // Doorway first — marks the center slot before walls fill the rest
            if (def.exitNorth && def.doorwayNorth)
            {
                occupied.Add(doorIdx);
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Doorway, root,
                    new Vector3(TileCenter(doorIdx, gridW), 0f, halfD), rot, rng, "N_Door");
            }

            // Remaining wall slots
            for (int ix = 0; ix < gridW; ix++)
            {
                if (occupied.Contains(ix)) continue;
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Wall, root,
                    new Vector3(TileCenter(ix, gridW), 0f, halfD), rot, rng, $"N_{ix}");
            }
        }

        // South wall — z = −halfD, pieces face inward (North) → y rot 0°
        private static void PlaceSouthWall(RoomDefinition def, PieceCatalogue cat,
                                            Transform root, int gridW, float halfD,
                                            System.Random rng)
        {
            var rot     = Quaternion.Euler(0f, 0f, 0f);
            int doorIdx = (gridW - 1) / 2;
            var occupied = new HashSet<int>();

            if (def.exitSouth && def.doorwaySouth)
            {
                occupied.Add(doorIdx);
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Doorway, root,
                    new Vector3(TileCenter(doorIdx, gridW), 0f, -halfD), rot, rng, "S_Door");
            }

            for (int ix = 0; ix < gridW; ix++)
            {
                if (occupied.Contains(ix)) continue;
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Wall, root,
                    new Vector3(TileCenter(ix, gridW), 0f, -halfD), rot, rng, $"S_{ix}");
            }
        }

        // East wall — x = +halfW, pieces face inward (West) → y rot 270°
        private static void PlaceEastWall(RoomDefinition def, PieceCatalogue cat,
                                           Transform root, int gridD, float halfW,
                                           System.Random rng)
        {
            var rot     = Quaternion.Euler(0f, 270f, 0f);
            int doorIdx = (gridD - 1) / 2;
            var occupied = new HashSet<int>();

            if (def.exitEast && def.doorwayEast)
            {
                occupied.Add(doorIdx);
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Doorway, root,
                    new Vector3(halfW, 0f, TileCenter(doorIdx, gridD)), rot, rng, "E_Door");
            }

            for (int iz = 0; iz < gridD; iz++)
            {
                if (occupied.Contains(iz)) continue;
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Wall, root,
                    new Vector3(halfW, 0f, TileCenter(iz, gridD)), rot, rng, $"E_{iz}");
            }
        }

        // West wall — x = −halfW, pieces face inward (East) → y rot 90°
        private static void PlaceWestWall(RoomDefinition def, PieceCatalogue cat,
                                           Transform root, int gridD, float halfW,
                                           System.Random rng)
        {
            var rot     = Quaternion.Euler(0f, 90f, 0f);
            int doorIdx = (gridD - 1) / 2;
            var occupied = new HashSet<int>();

            if (def.exitWest && def.doorwayWest)
            {
                occupied.Add(doorIdx);
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Doorway, root,
                    new Vector3(-halfW, 0f, TileCenter(doorIdx, gridD)), rot, rng, "W_Door");
            }

            for (int iz = 0; iz < gridD; iz++)
            {
                if (occupied.Contains(iz)) continue;
                PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Wall, root,
                    new Vector3(-halfW, 0f, TileCenter(iz, gridD)), rot, rng, $"W_{iz}");
            }
        }

        // ── Private: corners ──────────────────────────────────────────────────

        // Four outer corners, each rotated so its forward (+Z) faces inward diagonally.
        //   SW (−halfW, −halfD) → 0°   SE (+halfW, −halfD) → 90°
        //   NE (+halfW, +halfD) → 180° NW (−halfW, +halfD) → 270°
        private static void PlaceCorners(PieceCatalogue cat, Transform root,
                                          float halfW, float halfD, System.Random rng)
        {
            PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Corner, root,
                new Vector3(-halfW, 0f, -halfD), Quaternion.Euler(0f,   0f, 0f), rng, "Corner_SW");
            PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Corner, root,
                new Vector3( halfW, 0f, -halfD), Quaternion.Euler(0f,  90f, 0f), rng, "Corner_SE");
            PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Corner, root,
                new Vector3( halfW, 0f,  halfD), Quaternion.Euler(0f, 180f, 0f), rng, "Corner_NE");
            PlaceFromCatalogue(cat, PieceCatalogue.PieceType.Corner, root,
                new Vector3(-halfW, 0f,  halfD), Quaternion.Euler(0f, 270f, 0f), rng, "Corner_NW");
        }

        // ── Private: ExitPoints ───────────────────────────────────────────────

        private static void CreateExitPoints(RoomDefinition def, Transform root,
                                              float halfW, float halfD)
        {
            // Horizontal exits placed at the wall face center, y = 0 (floor level).
            // Only create an ExitPoint when both the exit flag AND doorway flag are set
            // (a wall-only exit has no physical opening for the generator to connect through).
            if (def.exitNorth && def.doorwayNorth)
                AddExitPoint(root, ExitPoint.Direction.North, new Vector3(0f, 0f,  halfD));
            if (def.exitSouth && def.doorwaySouth)
                AddExitPoint(root, ExitPoint.Direction.South, new Vector3(0f, 0f, -halfD));
            if (def.exitEast  && def.doorwayEast)
                AddExitPoint(root, ExitPoint.Direction.East,  new Vector3( halfW, 0f, 0f));
            if (def.exitWest  && def.doorwayWest)
                AddExitPoint(root, ExitPoint.Direction.West,  new Vector3(-halfW, 0f, 0f));

            // Vertical exits
            if (def.exitUp)
                AddExitPoint(root, ExitPoint.Direction.Up,   new Vector3(0f, WALL_HEIGHT, 0f));
            if (def.exitDown)
                AddExitPoint(root, ExitPoint.Direction.Down, Vector3.zero);
        }

        private static void AddExitPoint(Transform root, ExitPoint.Direction dir, Vector3 localPos)
        {
            var go = new GameObject($"Exit_{dir}");
            go.transform.SetParent(root, worldPositionStays: false);
            go.transform.localPosition = localPos;
            go.AddComponent<ExitPoint>().exitDirection = dir;
        }

        // ── Private: piece placement ──────────────────────────────────────────

        private static void PlaceFromCatalogue(PieceCatalogue cat,
                                                PieceCatalogue.PieceType type,
                                                Transform root,
                                                Vector3 localPos,
                                                Quaternion localRot,
                                                System.Random rng,
                                                string label)
        {
            if (cat == null) return;

            var prefab = cat.GetRandom(type, rng);
            if (prefab == null)
            {
                Debug.LogWarning($"[RoomBuilder] No '{type}' prefab found in catalogue for slot '{label}'. " +
                                 "Add pieces of this type to the PieceCatalogue.");
                return;
            }

            GameObject go;
#if UNITY_EDITOR
            go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root);
#else
            go = Object.Instantiate(prefab, root);
#endif
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.name = $"{type}_{label}";
        }

        // ── Private: utilities ────────────────────────────────────────────────

        /// <summary>
        /// Returns the local-space centre coordinate of tile <paramref name="i"/>
        /// in a row of <paramref name="count"/> tiles, symmetric around 0.
        ///
        /// Formula: (−count + 1 + 2i) × SNAP_UNIT × 0.5
        /// Example (SNAP_UNIT=2, count=4): centres at −3, −1, 1, 3.
        /// Example (SNAP_UNIT=2, count=6): centres at −5, −3, −1, 1, 3, 5.
        /// </summary>
        private static float TileCenter(int i, int count)
        {
            return (-count + 1 + 2 * i) * SNAP_UNIT * 0.5f;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }
}
