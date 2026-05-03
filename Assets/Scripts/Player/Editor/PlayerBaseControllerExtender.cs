// PlayerBaseControllerExtender.cs — adds the M5 Death state to the
// existing PlayerBaseController.controller without rebuilding it.
//
// Idempotent: each addition (Death parameter, Death state,
// AnyState → Death transition) checks for existing presence and
// skips silently. Safe to re-run.
//
// Why an extender (not a from-scratch builder)? The controller
// was authored by direct YAML edits across M2-B (parameters,
// states, transitions). Rebuilding from scratch would lose all
// the existing M2-B / M2-C work. Adding the Death state in place
// preserves everything else.
//
// Menu: LevelGen ▶ Player ▶ Extend PlayerBaseController (M5 Death)

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerBaseControllerExtender
    {
        private const string ControllerPath  = "Assets/Animators/Player/PlayerBaseController.controller";

        // Die01 — FBX filename and sub-asset name match (no Shiled
        // typo on this clip; confirmed via .meta clipAnimations).
        private const string DeathClipPath   = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Die01_SwordAndShield.fbx";
        private const string DeathClipName   = "Die01_SwordAndShield";

        public const string ParamDeath  = "Death";
        public const string StateDeath  = "Death";

        [MenuItem("LevelGen/Player/Extend PlayerBaseController (M5 Death)")]
        public static void Extend()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[PlayerBaseControllerExtender] No controller at {ControllerPath}. Aborting.");
                return;
            }

            var deathClip = LoadClip(DeathClipPath, DeathClipName);
            if (deathClip == null)
            {
                Debug.LogError($"[PlayerBaseControllerExtender] Could not load Death clip '{DeathClipName}' " +
                               $"from {DeathClipPath}. Aborting.");
                return;
            }

            int added = 0;
            int skipped = 0;

            // ── 1. Death parameter (Trigger) ────────────────────────────
            bool hasParam = false;
            foreach (var p in controller.parameters)
            {
                if (p.name == ParamDeath) { hasParam = true; break; }
            }
            if (!hasParam)
            {
                controller.AddParameter(ParamDeath, AnimatorControllerParameterType.Trigger);
                added++;
                Debug.Log($"[PlayerBaseControllerExtender] Added parameter '{ParamDeath}' (Trigger).");
            }
            else
            {
                skipped++;
                Debug.Log($"[PlayerBaseControllerExtender] Parameter '{ParamDeath}' already present — skipped.");
            }

            // ── 2. Death state (terminal) ───────────────────────────────
            if (controller.layers.Length == 0)
            {
                Debug.LogError("[PlayerBaseControllerExtender] Controller has no layers. Aborting.");
                return;
            }
            var rootSm = controller.layers[0].stateMachine;

            AnimatorState deathState = null;
            foreach (var sc in rootSm.states)
            {
                if (sc.state.name == StateDeath) { deathState = sc.state; break; }
            }
            if (deathState == null)
            {
                deathState = rootSm.AddState(StateDeath);
                deathState.motion             = deathClip;
                deathState.writeDefaultValues = true;
                deathState.speed              = 1f;
                added++;
                Debug.Log($"[PlayerBaseControllerExtender] Added state '{StateDeath}' " +
                          $"(motion={deathClip.name}, terminal — no outgoing transitions).");
            }
            else
            {
                skipped++;
                Debug.Log($"[PlayerBaseControllerExtender] State '{StateDeath}' already present — skipped.");
            }

            // ── 3. AnyState → Death transition ──────────────────────────
            bool hasAnyToDeath = false;
            foreach (var t in rootSm.anyStateTransitions)
            {
                if (t.destinationState == deathState) { hasAnyToDeath = true; break; }
            }
            if (!hasAnyToDeath)
            {
                var anyToDeath = rootSm.AddAnyStateTransition(deathState);
                anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, ParamDeath);
                anyToDeath.hasExitTime         = false;
                anyToDeath.hasFixedDuration    = true;
                anyToDeath.duration            = 0.05f;
                anyToDeath.offset              = 0f;
                anyToDeath.canTransitionToSelf = false;
                added++;
                Debug.Log("[PlayerBaseControllerExtender] Added AnyState → Death " +
                          "(Death trigger, dur 0.05, canTransitionToSelf=false).");
            }
            else
            {
                skipped++;
                Debug.Log("[PlayerBaseControllerExtender] AnyState → Death transition already present — skipped.");
            }

            // Death has NO outgoing transitions — terminal. The asset is
            // already in that state on first creation; nothing to do here.
            // If a future hand-edit adds an outgoing transition, the
            // PlayerDeathValidator's terminal check will FAIL.

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerBaseControllerExtender] Done — {added} added, {skipped} skipped " +
                      $"(re-runnable; all-skipped output is the green idempotent state).");
        }

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a named AnimationClip sub-asset from a model FBX. Returns
        /// null if no matching clip is found. Lesson from M4-A: World
        /// Bundle clip sub-asset names sometimes differ from FBX filenames
        /// — Die01 happens to match, but verify via .meta if a future
        /// extension targets a different clip.
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
