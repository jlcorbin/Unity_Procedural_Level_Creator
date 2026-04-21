#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Custom inspector for <see cref="RoomBuilder"/> that exposes Build and Clear
    /// buttons below the default field inspector.
    /// </summary>
    [CustomEditor(typeof(RoomBuilder))]
    public class RoomBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            RoomBuilder builder = (RoomBuilder)target;

            if (GUILayout.Button("Build", GUILayout.Height(30f)))
            {
                Undo.RecordObject(builder, "RoomBuilder Build");
                builder.Build();
                EditorUtility.SetDirty(builder);
            }

            if (GUILayout.Button("Clear", GUILayout.Height(30f)))
            {
                Undo.RecordObject(builder, "RoomBuilder Clear");
                builder.Clear();
                EditorUtility.SetDirty(builder);
            }
        }
    }
}
#endif
