using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Identifies what shape sits in a single grid cell.
    /// Each tile type has a known set of occupied edges (which of N/S/E/W edges
    /// of the cell are considered "solid" — walls are placed on edges that
    /// transition from solid to empty).
    ///
    /// Rotation is stored separately on the Cell (0°, 90°, 180°, 270°).
    /// The enum values themselves describe the BASE (un-rotated) geometry —
    /// e.g. TriangleSW always refers to a triangle filling the south-west half
    /// of the cell at rotation 0. To rotate, apply Cell.rotation to the cell.
    /// </summary>
    public enum TileType : byte
    {
        Empty       = 0,
        Square      = 1,

        /// <summary>Right-triangle filling the SW half of the cell (hypotenuse runs NE-SW).</summary>
        TriangleSW  = 2,
        /// <summary>Right-triangle filling the SE half of the cell (hypotenuse runs NW-SE).</summary>
        TriangleSE  = 3,
        /// <summary>Right-triangle filling the NE half of the cell (hypotenuse runs NE-SW).</summary>
        TriangleNE  = 4,
        /// <summary>Right-triangle filling the NW half of the cell (hypotenuse runs NW-SE).</summary>
        TriangleNW  = 5,

        /// <summary>Quarter-circle with the flat sides on S and W edges.</summary>
        QuarterSW   = 6,
        /// <summary>Quarter-circle with the flat sides on S and E edges.</summary>
        QuarterSE   = 7,
        /// <summary>Quarter-circle with the flat sides on N and E edges.</summary>
        QuarterNE   = 8,
        /// <summary>Quarter-circle with the flat sides on N and W edges.</summary>
        QuarterNW   = 9,

        /// <summary>Inner corner piece (angle) — fills most of the cell, hypotenuse is concave.</summary>
        Angle       = 10,
        /// <summary>Concave piece — curved hypotenuse bowing INTO the cell.</summary>
        Concave     = 11,
        /// <summary>Convex piece — curved hypotenuse bowing OUT of the cell.</summary>
        Convex      = 12,
        /// <summary>Full circular floor tile (freestanding disc shape).</summary>
        Circle      = 13,
    }

    /// <summary>
    /// Which edge of a grid cell a wall or neighbor lookup refers to.
    /// Kept as a simple 0–3 int so it can be cast cheaply.
    /// N = +Z, S = -Z, E = +X, W = -X (Unity's world axis convention).
    /// </summary>
    public enum CellEdge : byte
    {
        North = 0,
        East  = 1,
        South = 2,
        West  = 3,
    }

    /// <summary>
    /// Static lookup tables and helpers describing how each <see cref="TileType"/>
    /// interacts with its neighbors.
    ///
    /// A tile's "edge occupancy" is a 4-bit mask of which cell edges the tile
    /// actually reaches. A square fills all four edges. A triangle fills two
    /// (its legs) and leaves the hypotenuse side open. Quarter-circles are the
    /// same as triangles for occupancy purposes — the curve starts behind the
    /// hypotenuse edge, not on it.
    ///
    /// The <see cref="EdgeSolver"/> uses this mask to decide which edges need
    /// wall segments placed on them.
    /// </summary>
    public static class TileTypeInfo
    {
        /// <summary>
        /// Base edge occupancy for each tile type at rotation 0.
        /// Bit 0 = North, Bit 1 = East, Bit 2 = South, Bit 3 = West.
        /// </summary>
        private static readonly byte[] BaseEdgeMask = new byte[TileTypeCount];
        // Filled entirely by the static constructor below — do not add literals here.

        /// <summary>Number of distinct <see cref="TileType"/> enum values (including Empty).</summary>
        public const int TileTypeCount = 14;

        static TileTypeInfo()
        {
            // Rebuild the mask table from a clearer source-of-truth.
            // Bits: N=1, E=2, S=4, W=8.
            BaseEdgeMask[(int)TileType.Empty]      = 0;
            BaseEdgeMask[(int)TileType.Square]     = 1 | 2 | 4 | 8;

            BaseEdgeMask[(int)TileType.TriangleSW] = 4 | 8;          // S, W
            BaseEdgeMask[(int)TileType.TriangleSE] = 4 | 2;          // S, E
            BaseEdgeMask[(int)TileType.TriangleNE] = 1 | 2;          // N, E
            BaseEdgeMask[(int)TileType.TriangleNW] = 1 | 8;          // N, W

            BaseEdgeMask[(int)TileType.QuarterSW]  = 4 | 8;
            BaseEdgeMask[(int)TileType.QuarterSE]  = 4 | 2;
            BaseEdgeMask[(int)TileType.QuarterNE]  = 1 | 2;
            BaseEdgeMask[(int)TileType.QuarterNW]  = 1 | 8;

            BaseEdgeMask[(int)TileType.Angle]      = 1 | 2 | 4 | 8;
            BaseEdgeMask[(int)TileType.Concave]    = 1 | 2 | 4 | 8;
            BaseEdgeMask[(int)TileType.Convex]     = 1 | 2 | 4 | 8;

            BaseEdgeMask[(int)TileType.Circle]     = 0;               // freestanding
        }

        /// <summary>
        /// Returns true if the given tile type reaches the given edge at rotation 0.
        /// To check at an arbitrary rotation, pass the *pre-rotated* edge to this
        /// method, OR use <see cref="OccupiesEdgeRotated"/>.
        /// </summary>
        public static bool OccupiesEdge(TileType type, CellEdge edge)
        {
            int mask = BaseEdgeMask[(int)type];
            int bit  = 1 << (int)edge; // North=1, East=2, South=4, West=8
            return (mask & bit) != 0;
        }

        /// <summary>
        /// Returns true if the given tile, when rotated by <paramref name="rotSteps"/>
        /// quarter-turns clockwise (0–3), reaches the given edge in WORLD space.
        ///
        /// A 90° CW rotation maps base edges N→E, E→S, S→W, W→N.
        /// So asking "does this rotated tile occupy world-east?" becomes
        /// "does the base tile occupy the edge that was rotated INTO world-east?",
        /// which is the edge (world-east - rotSteps) mod 4.
        /// </summary>
        public static bool OccupiesEdgeRotated(TileType type, int rotSteps, CellEdge worldEdge)
        {
            rotSteps = ((rotSteps % 4) + 4) % 4; // normalize to 0..3
            int baseEdge = ((int)worldEdge - rotSteps + 4) % 4;
            int mask = BaseEdgeMask[(int)type];
            int bit  = 1 << baseEdge;
            return (mask & bit) != 0;
        }

        /// <summary>
        /// Returns true if this tile type has a curved (non-straight) edge.
        /// Used by the solver to decide between straight wall vs curved wall prefab.
        /// </summary>
        public static bool HasCurvedEdge(TileType type)
        {
            switch (type)
            {
                case TileType.QuarterNE:
                case TileType.QuarterNW:
                case TileType.QuarterSE:
                case TileType.QuarterSW:
                case TileType.Concave:
                case TileType.Convex:
                case TileType.Circle:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns true if this tile has a diagonal hypotenuse (triangle-like).
        /// Diagonal edges need either a rotated straight wall or a special diagonal prefab.
        /// </summary>
        public static bool HasDiagonalEdge(TileType type)
        {
            switch (type)
            {
                case TileType.TriangleNE:
                case TileType.TriangleNW:
                case TileType.TriangleSE:
                case TileType.TriangleSW:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the opposite edge (N↔S, E↔W). Useful when looking up the
        /// neighbor's facing edge to decide if a wall is needed between two cells.
        /// </summary>
        public static CellEdge Opposite(CellEdge edge) => edge switch
        {
            CellEdge.North => CellEdge.South,
            CellEdge.South => CellEdge.North,
            CellEdge.East  => CellEdge.West,
            _              => CellEdge.East,
        };

        /// <summary>
        /// Converts a CellEdge to a 2D step vector (Δx, Δz) in cell-grid coordinates.
        /// Used by CellMap to find the neighbor cell across a given edge.
        /// </summary>
        public static Vector2Int Step(CellEdge edge) => edge switch
        {
            CellEdge.North => new Vector2Int( 0,  1),
            CellEdge.East  => new Vector2Int( 1,  0),
            CellEdge.South => new Vector2Int( 0, -1),
            _              => new Vector2Int(-1,  0),
        };
    }
}
