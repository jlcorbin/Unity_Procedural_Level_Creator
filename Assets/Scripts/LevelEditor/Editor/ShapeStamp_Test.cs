#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Smoke-test menu item for <see cref="ShapeStamp"/>. Generates a Rectangle
    /// map and dumps its ASCII representation to the console — confirms the
    /// active V2 shape runs without exception and produces plausible output.
    /// (Diamond/Circle moved to Assets/Scripts/Experimental/ behind #if FALSE.)
    /// </summary>
    public static class ShapeStamp_Test
    {
        [MenuItem("LevelEditor/Tests/Dump Shape Stamps to Console")]
        private static void DumpShapeStamps()
        {
            CellMap rect = ShapeStamp.Rectangle(5, 3);

            Debug.Log($"[ShapeStamp] Rectangle(5,3) — {rect.FilledCount()} filled cells\n{rect.ToAscii()}");
        }
    }
}
#endif
