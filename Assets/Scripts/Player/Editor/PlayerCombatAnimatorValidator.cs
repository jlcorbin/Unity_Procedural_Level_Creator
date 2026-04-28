// PlayerCombatAnimatorValidator.cs — M2-B Step 2 validation pass.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Combat Animator (M2-B Step 2)
//
// Runs the six checks specified in the M2-B Step 2 prompt against
// PlayerBaseController.controller and PlayerOverride_MaleHero.overrideController.
// Read-only — does not modify any asset. Each check prints PASS or FAIL with
// the actual value, then a final SUMMARY line.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.EditorTools
{
    public static class PlayerCombatAnimatorValidator
    {
        const string ControllerPath = "Assets/Animators/Player/PlayerBaseController.controller";
        const string OverridePath   = "Assets/Animators/Player/PlayerOverride_MaleHero.overrideController";
        const string Attack01Fbx    = "Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/Attack01_SwordAndShiled.fbx";
        const string Attack01Clip   = "Attack01_SwordAndShiled";
        const string GetHit01Fbx    = "Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/GetHit01_SwordAndShield.fbx";
        const string GetHit01Clip   = "GetHit01_SwordAndShield";

        [MenuItem("LevelGen/Player/Validate Combat Animator (M2-B Step 2)")]
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

            // ── Check 1: Param presence (Attack + Hit, both Trigger) ────────
            bool attackParam = controller.parameters.Any(p =>
                p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger);
            bool hitParam = controller.parameters.Any(p =>
                p.name == "Hit" && p.type == AnimatorControllerParameterType.Trigger);
            Check("1a Attack Trigger param", attackParam,
                attackParam ? "present"
                            : $"params: [{string.Join(", ", controller.parameters.Select(p => $"{p.name}({p.type})"))}]");
            Check("1b Hit Trigger param", hitParam,
                hitParam ? "present"
                         : $"params: [{string.Join(", ", controller.parameters.Select(p => $"{p.name}({p.type})"))}]");

            // ── Check 2: State presence (Attack + Hit on Base Layer) ────────
            var rootSm = controller.layers[0].stateMachine;
            var attackState = rootSm.states.FirstOrDefault(s => s.state.name == "Attack").state;
            var hitState    = rootSm.states.FirstOrDefault(s => s.state.name == "Hit").state;
            Check("2a Attack state", attackState != null,
                attackState != null ? "found in Base Layer"
                                    : $"states: [{string.Join(", ", rootSm.states.Select(s => s.state.name))}]");
            Check("2b Hit state", hitState != null,
                hitState != null ? "found in Base Layer"
                                 : $"states: [{string.Join(", ", rootSm.states.Select(s => s.state.name))}]");

            // ── Check 3: State motion non-null + name match ─────────────────
            // M2 strafe lesson: verify clips actually resolve, not just slots exist.
            if (attackState != null)
            {
                var clip = attackState.motion as AnimationClip;
                bool ok = clip != null && clip.name == Attack01Clip;
                Check("3a Attack.motion resolves", ok,
                    clip == null ? "motion is null"
                                 : $"clip.name = '{clip.name}' (expected '{Attack01Clip}')");
            }
            if (hitState != null)
            {
                var clip = hitState.motion as AnimationClip;
                bool ok = clip != null && clip.name == GetHit01Clip;
                Check("3b Hit.motion resolves", ok,
                    clip == null ? "motion is null"
                                 : $"clip.name = '{clip.name}' (expected '{GetHit01Clip}')");
            }

            // ── Check 4: Override resolution for new slots ──────────────────
            // The override controller's m_Clips list pairs each "original" clip
            // (a clip referenced by the base controller) with an "override" clip
            // (the actual clip the player will play). We verify the override
            // target for the two new slots is non-null and has the expected name.
            var overridePairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideCtrl.GetOverrides(overridePairs);

            AnimationClip ResolveOverrideFor(string clipName)
            {
                // GUID-free lookup per CLAUDE.md M2 strafe lesson.
                var pair = overridePairs.FirstOrDefault(p => p.Key != null && p.Key.name == clipName);
                return pair.Value; // .Value is the override clip; null if no override
            }

            var attackOverride = ResolveOverrideFor(Attack01Clip);
            var hitOverride    = ResolveOverrideFor(GetHit01Clip);
            // For self-mapped slots (current pattern), Value == Key. If Value is
            // null but Key exists, Unity will fall back to Key — also acceptable.
            // The slot is "resolved" if the original is in the list.
            bool attackKeyPresent = overridePairs.Any(p => p.Key != null && p.Key.name == Attack01Clip);
            bool hitKeyPresent    = overridePairs.Any(p => p.Key != null && p.Key.name == GetHit01Clip);
            Check("4a Attack01 override slot", attackKeyPresent && (attackOverride == null || attackOverride.name == Attack01Clip),
                $"key-present={attackKeyPresent}, override={(attackOverride != null ? attackOverride.name : "null (fallback to original)")}");
            Check("4b GetHit01 override slot", hitKeyPresent && (hitOverride == null || hitOverride.name == GetHit01Clip),
                $"key-present={hitKeyPresent}, override={(hitOverride != null ? hitOverride.name : "null (fallback to original)")}");

            // ── Check 5: Transition counts ──────────────────────────────────
            // Expected after Step 2:
            //   - 6 existing state-to-state + 5 new state-to-state = 11 normal
            //   - 1 anyStateTransition (N5: AnyState → Hit)
            int totalStateTransitions = rootSm.states.Sum(s => s.state.transitions.Length);
            int anyStateTransitions   = rootSm.anyStateTransitions.Length;
            Check("5a State-to-state transitions == 11",
                totalStateTransitions == 11,
                $"got {totalStateTransitions} (per-state: " +
                string.Join(", ", rootSm.states.Select(s => $"{s.state.name}={s.state.transitions.Length}")) + ")");
            Check("5b AnyState transitions == 1",
                anyStateTransitions == 1,
                $"got {anyStateTransitions}");

            // ── Check 6: Exit-time spot check on Attack→Idle and Hit→Idle ──
            if (attackState != null)
            {
                var t = attackState.transitions.FirstOrDefault(x => x.destinationState != null && x.destinationState.name == "Idle");
                bool ok = t != null && t.hasExitTime && Mathf.Approximately(t.exitTime, 0.90f);
                Check("6a Attack→Idle hasExitTime=true & exitTime=0.90", ok,
                    t == null ? "transition not found"
                              : $"hasExitTime={t.hasExitTime}, exitTime={t.exitTime}");
            }
            if (hitState != null)
            {
                var t = hitState.transitions.FirstOrDefault(x => x.destinationState != null && x.destinationState.name == "Idle");
                bool ok = t != null && t.hasExitTime && Mathf.Approximately(t.exitTime, 0.85f);
                Check("6b Hit→Idle hasExitTime=true & exitTime=0.85", ok,
                    t == null ? "transition not found"
                              : $"hasExitTime={t.hasExitTime}, exitTime={t.exitTime}");
            }

            // ── Summary ─────────────────────────────────────────────────────
            string summary = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(summary + " — all M2-B Step 2 checks passed.");
            else           Debug.LogError(summary + " — see FAIL lines above; revert via git if needed.");
        }
    }
}
#endif
