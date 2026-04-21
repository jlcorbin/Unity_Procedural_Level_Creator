using UnityEngine;
using UnityEditor;

namespace LevelGen
{
    /// <summary>
    /// Custom Inspector for LevelGenerator.
    /// Adds Generate, Clear, and Random Seed + Generate buttons
    /// so you can preview levels directly in the Editor without entering Play mode.
    /// </summary>
    [CustomEditor(typeof(LevelGenerator))]
    public class LevelGeneratorEditor : Editor
    {
        private bool _showStats = true;

        public override void OnInspectorGUI()
        {
            LevelGenerator gen = (LevelGenerator)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            DrawDivider();

            // ── Generation controls ───────────────────────────────────
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate", GUILayout.Height(32)))
            {
                Undo.RecordObject(gen.gameObject, "Generate Level");
                gen.Generate();
                EditorUtility.SetDirty(gen);
            }

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear", GUILayout.Height(32)))
            {
                Undo.RecordObject(gen.gameObject, "Clear Level");
                gen.ClearLevel();
                EditorUtility.SetDirty(gen);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.6f, 0.7f, 1.0f);
            if (GUILayout.Button("Random Seed + Generate", GUILayout.Height(26)))
            {
                Undo.RecordObject(gen, "Randomize Seed");
                gen.RandomizeSeed();
                gen.Generate();
                EditorUtility.SetDirty(gen);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // ── Stats ─────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            DrawDivider();

            _showStats = EditorGUILayout.Foldout(_showStats, "Last Generation Stats", true);
            if (_showStats)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Last Seed",
                    gen.LastUsedSeed.ToString(),
                    EditorStyles.helpBox);

                EditorGUILayout.LabelField("Rooms Placed",
                    $"{gen.LastRoomsPlaced} / {gen.roomCount}");

                EditorGUILayout.LabelField("Halls Placed",
                    $"{gen.LastHallsPlaced} / {gen.roomCount}");

                EditorGUILayout.LabelField("Total Pieces",
                    gen.PlacedPieces.Count.ToString());

                EditorGUILayout.LabelField("Is Generating",
                    gen.IsGenerating ? "Yes" : "No");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void DrawDivider()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.4f));
            EditorGUILayout.Space(4);
        }
    }
}
