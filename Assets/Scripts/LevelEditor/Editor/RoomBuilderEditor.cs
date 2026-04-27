#if UNITY_EDITOR
using LevelGen;
using UnityEditor;
using UnityEngine;

namespace LevelEditor
{
    /// <summary>
    /// Custom inspector for <see cref="RoomBuilder"/>.
    /// Adds Build / Clear action buttons, a Theme Picker popup when a
    /// <see cref="PieceCatalogue"/> is assigned, a Save Classification section
    /// with conditional category dropdown and folder preview, and Save buttons.
    /// </summary>
    [CustomEditor(typeof(RoomBuilder))]
    public class RoomBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw all default fields EXCEPT the classification ones — those
            // are drawn in the Save Classification section below.
            DrawPropertiesExcluding(serializedObject,
                "m_Script",
                "pieceType",
                "roomCategory",
                "hallCategory");

            var builder = (RoomBuilder)target;

            EditorGUILayout.Space(8f);

            // ── Build / Clear ──────────────────────────────────────────────────
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

            // ── Theme Picker ───────────────────────────────────────────────────
            if (builder.catalogue != null)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Theme Picker", EditorStyles.boldLabel);

                var names = builder.catalogue.GetThemeNames();
                if (names == null || names.Length == 0)
                {
                    EditorGUILayout.HelpBox("No themes defined on this catalogue.", MessageType.Info);
                }
                else
                {
                    var options = new string[names.Length + 1];
                    options[0] = "(none / use direct slots)";
                    System.Array.Copy(names, 0, options, 1, names.Length);

                    int currentIdx = 0;
                    for (int i = 0; i < names.Length; i++)
                        if (names[i] == builder.themeName) { currentIdx = i + 1; break; }

                    int newIdx = EditorGUILayout.Popup("Theme", currentIdx, options);
                    if (newIdx != currentIdx)
                    {
                        Undo.RecordObject(builder, "Change Theme");
                        builder.themeName = (newIdx == 0) ? "" : names[newIdx - 1];
                        EditorUtility.SetDirty(builder);
                    }
                }
            }

            // ── Save Classification ────────────────────────────────────────────
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Save Classification", EditorStyles.boldLabel);

            var typeProp = serializedObject.FindProperty("pieceType");
            EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));

            if (builder.pieceType == PieceType.Room)
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("roomCategory"), new GUIContent("Category"));
            else
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("hallCategory"), new GUIContent("Category"));

            EditorGUILayout.LabelField("Save folder:", builder.ResolveSaveFolder(),
                EditorStyles.miniLabel);

            // ── Save buttons ───────────────────────────────────────────────────
            EditorGUILayout.Space(4f);

            if (GUILayout.Button("Save as RoomPiece", GUILayout.Height(26f)))
                builder.SaveAsRoomPiece();

            if (GUILayout.Button("Save to Custom Path…", GUILayout.Height(22f)))
                builder.SaveToCustomPath();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
