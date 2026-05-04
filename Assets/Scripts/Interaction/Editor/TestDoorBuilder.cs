// TestDoorBuilder.cs — procedural test-door scaffold for M7 verification.
//
// Two menu items:
//   LevelGen ▶ Interaction ▶ Build TestDoor Prefab
//   LevelGen ▶ Interaction ▶ Place TestDoor in Active Scene
//
// The test door is a stand-in: a primitive cube on a hinge, NOT a
// real game prefab. It exists so we can verify OpenInteractable's
// behavior without depending on level-generation work. M16 (much
// later in the campaign) will wire OpenInteractable onto real FDP
// COMP_Door_* prefabs; until then, TestDoor stays in
// Assets/Prefabs/TestRig/ as a diagnostic.
//
// Idempotent: rebuild deletes & recreates. Place skips if a
// TestDoor instance is already in the active scene.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LevelGen.Interaction;

namespace LevelGen.Interaction.Editor
{
    public static class TestDoorBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/TestRig/TestDoor.prefab";

        // Door dimensions (per M7 prompt — concrete numbers, no derivation).
        private static readonly Vector3 DoorScale         = new Vector3(1.0f, 2.0f, 0.1f);
        private static readonly Vector3 DoorLocalPosition = new Vector3(0.5f, 1.0f, 0f);

        // Distinctive brown so the door is obvious in the scene view.
        private static readonly Color   DoorColor = new Color(0.6f, 0.4f, 0.2f, 1f);

        // SphereCollider trigger sized for a comfortable approach radius.
        private const float TriggerRadius = 1.5f;

        // Place-into-scene world position. Far enough from spawn that the
        // player has to walk to it, close enough to find quickly.
        private static readonly Vector3 PlaceWorldPosition = new Vector3(0f, 0f, 3f);

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build the prefab
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Interaction/Build TestDoor Prefab")]
        public static void BuildPrefab()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "TestRig");

            // Idempotent rebuild — clean slate every time.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);

            // ── Build the in-memory hierarchy ───────────────────────────────
            //
            //   TestDoor (root)
            //   ├── HingePivot
            //   │   └── DoorLeaf (Cube primitive)
            //   └── _OpenZone
            //       ├── SphereCollider (isTrigger=true, radius=1.5)
            //       └── OpenInteractable (_hinge → HingePivot)
            //
            var root = new GameObject("TestDoor");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var hingePivot = new GameObject("HingePivot");
            hingePivot.transform.SetParent(root.transform, worldPositionStays: false);
            hingePivot.transform.localPosition = Vector3.zero;
            hingePivot.transform.localRotation = Quaternion.identity;
            hingePivot.transform.localScale    = Vector3.one;

            // DoorLeaf — primitive cube at offset position so HingePivot
            // sits at the door's hinge edge (not its center).
            var doorLeaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorLeaf.name = "DoorLeaf";
            doorLeaf.transform.SetParent(hingePivot.transform, worldPositionStays: false);
            doorLeaf.transform.localPosition = DoorLocalPosition;
            doorLeaf.transform.localRotation = Quaternion.identity;
            doorLeaf.transform.localScale    = DoorScale;

            // Material — URP/Lit with a distinctive brown. Project standard
            // (per WhiteboxPackFactory pattern); avoid Standard shader which
            // displays magenta in URP.
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                var mat = new Material(urpLit);
                mat.name = "M_TestDoor";
                // URP/Lit uses _BaseColor, not _Color.
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", DoorColor);
                else                                mat.color = DoorColor;
                doorLeaf.GetComponent<Renderer>().sharedMaterial = mat;
            }
            else
            {
                Debug.LogWarning("[TestDoorBuilder] Universal Render Pipeline/Lit shader not found. " +
                                 "Door will display with the default magenta. Install URP or change " +
                                 "the project's render pipeline.");
            }

            // The cube primitive ships with a BoxCollider. Disable trigger /
            // remove? Leave as a non-trigger physical collider so the player
            // can't walk through the closed door. Acceptable tradeoff for
            // a test scaffold.

            // ── _OpenZone child — trigger volume + OpenInteractable ─────────
            var zone = new GameObject("_OpenZone");
            zone.transform.SetParent(root.transform, worldPositionStays: false);
            zone.transform.localPosition = Vector3.zero;
            zone.transform.localRotation = Quaternion.identity;
            zone.transform.localScale    = Vector3.one;

            var sc = zone.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius    = TriggerRadius;
            // Center on the door leaf's mid-height so the trigger surrounds
            // the door visually.
            sc.center    = new Vector3(0.5f, 1.0f, 0f);

            var openable = zone.AddComponent<OpenInteractable>();
            // Reset() does not fire on programmatic AddComponent (M6 lesson).
            // Wire _hinge + prompt anchor explicitly via SerializedObject.
            AssignOpenInteractableRefs(openable, hingePivot.transform);

            // Build the prompt UI now so it's visible in the prefab inspector
            // (otherwise it's only built on Awake at runtime).
            openable.EnsurePromptUI();

            // ── Save the prefab ─────────────────────────────────────────────
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[TestDoorBuilder] SaveAsPrefabAsset failed.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[TestDoorBuilder] Built {PrefabPath}.\n" +
                $"  Hierarchy: TestDoor / HingePivot / DoorLeaf (cube {DoorScale}) + _OpenZone (SphereCollider trigger r={TriggerRadius})\n" +
                $"  OpenInteractable._hinge → HingePivot\n" +
                $"  Place into a scene via 'LevelGen ▶ Interaction ▶ Place TestDoor in Active Scene'."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: place into active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Interaction/Place TestDoor in Active Scene")]
        public static void PlaceInActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TestDoorBuilder] {PrefabPath} not found. " +
                               "Run 'LevelGen ▶ Interaction ▶ Build TestDoor Prefab' first.");
                return;
            }

            // Skip if a TestDoor instance is already in the active scene.
            var existing = GameObject.Find("TestDoor");
            if (existing != null)
            {
                Debug.Log("[TestDoorBuilder] TestDoor already in scene — selecting existing instance.");
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Debug.LogError("[TestDoorBuilder] InstantiatePrefab failed.");
                return;
            }
            instance.transform.position = PlaceWorldPosition;
            instance.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Place TestDoor");
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            Debug.Log($"[TestDoorBuilder] Placed '{instance.name}' at {instance.transform.position}.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wires OpenInteractable's _hinge SerializeField via SerializedObject.
        /// Reset() does not fire on programmatic AddComponent (M6 lesson) —
        /// the build path must explicitly write the field.
        /// </summary>
        private static void AssignOpenInteractableRefs(OpenInteractable target, Transform hinge)
        {
            var so = new SerializedObject(target);
            void Wire(string fieldName, Object value)
            {
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                {
                    Debug.LogError($"[TestDoorBuilder] OpenInteractable has no '{fieldName}' field.");
                    return;
                }
                prop.objectReferenceValue = value;
            }
            Wire("_hinge", hinge);
            // The prompt anchor on Interactable defaults to PromptAnchor =>
            // transform-fallback when null. Wire it explicitly to the hinge
            // so the world-space label sits at the door's hinge edge.
            Wire("_promptAnchor", hinge);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[TestDoorBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[TestDoorBuilder] Created folder: {path}");
        }
    }
}
#endif
