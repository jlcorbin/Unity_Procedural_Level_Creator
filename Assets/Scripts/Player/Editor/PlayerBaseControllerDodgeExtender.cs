// PlayerBaseControllerDodgeExtender.cs — adds the M12 Dodge sub-
// state-machine + Roll{FWD,BWD,LFT,RGT} states + AnyState transitions
// to PlayerBaseController in place.
//
// Idempotent: each addition (parameters, sub-state-machine, states,
// transitions, exit transitions) is presence-checked and skipped if
// already wired. Safe to re-run.
//
// Why a sub-state-machine? The milestone spec ("Add four new states
// under a sub-state-machine named `Dodge`") groups the four Roll states
// for readability in the Animator graph. AnyState→{state-inside-sub-SM}
// still works in Unity's API and YAML; the validator walks both the
// root SM's states and sub-SMs' states when checking presence.
//
// Roll clip choice: _InPlace_ variants (root motion locked at the FBX
// level). PlayerDodge.RollCoroutine drives horizontal displacement via
// CharacterController.Move; the clip is visual only. Same convention
// as the rest of the player rig (M2-B / M2-C used InPlace clips for
// the same reason).
//
// Menu: LevelGen ▶ Player ▶ Extend PlayerBaseController (M12 Dodge)

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerBaseControllerDodgeExtender
    {
        private const string ControllerPath = "Assets/Animators/Player/PlayerBaseController.controller";

        // Sword&Shield InPlace Roll clips — FBX filename and sub-asset
        // name match for these (no Shiled-typo lesson from M3-02A applies
        // here; verified via .meta clipAnimations entries on each FBX).
        private const string ClipDirBase = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/";
        private const string FbxFWD = ClipDirBase + "RollFWD_Battle_InPlace_SwordAndShield.fbx";
        private const string FbxBWD = ClipDirBase + "RollBWD_Battle_InPlace_SwordAndShield.fbx";
        private const string FbxLFT = ClipDirBase + "RollLFT_Battle_InPlace_SwordAndShield.fbx";
        private const string FbxRGT = ClipDirBase + "RollRGT_Battle_InPlace_SwordAndShield.fbx";
        private const string ClipFWD = "RollFWD_Battle_InPlace_SwordAndShield";
        private const string ClipBWD = "RollBWD_Battle_InPlace_SwordAndShield";
        private const string ClipLFT = "RollLFT_Battle_InPlace_SwordAndShield";
        private const string ClipRGT = "RollRGT_Battle_InPlace_SwordAndShield";

        public const string ParamDodgeTrigger   = "DodgeTrigger";
        public const string ParamDodgeDirection = "DodgeDirection";

        public const string SubStateMachineName = "Dodge";
        public const string StateRollFWD = "RollFWD";
        public const string StateRollBWD = "RollBWD";
        public const string StateRollLFT = "RollLFT";
        public const string StateRollRGT = "RollRGT";

        // DodgeDirection int values — match PlayerDodge's constants.
        private const int DirFWD = 0;
        private const int DirBWD = 1;
        private const int DirLFT = 2;
        private const int DirRGT = 3;

        [MenuItem("LevelGen/Player/Extend PlayerBaseController (M12 Dodge)")]
        public static void Extend()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerBaseControllerDodgeExtender] No controller at {ControllerPath}. Aborting.");
                return;
            }

            var clipFWD = LoadClip(FbxFWD, ClipFWD);
            var clipBWD = LoadClip(FbxBWD, ClipBWD);
            var clipLFT = LoadClip(FbxLFT, ClipLFT);
            var clipRGT = LoadClip(FbxRGT, ClipRGT);
            if (clipFWD == null || clipBWD == null || clipLFT == null || clipRGT == null)
            {
                Debug.LogError($"[PlayerBaseControllerDodgeExtender] Roll clip(s) missing: " +
                               $"FWD={(clipFWD!=null)}, BWD={(clipBWD!=null)}, " +
                               $"LFT={(clipLFT!=null)}, RGT={(clipRGT!=null)}. Aborting.");
                return;
            }

            if (controller.layers.Length == 0)
            {
                Debug.LogError("[PlayerBaseControllerDodgeExtender] Controller has no layers. Aborting.");
                return;
            }

            int added = 0;
            int skipped = 0;

            // ── 1. Parameters ───────────────────────────────────────────
            AddTriggerIfMissing(controller, ParamDodgeTrigger, ref added, ref skipped);
            AddIntIfMissing(controller, ParamDodgeDirection, ref added, ref skipped);

            // ── 2. Sub-state-machine + states ───────────────────────────
            var rootSm = controller.layers[0].stateMachine;
            var dodgeSm = FindChildStateMachine(rootSm, SubStateMachineName);
            if (dodgeSm == null)
            {
                dodgeSm = rootSm.AddStateMachine(SubStateMachineName, new Vector3(400, 50, 0));
                added++;
                Debug.Log($"[PlayerBaseControllerDodgeExtender] Added sub-state-machine '{SubStateMachineName}'.");
            }
            else
            {
                skipped++;
                Debug.Log($"[PlayerBaseControllerDodgeExtender] Sub-state-machine '{SubStateMachineName}' already present — skipped.");
            }

            var rollFWD = EnsureState(dodgeSm, StateRollFWD, clipFWD, ref added, ref skipped);
            var rollBWD = EnsureState(dodgeSm, StateRollBWD, clipBWD, ref added, ref skipped);
            var rollLFT = EnsureState(dodgeSm, StateRollLFT, clipLFT, ref added, ref skipped);
            var rollRGT = EnsureState(dodgeSm, StateRollRGT, clipRGT, ref added, ref skipped);

            // ── 3. AnyState → Roll{X} transitions ───────────────────────
            EnsureAnyStateRollTransition(rootSm, rollFWD, DirFWD, ref added, ref skipped);
            EnsureAnyStateRollTransition(rootSm, rollBWD, DirBWD, ref added, ref skipped);
            EnsureAnyStateRollTransition(rootSm, rollLFT, DirLFT, ref added, ref skipped);
            EnsureAnyStateRollTransition(rootSm, rollRGT, DirRGT, ref added, ref skipped);

            // ── 4. Roll{X} → Locomotion exit transitions ────────────────
            // Find Locomotion in the root SM. Required — controller was
            // built in M2-B with a Locomotion state. Abort cleanly if
            // missing (something is structurally wrong with the controller).
            AnimatorState locomotion = FindRootState(rootSm, "Locomotion");
            if (locomotion == null)
            {
                Debug.LogError("[PlayerBaseControllerDodgeExtender] Root SM has no 'Locomotion' state. " +
                               "Roll→Locomotion exit transitions cannot be wired. Aborting after partial wiring.");
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            EnsureRollExitTransition(rollFWD, locomotion, ref added, ref skipped);
            EnsureRollExitTransition(rollBWD, locomotion, ref added, ref skipped);
            EnsureRollExitTransition(rollLFT, locomotion, ref added, ref skipped);
            EnsureRollExitTransition(rollRGT, locomotion, ref added, ref skipped);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerBaseControllerDodgeExtender] Done — {added} added, {skipped} skipped " +
                      $"(re-runnable; all-skipped output is the green idempotent state).");
        }

        // ── Parameter helpers ────────────────────────────────────────────

        private static void AddTriggerIfMissing(AnimatorController controller, string name,
                                                ref int added, ref int skipped)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) { skipped++; return; }
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            added++;
            Debug.Log($"[PlayerBaseControllerDodgeExtender] Added Trigger parameter '{name}'.");
        }

        private static void AddIntIfMissing(AnimatorController controller, string name,
                                            ref int added, ref int skipped)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) { skipped++; return; }
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Int);
            added++;
            Debug.Log($"[PlayerBaseControllerDodgeExtender] Added Int parameter '{name}'.");
        }

        // ── State / SM helpers ───────────────────────────────────────────

        private static AnimatorStateMachine FindChildStateMachine(AnimatorStateMachine parent, string name)
        {
            foreach (var c in parent.stateMachines)
            {
                if (c.stateMachine != null && c.stateMachine.name == name) return c.stateMachine;
            }
            return null;
        }

        private static AnimatorState FindRootState(AnimatorStateMachine sm, string name)
        {
            foreach (var sc in sm.states)
            {
                if (sc.state != null && sc.state.name == name) return sc.state;
            }
            return null;
        }

        private static AnimatorState FindStateInSubMachine(AnimatorStateMachine sm, string name)
        {
            foreach (var sc in sm.states)
            {
                if (sc.state != null && sc.state.name == name) return sc.state;
            }
            return null;
        }

        private static AnimatorState EnsureState(AnimatorStateMachine dodgeSm, string name, AnimationClip clip,
                                                 ref int added, ref int skipped)
        {
            var existing = FindStateInSubMachine(dodgeSm, name);
            if (existing != null)
            {
                skipped++;
                return existing;
            }
            var s = dodgeSm.AddState(name);
            s.motion             = clip;
            s.writeDefaultValues = true;
            s.speed              = 1f;
            added++;
            Debug.Log($"[PlayerBaseControllerDodgeExtender] Added state '{name}' (motion={clip.name}).");
            return s;
        }

        // ── Transition helpers ───────────────────────────────────────────

        private static void EnsureAnyStateRollTransition(AnimatorStateMachine rootSm, AnimatorState dest,
                                                         int directionValue, ref int added, ref int skipped)
        {
            // Idempotency: an AnyState transition to this destination
            // with a DodgeDirection==directionValue condition already
            // present is treated as "already wired".
            foreach (var t in rootSm.anyStateTransitions)
            {
                if (t.destinationState != dest) continue;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == ParamDodgeDirection
                        && c.mode      == AnimatorConditionMode.Equals
                        && Mathf.Approximately(c.threshold, directionValue))
                    { skipped++; return; }
                }
            }

            var transition = rootSm.AddAnyStateTransition(dest);
            transition.AddCondition(AnimatorConditionMode.If,    0f, ParamDodgeTrigger);
            transition.AddCondition(AnimatorConditionMode.Equals, directionValue, ParamDodgeDirection);
            transition.hasExitTime         = false;
            transition.hasFixedDuration    = true;
            transition.duration            = 0.05f;
            transition.offset              = 0f;
            transition.canTransitionToSelf = false;
            added++;
            Debug.Log($"[PlayerBaseControllerDodgeExtender] Added AnyState → {dest.name} " +
                      $"(DodgeTrigger + DodgeDirection == {directionValue}, canTransitionToSelf=false).");
        }

        private static void EnsureRollExitTransition(AnimatorState rollState, AnimatorState locomotion,
                                                     ref int added, ref int skipped)
        {
            foreach (var t in rollState.transitions)
            {
                if (t.destinationState == locomotion) { skipped++; return; }
            }

            var exit = rollState.AddTransition(locomotion);
            exit.hasExitTime         = true;
            exit.exitTime            = 1.0f;
            exit.hasFixedDuration    = true;
            exit.duration            = 0.10f;
            exit.offset              = 0f;
            // No conditions — auto-fire at exit time.
            added++;
            Debug.Log($"[PlayerBaseControllerDodgeExtender] Added {rollState.name} → Locomotion " +
                      $"(no conditions, exitTime=1.0, dur 0.10).");
        }

        // ── Asset loader ─────────────────────────────────────────────────

        /// <summary>
        /// Loads a named AnimationClip sub-asset from a model FBX.
        /// Returns null if no matching clip is found. World Bundle Roll
        /// clip filenames match their sub-asset names — verified during
        /// the M3 pack swap (only Idle was renamed).
        /// </summary>
        private static AnimationClip LoadClip(string fbxPath, string clipName)
        {
            var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
            if (subs == null) return null;
            foreach (var s in subs)
            {
                if (s is AnimationClip c && c.name == clipName) return c;
            }
            return null;
        }
    }
}
#endif
