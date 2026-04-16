using UnityEngine;
using UnityEditor;

namespace LevelGen
{
    /// <summary>
    /// Custom Inspector for LevelGenerator.
    /// Adds Generate, Clear, Save Seed, and Randomize buttons
    /// so you can preview levels directly in the Editor without entering Play mode.
    /// </summary>
    [CustomEditor(typeof(LevelGenerator))]
    public class LevelGeneratorEditor : Editor
    {
        // Foldout state
        private bool _showStats     = true;
        private bool _showSequence  = true;

        // Level name field for saving
        private string _saveLevelName = "";

        public override void OnInspectorGUI()
        {
            LevelGenerator gen = (LevelGenerator)target;

            // Draw all the default serialized fields first
            DrawDefaultInspector();

            // ── Dressing validation ───────────────────────────────────────
            // DrawDefaultInspector already shows the Dressing header and its three
            // fields (dressRoomsOnGenerate, defaultPropCatalogue, dressingTheme).
            // Add a contextual warning here so it appears right below those fields.
            if (gen.dressRoomsOnGenerate && gen.defaultPropCatalogue == null)
            {
                EditorGUILayout.HelpBox(
                    "Dress Rooms On Generate is enabled but no Prop Catalogue is assigned.\n" +
                    "Assign a PropCatalogue in the Dressing section above.",
                    MessageType.Warning);
            }

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

                EditorGUILayout.LabelField("Pieces Placed",
                    gen.PlacedPieces.Count.ToString());

                EditorGUILayout.LabelField("Is Generating",
                    gen.IsGenerating ? "Yes" : "No");

                EditorGUI.indentLevel--;
            }

            // ── Save seed ─────────────────────────────────────────────
            EditorGUILayout.Space(6);
            DrawDivider();

            _showSequence = EditorGUILayout.Foldout(_showSequence, "Save to Sequence", true);
            if (_showSequence)
            {
                EditorGUI.indentLevel++;

                _saveLevelName = EditorGUILayout.TextField("Level Name", _saveLevelName);

                EditorGUILayout.Space(4);

                GUI.backgroundColor = new Color(1.0f, 0.85f, 0.4f);
                if (GUILayout.Button("Save Current Seed to Sequence"))
                {
                    if (gen.levelSequence == null)
                    {
                        EditorUtility.DisplayDialog(
                            "No Sequence Assigned",
                            "Assign a LevelSequence asset in the inspector first.\n\n" +
                            "Create one via: Assets → Create → LevelGen → Level Sequence",
                            "OK");
                    }
                    else
                    {
                        gen.SaveCurrentSeedToSequence(_saveLevelName);
                        EditorUtility.SetDirty(gen.levelSequence);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[Editor] Saved seed {gen.LastUsedSeed} to sequence.");
                    }
                }
                GUI.backgroundColor = Color.white;

                // Quick copy seed to clipboard
                if (GUILayout.Button("Copy Seed to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = gen.LastUsedSeed.ToString();
                    Debug.Log($"[Editor] Copied seed {gen.LastUsedSeed} to clipboard.");
                }

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
