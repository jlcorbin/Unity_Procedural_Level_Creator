// DamageNumberBuilder.cs — authoring for floating combat text (M8).
//
// Three menu items:
//   LevelGen ▶ UI ▶ Build DamageNumber Prefab
//   LevelGen ▶ UI ▶ Build DamageNumberSpawner Prefab
//   LevelGen ▶ UI ▶ Place DamageNumberSpawner in Active Scene
//
// Build steps are idempotent (delete + recreate). Place is
// idempotent (skip if a spawner already exists in the active scene).

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LevelGen.UI;

namespace LevelGen.UI.EditorTools
{
    public static class DamageNumberBuilder
    {
        private const string DamageNumberPrefabPath  = "Assets/Prefabs/UI/DamageNumber.prefab";
        private const string SpawnerPrefabPath       = "Assets/Prefabs/UI/DamageNumberSpawner.prefab";

        // World-space TMP_Text size — scaled for the existing top-down
        // camera distance. Tune in playtest.
        private const float DamageNumberFontSize = 6f;

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build DamageNumber prefab
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Build DamageNumber Prefab")]
        private static void BuildDamageNumberPrefab()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            // Idempotent rebuild — clean slate every time.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath) != null)
                AssetDatabase.DeleteAsset(DamageNumberPrefabPath);

            // World-space TMP_Text on a bare GameObject (NO Canvas — that
            // would force ScreenSpace + RectTransform sizing). The
            // TextMeshPro component on a GameObject without a Canvas
            // renders via the world-space mesh-renderer path.
            var root = new GameObject("DamageNumber",
                typeof(TextMeshPro),
                typeof(DamageNumber));

            var tmp = root.GetComponent<TextMeshPro>();
            tmp.text       = "0"; // placeholder — overwritten by Initialize at runtime
            tmp.fontSize   = DamageNumberFontSize;
            tmp.alignment  = TextAlignmentOptions.Center;
            tmp.color      = Color.white;
            tmp.fontStyle  = FontStyles.Bold;
            // Outline for legibility against varied scene backgrounds.
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;

            // Save prefab.
            var saved = PrefabUtility.SaveAsPrefabAsset(root, DamageNumberPrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[DamageNumberBuilder] SaveAsPrefabAsset failed for DamageNumber.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[DamageNumberBuilder] Built {DamageNumberPrefabPath}.\n" +
                $"  TextMeshPro world-space, fontSize={DamageNumberFontSize}, white + black outline.\n" +
                $"  Then run 'LevelGen ▶ UI ▶ Build DamageNumberSpawner Prefab'."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build DamageNumberSpawner prefab
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Build DamageNumberSpawner Prefab")]
        private static void BuildSpawnerPrefab()
        {
            var dnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath);
            if (dnPrefab == null)
            {
                Debug.LogError($"[DamageNumberBuilder] {DamageNumberPrefabPath} not found. " +
                               "Run 'LevelGen ▶ UI ▶ Build DamageNumber Prefab' first.");
                return;
            }
            var dnComponent = dnPrefab.GetComponent<DamageNumber>();
            if (dnComponent == null)
            {
                Debug.LogError("[DamageNumberBuilder] DamageNumber prefab missing DamageNumber component. " +
                               "Re-run 'Build DamageNumber Prefab'.");
                return;
            }

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            // Idempotent rebuild.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerPrefabPath) != null)
                AssetDatabase.DeleteAsset(SpawnerPrefabPath);

            var root = new GameObject("DamageNumberSpawner",
                typeof(DamageNumberSpawner));
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Wire _damageNumberPrefab via SerializedObject. Reset() doesn't
            // fire on programmatic AddComponent (M6 lesson) — we must write
            // the field explicitly here.
            var spawner = root.GetComponent<DamageNumberSpawner>();
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("_damageNumberPrefab");
            if (prop == null)
            {
                Debug.LogError("[DamageNumberBuilder] DamageNumberSpawner has no '_damageNumberPrefab' field.");
                Object.DestroyImmediate(root);
                return;
            }
            prop.objectReferenceValue = dnComponent;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, SpawnerPrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[DamageNumberBuilder] SaveAsPrefabAsset failed for DamageNumberSpawner.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerPrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[DamageNumberBuilder] Built {SpawnerPrefabPath}.\n" +
                $"  _damageNumberPrefab → {DamageNumberPrefabPath}\n" +
                $"  Place into a scene via 'LevelGen ▶ UI ▶ Place DamageNumberSpawner in Active Scene'."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: place spawner into active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Place DamageNumberSpawner in Active Scene")]
        private static void PlaceSpawnerInActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[DamageNumberBuilder] {SpawnerPrefabPath} not found. " +
                               "Run 'LevelGen ▶ UI ▶ Build DamageNumberSpawner Prefab' first.");
                return;
            }

            var existing = Object.FindAnyObjectByType<DamageNumberSpawner>();
            if (existing != null)
            {
                Debug.Log("[DamageNumberBuilder] DamageNumberSpawner already in scene — selecting existing instance.");
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Debug.LogError("[DamageNumberBuilder] InstantiatePrefab failed.");
                return;
            }
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Place DamageNumberSpawner");
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            Debug.Log($"[DamageNumberBuilder] Placed '{instance.name}' at world origin.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[DamageNumberBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[DamageNumberBuilder] Created folder: {path}");
        }
    }
}
#endif
