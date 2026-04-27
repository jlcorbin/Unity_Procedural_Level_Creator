using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// How a wall edge is shaped.
    /// </summary>
    public enum WallKind
    {
        Straight,   // standard full-length wall at edge midpoint
        HalfL,      // 2-unit half-wall; mesh extends LEFT of pivot (local -X side)
        HalfR,      // 2-unit half-wall; mesh extends RIGHT of pivot (local +X side)
        Angle,      // diagonal hypotenuse wall placed at the cell center of a Triangle tile
        Concave,    // deferred
        Convex,     // deferred
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
    /// How much of each adjacent wall the corner prefab's arms cover.
    /// Controls whether the straight-wall pass emits walls on edges that
    /// meet at a corner vertex, and whether half-walls are substituted.
    /// </summary>
    public enum CornerArmLength
    {
        /// <summary>Corner arms are 4 units each — corner fully replaces the two adjacent walls.</summary>
        Full,
        /// <summary>
        /// Corner arms are 2 units each — the two adjacent walls are replaced with
        /// HalfL or HalfR variants whose meshes fill the corner-adjacent half of each edge.
        /// Requires rooms at least 3 cells on each axis.
        /// </summary>
        Half,
        /// <summary>Corner has no arms (decorative/column style) — both adjacent walls remain full.</summary>
        Column,
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
        /// World-space centre of the wall piece — the midpoint of the cell edge for cardinal
        /// walls, or the cell center for Angle (hypotenuse) walls.
        /// </summary>
        public Vector3    worldPosition;
        /// <summary>
        /// Rotation such that the wall's local +Z points INTO the room interior.
        /// For Angle walls, local +Z faces INTO the triangle's filled body.
        /// </summary>
        public Quaternion rotation;
        /// <summary>Wall shape kind (Straight, HalfL, HalfR, Angle, or deferred types).</summary>
        public WallKind   kind;
        /// <summary>Vertical tier index.</summary>
        public int        tier;
        /// <summary>
        /// Which cardinal edge of the source cell this wall sits on.
        /// For Angle walls this is CellEdge.North as a sentinel (angle walls have no cardinal edge).
        /// </summary>
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
    /// <para><b>Current scope (Phase 2):</b>
    /// <list type="bullet">
    /// <item><see cref="TileType.Square"/> cells — floor, cardinal walls, outward corners.</item>
    /// <item>Triangle cells (NE/NW/SE/SW) — floor, two cardinal leg walls, one Angle hypotenuse wall.</item>
    /// <item>Tier 0 only — higher tiers are skipped in wall and corner passes.</item>
    /// <item>Straight walls on cardinal cell edges, with optional corner-arm absorption.</item>
    /// <item>Outward 90-degree corners at convex cardinal-wall junctions.</item>
    /// </list>
    /// Quarter-circles, tier stacking, and inward (concave) corners are deferred.
    /// </para>
    /// </summary>
    public static class EdgeSolver
    {
        // Which end of a cell edge a corner vertex sits at.
        // Start = lower grid coordinate end (−X end for N/S edges; −Z end for E/W edges).
        // End   = higher grid coordinate end (+X or +Z).
        private enum EdgeEndpoint { Start, End }

        private const float HalfCell = CellMap.CellSize * 0.5f; // 2f

        // Precomputed wall rotations: local +Z faces INTO the room from each edge.
        private static readonly Quaternion WallRotNorth = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Quaternion WallRotEast  = Quaternion.Euler(0f, 270f, 0f);
        private static readonly Quaternion WallRotSouth = Quaternion.Euler(0f,   0f, 0f);
        private static readonly Quaternion WallRotWest  = Quaternion.Euler(0f,  90f, 0f);

        // Corner rotations — FDP convention:
        //   At rotation 0, the L-shape arms extend toward -X (west) and +Z (north).
        //   Rotating CW by N*90° re-aligns the arms to each room corner's two walls,
        //   pointing INTO the room. Rotations are cardinal-axis aligned (multiples of 90°).
        //
        // Verification table (confirmed empirically 2025-04-20):
        //   Room corner | Arms point into room     | Rotation
        //   SE          | west (-X) and north (+Z) |   0°
        //   SW          | north (+Z) and east (+X) |  90°
        //   NW          | east (+X) and south (-Z) | 180°
        //   NE          | south (-Z) and west (-X) | 270°
        private static readonly Quaternion CornerRotSE = Quaternion.Euler(0f,   0f, 0f);
        private static readonly Quaternion CornerRotSW = Quaternion.Euler(0f,  90f, 0f);
        private static readonly Quaternion CornerRotNW = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Quaternion CornerRotNE = Quaternion.Euler(0f, 270f, 0f);

        /// <summary>
        /// Solves the given <paramref name="map"/> and returns a <see cref="SolveResult"/>
        /// containing floor, wall, and corner placements.
        ///
        /// <para>Never returns null. On a null or empty map returns a SolveResult with
        /// empty placement lists and a single warning string. Does not throw.</para>
        /// </summary>
        /// <param name="map">Source cell map. May be null.</param>
        /// <param name="cornerArms">
        /// How far the corner prefab's arms extend along adjacent edges.
        /// Full suppresses adjacent walls entirely; Half replaces them with HalfL/HalfR
        /// variants at the normal edge-midpoint position; Column leaves walls unchanged.
        /// Half mode requires the map to be at least 3 cells on each axis.
        /// </param>
        public static SolveResult Solve(CellMap map, CornerArmLength cornerArms = CornerArmLength.Full)
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

            if (cornerArms == CornerArmLength.Half && (map.Width < 3 || map.Depth < 3))
            {
                result.warnings.Add(
                    $"Half corner mode requires rooms at least 3 cells on each axis. " +
                    $"Map is {map.Width}x{map.Depth}. No placements emitted.");
                return result;
            }

            // Track which unsupported tile types have already produced a warning.
            var warnedTypes = new HashSet<TileType>();

            // Pass 1 — Floors
            // Emit one FloorPlacement per filled Square cell.
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

            // Pass 2 — Corners (runs BEFORE the wall pass so claim sets are populated first).
            // Only Square cells participate in corner checks — Triangle tile vertices are the
            // "points" of the diamond and do not produce outward 90-degree corner junctions.
            //
            // Claim sets:
            //   fullyClaimedEdges — edges fully suppressed (Full mode).
            //   halfCornerEdges   — edges where one endpoint has a Half corner arm.
            var emittedVertices   = new HashSet<long>();
            var fullyClaimedEdges = new HashSet<long>();
            var halfCornerEdges   = new Dictionary<long, EdgeEndpoint>();

            foreach (var (x, z, cell) in map.EnumerateFilled())
            {
                if (cell.type != TileType.Square || cell.tier != 0) continue;

                Vector3 center = map.CellCenterWorld(x, z);

                // NE vertex: walls on N and E, diagonal at (x+1, z+1).
                // Corner arm sits at +X end of N edge (End) and +Z end of E edge (End).
                if (map.HasWallOnEdge(x, z, CellEdge.North) &&
                    map.HasWallOnEdge(x, z, CellEdge.East)  &&
                    map.GetCell(x + 1, z + 1).IsEmpty)
                {
                    bool emitted = TryEmitCorner(result, emittedVertices,
                        x + 1, z + 1,
                        center + new Vector3( HalfCell, 0f,  HalfCell),
                        CornerRotNE, x, z);
                    if (emitted)
                    {
                        if (cornerArms == CornerArmLength.Full)
                        {
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.North));
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.East));
                        }
                        else if (cornerArms == CornerArmLength.Half)
                        {
                            halfCornerEdges[EdgeKey(x, z, CellEdge.North)] = EdgeEndpoint.End;
                            halfCornerEdges[EdgeKey(x, z, CellEdge.East)]  = EdgeEndpoint.End;
                        }
                    }
                }

                // NW vertex: walls on N and W, diagonal at (x-1, z+1).
                // Corner arm sits at -X end of N edge (Start) and +Z end of W edge (End).
                if (map.HasWallOnEdge(x, z, CellEdge.North) &&
                    map.HasWallOnEdge(x, z, CellEdge.West)  &&
                    map.GetCell(x - 1, z + 1).IsEmpty)
                {
                    bool emitted = TryEmitCorner(result, emittedVertices,
                        x, z + 1,
                        center + new Vector3(-HalfCell, 0f,  HalfCell),
                        CornerRotNW, x, z);
                    if (emitted)
                    {
                        if (cornerArms == CornerArmLength.Full)
                        {
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.North));
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.West));
                        }
                        else if (cornerArms == CornerArmLength.Half)
                        {
                            halfCornerEdges[EdgeKey(x, z, CellEdge.North)] = EdgeEndpoint.Start;
                            halfCornerEdges[EdgeKey(x, z, CellEdge.West)]  = EdgeEndpoint.End;
                        }
                    }
                }

                // SE vertex: walls on S and E, diagonal at (x+1, z-1).
                // Corner arm sits at +X end of S edge (End) and -Z end of E edge (Start).
                if (map.HasWallOnEdge(x, z, CellEdge.South) &&
                    map.HasWallOnEdge(x, z, CellEdge.East)  &&
                    map.GetCell(x + 1, z - 1).IsEmpty)
                {
                    bool emitted = TryEmitCorner(result, emittedVertices,
                        x + 1, z,
                        center + new Vector3( HalfCell, 0f, -HalfCell),
                        CornerRotSE, x, z);
                    if (emitted)
                    {
                        if (cornerArms == CornerArmLength.Full)
                        {
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.South));
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.East));
                        }
                        else if (cornerArms == CornerArmLength.Half)
                        {
                            halfCornerEdges[EdgeKey(x, z, CellEdge.South)] = EdgeEndpoint.End;
                            halfCornerEdges[EdgeKey(x, z, CellEdge.East)]  = EdgeEndpoint.Start;
                        }
                    }
                }

                // SW vertex: walls on S and W, diagonal at (x-1, z-1).
                // Corner arm sits at -X end of S edge (Start) and -Z end of W edge (Start).
                if (map.HasWallOnEdge(x, z, CellEdge.South) &&
                    map.HasWallOnEdge(x, z, CellEdge.West)  &&
                    map.GetCell(x - 1, z - 1).IsEmpty)
                {
                    bool emitted = TryEmitCorner(result, emittedVertices,
                        x, z,
                        center + new Vector3(-HalfCell, 0f, -HalfCell),
                        CornerRotSW, x, z);
                    if (emitted)
                    {
                        if (cornerArms == CornerArmLength.Full)
                        {
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.South));
                            fullyClaimedEdges.Add(EdgeKey(x, z, CellEdge.West));
                        }
                        else if (cornerArms == CornerArmLength.Half)
                        {
                            halfCornerEdges[EdgeKey(x, z, CellEdge.South)] = EdgeEndpoint.Start;
                            halfCornerEdges[EdgeKey(x, z, CellEdge.West)]  = EdgeEndpoint.Start;
                        }
                    }
                }
            }

            // Pass 3 — Cardinal walls (after corners, so claim sets are complete).
            foreach (var (x, z, cell) in map.EnumerateFilled())
            {
                if (cell.type != TileType.Square || cell.tier != 0) continue;

                Vector3 center = map.CellCenterWorld(x, z);

                EmitWall(map, result, fullyClaimedEdges, halfCornerEdges, x, z, center, CellEdge.North);
                EmitWall(map, result, fullyClaimedEdges, halfCornerEdges, x, z, center, CellEdge.East);
                EmitWall(map, result, fullyClaimedEdges, halfCornerEdges, x, z, center, CellEdge.South);
                EmitWall(map, result, fullyClaimedEdges, halfCornerEdges, x, z, center, CellEdge.West);
            }

            return result;
        }

        // Checks claim sets and emits the appropriate wall kind for the given cardinal edge.
        private static void EmitWall(
            CellMap map, SolveResult result,
            HashSet<long> fullyClaimedEdges, Dictionary<long, EdgeEndpoint> halfCornerEdges,
            int x, int z, Vector3 center, CellEdge edge)
        {
            if (!map.HasWallOnEdge(x, z, edge)) return;

            long key = EdgeKey(x, z, edge);

            if (fullyClaimedEdges.Contains(key)) return;

            if (halfCornerEdges.TryGetValue(key, out EdgeEndpoint cornerEnd))
            {
                WallKind kind = HalfKindForCornerEnd(edge, cornerEnd);
                EmitWallAtMidpoint(result, x, z, center, edge, kind);
                return;
            }

            EmitWallAtMidpoint(result, x, z, center, edge, WallKind.Straight);
        }

        // Determines HalfL or HalfR based on which end of the edge the corner sits at.
        //
        // Each wall's local +X direction relative to edge Start/End:
        //   South edge (rotation   0°): local +X = east  = End   → End   → local +X → HalfL
        //   East  edge (rotation 270°): local +X = north = End   → End   → local +X → HalfL
        //   North edge (rotation 180°): local +X = west  = Start → Start → local +X → HalfL
        //   West  edge (rotation  90°): local +X = south = Start → Start → local +X → HalfL
        //
        // Corner at wall's local +X end → HalfL (mesh extends -X, filling gap toward +X).
        // Corner at wall's local -X end → HalfR (mesh extends +X, filling gap toward -X).
        private static WallKind HalfKindForCornerEnd(CellEdge edge, EdgeEndpoint cornerEnd)
        {
            bool cornerAtLocalPlusX;
            switch (edge)
            {
                case CellEdge.South:
                case CellEdge.East:
                    cornerAtLocalPlusX = (cornerEnd == EdgeEndpoint.End);
                    break;
                default: // North, West
                    cornerAtLocalPlusX = (cornerEnd == EdgeEndpoint.Start);
                    break;
            }
            return cornerAtLocalPlusX ? WallKind.HalfL : WallKind.HalfR;
        }

        // Emits a WallPlacement at the cardinal edge midpoint with the given kind.
        // Used for Straight, HalfL, and HalfR — all use the same world position.
        private static void EmitWallAtMidpoint(
            SolveResult result, int x, int z, Vector3 center, CellEdge edge, WallKind kind)
        {
            Vector3    offset;
            Quaternion rotation;

            switch (edge)
            {
                case CellEdge.North:
                    offset   = new Vector3(0f,       0f,  HalfCell);
                    rotation = WallRotNorth;
                    break;
                case CellEdge.East:
                    offset   = new Vector3( HalfCell, 0f, 0f);
                    rotation = WallRotEast;
                    break;
                case CellEdge.South:
                    offset   = new Vector3(0f,       0f, -HalfCell);
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
                kind          = kind,
                tier          = 0,
                edge          = edge,
                gridCoord     = new Vector2Int(x, z),
            });
        }

        // Adds a CornerPlacement for vertex (vx,vz) if not already emitted.
        // Returns true if newly emitted, false if duplicate.
        private static bool TryEmitCorner(
            SolveResult result, HashSet<long> emitted,
            int vx, int vz,
            Vector3 worldPos, Quaternion rotation,
            int anchorX, int anchorZ)
        {
            long key = VertexKey(vx, vz);
            if (!emitted.Add(key)) return false;

            result.corners.Add(new CornerPlacement
            {
                worldPosition = worldPos,
                rotation      = rotation,
                kind          = CornerKind.Outward,
                tier          = 0,
                gridCoord     = new Vector2Int(anchorX, anchorZ),
            });
            return true;
        }

        // Packs two ints into a long for use as a HashSet key.
        private static long VertexKey(int vx, int vz) => ((long)vx << 32) | (uint)vz;

        // Packs (cellX, cellZ, edge) into a long for use as an edge identity key.
        // 20 bits for x (bits 24–43), 20 bits for z (bits 4–23), 4 bits for edge (bits 0–3).
        private static long EdgeKey(int x, int z, CellEdge edge) =>
            ((long)(x & 0xFFFFF) << 24) | ((long)(z & 0xFFFFF) << 4) | (long)edge;
    }
}
