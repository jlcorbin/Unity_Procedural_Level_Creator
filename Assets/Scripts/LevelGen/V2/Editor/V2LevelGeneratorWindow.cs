#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LevelGen.V2.Editor
{
    public class V2LevelGeneratorWindow : EditorWindow
    {
        LevelGenSettings _settings = new LevelGenSettings();
        Vector2          _scroll;

        // ── Entry point ───────────────────────────────────────────────────────

        [MenuItem("LevelGen/V2 Level Generator")]
        public static void Open()
        {
            var win = GetWindow<V2LevelGeneratorWindow>("V2 Level Generator");
            win.minSize = new Vector2(380, 600);
        }

        // ── Main draw ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawOutputSection();
            DrawSourceSection();
            DrawRoomBudgetSection();
            DrawHallSizingSection();
            DrawLayoutSection();
            DrawDifficultySection();
            DrawReproducibilitySection();
            EditorGUILayout.Space(12);
            DrawGenerateButton();

            EditorGUILayout.EndScrollView();
        }

        // ── Sections ──────────────────────────────────────────────────────────

        void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            _settings.sceneName = EditorGUILayout.TextField("Scene name", _settings.sceneName);

            EditorGUILayout.BeginHorizontal();
            _settings.outputFolder = EditorGUILayout.TextField("Output folder", _settings.outputFolder);
            if (GUILayout.Button("…", GUILayout.Width(28)))
                _settings.outputFolder = PickFolderRelativeToAssets(_settings.outputFolder);
            EditorGUILayout.EndHorizontal();

            _settings.saveToSceneFile = EditorGUILayout.Toggle(
                new GUIContent("Save to scene file",
                    "When ON, generation writes a .unity scene file to " +
                    "outputFolder/sceneName.unity. When OFF, the generated " +
                    "level is dropped into the currently open scene."),
                _settings.saveToSceneFile);

            EditorGUILayout.Space(8);
        }

        void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            _settings.catalogue = (PieceCatalogue)EditorGUILayout.ObjectField(
                "Catalogue", _settings.catalogue, typeof(PieceCatalogue), false);

            if (_settings.catalogue == null)
            {
                EditorGUILayout.HelpBox("Assign a catalogue to enable themes.", MessageType.Info);
            }
            else
            {
                string[] catalogueThemes = _settings.catalogue.GetThemeNames();
                string[] allOptions      = new string[catalogueThemes.Length + 1];
                allOptions[0] = "(none)";
                System.Array.Copy(catalogueThemes, 0, allOptions, 1, catalogueThemes.Length);

                int currentIndex = 0;
                if (!string.IsNullOrEmpty(_settings.themeName))
                {
                    for (int i = 0; i < catalogueThemes.Length; i++)
                    {
                        if (catalogueThemes[i] == _settings.themeName)
                        {
                            currentIndex = i + 1;
                            break;
                        }
                    }
                }

                int newIndex = EditorGUILayout.Popup("Theme", currentIndex, allOptions);
                _settings.themeName = (newIndex == 0) ? "" : allOptions[newIndex];
            }

            EditorGUILayout.Space(8);
        }

        void DrawRoomBudgetSection()
        {
            EditorGUILayout.LabelField("Room Budget", EditorStyles.boldLabel);

            _settings.starterCount  = Mathf.Clamp(EditorGUILayout.IntField("Starter",  _settings.starterCount),  0,  1);
            _settings.bossCount     = Mathf.Clamp(EditorGUILayout.IntField("Boss",      _settings.bossCount),     0,  1);
            _settings.smallCount    = Mathf.Clamp(EditorGUILayout.IntField("Small",     _settings.smallCount),    0, 50);
            _settings.mediumCount   = Mathf.Clamp(EditorGUILayout.IntField("Medium",    _settings.mediumCount),   0, 50);
            _settings.largeCount    = Mathf.Clamp(EditorGUILayout.IntField("Large",     _settings.largeCount),    0, 50);
            _settings.specialCount  = Mathf.Clamp(EditorGUILayout.IntField("Special",   _settings.specialCount),  0, 50);

            EditorGUILayout.LabelField(
                $"Total rooms: {_settings.TotalRoomCount},  Spine length: {_settings.SpineLength}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
        }

        void DrawHallSizingSection()
        {
            EditorGUILayout.LabelField("Hall Sizing", EditorStyles.boldLabel);

            _settings.spineHallSize  = (HallCategory)EditorGUILayout.EnumPopup("Spine hall size",  _settings.spineHallSize);
            _settings.branchHallSize = (HallCategory)EditorGUILayout.EnumPopup("Branch hall size", _settings.branchHallSize);

            EditorGUILayout.Space(8);
        }

        void DrawLayoutSection()
        {
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);

            _settings.layoutStyle = (LayoutStyle)EditorGUILayout.EnumPopup("Layout style", _settings.layoutStyle);

            if (_settings.layoutStyle != LayoutStyle.LinearWithBranches)
            {
                EditorGUILayout.HelpBox(
                    "Only Linear-with-branches is implemented this phase. Other styles are stubs.",
                    MessageType.Warning);
            }

            int poolSize = _settings.smallCount + _settings.mediumCount
                         + _settings.largeCount + _settings.specialCount;
            int rawBranch = EditorGUILayout.IntField("Branch slot count", _settings.branchSlotCount);
            _settings.branchSlotCount = Mathf.Clamp(rawBranch, 0, poolSize);

            EditorGUILayout.Space(8);
        }

        void DrawDifficultySection()
        {
            EditorGUILayout.LabelField("Difficulty Signals", EditorStyles.boldLabel);

            _settings.branchingFactor = EditorGUILayout.Slider(
                "Branching factor", _settings.branchingFactor, 0f, 1f);

            _settings.deadEndCount    = Mathf.Clamp(EditorGUILayout.IntField("Dead-end count",    _settings.deadEndCount),    0, 50);
            _settings.secretRoomCount = Mathf.Clamp(EditorGUILayout.IntField("Secret-room count", _settings.secretRoomCount), 0, 10);

            EditorGUILayout.HelpBox(
                "Difficulty signals are recorded but not yet implemented.",
                MessageType.Info);

            EditorGUILayout.Space(8);
        }

        void DrawReproducibilitySection()
        {
            EditorGUILayout.LabelField("Reproducibility", EditorStyles.boldLabel);

            _settings.seed = EditorGUILayout.IntField("Seed (0 = random)", _settings.seed);

            EditorGUILayout.Space(8);
        }

        void DrawGenerateButton()
        {
            bool valid = TryValidate(out var errors);

            foreach (string err in errors)
                EditorGUILayout.HelpBox(err, MessageType.Error);

            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(!valid);
            if (GUILayout.Button("Generate", GUILayout.Height(40)))
                OnGenerate();
            EditorGUI.EndDisabledGroup();
        }

        // ── Validation ────────────────────────────────────────────────────────

        bool TryValidate(out List<string> errors)
        {
            errors = new List<string>();

            if (_settings.catalogue == null)
                errors.Add("Catalogue is required.");

            // sceneName / outputFolder only required when actually saving a .unity file.
            // When saveToSceneFile is off, the manifest still writes (with a default
            // folder + seed-based filename fallback handled by the generator).
            if (_settings.saveToSceneFile)
            {
                if (string.IsNullOrWhiteSpace(_settings.sceneName))
                    errors.Add("Scene name is required when 'Save to scene file' is on.");

                if (string.IsNullOrEmpty(_settings.outputFolder)
                    || !_settings.outputFolder.StartsWith("Assets/"))
                    errors.Add("Output folder must start with \"Assets/\" when 'Save to scene file' is on.");
            }

            if (_settings.TotalRoomCount < 2)
                errors.Add("Total room count must be at least 2 (Starter + Boss minimum).");

            if (_settings.starterCount != 1)
                errors.Add("Exactly 1 Starter room is required.");

            if (_settings.bossCount != 1)
                errors.Add("Exactly 1 Boss room is required.");

            int pool = _settings.smallCount + _settings.mediumCount
                     + _settings.largeCount + _settings.specialCount;
            if (_settings.branchSlotCount > pool)
                errors.Add("Branch slot count exceeds combined Small+Medium+Large+Special pool size.");

            if (!string.IsNullOrEmpty(_settings.themeName) && _settings.catalogue != null
                && _settings.catalogue.GetTheme(_settings.themeName) == null)
                errors.Add($"Theme \"{_settings.themeName}\" not found in catalogue.");

            return errors.Count == 0;
        }

        bool IsValid() => TryValidate(out _);

        // ── Generate ──────────────────────────────────────────────────────────

        void OnGenerate()
        {
            var result = V2LevelGenerator.Generate(_settings);
            if (result.Success)
            {
                Debug.Log($"[V2 Gen] Success — seed={result.Seed}, " +
                          $"rooms={result.RoomsPlaced}, halls={result.HallsPlaced}, " +
                          $"branches={result.BranchesPlaced}/{result.BranchesRequested}, " +
                          $"backtracks={result.BacktrackCount}, " +
                          $"{result.ElapsedSeconds:F2}s\n" +
                          $"  Manifest: {result.ManifestPath}\n" +
                          $"  Scene:    {result.ScenePath ?? "(left in active scene)"}");

                if (result.BranchesPlaced < result.BranchesRequested)
                {
                    Debug.LogWarning($"[V2 Gen] Only {result.BranchesPlaced}/{result.BranchesRequested} " +
                                     $"branches placed — see warnings above.");
                }

                if (!string.IsNullOrEmpty(result.ScenePath))
                {
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(result.ScenePath);
                    if (sceneAsset != null)
                        EditorGUIUtility.PingObject(sceneAsset);
                }
                else if (result.Root != null)
                {
                    Selection.activeGameObject = result.Root;
                }
            }
            else
            {
                Debug.LogError($"[V2 Gen] Failed: {result.FailureReason}");
                EditorUtility.DisplayDialog("Generation failed",
                    result.FailureReason, "OK");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        string PickFolderRelativeToAssets(string current)
        {
            string startPath = string.IsNullOrEmpty(current)
                ? Application.dataPath
                : current;

            string abs = EditorUtility.OpenFolderPanel("Output folder", startPath, "");

            if (string.IsNullOrEmpty(abs))
                return current;

            if (!abs.StartsWith(Application.dataPath))
            {
                EditorUtility.DisplayDialog(
                    "Invalid folder",
                    "Folder must be under Assets/.",
                    "OK");
                return current;
            }

            return "Assets" + abs.Substring(Application.dataPath.Length);
        }
    }
}
#endif
