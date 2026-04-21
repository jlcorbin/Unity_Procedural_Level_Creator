#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Smoke-test menu item for <see cref="ShapeStamp"/>. Generates one map per
    /// method and dumps its ASCII representation to the console — confirms all
    /// three methods run without exception and produce plausible output.
    /// </summary>
    public static class ShapeStamp_Test
    {
        [MenuItem("LevelEditor/Tests/Dump Shape Stamps to Console")]
        private static void DumpShapeStamps()
        {
            CellMap rect    = ShapeStamp.Rectangle(5, 3);
            CellMap diamond = ShapeStamp.Diamond(5);
            CellMap circle  = ShapeStamp.Circle(3);

            Debug.Log($"[ShapeStamp] Rectangle(5,3) — {rect.FilledCount()} filled cells\n{rect.ToAscii()}");
            Debug.Log($"[ShapeStamp] Diamond(5) — {diamond.FilledCount()} filled cells\n{diamond.ToAscii()}");
            Debug.Log($"[ShapeStamp] Circle(3) — {circle.FilledCount()} filled cells\n{circle.ToAscii()}");
        }
    }
}
#endif
