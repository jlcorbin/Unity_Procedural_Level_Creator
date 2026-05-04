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
using UnityEngine.AI;
using LevelGen.Combat;
using LevelGen.Interaction;

namespace LevelGen.Combat.EditorTools
{
    public static class DummyPrefabBuilder
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string DummyPrefabPath  = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string PackPrefabPath   = "Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab";
        private const string BaseCtrlPath     = "Assets/Animators/Enemy/EnemyBaseController.controller";
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
                Debug.LogError($"[DummyPrefabBuilder] Cannot load base controller at {BaseCtrlPath}. " +
                               "Run 'LevelGen ▶ Combat ▶ Build EnemyBaseController' first. Aborting.");
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

            // Body collider — the trigger collider that PlayerCombat's
            // weapon hitbox enters during a swing. Values mirror
            // PlayerCombatHitboxBuilder.AddColliderToDummy (the original
            // M3 authoring step). Folded into the build so rebuilds don't
            // drop the capsule.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.radius    = 0.4f;
            capsule.height    = 1.8f;
            capsule.center    = new Vector3(0f, 0.9f, 0f);
            capsule.direction = 1; // Y axis

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

            // ── EnemyHitReaction (sole writer to the Hit Animator parameter) ─
            // Added after the Animator is wired so the field-binding finds it.
            // Re-running Build creates a new root, so this is naturally
            // idempotent — no AddComponent guard needed.
            var hitReaction = root.AddComponent<EnemyHitReaction>();
            AssignAnimatorField(hitReaction, animator);

            // ── EnemyDeath (sole writer to the Death Animator parameter) ────
            // Subscribes to CharacterStatsRuntime.OnDied at runtime; on
            // death disables Targetable + Collider + EnemyHitReaction and
            // schedules Destroy with despawnDelay (5f default).
            var enemyDeath = root.AddComponent<EnemyDeath>();
            AssignEnemyDeathRefs(enemyDeath, animator, capsule, hitReaction);

            // ── M10: NavMeshAgent (movement) ────────────────────────────────
            // Authoritative for position once added — Dummy must NOT also
            // gain a CharacterController. Height + radius mirror the
            // CapsuleCollider so the agent's collision footprint matches
            // its physics footprint.
            var agent = root.AddComponent<NavMeshAgent>();
            agent.height            = 1.8f;
            agent.radius            = 0.4f;
            agent.baseOffset        = 0f;
            agent.speed             = 2.5f;   // matches EnemyAI._chaseSpeed default
            agent.angularSpeed      = 540f;
            agent.acceleration      = 12f;
            agent.stoppingDistance  = 1.0f;   // matches EnemyAI._stoppingDistance default (post-M11 tune)
            agent.autoBraking       = true;
            agent.updateRotation    = true;
            agent.updatePosition    = true;

            // ── M10: EnemyAI (FSM + sole writer to MoveSpeed + Attack) ──────
            // Subscribes to nothing; reads player by tag at Awake. _animator
            // is wired via SerializedObject (Reset() doesn't fire on
            // programmatic AddComponent — M6 lesson).
            var enemyAI = root.AddComponent<EnemyAI>();
            AssignEnemyAIRefs(enemyAI, animator);

            // ── M11: EnemyCombat (sole owner of enemy weapon hitbox) ────────
            // Replaces the M10 EnemyAnimationEventAbsorber stub. Owns the
            // hitbox enable/disable + per-swing hit list + damage routing.
            // The _hitbox SerializeField is wired below after the
            // EnemyWeaponHitbox child is built.
            var enemyCombat = root.AddComponent<EnemyCombat>();

            // ── M11: EnemyAnimationEventForwarder on the Animator's GO ──────
            // Replaces the M10 absorber with a real consumer. Forwards
            // OnHitboxOpen/Close events from Attack01_SwordAndShiled to
            // EnemyCombat on the prefab root (Unity dispatches AnimationEvents
            // to the Animator's GO only — M4-A lesson).
            var forwarder = character.AddComponent<EnemyAnimationEventForwarder>();
            AssignForwarderCombatRef(forwarder, enemyCombat);

            // ── M11: EnemyWeaponHitbox child under weapon_r ─────────────────
            // Mirror of Player's WeaponHitbox setup. Trigger BoxCollider
            // (default disabled) + kinematic Rigidbody (REQUIRED for
            // OnTriggerEnter to fire on a moving collider — M3 lesson) +
            // EnemyHitboxRelay routing into EnemyCombat. Wires
            // EnemyCombat._hitbox back to this BoxCollider as the final step.
            BuildEnemyWeaponHitbox(character, enemyCombat);

            // ── M6: _AssassinateZone child (interact system) ────────────────
            // Sphere trigger + AssassinateInteractable on a separate child
            // GameObject so it doesn't share the body collider's physics
            // semantics. Prompt UI is auto-built by Interactable.Awake but
            // we also call EnsurePromptUI now for editor-time visibility.
            BuildAssassinateZone(root, runtime);

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
                $"  Components on root:    CharacterStatsRuntime, Targetable, CapsuleCollider, EnemyHitReaction, EnemyDeath, NavMeshAgent, EnemyAI, EnemyCombat\n" +
                $"  Components on child:   EnemyAnimationEventForwarder (M11; replaced M10 absorber)\n" +
                $"  Children:              _AssassinateZone (SphereCollider trigger + AssassinateInteractable),\n" +
                $"                         weapon_r/EnemyWeaponHitbox (trigger BoxCollider + kinematic Rigidbody + EnemyHitboxRelay)\n" +
                $"  Drop into a scene with a baked NavMesh ('LevelGen ▶ Combat ▶ Bake Test Scene NavMesh') and press Play."
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
            asset.maxHP       = 50;
            asset.maxStamina  = 100;
            asset.description = "Stationary target practice. Dies on the 2nd hit of a 2nd combo " +
                                "(full 3-hit combo = 30 dmg) so the M4-B death sequence triggers organically.";

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

        /// <summary>
        /// Assigns the private <c>animator</c> SerializeField on an
        /// EnemyHitReaction (cross-assembly write — Editor assembly can't
        /// touch the private field directly).
        /// </summary>
        private static void AssignAnimatorField(EnemyHitReaction reaction, Animator animator)
        {
            var so = new SerializedObject(reaction);
            var prop = so.FindProperty("animator");
            if (prop == null)
            {
                Debug.LogError("[DummyPrefabBuilder] EnemyHitReaction has no 'animator' serialized field.");
                return;
            }
            prop.objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reaction);
        }

        /// <summary>
        /// Wires the three SerializeField references on EnemyDeath
        /// (animator, deathCollider, hitReaction) at build time. Despawn
        /// delay keeps its inspector default (5f).
        /// </summary>
        private static void AssignEnemyDeathRefs(EnemyDeath death, Animator animator,
                                                 Collider deathCollider, EnemyHitReaction hitReaction)
        {
            var so = new SerializedObject(death);
            void Wire(string fieldName, Object value)
            {
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                {
                    Debug.LogError($"[DummyPrefabBuilder] EnemyDeath has no '{fieldName}' serialized field.");
                    return;
                }
                prop.objectReferenceValue = value;
            }
            Wire("animator",      animator);
            Wire("deathCollider", deathCollider);
            Wire("hitReaction",   hitReaction);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(death);
        }

        /// <summary>
        /// Wires the private <c>_combat</c> SerializeField on the M11
        /// EnemyAnimationEventForwarder via SerializedObject. The
        /// Forwarder's Reset() resolves it via GetComponentInParent,
        /// but Reset doesn't fire on programmatic AddComponent (M6
        /// lesson) — wire explicitly.
        /// </summary>
        private static void AssignForwarderCombatRef(EnemyAnimationEventForwarder forwarder, EnemyCombat combat)
        {
            var so = new SerializedObject(forwarder);
            var prop = so.FindProperty("_combat");
            if (prop == null)
            {
                Debug.LogError("[DummyPrefabBuilder] EnemyAnimationEventForwarder has no '_combat' serialized field.");
                return;
            }
            prop.objectReferenceValue = combat;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(forwarder);
        }

        // ── M11: EnemyWeaponHitbox child build ──────────────────────────────

        // Mirror of Player WeaponHitbox dimensions (PlayerCombatHitboxBuilder).
        // Same rig, same weapon mesh — same numbers. If a future enemy uses a
        // different weapon, fork these per-prefab.
        private static readonly Vector3 EnemyHitboxSize   = new Vector3(0.15f, 0.15f, 0.8f);
        private static readonly Vector3 EnemyHitboxCenter = new Vector3(0f, 0f, 0.4f);
        private const string EnemyHitboxName = "EnemyWeaponHitbox";

        // Same bone-name candidates as Player. SwordAndShield is right-handed.
        private static readonly string[] WeaponAttachCandidates =
            { "weapon_r", "weapon_l", "Weapon_R", "Weapon_L" };

        /// <summary>
        /// Builds the EnemyWeaponHitbox child under the Dummy's weapon_r
        /// bone: trigger BoxCollider (default disabled), kinematic
        /// Rigidbody (REQUIRED for OnTriggerEnter to fire on a child
        /// collider that moves via skeletal animation — M3 lesson),
        /// EnemyHitboxRelay routing into EnemyCombat. Wires
        /// EnemyCombat._hitbox back to the BoxCollider as the final step.
        /// </summary>
        private static void BuildEnemyWeaponHitbox(GameObject character, EnemyCombat combat)
        {
            Transform attach = null;
            foreach (var candidate in WeaponAttachCandidates)
            {
                attach = FindByNameRecursive(character.transform, candidate);
                if (attach != null) break;
            }
            if (attach == null)
            {
                Debug.LogError("[DummyPrefabBuilder] Could not find a weapon attach Transform " +
                               $"in the Dummy's MaleCharacterPBR hierarchy. Looked for: " +
                               $"{string.Join(", ", WeaponAttachCandidates)}. Aborting hitbox build — " +
                               "Dummy attacks will swing but produce no damage.");
                return;
            }

            var hitboxGO = new GameObject(EnemyHitboxName);
            hitboxGO.transform.SetParent(attach, worldPositionStays: false);
            hitboxGO.transform.localPosition = Vector3.zero;
            hitboxGO.transform.localRotation = Quaternion.identity;
            hitboxGO.transform.localScale    = Vector3.one;

            // Trigger BoxCollider — default disabled, opened by OnHitboxOpen.
            var box = hitboxGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled   = false;
            box.size      = EnemyHitboxSize;
            box.center    = EnemyHitboxCenter;

            // Kinematic Rigidbody — required for OnTriggerEnter on a moving
            // child collider. CharacterController/CapsuleCollider on the root
            // doesn't promote deeply-nested child triggers to "moving" status
            // (M3 PlayerCombat lesson). Without this, no hits register.
            var rb = hitboxGO.AddComponent<Rigidbody>();
            rb.isKinematic            = true;
            rb.useGravity             = false;
            rb.interpolation          = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            // EnemyHitboxRelay — wires its _combat ref to the EnemyCombat
            // on the Dummy root.
            var relay = hitboxGO.AddComponent<EnemyHitboxRelay>();
            var relaySo = new SerializedObject(relay);
            var relayProp = relaySo.FindProperty("_combat");
            if (relayProp != null)
            {
                relayProp.objectReferenceValue = combat;
                relaySo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(relay);
            }

            // EnemyCombat._hitbox → the BoxCollider on the new child.
            var combatSo = new SerializedObject(combat);
            var combatProp = combatSo.FindProperty("_hitbox");
            if (combatProp != null)
            {
                combatProp.objectReferenceValue = box;
                combatSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(combat);
            }
        }

        private static Transform FindByNameRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindByNameRecursive(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>
        /// Wires the private <c>_animator</c> SerializeField on EnemyAI
        /// (M10) via SerializedObject. Other EnemyAI fields keep their
        /// SerializeField defaults — tunables (ranges, speeds, cooldown)
        /// are intentionally inspector-tunable, not builder-baked.
        /// </summary>
        private static void AssignEnemyAIRefs(EnemyAI ai, Animator animator)
        {
            var so = new SerializedObject(ai);
            var prop = so.FindProperty("_animator");
            if (prop == null)
            {
                Debug.LogError("[DummyPrefabBuilder] EnemyAI has no '_animator' serialized field.");
                return;
            }
            prop.objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ai);
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

        /// <summary>
        /// Builds the M6 _AssassinateZone child: a child GameObject with
        /// a SphereCollider (trigger) + AssassinateInteractable, with the
        /// _targetStats + _targetTransform refs wired to the parent
        /// (which holds CharacterStatsRuntime), prompt anchor pointed at
        /// a head-height transform on the body. Idempotent within the
        /// clean-rebuild pattern (always called on a fresh root).
        /// </summary>
        private static void BuildAssassinateZone(GameObject root, CharacterStatsRuntime runtime)
        {
            var zone = new GameObject("_AssassinateZone");
            zone.transform.SetParent(root.transform, worldPositionStays: false);
            zone.transform.localPosition = Vector3.zero;
            zone.transform.localRotation = Quaternion.identity;
            zone.transform.localScale    = Vector3.one;

            var sc = zone.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius    = 1.5f;
            sc.center    = new Vector3(0f, 0.9f, 0f); // mid-body height

            var assassinate = zone.AddComponent<AssassinateInteractable>();
            // Reset() doesn't fire on programmatic AddComponent — wire the
            // SerializeField refs explicitly. _promptAnchor goes on a
            // head-height transform so the prompt floats above the head.
            var headAnchor = new GameObject("_PromptAnchor_Head");
            headAnchor.transform.SetParent(zone.transform, worldPositionStays: false);
            headAnchor.transform.localPosition = new Vector3(0f, 1.9f, 0f);

            AssignAssassinateRefs(assassinate, runtime, root.transform, headAnchor.transform);

            // Build the prompt child up-front so it's visible in the
            // prefab inspector (otherwise it's only built on Awake at
            // runtime).
            assassinate.EnsurePromptUI();
        }

        /// <summary>
        /// Wires AssassinateInteractable's three SerializeField refs via
        /// SerializedObject. Reset() does not fire on programmatic
        /// AddComponent — Awake fallbacks aren't relevant here either
        /// since the validator reads the serialized values.
        /// </summary>
        private static void AssignAssassinateRefs(AssassinateInteractable target,
                                                  CharacterStatsRuntime targetStats,
                                                  Transform targetTransform,
                                                  Transform promptAnchor)
        {
            var so = new SerializedObject(target);
            void Wire(string fieldName, Object value)
            {
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                {
                    Debug.LogError($"[DummyPrefabBuilder] AssassinateInteractable has no '{fieldName}' field.");
                    return;
                }
                prop.objectReferenceValue = value;
            }
            Wire("_targetStats",     targetStats);
            Wire("_targetTransform", targetTransform);
            Wire("_promptAnchor",    promptAnchor);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
#endif
