// EnemyBaseControllerBuilder.cs — builds Animators/Enemy/EnemyBaseController.controller.
//
// Minimal enemy graph: Idle (default) + Hit + Death (terminal).
// Hit reaction is gated by a Trigger parameter, fired from
// EnemyHitReaction after Targetable.OnHit. Death is gated by a
// separate Trigger parameter, fired from EnemyDeath after
// CharacterStatsRuntime.OnDied. Stagger window is C#-side (script-
// side cooldown), not Animator-side — graph stays simple. Death
// has NO outgoing transitions; the Animator parks on the last
// frame until the GameObject is destroyed by despawn.
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

        // Die01 — FBX filename and sub-asset name match (no Shiled typo
        // on this one; verified via the .meta clipAnimations entry).
        private const string DeathClipPath = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Die01_SwordAndShield.fbx";
        private const string DeathClipName = "Die01_SwordAndShield";

        public const string ParamHit   = "Hit";
        public const string ParamDeath = "Death";

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

            var deathClip = LoadClip(DeathClipPath, DeathClipName);
            if (deathClip == null)
            {
                Debug.LogError($"[EnemyBaseControllerBuilder] Could not load Death clip '{DeathClipName}' " +
                               $"from {DeathClipPath}. Aborting.");
                return;
            }

            EnsureFolder("Assets", "Animators");
            EnsureFolder("Assets/Animators", "Enemy");

            // Idempotent rebuild — delete prior asset for a clean slate.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter(ParamHit,   AnimatorControllerParameterType.Trigger);
            controller.AddParameter(ParamDeath, AnimatorControllerParameterType.Trigger);

            var rootSm = controller.layers[0].stateMachine;

            var idle = rootSm.AddState("Idle");
            idle.motion             = idleClip;
            idle.writeDefaultValues = true;
            idle.speed              = 1f;

            var hit = rootSm.AddState("Hit");
            hit.motion              = hitClip;
            hit.writeDefaultValues  = true;
            hit.speed               = 1f;

            // Death — terminal state. Animator parks on the last frame
            // until the GameObject is destroyed by despawn (EnemyDeath).
            // No outgoing transition.
            var death = rootSm.AddState("Death");
            death.motion             = deathClip;
            death.writeDefaultValues = true;
            death.speed              = 1f;

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

            // AnyState → Death. Trigger-driven; canTransitionToSelf=false
            // so a leaked second Death trigger can't restart the clip
            // (EnemyDeath also guards with _hasFired, but defense in depth).
            var anyToDeath = rootSm.AddAnyStateTransition(death);
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, ParamDeath);
            anyToDeath.hasExitTime         = false;
            anyToDeath.hasFixedDuration    = true;
            anyToDeath.duration            = 0.05f;
            anyToDeath.offset              = 0f;
            anyToDeath.canTransitionToSelf = false;

            // Hit → Idle. Exit-time driven (clip nearly finishes before
            // returning to Idle).
            var hitToIdle = hit.AddTransition(idle);
            hitToIdle.hasExitTime      = true;
            hitToIdle.exitTime         = 0.95f;
            hitToIdle.hasFixedDuration = true;
            hitToIdle.duration         = 0.10f;
            hitToIdle.offset           = 0f;

            // Death has NO outgoing transitions — terminal.

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[EnemyBaseControllerBuilder] Built {ControllerPath}.\n" +
                $"  Parameters: Hit (Trigger), Death (Trigger)\n" +
                $"  States: Idle (default, motion={idleClip.name}), Hit (motion={hitClip.name}), " +
                $"Death (terminal, motion={deathClip.name})\n" +
                $"  Transitions: AnyState → Hit (Hit trigger, dur 0.05, canTransitionToSelf=false), " +
                $"AnyState → Death (Death trigger, dur 0.05, canTransitionToSelf=false), " +
                $"Hit → Idle (no cond, exitTime 0.95, dur 0.10), Death has no outgoing transitions."
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
