// EnemyAIValidator.cs — read-only checks on M10 wiring.
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Validate Enemy AI
//
// 16 read-only checks covering:
//   - EnemyAI script + RequireComponent attributes + State enum
//   - EnemyAnimationEventAbsorber script + OnHitboxOpen/Close methods
//   - EnemyBaseController parameter shape (MoveSpeed Float + Attack Trigger)
//   - Locomotion state with BlendTree motion + Attack state with Attack01 clip
//   - Idle↔Locomotion + AnyState→Attack + Attack→Locomotion transitions
//   - Dummy.prefab NavMeshAgent + EnemyAI + Absorber on child + EnemyAI._animator wired

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace LevelGen.Combat.EditorTools
{
    public static class EnemyAIValidator
    {
        private const string EnemyAIPath          = "Assets/Scripts/Combat/EnemyAI.cs";
        // M11 swap: Absorber path retained as a constant just so the
        // detail string says something sensible if the script vanishes
        // (validator now expects the Forwarder; Absorber.cs is gone).
        private const string AbsorberPath         = "Assets/Scripts/Combat/EnemyAnimationEventForwarder.cs";
        private const string EnemyControllerPath  = "Assets/Animators/Enemy/EnemyBaseController.controller";
        private const string DummyPrefabPath      = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string AttackClipName       = "Attack01_SwordAndShiled"; // typo preserved per pack swap notes

        [MenuItem("LevelGen/Combat/Validate Enemy AI")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: EnemyAI.cs at expected path ──────────────────────────────
            bool ok1 = AssetDatabase.LoadAssetAtPath<MonoScript>(EnemyAIPath) != null;
            Check("1 EnemyAI.cs exists at expected path", ok1,
                ok1 ? EnemyAIPath : $"missing at {EnemyAIPath}");

            // ── 2: EnemyAI [RequireComponent(NavMeshAgent)] ─────────────────
            var aiType = typeof(EnemyAI);
            var requires = aiType.GetCustomAttributes<RequireComponent>();
            bool hasNavAgent = false;
            foreach (var a in requires)
            {
                if (a.m_Type0 == typeof(NavMeshAgent)
                    || a.m_Type1 == typeof(NavMeshAgent)
                    || a.m_Type2 == typeof(NavMeshAgent))
                { hasNavAgent = true; break; }
            }
            Check("2 EnemyAI has [RequireComponent(typeof(NavMeshAgent))]", hasNavAgent,
                hasNavAgent ? "attribute present" : "attribute missing");

            // ── 3: EnemyAI [RequireComponent(CharacterStatsRuntime)] ────────
            bool hasStats = false;
            foreach (var a in requires)
            {
                if (a.m_Type0 == typeof(CharacterStatsRuntime)
                    || a.m_Type1 == typeof(CharacterStatsRuntime)
                    || a.m_Type2 == typeof(CharacterStatsRuntime))
                { hasStats = true; break; }
            }
            Check("3 EnemyAI has [RequireComponent(typeof(CharacterStatsRuntime))]", hasStats,
                hasStats ? "attribute present" : "attribute missing");

            // ── 4: EnemyAI.State enum with Idle/Chase/Attack/Cooldown ───────
            var stateType = aiType.GetNestedType("State");
            bool ok4 = false;
            string detail4 = "EnemyAI.State enum missing";
            if (stateType != null && stateType.IsEnum)
            {
                var names = System.Enum.GetNames(stateType);
                bool hasIdle     = System.Array.IndexOf(names, "Idle")     >= 0;
                bool hasChase    = System.Array.IndexOf(names, "Chase")    >= 0;
                bool hasAttack   = System.Array.IndexOf(names, "Attack")   >= 0;
                bool hasCooldown = System.Array.IndexOf(names, "Cooldown") >= 0;
                ok4 = hasIdle && hasChase && hasAttack && hasCooldown;
                detail4 = ok4
                    ? "Idle, Chase, Attack, Cooldown all present"
                    : $"Idle={hasIdle}, Chase={hasChase}, Attack={hasAttack}, Cooldown={hasCooldown}";
            }
            Check("4 EnemyAI.State enum has Idle/Chase/Attack/Cooldown", ok4, detail4);

            // ── 5: EnemyAnimationEventForwarder + OnHitboxOpen/Close methods ─
            // M11 replaced the M10 Absorber with the Forwarder. The
            // Absorber.cs file is now deleted; the Forwarder fills the
            // same Animator-GO-receiver role but routes events to
            // EnemyCombat instead of discarding them.
            bool ok5 = AssetDatabase.LoadAssetAtPath<MonoScript>(AbsorberPath) != null;
            string detail5 = ok5 ? AbsorberPath : $"missing at {AbsorberPath}";
            if (ok5)
            {
                var absType = typeof(EnemyAnimationEventForwarder);
                var openM   = absType.GetMethod("OnHitboxOpen",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, System.Type.EmptyTypes, null);
                var closeM  = absType.GetMethod("OnHitboxClose",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, System.Type.EmptyTypes, null);
                ok5 = openM != null && closeM != null;
                detail5 = ok5
                    ? "OnHitboxOpen + OnHitboxClose both public/parameterless"
                    : $"OnHitboxOpen={openM != null}, OnHitboxClose={closeM != null}";
            }
            Check("5 EnemyAnimationEventForwarder present + OnHitboxOpen/Close public", ok5, detail5);

            // ── 6: EnemyBaseController has MoveSpeed (Float) param ──────────
            var enemyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
            if (enemyController == null)
            {
                Check("6 EnemyBaseController has MoveSpeed (Float)", false,
                    $"controller missing at {EnemyControllerPath} — run 'LevelGen ▶ Combat ▶ Build EnemyBaseController'");
                Check("7 EnemyBaseController has Attack (Trigger)", false, "controller missing — see check 6");
                Check("8 EnemyBaseController has Locomotion state with BlendTree motion", false, "controller missing");
                Check("9 EnemyBaseController has Attack state with Attack01 clip", false, "controller missing");
                Check("10 Idle → Locomotion (MoveSpeed > 0.1)", false, "controller missing");
                Check("11 Locomotion → Idle (MoveSpeed < 0.1)", false, "controller missing");
                Check("12 AnyState → Attack with canTransitionToSelf=false", false, "controller missing");
                Check("13 Attack → Locomotion with hasExitTime=true and exitTime ≥ 0.9", false, "controller missing");
                Summary(pass, fail + 8);
                return;
            }
            bool ok6 = false;
            string detail6 = "param 'MoveSpeed' not found";
            foreach (var p in enemyController.parameters)
            {
                if (p.name == "MoveSpeed")
                {
                    ok6 = p.type == AnimatorControllerParameterType.Float;
                    detail6 = ok6 ? "type=Float" : $"type={p.type} (expected Float)";
                    break;
                }
            }
            Check("6 EnemyBaseController has MoveSpeed (Float)", ok6, detail6);

            // ── 7: EnemyBaseController has Attack (Trigger) param ───────────
            bool ok7 = false;
            string detail7 = "param 'Attack' not found";
            foreach (var p in enemyController.parameters)
            {
                if (p.name == "Attack")
                {
                    ok7 = p.type == AnimatorControllerParameterType.Trigger;
                    detail7 = ok7 ? "type=Trigger" : $"type={p.type} (expected Trigger)";
                    break;
                }
            }
            Check("7 EnemyBaseController has Attack (Trigger)", ok7, detail7);

            // ── 8: Locomotion state with BlendTree motion ───────────────────
            var rootSm = enemyController.layers[0].stateMachine;
            AnimatorState locomotionState = null;
            AnimatorState attackState     = null;
            AnimatorState idleState       = null;
            foreach (var sc in rootSm.states)
            {
                if (sc.state.name == "Locomotion") locomotionState = sc.state;
                if (sc.state.name == "Attack")     attackState     = sc.state;
                if (sc.state.name == "Idle")       idleState       = sc.state;
            }
            bool ok8 = locomotionState != null && locomotionState.motion is BlendTree;
            Check("8 EnemyBaseController has Locomotion state with BlendTree motion", ok8,
                locomotionState == null
                    ? "Locomotion state missing"
                    : (locomotionState.motion is BlendTree
                        ? $"motion={locomotionState.motion.name}"
                        : $"motion type={(locomotionState.motion != null ? locomotionState.motion.GetType().Name : "null")} (expected BlendTree)"));

            // ── 9: Attack state with Attack01_SwordAndShiled clip ───────────
            bool ok9 = attackState != null
                       && attackState.motion is AnimationClip clip
                       && clip.name == AttackClipName;
            Check($"9 EnemyBaseController Attack state motion = {AttackClipName}", ok9,
                attackState == null
                    ? "Attack state missing"
                    : (attackState.motion is AnimationClip c
                        ? $"motion={c.name} (expected {AttackClipName})"
                        : $"motion type={(attackState.motion != null ? attackState.motion.GetType().Name : "null")}"));

            // ── 10: Idle → Locomotion (MoveSpeed > 0.1) ─────────────────────
            bool ok10 = false;
            string detail10 = "Idle or Locomotion state missing — see checks 6 / 8";
            if (idleState != null && locomotionState != null)
            {
                foreach (var t in idleState.transitions)
                {
                    if (t.destinationState != locomotionState) continue;
                    foreach (var cond in t.conditions)
                    {
                        if (cond.parameter == "MoveSpeed"
                            && cond.mode == AnimatorConditionMode.Greater
                            && Mathf.Approximately(cond.threshold, 0.1f))
                        { ok10 = true; break; }
                    }
                    if (ok10) break;
                }
                detail10 = ok10
                    ? "Idle → Locomotion with MoveSpeed > 0.1 found"
                    : "no transition Idle → Locomotion with condition MoveSpeed > 0.1";
            }
            Check("10 Idle → Locomotion (MoveSpeed > 0.1)", ok10, detail10);

            // ── 11: Locomotion → Idle (MoveSpeed < 0.1) ─────────────────────
            bool ok11 = false;
            string detail11 = "Idle or Locomotion state missing";
            if (idleState != null && locomotionState != null)
            {
                foreach (var t in locomotionState.transitions)
                {
                    if (t.destinationState != idleState) continue;
                    foreach (var cond in t.conditions)
                    {
                        if (cond.parameter == "MoveSpeed"
                            && cond.mode == AnimatorConditionMode.Less
                            && Mathf.Approximately(cond.threshold, 0.1f))
                        { ok11 = true; break; }
                    }
                    if (ok11) break;
                }
                detail11 = ok11
                    ? "Locomotion → Idle with MoveSpeed < 0.1 found"
                    : "no transition Locomotion → Idle with condition MoveSpeed < 0.1";
            }
            Check("11 Locomotion → Idle (MoveSpeed < 0.1)", ok11, detail11);

            // ── 12: AnyState → Attack with canTransitionToSelf=false ────────
            bool ok12 = false;
            string detail12 = "Attack state missing";
            if (attackState != null)
            {
                foreach (var t in rootSm.anyStateTransitions)
                {
                    if (t.destinationState != attackState) continue;
                    bool hasAttackCond = false;
                    foreach (var cond in t.conditions)
                    {
                        if (cond.parameter == "Attack" && cond.mode == AnimatorConditionMode.If)
                        { hasAttackCond = true; break; }
                    }
                    if (!hasAttackCond) continue;
                    ok12 = !t.canTransitionToSelf;
                    detail12 = ok12
                        ? "found, canTransitionToSelf=false"
                        : $"found, canTransitionToSelf={t.canTransitionToSelf} (expected false)";
                    break;
                }
                if (!ok12 && detail12 == "Attack state missing")
                    detail12 = "no AnyState → Attack transition with Attack condition";
            }
            Check("12 AnyState → Attack with canTransitionToSelf=false", ok12, detail12);

            // ── 13: Attack → Locomotion (hasExitTime=true, exitTime ≥ 0.9) ──
            bool ok13 = false;
            string detail13 = "Attack or Locomotion state missing";
            if (attackState != null && locomotionState != null)
            {
                foreach (var t in attackState.transitions)
                {
                    if (t.destinationState != locomotionState) continue;
                    ok13 = t.hasExitTime && t.exitTime >= 0.9f;
                    detail13 = ok13
                        ? $"hasExitTime=true, exitTime={t.exitTime:F2}"
                        : $"hasExitTime={t.hasExitTime}, exitTime={t.exitTime:F2} (expected hasExitTime=true and exitTime≥0.9)";
                    break;
                }
                if (!ok13 && detail13 == "Attack or Locomotion state missing")
                    detail13 = "no transition Attack → Locomotion";
            }
            Check("13 Attack → Locomotion (hasExitTime=true, exitTime ≥ 0.9)", ok13, detail13);

            // ── 14: Dummy.prefab has NavMeshAgent ───────────────────────────
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            bool ok14 = false;
            string detail14 = $"prefab missing at {DummyPrefabPath}";
            NavMeshAgent dummyAgent = null;
            if (dummyPrefab != null)
            {
                dummyAgent = dummyPrefab.GetComponent<NavMeshAgent>();
                ok14 = dummyAgent != null;
                detail14 = ok14
                    ? $"NavMeshAgent on root (radius={dummyAgent.radius}, height={dummyAgent.height})"
                    : "NavMeshAgent missing — run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'";
            }
            Check("14 Dummy.prefab has NavMeshAgent on root", ok14, detail14);

            // ── 15: Dummy.prefab has EnemyAI with _animator wired ───────────
            bool ok15 = false;
            string detail15 = "Dummy prefab missing — see check 14";
            if (dummyPrefab != null)
            {
                var ai = dummyPrefab.GetComponent<EnemyAI>();
                if (ai == null)
                {
                    detail15 = "EnemyAI component missing on Dummy root";
                }
                else
                {
                    var so = new SerializedObject(ai);
                    var prop = so.FindProperty("_animator");
                    var assigned = prop != null ? prop.objectReferenceValue as Animator : null;
                    ok15 = assigned != null;
                    detail15 = ok15
                        ? $"EnemyAI._animator wired to '{assigned.gameObject.name}'"
                        : "EnemyAI._animator unassigned — re-run 'Build Dummy Prefab'";
                }
            }
            Check("15 Dummy.prefab has EnemyAI with _animator SerializeField wired", ok15, detail15);

            // ── 16: Dummy.prefab MaleCharacterPBR child has Forwarder ───────
            // M11 swap: child now carries EnemyAnimationEventForwarder
            // (replaced the M10 Absorber stub). EnemyCombatValidator's
            // checks 6 + 15 cover the same ground from a different angle.
            bool ok16 = false;
            string detail16 = "Dummy prefab missing — see check 14";
            if (dummyPrefab != null)
            {
                var animOnChild = dummyPrefab.GetComponentInChildren<Animator>(includeInactive: true);
                if (animOnChild == null)
                {
                    detail16 = "no child Animator on Dummy";
                }
                else
                {
                    var fwd = animOnChild.GetComponent<EnemyAnimationEventForwarder>();
                    ok16 = fwd != null;
                    detail16 = ok16
                        ? $"EnemyAnimationEventForwarder on '{animOnChild.gameObject.name}'"
                        : $"EnemyAnimationEventForwarder missing on '{animOnChild.gameObject.name}' — " +
                          "re-run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'";
                }
            }
            Check("16 Dummy.prefab MaleCharacterPBR child has EnemyAnimationEventForwarder (M11)", ok16, detail16);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Enemy AI wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
