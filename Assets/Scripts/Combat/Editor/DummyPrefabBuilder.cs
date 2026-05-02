// DummyPrefabBuilder.cs — combat foundation prefab + assets authoring.
//
// Two menu items:
//   LevelGen ▶ Combat ▶ Build Dummy Prefab
//   LevelGen ▶ Combat ▶ Place Dummy in Active Scene
//
// Build is idempotent — re-running rebuilds in place without duplicating.
// Master + Dummy CharacterStats assets are created on first run; existing
// assets are left alone (so any author tweaks survive a rebuild).
//
// Pattern mirrors PlayerPrefabBuilder.cs.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Combat.EditorTools
{
    public static class DummyPrefabBuilder
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string DummyPrefabPath  = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string PackPrefabPath   = "Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab";
        private const string BaseCtrlPath     = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string StatsFolder      = "Assets/Data/CharacterStats";
        private const string MasterStatsPath  = "Assets/Data/CharacterStats/CharacterStats_Master.asset";
        private const string DummyStatsPath   = "Assets/Data/CharacterStats/CharacterStats_Dummy.asset";

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build the Dummy prefab + stats assets
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Build Dummy Prefab")]
        private static void BuildDummyPrefab()
        {
            // ── Stats assets (create if missing) ────────────────────────────
            var masterStats = EnsureMasterStats();
            var dummyStats  = EnsureDummyStats();
            if (masterStats == null || dummyStats == null) return;

            // ── Source prefabs ──────────────────────────────────────────────
            var packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath);
            if (packPrefab == null)
            {
                Debug.LogError($"[DummyPrefabBuilder] Cannot load pack prefab at {PackPrefabPath}. Aborting.");
                return;
            }

            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseCtrlPath);
            if (baseController == null)
            {
                Debug.LogError($"[DummyPrefabBuilder] Cannot load base controller at {BaseCtrlPath}. Aborting.");
                return;
            }

            // Overwrite confirmation
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath) != null)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Overwrite Dummy prefab?",
                    $"A prefab already exists at {DummyPrefabPath}.\n\nOverwrite?",
                    "Overwrite", "Cancel");
                if (!ok)
                {
                    Debug.Log("[DummyPrefabBuilder] Build canceled — existing prefab preserved.");
                    return;
                }
                AssetDatabase.DeleteAsset(DummyPrefabPath);
            }

            EnsureFolder("Assets/Prefabs/Character Prefabs", "Enemy");

            // ── Build the in-memory hierarchy ───────────────────────────────
            var root = new GameObject("Dummy");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Stats runtime + Targetable (no player control scripts)
            var runtime = root.AddComponent<CharacterStatsRuntime>();
            AssignStatsField(runtime, dummyStats);
            root.AddComponent<Targetable>();

            // Nested visible model — same MaleCharacterPBR rig the player uses.
            var character = (GameObject)PrefabUtility.InstantiatePrefab(packPrefab, root.transform);
            if (character == null)
            {
                Debug.LogError("[DummyPrefabBuilder] InstantiatePrefab returned null for the pack prefab. Aborting.");
                Object.DestroyImmediate(root);
                return;
            }
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale    = Vector3.one;

            // Animator points at the BASE controller (not the override —
            // the override is tied to the player's identity). Idle plays
            // by default; with Speed=0 and IsGrounded=true the graph rests
            // in Idle indefinitely.
            var animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[DummyPrefabBuilder] MaleCharacterPBR child has no Animator. Aborting.");
                Object.DestroyImmediate(root);
                return;
            }
            animator.runtimeAnimatorController = baseController;
            animator.applyRootMotion = false;
            animator.updateMode      = AnimatorUpdateMode.Normal;
            animator.cullingMode     = AnimatorCullingMode.CullUpdateTransforms;
            EditorUtility.SetDirty(animator);

            // ── Save the prefab ─────────────────────────────────────────────
            bool success;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, DummyPrefabPath, out success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[DummyPrefabBuilder] SaveAsPrefabAsset failed.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Reload + summary ────────────────────────────────────────────
            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[DummyPrefabBuilder] Built {DummyPrefabPath}.\n" +
                $"  CharacterStats_Master: {AssetDatabase.LoadAssetAtPath<CharacterStats>(MasterStatsPath)?.name ?? "(missing)"}\n" +
                $"  CharacterStats_Dummy:  {dummyStats.name} (HP={dummyStats.maxHP}, Stamina={dummyStats.maxStamina})\n" +
                $"  Animator controller:   {baseController.name}\n" +
                $"  Components on root:    CharacterStatsRuntime, Targetable\n" +
                $"  No player control scripts. Drop into a scene and press Play to see Idle."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: drop a Dummy into the active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Place Dummy in Active Scene")]
        private static void PlaceDummyInActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[DummyPrefabBuilder] {DummyPrefabPath} not found. " +
                               "Run 'LevelGen ▶ Combat ▶ Build Dummy Prefab' first.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Debug.LogError("[DummyPrefabBuilder] InstantiatePrefab failed.");
                return;
            }
            instance.transform.position = new Vector3(2f, 0f, 2f);
            instance.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Place Dummy");

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            Debug.Log($"[DummyPrefabBuilder] Placed '{instance.name}' at {instance.transform.position}.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Stats asset creation
        // ════════════════════════════════════════════════════════════════════

        private static CharacterStats EnsureMasterStats()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "CharacterStats");

            var existing = AssetDatabase.LoadAssetAtPath<CharacterStats>(MasterStatsPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<CharacterStats>();
            asset.displayName = "Master Template";
            asset.maxHP       = 100;
            asset.maxStamina  = 100;
            asset.description = "Master template — duplicate and tweak values per character. " +
                                "Do not assign directly to a character.";

            AssetDatabase.CreateAsset(asset, MasterStatsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DummyPrefabBuilder] Created {MasterStatsPath}.");
            return asset;
        }

        private static CharacterStats EnsureDummyStats()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterStats>(DummyStatsPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<CharacterStats>();
            asset.displayName = "Dummy";
            asset.maxHP       = 999;
            asset.maxStamina  = 100;
            asset.description = "Stationary target practice. Will not die — high HP gives indefinite " +
                                "practice without damage application existing yet.";

            AssetDatabase.CreateAsset(asset, DummyStatsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DummyPrefabBuilder] Created {DummyStatsPath}.");
            return asset;
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns the private <c>stats</c> SerializeField on a
        /// CharacterStatsRuntime via SerializedObject (cross-assembly write
        /// path — Editor assembly can't touch the private field directly).
        /// </summary>
        private static void AssignStatsField(CharacterStatsRuntime runtime, CharacterStats stats)
        {
            var so = new SerializedObject(runtime);
            var prop = so.FindProperty("stats");
            if (prop == null)
            {
                Debug.LogError("[DummyPrefabBuilder] CharacterStatsRuntime has no 'stats' serialized field.");
                return;
            }
            prop.objectReferenceValue = stats;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtime);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[DummyPrefabBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[DummyPrefabBuilder] Created folder: {path}");
        }
    }
}
#endif
