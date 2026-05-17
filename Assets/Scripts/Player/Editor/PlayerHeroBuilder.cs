// PlayerHeroBuilder.cs — M12-R single canonical path that creates,
// renames, and wires Player_Hero.prefab.
//
// Replaces (consolidates the responsibilities of) the deleted:
//   PlayerPrefabBuilder.cs
//   PlayerDodgePrefabAdder.cs
//   PlayerStaminaPrefabAdder.cs
//   PlayerTakesDamagePrefabAdder.cs
//   PlayerDeathPrefabAdder.cs
//   PlayerInteractorPrefabAdder.cs
//   PlayerCapsuleTuner.cs
//
// What this builder does NOT do:
//   - Build a WeaponHitbox child. As of M20b, the weapon WorldPrefab itself
//     acts as the hitbox — each weapon prefab must carry its own Collider
//     (isTrigger=true), kinematic Rigidbody, and HitboxRelay. The static
//     WeaponHitbox child under weapon_r has been retired.
//   - Build the HUD prefab or wire CharacterStats_Player.asset assignment
//     (owned by PlayerHUDBuilder).
//   - Place the player in a scene or set up the Cinemachine camera
//     (owned by the M2-A camera menu — unchanged).
//   - Bake the NavMesh (owned by the M10 menu).
//
// Why programmatic? UnityEvent persistent listeners survive prefab
// save/reload only when written via SerializedObject manipulation of
// m_PersistentCalls. Hand-configured prefabs are easy to drift; this
// script is the canonical rebuild path.
//
// Menu: LevelGen ▶ Player ▶ Build Player_Hero Prefab

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using LevelGen.Combat;
using LevelGen.Input;

namespace LevelGen.Player.Editor
{
    public static class PlayerHeroBuilder
    {
        // ── Paths ────────────────────────────────────────────────────────────
        private const string PrefabFolder      = "Assets/Prefabs/Character Prefabs/Player";
        private const string OldPrefabPath     = PrefabFolder + "/Player_MaleHero.prefab";
        private const string NewPrefabPath     = PrefabFolder + "/Player_Hero.prefab";
        private const string NewPrefabName     = "Player_Hero";
        private const string PackPrefabPath    = "Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab";
        private const string ActionsAssetPath  = "Assets/InputSystem_Actions.inputactions";
        private const string OverrideCtrlPath  = "Assets/Animators/Player/PlayerOverride_MaleHero.overrideController";

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
            ("Dodge",    "OnDodge"),
            ("LockOn",   "OnLockOn"),
        };

        [MenuItem("LevelGen/Player/Build Player_Hero Prefab")]
        public static void Build()
        {
            // ── Step 0: rename Player_MaleHero.prefab → Player_Hero.prefab ──
            //          (GUID-preserving; scene references auto-resolve)
            RenameOldPrefabIfNeeded();

            // ── Step 1: locate or create the prefab ───────────────────────
            EnsureFolder("Assets/Prefabs/Character Prefabs", "Player");
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(NewPrefabPath);
            bool prefabCreatedNow = false;
            if (prefabAsset == null)
            {
                prefabAsset = CreateFreshPrefab();
                if (prefabAsset == null) return; // CreateFreshPrefab logged its own error
                prefabCreatedNow = true;
            }

            // ── Step 2: load editable contents ────────────────────────────
            var contents = PrefabUtility.LoadPrefabContents(NewPrefabPath);
            try
            {
                // ── Step 3: ensure all root components present ────────────
                EnsureTagPlayer(contents);
                EnsureCharacterController(contents);
                EnsurePlayerInput(contents);
                EnsureNestedCharacterChild(contents);
                EnsureCameraTargetChild(contents);

                var added = new System.Collections.Generic.List<string>();
                var already = new System.Collections.Generic.List<string>();
                // Component-add order: Unity auto-adds via [RequireComponent],
                // but explicit order keeps logs readable.
                AddIfMissing<PlayerInputReader>(contents, added, already);
                AddIfMissing<PlayerAnimator>(contents, added, already);
                AddIfMissing<PlayerController>(contents, added, already);
                AddIfMissing<PlayerCombat>(contents, added, already);
                AddIfMissing<CharacterStatsRuntime>(contents, added, already);
                AddIfMissing<Targetable>(contents, added, already);
                AddIfMissing<PlayerHitReaction>(contents, added, already);
                AddIfMissing<PlayerDeath>(contents, added, already);
                AddIfMissing<PlayerInteractor>(contents, added, already);
                AddIfMissing<PlayerStamina>(contents, added, already);
                AddIfMissing<PlayerDodge>(contents, added, already);
                AddIfMissing<MouseLook>(contents, added, already);
                AddIfMissing<TargetLock>(contents, added, already);
                AddIfMissing<PlayerEquipmentVisuals>(contents, added, already);
                AddIfMissing<PlayerHero>(contents, added, already);

                // ── Step 4: wire PlayerHero SerializeField refs ───────────
                WirePlayerHeroRefs(contents);

                // ── Step 5: wire PlayerInputReader UnityEvent listeners ───
                int wired = WirePlayerInputUnityEvents(contents);
                if (wired != s_Bindings.Length)
                {
                    Debug.LogError($"[PlayerHeroBuilder] UnityEvent wiring failed: " +
                                   $"{wired}/{s_Bindings.Length} listeners attached. Aborting save.");
                    return;
                }

                // ── Step 6: nested-Animator config (controller + flags) ───
                ConfigureNestedAnimator(contents);

                // ── Step 7: CharacterController tuning (M11 Q5 inline) ────
                TuneCharacterController(contents);

                // ── Step 8: save the prefab ───────────────────────────────
                PrefabUtility.SaveAsPrefabAsset(contents, NewPrefabPath);

                Debug.Log(
                    $"[PlayerHeroBuilder] Player_Hero prefab build complete.\n" +
                    $"  Path: {NewPrefabPath}\n" +
                    $"  Prefab status: {(prefabCreatedNow ? "CREATED" : "REWIRED")}\n" +
                    $"  Components added now: {string.Join(", ", added)}\n" +
                    $"  Components already present: {string.Join(", ", already)}\n" +
                    $"  UnityEvent bindings: {wired}/{s_Bindings.Length}\n" +
                    $"  Tag: Player; CameraTarget at local (0, 1.6, 0); " +
                    $"CC radius=0.4 (M11)."
                );
                Debug.Log(
                    "[PlayerHeroBuilder] PlayerEquipmentVisuals added — " +
                    "wire _weaponSocket manually (drag weapon_r bone in Inspector)."
                );

                // Reload + select for visual confirmation
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(NewPrefabPath);
                if (reloaded != null)
                {
                    Selection.activeObject = reloaded;
                    EditorGUIUtility.PingObject(reloaded);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Step 0: GUID-preserving rename
        // ════════════════════════════════════════════════════════════════════

        private static void RenameOldPrefabIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefabPath) == null)
            {
                // Old name not present — nothing to rename.
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NewPrefabPath) != null)
            {
                // Already renamed in a prior run — collision; leave old alone.
                Debug.LogWarning($"[PlayerHeroBuilder] Both {OldPrefabPath} and {NewPrefabPath} exist. " +
                                 "Skipping rename — manually resolve which is canonical.");
                return;
            }
            string error = AssetDatabase.RenameAsset(OldPrefabPath, NewPrefabName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[PlayerHeroBuilder] Rename failed: {error}");
                return;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerHeroBuilder] Renamed {OldPrefabPath} → {NewPrefabPath} (GUID preserved).");
        }

        // ════════════════════════════════════════════════════════════════════
        // First-build path: create the prefab from scratch when it doesn't exist
        // ════════════════════════════════════════════════════════════════════

        private static GameObject CreateFreshPrefab()
        {
            var root = new GameObject(NewPrefabName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // PlayerHero forces every required sibling via [RequireComponent].
            // Adding it first triggers Unity to auto-add the chain.
            root.AddComponent<PlayerHero>();

            bool ok;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, NewPrefabPath, out ok);
            Object.DestroyImmediate(root);
            if (!ok || saved == null)
            {
                Debug.LogError($"[PlayerHeroBuilder] First-build SaveAsPrefabAsset failed at {NewPrefabPath}.");
                return null;
            }
            return saved;
        }

        // ════════════════════════════════════════════════════════════════════
        // Per-component idempotent add helpers
        // ════════════════════════════════════════════════════════════════════

        private static void AddIfMissing<T>(GameObject go,
                                            System.Collections.Generic.List<string> added,
                                            System.Collections.Generic.List<string> already)
            where T : Component
        {
            if (go.GetComponent<T>() != null)
            {
                already.Add(typeof(T).Name);
                return;
            }
            go.AddComponent<T>();
            added.Add(typeof(T).Name);
        }

        private static void EnsureTagPlayer(GameObject root)
        {
            if (root.CompareTag("Player")) return;
            try { root.tag = "Player"; }
            catch (UnityException)
            {
                Debug.LogWarning("[PlayerHeroBuilder] 'Player' tag not found in TagManager. Leaving root Untagged.");
            }
        }

        private static void EnsureCharacterController(GameObject root)
        {
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.slopeLimit      = 45f;
            cc.stepOffset      = 0.3f;
            cc.skinWidth       = 0.08f;
            cc.minMoveDistance = 0.001f;
            cc.center          = new Vector3(0f, 0.9f, 0f);
            cc.height          = 1.8f;
            // radius set in TuneCharacterController (M11 Q5)
        }

        private static void TuneCharacterController(GameObject root)
        {
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) return;
            // M11 Q5 — symmetric reach for hit reception. 0.4 matches
            // the Dummy's CapsuleCollider so swings consistently land.
            cc.radius = 0.4f;
        }

        private static void EnsurePlayerInput(GameObject root)
        {
            var ipi = root.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (ipi == null) ipi = root.AddComponent<UnityEngine.InputSystem.PlayerInput>();

            var actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
            if (actionAsset == null)
            {
                Debug.LogError($"[PlayerHeroBuilder] InputActionAsset missing at {ActionsAssetPath}.");
                return;
            }
            ipi.actions              = actionAsset;
            ipi.defaultActionMap     = "Player";
            ipi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
        }

        private static void EnsureNestedCharacterChild(GameObject root)
        {
            // Skip if MaleCharacterPBR child already exists.
            if (root.transform.Find("MaleCharacterPBR") != null) return;

            var packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath);
            if (packPrefab == null)
            {
                Debug.LogError($"[PlayerHeroBuilder] Pack prefab missing at {PackPrefabPath}. " +
                               "Nested character rig will not be added.");
                return;
            }
            var character = (GameObject)PrefabUtility.InstantiatePrefab(packPrefab, root.transform);
            if (character == null)
            {
                Debug.LogError("[PlayerHeroBuilder] InstantiatePrefab returned null for MaleCharacterPBR.");
                return;
            }
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale    = Vector3.one;
        }

        private static void EnsureCameraTargetChild(GameObject root)
        {
            if (root.transform.Find("CameraTarget") != null) return;
            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(root.transform, worldPositionStays: false);
            camTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camTarget.transform.localRotation = Quaternion.identity;
            camTarget.transform.localScale    = Vector3.one;
        }

        private static void ConfigureNestedAnimator(GameObject root)
        {
            var character = root.transform.Find("MaleCharacterPBR");
            if (character == null) return;
            var animator = character.GetComponent<Animator>();
            if (animator == null) return;

            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverrideCtrlPath);
            if (overrideController == null)
            {
                Debug.LogError($"[PlayerHeroBuilder] Override controller missing at {OverrideCtrlPath}. " +
                               "Nested Animator will retain its current runtimeAnimatorController.");
                return;
            }
            animator.runtimeAnimatorController = overrideController;
            animator.applyRootMotion           = false;
            animator.updateMode                = AnimatorUpdateMode.Normal;
            animator.cullingMode               = AnimatorCullingMode.CullUpdateTransforms;
            EditorUtility.SetDirty(animator);
        }

        // ════════════════════════════════════════════════════════════════════
        // PlayerHero ref wiring
        // ════════════════════════════════════════════════════════════════════

        private static void WirePlayerHeroRefs(GameObject root)
        {
            var hero = root.GetComponent<PlayerHero>();
            if (hero == null)
            {
                Debug.LogError("[PlayerHeroBuilder] PlayerHero is missing post-AddIfMissing. " +
                               "Cannot wire SerializeField refs.");
                return;
            }
            var so = new SerializedObject(hero);
            WireProp(so, "_characterController", root.GetComponent<CharacterController>());
            WireProp(so, "_stats",               root.GetComponent<CharacterStatsRuntime>());
            WireProp(so, "_targetable",          root.GetComponent<Targetable>());
            WireProp(so, "_playerInput",         root.GetComponent<UnityEngine.InputSystem.PlayerInput>());
            WireProp(so, "_input",               root.GetComponent<PlayerInputReader>());
            WireProp(so, "_mouseLook",           root.GetComponent<MouseLook>());
            WireProp(so, "_controller",          root.GetComponent<PlayerController>());
            WireProp(so, "_animator",            root.GetComponent<PlayerAnimator>());
            WireProp(so, "_stamina",             root.GetComponent<PlayerStamina>());
            WireProp(so, "_dodge",               root.GetComponent<PlayerDodge>());
            WireProp(so, "_combat",              root.GetComponent<PlayerCombat>());
            WireProp(so, "_hitReaction",         root.GetComponent<PlayerHitReaction>());
            WireProp(so, "_death",               root.GetComponent<PlayerDeath>());
            WireProp(so, "_interactor",          root.GetComponent<PlayerInteractor>());
            WireProp(so, "_equipmentVisuals",    root.GetComponent<PlayerEquipmentVisuals>());
            WireProp(so, "_targetLock",          root.GetComponent<TargetLock>());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hero);

            // Also wire PlayerDeath's three SerializeField refs (legacy
            // wiring from M5; PlayerHero has them too, but PlayerDeath
            // uses its own fields internally).
            var death = root.GetComponent<PlayerDeath>();
            if (death != null)
            {
                var dso = new SerializedObject(death);
                WireProp(dso, "_animator",   root.GetComponent<PlayerAnimator>());
                WireProp(dso, "_controller", root.GetComponent<PlayerController>());
                WireProp(dso, "_combat",     root.GetComponent<PlayerCombat>());
                dso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(death);
            }
        }

        private static void WireProp(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[PlayerHeroBuilder] Field '{fieldName}' not found on " +
                                 $"{so.targetObject.GetType().Name}. Skipping.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        // ════════════════════════════════════════════════════════════════════
        // UnityEvent wiring (rebuilds m_ActionEvents on PlayerInput)
        // ════════════════════════════════════════════════════════════════════

        private static int WirePlayerInputUnityEvents(GameObject root)
        {
            var ipi = root.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            var reader = root.GetComponent<PlayerInputReader>();
            if (ipi == null || reader == null)
            {
                Debug.LogError("[PlayerHeroBuilder] PlayerInput or PlayerInputReader missing — cannot wire UnityEvents.");
                return 0;
            }
            var actionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
            if (actionAsset == null) return 0;

            var playerMap = actionAsset.FindActionMap("Player", throwIfNotFound: false);
            if (playerMap == null)
            {
                Debug.LogError("[PlayerHeroBuilder] No 'Player' action map in InputActions asset.");
                return 0;
            }

            var readerType = typeof(PlayerInputReader);
            string targetTypeName = $"{readerType.FullName}, {readerType.Assembly.GetName().Name}";

            var serialized = new SerializedObject(ipi);
            serialized.Update();
            var eventsArray = serialized.FindProperty("m_ActionEvents");
            if (eventsArray == null)
            {
                Debug.LogError("[PlayerHeroBuilder] PlayerInput has no m_ActionEvents serialized field.");
                return 0;
            }
            eventsArray.ClearArray();

            int wiredOk = 0;
            for (int i = 0; i < s_Bindings.Length; i++)
            {
                var (actionName, methodName) = s_Bindings[i];
                var mi = readerType.GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (mi == null)
                {
                    Debug.LogError($"[PlayerHeroBuilder] Method '{methodName}' not found on PlayerInputReader.");
                    continue;
                }
                var action = playerMap.FindAction(actionName, throwIfNotFound: false);
                if (action == null)
                {
                    Debug.LogError($"[PlayerHeroBuilder] Action '{actionName}' not found in Player map.");
                    continue;
                }

                eventsArray.InsertArrayElementAtIndex(i);
                var entry = eventsArray.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("m_ActionId").stringValue   = action.id.ToString();
                entry.FindPropertyRelative("m_ActionName").stringValue = $"Player/{action.name}[{action.id}]";

                var callsArray = entry.FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (callsArray == null) continue;
                callsArray.ClearArray();
                callsArray.InsertArrayElementAtIndex(0);
                var call = callsArray.GetArrayElementAtIndex(0);

                call.FindPropertyRelative("m_Target").objectReferenceValue           = reader;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue    = targetTypeName;
                call.FindPropertyRelative("m_MethodName").stringValue                = methodName;
                call.FindPropertyRelative("m_Mode").intValue                         = 0; // EventDefined
                call.FindPropertyRelative("m_CallState").intValue                    = 2; // RuntimeOnly

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
            return wiredOk;
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
