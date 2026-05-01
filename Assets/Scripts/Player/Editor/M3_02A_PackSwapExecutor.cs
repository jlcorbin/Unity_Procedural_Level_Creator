// M3_02A_PackSwapExecutor.cs — One-off pack-swap automation.
//
// Three sequential menu items under LevelGen ▶ Pack Swap ▶ ...
//   Step 1: Delete Duo pack folder
//   Step 2: Import World Bundle .unitypackage + relocate to AssetPacks/
//   Step 3: Verify auto-relink + run all 6 M2-B validators
//
// Run them in order. Each step prints PASS / FAIL and aborts on error.
// After all three pass, the swap is complete on the asset side; the
// player rig (MaleCharacterPBR child of Player_MaleHero.prefab) will
// be in a "missing prefab" state — that's M3-02B's concern.
//
// This file can be safely deleted after the swap completes; it is
// one-off scaffolding.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LevelGen.Player.EditorTools
{
    public static class M3_02A_PackSwapExecutor
    {
        // ── Configuration ───────────────────────────────────────────────
        private const string PackagePath =
            @"C:\Users\Jason\AppData\Roaming\Unity\Asset Store-5.x\Dungeon Mason\3D ModelsEnvironmentsFantasy\RPG Tiny Hero World Bundle PBR.unitypackage";
        private const string DuoPackPath           = "Assets/AssetPacks/RPG Tiny Hero Duo";
        private const string PublisherDefaultPath  = "Assets/RPGTinyHeroWorldBundlePBR";
        private const string AssetPacksFolder      = "Assets/AssetPacks";
        private const string TargetPath            = "Assets/AssetPacks/RPG Tiny Hero World Bundle";
        private const string ControllerPath        = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string OverridePath          = "Assets/Animators/Player/PlayerOverride_MaleHero.overrideController";

        private static readonly string[] HdrpWrappersToDelete = new[]
        {
            "Assets/RPGTinyHeroWorldBundlePBR/HDRP_BuiltIN/HDRP.unitypackage",
            "Assets/RPGTinyHeroWorldBundlePBR/HDRP_BuiltIN/BuiltIn.unitypackage",
        };

        // The 13 currently-wired clip GUIDs (from M3_02A_preswap_baseline.md).
        private static readonly Dictionary<string, string> ExpectedClips = new Dictionary<string, string>
        {
            { "0308cf4e83cf517488b60af58b290fe0", "Idle_Battle_SwordAndShiled" },
            { "4897d9e1e93439744a78d1cebdef17ff", "MoveBWD_Battle_InPlace_SwordAndShield" },
            { "7d4f9e9da55a3bd4f958a63308a522a1", "MoveFWD_Battle_InPlace_SwordAndShield" },
            { "048a541568c52514c9996fea7b37d6e0", "MoveLFT_Battle_InPlace_SwordAndShield" },
            { "5eee3d6dbfbcef04ab20b548575d7b9d", "SprintFWD_Battle_InPlace_SwordAndShield" },
            { "f531fd2d5a6a8a440b5d450e029c4041", "MoveRGT_Battle_InPlace_SwordAndShield" },
            { "db509ad77f9b4f84a8eb1989f589b24c", "Attack01_SwordAndShiled" },
            { "c98546b8d8d3ab046afc8acfe706361f", "GetHit01_SwordAndShield" },
            { "c2b2e4c79d87c3045838cbc5935d8a98", "JumpStart_Normal_InPlace_SwordAndShield" },
            { "8be8f9bf3f16f184fb9719bd233874e6", "JumpAir_Normal_InPlace_SwordAndShield" },
            { "8b662f6fbb996ba429182e54857361d3", "JumpEnd_Normal_InPlace_SwordAndShield" },
            { "8283fadf2c89507469495f30db8680db", "Attack02_SwordAndShiled" },
            { "9a6c3585df66f2e4782635fc7a23494c", "Attack03_SwordAndShiled" },
        };

        private static readonly string[] M2BValidators = new[]
        {
            "LevelGen/Player/Validate Combat Animator (M2-B Step 2)",
            "LevelGen/Player/Validate PlayerCombat Wiring (M2-B Step 3)",
            "LevelGen/Player/Validate Jump Animator (M2-B Step 4)",
            "LevelGen/Player/Validate Jump Runtime (M2-B Step 5)",
            "LevelGen/Player/Validate Combo Animator (M2-B Step 6)",
            "LevelGen/Player/Validate Combo Runtime (M2-B Step 7)",
        };

        // ── Step 1: Delete Duo pack ─────────────────────────────────────

        [MenuItem("LevelGen/Pack Swap/M3-02A · Step 1 (Delete Duo)")]
        public static void Step1_DeleteDuo()
        {
            Debug.Log($"[M3-02A Step 1] Starting. Target: {DuoPackPath}");

            if (!AssetDatabase.IsValidFolder(DuoPackPath))
            {
                Debug.LogError($"[M3-02A Step 1] FAIL — folder not found: {DuoPackPath}");
                return;
            }

            // Confirm with user explicitly
            if (!EditorUtility.DisplayDialog(
                "M3-02A Step 1: Delete Duo pack",
                $"This will permanently delete:\n{DuoPackPath}\n\n" +
                "Animator references will become temporarily 'missing' until Step 2 imports the new pack. " +
                "Make sure git checkpoint commit 'PreAsset Swap' exists.\n\n" +
                "Proceed?",
                "Delete", "Cancel"))
            {
                Debug.Log("[M3-02A Step 1] User cancelled.");
                return;
            }

            bool deleted = AssetDatabase.DeleteAsset(DuoPackPath);
            if (!deleted)
            {
                Debug.LogError($"[M3-02A Step 1] FAIL — AssetDatabase.DeleteAsset returned false. Try restarting Unity.");
                return;
            }

            AssetDatabase.Refresh();

            if (AssetDatabase.IsValidFolder(DuoPackPath))
            {
                Debug.LogError($"[M3-02A Step 1] FAIL — folder still exists after DeleteAsset: {DuoPackPath}");
                return;
            }

            Debug.Log($"[M3-02A Step 1] PASS — Duo pack deleted. Run Step 2 next.");
        }

        // ── Step 2: Import World Bundle + relocate ──────────────────────

        [MenuItem("LevelGen/Pack Swap/M3-02A · Step 2 (Import + Relocate)")]
        public static void Step2_ImportAndRelocate()
        {
            Debug.Log($"[M3-02A Step 2] Starting.");

            if (!File.Exists(PackagePath))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — package not found at {PackagePath}");
                return;
            }

            if (AssetDatabase.IsValidFolder(DuoPackPath))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — Step 1 not yet run. Duo folder still exists at {DuoPackPath}");
                return;
            }

            if (AssetDatabase.IsValidFolder(TargetPath))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — target folder already exists: {TargetPath}. Aborting to avoid overwrite.");
                return;
            }

            // Subscribe ONCE to import-completed callback. The callback chains the rest of Step 2.
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;
            AssetDatabase.importPackageCancelled += OnImportCancelled;
            AssetDatabase.importPackageFailed    -= OnImportFailed;
            AssetDatabase.importPackageFailed    += OnImportFailed;

            Debug.Log($"[M3-02A Step 2] Calling ImportPackage(interactive=false). May take a minute for 1607 assets...");
            AssetDatabase.ImportPackage(PackagePath, false);
            // Control returns immediately; OnImportCompleted fires when import is done.
        }

        private static void OnImportCompleted(string packageName)
        {
            // Detach handlers — one-shot.
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;
            AssetDatabase.importPackageFailed    -= OnImportFailed;

            Debug.Log($"[M3-02A Step 2] Import completed: '{packageName}'. Continuing with cleanup + relocate...");

            AssetDatabase.Refresh();

            // Verify the publisher default folder appeared
            if (!AssetDatabase.IsValidFolder(PublisherDefaultPath))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — expected folder not present after import: {PublisherDefaultPath}");
                return;
            }

            // Delete embedded HDRP / BuiltIn .unitypackage wrappers (alt-pipeline variants).
            int wrappersDeleted = 0;
            foreach (var wrapperPath in HdrpWrappersToDelete)
            {
                if (File.Exists(wrapperPath))
                {
                    if (AssetDatabase.DeleteAsset(wrapperPath))
                    {
                        Debug.Log($"[M3-02A Step 2] Deleted alt-pipeline wrapper: {wrapperPath}");
                        wrappersDeleted++;
                    }
                    else
                    {
                        Debug.LogWarning($"[M3-02A Step 2] Could not delete: {wrapperPath} (continuing)");
                    }
                }
            }
            Debug.Log($"[M3-02A Step 2] Deleted {wrappersDeleted} alt-pipeline wrappers.");

            // Ensure AssetPacks parent folder exists
            if (!AssetDatabase.IsValidFolder(AssetPacksFolder))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — {AssetPacksFolder} does not exist. Manual create required.");
                return;
            }

            // Relocate via AssetDatabase.MoveAsset — preserves all GUID references
            string moveResult = AssetDatabase.MoveAsset(PublisherDefaultPath, TargetPath);
            if (!string.IsNullOrEmpty(moveResult))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — MoveAsset error: {moveResult}");
                return;
            }

            AssetDatabase.Refresh();

            if (AssetDatabase.IsValidFolder(PublisherDefaultPath))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — original folder still exists post-move: {PublisherDefaultPath}");
                return;
            }
            if (!AssetDatabase.IsValidFolder(TargetPath))
            {
                Debug.LogError($"[M3-02A Step 2] FAIL — target folder missing post-move: {TargetPath}");
                return;
            }

            Debug.Log($"[M3-02A Step 2] PASS — pack imported, alt-pipeline wrappers deleted, relocated to {TargetPath}. Run Step 3 next.");
        }

        private static void OnImportCancelled(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;
            AssetDatabase.importPackageFailed    -= OnImportFailed;
            Debug.LogWarning($"[M3-02A Step 2] Import cancelled: '{packageName}'. Re-run Step 2 to retry.");
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;
            AssetDatabase.importPackageFailed    -= OnImportFailed;
            Debug.LogError($"[M3-02A Step 2] Import FAILED: '{packageName}' — {errorMessage}");
        }

        // ── Step 3: Verify auto-relink + run validators ─────────────────

        [MenuItem("LevelGen/Pack Swap/M3-02A · Step 3 (Verify + Validate)")]
        public static void Step3_VerifyAndValidate()
        {
            Debug.Log($"[M3-02A Step 3] Starting.");

            if (!AssetDatabase.IsValidFolder(TargetPath))
            {
                Debug.LogError($"[M3-02A Step 3] FAIL — target folder missing: {TargetPath}. Run Step 2 first.");
                return;
            }

            // ─── Auto-relink verification ───────────────────────────────
            int pass = 0;
            int fail = 0;
            int warn = 0;
            var report = new List<string>();
            report.Add("# M3-02A Step 3 — Post-Swap Verification");
            report.Add("");
            report.Add($"**Date:** {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            report.Add($"**Target pack root:** `{TargetPath}/`");
            report.Add("");
            report.Add("PASS criteria: GUID resolves to a non-null AnimationClip whose path is inside the new pack root.");
            report.Add("Clip-name mismatches are INFORMATIONAL only (publisher renamed clip-subasset internally without changing FBX filename or GUID — runtime resolves by GUID, not by name).");
            report.Add("");
            report.Add("## Auto-relink results — 13 currently-wired clips");
            report.Add("");
            report.Add("| GUID | Expected name | Resolved name | Resolved path | Verdict |");
            report.Add("|---|---|---|---|---|");

            foreach (var kvp in ExpectedClips)
            {
                string guid = kvp.Key;
                string expectedName = kvp.Value;
                string resolvedPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    report.Add($"| `{guid}` | {expectedName} | — | (unresolved) | **FAIL** |");
                    Debug.LogError($"[M3-02A Step 3] FAIL — GUID {guid} ({expectedName}) does not resolve to any asset.");
                    fail++;
                    continue;
                }

                // Try to load the actual clip — first by direct asset, then sub-asset scan.
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(resolvedPath);
                if (clip == null)
                {
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(resolvedPath);
                    clip = subAssets.OfType<AnimationClip>().FirstOrDefault();
                }

                bool inNewPack = resolvedPath.StartsWith(TargetPath + "/", System.StringComparison.Ordinal);

                if (clip == null)
                {
                    report.Add($"| `{guid}` | {expectedName} | (no AnimationClip) | {resolvedPath} | **FAIL** |");
                    Debug.LogError($"[M3-02A Step 3] FAIL — GUID {guid} resolved to path '{resolvedPath}' but contains no AnimationClip.");
                    fail++;
                    continue;
                }

                if (!inNewPack)
                {
                    report.Add($"| `{guid}` | {expectedName} | {clip.name} | {resolvedPath} | **FAIL** |");
                    Debug.LogError($"[M3-02A Step 3] FAIL — GUID {guid} resolved outside new pack: {resolvedPath}");
                    fail++;
                    continue;
                }

                // Clip resolves and is in new pack — PASS. Cosmetic name check is informational only.
                bool nameMatch = clip.name == expectedName;
                if (nameMatch)
                {
                    report.Add($"| `{guid}` | {expectedName} | {clip.name} | {resolvedPath} | **PASS** |");
                }
                else
                {
                    report.Add($"| `{guid}` | {expectedName} | {clip.name} | {resolvedPath} | **PASS** (clip name differs — cosmetic) |");
                    Debug.LogWarning($"[M3-02A Step 3] WARN — GUID {guid} resolved correctly to '{resolvedPath}' but clip-subasset name is '{clip.name}' (expected '{expectedName}'). Cosmetic only — runtime resolves by GUID. Continuing.");
                    warn++;
                }
                pass++;
            }

            report.Add("");
            report.Add($"**Auto-relink summary: {pass} PASS / {fail} FAIL / {warn} cosmetic name-mismatch warnings** (13 expected total).");
            report.Add("");

            if (fail > 0)
            {
                Debug.LogError($"[M3-02A Step 3] AUTO-RELINK FAILED ({fail} of 13). Aborting before validator run. Review console errors above.");
                report.Add("⚠ Auto-relink failed; validator run aborted. See console errors.");
                WriteReport(report);
                return;
            }

            Debug.Log($"[M3-02A Step 3] Auto-relink: {pass}/13 PASS. Running M2-B validators...");

            // ─── Run all 6 M2-B validators ──────────────────────────────
            report.Add("## M2-B Validator results");
            report.Add("");
            report.Add("Each menu item below was invoked via EditorApplication.ExecuteMenuItem.");
            report.Add("Validator output is in the Console; this report only captures invocation success.");
            report.Add("");
            int validatorsInvoked = 0;
            int validatorsFailed = 0;
            foreach (var menuPath in M2BValidators)
            {
                bool ok = EditorApplication.ExecuteMenuItem(menuPath);
                if (ok)
                {
                    Debug.Log($"[M3-02A Step 3] Invoked: {menuPath}");
                    validatorsInvoked++;
                    report.Add($"- ✓ Invoked: `{menuPath}`");
                }
                else
                {
                    Debug.LogError($"[M3-02A Step 3] FAIL to invoke: {menuPath}");
                    validatorsFailed++;
                    report.Add($"- ✗ FAIL to invoke: `{menuPath}`");
                }
            }

            report.Add("");
            report.Add($"**Validators invoked: {validatorsInvoked} / {M2BValidators.Length}.** Validator-internal PASS/FAIL counts are in the Console — review individually.");
            report.Add("");
            report.Add("---");
            report.Add("");
            report.Add("**Next step:** M3-02B — replace the broken `MaleCharacterPBR` PrefabInstance under `Player_MaleHero.prefab` with a chosen MC* prefab from the new pack.");

            WriteReport(report);

            if (validatorsFailed == 0)
            {
                Debug.Log($"[M3-02A Step 3] PASS — auto-relink {pass}/13, all {validatorsInvoked} validators invoked. Review individual validator console output for internal PASS/FAIL.");
            }
            else
            {
                Debug.LogError($"[M3-02A Step 3] PARTIAL — auto-relink {pass}/13 PASS, but {validatorsFailed} validator(s) failed to invoke.");
            }
        }

        private static void WriteReport(List<string> lines)
        {
            const string reportPath = "Assets/Documentation/M3_02A_postswap_verification.md";
            File.WriteAllLines(reportPath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"[M3-02A Step 3] Report written: {reportPath}");
        }
    }
}
#endif
