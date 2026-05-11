// EnemyBaseValidator.cs — M13-EnemyBase consolidated enemy validator.
//
// Replaces the M-MenuCleanup ValidateEnemy.cs (32 checks, Dummy-targeted)
// with a SUPERSET that adds Enemy_Grunt + EnemyBase + EnemyData coverage.
//
// 41 read-only checks:
//   1-32: original ValidateEnemy coverage (Dummy.prefab, EnemyBaseController,
//         script API surface, friendly-fire guard, AI ranges).
//   33-41: M13-EnemyBase additions — EnemyBase presence + _data wiring,
//          [DefaultExecutionOrder(-50)], InitFromEnemyData methods on
//          consumers, EnemyData_Grunt asset existence + range ordering.
//
// Menu: LevelGen ▶ Combat ▶ Validate Enemy
//
// Targets BOTH Dummy.prefab (the sandbox / M4-M11 stack) AND
// Enemy_Grunt.prefab (the M13-EnemyBase production archetype). Dummy
// stays the smoke-test prefab; Enemy_Grunt is the canonical production
// enemy. Both must pass.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using LevelGen.Combat;

namespace LevelGen.Enemy.EditorTools
{
    public static class EnemyBaseValidator
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string DummyPrefabPath         = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string GruntPrefabPath         = "Assets/Prefabs/Character Prefabs/Enemy/Enemy_Grunt.prefab";
        private const string DummyStatsAssetPath     = "Assets/Data/CharacterStats/CharacterStats_Dummy.asset";
        private const string GruntDataAssetPath      = "Assets/Data/EnemyData/EnemyData_Grunt.asset";
        private const string EnemyBaseControllerPath = "Assets/Animators/Enemy/EnemyBaseController.controller";
        private const string EnemyCombatSrcPath      = "Assets/Scripts/Combat/EnemyCombat.cs";

        [MenuItem("LevelGen/Combat/Validate Enemy")]
        public static void Run()
        {
            int pass = 0, fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ════════════════════════════════════════════════════════════════
            // 1-32: original ValidateEnemy coverage (Dummy + script surface)
            // ════════════════════════════════════════════════════════════════

            // ── 1: CharacterStats_Dummy.asset exists ─────────────────────────
            var dummyStats = AssetDatabase.LoadAssetAtPath<CharacterStats>(DummyStatsAssetPath);
            Check("1 CharacterStats_Dummy.asset exists",
                dummyStats != null,
                dummyStats != null ? DummyStatsAssetPath : $"missing at {DummyStatsAssetPath}");

            var dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (dummy == null)
            {
                Debug.LogError($"[Validator] FATAL — Dummy.prefab missing at {DummyPrefabPath}. " +
                               "Many checks will FAIL. Run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'.");
            }

            // ── 2: CharacterStatsRuntime on Dummy root ───────────────────────
            var stats = dummy != null ? dummy.GetComponent<CharacterStatsRuntime>() : null;
            Check("2 CharacterStatsRuntime component on Dummy root",
                stats != null,
                stats != null ? "present" : "missing");

            // ── 3-5: CharacterStatsRuntime API surface ───────────────────────
            var statsType = typeof(CharacterStatsRuntime);
            var invProp = statsType.GetProperty("IsInvulnerable",
                BindingFlags.Public | BindingFlags.Instance);
            Check("3 CharacterStatsRuntime.IsInvulnerable property",
                invProp != null && invProp.PropertyType == typeof(bool),
                invProp != null ? "bool, getter present" : "property missing");

            var setInv = statsType.GetMethod("SetInvulnerable",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(bool) }, null);
            Check("4 CharacterStatsRuntime.SetInvulnerable(bool) method",
                setInv != null && setInv.ReturnType == typeof(void),
                setInv != null ? "signature OK" : "method missing");

            var applyDmg = statsType.GetMethod("ApplyDamage",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            Check("5 CharacterStatsRuntime.ApplyDamage(int) method",
                applyDmg != null && applyDmg.ReturnType == typeof(void),
                applyDmg != null ? "signature OK" : "method missing");

            // ── 6: Targetable on Dummy root ──────────────────────────────────
            var targetable = dummy != null ? dummy.GetComponent<Targetable>() : null;
            Check("6 Targetable component on Dummy root",
                targetable != null,
                targetable != null ? "present" : "missing");

            // ── 7-9: Targetable surface ──────────────────────────────────────
            var targType = typeof(Targetable);
            var onHit = targType.GetEvent("OnHit", BindingFlags.Public | BindingFlags.Instance);
            Check("7 Targetable.OnHit event signature Action<Vector3, float>",
                onHit != null && onHit.EventHandlerType == typeof(Action<Vector3, float>),
                onHit != null ? $"handlerType={onHit.EventHandlerType.Name}" : "event missing");

            var anyHit = targType.GetEvent("AnyTargetableHit", BindingFlags.Public | BindingFlags.Static);
            Check("8 Targetable.AnyTargetableHit static event",
                anyHit != null,
                anyHit != null ? "present (Action<Vector3, float>)" : "event missing");

            var raiseHit = targType.GetMethod("RaiseHit",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            Check("9 Targetable.RaiseHit(Vector3, float) method",
                raiseHit != null && raiseHit.ReturnType == typeof(void),
                raiseHit != null ? "signature OK" : "method missing");

            // ── 10: EnemyHitReaction on Dummy root ───────────────────────────
            var hitReact = dummy != null ? dummy.GetComponent<EnemyHitReaction>() : null;
            Check("10 EnemyHitReaction component on Dummy root",
                hitReact != null,
                hitReact != null ? "present" : "missing");

            // ── 11-12: EnemyHitReaction attributes ───────────────────────────
            var hrType = typeof(EnemyHitReaction);
            bool requiresTarget = HasRequireComponent(hrType, typeof(Targetable));
            Check("11 EnemyHitReaction [RequireComponent(Targetable)]",
                requiresTarget,
                requiresTarget ? "attribute present" : "attribute missing");

            bool hrDisallowDup = hrType.GetCustomAttribute<DisallowMultipleComponent>() != null;
            Check("12 EnemyHitReaction [DisallowMultipleComponent]",
                hrDisallowDup,
                hrDisallowDup ? "attribute present" : "attribute missing");

            // ── 13: HandleHit(Vector3, float) method ─────────────────────────
            var handleHit = hrType.GetMethod("HandleHit",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            Check("13 EnemyHitReaction.HandleHit(Vector3, float) method",
                handleHit != null,
                handleHit != null ? "signature OK" : "method missing or wrong signature");

            // ── 14: EnemyDeath on Dummy root ─────────────────────────────────
            var death = dummy != null ? dummy.GetComponent<EnemyDeath>() : null;
            Check("14 EnemyDeath component on Dummy root",
                death != null,
                death != null ? "present" : "missing");

            // ── 15: CharacterStatsRuntime.OnDied event ───────────────────────
            var onDied = statsType.GetEvent("OnDied", BindingFlags.Public | BindingFlags.Instance);
            Check("15 CharacterStatsRuntime.OnDied event Action<CharacterStatsRuntime>",
                onDied != null && onDied.EventHandlerType == typeof(Action<CharacterStatsRuntime>),
                onDied != null ? $"handlerType={onDied.EventHandlerType.Name}" : "event missing");

            // ── 16: CharacterStatsRuntime.IsDead property ────────────────────
            var isDead = statsType.GetProperty("IsDead",
                BindingFlags.Public | BindingFlags.Instance);
            Check("16 CharacterStatsRuntime.IsDead property (bool)",
                isDead != null && isDead.PropertyType == typeof(bool),
                isDead != null ? "bool, getter present" : "property missing");

            // ── 17: EnemyBaseController asset exists ─────────────────────────
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyBaseControllerPath);
            Check("17 EnemyBaseController.controller asset exists",
                controller != null,
                controller != null ? EnemyBaseControllerPath : $"missing at {EnemyBaseControllerPath}");

            // ── 18: Dummy Animator references EnemyBaseController ────────────
            var dummyAnimChild = dummy != null ? dummy.transform.Find("MaleCharacterPBR") : null;
            Animator dummyAnim = dummyAnimChild != null ? dummyAnimChild.GetComponent<Animator>() : null;
            bool ok18 = false;
            string detail18 = "MaleCharacterPBR child not found";
            if (dummyAnim != null)
            {
                var rac = dummyAnim.runtimeAnimatorController;
                ok18 = rac != null && rac.name == "EnemyBaseController";
                detail18 = rac != null
                    ? $"controller='{rac.name}'"
                    : "Animator has no runtimeAnimatorController";
            }
            Check("18 Dummy Animator references EnemyBaseController (not PlayerBaseController)",
                ok18, detail18);

            // ── 19-22: Animator parameters ───────────────────────────────────
            bool hasHit = false, hasDeath = false, hasMoveSpeed = false, hasAttack = false;
            if (controller != null)
            {
                foreach (var p in controller.parameters)
                {
                    if (p.name == "Hit"       && p.type == AnimatorControllerParameterType.Trigger) hasHit       = true;
                    if (p.name == "Death"     && p.type == AnimatorControllerParameterType.Trigger) hasDeath     = true;
                    if (p.name == "MoveSpeed" && p.type == AnimatorControllerParameterType.Float)   hasMoveSpeed = true;
                    if (p.name == "Attack"    && p.type == AnimatorControllerParameterType.Trigger) hasAttack    = true;
                }
            }
            Check("19 EnemyBaseController Hit Trigger parameter",       hasHit,       hasHit       ? "type=Trigger" : "missing");
            Check("20 EnemyBaseController Death Trigger parameter",     hasDeath,     hasDeath     ? "type=Trigger" : "missing");
            Check("21 EnemyBaseController MoveSpeed Float parameter",   hasMoveSpeed, hasMoveSpeed ? "type=Float"   : "missing");
            Check("22 EnemyBaseController Attack Trigger parameter",    hasAttack,    hasAttack    ? "type=Trigger" : "missing");

            // ── 23: AnyState → Hit transition canTransitionToSelf=false ──────
            bool ok23 = false;
            string detail23 = "controller missing";
            if (controller != null && controller.layers.Length > 0)
            {
                AnimatorState hitState = null;
                var rootSm = controller.layers[0].stateMachine;
                foreach (var sc in rootSm.states)
                    if (sc.state != null && sc.state.name == "Hit") { hitState = sc.state; break; }

                if (hitState != null)
                {
                    detail23 = "no AnyState → Hit transition found";
                    foreach (var t in rootSm.anyStateTransitions)
                    {
                        if (t.destinationState == hitState)
                        {
                            ok23 = !t.canTransitionToSelf;
                            detail23 = ok23
                                ? "canTransitionToSelf=false"
                                : $"canTransitionToSelf={t.canTransitionToSelf} (expected false)";
                            break;
                        }
                    }
                }
                else
                {
                    detail23 = "Hit state missing from controller";
                }
            }
            Check("23 AnyState → Hit transition with canTransitionToSelf=false", ok23, detail23);

            // ── 24: Death state is terminal (no outgoing transitions) ────────
            bool ok24 = false;
            string detail24 = "controller missing";
            if (controller != null && controller.layers.Length > 0)
            {
                AnimatorState deathState = null;
                var rootSm = controller.layers[0].stateMachine;
                foreach (var sc in rootSm.states)
                    if (sc.state != null && sc.state.name == "Death") { deathState = sc.state; break; }
                if (deathState != null)
                {
                    ok24 = deathState.transitions.Length == 0;
                    detail24 = ok24
                        ? "no outgoing transitions"
                        : $"outgoing transitions={deathState.transitions.Length} (expected 0)";
                }
                else
                {
                    detail24 = "Death state missing from controller";
                }
            }
            Check("24 Death state is terminal (no outgoing transitions)", ok24, detail24);

            // ── 25: EnemyCombat on Dummy root ────────────────────────────────
            var enemyCombat = dummy != null ? dummy.GetComponent<EnemyCombat>() : null;
            Check("25 EnemyCombat component on Dummy root",
                enemyCombat != null,
                enemyCombat != null ? "present" : "missing");

            // ── 26: EnemyWeaponHitbox child under weapon_r ───────────────────
            Transform weaponBone = null;
            if (dummy != null)
            {
                weaponBone = FindChildRecursive(dummy.transform, "weapon_r")
                          ?? FindChildRecursive(dummy.transform, "weapon_l")
                          ?? FindChildRecursive(dummy.transform, "Weapon_R")
                          ?? FindChildRecursive(dummy.transform, "Weapon_L");
            }
            bool ok26 = false;
            string detail26 = "no weapon bone found in hierarchy";
            if (weaponBone != null)
            {
                Transform hitbox = null;
                foreach (Transform c in weaponBone) if (c.name == "EnemyWeaponHitbox") { hitbox = c; break; }
                ok26 = hitbox != null;
                detail26 = ok26
                    ? $"EnemyWeaponHitbox under '{weaponBone.name}'"
                    : $"weapon bone '{weaponBone.name}' has no EnemyWeaponHitbox child";
            }
            Check("26 EnemyWeaponHitbox child exists under weapon_r", ok26, detail26);

            // ── 27: EnemyAnimationEventForwarder on MaleCharacterPBR child ───
            EnemyAnimationEventForwarder forwarder = null;
            if (dummyAnimChild != null)
                forwarder = dummyAnimChild.GetComponent<EnemyAnimationEventForwarder>();
            Check("27 EnemyAnimationEventForwarder on MaleCharacterPBR child",
                forwarder != null,
                forwarder != null ? "present" : "missing");

            // ── 28: Friendly-fire guard literal-stub source scan ─────────────
            bool ok28 = false;
            string detail28 = $"source missing at {EnemyCombatSrcPath}";
            string srcFull = Path.Combine(Application.dataPath, "..", EnemyCombatSrcPath);
            if (File.Exists(srcFull))
            {
                string src = File.ReadAllText(srcFull);
                ok28 = src.Contains("CompareTag(\"Player\")");
                detail28 = ok28
                    ? "CompareTag(\"Player\") literal found"
                    : "friendly-fire guard literal missing from EnemyCombat.cs";
            }
            Check("28 EnemyCombat friendly-fire guard (CompareTag(\"Player\")) present", ok28, detail28);

            // ── 29: EnemyAI on Dummy root ────────────────────────────────────
            var ai = dummy != null ? dummy.GetComponent<EnemyAI>() : null;
            Check("29 EnemyAI component on Dummy root",
                ai != null,
                ai != null ? "present" : "missing");

            // ── 30: NavMeshAgent on Dummy root ───────────────────────────────
            var agent = dummy != null ? dummy.GetComponent<NavMeshAgent>() : null;
            Check("30 NavMeshAgent component on Dummy root",
                agent != null,
                agent != null ? "present" : "missing");

            // ── 31-32: EnemyAI SerializeField range fields > 0 ───────────────
            bool ok31 = false, ok32 = false;
            string detail31 = "EnemyAI missing", detail32 = "EnemyAI missing";
            if (ai != null)
            {
                var so = new SerializedObject(ai);
                var attackProp    = so.FindProperty("_attackRange");
                var detectionProp = so.FindProperty("_detectionRange");
                if (attackProp != null)
                {
                    ok31 = attackProp.floatValue > 0f;
                    detail31 = $"_attackRange={attackProp.floatValue:0.00}";
                }
                else { detail31 = "_attackRange field not found"; }
                if (detectionProp != null)
                {
                    ok32 = detectionProp.floatValue > 0f;
                    detail32 = $"_detectionRange={detectionProp.floatValue:0.00}";
                }
                else { detail32 = "_detectionRange field not found"; }
            }
            Check("31 EnemyAI _attackRange > 0",    ok31, detail31);
            Check("32 EnemyAI _detectionRange > 0", ok32, detail32);

            // ════════════════════════════════════════════════════════════════
            // 33-41: M13-EnemyBase additions
            // ════════════════════════════════════════════════════════════════

            // ── 33: Enemy_Grunt.prefab exists + EnemyBase on root ────────────
            var grunt = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
            EnemyBase gruntBase = grunt != null ? grunt.GetComponent<EnemyBase>() : null;
            Check("33 Enemy_Grunt.prefab exists with EnemyBase on root",
                gruntBase != null,
                grunt == null
                    ? $"prefab missing at {GruntPrefabPath} — run 'Build Enemy_Grunt Prefab'"
                    : (gruntBase != null ? "EnemyBase present" : "EnemyBase component missing"));

            // ── 34: EnemyBase._data SerializeField non-null ──────────────────
            bool ok34 = false;
            string detail34 = "EnemyBase missing";
            if (gruntBase != null)
            {
                var so = new SerializedObject(gruntBase);
                var prop = so.FindProperty("_data");
                if (prop != null)
                {
                    ok34 = prop.objectReferenceValue != null;
                    detail34 = ok34
                        ? $"wired to '{prop.objectReferenceValue.name}'"
                        : "_data field is null — re-run 'Build Enemy_Grunt Prefab'";
                }
                else { detail34 = "_data field not found on EnemyBase"; }
            }
            Check("34 EnemyBase._data SerializeField non-null on Enemy_Grunt.prefab",
                ok34, detail34);

            // ── 35: [DefaultExecutionOrder(-50)] on EnemyBase ────────────────
            var enemyBaseType = typeof(EnemyBase);
            var execAttr = enemyBaseType.GetCustomAttribute<DefaultExecutionOrder>();
            bool ok35 = execAttr != null && execAttr.order == -50;
            string detail35 = execAttr == null
                ? "attribute missing"
                : $"order={execAttr.order} (expected -50)";
            Check("35 EnemyBase has [DefaultExecutionOrder(-50)]", ok35, detail35);

            // ── 36-38: InitFromEnemyData methods on consumers ────────────────
            var initOnAI = typeof(EnemyAI).GetMethod("InitFromEnemyData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(EnemyData) }, null);
            Check("36 EnemyAI.InitFromEnemyData(EnemyData) method",
                initOnAI != null && initOnAI.ReturnType == typeof(void),
                initOnAI != null ? "signature OK" : "method missing");

            var initOnCombat = typeof(EnemyCombat).GetMethod("InitFromEnemyData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(EnemyData) }, null);
            Check("37 EnemyCombat.InitFromEnemyData(EnemyData) method",
                initOnCombat != null && initOnCombat.ReturnType == typeof(void),
                initOnCombat != null ? "signature OK" : "method missing");

            var initOnStats = typeof(CharacterStatsRuntime).GetMethod("InitFromEnemyData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(EnemyData) }, null);
            Check("38 CharacterStatsRuntime.InitFromEnemyData(EnemyData) method",
                initOnStats != null && initOnStats.ReturnType == typeof(void),
                initOnStats != null ? "signature OK" : "method missing");

            // ── 39: EnemyData_Grunt.asset exists ─────────────────────────────
            var gruntData = AssetDatabase.LoadAssetAtPath<EnemyData>(GruntDataAssetPath);
            Check("39 EnemyData_Grunt.asset exists",
                gruntData != null,
                gruntData != null ? GruntDataAssetPath : $"missing at {GruntDataAssetPath}");

            // ── 40: EnemyData_Grunt.maxHP > 0 ────────────────────────────────
            bool ok40 = gruntData != null && gruntData.maxHP > 0;
            string detail40 = gruntData != null
                ? $"maxHP={gruntData.maxHP}"
                : "EnemyData_Grunt.asset missing";
            Check("40 EnemyData_Grunt.maxHP > 0", ok40, detail40);

            // ── 41: EnemyData_Grunt range ordering (attack < detection) ──────
            bool ok41 = gruntData != null && gruntData.attackRange < gruntData.detectionRange;
            string detail41 = gruntData != null
                ? $"attackRange={gruntData.attackRange:0.00}, detectionRange={gruntData.detectionRange:0.00}"
                : "EnemyData_Grunt.asset missing";
            Check("41 EnemyData_Grunt.attackRange < detectionRange", ok41, detail41);

            Summary(pass, fail);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool HasRequireComponent(Type t, Type required)
        {
            foreach (var a in t.GetCustomAttributes<RequireComponent>())
            {
                if (a.m_Type0 == required || a.m_Type1 == required || a.m_Type2 == required)
                    return true;
            }
            return false;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var found = FindChildRecursive(c, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Enemy wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
