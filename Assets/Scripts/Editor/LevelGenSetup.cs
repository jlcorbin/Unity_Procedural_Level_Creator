using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// One-time project setup utilities.
    ///
    /// Creates the three pipeline scenes and default ScriptableObject assets
    /// so the project is ready to use without manual file creation.
    ///
    /// Menu: LevelGen ▶ Setup
    ///
    /// Scenes:
    ///   Assets/Scenes/RoomWorkshop.unity    — open Room Workshop window here
    ///   Assets/Scenes/LevelGenerator.unity  — LevelGenerator component pre-added
    ///
    /// Assets:
    ///   Assets/PropCatalogues/DefaultPieceCatalogue.asset
    ///   Assets/RoomPresets/DefaultRoomPresetLibrary.asset
    /// </summary>
    public static class LevelGenSetup
    {
        // ── Paths ─────────────────────────────────────────────────────────────

        private const string ScenesFolder          = "Assets/Scenes";
        private const string WorkshopScenePath     = ScenesFolder + "/RoomWorkshop.unity";
        private const string GeneratorScenePath    = ScenesFolder + "/LevelGenerator.unity";
        private const string PropCataloguesFolder  = "Assets/PropCatalogues";
        private const string RoomPresetsFolder     = "Assets/RoomPresets";
        private const string PieceCatPath          = PropCataloguesFolder + "/DefaultPieceCatalogue.asset";
        private const string PresetLibPath         = RoomPresetsFolder   + "/DefaultRoomPresetLibrary.asset";

        // ── Combined entry point ──────────────────────────────────────────────

        /// <summary>
        /// Runs all setup steps in sequence.
        /// Safe to call multiple times — skips anything that already exists.
        /// </summary>
        [MenuItem("LevelGen/Setup/Create All (Scenes + Assets)")]
        public static void CreateAll()
        {
            CreateDefaultPieceCatalogue();
            CreateDefaultRoomPresetLibrary();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Scene creation prompts to save the current scene; run last
            CreateRoomWorkshopScene();
            CreateLevelGeneratorScene();

            Debug.Log("[LevelGen] Setup complete. Use LevelGen ▶ Room Workshop to start building rooms.");
        }

        // ── Scene creation ────────────────────────────────────────────────────

        /// <summary>
        /// Creates Assets/Scenes/RoomWorkshop.unity — an empty scene intended to be
        /// the environment for the Room Workshop EditorWindow.
        ///
        /// After loading this scene, open it via: LevelGen ▶ Room Workshop
        /// </summary>
        [MenuItem("LevelGen/Setup/Create RoomWorkshop Scene")]
        public static void CreateRoomWorkshopScene()
        {
            if (!ConfirmOverwriteIfExists<SceneAsset>(WorkshopScenePath, "scene")) return;

            EnsureDirectory(ScenesFolder);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Add a helper note as a disabled GameObject so the intent is clear
            var guide = new GameObject("[Open LevelGen ► Room Workshop to build rooms]");
            guide.SetActive(false);

            bool saved = EditorSceneManager.SaveScene(scene, WorkshopScenePath);
            AssetDatabase.Refresh();

            if (saved)
                Debug.Log($"[LevelGen] Created scene: {WorkshopScenePath}");
            else
                Debug.LogError($"[LevelGen] Failed to save scene: {WorkshopScenePath}");
        }

        /// <summary>
        /// Creates Assets/Scenes/LevelGenerator.unity with a LevelGenerator
        /// GameObject pre-configured so you can assign prefabs and generate immediately.
        /// </summary>
        [MenuItem("LevelGen/Setup/Create LevelGenerator Scene")]
        public static void CreateLevelGeneratorScene()
        {
            if (!ConfirmOverwriteIfExists<SceneAsset>(GeneratorScenePath, "scene")) return;

            EnsureDirectory(ScenesFolder);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Pre-add a LevelGenerator GameObject so the scene is ready to use
            var go = new GameObject("LevelGenerator");
            go.AddComponent<LevelGenerator>();

            bool saved = EditorSceneManager.SaveScene(scene, GeneratorScenePath);
            AssetDatabase.Refresh();

            if (saved)
                Debug.Log($"[LevelGen] Created scene: {GeneratorScenePath}");
            else
                Debug.LogError($"[LevelGen] Failed to save scene: {GeneratorScenePath}");
        }

        // ── Asset creation ────────────────────────────────────────────────────

        /// <summary>
        /// Creates a blank PieceCatalogue asset at Assets/PropCatalogues/DefaultPieceCatalogue.asset.
        /// Use the PieceCatalogue inspector's Auto-Populate button to fill it from your asset pack.
        /// </summary>
        [MenuItem("LevelGen/Setup/Create Default PieceCatalogue")]
        public static void CreateDefaultPieceCatalogue()
        {
            if (!ConfirmOverwriteIfExists<PieceCatalogue>(PieceCatPath, "asset")) return;

            EnsureDirectory(PropCataloguesFolder);

            var asset = ScriptableObject.CreateInstance<PieceCatalogue>();
            AssetDatabase.CreateAsset(asset, PieceCatPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[LevelGen] Created PieceCatalogue: {PieceCatPath}\n" +
                      "Use the Auto-Populate button in its Inspector to add prefabs.");
        }

        /// <summary>
        /// Creates a blank RoomPresetLibrary asset at Assets/RoomPresets/DefaultRoomPresetLibrary.asset.
        /// Drag saved RoomPreset assets into its list to make them available to the generator.
        /// </summary>
        [MenuItem("LevelGen/Setup/Create Default RoomPresetLibrary")]
        public static void CreateDefaultRoomPresetLibrary()
        {
            if (!ConfirmOverwriteIfExists<RoomPresetLibrary>(PresetLibPath, "asset")) return;

            EnsureDirectory(RoomPresetsFolder);

            var asset = ScriptableObject.CreateInstance<RoomPresetLibrary>();
            AssetDatabase.CreateAsset(asset, PresetLibPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[LevelGen] Created RoomPresetLibrary: {PresetLibPath}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the asset does not yet exist, or if the user confirms overwrite.
        /// Returning false means the caller should abort without modifying anything.
        /// </summary>
        private static bool ConfirmOverwriteIfExists<T>(string path, string kind) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null) return true;

            return EditorUtility.DisplayDialog(
                $"{kind} already exists",
                $"{path} already exists.\n\nSkip (keep existing) or overwrite?",
                "Overwrite", "Skip");
        }

        /// <summary>
        /// Creates a folder (and its parent chain) if it does not already exist.
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            string folder = Path.GetFileName(path);

            // Recurse to ensure parent exists first
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
