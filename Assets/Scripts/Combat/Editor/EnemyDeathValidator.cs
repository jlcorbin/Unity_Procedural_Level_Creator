// EnemyDeathValidator.cs — read-only checks on M4-B wiring.
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Validate EnemyDeath
//
// 16 checks covering:
//   - CharacterStatsRuntime: OnDied event + IsDead property
//   - EnemyDeath script + attributes
//   - EnemyBaseController: Death param, Death state, AnyState→Death
//     with canTransitionToSelf=false, Death has no outgoing transitions
//   - Dummy.prefab has EnemyDeath with all three references wired
//   - EnemyHitReaction.cs source contains IsDead reference
//   - M4-A validator (EnemyHitReaction) re-runnable and clean

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Combat.EditorTools
{
    public static class EnemyDeathValidator
    {
        private const string EnemyDeathPath          = "Assets/Scripts/Combat/EnemyDeath.cs";
        private const string EnemyHitReactionSrcPath = "Assets/Scripts/Combat/EnemyHitReaction.cs";
        private const string EnemyControllerPath     = "Assets/Animators/Enemy/EnemyBaseController.controller";
        private const string DummyPrefabPath         = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";

        [MenuItem("LevelGen/Combat/Validate EnemyDeath")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: CharacterStatsRuntime.OnDied event ───────────────────────
            var statsType = typeof(CharacterStatsRuntime);
            var onDiedEvent = statsType.GetEvent("OnDied",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok1 = onDiedEvent != null
                       && onDiedEvent.EventHandlerType == typeof(Action<CharacterStatsRuntime>);
            Check("1 CharacterStatsRuntime.OnDied event of type Action<CharacterStatsRuntime>", ok1,
                onDiedEvent == null
                    ? "event missing"
                    : $"handlerType='{onDiedEvent.EventHandlerType.Name}'");

            // ── 2: CharacterStatsRuntime.IsDead public property ─────────────
            var isDeadProp = statsType.GetProperty("IsDead",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok2 = isDeadProp != null && isDeadProp.PropertyType == typeof(bool)
                       && isDeadProp.GetGetMethod() != null;
            Check("2 CharacterStatsRuntime.IsDead public bool property", ok2,
                isDeadProp == null
                    ? "property missing"
                    : $"type={isDeadProp.PropertyType.Name}, getter={(isDeadProp.GetGetMethod() != null ? "OK" : "missing")}");

            // ── 3: EnemyDeath.cs at expected path ───────────────────────────
            bool ok3 = AssetDatabase.LoadAssetAtPath<MonoScript>(EnemyDeathPath) != null;
            Check("3 EnemyDeath.cs exists at expected path", ok3,
                ok3 ? EnemyDeathPath : $"missing at {EnemyDeathPath}");

            // ── 4-6: EnemyDeath attributes ──────────────────────────────────
            var deathType = typeof(EnemyDeath);
            var requireAttrs = deathType.GetCustomAttributes<RequireComponent>();
            bool requireStats = false;
            bool requireTargetable = false;
            foreach (var a in requireAttrs)
            {
                if (a.m_Type0 == typeof(CharacterStatsRuntime)
                    || a.m_Type1 == typeof(CharacterStatsRuntime)
                    || a.m_Type2 == typeof(CharacterStatsRuntime))
                    requireStats = true;
                if (a.m_Type0 == typeof(Targetable)
                    || a.m_Type1 == typeof(Targetable)
                    || a.m_Type2 == typeof(Targetable))
                    requireTargetable = true;
            }
            Check("4 EnemyDeath has [RequireComponent(typeof(CharacterStatsRuntime))]", requireStats,
                requireStats ? "attribute present" : "attribute missing");
            Check("5 EnemyDeath has [RequireComponent(typeof(Targetable))]", requireTargetable,
                requireTargetable ? "attribute present" : "attribute missing");

            bool ok6 = deathType.GetCustomAttribute<DisallowMultipleComponent>() != null;
            Check("6 EnemyDeath has [DisallowMultipleComponent]", ok6,
                ok6 ? "attribute present" : "attribute missing");

            // ── 7-10: EnemyBaseController shape ─────────────────────────────
            var enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (enemyController == null)
            {
                string detail = $"missing at {EnemyControllerPath} — run " +
                                "'LevelGen ▶ Combat ▶ Build EnemyBaseController'";
                Check("7 EnemyBaseController has parameter 'Death' (Trigger)",     false, detail);
                Check("8 EnemyBaseController has state 'Death'",                   false, detail);
                Check("9 AnyState → Death transition with canTransitionToSelf=false", false, detail);
                Check("10 Death state has no outgoing transitions (terminal)",       false, detail);
            }
            else
            {
                // 7 — Death param Trigger
                bool ok7 = false;
                string detail7 = "parameter 'Death' not found";
                foreach (var p in enemyController.parameters)
                {
                    if (p.name == "Death")
                    {
                        ok7 = p.type == AnimatorControllerParameterType.Trigger;
                        detail7 = ok7 ? "type=Trigger" : $"type={p.type} (expected Trigger)";
                        break;
                    }
                }
                Check("7 EnemyBaseController has parameter 'Death' (Trigger)", ok7, detail7);

                // 8 — Death state
                AnimatorState deathState = null;
                if (enemyController.layers.Length > 0)
                {
                    var sm = enemyController.layers[0].stateMachine;
                    foreach (var sc in sm.states)
                    {
                        if (sc.state.name == "Death") { deathState = sc.state; break; }
                    }
                }
                Check("8 EnemyBaseController has state 'Death'", deathState != null,
                    deathState != null ? "found" : "missing");

                // 9 — AnyState → Death, canTransitionToSelf=false
                bool ok9 = false;
                string detail9 = "no AnyState transition to Death";
                if (deathState != null && enemyController.layers.Length > 0)
                {
                    var sm = enemyController.layers[0].stateMachine;
                    foreach (var t in sm.anyStateTransitions)
                    {
                        if (t.destinationState == deathState)
                        {
                            ok9 = !t.canTransitionToSelf;
                            detail9 = ok9
                                ? "found, canTransitionToSelf=false"
                                : $"found, canTransitionToSelf={t.canTransitionToSelf} (expected false)";
                            break;
                        }
                    }
                }
                Check("9 AnyState → Death transition with canTransitionToSelf=false", ok9, detail9);

                // 10 — Death has no outgoing transitions
                bool ok10 = deathState != null && deathState.transitions.Length == 0;
                Check("10 Death state has no outgoing transitions (terminal)", ok10,
                    deathState == null
                        ? "Death state missing — see check 8"
                        : $"outgoing transitions={deathState.transitions.Length} (expected 0)");
            }

            // ── 11-14: Dummy.prefab EnemyDeath wiring ───────────────────────
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            EnemyDeath deathComp = null;
            if (dummyPrefab != null) deathComp = dummyPrefab.GetComponent<EnemyDeath>();

            Check("11 Dummy.prefab has EnemyDeath on root", deathComp != null,
                dummyPrefab == null
                    ? $"prefab missing at {DummyPrefabPath}"
                    : (deathComp != null
                        ? "found on prefab root"
                        : "missing — re-run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'"));

            if (deathComp == null)
            {
                Check("12 EnemyDeath.animator field non-null",      false, "EnemyDeath missing — see check 11");
                Check("13 EnemyDeath.deathCollider field non-null", false, "EnemyDeath missing — see check 11");
                Check("14 EnemyDeath.hitReaction field non-null",   false, "EnemyDeath missing — see check 11");
            }
            else
            {
                var so = new SerializedObject(deathComp);

                var animProp     = so.FindProperty("animator");
                var colliderProp = so.FindProperty("deathCollider");
                var reactionProp = so.FindProperty("hitReaction");

                var animVal     = animProp     != null ? animProp.objectReferenceValue     as Animator         : null;
                var colliderVal = colliderProp != null ? colliderProp.objectReferenceValue as Collider         : null;
                var reactionVal = reactionProp != null ? reactionProp.objectReferenceValue as EnemyHitReaction : null;

                Check("12 EnemyDeath.animator field non-null", animVal != null,
                    animVal != null
                        ? $"wired to Animator on '{animVal.gameObject.name}'"
                        : "field unassigned");
                Check("13 EnemyDeath.deathCollider field non-null", colliderVal != null,
                    colliderVal != null
                        ? $"wired to {colliderVal.GetType().Name} on '{colliderVal.gameObject.name}'"
                        : "field unassigned");
                Check("14 EnemyDeath.hitReaction field non-null", reactionVal != null,
                    reactionVal != null
                        ? $"wired to EnemyHitReaction on '{reactionVal.gameObject.name}'"
                        : "field unassigned");
            }

            // ── 15: EnemyHitReaction.cs source contains IsDead reference ────
            bool ok15 = false;
            string detail15 = $"source missing at {EnemyHitReactionSrcPath}";
            string fullSrcPath = Path.Combine(Application.dataPath, "..", EnemyHitReactionSrcPath);
            if (File.Exists(fullSrcPath))
            {
                string src = File.ReadAllText(fullSrcPath);
                ok15 = src.Contains("IsDead");
                detail15 = ok15
                    ? "IsDead reference present"
                    : "no IsDead reference — Step 5 wiring not landed";
            }
            Check("15 EnemyHitReaction.cs contains IsDead reference", ok15, detail15);

            // ── 16: M4-A validator surface still present ────────────────────
            // Reflect on the M4-A validator's Run method rather than invoking
            // it (avoid flooding the console mid-run; let the user re-run
            // manually for full output). This confirms the M4-A wiring
            // surface didn't disappear.
            var m4aType = Type.GetType("LevelGen.Combat.EditorTools.EnemyHitReactionValidator, Assembly-CSharp-Editor");
            bool ok16 = m4aType != null
                        && m4aType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static) != null;
            Check("16 M4-A EnemyHitReactionValidator.Run still present", ok16,
                ok16
                    ? "found — re-run 'LevelGen ▶ Combat ▶ Validate EnemyHitReaction' for full M4-A check"
                    : "validator type or Run method missing");

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — EnemyDeath wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
