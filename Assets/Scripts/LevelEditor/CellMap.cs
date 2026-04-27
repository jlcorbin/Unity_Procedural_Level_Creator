using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// One cell in the room grid. Plain serializable struct so CellMap can
    /// live in ScriptableObjects / EditorWindow state / room prefabs.
    ///
    /// A cell is a unit of FLOOR PLAN. Tier means "how many wall-heights
    /// above the base plane this cell's floor sits at" (0 = ground, 1 = one
    /// tier up, etc). Cells stack — tier 2 can only exist if tiers 0 and 1
    /// exist at the same (x,z).
    ///
    /// Rotation is stored as a quarter-turn count (0..3) rather than
    /// Quaternion/euler so the data is compact and comparison-safe.
    /// </summary>
    [Serializable]
    public struct Cell : IEquatable<Cell>
    {
        public TileType type;
        public byte     tier;       // 0 = ground, max 3 for v1 (stackable only)
        public byte     rotSteps;   // 0..3 = 0°, 90°, 180°, 270° CW around Y

        public static Cell Empty => default;

        public readonly bool IsEmpty => type == TileType.Empty;

        public readonly Quaternion Rotation =>
            Quaternion.Euler(0f, rotSteps * 90f, 0f);

        /// <summary>
        /// Convenience constructor. Rotation defaults to 0.
        /// </summary>
        public Cell(TileType t, byte tier = 0, byte rotSteps = 0)
        {
            this.type     = t;
            this.tier     = tier;
            this.rotSteps = (byte)(rotSteps & 3);
        }

        public readonly bool Equals(Cell other) =>
            type == other.type && tier == other.tier && rotSteps == other.rotSteps;

        public override readonly bool Equals(object obj) => obj is Cell c && Equals(c);
        public override readonly int GetHashCode() =>
            ((int)type << 16) | ((int)tier << 8) | rotSteps;

        public override readonly string ToString() =>
            IsEmpty ? "empty" : $"{type} t{tier} r{rotSteps * 90}°";
    }

    /// <summary>
    /// A 2D grid of <see cref="Cell"/>s representing a room floor plan.
    ///
    /// The grid has a fixed width/depth (set at construction). Individual cells
    /// default to <see cref="TileType.Empty"/> and must be painted in via
    /// <see cref="SetCell"/> or the ShapeStamp helpers.
    ///
    /// World-space mapping:
    ///   Cell (x, z) center sits at world position
    ///     (x * CellSize - totalWidth/2 + CellSize/2,
    ///      0,
    ///      z * CellSize - totalDepth/2 + CellSize/2)
    ///   so that the origin (0,0,0) is the room's floor-plane center.
    ///
    /// Grid coordinates:
    ///   +X = East, +Z = North. (0,0) is the SW-most cell.
    /// </summary>
    [Serializable]
    public class CellMap
    {
        /// <summary>
        /// World size of one cell on X and Z. Matches the floor tile step
        /// used by the old FDP-based RoomWorkshop (FloorStep = 4).
        /// Change here if you adopt a different floor tile size later.
        /// </summary>
        public const float CellSize = 4f;

        /// <summary>
        /// World height of one tier (vertical stack unit). Matches WallTier
        /// from the old system — a full wall piece is this tall.
        /// </summary>
        public const float TierHeight = 6f;

        /// <summary>Maximum tier count allowed for v1 (stackable only, no floating).</summary>
        public const int MaxTiers = 3;

        [SerializeField] private int _width;
        [SerializeField] private int _depth;

        /// <summary>
        /// Flat cell array indexed by (x + z * width). Each entry stores the
        /// TOP cell at that column — the floor the user actually walks on.
        /// Tier stacking is implicit: a cell with tier=2 means tiers 0 and 1
        /// also exist underneath (but their geometry is only the wall, not
        /// a floor piece).
        /// </summary>
        [SerializeField] private Cell[] _cells;

        public int Width => _width;
        public int Depth => _depth;

        // Doorway set — not serialized; rebuilt at runtime by whoever owns the map.
        // Null-safe via the Doorways property (lazy init after Unity deserialization).
        private HashSet<(int x, int z, CellEdge edge)> _doorways;
        private HashSet<(int x, int z, CellEdge edge)> Doorways =>
            _doorways ??= new HashSet<(int, int, CellEdge)>();

        /// <summary>
        /// Creates an empty grid of the given size. All cells start as Empty.
        /// Width and depth must be at least 1.
        /// </summary>
        public CellMap(int width, int depth)
        {
            if (width  < 1) throw new ArgumentOutOfRangeException(nameof(width),  "must be >= 1");
            if (depth  < 1) throw new ArgumentOutOfRangeException(nameof(depth),  "must be >= 1");
            _width  = width;
            _depth  = depth;
            _cells  = new Cell[width * depth];
        }

        /// <summary>True if (x, z) is inside the grid bounds.</summary>
        public bool InBounds(int x, int z) =>
            x >= 0 && x < _width && z >= 0 && z < _depth;

        public bool InBounds(Vector2Int p) => InBounds(p.x, p.y);

        /// <summary>
        /// Reads the cell at (x, z). Returns <see cref="Cell.Empty"/> for
        /// out-of-bounds coordinates — this keeps neighbor lookups simple
        /// (the "void outside the grid" is just more empty cells).
        /// </summary>
        public Cell GetCell(int x, int z) =>
            InBounds(x, z) ? _cells[x + z * _width] : Cell.Empty;

        public Cell GetCell(Vector2Int p) => GetCell(p.x, p.y);

        /// <summary>
        /// Writes a cell at (x, z). Clamps tier to [0, MaxTiers - 1] and
        /// normalizes rotation to 0..3.
        /// Silently ignores out-of-bounds writes (no exception) so shape
        /// stamps can paint near the edges without bounds checks every call.
        /// </summary>
        public void SetCell(int x, int z, Cell cell)
        {
            if (!InBounds(x, z)) return;

            // Clamp tier
            if (cell.tier >= MaxTiers) cell.tier = MaxTiers - 1;

            // Normalize rotation
            cell.rotSteps = (byte)(cell.rotSteps & 3);

            _cells[x + z * _width] = cell;
        }

        public void SetCell(Vector2Int p, Cell cell) => SetCell(p.x, p.y, cell);

        /// <summary>Clears every cell in the grid and removes all doorway marks.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = Cell.Empty;
            ClearDoorways();
        }

        // ── Doorway authoring ─────────────────────────────────────────────────

        /// <summary>
        /// Marks the given cell edge as a doorway opening. Any wall that would
        /// normally be emitted on this edge is suppressed by <see cref="HasWallOnEdge"/>.
        /// Out-of-bounds coordinates are silently ignored.
        /// </summary>
        public void AddDoorway(int x, int z, CellEdge edge)
        {
            if (!InBounds(x, z)) return;
            Doorways.Add((x, z, edge));
        }

        /// <summary>Returns true if the given cell edge has been marked as a doorway.</summary>
        public bool HasDoorway(int x, int z, CellEdge edge) =>
            Doorways.Contains((x, z, edge));

        /// <summary>Removes all doorway marks from the map.</summary>
        public void ClearDoorways() => Doorways.Clear();

        /// <summary>Enumerates every doorway mark currently on the map.</summary>
        public IEnumerable<(int x, int z, CellEdge edge)> AllDoorways() => Doorways;

        /// <summary>
        /// Returns the cell adjacent to (x,z) across the given edge.
        /// Out-of-bounds neighbors return <see cref="Cell.Empty"/>.
        /// </summary>
        public Cell GetNeighbor(int x, int z, CellEdge edge)
        {
            var step = TileTypeInfo.Step(edge);
            return GetCell(x + step.x, z + step.y);
        }

        /// <summary>
        /// Returns true if cell A (at x,z) has a wall on the given edge
        /// facing an empty/different-height neighbor.
        ///
        /// Rules:
        ///   - If cell A is empty, there is no wall (walls belong to solid cells).
        ///   - If cell A's rotated geometry doesn't OCCUPY this edge, no wall here
        ///     (e.g. the hypotenuse side of a triangle doesn't get a straight wall).
        ///   - If the neighbor is empty, a wall is placed.
        ///   - If the neighbor is at a LOWER tier, a wall is placed (step-down).
        ///   - If the neighbor is at the SAME tier and is solid-on-this-side,
        ///     no wall (interior edge between two cells).
        ///   - If the neighbor is at a HIGHER tier, the neighbor's edge will
        ///     generate the wall instead — this cell does not.
        /// </summary>
        public bool HasWallOnEdge(int x, int z, CellEdge edge)
        {
            if (HasDoorway(x, z, edge)) return false;

            Cell here = GetCell(x, z);
            if (here.IsEmpty) return false;
            if (!TileTypeInfo.OccupiesEdgeRotated(here.type, here.rotSteps, edge))
                return false;

            Cell neighbor = GetNeighbor(x, z, edge);

            // Edge of grid -> neighbor is empty -> wall needed
            if (neighbor.IsEmpty) return true;

            // Lower neighbor -> wall (step-down)
            if (neighbor.tier < here.tier) return true;

            // Higher neighbor -> the higher cell owns that wall, not us
            if (neighbor.tier > here.tier) return false;

            // Same tier: wall needed only if the neighbor's facing edge is NOT occupied.
            // (An empty hypotenuse facing a triangle hypotenuse = diagonal gap, no wall.
            //  A square-to-square interior join = both edges occupied, no wall.)
            CellEdge facingEdge = TileTypeInfo.Opposite(edge);
            bool neighborSolid =
                TileTypeInfo.OccupiesEdgeRotated(neighbor.type, neighbor.rotSteps, facingEdge);

            return !neighborSolid;
        }

        /// <summary>
        /// Translates a grid coordinate to the WORLD-SPACE CENTER of that cell.
        /// Uses the map's own extents so the room is centered on the origin.
        /// </summary>
        public Vector3 CellCenterWorld(int x, int z)
        {
            float totalX = _width * CellSize;
            float totalZ = _depth * CellSize;
            float wx = x * CellSize - totalX * 0.5f + CellSize * 0.5f;
            float wz = z * CellSize - totalZ * 0.5f + CellSize * 0.5f;
            return new Vector3(wx, 0f, wz);
        }

        /// <summary>
        /// Enumerates every NON-EMPTY cell as (x, z, Cell) tuples.
        /// Useful for the builder: it only needs to visit filled cells.
        /// </summary>
        public IEnumerable<(int x, int z, Cell cell)> EnumerateFilled()
        {
            for (int z = 0; z < _depth; z++)
            for (int x = 0; x < _width; x++)
            {
                Cell c = _cells[x + z * _width];
                if (!c.IsEmpty) yield return (x, z, c);
            }
        }

        /// <summary>
        /// Returns the highest tier value present across all filled cells.
        /// Returns 0 if the map is empty or all cells are at tier 0.
        /// </summary>
        public int GetMaxTierUsed()
        {
            int max = 0;
            for (int i = 0; i < _cells.Length; i++)
                if (!_cells[i].IsEmpty && _cells[i].tier > max)
                    max = _cells[i].tier;
            return max;
        }

        /// <summary>
        /// Total count of non-empty cells. O(n) in grid size.
        /// </summary>
        public int FilledCount()
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
                if (!_cells[i].IsEmpty) n++;
            return n;
        }

        /// <summary>
        /// Produces a human-readable ASCII dump of the grid. Useful for
        /// unit tests and for pasting into bug reports. One char per cell,
        /// space = empty, '#' = square, digit = tier (when tier > 0).
        /// North is on top (higher z = earlier lines).
        /// </summary>
        public string ToAscii()
        {
            var sb = new System.Text.StringBuilder();
            for (int z = _depth - 1; z >= 0; z--)
            {
                for (int x = 0; x < _width; x++)
                {
                    Cell c = _cells[x + z * _width];
                    sb.Append(AsciiChar(c));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static char AsciiChar(Cell c)
        {
            if (c.IsEmpty) return '.';
            if (c.tier > 0) return (char)('0' + c.tier);

            return c.type switch
            {
                TileType.Square     => '#',
                TileType.TriangleNE => '◸',  // will likely render as '?' in console
                TileType.TriangleNW => '◹',
                TileType.TriangleSE => '◿',
                TileType.TriangleSW => '◺',
                TileType.QuarterNE  => 'q',
                TileType.QuarterNW  => 'q',
                TileType.QuarterSE  => 'q',
                TileType.QuarterSW  => 'q',
                TileType.Angle      => 'a',
                TileType.Concave    => 'c',
                TileType.Convex     => 'v',
                TileType.Circle     => 'o',
                _                   => '?',
            };
        }
    }
}
