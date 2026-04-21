using System;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Static utility that stamps pre-populated <see cref="CellMap"/>s for common
    /// geometric room shapes. All methods produce floor cells only — no wall, corner,
    /// or prefab logic is applied here.
    ///
    /// Invalid inputs are clamped to the nearest valid value and logged via
    /// <see cref="Debug.LogWarning"/>. Methods never throw.
    /// </summary>
    public static class ShapeStamp
    {
        /// <summary>
        /// Returns a new <see cref="CellMap"/> filled entirely with
        /// <see cref="TileType.Square"/> cells at tier 0, rotation 0.
        /// </summary>
        /// <param name="width">Grid width in cells. Clamped to a minimum of 1.</param>
        /// <param name="depth">Grid depth in cells. Clamped to a minimum of 1.</param>
        public static CellMap Rectangle(int width, int depth)
        {
            if (width < 1)
            {
                Debug.LogWarning($"ShapeStamp.Rectangle: width {width} clamped to 1.");
                width = 1;
            }
            if (depth < 1)
            {
                Debug.LogWarning($"ShapeStamp.Rectangle: depth {depth} clamped to 1.");
                depth = 1;
            }

            var map = new CellMap(width, depth);
            var sq  = new Cell(TileType.Square, 0, 0);
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
                map.SetCell(x, z, sq);

            return map;
        }

        /// <summary>
        /// Returns a new <see cref="CellMap"/> of size × size with a diamond
        /// inscribed inside it. Cells within Manhattan radius of the center become
        /// <see cref="TileType.Square"/>; diagonal-edge border cells use the
        /// appropriate <c>Triangle</c> tile with its hypotenuse facing outward.
        ///
        /// Triangle assignment by quadrant (hypotenuse faces away from center):
        /// <list type="bullet">
        /// <item>NE-facing cut → <see cref="TileType.TriangleSW"/></item>
        /// <item>NW-facing cut → <see cref="TileType.TriangleSE"/></item>
        /// <item>SE-facing cut → <see cref="TileType.TriangleNW"/></item>
        /// <item>SW-facing cut → <see cref="TileType.TriangleNE"/></item>
        /// </list>
        /// </summary>
        /// <param name="size">Side length of the grid. Clamped to a minimum of 3.</param>
        public static CellMap Diamond(int size)
        {
            if (size < 3)
            {
                Debug.LogWarning($"ShapeStamp.Diamond: size {size} clamped to 3.");
                size = 3;
            }

            var map = new CellMap(size, size);

            // Float center and radius support both odd (integer center) and
            // even (half-unit center) sizes, keeping the shape symmetric.
            float cx     = (size - 1) * 0.5f;
            float cz     = (size - 1) * 0.5f;
            float radius = (size - 1) * 0.5f;
            const float eps = 0.001f;

            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                float dx       = x - cx;
                float dz       = z - cz;
                float manhattan = (dx < 0 ? -dx : dx) + (dz < 0 ? -dz : dz);

                if (manhattan > radius + eps) continue; // outside diamond — leave Empty

                TileType type;
                bool onBoundary = manhattan >= radius - eps;
                bool onAxis     = (dx < eps && dx > -eps) || (dz < eps && dz > -eps);

                if (onBoundary && !onAxis)
                {
                    // Diagonal corner: hypotenuse faces outward toward empty space.
                    if      (dx > 0 && dz > 0) type = TileType.TriangleSW; // NE-facing cut
                    else if (dx < 0 && dz > 0) type = TileType.TriangleSE; // NW-facing cut
                    else if (dx > 0)            type = TileType.TriangleNW; // SE-facing cut
                    else                        type = TileType.TriangleNE; // SW-facing cut
                }
                else
                {
                    type = TileType.Square;
                }

                map.SetCell(x, z, new Cell(type, 0, 0));
            }

            return map;
        }

        /// <summary>
        /// Returns a new <see cref="CellMap"/> of (2*radius+1) × (2*radius+1) with
        /// a rasterized filled circle. Cells firmly inside the circle become
        /// <see cref="TileType.Square"/>; cells in the boundary ring (within 0.5
        /// cell-units of the exact radius) become the appropriate
        /// <c>Quarter</c> tile oriented so the curve faces the center. Axis-aligned
        /// boundary cells are forced to <see cref="TileType.Square"/>.
        ///
        /// Quarter assignment by quadrant (curve faces center):
        /// <list type="bullet">
        /// <item>NE quadrant → <see cref="TileType.QuarterSW"/></item>
        /// <item>NW quadrant → <see cref="TileType.QuarterSE"/></item>
        /// <item>SE quadrant → <see cref="TileType.QuarterNW"/></item>
        /// <item>SW quadrant → <see cref="TileType.QuarterNE"/></item>
        /// </list>
        /// </summary>
        /// <param name="radius">Circle radius in cells. Clamped to a minimum of 1.</param>
        public static CellMap Circle(int radius)
        {
            if (radius < 1)
            {
                Debug.LogWarning($"ShapeStamp.Circle: radius {radius} clamped to 1.");
                radius = 1;
            }

            int size = 2 * radius + 1;
            var map = new CellMap(size, size);

            int cx = radius;
            int cz = radius;

            // Compare squared distances to avoid sqrt in the hot loop.
            float innerSq = (radius - 0.5f) * (radius - 0.5f);
            float outerSq = (radius + 0.5f) * (radius + 0.5f);

            for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                int dx     = x - cx;
                int dz     = z - cz;
                float distSq = dx * dx + dz * dz;

                if (distSq > outerSq) continue; // outside circle — leave Empty

                TileType type;
                if (distSq <= innerSq || dx == 0 || dz == 0)
                {
                    // Firmly inside the circle, or on a cardinal axis — always Square.
                    type = TileType.Square;
                }
                else
                {
                    // Boundary ring, off-axis: curve faces toward the center.
                    if      (dx > 0 && dz > 0) type = TileType.QuarterSW; // NE quadrant
                    else if (dx < 0 && dz > 0) type = TileType.QuarterSE; // NW quadrant
                    else if (dx > 0)            type = TileType.QuarterNW; // SE quadrant
                    else                        type = TileType.QuarterNE; // SW quadrant
                }

                map.SetCell(x, z, new Cell(type, 0, 0));
            }

            return map;
        }
    }
}
