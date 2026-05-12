// EnemyBaseBuilder.cs — M13-EnemyBase canonical builder for Enemy_Grunt.prefab.
//
// Enemy equivalent of PlayerHeroBuilder. Single idempotent menu that:
//   1. Ensures EnemyData_Grunt.asset exists at Assets/Data/EnemyData/
//   2. Creates / rebuilds Enemy_Grunt.prefab from scratch
//   3. Wires every EnemyBase SerializeField ref via SerializedObject
//   4. Wires EnemyData → EnemyBase, EnemyCombat._hitbox → BoxCollider,
//      EnemyAnimationEventForwarder._combat → EnemyCombat,
//      EnemyHitboxRelay._combat → EnemyCombat
//
// Dummy.prefab is intentionally left UNTOUCHED. Enemy_Grunt is a clean
// production-grade prefab built on top of EnemyBase. Dummy remains as
// the sandbox / smoke-test target. Both prefabs reference the same
// EnemyBaseController and the same MaleCharacterPBR rig.
//
// Menu: LevelGen ▶ Combat ▶ Build Enemy_Grunt Prefab
// Menu: LevelGen ▶ Combat ▶ Place Enemy_Grunt in Active Scene

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using LevelGen.Combat;
using LevelGen.Interaction;

namespace LevelGen.Enemy.EditorTools
{
    public static class EnemyBaseBuilder
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string PrefabFolder      = "Assets/Prefabs/Character Prefabs/Enemy";
        private const string EnemyPrefabPath   = PrefabFolder + "/Enemy_Grunt.prefab";
        private const string PackPrefabPath    = "Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab";
        private const string BaseCtrlPath      = "Assets/Animators/Enemy/EnemyBaseController.controller";
        private const string EnemyDataFolder   = "Assets/Data/EnemyData";
        private const string GruntDataPath     = "Assets/Data/EnemyData/EnemyData_Grunt.asset";

        // EnemyWeaponHitbox dimensions — mirror of the Dummy / Player
        // weapon hitbox. Same rig, same weapon mesh, same numbers.
        private static readonly Vector3 EnemyHitboxSize   = new Vector3(0.15f, 0.15f, 0.8f);
        private static readonly Vector3 EnemyHitboxCenter = new Vector3(0f, 0f, 0.4f);
        private const string EnemyHitboxName = "EnemyWeaponHitbox";

        // Same bone-name candidates the Dummy + Player builders use.
        private static readonly string[] WeaponAttachCandidates =
            { "weapon_r", "weapon_l", "Weapon_R", "Weapon_L" };

        // ════════════════════════════════════════════════════════════════════
        // Menu: build the Enemy_Grunt prefab + data asset
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Build Enemy_Grunt Prefab")]
        public static void Build()
        {
            // ── Step 1: ensure EnemyData_Grunt.asset ────────────────────────
            var gruntData = EnsureGruntData();
            if (gruntData == null) return;

            // ── Step 2: ensure source assets exist ──────────────────────────
            var packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabPath);
            if (packPrefab == null)
            {
                Debug.LogError($"[EnemyBaseBuilder] Pack prefab missing at {PackPrefabPath}. Aborting.");
                return;
            }

            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseCtrlPath);
            if (baseController == null)
            {
                Debug.LogError($"[EnemyBaseBuilder] Base controller missing at {BaseCtrlPath}. " +
                               "Run 'LevelGen ▶ Combat ▶ Build EnemyBaseController' first. Aborting.");
                return;
            }

            // ── Step 3: ensure target folder ────────────────────────────────
            EnsureFolder("Assets/Prefabs/Character Prefabs", "Enemy");

            // ── Step 4: idempotent overwrite (clean-rebuild pattern) ────────
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(EnemyPrefabPath);
            }

            // ── Step 5: build the in-memory hierarchy ───────────────────────
            var root = new GameObject("Enemy_Grunt");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Body collider — same dimensions as Dummy. The CapsuleCollider
            // is what the Player's WeaponHitbox enters during a swing.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = false;
            capsule.radius    = 0.4f;
            capsule.height    = 1.8f;
            capsule.center    = new Vector3(0f, 0.9f, 0f);
            capsule.direction = 1; // Y axis

            // NavMeshAgent — speed + stoppingDistance get overridden at
            // runtime by EnemyBase from EnemyData, but seed sensible
            // inspector values for read consistency.
            var agent = root.AddComponent<NavMeshAgent>();
            agent.height            = 1.8f;
            agent.radius            = 0.4f;
            agent.baseOffset        = 0f;
            agent.speed             = gruntData.moveSpeed;
            agent.angularSpeed      = 540f;
            agent.acceleration      = 12f;
            agent.stoppingDistance  = gruntData.stoppingDistance;
            agent.autoBraking       = true;
            agent.updateRotation    = true;
            agent.updatePosition    = true;

            // Stats runtime — leave the CharacterStats SO null. EnemyBase
            // pushes EnemyData_Grunt into it via InitFromEnemyData at Awake;
            // CharacterStatsRuntime.Awake reads _enemyData and inits HP
            // from it (player path still uses the SO).
            var runtime = root.AddComponent<CharacterStatsRuntime>();

            // Targetable — bidirectional event publisher (M8).
            root.AddComponent<Targetable>();

            // Nested visible model — same MaleCharacterPBR rig the Dummy
            // and Player both use.
            var character = (GameObject)PrefabUtility.InstantiatePrefab(packPrefab, root.transform);
            if (character == null)
            {
                Debug.LogError("[EnemyBaseBuilder] InstantiatePrefab returned null for the pack prefab. Aborting.");
                Object.DestroyImmediate(root);
                return;
            }
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            character.transform.localScale    = Vector3.one;

            // Animator on the model child — points at EnemyBaseController.
            var animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[EnemyBaseBuilder] MaleCharacterPBR child has no Animator. Aborting.");
                Object.DestroyImmediate(root);
                return;
            }
            animator.runtimeAnimatorController = baseController;
            animator.applyRootMotion = false;
            animator.updateMode      = AnimatorUpdateMode.Normal;
            animator.cullingMode     = AnimatorCullingMode.CullUpdateTransforms;
            EditorUtility.SetDirty(animator);

            // EnemyHitReaction (sole writer to Hit Animator parameter, M4-A).
            var hitReaction = root.AddComponent<EnemyHitReaction>();
            WireSerializedRef(hitReaction, "animator", animator);

            // EnemyDeath (sole writer to Death Animator parameter, M4-B).
            var enemyDeath = root.AddComponent<EnemyDeath>();
            WireSerializedRef(enemyDeath, "animator",      animator);
            WireSerializedRef(enemyDeath, "deathCollider", capsule);
            WireSerializedRef(enemyDeath, "hitReaction",   hitReaction);

            // EnemyAI (FSM, sole writer to MoveSpeed + Attack, M10).
            var enemyAI = root.AddComponent<EnemyAI>();
            WireSerializedRef(enemyAI, "_animator", animator);

            // EnemyCombat (sole owner of weapon hitbox + damage routing, M11).
            var enemyCombat = root.AddComponent<EnemyCombat>();

            // EnemyAnimationEventForwarder on the Animator's GO — relays
            // OnHitboxOpen / OnHitboxClose to EnemyCombat on the root.
            var forwarder = character.AddComponent<EnemyAnimationEventForwarder>();
            WireSerializedRef(forwarder, "_combat", enemyCombat);

            // EnemyWeaponHitbox child under weapon_r.
            BuildEnemyWeaponHitbox(character, enemyCombat);

            // _AssassinateZone child (M6 interact system). Mirror of
            // DummyPrefabBuilder.BuildAssassinateZone — Enemy_Grunt is now
            // the canonical assassinate target after Dummy is retired.
            BuildAssassinateZone(root, runtime);

            // EnemyBase manifest — last so all sibling refs resolve.
            var enemyBase = root.AddComponent<EnemyBase>();
            WireEnemyBaseRefs(enemyBase, gruntData, runtime, root.GetComponent<Targetable>(),
                              enemyAI, enemyCombat, hitReaction, enemyDeath,
                              animator, agent, capsule);

            // ── Step 6: save the prefab ─────────────────────────────────────
            bool success;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath, out success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[EnemyBaseBuilder] SaveAsPrefabAsset failed.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Step 7: select + log ────────────────────────────────────────
            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[EnemyBaseBuilder] Built {EnemyPrefabPath}.\n" +
                $"  EnemyData_Grunt: {gruntData.name} (HP={gruntData.maxHP}, " +
                $"attackDmg={gruntData.attackDamage}, " +
                $"detect={gruntData.detectionRange}, attack={gruntData.attackRange}, leash={gruntData.leashRange})\n" +
                $"  Animator controller: {baseController.name}\n" +
                $"  Components on root:  EnemyBase, CapsuleCollider, NavMeshAgent, " +
                $"CharacterStatsRuntime, Targetable, EnemyAI, EnemyCombat, EnemyHitReaction, EnemyDeath\n" +
                $"  Components on child: EnemyAnimationEventForwarder\n" +
                $"  Children:            weapon_r/EnemyWeaponHitbox (trigger BoxCollider + " +
                $"kinematic Rigidbody + EnemyHitboxRelay),\n" +
                $"                       _AssassinateZone (SphereCollider trigger + AssassinateInteractable)\n" +
                $"  Drop into a scene with a baked NavMesh ('LevelGen ▶ Combat ▶ Bake Test Scene NavMesh')" +
                " and press Play."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu: place an Enemy_Grunt in the active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Place Enemy_Grunt in Active Scene")]
        private static void PlaceInActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[EnemyBaseBuilder] {EnemyPrefabPath} not found. " +
                               "Run 'LevelGen ▶ Combat ▶ Build Enemy_Grunt Prefab' first.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Debug.LogError("[EnemyBaseBuilder] InstantiatePrefab failed.");
                return;
            }
            instance.transform.position = new Vector3(4f, 0f, 4f);
            instance.transform.rotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Place Enemy_Grunt");

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            Debug.Log($"[EnemyBaseBuilder] Placed '{instance.name}' at {instance.transform.position}.");
        }

        // ════════════════════════════════════════════════════════════════════
        // EnemyData_Grunt.asset creation
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads or creates Assets/Data/EnemyData/EnemyData_Grunt.asset
        /// with the M13-EnemyBase locked Grunt defaults. Existing asset
        /// (any author tweaks) is left alone.
        /// </summary>
        private static EnemyData EnsureGruntData()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "EnemyData");

            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(GruntDataPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<EnemyData>();
            asset.enemyName        = "Grunt";
            asset.maxHP            = 80;
            asset.attackDamage     = 10f;
            asset.defense          = 2f;
            asset.moveSpeed        = 3.5f;
            asset.rotationSpeed    = 10f;
            asset.detectionRange   = 6f;
            asset.attackRange      = 1.3f;
            asset.leashRange       = 10f;
            asset.stoppingDistance = 1.0f;
            asset.attackCooldown   = 1.5f;

            AssetDatabase.CreateAsset(asset, GruntDataPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EnemyBaseBuilder] Created {GruntDataPath}.");
            return asset;
        }

        // ════════════════════════════════════════════════════════════════════
        // EnemyWeaponHitbox build (mirror of DummyPrefabBuilder's version)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the EnemyWeaponHitbox child under the rig's weapon_r bone:
        /// trigger BoxCollider (default disabled), kinematic Rigidbody
        /// (REQUIRED for OnTriggerEnter on a child collider that moves via
        /// skeletal animation), EnemyHitboxRelay wired to EnemyCombat.
        /// Wires EnemyCombat._hitbox back to the BoxCollider as the final
        /// step.
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
                Debug.LogError("[EnemyBaseBuilder] Could not find a weapon attach Transform " +
                               $"in the Grunt's MaleCharacterPBR hierarchy. Looked for: " +
                               $"{string.Join(", ", WeaponAttachCandidates)}. Aborting hitbox build — " +
                               "Grunt attacks will swing but produce no damage.");
                return;
            }

            var hitboxGO = new GameObject(EnemyHitboxName);
            hitboxGO.transform.SetParent(attach, worldPositionStays: false);
            hitboxGO.transform.localPosition = Vector3.zero;
            hitboxGO.transform.localRotation = Quaternion.identity;
            hitboxGO.transform.localScale    = Vector3.one;

            var box = hitboxGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled   = false;
            box.size      = EnemyHitboxSize;
            box.center    = EnemyHitboxCenter;

            var rb = hitboxGO.AddComponent<Rigidbody>();
            rb.isKinematic            = true;
            rb.useGravity             = false;
            rb.interpolation          = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            var relay = hitboxGO.AddComponent<EnemyHitboxRelay>();
            WireSerializedRef(relay, "_combat", combat);

            // Final: EnemyCombat._hitbox → new BoxCollider.
            WireSerializedRef(combat, "_hitbox", box);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wires EnemyBase's 9 SerializeField references via SerializedObject.
        /// Reset() doesn't fire on programmatic AddComponent — wire
        /// explicitly. Field names mirror EnemyBase.cs.
        /// </summary>
        private static void WireEnemyBaseRefs(
            EnemyBase enemyBase,
            EnemyData data,
            CharacterStatsRuntime stats,
            Targetable targetable,
            EnemyAI ai,
            EnemyCombat combat,
            EnemyHitReaction hitReaction,
            EnemyDeath death,
            Animator animator,
            NavMeshAgent agent,
            CapsuleCollider capsule)
        {
            var so = new SerializedObject(enemyBase);
            void Wire(string field, Object value)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[EnemyBaseBuilder] EnemyBase has no '{field}' serialized field.");
                    return;
                }
                prop.objectReferenceValue = value;
            }
            Wire("_data",        data);
            Wire("_stats",       stats);
            Wire("_targetable",  targetable);
            Wire("_ai",          ai);
            Wire("_combat",      combat);
            Wire("_hitReaction", hitReaction);
            Wire("_death",       death);
            Wire("_animator",    animator);
            Wire("_agent",       agent);
            Wire("_capsule",     capsule);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(enemyBase);
        }

        /// <summary>
        /// Generic single-field SerializedObject writer. Logs an error if
        /// the field doesn't exist (typo / API drift). Idempotent and
        /// cross-assembly-safe (Editor assembly can't touch private fields
        /// directly).
        /// </summary>
        private static void WireSerializedRef(Component target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[EnemyBaseBuilder] {target.GetType().Name} has no '{fieldName}' serialized field.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// Builds the M6 _AssassinateZone child: SphereCollider trigger +
        /// AssassinateInteractable, with prompt anchor at head height.
        /// Mirror of DummyPrefabBuilder.BuildAssassinateZone — Enemy_Grunt
        /// takes over as the canonical assassinate target.
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
            sc.center    = new Vector3(0f, 0.9f, 0f);

            var assassinate = zone.AddComponent<AssassinateInteractable>();

            // Prompt anchor sits at head height so the floating prompt
            // renders above the model.
            var headAnchor = new GameObject("_PromptAnchor_Head");
            headAnchor.transform.SetParent(zone.transform, worldPositionStays: false);
            headAnchor.transform.localPosition = new Vector3(0f, 1.9f, 0f);

            // Reset() does not fire on programmatic AddComponent — wire
            // SerializeField refs explicitly via SerializedObject (M6 lesson).
            var so = new SerializedObject(assassinate);
            void Wire(string field, Object value)
            {
                var prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[EnemyBaseBuilder] AssassinateInteractable has no '{field}' field.");
                    return;
                }
                prop.objectReferenceValue = value;
            }
            Wire("_targetStats",     runtime);
            Wire("_targetTransform", root.transform);
            Wire("_promptAnchor",    headAnchor.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(assassinate);

            // Build prompt UI at author time so it's visible in the prefab
            // inspector (otherwise it only spawns on Awake at runtime).
            assassinate.EnsurePromptUI();
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

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[EnemyBaseBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[EnemyBaseBuilder] Created folder: {path}");
        }
    }
}
#endif
