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
    ///
    /// Declared <c>partial</c> so dormant shapes can live in
    /// <c>Assets/Scripts/Experimental/</c> behind <c>#if FALSE</c> without leaving
    /// the active class. See Documentation/V1_CLEANUP_AUDIT.md Section B.
    /// </summary>
    public static partial class ShapeStamp
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
    }
}
