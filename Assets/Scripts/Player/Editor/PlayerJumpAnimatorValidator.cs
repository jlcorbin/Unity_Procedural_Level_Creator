// PlayerJumpAnimatorValidator.cs — M2-B Step 4 validation pass.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Jump Animator (M2-B Step 4)
//
// Runs the seven checks specified in the M2-B Step 4 prompt against
// PlayerBaseController.controller and PlayerOverride_MaleHero.overrideController.
// Read-only — does not modify any asset. Prints PASS / FAIL / WARN with the
// actual value, then a final SUMMARY line.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.EditorTools
{
    public static class PlayerJumpAnimatorValidator
    {
        const string ControllerPath = "Assets/Animators/Player/PlayerBaseController.controller";
        const string OverridePath   = "Assets/Animators/Player/PlayerOverride_MaleHero.overrideController";

        const string JumpStartClip = "JumpStart_Normal_InPlace_SwordAndShield";
        const string JumpAirClip   = "JumpAir_Normal_InPlace_SwordAndShield";
        const string JumpEndClip   = "JumpEnd_Normal_InPlace_SwordAndShield";

        [MenuItem("LevelGen/Player/Validate Jump Animator (M2-B Step 4)")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;
            int warn = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            void Warn(string label, string detail)
            {
                warn++;
                Debug.LogWarning($"[Validator] WARN — {label}: {detail}");
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
            var jumpParam = controller.parameters.FirstOrDefault(p =>
                p.name == "Jump" && p.type == AnimatorControllerParameterType.Trigger);
            var groundedParam = controller.parameters.FirstOrDefault(p =>
                p.name == "IsGrounded" && p.type == AnimatorControllerParameterType.Bool);

            Check("1a Jump Trigger param", jumpParam != null,
                jumpParam != null ? "present"
                                  : $"params: [{string.Join(", ", controller.parameters.Select(p => $"{p.name}({p.type})"))}]");
            Check("1b IsGrounded Bool param", groundedParam != null,
                groundedParam != null ? "present"
                                      : $"params: [{string.Join(", ", controller.parameters.Select(p => $"{p.name}({p.type})"))}]");
            if (groundedParam != null)
            {
                Check("1c IsGrounded default = true", groundedParam.defaultBool,
                    $"defaultBool={groundedParam.defaultBool}");
            }

            // ── Check 2: State presence ─────────────────────────────────────
            var rootSm    = controller.layers[0].stateMachine;
            var startState = rootSm.states.FirstOrDefault(s => s.state.name == "JumpStart").state;
            var airState   = rootSm.states.FirstOrDefault(s => s.state.name == "JumpAir").state;
            var endState   = rootSm.states.FirstOrDefault(s => s.state.name == "JumpEnd").state;
            var idleState  = rootSm.states.FirstOrDefault(s => s.state.name == "Idle").state;

            Check("2a JumpStart state", startState != null,
                startState != null ? "found in Base Layer"
                                   : $"states: [{string.Join(", ", rootSm.states.Select(s => s.state.name))}]");
            Check("2b JumpAir state", airState != null,
                airState != null ? "found"
                                 : "missing");
            Check("2c JumpEnd state", endState != null,
                endState != null ? "found"
                                 : "missing");

            // ── Check 3: State motion non-null + name match ─────────────────
            // M2 strafe lesson: verify clips actually resolve, not just slots exist.
            AnimationClip startClip = null, airClip = null, endClip = null;
            if (startState != null)
            {
                startClip = startState.motion as AnimationClip;
                bool ok = startClip != null && startClip.name == JumpStartClip;
                Check("3a JumpStart.motion resolves", ok,
                    startClip == null ? "motion is null"
                                      : $"clip.name = '{startClip.name}' (expected '{JumpStartClip}')");
            }
            if (airState != null)
            {
                airClip = airState.motion as AnimationClip;
                bool ok = airClip != null && airClip.name == JumpAirClip;
                Check("3b JumpAir.motion resolves", ok,
                    airClip == null ? "motion is null"
                                    : $"clip.name = '{airClip.name}' (expected '{JumpAirClip}')");
            }
            if (endState != null)
            {
                endClip = endState.motion as AnimationClip;
                bool ok = endClip != null && endClip.name == JumpEndClip;
                Check("3c JumpEnd.motion resolves", ok,
                    endClip == null ? "motion is null"
                                    : $"clip.name = '{endClip.name}' (expected '{JumpEndClip}')");
            }

            // ── Check 4: Override resolution for new slots ──────────────────
            // GUID-free: walk override pairs by clip-name.
            var overridePairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(overridePairs);
            bool startKey = overridePairs.Any(p => p.Key != null && p.Key.name == JumpStartClip);
            bool airKey   = overridePairs.Any(p => p.Key != null && p.Key.name == JumpAirClip);
            bool endKey   = overridePairs.Any(p => p.Key != null && p.Key.name == JumpEndClip);
            Check("4a JumpStart override slot", startKey,
                startKey ? "key present" : "missing");
            Check("4b JumpAir override slot", airKey,
                airKey ? "key present" : "missing");
            Check("4c JumpEnd override slot", endKey,
                endKey ? "key present" : "missing");

            // ── Check 5: Transition counts ──────────────────────────────────
            // Expected after Step 4:
            //   - 11 (Step 2) + 7 (Step 4) = 18 state-to-state
            //   - 1 anyStateTransition (N5: AnyState → Hit, unchanged from Step 2)
            int totalStateTransitions = rootSm.states.Sum(s => s.state.transitions.Length);
            int anyStateTransitions   = rootSm.anyStateTransitions.Length;
            Check("5a State-to-state transitions == 18",
                totalStateTransitions == 18,
                $"got {totalStateTransitions} (per-state: " +
                string.Join(", ", rootSm.states.Select(s => $"{s.state.name}={s.state.transitions.Length}")) + ")");
            Check("5b AnyState transitions == 1",
                anyStateTransitions == 1,
                $"got {anyStateTransitions}");

            // ── Check 6: Specific transition checks ─────────────────────────
            //   6a-c: N7/N8/N9 each have 2 conditions (Jump + IsGrounded, both If)
            //   6d:   N10 (JumpStart→JumpAir conditional) has 1 condition
            //         (IsGrounded IfNot)
            //   6e:   N11 (JumpStart→JumpAir fallback) hasExitTime=true,
            //         exitTime≈0.95, no conditions
            //   6f:   N12 (JumpAir→JumpEnd) has 1 condition (IsGrounded If)
            //   6g:   N13 (JumpEnd→Idle) hasExitTime=true, exitTime≈0.85,
            //         no conditions
            void CheckSourceToJump(string label, AnimatorState src)
            {
                if (src == null) return;
                var t = src.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "JumpStart");
                bool ok = t != null
                       && t.conditions != null && t.conditions.Length == 2
                       && t.conditions.Any(c => c.parameter == "Jump"       && c.mode == AnimatorConditionMode.If)
                       && t.conditions.Any(c => c.parameter == "IsGrounded" && c.mode == AnimatorConditionMode.If);
                Check(label, ok,
                    t == null ? "transition not found"
                              : $"conditions=[{string.Join(", ", t.conditions.Select(c => $"{c.parameter}({c.mode})"))}]");
            }

            CheckSourceToJump("6a Idle→JumpStart conditions",       idleState);
            CheckSourceToJump("6b Locomotion→JumpStart conditions", rootSm.states.FirstOrDefault(s => s.state.name == "Locomotion").state);
            CheckSourceToJump("6c Sprint→JumpStart conditions",     rootSm.states.FirstOrDefault(s => s.state.name == "Sprint").state);

            if (startState != null)
            {
                // JumpStart has two outgoing transitions to JumpAir: N10 conditional + N11 fallback.
                var startToAir = startState.transitions.Where(x =>
                    x.destinationState != null && x.destinationState.name == "JumpAir").ToArray();

                var n10 = startToAir.FirstOrDefault(t => !t.hasExitTime && t.conditions.Length == 1);
                bool n10Ok = n10 != null
                          && n10.conditions[0].parameter == "IsGrounded"
                          && n10.conditions[0].mode == AnimatorConditionMode.IfNot;
                Check("6d JumpStart→JumpAir conditional (IsGrounded IfNot)", n10Ok,
                    n10 == null ? "no transition with single condition + no exit-time found"
                                : $"cond=[{n10.conditions[0].parameter}({n10.conditions[0].mode})], hasExit={n10.hasExitTime}");

                var n11 = startToAir.FirstOrDefault(t => t.hasExitTime && (t.conditions == null || t.conditions.Length == 0));
                bool n11Ok = n11 != null && Mathf.Approximately(n11.exitTime, 0.95f);
                Check("6e JumpStart→JumpAir fallback exitTime≈0.95", n11Ok,
                    n11 == null ? "no fallback transition (hasExitTime+0 conds) found"
                                : $"exitTime={n11.exitTime}, conditions={n11.conditions.Length}");
            }

            if (airState != null)
            {
                var n12 = airState.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "JumpEnd");
                bool n12Ok = n12 != null
                          && n12.conditions != null && n12.conditions.Length == 1
                          && n12.conditions[0].parameter == "IsGrounded"
                          && n12.conditions[0].mode == AnimatorConditionMode.If;
                Check("6f JumpAir→JumpEnd condition (IsGrounded If)", n12Ok,
                    n12 == null ? "transition not found"
                                : $"conditions=[{string.Join(", ", n12.conditions.Select(c => $"{c.parameter}({c.mode})"))}]");
            }

            if (endState != null)
            {
                var n13 = endState.transitions.FirstOrDefault(x =>
                    x.destinationState != null && x.destinationState.name == "Idle");
                bool n13Ok = n13 != null && n13.hasExitTime && Mathf.Approximately(n13.exitTime, 0.85f);
                Check("6g JumpEnd→Idle hasExitTime=true & exitTime≈0.85", n13Ok,
                    n13 == null ? "transition not found"
                                : $"hasExitTime={n13.hasExitTime}, exitTime={n13.exitTime}");
            }

            // ── Check 7: JumpAir state loop check (clip-side) ───────────────
            // Per Step 4 design: JumpAir clip should have isLooping=true so the
            // Air state continues cycling while airborne. State-level loop is
            // not exposed on AnimatorState; the clip flag is the source of truth.
            if (airClip != null)
            {
                if (airClip.isLooping)
                {
                    pass++;
                    Debug.Log($"[Validator] PASS — 7 JumpAir clip is looping: clip.isLooping=true");
                }
                else
                {
                    Warn("7 JumpAir clip is NOT looping",
                        $"clip.isLooping=false. Set loopTime=1 in the FBX import settings: " +
                        $"Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpAir_Normal_InPlace_SwordAndShield.fbx");
                }
            }

            // ── Summary ─────────────────────────────────────────────────────
            string summary = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL / {warn} WARN";
            if (fail == 0 && warn == 0) Debug.Log(summary + " — all M2-B Step 4 checks passed cleanly.");
            else if (fail == 0)         Debug.LogWarning(summary + " — passed with warnings; review above.");
            else                        Debug.LogError(summary + " — see FAIL lines above; revert via git if needed.");
        }
    }
}
#endif
