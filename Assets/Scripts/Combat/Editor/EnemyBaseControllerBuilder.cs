// EnemyBaseControllerBuilder.cs — builds Animators/Enemy/EnemyBaseController.controller.
//
// Enemy graph:
//   States:   Idle (default) + Locomotion (1D blend tree) + Attack +
//             Hit + Death (terminal)
//   Params:   Hit (Trigger), Death (Trigger),
//             MoveSpeed (Float), Attack (Trigger)
//   Writers:  Hit       — EnemyHitReaction (M4-A)
//             Death     — EnemyDeath       (M4-B)
//             MoveSpeed — EnemyAI          (M10)
//             Attack    — EnemyAI          (M10)
//
// Hit reaction is gated by a Trigger parameter, fired from
// EnemyHitReaction after Targetable.OnHit. Death is gated by a
// separate Trigger parameter, fired from EnemyDeath after
// CharacterStatsRuntime.OnDied. Stagger window is C#-side (script-
// side cooldown), not Animator-side — graph stays simple. Death
// has NO outgoing transitions; the Animator parks on the last
// frame until the GameObject is destroyed by despawn.
//
// M10 added: Locomotion blend tree (Idle@0 → MoveFWD@1 on
// MoveSpeed Float), Attack state (Attack01_SwordAndShiled clip),
// transitions Idle↔Locomotion (MoveSpeed gate), AnyState→Attack
// (Trigger), Attack→Locomotion (exit time only). M4-A's
// AnyState→Hit transition now interrupts Locomotion + Attack
// states as well — canTransitionToSelf=false stays in place.
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

        // M10 — Locomotion blend tree forward-walk clip. FBX + sub-asset
        // names match (no typo on this one, verified via .meta).
        private const string MoveFwdClipPath = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveFWD_Battle_InPlace_SwordAndShield.fbx";
        private const string MoveFwdClipName = "MoveFWD_Battle_InPlace_SwordAndShield";

        // M10 — Attack clip. FBX filename has the publisher's "Shiled"
        // typo AND the sub-asset name is also typo'd (only the Idle clip
        // was renamed during the M3 pack swap). Verified via .meta:
        //   clipAnimations[0].name = Attack01_SwordAndShiled
        private const string AttackClipPath = "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Attack01_SwordAndShiled.fbx";
        private const string AttackClipName = "Attack01_SwordAndShiled";

        public const string ParamHit       = "Hit";
        public const string ParamDeath     = "Death";
        public const string ParamMoveSpeed = "MoveSpeed";
        public const string ParamAttack    = "Attack";

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

            var moveFwdClip = LoadClip(MoveFwdClipPath, MoveFwdClipName);
            if (moveFwdClip == null)
            {
                Debug.LogError($"[EnemyBaseControllerBuilder] Could not load MoveFWD clip '{MoveFwdClipName}' " +
                               $"from {MoveFwdClipPath}. Aborting.");
                return;
            }

            var attackClip = LoadClip(AttackClipPath, AttackClipName);
            if (attackClip == null)
            {
                Debug.LogError($"[EnemyBaseControllerBuilder] Could not load Attack clip '{AttackClipName}' " +
                               $"from {AttackClipPath}. Aborting.");
                return;
            }

            EnsureFolder("Assets", "Animators");
            EnsureFolder("Assets/Animators", "Enemy");

            // Idempotent rebuild — delete prior asset for a clean slate.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter(ParamHit,       AnimatorControllerParameterType.Trigger);
            controller.AddParameter(ParamDeath,     AnimatorControllerParameterType.Trigger);
            controller.AddParameter(ParamMoveSpeed, AnimatorControllerParameterType.Float);
            controller.AddParameter(ParamAttack,    AnimatorControllerParameterType.Trigger);

            var rootSm = controller.layers[0].stateMachine;

            var idle = rootSm.AddState("Idle");
            idle.motion             = idleClip;
            idle.writeDefaultValues = true;
            idle.speed              = 1f;

            // M10: Locomotion — 1D blend tree on MoveSpeed.
            //   Idle clip @ MoveSpeed=0 (so blend rests on Idle pose
            //                            when stopped — no pose pop)
            //   MoveFWD   @ MoveSpeed=1
            // CreateBlendTreeInController is a method on AnimatorController
            // (not AnimatorStateMachine). It creates an AnimatorState in
            // the base layer with a fresh BlendTree as its motion, and
            // saves the BlendTree as a sub-asset of the controller.
            BlendTree locoTree;
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out locoTree);
            locomotion.writeDefaultValues = true;
            locomotion.speed              = 1f;
            locoTree.blendType            = BlendTreeType.Simple1D;
            locoTree.blendParameter       = ParamMoveSpeed;
            locoTree.AddChild(idleClip,    0f);
            locoTree.AddChild(moveFwdClip, 1f);

            var attack = rootSm.AddState("Attack");
            attack.motion             = attackClip;
            attack.writeDefaultValues = true;
            attack.speed              = 1f;

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

            // ── M4-A AnyState → Hit ──────────────────────────────────────
            // Trigger-driven; canTransitionToSelf=false so mid-stagger
            // trigger fires don't restart the clip from frame 0 (visual
            // stutter). Stagger gating still happens script-side as
            // belt-and-suspenders.
            //
            // M10 note: with Locomotion + Attack states added, this
            // AnyState transition now interrupts those too. Hit always
            // wins; EnemyAI suspends FSM tick on IsInHitState, then
            // resumes when the Hit→Idle transition completes.
            var anyToHit = rootSm.AddAnyStateTransition(hit);
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, ParamHit);
            anyToHit.hasExitTime         = false;
            anyToHit.hasFixedDuration    = true;
            anyToHit.duration            = 0.05f;
            anyToHit.offset              = 0f;
            anyToHit.canTransitionToSelf = false;

            // ── M4-B AnyState → Death ────────────────────────────────────
            var anyToDeath = rootSm.AddAnyStateTransition(death);
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, ParamDeath);
            anyToDeath.hasExitTime         = false;
            anyToDeath.hasFixedDuration    = true;
            anyToDeath.duration            = 0.05f;
            anyToDeath.offset              = 0f;
            anyToDeath.canTransitionToSelf = false;

            // ── M4-A Hit → Idle ──────────────────────────────────────────
            // Exit-time driven (clip nearly finishes before returning
            // to Idle). EnemyAI's MoveSpeed gating (next frame) takes
            // the rig back into Locomotion blend if the agent is moving.
            var hitToIdle = hit.AddTransition(idle);
            hitToIdle.hasExitTime      = true;
            hitToIdle.exitTime         = 0.95f;
            hitToIdle.hasFixedDuration = true;
            hitToIdle.duration         = 0.10f;
            hitToIdle.offset           = 0f;

            // ── M10 Idle → Locomotion (MoveSpeed > 0.1) ──────────────────
            var idleToLoco = idle.AddTransition(locomotion);
            idleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, ParamMoveSpeed);
            idleToLoco.hasExitTime      = false;
            idleToLoco.hasFixedDuration = true;
            idleToLoco.duration         = 0.15f;
            idleToLoco.offset           = 0f;

            // ── M10 Locomotion → Idle (MoveSpeed < 0.1) ──────────────────
            var locoToIdle = locomotion.AddTransition(idle);
            locoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, ParamMoveSpeed);
            locoToIdle.hasExitTime      = false;
            locoToIdle.hasFixedDuration = true;
            locoToIdle.duration         = 0.15f;
            locoToIdle.offset           = 0f;

            // ── M10 AnyState → Attack ───────────────────────────────────
            // Trigger-driven; canTransitionToSelf=false so a leaked
            // second Attack trigger mid-swing doesn't restart the clip.
            // EnemyAI's coroutine guards the cooldown gate as well.
            var anyToAttack = rootSm.AddAnyStateTransition(attack);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, ParamAttack);
            anyToAttack.hasExitTime         = false;
            anyToAttack.hasFixedDuration    = true;
            anyToAttack.duration            = 0.10f;
            anyToAttack.offset              = 0f;
            anyToAttack.canTransitionToSelf = false;

            // ── M10 Attack → Locomotion (exit-time only) ────────────────
            // Exit at 0.92 — matches EnemyAI.AttackCoroutine's exitNT.
            // Locomotion's blend tree handles MoveSpeed=0 gracefully
            // (rests on Idle pose), so going Attack → Locomotion is
            // safe whether the agent will be Idle or Chasing next.
            var attackToLoco = attack.AddTransition(locomotion);
            attackToLoco.hasExitTime         = true;
            attackToLoco.exitTime            = 0.92f;
            attackToLoco.hasFixedDuration    = true;
            attackToLoco.duration            = 0.10f;
            attackToLoco.offset              = 0f;
            attackToLoco.canTransitionToSelf = false;

            // Death has NO outgoing transitions — terminal.

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[EnemyBaseControllerBuilder] Built {ControllerPath}.\n" +
                $"  Parameters: Hit (Trigger), Death (Trigger), MoveSpeed (Float), Attack (Trigger)\n" +
                $"  States: Idle (default, motion={idleClip.name}), " +
                $"Locomotion (1D blend on MoveSpeed: Idle@0 / MoveFWD@1), " +
                $"Attack (motion={attackClip.name}), " +
                $"Hit (motion={hitClip.name}), Death (terminal, motion={deathClip.name})\n" +
                $"  Transitions: AnyState → Hit (dur 0.05, canTransitionToSelf=false), " +
                $"AnyState → Death (dur 0.05, canTransitionToSelf=false), " +
                $"Hit → Idle (no cond, exitTime 0.95, dur 0.10), " +
                $"Idle → Locomotion (MoveSpeed>0.1, dur 0.15), " +
                $"Locomotion → Idle (MoveSpeed<0.1, dur 0.15), " +
                $"AnyState → Attack (Attack trigger, dur 0.10, canTransitionToSelf=false), " +
                $"Attack → Locomotion (no cond, exitTime 0.92, dur 0.10). " +
                $"Death has no outgoing transitions."
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
