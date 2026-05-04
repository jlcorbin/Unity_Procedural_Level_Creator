// EnemyHitReactionValidator.cs — read-only checks on M4-A wiring.
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Validate EnemyHitReaction
//
// 14 checks covering:
//   - Targetable event surface (OnHit + RaiseHit)
//   - EnemyHitReaction script + attributes
//   - EnemyBaseController asset + parameter/state/transition shape
//   - Dummy.prefab references the new controller and carries the
//     EnemyHitReaction component with a wired Animator field
//   - PlayerCombat source contains the RaiseHit call

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
    public static class EnemyHitReactionValidator
    {
        private const string EnemyHitReactionPath = "Assets/Scripts/Combat/EnemyHitReaction.cs";
        private const string EnemyControllerPath  = "Assets/Animators/Enemy/EnemyBaseController.controller";
        private const string PlayerControllerPath = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string DummyPrefabPath      = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string PlayerCombatSrcPath  = "Assets/Scripts/Player/PlayerCombat.cs";

        [MenuItem("LevelGen/Combat/Validate EnemyHitReaction")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: Targetable.OnHit event of type Action<Vector3, float> ────
            // M8 extended the payload from Action<Vector3> → Action<Vector3, float>
            // so DamageNumberSpawner can read the damage value alongside the
            // hit point. Hit-reaction subscribers ignore the new param.
            var targetableType = typeof(Targetable);
            var onHitEvent = targetableType.GetEvent("OnHit",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok1 = onHitEvent != null
                       && onHitEvent.EventHandlerType == typeof(Action<Vector3, float>);
            Check("1 Targetable.OnHit event of type Action<Vector3, float>", ok1,
                onHitEvent == null
                    ? "event missing"
                    : $"handlerType='{onHitEvent.EventHandlerType.Name}' (expected Action`2[Vector3, float])");

            // ── 2: Targetable.RaiseHit(Vector3, float) public ───────────────
            var raiseHitMethod = targetableType.GetMethod("RaiseHit",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            Check("2 Targetable.RaiseHit(Vector3, float) public", raiseHitMethod != null,
                raiseHitMethod != null ? "found" : "missing or wrong signature");

            // ── 3: EnemyHitReaction.cs at expected path ─────────────────────
            bool ok3 = AssetDatabase.LoadAssetAtPath<MonoScript>(EnemyHitReactionPath) != null;
            Check("3 EnemyHitReaction.cs exists at expected path", ok3,
                ok3 ? EnemyHitReactionPath : $"missing at {EnemyHitReactionPath}");

            // ── 4-5: EnemyHitReaction attributes ────────────────────────────
            var reactionType = typeof(EnemyHitReaction);
            var requireAttrs = reactionType.GetCustomAttributes<RequireComponent>();
            bool ok4 = false;
            foreach (var a in requireAttrs)
            {
                if (a.m_Type0 == typeof(Targetable)
                    || a.m_Type1 == typeof(Targetable)
                    || a.m_Type2 == typeof(Targetable))
                {
                    ok4 = true;
                    break;
                }
            }
            Check("4 EnemyHitReaction has [RequireComponent(typeof(Targetable))]", ok4,
                ok4 ? "attribute present" : "attribute missing");

            bool ok5 = reactionType.GetCustomAttribute<DisallowMultipleComponent>() != null;
            Check("5 EnemyHitReaction has [DisallowMultipleComponent]", ok5,
                ok5 ? "attribute present" : "attribute missing");

            // ── 6: EnemyBaseController asset exists ─────────────────────────
            var enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            Check("6 EnemyBaseController.controller exists", enemyController != null,
                enemyController != null
                    ? EnemyControllerPath
                    : $"missing at {EnemyControllerPath} — run 'LevelGen ▶ Combat ▶ Build EnemyBaseController'");

            // ── 7: Hit parameter (Trigger) ──────────────────────────────────
            bool ok7 = false;
            string detail7 = "controller missing — see check 6";
            if (enemyController != null)
            {
                foreach (var p in enemyController.parameters)
                {
                    if (p.name == "Hit")
                    {
                        ok7 = p.type == AnimatorControllerParameterType.Trigger;
                        detail7 = ok7 ? "type=Trigger" : $"type={p.type} (expected Trigger)";
                        break;
                    }
                }
                if (!ok7 && detail7 == "controller missing — see check 6")
                    detail7 = "parameter 'Hit' not found";
            }
            Check("7 EnemyBaseController has parameter 'Hit' (Trigger)", ok7, detail7);

            // ── 8: Idle (default) + Hit states ──────────────────────────────
            AnimatorState idleState = null, hitState = null;
            bool idleIsDefault = false;
            if (enemyController != null && enemyController.layers.Length > 0)
            {
                var sm = enemyController.layers[0].stateMachine;
                foreach (var sc in sm.states)
                {
                    if (sc.state.name == "Idle") idleState = sc.state;
                    if (sc.state.name == "Hit")  hitState  = sc.state;
                }
                idleIsDefault = idleState != null && sm.defaultState == idleState;
            }
            bool ok8 = idleState != null && hitState != null && idleIsDefault;
            Check("8 EnemyBaseController has states Idle (default) + Hit", ok8,
                $"Idle={(idleState != null ? "found" : "missing")}, " +
                $"Hit={(hitState != null ? "found" : "missing")}, " +
                $"Idle is default={idleIsDefault}");

            // ── 9: AnyState → Hit, canTransitionToSelf == false ─────────────
            bool ok9 = false;
            string detail9 = "controller missing — see check 6";
            if (enemyController != null && hitState != null && enemyController.layers.Length > 0)
            {
                var sm = enemyController.layers[0].stateMachine;
                foreach (var t in sm.anyStateTransitions)
                {
                    if (t.destinationState == hitState)
                    {
                        ok9 = !t.canTransitionToSelf;
                        detail9 = ok9
                            ? "found, canTransitionToSelf=false"
                            : $"found, canTransitionToSelf={t.canTransitionToSelf} (expected false)";
                        break;
                    }
                }
                if (!ok9 && detail9 == "controller missing — see check 6")
                    detail9 = "no AnyState transition to Hit";
            }
            Check("9 AnyState → Hit transition with canTransitionToSelf=false", ok9, detail9);

            // ── 10: Hit → Idle, hasExitTime == true ─────────────────────────
            bool ok10 = false;
            string detail10 = "controller missing — see check 6";
            if (hitState != null && idleState != null)
            {
                foreach (var t in hitState.transitions)
                {
                    if (t.destinationState == idleState)
                    {
                        ok10 = t.hasExitTime;
                        detail10 = ok10
                            ? $"found, hasExitTime=true, exitTime={t.exitTime:F2}"
                            : "found, hasExitTime=false (expected true)";
                        break;
                    }
                }
                if (!ok10 && detail10 == "controller missing — see check 6")
                    detail10 = "no Hit → Idle transition";
            }
            Check("10 Hit → Idle transition with hasExitTime=true", ok10, detail10);

            // ── 11: Dummy.prefab Animator references EnemyBaseController ────
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            bool ok11 = false;
            string detail11 = $"prefab missing at {DummyPrefabPath}";
            Animator dummyAnimator = null;
            if (dummyPrefab != null)
            {
                dummyAnimator = dummyPrefab.GetComponentInChildren<Animator>(includeInactive: true);
                if (dummyAnimator == null)
                {
                    detail11 = "no Animator in Dummy hierarchy";
                }
                else
                {
                    var assigned = dummyAnimator.runtimeAnimatorController as AnimatorController;
                    string assignedPath = assigned != null
                        ? AssetDatabase.GetAssetPath(assigned)
                        : "(null)";
                    ok11 = assigned == enemyController && enemyController != null;
                    if (assignedPath == PlayerControllerPath)
                        detail11 = "still pointing at PlayerBaseController — re-run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'";
                    else
                        detail11 = ok11
                            ? $"references {assignedPath}"
                            : $"references {assignedPath} (expected {EnemyControllerPath})";
                }
            }
            Check("11 Dummy.prefab Animator references EnemyBaseController", ok11, detail11);

            // ── 12: Dummy.prefab has EnemyHitReaction ───────────────────────
            EnemyHitReaction reaction = null;
            if (dummyPrefab != null) reaction = dummyPrefab.GetComponent<EnemyHitReaction>();
            Check("12 Dummy.prefab has EnemyHitReaction on root", reaction != null,
                reaction != null
                    ? "found on prefab root"
                    : "missing — re-run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'");

            // ── 13: EnemyHitReaction.animator field wired to child Animator ─
            bool ok13 = false;
            string detail13 = "EnemyHitReaction missing — see check 12";
            if (reaction != null)
            {
                var so = new SerializedObject(reaction);
                var prop = so.FindProperty("animator");
                var assigned = prop != null ? prop.objectReferenceValue as Animator : null;
                if (assigned == null)
                {
                    detail13 = "EnemyHitReaction.animator field unassigned";
                }
                else if (dummyAnimator != null && assigned == dummyAnimator)
                {
                    ok13 = true;
                    detail13 = $"wired to Animator on '{assigned.gameObject.name}'";
                }
                else
                {
                    detail13 = $"wired to '{assigned.gameObject.name}' (expected Dummy's child Animator)";
                }
            }
            Check("13 EnemyHitReaction.animator field wired to child Animator", ok13, detail13);

            // ── 14: PlayerCombat.cs source contains RaiseHit( call ──────────
            bool ok14 = false;
            string detail14 = $"source missing at {PlayerCombatSrcPath}";
            string fullSrcPath = Path.Combine(Application.dataPath, "..", PlayerCombatSrcPath);
            if (File.Exists(fullSrcPath))
            {
                string src = File.ReadAllText(fullSrcPath);
                ok14 = src.Contains("RaiseHit(");
                detail14 = ok14
                    ? "RaiseHit( call present"
                    : "no RaiseHit( call — Step 3 wiring not landed";
            }
            Check("14 PlayerCombat.cs contains RaiseHit( call", ok14, detail14);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — EnemyHitReaction wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
