// PlayerBaseControllerStanceBuilder.cs — M22 Steps 12–14, automated.
//
// Stamps the per-stance combat + locomotion + dodge graph into
// PlayerBaseController so it doesn't have to be hand-wired:
//   12 — per-stance melee Attack chains (stances 0–5): 3 tagged states
//        (Attack1/Attack2/Attack3) with ComboNext + exit-time-fallback
//        transitions, entered from Any State on Attack + WeaponType==index.
//        Ranged stances 6–7 get a single (untagged) shot state.
//   13 — Locomotion motion → a 1D blend tree on WeaponType whose 8 children
//        are per-stance 2D Freeform-Directional (MoveX/MoveZ) walk trees.
//   14 — each RollFWD/BWD/LFT/RGT motion → a 1D blend tree on WeaponType
//        selecting that stance's roll clip.
//
// Idempotent: it removes the attack states/transitions it owns (and the old
// hand-wired "Attack*" states) and rebuilds, and it re-swaps the loco/dodge
// motions each run. Clips are found by tolerant name search, so the pack's
// "Shiled"/"_THS"/"Battle vs Normal" quirks don't need hardcoding. Any clip it
// can't find is reported and left empty for you to drop in by hand.
//
// The WeaponType int is the STANCE INDEX (0–7): NoWeapon, SingleSword,
// TwoHandsSword, SwordAndShield, DoubleSword, Spear, MagicWand, BowAndArrow.
//
// Menu: LevelGen ▶ Player ▶ Build Stance Animator (M22 12-14)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerBaseControllerStanceBuilder
    {
        private const string ControllerPath = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string AnimRoot =
            "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation";

        // Index = stance index. Folder names in the pack's Animation/ directory.
        private static readonly string[] StanceFolders =
        {
            "NoWeapon", "SingleSword", "TwoHandSword", "SwordAndShield",
            "DoubleSword", "Spear", "MagicWand", "BowAndArrow"
        };
        private const int RangedFirst = 6; // stances 6,7 are ranged (single shot, no combo)

        private static readonly List<string> _missing = new List<string>();
        private static int _statesMade;

        [MenuItem("LevelGen/Player/Build Stance Animator (M22 12-14)")]
        public static void Build()
        {
            _missing.Clear();
            _statesMade = 0;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[StanceBuilder] Controller not found at {ControllerPath}.");
                return;
            }
            if (!Directory.Exists(AnimRoot))
            {
                Debug.LogError($"[StanceBuilder] Animation root not found at {AnimRoot}.");
                return;
            }

            EnsureParams(controller);
            var root = controller.layers[0].stateMachine;

            var loco = FindState(root, "Locomotion") ?? FindState(root, "Idle");
            if (loco == null)
            {
                Debug.LogError("[StanceBuilder] Could not find a 'Locomotion' or 'Idle' state to use as the " +
                               "attack fallback target. Aborting — no changes made.");
                return;
            }

            BuildAttacks(controller, root, loco);   // step 12
            BuildLocomotion(controller, loco);       // step 13
            BuildDodge(controller, root);            // step 14

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath);

            string miss = _missing.Count == 0
                ? "all clips found"
                : $"{_missing.Count} clip(s) NOT found — fill by hand:\n  - " + string.Join("\n  - ", _missing);
            Debug.Log($"[StanceBuilder] Done. {_statesMade} attack state(s) (re)built; Locomotion + 4 Roll " +
                      $"states re-motioned. {miss}\nWeaponType = stance index 0–7. Tag/transition wiring is automatic.");
        }

        // ── Hitbox animation events on the per-stance melee attack clips ────
        // The new per-stance clips don't carry the OnHitboxOpen/OnHitboxClose
        // events that enable the weapon collider mid-swing, so melee deals no
        // damage until these are stamped. Events fire on the Animator's GO and
        // are relayed to PlayerCombat by the AnimationEventForwarder (M20b), so
        // the forwarder must be present on the MaleCharacterPBR child.
        [MenuItem("LevelGen/Player/Add Hitbox Events to Stance Attack Clips")]
        public static void AddHitboxEvents()
        {
            _missing.Clear();
            int n = 0;
            var seen = new HashSet<string>();
            for (int stance = 0; stance < RangedFirst; stance++)   // melee stances 0–5
            {
                for (int hit = 1; hit <= 3; hit++)
                {
                    var clip = Attack(stance, hit);
                    if (clip == null) continue;
                    string key = AssetDatabase.GetAssetPath(clip) + "::" + clip.name;
                    if (!seen.Add(key)) continue;   // don't reimport the same clip twice
                    if (StampEvents(clip)) n++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[StanceBuilder] Stamped OnHitboxOpen/OnHitboxClose (0.35/0.65 of length) on " +
                      $"{n} melee attack clip(s). Ensure MaleCharacterPBR has an AnimationEventForwarder " +
                      "and the equipped/dev weapon's collider is wired to PlayerCombat." +
                      (_missing.Count > 0 ? $" {_missing.Count} clip(s) not found." : ""));
        }

        private static bool StampEvents(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[StanceBuilder] No ModelImporter: {path}"); return false; }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) { Debug.LogWarning($"[StanceBuilder] No clips: {path}"); return false; }

            int idx = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                string nm = string.IsNullOrEmpty(clips[i].name) ? clips[i].takeName : clips[i].name;
                if (nm == clip.name) { idx = i; break; }
            }

            float len = clip.length > 0f ? clip.length : 1f;
            var list = new List<AnimationEvent>();
            if (clips[idx].events != null)
                foreach (var ev in clips[idx].events)
                    if (ev.functionName != "OnHitboxOpen" && ev.functionName != "OnHitboxClose")
                        list.Add(ev);
            list.Add(new AnimationEvent { functionName = "OnHitboxOpen",  time = len * 0.35f });
            list.Add(new AnimationEvent { functionName = "OnHitboxClose", time = len * 0.65f });
            clips[idx].events = list.ToArray();

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return true;
        }

        // ── Step 12: melee chains (0–5) + ranged singles (6,7) ──────────────
        private static void BuildAttacks(AnimatorController ctrl, AnimatorStateMachine root, AnimatorState loco)
        {
            // Remove our own + the old hand-wired attack entries and states so the
            // rebuild is clean and idempotent.
            foreach (var t in new List<AnimatorStateTransition>(root.anyStateTransitions))
                if (t.destinationState != null &&
                    (t.destinationState.name.StartsWith("Attack") || t.destinationState.name.StartsWith("M22_Atk")))
                    root.RemoveAnyStateTransition(t);

            foreach (var cs in new List<ChildAnimatorState>(root.states))
                if (cs.state.name.StartsWith("Attack") || cs.state.name.StartsWith("M22_Atk"))
                    root.RemoveState(cs.state);

            for (int i = 0; i < StanceFolders.Length; i++)
            {
                string set = StanceFolders[i];
                bool ranged = i >= RangedFirst;

                var a1 = MakeState(root, $"M22_Atk1_{set}", Attack(i, 1), new Vector3(i * 260, 0, 0));
                if (a1 == null) continue;

                if (ranged)
                {
                    // Single shot state (no combo). Tagged "RangedShot" so
                    // RangedCombat can spawn the arrow at the release frame.
                    a1.tag = "RangedShot";
                    AnyEntry(root, a1, i);
                    Fallback(a1, loco);
                    continue;
                }

                a1.tag = "Attack1";
                var a2 = MakeState(root, $"M22_Atk2_{set}", Attack(i, 2), new Vector3(i * 260, 90, 0));
                var a3 = MakeState(root, $"M22_Atk3_{set}", Attack(i, 3), new Vector3(i * 260, 180, 0));
                if (a2 != null) a2.tag = "Attack2";
                if (a3 != null) a3.tag = "Attack3";

                AnyEntry(root, a1, i);

                // Combo transitions FIRST (ComboNext, no exit time), then the
                // exit-time fallback — order matters, combo must win.
                if (a2 != null) Combo(a1, a2);
                Fallback(a1, loco);
                if (a2 != null)
                {
                    if (a3 != null) Combo(a2, a3);
                    Fallback(a2, loco);
                }
                if (a3 != null) Fallback(a3, loco);
            }
        }

        private static AnimationClip Attack(int stance, int n) =>
            FindClip(StanceFolders[stance],
                required: new[] { $"Attack0{n}" },
                prefer: new[] { "Battle", StanceFolders[stance] },
                excludeName: new[] { "Combo", "Start", "Spining", "Spinning" });

        private static void AnyEntry(AnimatorStateMachine root, AnimatorState dst, int weaponType)
        {
            var t = root.AddAnyStateTransition(dst);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.Equals, weaponType, "WeaponType");
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.10f;
            t.canTransitionToSelf = false;
        }

        private static void Combo(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0, "ComboNext");
            t.hasExitTime = false;        // never combine exit time + trigger (Unity 6.4 gotcha)
            t.hasFixedDuration = true;
            t.duration = 0.10f;
        }

        private static void Fallback(AnimatorState from, AnimatorState loco)
        {
            var t = from.AddTransition(loco);
            t.hasExitTime = true;
            t.exitTime = 0.90f;
            t.hasFixedDuration = true;
            t.duration = 0.10f;
        }

        // ── Step 13: locomotion nested blend tree ───────────────────────────
        private static void BuildLocomotion(AnimatorController ctrl, AnimatorState loco)
        {
            var parent = NewTree(ctrl, "Loco_ByStance");
            parent.blendType = BlendTreeType.Simple1D;
            parent.blendParameter = "StanceBlend";   // float mirror of WeaponType (1D trees need a float)

            for (int i = 0; i < StanceFolders.Length; i++)
            {
                string set = StanceFolders[i];
                var child = NewTree(ctrl, $"Loco_{set}");
                child.blendType = BlendTreeType.FreeformDirectional2D;
                child.blendParameter = "MoveX";
                child.blendParameterY = "MoveZ";

                Add2D(child, Move(set, "Idle"),    0f,  0f);
                Add2D(child, Move(set, "MoveFWD"), 0f,  1f);
                Add2D(child, Move(set, "MoveBWD"), 0f, -1f);
                Add2D(child, Move(set, "MoveRGT"), 1f,  0f);
                Add2D(child, Move(set, "MoveLFT"),-1f,  0f);

                parent.AddChild(child, (float)i);
            }
            loco.motion = parent;
        }

        private static AnimationClip Move(string set, string action)
        {
            if (action == "Idle")
                return FindClip(set, new[] { "Idle" }, new[] { "Battle", set },
                                new[] { "Combo" });
            return FindClip(set, new[] { action, "InPlace" }, new[] { "Battle", set }, null);
        }

        private static void Add2D(BlendTree tree, AnimationClip clip, float x, float y)
        {
            if (clip == null) return;   // miss already reported by FindClip
            tree.AddChild(clip, new Vector2(x, y));
        }

        // ── Step 14: per-stance dodge blend trees ───────────────────────────
        private static void BuildDodge(AnimatorController ctrl, AnimatorStateMachine root)
        {
            string[] dirs = { "RollFWD", "RollBWD", "RollLFT", "RollRGT" };
            foreach (var dir in dirs)
            {
                var state = FindState(root, dir);
                if (state == null) { _missing.Add($"(state) {dir}"); continue; }

                var tree = NewTree(ctrl, $"{dir}_ByStance");
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = "StanceBlend";   // float mirror of WeaponType (1D trees need a float)

                for (int i = 0; i < StanceFolders.Length; i++)
                {
                    var clip = FindClip(StanceFolders[i],
                        new[] { dir, "InPlace" }, new[] { "Battle", StanceFolders[i] }, null);
                    if (clip != null) tree.AddChild(clip, (float)i);
                }
                state.motion = tree;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private static AnimatorState MakeState(AnimatorStateMachine root, string name, Motion motion, Vector3 pos)
        {
            var st = root.AddState(name, pos);
            st.writeDefaultValues = true;
            st.speed = 1f;
            st.motion = motion;   // may be null → reported already
            _statesMade++;
            return st;
        }

        private static BlendTree NewTree(AnimatorController ctrl, string name)
        {
            var bt = new BlendTree { name = name, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            return bt;
        }

        private static void EnsureParams(AnimatorController c)
        {
            EnsureParam(c, "WeaponType", AnimatorControllerParameterType.Int);
            EnsureParam(c, "StanceBlend", AnimatorControllerParameterType.Float); // M22: float mirror for blend trees
            EnsureParam(c, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParam(c, "MoveZ", AnimatorControllerParameterType.Float);
            EnsureParam(c, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParam(c, "ComboNext", AnimatorControllerParameterType.Trigger);
            EnsureParam(c, "DodgeTrigger", AnimatorControllerParameterType.Trigger);
            EnsureParam(c, "DodgeDirection", AnimatorControllerParameterType.Int);
        }

        private static void EnsureParam(AnimatorController c, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in c.parameters) if (p.name == name) return;
            c.AddParameter(name, type);
        }

        /// <summary>Recursive state lookup (states may live in sub-state-machines, e.g. the M12 Dodge SM).</summary>
        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states)
                if (cs.state.name == name) return cs.state;
            foreach (var sub in sm.stateMachines)
            {
                var found = FindState(sub.stateMachine, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Finds the AnimationClip sub-asset of the best-matching FBX in a stance
        /// folder. Tolerant of the pack's naming quirks: matches by substring,
        /// prefers "Battle"/set-named files, excludes RootMotion/Bow/Arrow/showcase
        /// paths and (optionally) name tokens. Records a miss if nothing matches.
        /// </summary>
        private static AnimationClip FindClip(string stanceFolder, string[] required, string[] prefer, string[] excludeName)
        {
            string dir = $"{AnimRoot}/{stanceFolder}";
            string best = null; int bestScore = -1; int bestLen = int.MaxValue;

            if (Directory.Exists(dir))
            {
                foreach (var raw in Directory.GetFiles(dir, "*.fbx", SearchOption.AllDirectories))
                {
                    string p = raw.Replace('\\', '/');
                    if (p.Contains("/RootMotion/") || p.Contains("/BowAnim/") ||
                        p.Contains("/ArrowAnim/") || p.Contains("/ForShowcasing/")) continue;

                    string fn = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

                    bool ok = true;
                    foreach (var r in required) if (!fn.Contains(r.ToLowerInvariant())) { ok = false; break; }
                    if (!ok) continue;

                    if (excludeName != null)
                    {
                        bool ex = false;
                        foreach (var e in excludeName) if (fn.Contains(e.ToLowerInvariant())) { ex = true; break; }
                        if (ex) continue;
                    }

                    int score = 0;
                    if (prefer != null) foreach (var pr in prefer) if (fn.Contains(pr.ToLowerInvariant())) score++;
                    if (score > bestScore || (score == bestScore && fn.Length < bestLen))
                    {
                        bestScore = score; bestLen = fn.Length; best = p;
                    }
                }
            }

            if (best != null)
            {
                foreach (var rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(best))
                    if (rep is AnimationClip clip) return clip;
            }

            _missing.Add($"{stanceFolder}: {string.Join("+", required)}");
            return null;
        }
    }
}
#endif
