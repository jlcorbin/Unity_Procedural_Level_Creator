// EnemyBaseValidator.cs — consolidated enemy validator (M13-EnemyBase /
// M-MenuDelete).
//
// 40 read-only checks covering Enemy_Grunt.prefab + enemy-side scripts
// + EnemyBaseController + EnemyData_Grunt. Originally inherited 32
// Dummy-targeted checks from M-MenuCleanup's ValidateEnemy.cs; after
// M-MenuDelete retired Dummy.prefab + CharacterStats_Dummy.asset, every
// Dummy-prefab check was retargeted to Enemy_Grunt. Script-API surface
// + Animator-graph checks are prefab-agnostic and stayed unchanged.
//
// Numbering: 1-31 are the original prefab/script checks retargeted to
// Enemy_Grunt (one Dummy-specific check — CharacterStats_Dummy.asset
// existence — was dropped, so the count shifted from 32 → 31).
// 32-40 are the M13-EnemyBase additions covering EnemyBase + EnemyData.
//
// Menu: LevelGen ▶ Combat ▶ Validate Enemy

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
        private const string GruntPrefabPath         = "Assets/Prefabs/Character Prefabs/Enemy/Enemy_Grunt.prefab";
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

            // Load the canonical enemy prefab once for the prefab-relative checks.
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
            if (enemy == null)
            {
                Debug.LogError($"[Validator] FATAL — Enemy_Grunt.prefab missing at {GruntPrefabPath}. " +
                               "All prefab-relative checks will FAIL. Run 'LevelGen ▶ Combat ▶ Build Enemy_Grunt Prefab'.");
            }

            // ── 1: CharacterStatsRuntime on Enemy_Grunt root ─────────────────
            var stats = enemy != null ? enemy.GetComponent<CharacterStatsRuntime>() : null;
            Check("1 CharacterStatsRuntime component on Enemy_Grunt root",
                stats != null,
                stats != null ? "present" : "missing");

            // ── 2-4: CharacterStatsRuntime API surface ───────────────────────
            var statsType = typeof(CharacterStatsRuntime);
            var invProp = statsType.GetProperty("IsInvulnerable",
                BindingFlags.Public | BindingFlags.Instance);
            Check("2 CharacterStatsRuntime.IsInvulnerable property",
                invProp != null && invProp.PropertyType == typeof(bool),
                invProp != null ? "bool, getter present" : "property missing");

            var setInv = statsType.GetMethod("SetInvulnerable",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(bool) }, null);
            Check("3 CharacterStatsRuntime.SetInvulnerable(bool) method",
                setInv != null && setInv.ReturnType == typeof(void),
                setInv != null ? "signature OK" : "method missing");

            var applyDmg = statsType.GetMethod("ApplyDamage",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            Check("4 CharacterStatsRuntime.ApplyDamage(int) method",
                applyDmg != null && applyDmg.ReturnType == typeof(void),
                applyDmg != null ? "signature OK" : "method missing");

            // ── 5: Targetable on Enemy_Grunt root ────────────────────────────
            var targetable = enemy != null ? enemy.GetComponent<Targetable>() : null;
            Check("5 Targetable component on Enemy_Grunt root",
                targetable != null,
                targetable != null ? "present" : "missing");

            // ── 6-8: Targetable surface ──────────────────────────────────────
            var targType = typeof(Targetable);
            var onHit = targType.GetEvent("OnHit", BindingFlags.Public | BindingFlags.Instance);
            Check("6 Targetable.OnHit event signature Action<Vector3, float>",
                onHit != null && onHit.EventHandlerType == typeof(Action<Vector3, float>),
                onHit != null ? $"handlerType={onHit.EventHandlerType.Name}" : "event missing");

            var anyHit = targType.GetEvent("AnyTargetableHit", BindingFlags.Public | BindingFlags.Static);
            Check("7 Targetable.AnyTargetableHit static event",
                anyHit != null,
                anyHit != null ? "present (Action<Vector3, float>)" : "event missing");

            var raiseHit = targType.GetMethod("RaiseHit",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            Check("8 Targetable.RaiseHit(Vector3, float) method",
                raiseHit != null && raiseHit.ReturnType == typeof(void),
                raiseHit != null ? "signature OK" : "method missing");

            // ── 9: EnemyHitReaction on Enemy_Grunt root ──────────────────────
            var hitReact = enemy != null ? enemy.GetComponent<EnemyHitReaction>() : null;
            Check("9 EnemyHitReaction component on Enemy_Grunt root",
                hitReact != null,
                hitReact != null ? "present" : "missing");

            // ── 10-11: EnemyHitReaction attributes ───────────────────────────
            var hrType = typeof(EnemyHitReaction);
            bool requiresTarget = HasRequireComponent(hrType, typeof(Targetable));
            Check("10 EnemyHitReaction [RequireComponent(Targetable)]",
                requiresTarget,
                requiresTarget ? "attribute present" : "attribute missing");

            bool hrDisallowDup = hrType.GetCustomAttribute<DisallowMultipleComponent>() != null;
            Check("11 EnemyHitReaction [DisallowMultipleComponent]",
                hrDisallowDup,
                hrDisallowDup ? "attribute present" : "attribute missing");

            // ── 12: HandleHit(Vector3, float) method ─────────────────────────
            var handleHit = hrType.GetMethod("HandleHit",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            Check("12 EnemyHitReaction.HandleHit(Vector3, float) method",
                handleHit != null,
                handleHit != null ? "signature OK" : "method missing or wrong signature");

            // ── 13: EnemyDeath on Enemy_Grunt root ───────────────────────────
            var death = enemy != null ? enemy.GetComponent<EnemyDeath>() : null;
            Check("13 EnemyDeath component on Enemy_Grunt root",
                death != null,
                death != null ? "present" : "missing");

            // ── 14: CharacterStatsRuntime.OnDied event ───────────────────────
            var onDied = statsType.GetEvent("OnDied", BindingFlags.Public | BindingFlags.Instance);
            Check("14 CharacterStatsRuntime.OnDied event Action<CharacterStatsRuntime>",
                onDied != null && onDied.EventHandlerType == typeof(Action<CharacterStatsRuntime>),
                onDied != null ? $"handlerType={onDied.EventHandlerType.Name}" : "event missing");

            // ── 15: CharacterStatsRuntime.IsDead property ────────────────────
            var isDead = statsType.GetProperty("IsDead",
                BindingFlags.Public | BindingFlags.Instance);
            Check("15 CharacterStatsRuntime.IsDead property (bool)",
                isDead != null && isDead.PropertyType == typeof(bool),
                isDead != null ? "bool, getter present" : "property missing");

            // ── 16: EnemyBaseController asset exists ─────────────────────────
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyBaseControllerPath);
            Check("16 EnemyBaseController.controller asset exists",
                controller != null,
                controller != null ? EnemyBaseControllerPath : $"missing at {EnemyBaseControllerPath}");

            // ── 17: Enemy_Grunt Animator references EnemyBaseController ──────
            var enemyAnimChild = enemy != null ? enemy.transform.Find("MaleCharacterPBR") : null;
            Animator enemyAnim = enemyAnimChild != null ? enemyAnimChild.GetComponent<Animator>() : null;
            bool ok17 = false;
            string detail17 = "MaleCharacterPBR child not found";
            if (enemyAnim != null)
            {
                var rac = enemyAnim.runtimeAnimatorController;
                ok17 = rac != null && rac.name == "EnemyBaseController";
                detail17 = rac != null
                    ? $"controller='{rac.name}'"
                    : "Animator has no runtimeAnimatorController";
            }
            Check("17 Enemy_Grunt Animator references EnemyBaseController",
                ok17, detail17);

            // ── 18-21: Animator parameters ───────────────────────────────────
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
            Check("18 EnemyBaseController Hit Trigger parameter",     hasHit,       hasHit       ? "type=Trigger" : "missing");
            Check("19 EnemyBaseController Death Trigger parameter",   hasDeath,     hasDeath     ? "type=Trigger" : "missing");
            Check("20 EnemyBaseController MoveSpeed Float parameter", hasMoveSpeed, hasMoveSpeed ? "type=Float"   : "missing");
            Check("21 EnemyBaseController Attack Trigger parameter",  hasAttack,    hasAttack    ? "type=Trigger" : "missing");

            // ── 22: AnyState → Hit transition canTransitionToSelf=false ──────
            bool ok22 = false;
            string detail22 = "controller missing";
            if (controller != null && controller.layers.Length > 0)
            {
                AnimatorState hitState = null;
                var rootSm = controller.layers[0].stateMachine;
                foreach (var sc in rootSm.states)
                    if (sc.state != null && sc.state.name == "Hit") { hitState = sc.state; break; }

                if (hitState != null)
                {
                    detail22 = "no AnyState → Hit transition found";
                    foreach (var t in rootSm.anyStateTransitions)
                    {
                        if (t.destinationState == hitState)
                        {
                            ok22 = !t.canTransitionToSelf;
                            detail22 = ok22
                                ? "canTransitionToSelf=false"
                                : $"canTransitionToSelf={t.canTransitionToSelf} (expected false)";
                            break;
                        }
                    }
                }
                else
                {
                    detail22 = "Hit state missing from controller";
                }
            }
            Check("22 AnyState → Hit transition with canTransitionToSelf=false", ok22, detail22);

            // ── 23: Death state is terminal (no outgoing transitions) ────────
            bool ok23 = false;
            string detail23 = "controller missing";
            if (controller != null && controller.layers.Length > 0)
            {
                AnimatorState deathState = null;
                var rootSm = controller.layers[0].stateMachine;
                foreach (var sc in rootSm.states)
                    if (sc.state != null && sc.state.name == "Death") { deathState = sc.state; break; }
                if (deathState != null)
                {
                    ok23 = deathState.transitions.Length == 0;
                    detail23 = ok23
                        ? "no outgoing transitions"
                        : $"outgoing transitions={deathState.transitions.Length} (expected 0)";
                }
                else
                {
                    detail23 = "Death state missing from controller";
                }
            }
            Check("23 Death state is terminal (no outgoing transitions)", ok23, detail23);

            // ── 24: EnemyCombat on Enemy_Grunt root ──────────────────────────
            var enemyCombat = enemy != null ? enemy.GetComponent<EnemyCombat>() : null;
            Check("24 EnemyCombat component on Enemy_Grunt root",
                enemyCombat != null,
                enemyCombat != null ? "present" : "missing");

            // ── 25: EnemyWeaponHitbox child under weapon_r ───────────────────
            Transform weaponBone = null;
            if (enemy != null)
            {
                weaponBone = FindChildRecursive(enemy.transform, "weapon_r")
                          ?? FindChildRecursive(enemy.transform, "weapon_l")
                          ?? FindChildRecursive(enemy.transform, "Weapon_R")
                          ?? FindChildRecursive(enemy.transform, "Weapon_L");
            }
            bool ok25 = false;
            string detail25 = "no weapon bone found in hierarchy";
            if (weaponBone != null)
            {
                Transform hitbox = null;
                foreach (Transform c in weaponBone) if (c.name == "EnemyWeaponHitbox") { hitbox = c; break; }
                ok25 = hitbox != null;
                detail25 = ok25
                    ? $"EnemyWeaponHitbox under '{weaponBone.name}'"
                    : $"weapon bone '{weaponBone.name}' has no EnemyWeaponHitbox child";
            }
            Check("25 EnemyWeaponHitbox child exists under weapon_r", ok25, detail25);

            // ── 26: EnemyAnimationEventForwarder on MaleCharacterPBR child ───
            EnemyAnimationEventForwarder forwarder = null;
            if (enemyAnimChild != null)
                forwarder = enemyAnimChild.GetComponent<EnemyAnimationEventForwarder>();
            Check("26 EnemyAnimationEventForwarder on MaleCharacterPBR child",
                forwarder != null,
                forwarder != null ? "present" : "missing");

            // ── 27: Friendly-fire guard literal-stub source scan ─────────────
            bool ok27 = false;
            string detail27 = $"source missing at {EnemyCombatSrcPath}";
            string srcFull = Path.Combine(Application.dataPath, "..", EnemyCombatSrcPath);
            if (File.Exists(srcFull))
            {
                string src = File.ReadAllText(srcFull);
                ok27 = src.Contains("CompareTag(\"Player\")");
                detail27 = ok27
                    ? "CompareTag(\"Player\") literal found"
                    : "friendly-fire guard literal missing from EnemyCombat.cs";
            }
            Check("27 EnemyCombat friendly-fire guard (CompareTag(\"Player\")) present", ok27, detail27);

            // ── 28: EnemyAI on Enemy_Grunt root ──────────────────────────────
            var ai = enemy != null ? enemy.GetComponent<EnemyAI>() : null;
            Check("28 EnemyAI component on Enemy_Grunt root",
                ai != null,
                ai != null ? "present" : "missing");

            // ── 29: NavMeshAgent on Enemy_Grunt root ─────────────────────────
            var agent = enemy != null ? enemy.GetComponent<NavMeshAgent>() : null;
            Check("29 NavMeshAgent component on Enemy_Grunt root",
                agent != null,
                agent != null ? "present" : "missing");

            // ── 30-31: EnemyAI SerializeField range fields > 0 ───────────────
            bool ok30 = false, ok31 = false;
            string detail30 = "EnemyAI missing", detail31 = "EnemyAI missing";
            if (ai != null)
            {
                var so = new SerializedObject(ai);
                var attackProp    = so.FindProperty("_attackRange");
                var detectionProp = so.FindProperty("_detectionRange");
                if (attackProp != null)
                {
                    ok30 = attackProp.floatValue > 0f;
                    detail30 = $"_attackRange={attackProp.floatValue:0.00}";
                }
                else { detail30 = "_attackRange field not found"; }
                if (detectionProp != null)
                {
                    ok31 = detectionProp.floatValue > 0f;
                    detail31 = $"_detectionRange={detectionProp.floatValue:0.00}";
                }
                else { detail31 = "_detectionRange field not found"; }
            }
            Check("30 EnemyAI _attackRange > 0",    ok30, detail30);
            Check("31 EnemyAI _detectionRange > 0", ok31, detail31);

            // ════════════════════════════════════════════════════════════════
            // 32-40: M13-EnemyBase additions
            // ════════════════════════════════════════════════════════════════

            // ── 32: EnemyBase on Enemy_Grunt root ────────────────────────────
            EnemyBase enemyBase = enemy != null ? enemy.GetComponent<EnemyBase>() : null;
            Check("32 EnemyBase component on Enemy_Grunt root",
                enemyBase != null,
                enemy == null
                    ? $"prefab missing at {GruntPrefabPath} — run 'Build Enemy_Grunt Prefab'"
                    : (enemyBase != null ? "EnemyBase present" : "EnemyBase component missing"));

            // ── 33: EnemyBase._data SerializeField non-null ──────────────────
            bool ok33 = false;
            string detail33 = "EnemyBase missing";
            if (enemyBase != null)
            {
                var so = new SerializedObject(enemyBase);
                var prop = so.FindProperty("_data");
                if (prop != null)
                {
                    ok33 = prop.objectReferenceValue != null;
                    detail33 = ok33
                        ? $"wired to '{prop.objectReferenceValue.name}'"
                        : "_data field is null — re-run 'Build Enemy_Grunt Prefab'";
                }
                else { detail33 = "_data field not found on EnemyBase"; }
            }
            Check("33 EnemyBase._data SerializeField non-null on Enemy_Grunt.prefab",
                ok33, detail33);

            // ── 34: [DefaultExecutionOrder(-50)] on EnemyBase ────────────────
            var enemyBaseType = typeof(EnemyBase);
            var execAttr = enemyBaseType.GetCustomAttribute<DefaultExecutionOrder>();
            bool ok34 = execAttr != null && execAttr.order == -50;
            string detail34 = execAttr == null
                ? "attribute missing"
                : $"order={execAttr.order} (expected -50)";
            Check("34 EnemyBase has [DefaultExecutionOrder(-50)]", ok34, detail34);

            // ── 35-37: InitFromEnemyData methods on consumers ────────────────
            var initOnAI = typeof(EnemyAI).GetMethod("InitFromEnemyData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(EnemyData) }, null);
            Check("35 EnemyAI.InitFromEnemyData(EnemyData) method",
                initOnAI != null && initOnAI.ReturnType == typeof(void),
                initOnAI != null ? "signature OK" : "method missing");

            var initOnCombat = typeof(EnemyCombat).GetMethod("InitFromEnemyData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(EnemyData) }, null);
            Check("36 EnemyCombat.InitFromEnemyData(EnemyData) method",
                initOnCombat != null && initOnCombat.ReturnType == typeof(void),
                initOnCombat != null ? "signature OK" : "method missing");

            var initOnStats = typeof(CharacterStatsRuntime).GetMethod("InitFromEnemyData",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(EnemyData) }, null);
            Check("37 CharacterStatsRuntime.InitFromEnemyData(EnemyData) method",
                initOnStats != null && initOnStats.ReturnType == typeof(void),
                initOnStats != null ? "signature OK" : "method missing");

            // ── 38: EnemyData_Grunt.asset exists ─────────────────────────────
            var gruntData = AssetDatabase.LoadAssetAtPath<EnemyData>(GruntDataAssetPath);
            Check("38 EnemyData_Grunt.asset exists",
                gruntData != null,
                gruntData != null ? GruntDataAssetPath : $"missing at {GruntDataAssetPath}");

            // ── 39: EnemyData_Grunt.maxHP > 0 ────────────────────────────────
            bool ok39 = gruntData != null && gruntData.maxHP > 0;
            string detail39 = gruntData != null
                ? $"maxHP={gruntData.maxHP}"
                : "EnemyData_Grunt.asset missing";
            Check("39 EnemyData_Grunt.maxHP > 0", ok39, detail39);

            // ── 40: EnemyData_Grunt range ordering (attack < detection) ──────
            bool ok40 = gruntData != null && gruntData.attackRange < gruntData.detectionRange;
            string detail40 = gruntData != null
                ? $"attackRange={gruntData.attackRange:0.00}, detectionRange={gruntData.detectionRange:0.00}"
                : "EnemyData_Grunt.asset missing";
            Check("40 EnemyData_Grunt.attackRange < detectionRange", ok40, detail40);

            // ════════════════════════════════════════════════════════════════
            // 41-44: Defense + health bar wiring (enemy health bar milestone)
            // ════════════════════════════════════════════════════════════════

            // ── 41: EnemyData_Grunt.Defense >= 0 ─────────────────────────────
            bool ok41 = gruntData != null && gruntData.Defense >= 0f;
            string detail41 = gruntData != null
                ? $"Defense={gruntData.Defense:0.##}"
                : "EnemyData_Grunt.asset missing";
            Check("41 EnemyData_Grunt.Defense >= 0", ok41, detail41);

            // ── 42: CharacterStatsRuntime.Defense getter + SetDefense(float) ─
            var defenseProp = statsType.GetProperty("Defense",
                BindingFlags.Public | BindingFlags.Instance);
            Check("42 CharacterStatsRuntime.Defense property (float, getter)",
                defenseProp != null && defenseProp.PropertyType == typeof(float),
                defenseProp != null ? "float, getter present" : "property missing");

            var setDefense = statsType.GetMethod("SetDefense",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(float) }, null);
            Check("43 CharacterStatsRuntime.SetDefense(float) method",
                setDefense != null && setDefense.ReturnType == typeof(void),
                setDefense != null ? "signature OK" : "method missing");

            // ── 44: EnemyHealthBar + EnemyHealthBarProximityDriver on Enemy_Grunt ──
            // Types looked up by name so the check compiles before the scripts
            // are created (health bar milestone ships these scripts). If the type
            // is not yet compiled, the check is a FAIL with a clear message.
            var healthBarType = Type.GetType("LevelGen.EnemyHealthBar, Assembly-CSharp");
            var driverType    = Type.GetType("LevelGen.EnemyHealthBarProximityDriver, Assembly-CSharp");

            bool ok44hb = false;
            string detail44hb;
            if (healthBarType == null)
            {
                detail44hb = "Type not found — script may not have compiled yet.";
            }
            else if (enemy == null)
            {
                detail44hb = "Enemy_Grunt.prefab missing.";
            }
            else
            {
                var hbComp = enemy.GetComponentInChildren(healthBarType);
                ok44hb    = hbComp != null;
                detail44hb = ok44hb
                    ? $"EnemyHealthBar found on '{hbComp.gameObject.name}'"
                    : "Component missing from prefab — wire it in the Editor.";
            }
            Check("44a EnemyHealthBar component exists in Enemy_Grunt hierarchy",
                ok44hb, detail44hb);

            bool ok44pd = false;
            string detail44pd;
            if (driverType == null)
            {
                detail44pd = "Type not found — script may not have compiled yet.";
            }
            else if (enemy == null)
            {
                detail44pd = "Enemy_Grunt.prefab missing.";
            }
            else
            {
                var pdComp = enemy.GetComponentInChildren(driverType);
                ok44pd    = pdComp != null;
                detail44pd = ok44pd
                    ? $"EnemyHealthBarProximityDriver found on '{pdComp.gameObject.name}'"
                    : "Component missing from prefab — wire it in the Editor.";
            }
            Check("44b EnemyHealthBarProximityDriver component exists in Enemy_Grunt hierarchy",
                ok44pd, detail44pd);

            // ════════════════════════════════════════════════════════════════
            // 45-47: M11.1 post-hit i-frame follow-up additions
            // ════════════════════════════════════════════════════════════════

            // ── 45: EnemyCombat._iFrameDuration SerializeField present ────────
            var ecType = typeof(EnemyCombat);
            var iFrameField = ecType.GetField("_iFrameDuration",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Check("45 EnemyCombat._iFrameDuration SerializeField present",
                iFrameField != null && iFrameField.FieldType == typeof(float),
                iFrameField != null ? "float field present" : "field missing");

            // ── 46: EnemyAnimationEventAbsorber.cs no longer exists ───────────
            bool ok46 = !File.Exists(
                Path.Combine(Application.dataPath, "..", "Assets/Scripts/Combat/EnemyAnimationEventAbsorber.cs"))
                && !File.Exists(
                Path.Combine(Application.dataPath, "..", "Assets/Scripts/Enemy/EnemyAnimationEventAbsorber.cs"));
            Check("46 EnemyAnimationEventAbsorber.cs deleted (both known paths)",
                ok46,
                ok46 ? "not found at either path (correct)" : "file still exists — delete it");

            // ── 47: OnHitboxOpen / OnHitboxClose methods on EnemyCombat ──────
            var onOpen  = ecType.GetMethod("OnHitboxOpen",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            var onClose = ecType.GetMethod("OnHitboxClose",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            Check("47a EnemyCombat.OnHitboxOpen() public void method",
                onOpen != null && onOpen.ReturnType == typeof(void),
                onOpen != null ? "signature OK" : "method missing");
            Check("47b EnemyCombat.OnHitboxClose() public void method",
                onClose != null && onClose.ReturnType == typeof(void),
                onClose != null ? "signature OK" : "method missing");

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
