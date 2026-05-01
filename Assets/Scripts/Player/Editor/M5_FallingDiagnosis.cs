// M5_FallingDiagnosis.cs — Read-only edit-time physics diagnostic.
//
// Single menu item:
//   LevelGen ▶ Diagnostics ▶ SampleScene Falling Diagnosis
//
// Walks SampleScene at edit time (does NOT enter Play mode), inspects every
// Collider in the scene, runs four downward Physics.RaycastAll probes, checks
// the Layer Collision Matrix, and emits a markdown report at
// Assets/Documentation/SampleScene_falling_diagnosis.md.
//
// Read-only: opens the scene single-mode, gathers data, writes the report.
// No scene / prefab / asset modifications other than the markdown report.
//
// One-off scaffolding — can be deleted after the diagnosis is consumed by a
// follow-up fix prompt.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelGen.Player.EditorTools
{
    public static class M5_FallingDiagnosis
    {
        private const string ScenePath  = "Assets/Scenes/SampleScene.unity";
        private const string OutputPath = "Assets/Documentation/SampleScene_falling_diagnosis.md";

        private static readonly Vector3 PlayerSpawnPos = new Vector3(5f, 0f, -5f);

        [MenuItem("LevelGen/Diagnostics/SampleScene Falling Diagnosis")]
        public static void Run()
        {
            // ── ① Open scene (single-mode) ───────────────────────────────
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            Physics.SyncTransforms();

            var sb = new StringBuilder();
            sb.AppendLine("# SampleScene — Player Falling Diagnosis");
            sb.AppendLine();
            sb.AppendLine($"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"**Scene:** `{ScenePath}`");
            sb.AppendLine($"**Spawn position:** {Fmt(PlayerSpawnPos)}");
            sb.AppendLine();
            sb.AppendLine("Read-only edit-time inspection. No Play mode. No scene/prefab/script");
            sb.AppendLine("modifications were performed; only this report file is written.");
            sb.AppendLine();

            // ── Locate player + CM cameras (for collider exclusion) ──────
            GameObject playerRoot = null;
            GameObject brainRoot  = null;
            GameObject vcamRoot   = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Player_MaleHero")     playerRoot = go;
                else if (go.name == "CM Brain Camera") brainRoot  = go;
                else if (go.name == "CM Follow Camera") vcamRoot  = go;
            }

            // ── ② Player ────────────────────────────────────────────────
            sb.AppendLine("## Step ② — Player");
            sb.AppendLine();
            if (playerRoot == null)
            {
                sb.AppendLine("**FAIL — no GameObject named `Player_MaleHero` at scene root.**");
                sb.AppendLine("Aborting diagnosis. Run the SampleScene Setup before re-running.");
                File.WriteAllText(OutputPath, sb.ToString());
                AssetDatabase.Refresh();
                Debug.LogError("[M5] Player_MaleHero missing — diagnosis aborted.");
                return;
            }

            var ppos = playerRoot.transform.position;
            sb.AppendLine($"- World position: {Fmt(ppos)}");
            sb.AppendLine($"- activeInHierarchy: {playerRoot.activeInHierarchy}");
            sb.AppendLine($"- Layer: {LayerName(playerRoot.layer)} ({playerRoot.layer})");

            float capsuleBottomY = float.NaN;
            var cc = playerRoot.GetComponent<CharacterController>();
            if (cc == null)
            {
                sb.AppendLine("- CharacterController: **NOT PRESENT** on root.");
            }
            else
            {
                capsuleBottomY = ppos.y + cc.center.y - (cc.height / 2f) - cc.skinWidth;
                sb.AppendLine("- CharacterController:");
                sb.AppendLine($"  - center: {Fmt(cc.center)}");
                sb.AppendLine($"  - height: {cc.height}");
                sb.AppendLine($"  - radius: {cc.radius}");
                sb.AppendLine($"  - skinWidth: {cc.skinWidth}");
                sb.AppendLine($"  - slopeLimit: {cc.slopeLimit}");
                sb.AppendLine($"  - stepOffset: {cc.stepOffset}");
                sb.AppendLine($"  - **Capsule bottom Y (world):** {capsuleBottomY:F4}" +
                              (capsuleBottomY < 0f ? "  *(below Y=0)*" : ""));
            }
            sb.AppendLine();

            // ── ③ Every collider in scene (excluding player + CM rigs) ──
            sb.AppendLine("## Step ③ — All scene colliders");
            sb.AppendLine();
            sb.AppendLine("Excludes colliders under `Player_MaleHero`, `CM Brain Camera`, `CM Follow Camera`.");
            sb.AppendLine();

            var excludeRoots = new HashSet<GameObject>();
            if (playerRoot != null) excludeRoots.Add(playerRoot);
            if (brainRoot  != null) excludeRoots.Add(brainRoot);
            if (vcamRoot   != null) excludeRoots.Add(vcamRoot);

            var rows = new List<ColliderRow>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (excludeRoots.Contains(root)) continue;
                var colliders = root.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders)
                {
                    rows.Add(new ColliderRow
                    {
                        Path       = GetHierarchyPath(c.transform),
                        Type       = c.GetType().Name,
                        Enabled    = c.enabled,
                        IsTrigger  = c.isTrigger,
                        Active     = c.gameObject.activeInHierarchy,
                        LayerName  = LayerName(c.gameObject.layer),
                        LayerIndex = c.gameObject.layer,
                        Bounds     = c.bounds
                    });
                }
            }
            rows.Sort((a, b) => a.Bounds.center.y.CompareTo(b.Bounds.center.y));

            int solidCount = rows.Count(r => r.Enabled && !r.IsTrigger && r.Active);
            int triggerCount = rows.Count(r => r.IsTrigger);
            int disabledOrInactive = rows.Count(r => !r.Enabled || !r.Active);

            sb.AppendLine($"**Total colliders:** {rows.Count}");
            sb.AppendLine($"**Solid surfaces** (`enabled && !isTrigger && activeInHierarchy`): {solidCount}");
            sb.AppendLine($"**Triggers:** {triggerCount}");
            sb.AppendLine($"**Disabled / inactive:** {disabledOrInactive}");
            sb.AppendLine();

            if (rows.Count > 0)
            {
                sb.AppendLine("**Colliders by layer:**");
                foreach (var g in rows.GroupBy(r => r.LayerIndex).OrderBy(g => g.Key))
                {
                    sb.AppendLine($"- {LayerName(g.Key)} ({g.Key}): {g.Count()}");
                }
                sb.AppendLine();

                sb.AppendLine("Sorted by `bounds.center.y` ascending — floor candidates appear first.");
                sb.AppendLine();
                sb.AppendLine("| # | Path | Type | Enabled | Trigger | Active | Layer | min.y | max.y | center | size |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
                int idx = 0;
                foreach (var r in rows)
                {
                    sb.AppendLine($"| {idx++} | `{r.Path}` | {r.Type} | {r.Enabled} | {r.IsTrigger} | {r.Active} | {r.LayerName}({r.LayerIndex}) | {r.Bounds.min.y:F2} | {r.Bounds.max.y:F2} | {Fmt(r.Bounds.center)} | {Fmt(r.Bounds.size)} |");
                }
            }
            else
            {
                sb.AppendLine("**ZERO colliders found in scene** outside of player/CM rigs.");
            }
            sb.AppendLine();

            // ── ④ Raycast probes ────────────────────────────────────────
            sb.AppendLine("## Step ④ — Raycast probes");
            sb.AppendLine();
            sb.AppendLine("Downward `Physics.RaycastAll` from `(x, 10, z)`, distance 50, all layers, triggers reported.");
            sb.AppendLine();

            var probes = new (string label, Vector3 origin)[]
            {
                ("Spawn",            new Vector3(5f, 10f, -5f)),
                ("NW corner inset",  new Vector3(1f, 10f, -1f)),
                ("SE corner inset",  new Vector3(9f, 10f, -9f)),
                ("North edge mid",   new Vector3(5f, 10f, -1f)),
            };

            var probeResults = new List<(string label, Vector3 origin, RaycastHit[] hits, float topSolidY)>();
            foreach (var (label, origin) in probes)
            {
                var hits = Physics.RaycastAll(origin, Vector3.down, 50f, Physics.AllLayers, QueryTriggerInteraction.Collide);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                float topSolidY = float.NaN;
                foreach (var h in hits)
                {
                    if (h.collider != null && !h.collider.isTrigger)
                    {
                        if (float.IsNaN(topSolidY) || h.point.y > topSolidY)
                            topSolidY = h.point.y;
                    }
                }
                probeResults.Add((label, origin, hits, topSolidY));

                sb.AppendLine($"### {label}  — origin {Fmt(origin)}");
                sb.AppendLine();
                sb.AppendLine($"Hits: **{hits.Length}**");
                sb.AppendLine();
                if (hits.Length > 0)
                {
                    sb.AppendLine("| dist | point | type | trigger | layer | path |");
                    sb.AppendLine("|---|---|---|---|---|---|");
                    foreach (var h in hits)
                    {
                        var col = h.collider;
                        if (col == null) continue;
                        sb.AppendLine($"| {h.distance:F3} | {Fmt(h.point)} | {col.GetType().Name} | {col.isTrigger} | {LayerName(col.gameObject.layer)}({col.gameObject.layer}) | `{GetHierarchyPath(col.transform)}` |");
                    }
                    sb.AppendLine();
                }
                if (float.IsNaN(topSolidY))
                    sb.AppendLine("**Highest non-trigger surface:** *(none)*");
                else
                    sb.AppendLine($"**Highest non-trigger surface Y:** {topSolidY:F4}");
                sb.AppendLine();
            }

            // ── ⑤ Layer collision matrix ────────────────────────────────
            sb.AppendLine("## Step ⑤ — Layer collision matrix");
            sb.AppendLine();
            int playerLayer = playerRoot.layer;
            sb.AppendLine($"Player layer: **{LayerName(playerLayer)} ({playerLayer})**");
            sb.AppendLine();

            sb.AppendLine("Layers the player collides with:");
            for (int i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(playerLayer, i))
                {
                    var n = LayerName(i);
                    if (!string.IsNullOrEmpty(n))
                        sb.AppendLine($"- {n} ({i})");
                }
            }
            sb.AppendLine();

            sb.AppendLine("Layers used by **non-trigger** colliders found in Step ③, and whether the player collides with each:");
            var nonTriggerLayers = rows.Where(r => !r.IsTrigger).Select(r => r.LayerIndex).Distinct().OrderBy(i => i).ToList();
            int layerMismatchCount = 0;
            if (nonTriggerLayers.Count == 0)
            {
                sb.AppendLine("- *(no non-trigger colliders to compare)*");
            }
            else
            {
                foreach (var fl in nonTriggerLayers)
                {
                    bool collides = !Physics.GetIgnoreLayerCollision(playerLayer, fl);
                    sb.AppendLine($"- {LayerName(fl)} ({fl}): {(collides ? "COLLIDES" : "**IGNORED**")}");
                    if (!collides) layerMismatchCount++;
                }
            }
            sb.AppendLine();

            // ── ⑥ Diagnosis ─────────────────────────────────────────────
            sb.AppendLine("## Step ⑥ — Diagnosis");
            sb.AppendLine();

            var spawnProbe = probeResults[0];
            int spawnSolidHits = spawnProbe.hits.Count(h => h.collider != null && !h.collider.isTrigger);
            int spawnTriggerHits = spawnProbe.hits.Count(h => h.collider != null && h.collider.isTrigger);

            int probesWithoutSolid = probeResults.Count(p => float.IsNaN(p.topSolidY));

            var conclusions = new List<string>();

            // A — no solid under spawn
            if (spawnSolidHits == 0 && solidCount == 0)
            {
                conclusions.Add("**A. No solid colliders exist anywhere in the scene** (outside player/CM rigs). " +
                                "Step ③ found 0 non-trigger colliders. The CharacterController falls because nothing physical is below it.");
            }
            else if (spawnSolidHits == 0 && solidCount > 0)
            {
                conclusions.Add("**A (partial). No solid collider directly under the spawn position.** " +
                                $"Step ③ found {solidCount} solid collider(s) elsewhere, but the downward raycast at {Fmt(spawnProbe.origin)} hit none of them.");
            }

            // B — disabled/inactive
            if (disabledOrInactive > 0)
            {
                conclusions.Add($"**B. {disabledOrInactive} collider(s) are disabled or on inactive GameObjects.** " +
                                "See the Step ③ table for rows with Enabled=False or Active=False.");
            }

            // C — only triggers under spawn
            if (spawnSolidHits == 0 && spawnTriggerHits > 0)
            {
                conclusions.Add($"**C. The spawn raycast only hit triggers** ({spawnTriggerHits} trigger hit(s), 0 solid). " +
                                "Triggers detect overlap but provide no surface — the player passes through them.");
            }

            // D — layer mismatch
            if (layerMismatchCount > 0)
            {
                conclusions.Add($"**D. Layer collision mismatch.** Player's layer ignores {layerMismatchCount} layer(s) that have non-trigger colliders. See Step ⑤ for the specific layer pairs.");
            }

            // E — floor above spawn Y
            if (!float.IsNaN(spawnProbe.topSolidY) && spawnProbe.topSolidY > ppos.y + 0.01f)
            {
                conclusions.Add($"**E. Floor surface above spawn Y.** Highest non-trigger Y under spawn = {spawnProbe.topSolidY:F4}, " +
                                $"player position Y = {ppos.y:F4}. Player spawns embedded in / below the floor. " +
                                $"Move spawn to Y={spawnProbe.topSolidY:F4} (or higher).");
            }

            // F — capsule bottom below floor by > skinWidth + 0.1
            if (!float.IsNaN(capsuleBottomY) && !float.IsNaN(spawnProbe.topSolidY))
            {
                float gap = spawnProbe.topSolidY - capsuleBottomY;
                if (gap > (cc != null ? cc.skinWidth : 0f) + 0.1f)
                {
                    conclusions.Add($"**F. Capsule bottom is {gap:F4} below the floor surface** (capsule bottom = {capsuleBottomY:F4}, floor top = {spawnProbe.topSolidY:F4}). " +
                                    "Larger than skinWidth + 0.1 — embedded in floor at start.");
                }
            }

            // G — gaps in coverage
            if (probesWithoutSolid > 0 && probesWithoutSolid < probes.Length)
            {
                var gapList = string.Join(", ", probeResults.Where(p => float.IsNaN(p.topSolidY)).Select(p => p.label));
                conclusions.Add($"**G. Gaps in floor coverage.** {probesWithoutSolid}/{probes.Length} probe(s) found no solid surface: {gapList}.");
            }

            // H — fallthrough
            if (conclusions.Count == 0)
            {
                conclusions.Add("**H. None of patterns A–G apply.** " +
                                "Solid colliders exist under the spawn, layer matrix permits collision, capsule bottom sits at or above the floor, all four probes hit a solid surface. " +
                                "If the player still falls in Play mode, the issue is likely runtime (e.g., gravity overriding ground stick, CharacterController not actually grounded after first physics step, or a script issue). Consider adding logs to PlayerController.ApplyGravity / step 1.5 to see what `_cc.isGrounded` returns each frame.");
            }

            foreach (var c in conclusions)
            {
                sb.AppendLine($"- {c}");
                sb.AppendLine();
            }

            sb.AppendLine("### Suggested fix paths");
            sb.AppendLine();
            if (conclusions.Any(c => c.StartsWith("**A.")))
            {
                sb.AppendLine("If the room geometry has no colliders (Conclusion A):");
                sb.AppendLine();
                sb.AppendLine("1. **Quick scene-only fix** — add one thin `BoxCollider` GameObject under the room root covering the 10×10 footprint at Y=0. Non-invasive, doesn't change prefabs.");
                sb.AppendLine("2. **Per-floor prefab fix** — add `MeshCollider` (convex=false) to each FDP floor part prefab. Affects every scene that uses those parts; preferred long-term answer.");
                sb.AppendLine("3. **Manual ground plane** — drop a Plane GameObject at the room root scaled to the room footprint, with its default MeshCollider.");
                sb.AppendLine();
            }
            if (conclusions.Any(c => c.StartsWith("**B.")))
            {
                sb.AppendLine("If colliders exist but are disabled/inactive (Conclusion B): re-enable them on the prefab or scene instance. Look at Step ③'s rows where Enabled=False or Active=False.");
                sb.AppendLine();
            }
            if (conclusions.Any(c => c.StartsWith("**C.")))
            {
                sb.AppendLine("If only triggers exist (Conclusion C): the floor parts likely had `isTrigger` enabled by mistake. Toggle off on the prefabs.");
                sb.AppendLine();
            }
            if (conclusions.Any(c => c.StartsWith("**D.")))
            {
                sb.AppendLine("If layer mismatch (Conclusion D): adjust `Edit ▶ Project Settings ▶ Physics ▶ Layer Collision Matrix` so the player's layer collides with the floor's layer.");
                sb.AppendLine();
            }
            if (conclusions.Any(c => c.StartsWith("**E.")))
            {
                sb.AppendLine("If spawn is below floor (Conclusion E): move the spawn Y to at least the floor's top Y (see the value reported in the conclusion).");
                sb.AppendLine();
            }
            if (conclusions.Any(c => c.StartsWith("**G.")))
            {
                sb.AppendLine("If gap coverage (Conclusion G): the room has missing floor parts in the gap regions. Either add floor pieces or drop in a single covering BoxCollider as a stop-gap.");
                sb.AppendLine();
            }

            // ── Write report ────────────────────────────────────────────
            File.WriteAllText(OutputPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[M5 Diagnosis] Report written: {OutputPath}");
            Debug.Log($"[M5 Diagnosis] Summary — total colliders: {rows.Count}, solid: {solidCount}, " +
                      $"spawn solid hits: {spawnSolidHits}, top solid Y under spawn: " +
                      $"{(float.IsNaN(spawnProbe.topSolidY) ? "n/a" : spawnProbe.topSolidY.ToString("F4"))}");
            Debug.Log($"[M5 Diagnosis] Conclusions reached: {conclusions.Count} ({string.Join("; ", conclusions.Select(c => c.Substring(0, Math.Min(c.IndexOf('.') >= 0 ? c.IndexOf('.') + 1 : 4, c.Length))))})");
        }

        // ─────────────────────────────────────────────────────────────────
        private struct ColliderRow
        {
            public string Path;
            public string Type;
            public bool   Enabled;
            public bool   IsTrigger;
            public bool   Active;
            public string LayerName;
            public int    LayerIndex;
            public Bounds Bounds;
        }

        private static string LayerName(int idx)
        {
            var n = LayerMask.LayerToName(idx);
            return string.IsNullOrEmpty(n) ? $"<unnamed-{idx}>" : n;
        }

        private static string Fmt(Vector3 v) => $"({v.x:F2}, {v.y:F2}, {v.z:F2})";

        private static string GetHierarchyPath(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return "/" + string.Join("/", stack);
        }
    }
}
#endif
