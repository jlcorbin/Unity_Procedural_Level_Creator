// EnemyBaseControllerBuilder.cs — builds Animators/Enemy/EnemyBaseController.controller.
//
// Minimal enemy graph: Idle (default) + Hit. The Hit reaction is
// gated by a single Trigger parameter, fired from EnemyHitReaction
// after Targetable.OnHit. Stagger window is C#-side (script-side
// cooldown), not Animator-side — graph stays simple.
//
// Idempotent: re-running deletes the existing controller and rebuilds
// from scratch. No interactive prompt.
//
// Menu: LevelGen ▶ Combat ▶ Build EnemyBaseController

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Combat.EditorTools
{
    public static class EnemyBaseControllerBuilder
    {
        private const string ControllerFolder = "Assets/Animators/Enemy";
        private const string ControllerPath   = "Assets/Animators/Enemy/EnemyBaseController.controller";

        // Same Idle clip the Dummy is currently displaying via PlayerBaseController —
        // preserves visual continuity across the controller swap.
        // FBX filename retains the publisher's "Shiled" typo, but the
        // AnimationClip sub-asset name was corrected to "Shield" during
        // the M3 pack swap (Duo → World Bundle). Path uses Shiled,
        // sub-asset uses Shield.
        private const string IdleClipPath = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx";
        private const string IdleClipName = "Idle_Battle_SwordAndShield";

        private const string HitClipPath  = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/GetHit01_SwordAndShield.fbx";
        private const string HitClipName  = "GetHit01_SwordAndShield";

        public const string ParamHit = "Hit";

        [MenuItem("LevelGen/Combat/Build EnemyBaseController")]
        public static void Build()
        {
            var idleClip = LoadClip(IdleClipPath, IdleClipName);
            if (idleClip == null)
            {
                Debug.LogError($"[EnemyBaseControllerBuilder] Could not load Idle clip '{IdleClipName}' " +
                               $"from {IdleClipPath}. Aborting.");
                return;
            }

            var hitClip = LoadClip(HitClipPath, HitClipName);
            if (hitClip == null)
            {
                Debug.LogError($"[EnemyBaseControllerBuilder] Could not load Hit clip '{HitClipName}' " +
                               $"from {HitClipPath}. Aborting.");
                return;
            }

            EnsureFolder("Assets", "Animators");
            EnsureFolder("Assets/Animators", "Enemy");

            // Idempotent rebuild — delete prior asset for a clean slate.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter(ParamHit, AnimatorControllerParameterType.Trigger);

            var rootSm = controller.layers[0].stateMachine;

            var idle = rootSm.AddState("Idle");
            idle.motion             = idleClip;
            idle.writeDefaultValues = true;
            idle.speed              = 1f;

            var hit = rootSm.AddState("Hit");
            hit.motion              = hitClip;
            hit.writeDefaultValues  = true;
            hit.speed               = 1f;

            rootSm.defaultState = idle;

            // AnyState → Hit. Trigger-driven; canTransitionToSelf=false so
            // mid-stagger trigger fires don't restart the clip from frame 0
            // (visual stutter). Stagger gating still happens script-side as
            // belt-and-suspenders.
            var anyToHit = rootSm.AddAnyStateTransition(hit);
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, ParamHit);
            anyToHit.hasExitTime         = false;
            anyToHit.hasFixedDuration    = true;
            anyToHit.duration            = 0.05f;
            anyToHit.offset              = 0f;
            anyToHit.canTransitionToSelf = false;

            // Hit → Idle. Exit-time driven (clip nearly finishes before
            // returning to Idle).
            var hitToIdle = hit.AddTransition(idle);
            hitToIdle.hasExitTime      = true;
            hitToIdle.exitTime         = 0.95f;
            hitToIdle.hasFixedDuration = true;
            hitToIdle.duration         = 0.10f;
            hitToIdle.offset           = 0f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[EnemyBaseControllerBuilder] Built {ControllerPath}.\n" +
                $"  Parameters: Hit (Trigger)\n" +
                $"  States: Idle (default, motion={idleClip.name}), Hit (motion={hitClip.name})\n" +
                $"  Transitions: AnyState → Hit (Hit trigger, dur 0.05, canTransitionToSelf=false), " +
                $"Hit → Idle (no cond, exitTime 0.95, dur 0.10)"
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a named AnimationClip sub-asset from a model FBX. Returns
        /// null if no matching clip is found.
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

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[EnemyBaseControllerBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[EnemyBaseControllerBuilder] Created folder: {path}");
        }
    }
}
#endif
