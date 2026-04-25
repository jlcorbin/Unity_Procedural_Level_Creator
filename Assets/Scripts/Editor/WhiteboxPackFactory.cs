#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Whitebox asset pipeline for the Fantastic Dungeon Pack.
    ///
    /// <b>Step 1</b> — Mirror meshes: for every FBX in
    /// <c>Assets/Fantastic Dungeon Pack/3d/modular/</c>, deep-copies its embedded
    /// Mesh sub-assets verbatim into <c>Assets/Whitebox/3d/modular/</c>
    /// (same geometry, same silhouette — no bounding-box approximation).
    ///
    /// <b>Step 2</b> — Wrap in prefabs: wraps each Step-1 mesh in a single-object
    /// prefab (MeshFilter + MeshRenderer) tinted by subfolder, saved under
    /// <c>Assets/Whitebox/prefabs/modular/</c>. Creates an opaque and an
    /// alpha-clipped material variant per tint category.
    ///
    /// Menus: <b>LevelGen ▶ Whitebox ▶ Generate (Step 1: mirror meshes)</b>
    ///        <b>LevelGen ▶ Whitebox ▶ Generate (Step 2: wrap meshes in prefabs)</b>
    /// </summary>
    public static class WhiteboxPackFactory
    {
        // ─── Paths ────────────────────────────────────────────────────────────
        private const string FDP_ASSET_ROOT = "Assets/Fantastic Dungeon Pack/3d/modular";
        private const string WB_ASSET_ROOT  = "Assets/Whitebox/3d/modular";
        private const string WB_PREFAB_ROOT = "Assets/Whitebox/prefabs/modular";
        private const string WB_MAT_FOLDER  = "Assets/Whitebox/Materials";

        // ─── Step 3 paths ─────────────────────────────────────────────────────
        private const string FDP_PARTS_ROOT = "Assets/Fantastic Dungeon Pack/prefabs/MODULAR/01_PARTS";
        private const string FDP_COMPS_ROOT = "Assets/Fantastic Dungeon Pack/prefabs/MODULAR/02_COMPS";
        private const string WB_COMPS_ROOT  = "Assets/Whitebox/prefabs/COMPS";

        // ─── Step 4 paths ─────────────────────────────────────────────────────
        private const string FDP_LVL_ROOT = "Assets/Fantastic Dungeon Pack/prefabs/MODULAR/03_LEVEL_MODULES";
        private const string WB_LVL_ROOT  = "Assets/Whitebox/prefabs/LEVEL_MODULES";

        // ─── Step 2 tint table ────────────────────────────────────────────────
        // Maps top-level source subfolder → (material base-name, base tint color).
        // WallTrim and Trim reuse the Wall material; their _Cutout variants share
        // the same material object via the cache.
        private static readonly (string folder, string matName, Color color)[] s_tintTable =
        {
            ("Wall",     "Whitebox_Wall",    HexColor("EDEDED")),
            ("WallTrim", "Whitebox_Wall",    HexColor("EDEDED")),
            ("Trim",     "Whitebox_Wall",    HexColor("EDEDED")),
            ("Floor",    "Whitebox_Floor",   HexColor("C8C8C8")),
            ("Gateway",  "Whitebox_Gateway", HexColor("A8C8E8")),
            ("Column",   "Whitebox_Column",  HexColor("B8E0B8")),
            ("Stairs",   "Whitebox_Stairs",  HexColor("E8D878")),
            ("Railing",  "Whitebox_Railing", HexColor("D8B8A0")),
            ("Base",     "Whitebox_Base",    HexColor("B0B0B0")),
        };

        private static readonly Color COLOR_DEFAULT = HexColor("E0E0E0");
        private const string MAT_DEFAULT_NAME = "Whitebox_Default";

        // =====================================================================
        //  STEP 1 — MIRROR MESHES
        // =====================================================================

        /// <summary>
        /// For every FBX in the FDP modular folder, deep-copies each embedded
        /// Mesh sub-asset verbatim into <c>Assets/Whitebox/3d/modular/</c>.
        /// Also deletes any stale <c>Assets/Whitebox/prefabs/modular/</c> output
        /// (prefabs reference these meshes and must be regenerated via Step 2).
        /// <c>Assets/Whitebox/Materials/</c> is preserved so Step 2 material
        /// references survive between re-runs.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Generate (Step 1: mirror meshes)")]
        public static void GenerateMirroredMeshes()
        {
            bool meshesExist  = AssetDatabase.IsValidFolder(WB_ASSET_ROOT);
            bool prefabsExist = AssetDatabase.IsValidFolder(WB_PREFAB_ROOT);

            if (meshesExist || prefabsExist)
            {
                string deleteList = (meshesExist  ? "  • Assets/Whitebox/3d/modular/\n"     : "")
                                  + (prefabsExist ? "  • Assets/Whitebox/prefabs/modular/\n" : "");

                bool ok = EditorUtility.DisplayDialog(
                    "Regenerate whitebox meshes?",
                    $"This will delete:\n{deleteList}" +
                    "Materials will be preserved.\n\nContinue?",
                    "OK", "Cancel");

                if (!ok) { Debug.Log("[Whitebox] Cancelled."); return; }

                if (meshesExist)  AssetDatabase.DeleteAsset(WB_ASSET_ROOT);
                if (prefabsExist) AssetDatabase.DeleteAsset(WB_PREFAB_ROOT);
                AssetDatabase.Refresh();
            }

            // ── Collect FBX paths ─────────────────────────────────────────────
            string fsRoot = Path.Combine(Application.dataPath,
                                         "Fantastic Dungeon Pack/3d/modular")
                                .Replace('\\', '/');

            if (!Directory.Exists(fsRoot))
            {
                Debug.LogError($"[Whitebox] Source folder not found: {fsRoot}");
                return;
            }

            string dataPath = Application.dataPath.Replace('\\', '/');
            var srcPaths = Directory.GetFiles(fsRoot, "*.fbx", SearchOption.AllDirectories)
                .Select(p => "Assets" + p.Replace('\\', '/').Substring(dataPath.Length))
                .OrderBy(p => p)
                .ToList();

            // ── Inventory log ─────────────────────────────────────────────────
            var perFolder = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (string ap in srcPaths)
            {
                string rel    = ap.Substring(FDP_ASSET_ROOT.Length).TrimStart('/');
                string folder = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
                perFolder.TryGetValue(folder, out int n);
                perFolder[folder] = n + 1;
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Whitebox] Source inventory:");
            foreach (var kv in perFolder.OrderBy(kv => kv.Key))
                sb.AppendLine($"  {FDP_ASSET_ROOT}/{kv.Key}: {kv.Value} FBX files");
            sb.Append($"  Total: {srcPaths.Count} FBX files");
            Debug.Log(sb.ToString());

            // ── Deep-copy meshes ──────────────────────────────────────────────
            int generated = 0, skipped = 0;

            foreach (string srcPath in srcPaths)
            {
                Mesh[] srcMeshes = AssetDatabase.LoadAllAssetsAtPath(srcPath)
                    .OfType<Mesh>()
                    .Where(m => !string.IsNullOrEmpty(m.name) && !m.name.StartsWith("$"))
                    .ToArray();

                if (srcMeshes.Length == 0)
                {
                    Debug.LogWarning($"[Whitebox] No usable meshes in {srcPath} — skipped.");
                    skipped++;
                    continue;
                }

                string rel    = srcPath.Substring(FDP_ASSET_ROOT.Length).TrimStart('/');
                string subDir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
                string fbxBase = Path.GetFileNameWithoutExtension(rel);

                string destDir = string.IsNullOrEmpty(subDir)
                    ? WB_ASSET_ROOT
                    : $"{WB_ASSET_ROOT}/{subDir}";
                EnsureDir(destDir);

                foreach (Mesh srcMesh in srcMeshes)
                {
                    // Single-mesh FBX: asset name = FBX base name (matches Step 1 v1 behavior).
                    // Multi-mesh FBX:  asset name = mesh.name (distinguishes LODs / sub-parts).
                    string assetName = (srcMeshes.Length == 1) ? fbxBase : srcMesh.name;
                    string destPath  = $"{destDir}/{assetName}.asset";

                    Mesh copy = DeepCopyMesh(srcMesh, assetName);

                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(destPath);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(copy, existing);
                        EditorUtility.SetDirty(existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(copy, destPath);
                    }
                    generated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string skipNote = skipped > 0 ? $" Skipped {skipped} FBX (no usable meshes)." : "";
            Debug.Log($"[Whitebox] Step 1 complete. Generated {generated} mesh assets " +
                      $"in {WB_ASSET_ROOT}.{skipNote}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MESH COPY
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns an in-memory deep copy of <paramref name="src"/> that can be
        /// saved as a standalone Unity mesh asset via
        /// <c>AssetDatabase.CreateAsset</c>.
        /// All geometry data (vertices, normals, tangents, UVs, sub-meshes,
        /// bind-poses, blend shapes) is preserved verbatim.
        /// </summary>
        static Mesh DeepCopyMesh(Mesh src, string newName)
        {
            var dst = Object.Instantiate(src);
            dst.name = newName;
            dst.RecalculateBounds();
            return dst;
        }

        // =====================================================================
        //  STEP 2 — WRAP MESHES IN PREFABS
        // =====================================================================

        /// <summary>
        /// Wraps every mesh asset in <c>Assets/Whitebox/3d/modular/</c> in a
        /// single-object prefab (MeshFilter + MeshRenderer).  Each prefab is
        /// tinted by its top-level subfolder.  Two material variants are created
        /// per category: opaque and alpha-clipped (cutout).
        ///
        /// Requires Step 1 to have been run first.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Generate (Step 2: wrap meshes in prefabs)")]
        public static void GeneratePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(WB_ASSET_ROOT)
                || AssetDatabase.FindAssets("t:Mesh", new[] { WB_ASSET_ROOT }).Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Step 1 required",
                    $"Run Step 1 first.\n{WB_ASSET_ROOT} not found or empty.",
                    "OK");
                return;
            }

            Debug.Log("[Whitebox] Step 2 start.");

            // ── 2a: Materials ─────────────────────────────────────────────────
            EnsureDir(WB_MAT_FOLDER);

            int matsCreated = 0, matsReused = 0;
            var matCache = new Dictionary<string, Material>(System.StringComparer.Ordinal);

            // Default material (opaque + cutout).
            EnsureMat(MAT_DEFAULT_NAME, COLOR_DEFAULT, cutout: false,
                      matCache, ref matsCreated, ref matsReused);
            EnsureMat(MAT_DEFAULT_NAME + "_Cutout", COLOR_DEFAULT, cutout: true,
                      matCache, ref matsCreated, ref matsReused);

            // Per-category materials (deduplicated — WallTrim/Trim reuse Wall entry).
            foreach (var (_, matName, color) in s_tintTable)
            {
                EnsureMat(matName,            color, cutout: false,
                          matCache, ref matsCreated, ref matsReused);
                EnsureMat(matName + "_Cutout", color, cutout: true,
                          matCache, ref matsCreated, ref matsReused);
            }

            Debug.Log($"[Whitebox] Materials: {matsCreated} created, {matsReused} reused.");

            // ── 2b: Prefabs ───────────────────────────────────────────────────
            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { WB_ASSET_ROOT });

            int prefabCount = 0, opaqueCount = 0, cutoutCount = 0;
            int matDetectCount = 0, filenameDetectCount = 0;
            var perCategory = new Dictionary<string, int>(System.StringComparer.Ordinal);

            foreach (string guid in meshGuids)
            {
                string meshPath = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null) continue;

                string rel       = meshPath.Substring(WB_ASSET_ROOT.Length).TrimStart('/');
                string baseName  = Path.GetFileNameWithoutExtension(rel);
                string subDir    = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? "";
                string topFolder = subDir.Contains('/')
                    ? subDir.Substring(0, subDir.IndexOf('/'))
                    : subDir;

                var (opaqueMat, cutoutMat) = ResolveMaterials(topFolder, matCache);

                var (isCutout, detPath) = DetectCutout(subDir, baseName, topFolder);
                if (detPath == "material-inspection") matDetectCount++;
                else filenameDetectCount++;

                Material mat = isCutout ? cutoutMat : opaqueMat;
                if (isCutout) cutoutCount++; else opaqueCount++;

                // Output prefab mirrors the mesh tree.
                string destDir  = string.IsNullOrEmpty(subDir)
                    ? WB_PREFAB_ROOT
                    : $"{WB_PREFAB_ROOT}/{subDir}";
                string destPath = $"{destDir}/{baseName}.prefab";
                EnsureDir(destDir);

                var go = new GameObject(baseName);
                go.AddComponent<MeshFilter>().sharedMesh       = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                PrefabUtility.SaveAsPrefabAsset(go, destPath);
                Object.DestroyImmediate(go);

                prefabCount++;

                string cat = string.IsNullOrEmpty(topFolder) ? "(root)" : topFolder;
                perCategory.TryGetValue(cat, out int n);
                perCategory[cat] = n + 1;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Summary logs.
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Whitebox] Prefabs per category:");
            foreach (var kv in perCategory.OrderBy(kv => kv.Key))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
            Debug.Log(sb.ToString());

            Debug.Log($"[Whitebox] Detection: {matDetectCount} by material inspection, " +
                      $"{filenameDetectCount} by filename fallback. " +
                      $"Note: cutout materials use flat tint (no source texture — " +
                      $"alpha cutout shape not preserved in this pass).");
            Debug.Log($"[Whitebox] Step 2 complete. Generated {prefabCount} prefabs " +
                      $"({opaqueCount} opaque, {cutoutCount} cutout) in {WB_PREFAB_ROOT}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MATERIAL HELPERS
        // ─────────────────────────────────────────────────────────────────────

        // Creates or reuses a material asset at WB_MAT_FOLDER/{matName}.mat.
        // Always refreshes color (and alpha-clip settings for cutout) so tint
        // changes take effect on re-run without breaking prefab references.
        static void EnsureMat(string matName, Color color, bool cutout,
                               Dictionary<string, Material> cache,
                               ref int created, ref int reused)
        {
            if (cache.ContainsKey(matName)) return;   // already handled this run

            string path = $"{WB_MAT_FOLDER}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard");
                mat = new Material(shader) { name = matName };
                AssetDatabase.CreateAsset(mat, path);
                created++;
            }
            else
            {
                reused++;
            }

            mat.SetColor("_BaseColor", color);

            if (cutout)
            {
                // URP/Lit — opaque surface with alpha clipping enabled.
                mat.SetFloat("_Surface",   0f);    // Opaque (not Transparent)
                mat.SetFloat("_AlphaClip", 1f);    // Enable alpha clip
                mat.SetFloat("_Cutoff",    0.5f);  // Clip threshold
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;             // AlphaTest render queue
                // _BaseMap intentionally left unset — flat tint only (option 2).
                // Real alpha cutout shape requires per-piece source texture; deferred.
            }

            EditorUtility.SetDirty(mat);
            cache[matName] = mat;
        }

        // Returns the opaque and cutout material pair for a given top-level folder.
        static (Material opaque, Material cutout) ResolveMaterials(
            string topFolder, Dictionary<string, Material> matCache)
        {
            foreach (var (folder, matName, _) in s_tintTable)
            {
                if (string.Equals(folder, topFolder,
                                  System.StringComparison.OrdinalIgnoreCase))
                    return (matCache[matName], matCache[matName + "_Cutout"]);
            }
            return (matCache[MAT_DEFAULT_NAME], matCache[MAT_DEFAULT_NAME + "_Cutout"]);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CUTOUT DETECTION
        // ─────────────────────────────────────────────────────────────────────

        // Returns (isCutout, detectionPath) for a whitebox mesh asset.
        // Detection path 1 — material inspection: load the corresponding source
        //   FBX and check any embedded material for alpha-clip properties.
        // Detection path 2 — filename fallback: Railing/, Trim/ top-folders, or
        //   baseName containing "arch" or "gate".
        static (bool isCutout, string detectionPath) DetectCutout(
            string subDir, string baseName, string topFolder)
        {
            string fbxPath = FindSourceFbxPath(subDir, baseName);
            if (fbxPath != null)
            {
                Material[] embeddedMats = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                    .OfType<Material>().ToArray();

                if (embeddedMats.Length > 0)
                {
                    bool cutout = embeddedMats.Any(IsMaterialCutout);
                    return (cutout, "material-inspection");
                }
                // Materials were extracted to a separate folder (Unity default) —
                // no embedded mats found; fall through to filename heuristic.
            }

            // Filename fallback.
            string lower = baseName.ToLowerInvariant();
            bool isCutoutFn =
                string.Equals(topFolder, "Railing",
                              System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(topFolder, "Trim",
                              System.StringComparison.OrdinalIgnoreCase) ||
                lower.Contains("arch") ||
                lower.Contains("gate");

            return (isCutoutFn, "filename-fallback");
        }

        // Returns true when a material's properties indicate alpha clipping.
        static bool IsMaterialCutout(Material mat)
        {
            if (mat == null) return false;
            if (mat.IsKeywordEnabled("_ALPHATEST_ON")) return true;
            if (mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f) return true;
            // AlphaTest render queue range: 2450–2999.
            if (mat.renderQueue >= 2450 && mat.renderQueue < 3000) return true;
            return false;
        }

        // Reconstructs the source FBX asset path from a whitebox mesh's subDir + baseName.
        // Handles single-mesh FBX (exact match) and multi-mesh FBX (strips trailing _N).
        // Returns null if no matching FBX is found.
        static string FindSourceFbxPath(string subDir, string baseName)
        {
            string srcFolder = string.IsNullOrEmpty(subDir)
                ? FDP_ASSET_ROOT
                : $"{FDP_ASSET_ROOT}/{subDir}";

            // Direct match: baseName == FBX base name.
            string direct = $"{srcFolder}/{baseName}.fbx";
            if (AssetDatabase.LoadAssetAtPath<Object>(direct) != null)
                return direct;

            // Multi-mesh FBX case: baseName might be "foo_2" from "foo.fbx".
            // Strip a trailing _N suffix and retry.
            int lastUs = baseName.LastIndexOf('_');
            if (lastUs > 0 && int.TryParse(baseName.Substring(lastUs + 1), out _))
            {
                string stripped = $"{srcFolder}/{baseName.Substring(0, lastUs)}.fbx";
                if (AssetDatabase.LoadAssetAtPath<Object>(stripped) != null)
                    return stripped;
            }

            return null;
        }

        // =====================================================================
        //  STEP 3 — MIRROR COMPS
        // =====================================================================

        // Segment constants — sought case-insensitively when routing FDP paths.
        private const string PARTS_SEGMENT = "01_PARTS/";
        private const string COMPS_SEGMENT  = "02_COMPS/";
        private const string LVL_SEGMENT    = "03_LEVEL_MODULES/";

        // Outcome of a mapper call — drives per-run breakdown summary.
        private enum PartMapTier
        {
            // ── Part outcomes (01_PARTS) ──────────────────────────────────────
            NoSegment,       // path has no known segment (prop / other)
            Exact,           // tier-1 exact match (part)
            Fuzzy,           // tier-2 fuzzy match (part)
            NoMatch,         // no candidate (part)
            Ambiguous,       // multiple candidates (part)
            // ── Comp outcomes (02_COMPS) ──────────────────────────────────────
            CompExact,       // tier-1 exact match (comp)
            CompFuzzy,       // tier-2 fuzzy match (comp)
            CompNoMatch,     // no candidate (comp)
            CompAmbiguous,   // multiple candidates (comp)
            // ── LVL module outcomes (03_LEVEL_MODULES) ────────────────────────
            LvlExact,        // tier-1 exact match (LVL)
            LvlFuzzy,        // tier-2 fuzzy match (LVL)
            LvlNoMatch,      // no candidate (LVL) — tagged pending for Pass 2
            LvlAmbiguous,    // multiple candidates (LVL)
            // ── Skip outcomes ─────────────────────────────────────────────────
            PropSkip,        // prop or unknown path — no whitebox equivalent
        }

        // Captures one swap candidate's data from inside LoadPrefabContents scope.
        // All fields are value types or strings so they survive UnloadPrefabContents.
        struct ChildRecord
        {
            public Vector3    LocalPos;
            public Quaternion LocalRot;
            public Vector3    LocalScale;
            public string     Name;
            /// <summary>
            /// Asset path of the FDP 01_PARTS prefab this child instances.
            /// Null when GetCorrespondingObjectFromSource returned nothing.
            /// </summary>
            public string     PartPath;
            public int        SiblingIndex;
        }

        /// <summary>
        /// For every prefab under <c>Assets/Fantastic Dungeon Pack/prefabs/MODULAR/02_COMPS/</c>
        /// builds a whitebox equivalent under <c>Assets/Whitebox/prefabs/COMPS/</c>,
        /// replacing each FDP 01_PARTS child instance with its whitebox equivalent.
        /// Missing whitebox parts get a primitive cube placeholder.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Generate (Step 3: mirror comps)")]
        public static void GenerateComps() => GenerateCompsImpl(dryRun: false);

        /// <summary>
        /// Dry run — logs every decision Step 3 would make without writing any assets.
        /// Use this to verify path mapping before committing to a full run.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Generate (Step 3: dry run)")]
        public static void GenerateCompsDryRun() => GenerateCompsImpl(dryRun: true);

        // Core implementation shared by GenerateComps and GenerateCompsDryRun.
        // dryRun = true: all file writes, deletions, and hierarchy modifications are
        // suppressed; logging still occurs with the same format so you can read the
        // decisions before committing to a real run.
        static void GenerateCompsImpl(bool dryRun)
        {
            string tag = dryRun ? "[Whitebox COMPS DRY RUN]" : "[Whitebox COMPS]";

            // ── Prerequisites ─────────────────────────────────────────────────
            bool wbModularOk = AssetDatabase.IsValidFolder(WB_PREFAB_ROOT)
                && AssetDatabase.FindAssets("t:Prefab", new[] { WB_PREFAB_ROOT }).Length > 0;
            bool fdpCompsOk  = AssetDatabase.IsValidFolder(FDP_COMPS_ROOT)
                && AssetDatabase.FindAssets("t:Prefab", new[] { FDP_COMPS_ROOT }).Length > 0;

            if (!wbModularOk || !fdpCompsOk)
            {
                string missing = (!wbModularOk ? $"\n  • {WB_PREFAB_ROOT} (run Step 2 first)" : "")
                               + (!fdpCompsOk  ? $"\n  • {FDP_COMPS_ROOT} (FDP not found)"    : "");
                EditorUtility.DisplayDialog("Prerequisites not met",
                    $"Required paths are missing or empty:{missing}", "OK");
                return;
            }

            Debug.Log($"{tag} Step 3 {(dryRun ? "dry run" : "start")}.");

            // ── Clean previous output (real run only) ─────────────────────────
            if (!dryRun && AssetDatabase.IsValidFolder(WB_COMPS_ROOT))
            {
                AssetDatabase.DeleteAsset(WB_COMPS_ROOT);
                AssetDatabase.Refresh();
            }

            // Load default placeholder material (created by Step 2).
            var defaultMat = AssetDatabase.LoadAssetAtPath<Material>(
                $"{WB_MAT_FOLDER}/{MAT_DEFAULT_NAME}.mat");

            // ── Collect FDP comp prefab paths ─────────────────────────────────
            string dataPath = Application.dataPath.Replace('\\', '/');
            string fsFdpComps = Path.Combine(Application.dataPath,
                "Fantastic Dungeon Pack/prefabs/MODULAR/02_COMPS").Replace('\\', '/');

            var srcPaths = Directory.GetFiles(fsFdpComps, "*.prefab",
                                              SearchOption.AllDirectories)
                .Select(p => "Assets" + p.Replace('\\', '/').Substring(dataPath.Length))
                .OrderBy(p => p)
                .ToList();

            Debug.Log($"{tag} Source inventory: {srcPaths.Count} comp prefabs under 02_COMPS/");

            // ── Generate ──────────────────────────────────────────────────────
            int compCount    = 0;
            int totalSwapped = 0;
            int totalMissing = 0;
            var missingParts = new HashSet<string>(System.StringComparer.Ordinal);

            // Mapping tier breakdown — accumulated across all comps.
            int exactCount   = 0;
            int fuzzyCount   = 0;
            int noMatchCount = 0;
            int ambigCount   = 0;
            var ambigCases   = new List<string>();

            foreach (string srcPath in srcPaths)
            {
                // ── Phase 1: collect swap candidates (inside LoadPrefabContents) ──
                var fdpRoot = PrefabUtility.LoadPrefabContents(srcPath);
                if (fdpRoot == null)
                {
                    Debug.LogError($"{tag} Failed to load {srcPath}");
                    continue;
                }

                var records = new List<ChildRecord>();

                // Walk ALL descendants but only record transforms that are the
                // outermost root of a nested prefab instance.  This filters out:
                //   • the comp root itself (loaded scene root, not a nested instance)
                //   • part-internal sub-objects (colliders, mesh nodes, etc.)
                foreach (Transform t in fdpRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t == fdpRoot.transform) continue;
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;

                    var srcAsset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)
                                       as GameObject;
                    string partPath = srcAsset != null
                        ? AssetDatabase.GetAssetPath(srcAsset)
                        : null;

                    if (t.localScale != Vector3.one)
                    {
                        Debug.LogWarning(
                            $"{tag} Non-identity scale {t.localScale} on '{t.name}' " +
                            $"in {Path.GetFileNameWithoutExtension(srcPath)} — preserved.");
                    }

                    records.Add(new ChildRecord
                    {
                        LocalPos     = t.localPosition,
                        LocalRot     = t.localRotation,
                        LocalScale   = t.localScale,
                        Name         = t.name,
                        PartPath     = partPath,
                        SiblingIndex = t.GetSiblingIndex(),
                    });
                }

                string compName = fdpRoot.name;
                PrefabUtility.UnloadPrefabContents(fdpRoot);

                // ── Phase 2: build whitebox comp (or log dry-run decisions) ──────
                int swapped = 0, missing = 0;
                GameObject newRoot = dryRun ? null : new GameObject(compName);

                foreach (var rec in records.OrderBy(r => r.SiblingIndex))
                {
                    string wbPath    = null;
                    PartMapTier tier = PartMapTier.NoSegment;
                    bool    mapped   = rec.PartPath != null
                        && TryMapFdpPartToWhitebox(rec.PartPath, out wbPath, out tier);

                    // Accumulate tier for breakdown summary (both dry run and real run).
                    switch (tier)
                    {
                        case PartMapTier.Exact:  exactCount++;  break;
                        case PartMapTier.Fuzzy:  fuzzyCount++;  break;
                        case PartMapTier.Ambiguous:
                            ambigCount++;
                            ambigCases.Add(rec.PartPath ?? $"<unknown> in {compName}");
                            break;
                        default:  // NoMatch, NoSegment
                            noMatchCount++;  break;
                    }

                    if (dryRun)
                    {
                        if (mapped)
                        {
                            Debug.Log($"{tag}   WOULD SWAP  {rec.Name}  →  {wbPath}");
                            swapped++;
                        }
                        else
                        {
                            string key = rec.PartPath ?? $"<unknown> in {compName}";
                            Debug.Log($"{tag}   MISSING     {rec.Name}  (source: {key})");
                            missingParts.Add(key);
                            missing++;
                        }
                        continue;
                    }

                    // Real run — build the child GameObject.
                    GameObject child;
                    if (mapped)
                    {
                        var wbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wbPath);
                        if (wbPrefab != null)
                        {
                            child = (GameObject)PrefabUtility.InstantiatePrefab(
                                        wbPrefab, newRoot.transform);
                            swapped++;
                        }
                        else
                        {
                            missingParts.Add(rec.PartPath);
                            missing++;
                            child = CreatePlaceholderCube(newRoot.transform, defaultMat);
                        }
                    }
                    else
                    {
                        string key = rec.PartPath ?? $"<unknown> in {compName}";
                        missingParts.Add(key);
                        missing++;
                        child = CreatePlaceholderCube(newRoot.transform, defaultMat);
                    }

                    child.transform.localPosition = rec.LocalPos;
                    child.transform.localRotation = rec.LocalRot;
                    child.transform.localScale    = rec.LocalScale;
                    child.name                    = rec.Name;
                }

                // ── Phase 3: save whitebox comp (real run only) ───────────────────
                string relPath = srcPath.Substring(FDP_COMPS_ROOT.Length).TrimStart('/');
                string relDir  = Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? "";
                string destDir = string.IsNullOrEmpty(relDir)
                    ? WB_COMPS_ROOT
                    : $"{WB_COMPS_ROOT}/{relDir}";
                string destPath = $"{destDir}/{Path.GetFileName(srcPath)}";

                if (dryRun)
                {
                    Debug.Log($"{tag} {compName}: {records.Count} candidates, " +
                              $"{swapped} would swap, {missing} missing → {destPath}");
                }
                else
                {
                    EnsureDir(destDir);
                    PrefabUtility.SaveAsPrefabAsset(newRoot, destPath);
                    Object.DestroyImmediate(newRoot);
                    Debug.Log($"{tag} {compName}: " +
                              $"{records.Count} candidates, {swapped} swapped, {missing} missing.");
                }

                compCount++;
                totalSwapped += swapped;
                totalMissing += missing;
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // ── Summary ───────────────────────────────────────────────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Whitebox] Step 3 {(dryRun ? "dry run " : "")}complete.");
            sb.AppendLine($"  {(dryRun ? "Would generate" : "Generated")}: {compCount} comp prefabs");
            sb.AppendLine($"  Total child swaps: {totalSwapped}");
            sb.Append(    $"  Missing whitebox equivalents: {totalMissing}");
            if (missingParts.Count > 0)
            {
                sb.AppendLine("\n  Missing source paths (deduplicated):");
                foreach (string p in missingParts.OrderBy(p => p))
                    sb.AppendLine($"    {p}");
            }
            Debug.Log(sb.ToString());

            // ── Mapping breakdown ─────────────────────────────────────────────
            var bsb = new System.Text.StringBuilder();
            bsb.Append(
                $"{tag} Mapping breakdown: " +
                $"{exactCount} exact, {fuzzyCount} fuzzy, " +
                $"{noMatchCount} missing, {ambigCount} ambiguous.");
            if (ambigCount > 0)
            {
                bsb.AppendLine("\n  Ambiguous cases (resolve before real run):");
                foreach (string a in ambigCases)
                    bsb.AppendLine($"    {a}");
            }
            Debug.Log(bsb.ToString());
        }

        // =====================================================================
        //  DIAGNOSE STEP 3 — READ-ONLY HIERARCHY INTROSPECTION
        // =====================================================================

        /// <summary>
        /// Loads one FDP comp prefab and logs full PrefabUtility state for every
        /// Transform in its hierarchy.  Read-only — no assets written.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Diagnose Step 3 (log only, no output)")]
        public static void DiagnoseStep3()
        {
            const string preferredPath =
                "Assets/Fantastic Dungeon Pack/prefabs/MODULAR/02_COMPS/Wall/OneSided/" +
                "COMP_Wall_01_O_angle_1_large.prefab";

            string compPath = null;
            if (AssetDatabase.LoadAssetAtPath<Object>(preferredPath) != null)
                compPath = preferredPath;
            else
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { FDP_COMPS_ROOT });
                if (guids.Length > 0)
                    compPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            if (compPath == null)
            {
                Debug.LogError(
                    $"[DIAG] No comp prefab found under {FDP_COMPS_ROOT}. " +
                    "Is the Fantastic Dungeon Pack installed?");
                return;
            }

            Debug.Log($"[DIAG] ===== Diagnosing {compPath} =====");

            var root = PrefabUtility.LoadPrefabContents(compPath);
            if (root == null)
            {
                Debug.LogError($"[DIAG] LoadPrefabContents failed for {compPath}");
                return;
            }

            DiagLogTransform(root.transform);

            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[DIAG] ===== End =====");
        }

        // Recursively logs PrefabUtility diagnostics for a transform and all descendants.
        static void DiagLogTransform(Transform t)
        {
            var go = t.gameObject;

            bool   isInst        = PrefabUtility.IsPartOfPrefabInstance(go);
            bool   isAnyPrefab   = PrefabUtility.IsPartOfAnyPrefab(go);
            bool   isOuterRoot   = PrefabUtility.IsOutermostPrefabInstanceRoot(go);
            var    outerRoot     = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            string outerRootName = outerRoot != null ? outerRoot.name : "null";

            // GetCorrespondingObjectFromSource on this GO.
            var    corrThis     = PrefabUtility.GetCorrespondingObjectFromSource(go) as GameObject;
            string corrThisName = corrThis != null ? corrThis.name : "null";
            string corrThisPath = corrThis != null
                ? AssetDatabase.GetAssetPath(corrThis) : "(no asset)";

            // GetCorrespondingObjectFromSource on GetOutermostPrefabInstanceRoot result.
            string corrOuterName = "null";
            string corrOuterPath = "(no asset)";
            if (outerRoot != null)
            {
                var corrOuter    = PrefabUtility.GetCorrespondingObjectFromSource(outerRoot)
                                       as GameObject;
                corrOuterName    = corrOuter != null ? corrOuter.name : "null";
                corrOuterPath    = corrOuter != null
                    ? AssetDatabase.GetAssetPath(corrOuter) : "(no asset)";
            }

            // Component list.
            var comps = go.GetComponents<Component>();
            string compNames = comps.Length > 0
                ? string.Join(", ", System.Array.ConvertAll(
                      comps, c => c != null ? c.GetType().Name : "(null)"))
                : "(none)";

            Debug.Log(
                $"[DIAG] Transform: {DiagHierarchyPath(t)}\n" +
                $"  IsPartOfPrefabInstance:         {isInst}\n" +
                $"  IsPartOfAnyPrefab:               {isAnyPrefab}\n" +
                $"  IsOutermostPrefabInstanceRoot:   {isOuterRoot}\n" +
                $"  GetOutermostPrefabInstanceRoot:  {outerRootName}\n" +
                $"  GetCorrespondingObjectFromSource (on this GO):\n" +
                $"    result:            {corrThisName}\n" +
                $"    result asset path: {corrThisPath}\n" +
                $"  GetCorrespondingObjectFromSource (on outermost root):\n" +
                $"    result:            {corrOuterName}\n" +
                $"    result asset path: {corrOuterPath}\n" +
                $"  Components on this transform:    {compNames}\n" +
                $"  Child count:                      {t.childCount}");

            foreach (Transform child in t)
                DiagLogTransform(child);
        }

        // Builds a slash-delimited path from the prefab root down to t.
        static string DiagHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  STEP 3 HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Two-tier lookup mapping an FDP 01_PARTS prefab path to its whitebox equivalent.
        /// <para><b>Tier 1</b> — exact: checks <c>{WB_PREFAB_ROOT}/{rest}</c> verbatim.</para>
        /// <para><b>Tier 2</b> — fuzzy: scans the expected whitebox subfolder for a prefab
        /// whose normalized stem equals the FDP filename's normalized stem.
        /// Normalization: lowercase → strip leading <c>p_</c> → strip leading <c>mod_</c> →
        /// strip trailing <c> (N)</c> → trim.</para>
        /// Logs the outcome on every call. Returns false when the segment is absent, no
        /// candidate matches, or the match is ambiguous.
        /// </summary>
        static bool TryMapFdpPartToWhitebox(string fdpPath, out string whiteboxPath,
                                             out PartMapTier tier)
        {
            whiteboxPath = null;
            tier         = PartMapTier.NoSegment;

            string norm = fdpPath.Replace('\\', '/');
            int    idx  = norm.IndexOf(PARTS_SEGMENT, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                Debug.Log($"[Whitebox COMPS] Path does not contain 01_PARTS segment: {fdpPath}");
                return false;
            }

            string rest     = norm.Substring(idx + PARTS_SEGMENT.Length);
            string restDir  = Path.GetDirectoryName(rest)?.Replace('\\', '/') ?? "";
            string fileName = Path.GetFileName(rest);   // e.g. P_MOD_Wall_01_O_angle_large.prefab

            string expectedFolder = string.IsNullOrEmpty(restDir)
                ? WB_PREFAB_ROOT
                : $"{WB_PREFAB_ROOT}/{restDir}";
            string exactPath = $"{expectedFolder}/{fileName}";

            // ── Tier 1: exact filename match ──────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<GameObject>(exactPath) != null)
            {
                whiteboxPath = exactPath;
                tier         = PartMapTier.Exact;
                Debug.Log($"[Whitebox COMPS] Exact match: {exactPath}");
                return true;
            }

            // ── Tier 2: normalized-stem fuzzy match ───────────────────────────
            string dataPath       = Application.dataPath.Replace('\\', '/');
            string wbFolderFsPath = dataPath + "/" + expectedFolder.Substring("Assets/".Length);
            string fdpShort       = Path.GetFileNameWithoutExtension(fileName);

            if (!Directory.Exists(wbFolderFsPath))
            {
                tier = PartMapTier.NoMatch;
                Debug.Log($"[Whitebox COMPS] No match for {fdpShort} in {expectedFolder} " +
                          "(folder does not exist)");
                return false;
            }

            string fdpStem = NormalizePartStem(fdpShort);
            var matches = new List<string>();

            foreach (string cand in Directory.GetFiles(wbFolderFsPath, "*.prefab",
                                                        SearchOption.TopDirectoryOnly))
            {
                string candNorm = cand.Replace('\\', '/');
                string candStem = NormalizePartStem(
                    Path.GetFileNameWithoutExtension(candNorm));
                if (string.Equals(candStem, fdpStem, System.StringComparison.Ordinal))
                    matches.Add("Assets" + candNorm.Substring(dataPath.Length));
            }

            if (matches.Count == 1)
            {
                whiteboxPath = matches[0];
                tier         = PartMapTier.Fuzzy;
                Debug.Log($"[Whitebox COMPS] Fuzzy match: {fdpShort} → " +
                          Path.GetFileName(matches[0]));
                return true;
            }

            if (matches.Count == 0)
            {
                tier = PartMapTier.NoMatch;
                Debug.Log($"[Whitebox COMPS] No match for {fdpShort} in {expectedFolder}");
                return false;
            }

            // Two or more candidates — ambiguous, do not guess.
            tier = PartMapTier.Ambiguous;
            string list = string.Join(", ", matches.Select(m => Path.GetFileName(m)));
            Debug.Log($"[Whitebox COMPS] AMBIGUOUS match for {fdpShort} in {expectedFolder}: " +
                      $"candidates = [{list}]");
            return false;
        }

        // Normalizes a prefab stem for fuzzy matching:
        //   lowercase → strip leading p_ → strip leading mod_ → strip trailing (N) → trim.
        // Examples:
        //   P_MOD_Column_01_base   → column_01_base
        //   MOD_Wall_01_O_straight → wall_01_o_straight
        //   MOD_Column_01_base (2) → column_01_base
        static string NormalizePartStem(string stem)
        {
            string s = stem.ToLowerInvariant();
            if (s.StartsWith("p_"))    s = s.Substring(2);
            if (s.StartsWith("mod_"))  s = s.Substring(4);
            if (s.StartsWith("comp_")) s = s.Substring(5);
            if (s.StartsWith("lvl_"))  s = s.Substring(4);
            // Strip trailing Unity duplicate suffix e.g. " (2)".
            int parenIdx = s.IndexOf(" (");
            if (parenIdx >= 0) s = s.Substring(0, parenIdx);
            return s.Trim();
        }

        // Creates a unit cube placeholder in the whitebox comp hierarchy.
        // Removes the auto-added BoxCollider (components are out of scope here).
        static GameObject CreatePlaceholderCube(Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            if (mat != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            return go;
        }

        // =====================================================================
        //  STEP 4 — MIRROR LEVEL MODULES
        // =====================================================================

        /// <summary>
        /// Mirrors FDP level module prefabs from
        /// <c>Assets/Fantastic Dungeon Pack/prefabs/MODULAR/03_LEVEL_MODULES/</c>
        /// into <c>Assets/Whitebox/prefabs/LEVEL_MODULES/</c>, swapping each
        /// referenced child (part or comp) for its whitebox equivalent.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Generate (Step 4: mirror LVL modules)")]
        public static void GenerateLvlModules() => GenerateLvlModulesImpl(dryRun: false);

        /// <summary>
        /// Dry run — logs every decision Step 4 would make without writing any assets.
        /// Review mapping breakdown and fix ambiguities before the real run.
        /// </summary>
        [MenuItem("LevelGen/Whitebox [Complete]/Generate (Step 4: dry run)")]
        public static void GenerateLvlModulesDryRun() => GenerateLvlModulesImpl(dryRun: true);

        // Core implementation for Step 4 — two-pass generation.
        // Pass 1: generate whitebox LVLs; cross-LVL refs that can't resolve yet
        //          get a cube placeholder with a WhiteboxPendingLvlRef marker.
        // Pass 2: re-open each generated LVL and swap pending placeholders for
        //          real whitebox LVLs (all of which now exist on disk).
        static void GenerateLvlModulesImpl(bool dryRun)
        {
            string tag = dryRun ? "[Whitebox LVL DRY RUN]" : "[Whitebox LVL]";

            // ── Prerequisites ─────────────────────────────────────────────────
            bool partsOk = AssetDatabase.IsValidFolder(WB_PREFAB_ROOT)
                && AssetDatabase.FindAssets("t:Prefab", new[] { WB_PREFAB_ROOT }).Length > 0;
            bool compsOk = AssetDatabase.IsValidFolder(WB_COMPS_ROOT)
                && AssetDatabase.FindAssets("t:Prefab", new[] { WB_COMPS_ROOT }).Length > 0;
            bool srcOk   = AssetDatabase.IsValidFolder(FDP_LVL_ROOT)
                && AssetDatabase.FindAssets("t:Prefab", new[] { FDP_LVL_ROOT }).Length > 0;

            if (!partsOk || !compsOk || !srcOk)
            {
                string missing = (!partsOk ? $"\n  • {WB_PREFAB_ROOT} (run Step 2 first)"     : "")
                               + (!compsOk ? $"\n  • {WB_COMPS_ROOT} (run Step 3 first)"      : "")
                               + (!srcOk   ? $"\n  • {FDP_LVL_ROOT} (FDP not found or empty)" : "");
                EditorUtility.DisplayDialog("Prerequisites not met",
                    $"Required paths are missing or empty:{missing}", "OK");
                return;
            }

            Debug.Log($"{tag} Step 4 {(dryRun ? "dry run" : "start")}.");

            // ── Clean previous output (real run only) ─────────────────────────
            if (!dryRun && AssetDatabase.IsValidFolder(WB_LVL_ROOT))
            {
                AssetDatabase.DeleteAsset(WB_LVL_ROOT);
                AssetDatabase.Refresh();
            }

            var defaultMat = AssetDatabase.LoadAssetAtPath<Material>(
                $"{WB_MAT_FOLDER}/{MAT_DEFAULT_NAME}.mat");

            // ── Collect FDP LVL prefab paths ──────────────────────────────────
            string dataPath  = Application.dataPath.Replace('\\', '/');
            string fsFdpLvls = Path.Combine(Application.dataPath,
                "Fantastic Dungeon Pack/prefabs/MODULAR/03_LEVEL_MODULES").Replace('\\', '/');

            var srcPaths = Directory.GetFiles(fsFdpLvls, "*.prefab",
                                              SearchOption.AllDirectories)
                .Select(p => "Assets" + p.Replace('\\', '/').Substring(dataPath.Length))
                .OrderBy(p => p)
                .ToList();

            Debug.Log($"{tag} Source inventory: {srcPaths.Count} LVL prefabs under 03_LEVEL_MODULES/");

            // ── Breakdown counters ────────────────────────────────────────────
            int partExact = 0, partFuzzy = 0, partNoMatch = 0, partAmbig = 0;
            int compExact = 0, compFuzzy = 0, compNoMatch = 0, compAmbig = 0;
            int lvlExact  = 0, lvlFuzzy  = 0, lvlNoMatch  = 0, lvlAmbig  = 0;
            int propSkipped = 0;
            var partAmbigCases = new List<string>();
            var compAmbigCases = new List<string>();
            var lvlAmbigCases  = new List<string>();
            var missingParts   = new HashSet<string>(System.StringComparer.Ordinal);

            int lvlCount     = 0;
            int totalSwapped = 0;
            int totalMissing = 0;
            int pass1Pending = 0;   // LVL cross-refs tagged for Pass 2

            // Dry run: accumulate what would be generated (for Pass 2 simulation).
            var dryPendingRefs   = new List<string>();
            var dryWouldGenerate = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

            // Pass 1 dest paths — used to drive Pass 2 file iteration.
            var pass1Dests = new List<string>();

            // =====================================================================
            //  PASS 1 — generate whitebox LVLs (pending placeholders where needed)
            // =====================================================================
            Debug.Log($"{tag} Pass 1: generating {srcPaths.Count} LVL prefabs.");

            foreach (string srcPath in srcPaths)
            {
                // Compute dest path early (needed for dry-run set and per-LVL log).
                string relPath = srcPath.Substring(FDP_LVL_ROOT.Length).TrimStart('/');
                string relDir  = Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? "";
                string destDir = string.IsNullOrEmpty(relDir)
                    ? WB_LVL_ROOT
                    : $"{WB_LVL_ROOT}/{relDir}";
                string destPath = $"{destDir}/{Path.GetFileName(srcPath)}";
                pass1Dests.Add(destPath);
                if (dryRun) dryWouldGenerate.Add(destPath);

                // ── Phase 1: collect swap candidates ──────────────────────────
                var fdpRoot = PrefabUtility.LoadPrefabContents(srcPath);
                if (fdpRoot == null)
                {
                    Debug.LogError($"{tag} Failed to load {srcPath}");
                    continue;
                }

                var records = new List<ChildRecord>();
                foreach (Transform t in fdpRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (t == fdpRoot.transform) continue;
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;

                    var srcAsset = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)
                                       as GameObject;
                    string partPath = srcAsset != null
                        ? AssetDatabase.GetAssetPath(srcAsset)
                        : null;

                    if (t.localScale != Vector3.one)
                    {
                        Debug.LogWarning(
                            $"{tag} Non-identity scale {t.localScale} on '{t.name}' " +
                            $"in {Path.GetFileNameWithoutExtension(srcPath)} — preserved.");
                    }

                    records.Add(new ChildRecord
                    {
                        LocalPos     = t.localPosition,
                        LocalRot     = t.localRotation,
                        LocalScale   = t.localScale,
                        Name         = t.name,
                        PartPath     = partPath,
                        SiblingIndex = t.GetSiblingIndex(),
                    });
                }

                string lvlName = fdpRoot.name;
                PrefabUtility.UnloadPrefabContents(fdpRoot);

                if (records.Count == 0)
                    Debug.Log($"{tag} {lvlName} has no prefab instance children — copied structure only.");

                // ── Phase 2: build (or log dry-run decisions) ──────────────────
                int swapped = 0, missing = 0, pendingLvl = 0;
                int swappedParts = 0, swappedComps = 0;
                GameObject newRoot = dryRun ? null : new GameObject(lvlName);

                foreach (var rec in records.OrderBy(r => r.SiblingIndex))
                {
                    string wbPath    = null;
                    PartMapTier tier = PartMapTier.PropSkip;
                    bool    mapped   = rec.PartPath != null
                        && TryMapFdpReferenceToWhitebox(rec.PartPath, out wbPath, out tier);

                    // Accumulate breakdown counters.
                    switch (tier)
                    {
                        case PartMapTier.Exact:
                            partExact++;   break;
                        case PartMapTier.Fuzzy:
                            partFuzzy++;   break;
                        case PartMapTier.NoMatch:
                            partNoMatch++; break;
                        case PartMapTier.Ambiguous:
                            partAmbig++;
                            partAmbigCases.Add(rec.PartPath ?? rec.Name);
                            break;
                        case PartMapTier.CompExact:
                            compExact++;   break;
                        case PartMapTier.CompFuzzy:
                            compFuzzy++;   break;
                        case PartMapTier.CompNoMatch:
                            compNoMatch++; break;
                        case PartMapTier.CompAmbiguous:
                            compAmbig++;
                            compAmbigCases.Add(rec.PartPath ?? rec.Name);
                            break;
                        case PartMapTier.LvlExact:
                            lvlExact++;    break;
                        case PartMapTier.LvlFuzzy:
                            lvlFuzzy++;    break;
                        case PartMapTier.LvlNoMatch:
                            lvlNoMatch++;  break;
                        case PartMapTier.LvlAmbiguous:
                            lvlAmbig++;
                            lvlAmbigCases.Add(rec.PartPath ?? rec.Name);
                            break;
                        default: propSkipped++; break;  // PropSkip, NoSegment
                    }

                    // LvlNoMatch → pending placeholder tagged for Pass 2.
                    // LvlAmbiguous → genuine ambiguity, plain cube (not pending).
                    bool isLvlPending   = !mapped && tier == PartMapTier.LvlNoMatch;
                    bool isExpectedSkip = tier == PartMapTier.PropSkip
                                      || tier == PartMapTier.NoSegment;

                    if (dryRun)
                    {
                        if (mapped)
                        {
                            bool isComp = tier == PartMapTier.CompExact
                                       || tier == PartMapTier.CompFuzzy;
                            bool isLvl  = tier == PartMapTier.LvlExact
                                       || tier == PartMapTier.LvlFuzzy;
                            if (isComp)      swappedComps++;
                            else if (!isLvl) swappedParts++;
                            swapped++;
                            Debug.Log($"{tag}   WOULD SWAP  {rec.Name}  →  {wbPath}");
                        }
                        else if (isLvlPending)
                        {
                            pendingLvl++;
                            pass1Pending++;
                            dryPendingRefs.Add(rec.PartPath);
                            Debug.Log($"{tag}   [DRY] Would tag pending LVL ref for Pass 2: {rec.PartPath}");
                        }
                        else if (!isExpectedSkip)
                        {
                            string key = rec.PartPath ?? $"<unknown> in {lvlName}";
                            missingParts.Add(key);
                            missing++;
                            Debug.Log($"{tag}   MISSING     {rec.Name}  (source: {key})");
                        }
                        continue;
                    }

                    // Real run — build child GO.
                    GameObject child;
                    if (mapped)
                    {
                        var wbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wbPath);
                        if (wbPrefab != null)
                        {
                            child = (GameObject)PrefabUtility.InstantiatePrefab(
                                        wbPrefab, newRoot.transform);
                            bool isComp = tier == PartMapTier.CompExact
                                       || tier == PartMapTier.CompFuzzy;
                            bool isLvl  = tier == PartMapTier.LvlExact
                                       || tier == PartMapTier.LvlFuzzy;
                            if (isComp)      swappedComps++;
                            else if (!isLvl) swappedParts++;
                            swapped++;
                        }
                        else
                        {
                            missingParts.Add(rec.PartPath);
                            missing++;
                            child = CreatePlaceholderCube(newRoot.transform, defaultMat);
                            Debug.Log(
                                $"{tag} MISSING {rec.PartPath} — using primitive cube placeholder");
                        }
                    }
                    else if (isLvlPending)
                    {
                        // Target LVL not generated yet — tag placeholder for Pass 2.
                        child = CreatePlaceholderCube(newRoot.transform, defaultMat);
                        var pendingComp = child.AddComponent<WhiteboxPendingLvlRef>();
                        pendingComp.fdpSourcePath = rec.PartPath;
                        pendingLvl++;
                        pass1Pending++;
                        Debug.Log($"{tag} PENDING LVL {rec.PartPath} — tagged for Pass 2.");
                    }
                    else
                    {
                        string key = rec.PartPath ?? $"<unknown> in {lvlName}";
                        if (!isExpectedSkip)
                        {
                            missingParts.Add(key);
                            missing++;
                        }
                        child = CreatePlaceholderCube(newRoot.transform, defaultMat);
                        Debug.Log($"{tag} MISSING {key} — using primitive cube placeholder");
                    }

                    child.transform.localPosition = rec.LocalPos;
                    child.transform.localRotation = rec.LocalRot;
                    child.transform.localScale    = rec.LocalScale;
                    child.name                    = rec.Name;
                }

                // ── Phase 3: save (real run only) ─────────────────────────────
                if (dryRun)
                {
                    Debug.Log($"{tag} {lvlName}: {records.Count} candidates, " +
                              $"{swapped} would swap ({swappedParts} parts, {swappedComps} comps), " +
                              $"{pendingLvl} pending LVL, {missing} missing → {destPath}");
                }
                else
                {
                    EnsureDir(destDir);
                    PrefabUtility.SaveAsPrefabAsset(newRoot, destPath);
                    Object.DestroyImmediate(newRoot);
                    Debug.Log($"{tag} {lvlName}: {records.Count} candidates, " +
                              $"{swapped} swapped ({swappedParts} parts, {swappedComps} comps), " +
                              $"{pendingLvl} pending LVL, {missing} missing.");
                }

                lvlCount++;
                totalSwapped += swapped;
                totalMissing += missing;
            }

            Debug.Log($"{tag} Pass 1 complete. Generated {lvlCount} LVLs, " +
                      $"tagged {pass1Pending} pending LVL references for Pass 2.");

            // =====================================================================
            //  PASS 2 — resolve pending LVL cross-references
            // =====================================================================

            int pass2Resolved = 0, pass2Unresolved = 0;

            if (dryRun)
            {
                // Simulate Pass 2: for each pending ref, check whether its target WB
                // LVL path would exist after Pass 1 (present in dryWouldGenerate).
                foreach (string pendingFdpPath in dryPendingRefs)
                {
                    string norm2 = (pendingFdpPath ?? "").Replace('\\', '/');
                    int    idx2  = norm2.IndexOf(LVL_SEGMENT,
                                                 System.StringComparison.OrdinalIgnoreCase);
                    bool wouldResolve = false;

                    if (idx2 >= 0)
                    {
                        string rest2     = norm2.Substring(idx2 + LVL_SEGMENT.Length);
                        string restDir2  = Path.GetDirectoryName(rest2)?.Replace('\\', '/') ?? "";
                        string fileName2 = Path.GetFileName(rest2);
                        string tgtFolder = string.IsNullOrEmpty(restDir2)
                            ? WB_LVL_ROOT : $"{WB_LVL_ROOT}/{restDir2}";
                        string tgtPath   = $"{tgtFolder}/{fileName2}";

                        if (dryWouldGenerate.Contains(tgtPath))
                        {
                            wouldResolve = true;
                            Debug.Log($"{tag}   [DRY] Would resolve on Pass 2: {pendingFdpPath}");
                        }
                        else
                        {
                            // Try fuzzy match within would-generate set.
                            string fdpStem2 = NormalizePartStem(
                                Path.GetFileNameWithoutExtension(fileName2));
                            string fuzzyHit = dryWouldGenerate.FirstOrDefault(p =>
                                string.Equals(
                                    NormalizePartStem(Path.GetFileNameWithoutExtension(p)),
                                    fdpStem2,
                                    System.StringComparison.Ordinal));
                            if (fuzzyHit != null)
                            {
                                wouldResolve = true;
                                Debug.Log($"{tag}   [DRY] Would resolve on Pass 2 (fuzzy): " +
                                          $"{pendingFdpPath} → {fuzzyHit}");
                            }
                        }
                    }

                    if (wouldResolve) pass2Resolved++;
                    else
                    {
                        pass2Unresolved++;
                        Debug.Log($"{tag}   [DRY] Would remain unresolved: {pendingFdpPath}");
                    }
                }

                Debug.Log($"{tag} Pass 2 (simulated): would resolve {pass2Resolved} / " +
                          $"{dryPendingRefs.Count}. {pass2Unresolved} would remain unresolved.");
            }
            else
            {
                // Real Pass 2 — refresh DB so newly-generated LVLs are visible, then
                // re-open each whitebox LVL and swap WhiteboxPendingLvlRef placeholders.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"{tag} Pass 2: resolving pending cross-references " +
                          $"across {pass1Dests.Count} generated LVLs.");

                foreach (string wbLvlPath in pass1Dests)
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(wbLvlPath) == null)
                        continue;   // LVL generation may have failed for this one

                    var wbRoot = PrefabUtility.LoadPrefabContents(wbLvlPath);
                    if (wbRoot == null) continue;

                    var pendingComps = wbRoot
                        .GetComponentsInChildren<WhiteboxPendingLvlRef>(true)
                        .ToList();

                    if (pendingComps.Count == 0)
                    {
                        PrefabUtility.UnloadPrefabContents(wbRoot);
                        continue;
                    }

                    bool modified = false;

                    foreach (var pending in pendingComps)
                    {
                        string      fdpRef = pending.fdpSourcePath;
                        string      wbPath2 = null;
                        PartMapTier tier2   = PartMapTier.LvlNoMatch;
                        bool resolved = !string.IsNullOrEmpty(fdpRef)
                            && TryMapFdpLvlToWhitebox(fdpRef, out wbPath2, out tier2);

                        // Capture transform data before destroying.
                        Transform  parent  = pending.transform.parent;
                        int        sibIdx  = pending.transform.GetSiblingIndex();
                        Vector3    pos     = pending.transform.localPosition;
                        Quaternion rot     = pending.transform.localRotation;
                        Vector3    scale   = pending.transform.localScale;
                        string     chName  = pending.gameObject.name;

                        if (resolved)
                        {
                            var wbLvlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wbPath2);
                            if (wbLvlPrefab != null)
                            {
                                Object.DestroyImmediate(pending.gameObject);
                                var repl = (GameObject)PrefabUtility.InstantiatePrefab(
                                               wbLvlPrefab,
                                               parent != null ? parent : wbRoot.transform);
                                repl.transform.localPosition = pos;
                                repl.transform.localRotation = rot;
                                repl.transform.localScale    = scale;
                                repl.transform.SetSiblingIndex(sibIdx);
                                repl.name = chName;
                                pass2Resolved++;
                                modified = true;
                                Debug.Log($"{tag} Pass 2 RESOLVED {fdpRef} → {wbPath2} " +
                                          $"in {wbLvlPath}");
                            }
                            else
                            {
                                // Path resolved but prefab not loadable — strip marker.
                                Object.DestroyImmediate(pending);
                                pass2Unresolved++;
                                modified = true;
                                Debug.LogWarning(
                                    $"[Whitebox LVL Pass 2] UNRESOLVED {fdpRef} in {wbLvlPath} " +
                                    "(path resolved but asset not loadable)");
                            }
                        }
                        else
                        {
                            // Still unresolved — strip marker so it doesn't persist.
                            Object.DestroyImmediate(pending);
                            pass2Unresolved++;
                            modified = true;
                            Debug.LogWarning(
                                $"[Whitebox LVL Pass 2] UNRESOLVED {fdpRef} in {wbLvlPath}");
                        }
                    }

                    if (modified)
                        PrefabUtility.SaveAsPrefabAsset(wbRoot, wbLvlPath);

                    PrefabUtility.UnloadPrefabContents(wbRoot);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"{tag} Pass 2 complete. Resolved {pass2Resolved} / {pass1Pending} " +
                          $"pending references. {pass2Unresolved} unresolved.");
            }

            // ── Summary ───────────────────────────────────────────────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Whitebox] Step 4 {(dryRun ? "dry run " : "")}complete.");
            sb.AppendLine($"  {(dryRun ? "Would generate" : "Generated")}: {lvlCount} LVL prefabs in {WB_LVL_ROOT}");
            sb.AppendLine($"  Total child swaps: {totalSwapped}");
            sb.Append(    $"  Total missing: {totalMissing}");
            if (missingParts.Count > 0)
            {
                sb.AppendLine("\n  Missing source paths (deduplicated):");
                foreach (string p in missingParts.OrderBy(p => p))
                    sb.AppendLine($"    {p}");
            }
            Debug.Log(sb.ToString());

            // ── Mapping breakdown ─────────────────────────────────────────────
            var bsb = new System.Text.StringBuilder();
            bsb.AppendLine($"{tag} Mapping breakdown:");
            bsb.AppendLine(
                $"  Parts: {partExact} exact, {partFuzzy} fuzzy, " +
                $"{partNoMatch} missing, {partAmbig} ambiguous");
            bsb.AppendLine(
                $"  Comps: {compExact} exact, {compFuzzy} fuzzy, " +
                $"{compNoMatch} missing, {compAmbig} ambiguous");
            bsb.AppendLine(
                $"  LVLs:  {lvlExact} exact, {lvlFuzzy} fuzzy, " +
                $"{lvlNoMatch} missing, {lvlAmbig} ambiguous");
            bsb.AppendLine($"  Props skipped: {propSkipped}");
            bsb.AppendLine($"  Pass 1 pending refs: {pass1Pending}");
            if (dryRun)
            {
                bsb.AppendLine($"  Pass 2 would resolve: {pass2Resolved} / {pass1Pending}");
                bsb.Append(    $"  Still unresolved after Pass 2: {pass2Unresolved}");
            }
            else
            {
                bsb.Append(    $"  Pass 2 resolved: {pass2Resolved} / {pass1Pending}, " +
                               $"unresolved: {pass2Unresolved}");
            }
            if (partAmbig > 0)
            {
                bsb.AppendLine("\n  Ambiguous part cases:");
                foreach (string a in partAmbigCases)
                    bsb.AppendLine($"    {a}");
            }
            if (compAmbig > 0)
            {
                bsb.AppendLine("\n  Ambiguous comp cases:");
                foreach (string a in compAmbigCases)
                    bsb.AppendLine($"    {a}");
            }
            if (lvlAmbig > 0)
            {
                bsb.AppendLine("\n  Ambiguous LVL cases:");
                foreach (string a in lvlAmbigCases)
                    bsb.AppendLine($"    {a}");
            }
            Debug.Log(bsb.ToString());
        }

        // ─────────────────────────────────────────────────────────────────────
        //  STEP 4 HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dispatches to the correct whitebox mapper based on which FDP folder
        /// segment <paramref name="fdpPath"/> contains.
        /// <list type="bullet">
        /// <item>01_PARTS/ → <see cref="TryMapFdpPartToWhitebox"/></item>
        /// <item>02_COMPS/ → <see cref="TryMapFdpCompToWhitebox"/></item>
        /// <item>03_LEVEL_MODULES/ → <see cref="TryMapFdpLvlToWhitebox"/> (two-tier)</item>
        /// <item>no known segment → <see cref="PartMapTier.PropSkip"/></item>
        /// </list>
        /// </summary>
        static bool TryMapFdpReferenceToWhitebox(string fdpPath, out string whiteboxPath,
                                                  out PartMapTier tier)
        {
            whiteboxPath = null;
            tier         = PartMapTier.PropSkip;

            string norm = fdpPath.Replace('\\', '/');

            if (norm.IndexOf(PARTS_SEGMENT, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return TryMapFdpPartToWhitebox(fdpPath, out whiteboxPath, out tier);

            if (norm.IndexOf(COMPS_SEGMENT, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return TryMapFdpCompToWhitebox(fdpPath, out whiteboxPath, out tier);

            if (norm.IndexOf(LVL_SEGMENT, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return TryMapFdpLvlToWhitebox(fdpPath, out whiteboxPath, out tier);

            Debug.Log($"[Whitebox LVL] Path does not contain a known segment: {fdpPath}");
            return false;
        }

        /// <summary>
        /// Two-tier lookup mapping an FDP 02_COMPS prefab path to its whitebox
        /// equivalent under <c>Assets/Whitebox/prefabs/COMPS/</c>.
        /// Tier 1 — exact filename match (Step 3 preserves FDP comp filenames verbatim).
        /// Tier 2 — normalized-stem fuzzy match as fallback.
        /// </summary>
        static bool TryMapFdpCompToWhitebox(string fdpPath, out string whiteboxPath,
                                             out PartMapTier tier)
        {
            whiteboxPath = null;
            tier         = PartMapTier.CompNoMatch;

            string norm = fdpPath.Replace('\\', '/');
            int    idx  = norm.IndexOf(COMPS_SEGMENT, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                Debug.Log($"[Whitebox LVL] Path does not contain 02_COMPS segment: {fdpPath}");
                return false;
            }

            string rest     = norm.Substring(idx + COMPS_SEGMENT.Length);
            string restDir  = Path.GetDirectoryName(rest)?.Replace('\\', '/') ?? "";
            string fileName = Path.GetFileName(rest);

            string expectedFolder = string.IsNullOrEmpty(restDir)
                ? WB_COMPS_ROOT
                : $"{WB_COMPS_ROOT}/{restDir}";
            string exactPath = $"{expectedFolder}/{fileName}";

            // ── Tier 1: exact filename match ──────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<GameObject>(exactPath) != null)
            {
                whiteboxPath = exactPath;
                tier         = PartMapTier.CompExact;
                Debug.Log($"[Whitebox LVL] Exact match (comp): {exactPath}");
                return true;
            }

            // ── Tier 2: normalized-stem fuzzy match ───────────────────────────
            string dataPath       = Application.dataPath.Replace('\\', '/');
            string wbFolderFsPath = dataPath + "/" + expectedFolder.Substring("Assets/".Length);
            string fdpShort       = Path.GetFileNameWithoutExtension(fileName);

            if (!Directory.Exists(wbFolderFsPath))
            {
                tier = PartMapTier.CompNoMatch;
                Debug.Log($"[Whitebox LVL] No match (comp) for {fdpShort} in {expectedFolder} " +
                          "(folder does not exist)");
                return false;
            }

            string fdpStem = NormalizePartStem(fdpShort);
            var    matches = new List<string>();

            foreach (string cand in Directory.GetFiles(wbFolderFsPath, "*.prefab",
                                                        SearchOption.TopDirectoryOnly))
            {
                string candNorm = cand.Replace('\\', '/');
                string candStem = NormalizePartStem(
                    Path.GetFileNameWithoutExtension(candNorm));
                if (string.Equals(candStem, fdpStem, System.StringComparison.Ordinal))
                    matches.Add("Assets" + candNorm.Substring(dataPath.Length));
            }

            if (matches.Count == 1)
            {
                whiteboxPath = matches[0];
                tier         = PartMapTier.CompFuzzy;
                Debug.Log($"[Whitebox LVL] Fuzzy match (comp): {fdpShort} → " +
                          Path.GetFileName(matches[0]));
                return true;
            }

            if (matches.Count == 0)
            {
                tier = PartMapTier.CompNoMatch;
                Debug.Log($"[Whitebox LVL] No match (comp) for {fdpShort} in {expectedFolder}");
                return false;
            }

            tier = PartMapTier.CompAmbiguous;
            string list = string.Join(", ", matches.Select(m => Path.GetFileName(m)));
            Debug.Log($"[Whitebox LVL] AMBIGUOUS match (comp) for {fdpShort} in {expectedFolder}: " +
                      $"candidates = [{list}]");
            return false;
        }

        /// <summary>
        /// Two-tier lookup mapping an FDP 03_LEVEL_MODULES prefab path to its whitebox
        /// equivalent under <c>Assets/Whitebox/prefabs/LEVEL_MODULES/</c>.
        /// Tier 1 — exact filename match (Step 4 preserves FDP LVL filenames verbatim).
        /// Tier 2 — normalized-stem fuzzy match as fallback.
        /// Called by <see cref="TryMapFdpReferenceToWhitebox"/> and by Pass 2 directly.
        /// </summary>
        static bool TryMapFdpLvlToWhitebox(string fdpPath, out string whiteboxPath,
                                            out PartMapTier tier)
        {
            whiteboxPath = null;
            tier         = PartMapTier.LvlNoMatch;

            string norm = fdpPath.Replace('\\', '/');
            int    idx  = norm.IndexOf(LVL_SEGMENT, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                Debug.Log($"[Whitebox LVL] Path does not contain 03_LEVEL_MODULES segment: {fdpPath}");
                return false;
            }

            string rest     = norm.Substring(idx + LVL_SEGMENT.Length);
            string restDir  = Path.GetDirectoryName(rest)?.Replace('\\', '/') ?? "";
            string fileName = Path.GetFileName(rest);

            string expectedFolder = string.IsNullOrEmpty(restDir)
                ? WB_LVL_ROOT
                : $"{WB_LVL_ROOT}/{restDir}";
            string exactPath = $"{expectedFolder}/{fileName}";

            // ── Tier 1: exact filename match ──────────────────────────────────
            if (AssetDatabase.LoadAssetAtPath<GameObject>(exactPath) != null)
            {
                whiteboxPath = exactPath;
                tier         = PartMapTier.LvlExact;
                Debug.Log($"[Whitebox LVL] Exact match (lvl): {exactPath}");
                return true;
            }

            // ── Tier 2: normalized-stem fuzzy match ───────────────────────────
            string dataPath       = Application.dataPath.Replace('\\', '/');
            string wbFolderFsPath = dataPath + "/" + expectedFolder.Substring("Assets/".Length);
            string fdpShort       = Path.GetFileNameWithoutExtension(fileName);

            if (!Directory.Exists(wbFolderFsPath))
            {
                tier = PartMapTier.LvlNoMatch;
                Debug.Log($"[Whitebox LVL] No match (lvl) for {fdpShort} in {expectedFolder} " +
                          "(folder does not exist)");
                return false;
            }

            string fdpStem = NormalizePartStem(fdpShort);
            var    matches = new List<string>();

            foreach (string cand in Directory.GetFiles(wbFolderFsPath, "*.prefab",
                                                        SearchOption.TopDirectoryOnly))
            {
                string candNorm = cand.Replace('\\', '/');
                string candStem = NormalizePartStem(
                    Path.GetFileNameWithoutExtension(candNorm));
                if (string.Equals(candStem, fdpStem, System.StringComparison.Ordinal))
                    matches.Add("Assets" + candNorm.Substring(dataPath.Length));
            }

            if (matches.Count == 1)
            {
                whiteboxPath = matches[0];
                tier         = PartMapTier.LvlFuzzy;
                Debug.Log($"[Whitebox LVL] Fuzzy match (lvl): {fdpShort} → " +
                          Path.GetFileName(matches[0]));
                return true;
            }

            if (matches.Count == 0)
            {
                tier = PartMapTier.LvlNoMatch;
                Debug.Log($"[Whitebox LVL] No match (lvl) for {fdpShort} in {expectedFolder}");
                return false;
            }

            tier = PartMapTier.LvlAmbiguous;
            string lvlList = string.Join(", ", matches.Select(m => Path.GetFileName(m)));
            Debug.Log($"[Whitebox LVL] AMBIGUOUS match (lvl) for {fdpShort} in {expectedFolder}: " +
                      $"candidates = [{lvlList}]");
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SHARED HELPERS
        // ─────────────────────────────────────────────────────────────────────

        // Converts a 6-digit hex string (no leading #) to a Unity Color (alpha = 1).
        static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
                return c;
            return Color.white;
        }

        /// <summary>
        /// Recursively ensures <paramref name="assetPath"/> (and every ancestor)
        /// exists as a valid AssetDatabase folder.
        /// </summary>
        static void EnsureDir(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "Assets";
            string folder = Path.GetFileName(assetPath);
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    /// <summary>
    /// Editor-only marker placed on cube placeholders during Step 4 Pass 1.
    /// Records the FDP source path of a nested LVL reference that could not be
    /// resolved yet (the target LVL had not been generated at that point in Pass 1).
    /// Pass 2 scans for this component, resolves the reference against the
    /// now-complete <c>Assets/Whitebox/prefabs/LEVEL_MODULES/</c>, and swaps in
    /// the real whitebox LVL prefab.
    /// After Pass 2 completes, no whitebox LVL should retain this component.
    /// </summary>
    public class WhiteboxPendingLvlRef : MonoBehaviour
    {
        [Tooltip("FDP source path of the unresolved LVL reference. " +
                 "Used by Step 4 Pass 2 to find the whitebox LVL equivalent.")]
        public string fdpSourcePath;
    }
}
#endif
