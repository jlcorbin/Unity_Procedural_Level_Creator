using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// How a wall edge is shaped.
    /// Only <see cref="Straight"/> is emitted in the current pass.
    /// The remaining values are reserved for future curved/diagonal walls.
    /// </summary>
    public enum WallKind
    {
        Straight,  // standard wall along a cell edge
        Angle,     // deferred
        Concave,   // deferred
        Convex,    // deferred
    }

    /// <summary>
    /// How two meeting walls are oriented at a corner vertex.
    /// Only <see cref="Outward"/> is emitted in the current pass.
    /// </summary>
    public enum CornerKind
    {
        Outward,   // 90° corner facing away from the room interior
        Inward,    // deferred — concave junction in L-shapes
        Diagonal,  // deferred — where a diagonal meets a straight wall
    }

    /// <summary>
    /// Abstract placement instruction for one floor tile.
    /// No prefab reference — the builder resolves that from its catalogue.
    /// </summary>
    [Serializable]
    public struct FloorPlacement
    {
        /// <summary>World-space center of this floor cell at its tier Y height.</summary>
        public Vector3    worldPosition;
        /// <summary>Cell rotation (quarter-turns around Y).</summary>
        public Quaternion rotation;
        /// <summary>Tile type, used by the builder to pick the correct floor prefab.</summary>
        public TileType   tileType;
        /// <summary>Vertical tier index (0 = ground level).</summary>
        public int        tier;
        /// <summary>Grid coordinate (x, z) of the source cell — useful for naming and debug.</summary>
        public Vector2Int gridCoord;
    }

    /// <summary>
    /// Abstract placement instruction for one wall segment on a cell edge.
    /// </summary>
    [Serializable]
    public struct WallPlacement
    {
        /// <summary>
        /// World-space centre of the wall piece — the midpoint of the cell edge,
        /// flush with the cell boundary.
        /// </summary>
        public Vector3    worldPosition;
        /// <summary>
        /// Rotation such that the wall's local +Z points INTO the room interior
        /// (i.e. back toward the cell centre from the edge).
        /// </summary>
        public Quaternion rotation;
        /// <summary>Wall shape kind. Straight for this pass.</summary>
        public WallKind   kind;
        /// <summary>Vertical tier index.</summary>
        public int        tier;
        /// <summary>Which edge of the source cell this wall sits on.</summary>
        public CellEdge   edge;
        /// <summary>Grid coordinate (x, z) of the source cell.</summary>
        public Vector2Int gridCoord;
    }

    /// <summary>
    /// Abstract placement instruction for one corner piece at a cell-vertex where
    /// two perpendicular wall edges meet.
    /// </summary>
    [Serializable]
    public struct CornerPlacement
    {
        /// <summary>
        /// World-space centre of the corner piece — the shared vertex of the two
        /// meeting wall edges.
        /// </summary>
        public Vector3    worldPosition;
        /// <summary>
        /// Rotation such that the corner's local +Z bisects the two adjacent wall
        /// faces and points INTO the room interior.
        /// </summary>
        public Quaternion rotation;
        /// <summary>Corner geometry kind. Outward for this pass.</summary>
        public CornerKind kind;
        /// <summary>Vertical tier index.</summary>
        public int        tier;
        /// <summary>
        /// Grid coordinate (x, z) of the anchor cell — the filled cell that owns
        /// and emits this corner.
        /// </summary>
        public Vector2Int gridCoord;
    }

    /// <summary>
    /// Output container for a single <see cref="EdgeSolver.Solve"/> run.
    /// All four lists are always initialised (never null).
    /// </summary>
    public class SolveResult
    {
        /// <summary>One entry per filled floor cell.</summary>
        public List<FloorPlacement>  floors;
        /// <summary>One entry per cell edge that borders empty space or a lower tier.</summary>
        public List<WallPlacement>   walls;
        /// <summary>One entry per outward 90-degree junction between two wall edges.</summary>
        public List<CornerPlacement> corners;
        /// <summary>User-visible notes: unsupported tile types, empty maps, etc.</summary>
        public List<string>          warnings;

        /// <summary>Initialises all four lists to empty.</summary>
        public SolveResult()
        {
            floors   = new List<FloorPlacement>();
            walls    = new List<WallPlacement>();
            corners  = new List<CornerPlacement>();
            warnings = new List<string>();
        }

        /// <summary>Concise count summary, e.g. "Solve: 15 floors, 16 walls, 4 corners, 0 warnings".</summary>
        public override string ToString() =>
            $"Solve: {floors.Count} floors, {walls.Count} walls, {corners.Count} corners, {warnings.Count} warnings";
    }

    /// <summary>
    /// Walks a <see cref="CellMap"/> and produces three ordered placement lists:
    /// floors, walls, and outward corner junctions.
    ///
    /// <para>The solver is pure data — in: CellMap, out: SolveResult.
    /// It does not pick prefabs, read any catalogue, or touch the scene.</para>
    ///
    /// <para><b>Current scope (Phase 2 / Pass 1):</b>
    /// <list type="bullet">
    /// <item><see cref="TileType.Square"/> cells only — non-Square tiles are warned and skipped.</item>
    /// <item>Tier 0 only — higher tiers are skipped in wall and corner passes.</item>
    /// <item>Straight walls on cardinal cell edges.</item>
    /// <item>Outward 90-degree corners at convex junctions only.</item>
    /// </list>
    /// Triangles, quarter-circles, tier stacking, and inward (concave) corners are deferred.
    /// </para>
    /// </summary>
    public static class EdgeSolver
    {
        private const float HalfCell = CellMap.CellSize * 0.5f; // 2f

        // Precomputed wall rotations: local +Z faces INTO the room from each edge.
        private static readonly Quaternion WallRotNorth = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Quaternion WallRotEast  = Quaternion.Euler(0f, 270f, 0f);
        private static readonly Quaternion WallRotSouth = Quaternion.Euler(0f,   0f, 0f);
        private static readonly Quaternion WallRotWest  = Quaternion.Euler(0f,  90f, 0f);

        // Corner rotations: local +Z bisects the two wall faces and points INTO the room.
        // NE outward corner: walls on N and E → room interior is SW → bisector = 225°
        private static readonly Quaternion CornerRotNE = Quaternion.Euler(0f, 225f, 0f);
        // NW outward corner: walls on N and W → room interior is SE → bisector = 135°
        private static readonly Quaternion CornerRotNW = Quaternion.Euler(0f, 135f, 0f);
        // SE outward corner: walls on S and E → room interior is NW → bisector = 315°
        private static readonly Quaternion CornerRotSE = Quaternion.Euler(0f, 315f, 0f);
        // SW outward corner: walls on S and W → room interior is NE → bisector = 45°
        private static readonly Quaternion CornerRotSW = Quaternion.Euler(0f,  45f, 0f);

        /// <summary>
        /// Solves the given <paramref name="map"/> and returns a <see cref="SolveResult"/>
        /// containing floor, wall, and corner placements.
        ///
        /// <para>Never returns null. On a null or empty map returns a SolveResult with
        /// empty placement lists and a single warning string. Does not throw.</para>
        /// </summary>
        /// <param name="map">Source cell map. May be null.</param>
        public static SolveResult Solve(CellMap map)
        {
            var result = new SolveResult();

            if (map == null)
            {
                result.warnings.Add("map is null");
                return result;
            }

            if (map.FilledCount() == 0)
            {
                result.warnings.Add("map has no filled cells");
                return result;
            }

            // Track which non-Square tile types have already produced a warning so we
            // warn only once per type per Solve call.
            var warnedTypes = new HashSet<TileType>();

            // Pass 1 — Floors
            // Emit one FloorPlacement per filled cell. Skip non-Square tiles with a warning.
            foreach (var (x, z, cell) in map.EnumerateFilled())
            {
                if (cell.type != TileType.Square)
                {
                    if (warnedTypes.Add(cell.type))
                        result.warnings.Add($"Unsupported tile type skipped: {cell.type}");
                    continue;
                }

                float   yOffset = cell.tier * CellMap.TierHeight;
                Vector3 center  = map.CellCenterWorld(x, z);

                result.floors.Add(new FloorPlacement
                {
                    worldPosition = center + new Vector3(0f, yOffset, 0f),
                    rotation      = cell.Rotation,
                    tileType      = cell.type,
                    tier          = cell.tier,
                    gridCoord     = new Vector2Int(x, z),
                });
            }

            // Passes 2 & 3 — Walls and Corners
            // Only Square cells at tier 0 are processed here.
            //
            // Corner deduplication: each grid vertex (vx, vz) is a shared point between
            // up to four cells. A vertex is identified by an integer key derived from the
            // column/row of the cell whose NE corner lands on that vertex. If the owning
            // cell is outside the grid (common for boundary corners of convex shapes), the
            // vertex is instead emitted by the first qualifying cell that checks it.
            // A HashSet ensures each vertex is emitted at most once regardless of which
            // cell triggers it.
            var emittedVertices = new HashSet<long>();

            foreach (var (x, z, cell) in map.EnumerateFilled())
            {
                if (cell.type != TileType.Square || cell.tier != 0) continue;

                Vector3 center = map.CellCenterWorld(x, z);

                // -- Pass 2: Walls --
                EmitWallIfNeeded(map, result, x, z, center, CellEdge.North);
                EmitWallIfNeeded(map, result, x, z, center, CellEdge.East);
                EmitWallIfNeeded(map, result, x, z, center, CellEdge.South);
                EmitWallIfNeeded(map, result, x, z, center, CellEdge.West);

                // -- Pass 3: Corners --
                // Each filled cell checks the four vertices at its four corners.
                // For each vertex: if the two adjacent cell-edges both have walls AND the
                // diagonal cell across the vertex is empty, this is an outward corner.
                // A vertex HashSet key prevents any vertex from being emitted twice.

                // NE vertex: walls on N and E, diagonal at (x+1, z+1)
                if (map.HasWallOnEdge(x, z, CellEdge.North) &&
                    map.HasWallOnEdge(x, z, CellEdge.East)  &&
                    map.GetCell(x + 1, z + 1).IsEmpty)
                {
                    // Key: the vertex is at the NE of cell (x,z), i.e. grid-vertex (x+1, z+1).
                    TryEmitCorner(result, emittedVertices,
                        x + 1, z + 1,
                        center + new Vector3( HalfCell, 0f,  HalfCell),
                        CornerRotNE, x, z);
                }

                // NW vertex: walls on N and W, diagonal at (x-1, z+1)
                if (map.HasWallOnEdge(x, z, CellEdge.North) &&
                    map.HasWallOnEdge(x, z, CellEdge.West)  &&
                    map.GetCell(x - 1, z + 1).IsEmpty)
                {
                    TryEmitCorner(result, emittedVertices,
                        x, z + 1,
                        center + new Vector3(-HalfCell, 0f,  HalfCell),
                        CornerRotNW, x, z);
                }

                // SE vertex: walls on S and E, diagonal at (x+1, z-1)
                if (map.HasWallOnEdge(x, z, CellEdge.South) &&
                    map.HasWallOnEdge(x, z, CellEdge.East)   &&
                    map.GetCell(x + 1, z - 1).IsEmpty)
                {
                    TryEmitCorner(result, emittedVertices,
                        x + 1, z,
                        center + new Vector3( HalfCell, 0f, -HalfCell),
                        CornerRotSE, x, z);
                }

                // SW vertex: walls on S and W, diagonal at (x-1, z-1)
                if (map.HasWallOnEdge(x, z, CellEdge.South) &&
                    map.HasWallOnEdge(x, z, CellEdge.West)   &&
                    map.GetCell(x - 1, z - 1).IsEmpty)
                {
                    TryEmitCorner(result, emittedVertices,
                        x, z,
                        center + new Vector3(-HalfCell, 0f, -HalfCell),
                        CornerRotSW, x, z);
                }
            }

            return result;
        }

        // Emits a WallPlacement for the given edge of cell (x,z) if HasWallOnEdge returns true.
        private static void EmitWallIfNeeded(
            CellMap map, SolveResult result,
            int x, int z, Vector3 center, CellEdge edge)
        {
            if (!map.HasWallOnEdge(x, z, edge)) return;

            Vector3    offset;
            Quaternion rotation;

            switch (edge)
            {
                case CellEdge.North:
                    offset   = new Vector3(0f,      0f,  HalfCell);
                    rotation = WallRotNorth;
                    break;
                case CellEdge.East:
                    offset   = new Vector3( HalfCell, 0f, 0f);
                    rotation = WallRotEast;
                    break;
                case CellEdge.South:
                    offset   = new Vector3(0f,      0f, -HalfCell);
                    rotation = WallRotSouth;
                    break;
                default: // West
                    offset   = new Vector3(-HalfCell, 0f, 0f);
                    rotation = WallRotWest;
                    break;
            }

            result.walls.Add(new WallPlacement
            {
                worldPosition = center + offset,
                rotation      = rotation,
                kind          = WallKind.Straight,
                tier          = 0,
                edge          = edge,
                gridCoord     = new Vector2Int(x, z),
            });
        }

        // Adds a CornerPlacement for vertex (vx,vz) if not already emitted.
        private static void TryEmitCorner(
            SolveResult result, HashSet<long> emitted,
            int vx, int vz,
            Vector3 worldPos, Quaternion rotation,
            int anchorX, int anchorZ)
        {
            long key = VertexKey(vx, vz);
            if (!emitted.Add(key)) return; // already emitted by another cell

            result.corners.Add(new CornerPlacement
            {
                worldPosition = worldPos,
                rotation      = rotation,
                kind          = CornerKind.Outward,
                tier          = 0,
                gridCoord     = new Vector2Int(anchorX, anchorZ),
            });
        }

        // Packs two ints into a long for use as a HashSet key.
        private static long VertexKey(int vx, int vz) => ((long)vx << 32) | (uint)vz;
    }
}
