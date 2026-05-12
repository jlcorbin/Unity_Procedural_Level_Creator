// AnimationInventoryGenerator.cs — read-only scan + markdown report.
//
// Menu: LevelGen ▶ Diagnostics ▶ Generate Animation Inventory
// Output: Documentation/Animation_Inventory.md (overwrites)
//
// Scans every FBX (for AnimationClip sub-assets), every
// AnimatorController, every AnimatorOverrideController, and every
// standalone .anim file. Produces a single markdown reference doc
// so future sessions don't need to dig through FBX sub-assets or
// controller YAML at prompt time.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen
{
    public static class AnimationInventoryGenerator
    {
        private const string OutputPath = "Documentation/Animation_Inventory.md";
        private const string MenuPath   = "LevelGen/Diagnostics/Generate Animation Inventory";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Animation Inventory");
            sb.AppendLine($"*Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*");
            sb.AppendLine($"*Regenerate via `LevelGen ▶ Diagnostics ▶ Generate Animation Inventory`*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            WriteFbxClips(sb);
            WriteControllers(sb);
            WriteOverrideControllers(sb);
            WriteStandaloneAnims(sb);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath    = Path.Combine(projectRoot, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString());

            Debug.Log($"[AnimationInventory] Wrote {OutputPath} ({sb.Length} chars).");
            EditorUtility.RevealInFinder(fullPath);
        }

        // ── FBX clip scan ────────────────────────────────────────────────

        private static void WriteFbxClips(StringBuilder sb)
        {
            sb.AppendLine("## FBX Animation Clips");
            sb.AppendLine();

            var fbxPaths = AssetDatabase.FindAssets("t:Model")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            int totalClipsLogged = 0;
            int fbxWithClips     = 0;

            foreach (var path in fbxPaths)
            {
                var subs = AssetDatabase.LoadAllAssetsAtPath(path);
                var clips = subs.OfType<AnimationClip>()
                                .Where(c => !c.name.StartsWith("__preview__"))
                                .OrderBy(c => c.name, StringComparer.Ordinal)
                                .ToList();
                if (clips.Count == 0) continue;

                fbxWithClips++;
                sb.AppendLine($"### {Path.GetFileName(path)}");
                sb.AppendLine($"*{path}*");
                sb.AppendLine();
                sb.AppendLine("| Clip name | Duration (s) | Frames | Looping | Events |");
                sb.AppendLine("|-----------|--------------|--------|---------|--------|");
                foreach (var clip in clips)
                {
                    int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
                    sb.AppendLine(
                        $"| {EscapePipe(clip.name)} | {clip.length:F2} | {frames} | {clip.isLooping} | {clip.events.Length} |");
                    totalClipsLogged++;
                }
                sb.AppendLine();
            }

            sb.AppendLine($"*Total: {totalClipsLogged} clips across {fbxWithClips} FBX files " +
                          $"(of {fbxPaths.Count} FBX assets scanned).*");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // ── Animator Controller scan ────────────────────────────────────

        private static void WriteControllers(StringBuilder sb)
        {
            sb.AppendLine("## Animator Controllers");
            sb.AppendLine();

            var paths = AssetDatabase.FindAssets("t:AnimatorController")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (paths.Count == 0)
            {
                sb.AppendLine("*(none found)*");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                return;
            }

            foreach (var path in paths)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (ctrl == null) continue;
                WriteController(sb, ctrl, path);
            }
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void WriteController(StringBuilder sb, AnimatorController ctrl, string path)
        {
            sb.AppendLine($"### {ctrl.name}");
            sb.AppendLine($"*{path}*");
            sb.AppendLine();

            // Parameters
            sb.AppendLine("**Parameters:**");
            sb.AppendLine();
            if (ctrl.parameters == null || ctrl.parameters.Length == 0)
            {
                sb.AppendLine("*(none)*");
            }
            else
            {
                sb.AppendLine("| Name | Type |");
                sb.AppendLine("|------|------|");
                foreach (var p in ctrl.parameters)
                    sb.AppendLine($"| {EscapePipe(p.name)} | {p.type} |");
            }
            sb.AppendLine();

            // Per-layer
            foreach (var layer in ctrl.layers)
            {
                var sm = layer.stateMachine;
                string defaultState = sm != null && sm.defaultState != null
                    ? sm.defaultState.name
                    : "(none)";

                sb.AppendLine($"**States — Layer: {layer.name} (default: {defaultState}):**");
                sb.AppendLine();
                sb.AppendLine("| State | Motion clip |");
                sb.AppendLine("|-------|-------------|");
                int stateCount = WriteStates(sb, sm, "");
                if (stateCount == 0) sb.AppendLine("| *(none)* | |");
                sb.AppendLine();

                sb.AppendLine($"**AnyState transitions — Layer: {layer.name}:**");
                sb.AppendLine();
                var anyTrans = CollectAllAnyStateTransitions(sm).ToList();
                if (anyTrans.Count == 0)
                {
                    sb.AppendLine("*(none)*");
                }
                else
                {
                    sb.AppendLine("| Destination | Conditions |");
                    sb.AppendLine("|-------------|------------|");
                    foreach (var t in anyTrans)
                    {
                        string dest = t.destinationState != null
                            ? t.destinationState.name
                            : (t.destinationStateMachine != null
                                ? $"(SM) {t.destinationStateMachine.name}"
                                : "(exit)");
                        sb.AppendLine($"| {EscapePipe(dest)} | {EscapePipe(ConditionSummary(t.conditions))} |");
                    }
                }
                sb.AppendLine();
            }
        }

        private static int WriteStates(StringBuilder sb, AnimatorStateMachine sm, string prefix)
        {
            if (sm == null) return 0;
            int count = 0;
            foreach (var cs in sm.states.OrderBy(c => c.state.name, StringComparer.Ordinal))
            {
                string motion;
                if (cs.state.motion == null)              motion = "(none)";
                else if (cs.state.motion is BlendTree bt) motion = $"BlendTree: {bt.name}";
                else                                       motion = cs.state.motion.name;
                sb.AppendLine($"| {EscapePipe(prefix + cs.state.name)} | {EscapePipe(motion)} |");
                count++;
            }
            foreach (var csm in sm.stateMachines.OrderBy(c => c.stateMachine.name, StringComparer.Ordinal))
            {
                count += WriteStates(sb, csm.stateMachine, prefix + csm.stateMachine.name + "/");
            }
            return count;
        }

        private static IEnumerable<AnimatorStateTransition> CollectAllAnyStateTransitions(AnimatorStateMachine sm)
        {
            if (sm == null) yield break;
            foreach (var t in sm.anyStateTransitions) yield return t;
            foreach (var csm in sm.stateMachines)
                foreach (var t in CollectAllAnyStateTransitions(csm.stateMachine))
                    yield return t;
        }

        private static string ConditionSummary(AnimatorCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0) return "(none)";
            return string.Join(" AND ", conditions.Select(FormatCondition));
        }

        private static string FormatCondition(AnimatorCondition c)
        {
            switch (c.mode)
            {
                case AnimatorConditionMode.If:       return $"{c.parameter} (If)";
                case AnimatorConditionMode.IfNot:    return $"{c.parameter} (IfNot)";
                case AnimatorConditionMode.Greater:  return $"{c.parameter} > {c.threshold}";
                case AnimatorConditionMode.Less:     return $"{c.parameter} < {c.threshold}";
                case AnimatorConditionMode.Equals:   return $"{c.parameter} == {c.threshold}";
                case AnimatorConditionMode.NotEqual: return $"{c.parameter} != {c.threshold}";
                default:                             return $"{c.parameter} ?";
            }
        }

        // ── Override Controllers ───────────────────────────────────────

        private static void WriteOverrideControllers(StringBuilder sb)
        {
            sb.AppendLine("## Override Controllers");
            sb.AppendLine();

            var paths = AssetDatabase.FindAssets("t:AnimatorOverrideController")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (paths.Count == 0)
            {
                sb.AppendLine("*(none found)*");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                return;
            }

            foreach (var path in paths)
            {
                var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                if (oc == null) continue;
                sb.AppendLine($"### {oc.name}");
                sb.AppendLine($"*{path}*");
                sb.AppendLine();
                string baseName = oc.runtimeAnimatorController != null
                    ? oc.runtimeAnimatorController.name
                    : "(none)";
                sb.AppendLine($"**Base controller:** {baseName}");
                sb.AppendLine();

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                oc.GetOverrides(overrides);
                if (overrides.Count == 0)
                {
                    sb.AppendLine("*(no slots)*");
                }
                else
                {
                    sb.AppendLine("| Base clip | Override clip |");
                    sb.AppendLine("|-----------|---------------|");
                    foreach (var kv in overrides.OrderBy(k => k.Key != null ? k.Key.name : "",
                                                         StringComparer.Ordinal))
                    {
                        string baseClip     = kv.Key   != null ? kv.Key.name   : "(null)";
                        string overrideClip = kv.Value != null ? kv.Value.name : "(unmapped)";
                        sb.AppendLine($"| {EscapePipe(baseClip)} | {EscapePipe(overrideClip)} |");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        // ── Standalone .anim files ─────────────────────────────────────

        private static void WriteStandaloneAnims(StringBuilder sb)
        {
            sb.AppendLine("## Standalone .anim clips");
            sb.AppendLine();

            var standalone = AssetDatabase.FindAssets("t:AnimationClip")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (standalone.Count == 0)
            {
                sb.AppendLine("*(none found)*");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("| Clip name | Duration (s) | Frames | Looping | Events | Path |");
            sb.AppendLine("|-----------|--------------|--------|---------|--------|------|");
            foreach (var path in standalone)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;
                int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
                sb.AppendLine(
                    $"| {EscapePipe(clip.name)} | {clip.length:F2} | {frames} | {clip.isLooping} | " +
                    $"{clip.events.Length} | {EscapePipe(path)} |");
            }
            sb.AppendLine();
        }

        // Escape | so it doesn't break Markdown table rows.
        private static string EscapePipe(string s) => s == null ? "" : s.Replace("|", "\\|");
    }
}
