// PlayerPrefabBuilder.cs — M1 final prefab + test scene authoring.
//
// Two menu items:
//   LevelGen ▶ Player ▶ Build Player_MaleHero Prefab
//   LevelGen ▶ Player ▶ Create M1 Test Scene
//
// Both are idempotent — re-running rebuilds from scratch.
//
// Why programmatic? UnityEvent persistent listeners survive prefab
// save/reload only when written via UnityEventTools.AddPersistentListener
// (or via SerializedObject manipulation of m_PersistentCalls). Hand-
// configured prefabs are easy to drift; this script is the canonical
// rebuild path.
//
// References:
//   Documentation/Player_Animator_Design_2026-04-26.md  (§4 prefab structure, §6 data flow)
//   Documentation/Player_Asset_Inventory_2026-04-26.md  (pack prefab GUIDs, avatar)

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LevelGen.Player.Editor
{
    public static class PlayerPrefabBuilder
    {
        // ── Paths ────────────────────────────────────────────────────────────
        private const string PrefabPath        = "Assets/Prefabs/Player/Player_MaleHero.prefab";
        private const string OverrideCtrlPath  = "Assets/Animators/Player/PlayerOverride_MaleHero.overrideController";
        private const string PackPrefabPath    = "Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab";
        private const string ActionsAssetPath  = "Assets/InputSystem_Actions.inputactions";
        private const string TestScenePath     = "Assets/Scenes/Test/Player_M1_Test.unity";

        // ── Action → method mapping (Player map order matches the .inputactions file) ─
        private static readonly (string action, string method)[] s_Bindings =
        {
            ("Move",     "OnMove"),
            ("Look",     "OnLook"),
            ("Attack",   "OnAttack"),
            ("Interact", "OnInteract"),
            ("Crouch",   "OnCrouch"),
            ("Jump",     "OnJump"),
            ("Previous", "OnPrevious"),
            ("Next",     "OnNext"),
            ("Sprint",   "OnSprint"),
        };

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build the Player prefab
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Player/Build Player_MaleHero Prefab")]
        private static void BuildPlayerMaleHeroPrefab()
        {
            // ── Asset preflight ──────────────────────────────────────────────
            var packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath);
            if (packPrefab == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] Cannot load pack prefab at {PackPrefabPath}. Aborting.");
                return;
            }

            var actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
            if (actionAsset == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] Cannot load InputActionAsset at {ActionsAssetPath}. Aborting.");
                return;
            }

            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverrideCtrlPath);
            if (overrideController == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] Cannot load override controller at {OverrideCtrlPath}. Aborting.");
                return;
            }

            // Idempotency: blow away any existing prefab so this run is the
            // single source of truth.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);

            EnsureFolder("Assets/Prefabs", "Player");

            // ── Build the in-memory hierarchy ────────────────────────────────
            var root = new GameObject("Player_MaleHero");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Player tag (built-in). Falls back to Untagged if missing.
            try { root.tag = "Player"; }
            catch (UnityException) { Debug.LogWarning("[PlayerPrefabBuilder] 'Player' tag not found in TagManager. Leaving root Untagged."); }

            // ── Components on root, in spec order ────────────────────────────
            var cc = root.AddComponent<CharacterController>();
            cc.slopeLimit      = 45f;
            cc.stepOffset      = 0.3f;
            cc.skinWidth       = 0.08f;
            cc.minMoveDistance = 0.001f;
            cc.center          = new Vector3(0f, 0.9f, 0f);
            cc.radius          = 0.3f;
            cc.height          = 1.8f;

            var ipi = root.AddComponent<UnityEngine.InputSystem.PlayerInput>();
            ipi.actions              = actionAsset;
            ipi.defaultActionMap     = "Player";
            ipi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;

            var reader = root.AddComponent<PlayerInputReader>();
            root.AddComponent<PlayerAnimator>();
            root.AddComponent<PlayerController>();

            // ── Wire UnityEvent persistent listeners (TASK 3) ────────────────
            // We rebuild m_ActionEvents directly via SerializedObject because
            // PlayerInput's runtime code does NOT auto-populate that array on
            // actions assignment — only the inspector's custom editor does.
            int wired = WirePlayerInputUnityEvents(ipi, reader, actionAsset);
            if (wired != s_Bindings.Length)
            {
                Debug.LogError($"[PlayerPrefabBuilder] UnityEvent wiring failed: {wired}/{s_Bindings.Length} listeners attached. Aborting save.");
                Object.DestroyImmediate(root);
                return;
            }

            // ── Nested pack child + Animator overrides ───────────────────────
            var character = (GameObject)PrefabUtility.InstantiatePrefab(packPrefab, root.transform);
            if (character == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] InstantiatePrefab returned null for the pack prefab. Aborting.");
                Object.DestroyImmediate(root);
                return;
            }
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale    = Vector3.one;

            var animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] MaleCharacterPBR child has no Animator. Aborting.");
                Object.DestroyImmediate(root);
                return;
            }

            // Decision B (design doc §4): runtime prefab forces applyRootMotion
            // to false even though the pack ships true. _InPlace clips have no
            // root motion to apply, and CharacterController is the single
            // source of position truth.
            animator.runtimeAnimatorController = overrideController;
            animator.applyRootMotion = false;
            animator.updateMode      = AnimatorUpdateMode.Normal;
            animator.cullingMode     = AnimatorCullingMode.CullUpdateTransforms;
            EditorUtility.SetDirty(animator);

            // ── Save the prefab ──────────────────────────────────────────────
            bool success;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] SaveAsPrefabAsset failed.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Reload + verify (belt-and-suspenders, prompt 03 lesson) ──────
            VerifyPrefabRoundTrip();

            // Select the saved asset for visual confirmation
            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: create the M1 test scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Player/Create M1 Test Scene")]
        private static void CreateM1TestScene()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] {PrefabPath} not found. Run 'Build Player_MaleHero Prefab' first.");
                return;
            }

            // Prompt the user about unsaved work in the active scene.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[PlayerPrefabBuilder] Test scene creation canceled (active scene not saved).");
                return;
            }

            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets/Scenes", "Test");

            // Idempotency: delete any prior test scene at this path.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TestScenePath) != null)
                AssetDatabase.DeleteAsset(TestScenePath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Position the Main Camera (Q-3: static, hand-positioned, no follow).
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(0f, 5f, -8f);
                mainCam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                mainCam.fieldOfView = 60f;
            }
            else
            {
                Debug.LogWarning("[PlayerPrefabBuilder] DefaultGameObjects scene has no Main Camera. Adding one.");
                var camGo = new GameObject("Main Camera");
                var cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 5f, -8f);
                camGo.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
                cam.fieldOfView = 60f;
            }

            // Floor plane at origin, scaled 2× for 20×20m coverage.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            // Drop the player prefab directly (NOT via PlayerSpawner — that
            // stays unused in M1; Q-3-aligned).
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, TestScenePath);
            if (!saved)
            {
                Debug.LogError($"[PlayerPrefabBuilder] SaveScene failed at {TestScenePath}.");
                return;
            }

            Debug.Log(
                $"[PlayerPrefabBuilder] Test scene created:\n" +
                $"  Path: {TestScenePath}\n" +
                $"  GameObjects: Main Camera, Directional Light, Floor, Player_MaleHero\n" +
                $"  Player at origin, camera at (0, 5, -8) rot (25, 0, 0)\n" +
                $"  Press Play to verify §7 test plan."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // UnityEvent wiring (the careful part)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuilds <c>m_ActionEvents</c> on the Unity PlayerInput component so
        /// each action in the Player map has exactly one persistent listener
        /// pointing at the matching method on <paramref name="reader"/>.
        /// Uses SerializedObject directly because PlayerInput's runtime code
        /// does not populate <c>actionEvents</c> outside the custom inspector.
        /// </summary>
        private static int WirePlayerInputUnityEvents(
            UnityEngine.InputSystem.PlayerInput ipi,
            PlayerInputReader reader,
            InputActionAsset actionAsset)
        {
            var playerMap = actionAsset.FindActionMap("Player", throwIfNotFound: false);
            if (playerMap == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] No 'Player' action map in the asset.");
                return 0;
            }

            var readerType = typeof(PlayerInputReader);
            // Format: "Namespace.Type, AssemblyName" — matches what UnityEvents writes.
            string targetTypeName = $"{readerType.FullName}, {readerType.Assembly.GetName().Name}";

            var serialized = new SerializedObject(ipi);
            serialized.Update();

            var eventsArray = serialized.FindProperty("m_ActionEvents");
            if (eventsArray == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] PlayerInput has no m_ActionEvents serialized field. InputSystem version mismatch?");
                return 0;
            }

            eventsArray.ClearArray();

            int wiredOk = 0;
            for (int i = 0; i < s_Bindings.Length; i++)
            {
                var (actionName, methodName) = s_Bindings[i];

                // Verify the method actually exists on PlayerInputReader.
                var mi = readerType.GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (mi == null)
                {
                    Debug.LogError($"[PlayerPrefabBuilder] Method '{methodName}' not found on {readerType.Name}.");
                    continue;
                }

                var action = playerMap.FindAction(actionName, throwIfNotFound: false);
                if (action == null)
                {
                    Debug.LogError($"[PlayerPrefabBuilder] Action '{actionName}' not found in Player map.");
                    continue;
                }

                eventsArray.InsertArrayElementAtIndex(i);
                var entry = eventsArray.GetArrayElementAtIndex(i);

                // Action identity
                entry.FindPropertyRelative("m_ActionId").stringValue   = action.id.ToString();
                entry.FindPropertyRelative("m_ActionName").stringValue = $"Player/{action.name}[{action.id}]";

                // Persistent calls: clear any inherited entries, add one
                var callsArray = entry.FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (callsArray == null)
                {
                    Debug.LogError($"[PlayerPrefabBuilder] Cannot find m_PersistentCalls.m_Calls on action event for '{actionName}'.");
                    continue;
                }
                callsArray.ClearArray();
                callsArray.InsertArrayElementAtIndex(0);
                var call = callsArray.GetArrayElementAtIndex(0);

                call.FindPropertyRelative("m_Target").objectReferenceValue           = reader;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue    = targetTypeName;
                call.FindPropertyRelative("m_MethodName").stringValue                = methodName;
                call.FindPropertyRelative("m_Mode").intValue                         = 0; // PersistentListenerMode.EventDefined (pass ctx through)
                call.FindPropertyRelative("m_CallState").intValue                    = 2; // UnityEventCallState.RuntimeOnly

                // Default-zero the argument record so the YAML matches what the
                // inspector would have produced.
                var args = call.FindPropertyRelative("m_Arguments");
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue           = null;
                args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue    = "UnityEngine.Object, UnityEngine";
                args.FindPropertyRelative("m_IntArgument").intValue                          = 0;
                args.FindPropertyRelative("m_FloatArgument").floatValue                      = 0f;
                args.FindPropertyRelative("m_StringArgument").stringValue                    = string.Empty;
                args.FindPropertyRelative("m_BoolArgument").boolValue                        = false;

                wiredOk++;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(ipi);

            // Visible in console — confirms the method targets and counts at build time.
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PlayerPrefabBuilder] UnityEvent wiring:");
            for (int i = 0; i < s_Bindings.Length; i++)
            {
                var (actionName, methodName) = s_Bindings[i];
                bool ok = i < eventsArray.arraySize
                          && eventsArray.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize == 1;
                string mark = ok ? "✓" : "✗";
                sb.AppendLine($"  {actionName,-9}→ PlayerInputReader.{methodName,-12} {mark}");
            }
            Debug.Log(sb.ToString());
            return wiredOk;
        }

        // ════════════════════════════════════════════════════════════════════
        // Verification
        // ════════════════════════════════════════════════════════════════════

        private static void VerifyPrefabRoundTrip()
        {
            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (reloaded == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] Reload failed at {PrefabPath}.");
                return;
            }

            var ipi = reloaded.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (ipi == null)
            {
                Debug.LogError("[PlayerPrefabBuilder] Reloaded prefab has no UnityEngine.InputSystem.PlayerInput.");
                return;
            }

            int totalListeners = 0;
            int eventCount = 0;
            foreach (var ae in ipi.actionEvents)
            {
                eventCount++;
                totalListeners += ae.GetPersistentEventCount();
            }

            // Animator override check
            var characterTf = reloaded.transform.Find("MaleCharacterPBR");
            string animatorState = "<no MaleCharacterPBR child>";
            if (characterTf != null)
            {
                var a = characterTf.GetComponent<Animator>();
                if (a != null)
                {
                    animatorState =
                        $"Controller={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "(null)")}, " +
                        $"applyRootMotion={a.applyRootMotion}";
                }
            }

            Debug.Log(
                $"[PlayerPrefabBuilder] Saved {PrefabPath}.\n" +
                $"  ActionEvents: {eventCount} (expected 9).\n" +
                $"  Persistent listeners after reload: {totalListeners} (expected: 9).\n" +
                $"  Nested Animator: {animatorState} (expected: Controller=PlayerOverride_MaleHero, applyRootMotion=False)."
            );

            if (totalListeners != s_Bindings.Length)
                Debug.LogError("[PlayerPrefabBuilder] Listener count mismatch — bindings did not survive save/reload.");
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
                Debug.LogError($"[PlayerPrefabBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[PlayerPrefabBuilder] Created folder: {path}");
        }
    }
}
#endif
