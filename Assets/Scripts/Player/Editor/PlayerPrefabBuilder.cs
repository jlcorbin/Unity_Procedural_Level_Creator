// PlayerPrefabBuilder.cs — M1 final prefab + test scene authoring,
// extended in M2-A with Cinemachine 3.x camera setup.
//
// Four menu items:
//   LevelGen ▶ Player ▶ Build Player_MaleHero Prefab
//   LevelGen ▶ Player ▶ Create M1 Test Scene
//   LevelGen ▶ Player ▶ Add CameraTarget to Player_MaleHero Prefab  (M2-A)
//   LevelGen ▶ Player ▶ Add Cinemachine Follow Camera to Active Scene  (M2-A)
//
// All four are idempotent — re-running rebuilds/refreshes without
// duplicating state. The camera setup aborts cleanly if a
// CinemachineCamera already exists in the active scene.
//
// M2-A note: the design doc's "CinemachineFollow + RotationComposer"
// combination doesn't expose input axes for InputAxisController to
// drive. This implementation substitutes CinemachineOrbitalFollow
// (which DOES expose HorizontalAxis / VerticalAxis) and keeps
// CinemachineRotationComposer for the look-at framing.
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
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

            // ── M2-A: CameraTarget child for Cinemachine follow ──────────────
            // Local (0, 1.6, 0) ≈ chest height. Cinemachine's Follow + LookAt
            // both target this transform — pointing at the root would frame
            // the player's feet.
            EnsureCameraTargetChild(root);

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

            // CameraTarget child check (M2-A)
            var ctTf = reloaded.transform.Find("CameraTarget");
            string camTargetState = ctTf != null
                ? $"present at local {ctTf.localPosition}"
                : "MISSING";

            Debug.Log(
                $"[PlayerPrefabBuilder] Saved {PrefabPath}.\n" +
                $"  ActionEvents: {eventCount} (expected 9).\n" +
                $"  Persistent listeners after reload: {totalListeners} (expected: 9).\n" +
                $"  Nested Animator: {animatorState} (expected: Controller=PlayerOverride_MaleHero, applyRootMotion=False).\n" +
                $"  CameraTarget child: {camTargetState} (expected: present at (0.0, 1.6, 0.0))."
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

        /// <summary>
        /// Idempotently adds a CameraTarget child at local (0, 1.6, 0).
        /// Used by both <see cref="BuildPlayerMaleHeroPrefab"/> (in-memory
        /// path) and <see cref="AddCameraTargetToExistingPrefab"/>
        /// (LoadPrefabContents path).
        /// </summary>
        private static void EnsureCameraTargetChild(GameObject prefabRoot)
        {
            var existing = prefabRoot.transform.Find("CameraTarget");
            if (existing != null)
            {
                Debug.Log($"[PlayerPrefabBuilder] CameraTarget already present at local {existing.localPosition}; leaving in place.");
                return;
            }

            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(prefabRoot.transform, worldPositionStays: false);
            camTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camTarget.transform.localRotation = Quaternion.identity;
            camTarget.transform.localScale    = Vector3.one;
            Debug.Log("[PlayerPrefabBuilder] Added CameraTarget child at local (0, 1.6, 0).");
        }

        // ════════════════════════════════════════════════════════════════════
        // M2-A Menu item: add CameraTarget to existing prefab without rebuild
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Player/Add CameraTarget to Player_MaleHero Prefab")]
        private static void AddCameraTargetToExistingPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] {PrefabPath} not found. Run 'Build Player_MaleHero Prefab' first.");
                return;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[PlayerPrefabBuilder] LoadPrefabContents failed for {PrefabPath}.");
                return;
            }

            try
            {
                EnsureCameraTargetChild(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Reload + verify
            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var ct = reloaded != null ? reloaded.transform.Find("CameraTarget") : null;
            Debug.Log(ct != null
                ? $"[PlayerPrefabBuilder] CameraTarget verified on saved prefab at local {ct.localPosition}."
                : "[PlayerPrefabBuilder] CameraTarget MISSING after save/reload.");
        }

        // ════════════════════════════════════════════════════════════════════
        // M2-A Menu item: add Cinemachine 3.x follow camera to active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Player/Add Cinemachine Follow Camera to Active Scene")]
        private static void AddCinemachineFollowCameraToActiveScene()
        {
            // ── Preflight ────────────────────────────────────────────────────
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                Debug.LogError("[CM Setup] Active scene must be saved on disk before running. Save the scene and re-try.");
                return;
            }

            // Idempotency guard: bail if any CinemachineCamera already exists.
            var existingVcam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (existingVcam != null)
            {
                Debug.LogError($"[CM Setup] Scene already has a CinemachineCamera ('{existingVcam.name}'). " +
                               "Delete it (and any CM Brain Camera GameObject) before re-running this menu item.");
                return;
            }

            // Find player + CameraTarget
            var playerCtrl = Object.FindAnyObjectByType<PlayerController>();
            if (playerCtrl == null)
            {
                Debug.LogError("[CM Setup] No PlayerController found in active scene. Drop Player_MaleHero.prefab in first.");
                return;
            }
            var camTarget = playerCtrl.transform.Find("CameraTarget");
            if (camTarget == null)
            {
                Debug.LogError($"[CM Setup] '{playerCtrl.name}' has no CameraTarget child. " +
                               "Run 'Add CameraTarget to Player_MaleHero Prefab' first (or rebuild the prefab).");
                return;
            }

            // Look up InputActionReference for Look (sub-asset of the .inputactions)
            var lookRef = FindInputActionReference("Player", "Look");
            if (lookRef == null)
            {
                Debug.LogError(
                    "[CM Setup] No InputActionReference sub-asset found for Player/Look. " +
                    "Open Assets/InputSystem_Actions.inputactions in the editor once (which auto-generates the references), " +
                    "save, then re-run this menu item.");
                return;
            }

            // ── Remove existing MainCamera ───────────────────────────────────
            // Two MainCamera-tagged cameras = undefined Camera.main behavior.
            int removed = 0;
            var taggedCams = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (var go in taggedCams)
            {
                Object.DestroyImmediate(go);
                removed++;
            }
            if (removed > 0)
                Debug.Log($"[CM Setup] Removed {removed} existing MainCamera-tagged GameObject(s).");

            // ── Create CM Brain Camera (the rendering camera) ───────────────
            var brainGO = new GameObject("CM Brain Camera");
            brainGO.tag = "MainCamera";
            brainGO.AddComponent<Camera>();
            brainGO.AddComponent<AudioListener>();
            brainGO.AddComponent<CinemachineBrain>();
            // Reasonable default position; Brain doesn't move on its own — the
            // vcam drives it once Play starts.
            brainGO.transform.position = new Vector3(0f, 5f, -8f);
            brainGO.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

            // ── Create CM Follow Camera (the virtual camera) ────────────────
            var vcamGO = new GameObject("CM Follow Camera");
            var vcam = vcamGO.AddComponent<CinemachineCamera>();
            // Priority struct in CM 3.x: assign a fresh CameraPriority with Value 10
            vcam.Priority = new PrioritySettings { Value = 10, Enabled = true };

            // Targets: both Follow and LookAt point at the player's CameraTarget.
            vcam.Follow = camTarget;
            vcam.LookAt = camTarget;

            // Position component: OrbitalFollow (replaces CinemachineFollow per
            // M2-A substitution — OrbitalFollow exposes the input axes that
            // CinemachineInputAxisController drives).
            var orbital = vcamGO.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius     = 4.0f;                        // matches D5's 4m behind
            // CM 3.1.x OrbitalFollow has no Center field — orbit centers on
            // the target directly. CameraTarget is already at local (0,1.6,0)
            // which is chest height; the VerticalAxis Value=15° initial gives
            // the slight downward look that D5's "+2 up" would have provided.

            // Yaw axis (D7: 360° wrap). Read-modify-write to preserve any
            // CM-shipped defaults for fields we don't explicitly override.
            var hAxis = orbital.HorizontalAxis;
            hAxis.Value  = 0f;
            hAxis.Range  = new Vector2(-180f, 180f);
            hAxis.Wrap   = true;
            hAxis.Center = 0f;
            orbital.HorizontalAxis = hAxis;

            // Pitch axis (D6: -10° to 70°)
            var vAxis = orbital.VerticalAxis;
            vAxis.Value  = 15f;                            // slight downward look at start
            vAxis.Range  = new Vector2(-10f, 70f);
            vAxis.Wrap   = false;
            vAxis.Center = 15f;
            orbital.VerticalAxis = vAxis;

            // Aim component: RotationComposer points the camera at LookAt
            // every frame. OrbitalFollow runs at the Body stage (position
            // only) and does NOT set RawOrientation — without an Aim stage
            // component the camera orbits correctly on input but never turns
            // to face the target. Originally removed in 08-A on a wrong
            // diagnosis ("RotationComposer overrides input"); restored
            // 08-A-2 after empirical confirmation that input was reaching
            // OrbitalFollow's axes (HorizontalAxis.Value varied with mouse)
            // but rotation never updated. CM 3.x convention is Body+Aim
            // staged separately — they're complementary, not conflicting.
            vcamGO.AddComponent<CinemachineRotationComposer>();

            // Collision avoidance: Deoccluder per D9
            var deocc = vcamGO.AddComponent<CinemachineDeoccluder>();
            deocc.MinimumDistanceFromTarget = 1.0f;
            // CollideAgainst layer mask stays at default (everything-but-IgnoreRaycast).

            // Input wiring: scan auto-populates Controllers list from OrbitalFollow's axes
            var inputCtrl = vcamGO.AddComponent<CinemachineInputAxisController>();
            // The base class scans IInputAxisOwners in OnEnable; force a synchronize
            // here so Controllers is populated immediately (not after a frame).
            inputCtrl.SynchronizeControllers();

            int wiredAxes = 0;
            // Diagnostic: list all controller names + categorization.
            var axisDiag = new System.Text.StringBuilder();
            axisDiag.AppendLine($"[CM Setup] Controllers found ({inputCtrl.Controllers.Count}):");

            // The Reader's field for the InputActionReference is named "Input"
            // in some CM 3.x versions and "InputAction" in others — discover
            // it via reflection so we don't crash on a name change.
            System.Reflection.FieldInfo readerActionField = null;
            System.Reflection.FieldInfo readerGainField   = null;

            foreach (var c in inputCtrl.Controllers)
            {
                // Explicit exclusion: OrbitalFollow's third axis (RadialAxis)
                // controls zoom, not look. Match only yaw + pitch.
                string n = c.Name ?? "";
                bool isRadial = n.Contains("Radial") || n.Contains("Scale") || n.Contains("Zoom");
                bool isXAxis = !isRadial && (n.Contains(" X")
                                             || n.EndsWith("X")
                                             || n.Contains("Horizontal"));
                bool isYAxis = !isRadial && (n.Contains(" Y")
                                             || n.EndsWith("Y")
                                             || n.Contains("Vertical"));
                bool wireThis = isXAxis || isYAxis;
                bool isYish = isYAxis;

                axisDiag.AppendLine($"  '{n}' → {(isRadial ? "SKIP (radial/zoom)" : wireThis ? (isYish ? "wire as Y (inverted)" : "wire as X") : "SKIP (no match)")}");

                if (!wireThis) continue;

                c.Enabled = true;

                if (readerActionField == null && c.Input != null)
                {
                    var rt = c.Input.GetType();
                    readerActionField = rt.GetField("Input")
                                       ?? rt.GetField("InputAction");
                    readerGainField = rt.GetField("Gain");
                    if (readerActionField == null)
                    {
                        Debug.LogWarning($"[CM Setup] Reader on '{c.Name}' has neither 'Input' nor 'InputAction' field. CM API may have changed; wire by hand.");
                        break;
                    }
                }
                if (readerActionField != null && c.Input != null)
                {
                    readerActionField.SetValue(c.Input, lookRef);
                    if (readerGainField != null)
                        readerGainField.SetValue(c.Input, isYish ? -1.0f : 1.0f);  // D8: Y inverted; bumped from ±0.2 → ±1.0 (08-A-2-tune: 0.2 was too slow against featureless test scene)
                    wiredAxes++;
                }
            }

            // Also: defensively NULL any reader on radial/zoom-style axes in
            // case a previous run wired them. Prevents Look-input from
            // accidentally driving camera zoom.
            int unwiredRadial = 0;
            foreach (var c in inputCtrl.Controllers)
            {
                string n = c.Name ?? "";
                if (!(n.Contains("Radial") || n.Contains("Scale") || n.Contains("Zoom"))) continue;
                if (c.Input == null) continue;
                if (readerActionField == null) continue;
                var current = readerActionField.GetValue(c.Input) as InputActionReference;
                if (current != null)
                {
                    readerActionField.SetValue(c.Input, null);
                    unwiredRadial++;
                }
            }
            if (unwiredRadial > 0)
                axisDiag.AppendLine($"  Defensively cleared {unwiredRadial} pre-wired radial axis Reader.Input reference(s).");

            Debug.Log(axisDiag.ToString());

            if (wiredAxes != 2)
                Debug.LogWarning($"[CM Setup] Expected to wire 2 Look axes (yaw + pitch), wired {wiredAxes}. " +
                                 "Inspect CinemachineInputAxisController.Controllers list and wire any unset axes manually.");

            // ── Save the scene ──────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            // ── Reload + verify ─────────────────────────────────────────────
            var reopened = EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
            var brainCheck = Object.FindAnyObjectByType<CinemachineBrain>();
            var vcamCheck  = Object.FindAnyObjectByType<CinemachineCamera>();

            string followName  = vcamCheck != null && vcamCheck.Follow != null ? vcamCheck.Follow.name : "(null)";
            string lookAtName  = vcamCheck != null && vcamCheck.LookAt != null ? vcamCheck.LookAt.name : "(null)";

            int wiredAfterReload = 0;
            if (vcamCheck != null)
            {
                var ic = vcamCheck.GetComponent<CinemachineInputAxisController>();
                if (ic != null)
                {
                    foreach (var c in ic.Controllers)
                    {
                        if (c.Input == null) continue;
                        // Detect either 'Input' or 'InputAction' field, count as wired if non-null
                        var rt = c.Input.GetType();
                        var f = rt.GetField("Input") ?? rt.GetField("InputAction");
                        if (f != null && f.GetValue(c.Input) is InputActionReference iar && iar != null)
                            wiredAfterReload++;
                    }
                }
            }

            Debug.Log(
                $"[CM Setup] Cinemachine follow camera installed in '{reopened.name}'.\n" +
                $"  CinemachineBrain: {(brainCheck != null ? "✓" : "✗")}\n" +
                $"  CinemachineCamera: {(vcamCheck != null ? "✓" : "✗")}\n" +
                $"  Follow:  {followName} (expected: CameraTarget)\n" +
                $"  LookAt:  {lookAtName} (expected: CameraTarget)\n" +
                $"  OrbitalFollow:        Sphere, Radius=4, VerticalAxis range=(-10,70) initial=15, HorizontalAxis range=(-180,180,wrap)\n" +
                $"  Deoccluder:           MinDistance=1.0\n" +
                $"  InputAxisController:  {wiredAfterReload}/2 axes wired to Player/Look (gain ±1.0, Y inverted)\n" +
                $"  Press Play in this scene to verify M2-A acceptance items C1–C5."
            );
        }

        /// <summary>
        /// Walks the .inputactions asset's sub-assets to find the
        /// InputActionReference for the given map / action name.
        /// Returns null if not found (most likely cause: the asset
        /// hasn't been opened in the editor since import, so its
        /// auto-generated sub-asset references haven't been written).
        /// </summary>
        private static InputActionReference FindInputActionReference(string mapName, string actionName)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(ActionsAssetPath);
            foreach (var sub in subAssets)
            {
                if (sub is InputActionReference iar
                    && iar.action != null
                    && iar.action.name == actionName
                    && iar.action.actionMap != null
                    && iar.action.actionMap.name == mapName)
                {
                    return iar;
                }
            }
            return null;
        }
    }
}
#endif
