// PlayerComboAnimatorValidator.cs — M2-B Step 6 validation pass.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Combo Animator (M2-B Step 6)
//
// Runs the nine checks specified in the M2-B Step 6 prompt against
// PlayerBaseController.controller and PlayerOverride_MaleHero.overrideController.
// Read-only — does not modify any asset. Prints PASS / FAIL with the
// actual value, then a final SUMMARY line.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.EditorTools
{
    public static class PlayerComboAnimatorValidator
    {
        const string ControllerPath = "Assets/Animators/Player/PlayerBaseController.controller";
        const string OverridePath   = "Assets/Animators/Player/PlayerOverride_MaleHero.overrideController";

        const string Attack02Clip = "Attack02_SwordAndShiled";
        const string Attack03Clip = "Attack03_SwordAndShiled";

        [MenuItem("LevelGen/Player/Validate Combo Animator (M2-B Step 6)")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[Validator] FAIL — could not load controller at {ControllerPath}");
                return;
            }
            var overrideCtrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
            if (overrideCtrl == null)
            {
                Debug.LogError($"[Validator] FAIL — could not load override controller at {OverridePath}");
                return;
            }

            // ── Check 1: Param presence ─────────────────────────────────────
            var comboParam = controller.parameters.FirstOrDefault(p =>
                p.name == "ComboNext" && p.type == AnimatorControllerParameterType.Trigger);
            Check("1 ComboNext Trigger param", comboParam != null,
                comboParam != null ? "present"
                                   : $"params: [{string.Join(", ", controller.parameters.Select(p => $"{p.name}({p.type})"))}]");

            // ── Check 2: State presence ─────────────────────────────────────
            var rootSm     = controller.layers[0].stateMachine;
            var attackState   = rootSm.states.FirstOrDefault(s => s.state.name == "Attack").state;
            var attack02State = rootSm.states.FirstOrDefault(s => s.state.name == "Attack02").state;
            var attack03State = rootSm.states.FirstOrDefault(s => s.state.name == "Attack03").state;
            var idleState     = rootSm.states.FirstOrDefault(s => s.state.name == "Idle").state;

            Check("2a Attack02 state", attack02State != null,
                attack02State != null ? "found in Base Layer"
                                      : $"states: [{string.Join(", ", rootSm.states.Select(s => s.state.name))}]");
            Check("2b Attack03 state", attack03State != null,
                attack03State != null ? "found"
                                      : "missing");

            // ── Check 3: State motion non-null + name match ─────────────────
            // M2 strafe lesson: verify clips actually resolve, not just slots exist.
            AnimationClip clip02 = null, clip03 = null;
            if (attack02State != null)
            {
                clip02 = attack02State.motion as AnimationClip;
                bool ok = clip02 != null && clip02.name == Attack02Clip;
                Check("3a Attack02.motion resolves", ok,
                    clip02 == null ? "motion is null"
                                   : $"clip.name = '{clip02.name}' (expected '{Attack02Clip}')");
            }
            if (attack03State != null)
            {
                clip03 = attack03State.motion as AnimationClip;
                bool ok = clip03 != null && clip03.name == Attack03Clip;
                Check("3b Attack03.motion resolves", ok,
                    clip03 == null ? "motion is null"
                                   : $"clip.name = '{clip03.name}' (expected '{Attack03Clip}')");
            }

            // ── Check 4: Override resolution for new slots ──────────────────
            // GUID-free: walk override pairs by clip-name.
            var overridePairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(overridePairs);
            bool key02 = overridePairs.Any(p => p.Key != null && p.Key.name == Attack02Clip);
            bool key03 = overridePairs.Any(p => p.Key != null && p.Key.name == Attack03Clip);
            Check("4a Attack02 override slot", key02,
                key02 ? "key present" : "missing");
            Check("4b Attack03 override slot", key03,
                key03 ? "key present" : "missing");

            // ── Check 5: Transition counts ──────────────────────────────────
            // Expected after Step 6:
            //   - 18 (Step 4) + 4 (Step 6) = 22 state-to-state
            //   - 1 anyStateTransition (N5: AnyState → Hit, unchanged)
            int totalStateTransitions = rootSm.states.Sum(s => s.state.transitions.Length);
            int anyStateTransitions   = rootSm.anyStateTransitions.Length;
            Check("5a State-to-state transitions == 22",
                totalStateTransitions == 22,
                $"got {totalStateTransitions} (per-state: " +
                string.Join(", ", rootSm.states.Select(s => $"{s.state.name}={s.state.transitions.Length}")) + ")");
            Check("5b AnyState transitions == 1",
                anyStateTransitions == 1,
                $"got {anyStateTransitions}");

            // ── Check 6: Per-state transition counts ────────────────────────
            if (attackState != null)
            {
                Check("6a Attack.transitions.Length == 2",
                    attackState.transitions.Length == 2,
                    $"got {attackState.transitions.Length}");
            }
            if (attack02State != null)
            {
                Check("6b Attack02.transitions.Length == 2",
                    attack02State.transitions.Length == 2,
                    $"got {attack02State.transitions.Length}");
            }
            if (attack03State != null)
            {
                Check("6c Attack03.transitions.Length == 1",
                    attack03State.transitions.Length == 1,
                    $"got {attack03State.transitions.Length}");
            }

            // ── Check 7: Transition order on Attack ─────────────────────────
            // [0] = N14 (Attack→Attack02, ComboNext If)
            // [1] = N4  (Attack→Idle, no condition)
            if (attackState != null && attackState.transitions.Length >= 2)
            {
                var t0 = attackState.transitions[0];
                var t1 = attackState.transitions[1];

                bool t0Ok = t0.destinationState != null
                         && t0.destinationState.name == "Attack02"
                         && t0.conditions != null && t0.conditions.Length == 1
                         && t0.conditions[0].parameter == "ComboNext"
                         && t0.conditions[0].mode == AnimatorConditionMode.If;
                Check("7a Attack.transitions[0] = ComboNext→Attack02", t0Ok,
                    t0.destinationState == null ? "destination null"
                                                : $"dst='{t0.destinationState.name}', conds=[{string.Join(", ", t0.conditions.Select(c => $"{c.parameter}({c.mode})"))}]");

                bool t1Ok = t1.destinationState != null
                         && t1.destinationState.name == "Idle"
                         && (t1.conditions == null || t1.conditions.Length == 0);
                Check("7b Attack.transitions[1] = noCond→Idle", t1Ok,
                    t1.destinationState == null ? "destination null"
                                                : $"dst='{t1.destinationState.name}', conds={t1.conditions.Length}");
            }

            // ── Check 8: Transition order on Attack02 ───────────────────────
            // [0] = N15 (Attack02→Attack03, ComboNext If)
            // [1] = N16 (Attack02→Idle, no condition)
            if (attack02State != null && attack02State.transitions.Length >= 2)
            {
                var t0 = attack02State.transitions[0];
                var t1 = attack02State.transitions[1];

                bool t0Ok = t0.destinationState != null
                         && t0.destinationState.name == "Attack03"
                         && t0.conditions != null && t0.conditions.Length == 1
                         && t0.conditions[0].parameter == "ComboNext"
                         && t0.conditions[0].mode == AnimatorConditionMode.If;
                Check("8a Attack02.transitions[0] = ComboNext→Attack03", t0Ok,
                    t0.destinationState == null ? "destination null"
                                                : $"dst='{t0.destinationState.name}', conds=[{string.Join(", ", t0.conditions.Select(c => $"{c.parameter}({c.mode})"))}]");

                bool t1Ok = t1.destinationState != null
                         && t1.destinationState.name == "Idle"
                         && (t1.conditions == null || t1.conditions.Length == 0);
                Check("8b Attack02.transitions[1] = noCond→Idle", t1Ok,
                    t1.destinationState == null ? "destination null"
                                                : $"dst='{t1.destinationState.name}', conds={t1.conditions.Length}");
            }

            // ── Check 9: Exit-time / has-exit-time spot checks ──────────────
            // Design correction (post-Step 7 runtime test):
            //   N14 (Attack→Attack02):    hasExitTime=FALSE, condition-only (ComboNext)
            //   N15 (Attack02→Attack03):  hasExitTime=FALSE, condition-only (ComboNext)
            //   N16 (Attack02→Idle):      hasExitTime=TRUE,  exitTime≈0.90 (fallback)
            //   N17 (Attack03→Idle):      hasExitTime=TRUE,  exitTime≈0.90 (always)
            //
            // Why N14/N15 dropped Has Exit Time:
            //   Empirical Unity 6.4 behavior — "Has Exit Time + Trigger
            //   condition" auto-fires the transition at exitTime regardless
            //   of whether the trigger is set. The combo would chain Attack
            //   → Attack02 every time, no buffer required. Dropping
            //   hasExitTime makes the transition condition-only; PlayerCombat
            //   gates SetComboNext() at n>=bufferConsumeAt (0.85), so the
            //   effective fire time is unchanged but the gate is now
            //   enforced by script rather than by Animator exit-time.
            void CheckExitTimeFalse(string label, AnimatorStateTransition t)
            {
                if (t == null) { Check(label, false, "transition not found"); return; }
                bool ok = !t.hasExitTime;
                Check(label, ok, $"hasExitTime={t.hasExitTime} (expected false)");
            }

            void CheckExitTimeTrue(string label, AnimatorStateTransition t, float expected)
            {
                if (t == null) { Check(label, false, "transition not found"); return; }
                bool ok = t.hasExitTime && Mathf.Approximately(t.exitTime, expected);
                Check(label, ok,
                    $"hasExitTime={t.hasExitTime}, exitTime={t.exitTime} (expected true, {expected})");
            }

            if (attackState != null)
            {
                var n14 = attackState.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "Attack02");
                CheckExitTimeFalse("9a N14 (Attack→Attack02) hasExitTime=false (condition-only)", n14);
            }
            if (attack02State != null)
            {
                var n15 = attack02State.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "Attack03");
                CheckExitTimeFalse("9b N15 (Attack02→Attack03) hasExitTime=false (condition-only)", n15);

                var n16 = attack02State.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "Idle");
                CheckExitTimeTrue("9c N16 (Attack02→Idle) hasExitTime=true & exit≈0.90", n16, 0.90f);
            }
            if (attack03State != null)
            {
                var n17 = attack03State.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "Idle");
                CheckExitTimeTrue("9d N17 (Attack03→Idle) hasExitTime=true & exit≈0.90", n17, 0.90f);
            }

            // ── Summary ─────────────────────────────────────────────────────
            string summary = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(summary + " — all M2-B Step 6 checks passed.");
            else           Debug.LogError(summary + " — see FAIL lines above; revert via git if needed.");
        }
    }
}
#endif
