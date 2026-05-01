// M3_03B_DuoReimportVerifier.cs — One-off post-import verification.
//
// Single menu item:
//   LevelGen ▶ Pack Swap ▶ M3-03B · Verify Duo Re-Import + Validate
//
// Run AFTER the 11 Duo assets have been file-copied into
// Assets/AssetPacks/RPG Tiny Hero Duo/. This script does:
//   ⑤ AssetDatabase.Refresh + verify all 11 GUIDs resolve.
//   ⑥ Verify Player_MaleHero.prefab's MaleCharacterPBR PrefabInstance
//      auto-relinks (no missing-prefab warning).
//   ⑦ Run all 6 M2-B validators via ExecuteMenuItem.
//
// Read-only — does not modify any asset. Each step prints PASS/FAIL.
//
// This file can be safely deleted after M3-03B completes; it is
// one-off scaffolding (alongside M3_02A_PackSwapExecutor.cs).

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.EditorTools
{
    public static class M3_03B_DuoReimportVerifier
    {
        private const string DuoTargetRoot       = "Assets/AssetPacks/RPG Tiny Hero Duo";
        private const string PlayerPrefabPath    = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const string MaleCharacterGuid   = "2dfbb63c9cdf7504faf4ff26b0581598";
        private const string FemaleCharacterGuid = "cc91c8ba8b9a34f4d99e70d721f60b64";

        // The 11 GUIDs we re-imported from the Duo, mapped to expected target paths.
        private static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            { "2dfbb63c9cdf7504faf4ff26b0581598", DuoTargetRoot + "/Prefab/MaleCharacterPBR.prefab" },
            { "cc91c8ba8b9a34f4d99e70d721f60b64", DuoTargetRoot + "/Prefab/FemaleCharacterPBR.prefab" },
            { "34b0895dfc863f742aa5075a4e691859", DuoTargetRoot + "/Mesh/ModularCharacterPBR.fbx" },
            { "9fbe61d1f4bc091439f25f985dd189a5", DuoTargetRoot + "/Prefab/OHS03PBR.prefab" },
            { "3617e4093ca8d5145986d37c89cfd692", DuoTargetRoot + "/Prefab/OHS06PBR.prefab" },
            { "ac86f8ddca59acb4184d3eb42067bafa", DuoTargetRoot + "/Prefab/Shield05PBR.prefab" },
            { "573036acf9f1e0c42845dddc25cea245", DuoTargetRoot + "/Prefab/Shield08PBR.prefab" },
            { "de392f919a34e4e4c94a6e77ef08a22d", DuoTargetRoot + "/Mesh/OHS03PBR.fbx" },
            { "8e583e9185af1be4e9dd6f501e6fe7d4", DuoTargetRoot + "/Mesh/OHS06PBR.fbx" },
            { "99054bc45d8ad7442b1e413b0e260ca7", DuoTargetRoot + "/Mesh/Shield05PBR.fbx" },
            { "4f74d725d371c394ba3753952409c5ee", DuoTargetRoot + "/Mesh/Shield08PBR.fbx" },
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

        [MenuItem("LevelGen/Pack Swap/M3-03B · Verify Duo Re-Import + Validate")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[M3-03B] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[M3-03B] FAIL — {label}: {detail}"); }
            }

            Debug.Log("[M3-03B] Starting verification.");

            // ── Step ⑤a: Force AssetDatabase refresh to pick up the file copies.
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // ── Step ⑤b: Verify all 11 GUIDs resolve to expected target paths.
            int resolved = 0;
            foreach (var kvp in Expected)
            {
                string guid = kvp.Key;
                string expectedPath = kvp.Value;
                string resolvedPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(resolvedPath))
                {
                    Check($"GUID {guid}", false, $"unresolved (expected {expectedPath})");
                    continue;
                }
                bool match = resolvedPath == expectedPath;
                Check($"GUID {guid}", match,
                    match
                        ? $"→ {resolvedPath}"
                        : $"resolved to '{resolvedPath}' but expected '{expectedPath}'");
                if (match) resolved++;
            }

            if (resolved != 11)
            {
                Debug.LogError($"[M3-03B] AUTO-RELINK INCOMPLETE — {resolved}/11. Aborting before validator run.");
                return;
            }
            Debug.Log($"[M3-03B] All 11 GUIDs resolved to expected paths.");

            // ── Step ⑥: Verify Player_MaleHero PrefabInstance auto-relink.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Check("Player_MaleHero.prefab loadable", false, $"not found at {PlayerPrefabPath}");
                return;
            }
            Check("Player_MaleHero.prefab loadable", true, "loaded");

            // Walk root's children and find the MaleCharacterPBR PrefabInstance.
            Transform malePrefabInstance = null;
            Transform cameraTarget = null;
            foreach (Transform child in playerPrefab.transform)
            {
                if (child.name == "MaleCharacterPBR") malePrefabInstance = child;
                if (child.name == "CameraTarget")    cameraTarget = child;
            }

            // MaleCharacterPBR child existence
            Check("MaleCharacterPBR child present in Player_MaleHero", malePrefabInstance != null,
                malePrefabInstance != null
                    ? "found in hierarchy"
                    : "NOT found — auto-relink may have failed");

            if (malePrefabInstance != null)
            {
                // Verify it's a real prefab instance, not just a renamed empty.
                var src = PrefabUtility.GetCorrespondingObjectFromSource(malePrefabInstance.gameObject);
                Check("MaleCharacterPBR PrefabInstance source resolves", src != null,
                    src != null
                        ? $"source = '{src.name}' at {AssetDatabase.GetAssetPath(src)}"
                        : "GetCorrespondingObjectFromSource returned null (PrefabInstance broken)");

                if (src != null)
                {
                    string srcPath = AssetDatabase.GetAssetPath(src);
                    string srcGuid = AssetDatabase.AssetPathToGUID(srcPath);
                    Check("Source prefab GUID matches Duo MaleCharacterPBR",
                        srcGuid == MaleCharacterGuid,
                        $"source GUID = {srcGuid}");
                }

                // Check if Unity flagged the asset as missing (defensive).
                bool missing = PrefabUtility.IsPrefabAssetMissing(malePrefabInstance.gameObject);
                Check("PrefabInstance not flagged Missing",
                    !missing,
                    missing ? "flagged Missing" : "not flagged");
            }

            // CameraTarget child sanity
            Check("CameraTarget child present", cameraTarget != null,
                cameraTarget != null
                    ? $"localPosition = {cameraTarget.localPosition}"
                    : "missing");
            if (cameraTarget != null)
            {
                bool atExpected = Mathf.Approximately(cameraTarget.localPosition.x, 0f)
                               && Mathf.Approximately(cameraTarget.localPosition.y, 1.6f)
                               && Mathf.Approximately(cameraTarget.localPosition.z, 0f);
                Check("CameraTarget localPosition (0, 1.6, 0)", atExpected,
                    $"actual = {cameraTarget.localPosition}");
            }

            // ── Step ⑦: Run all 6 M2-B validators.
            Debug.Log("[M3-03B] Running M2-B validators...");
            int validatorsInvoked = 0;
            foreach (var menuPath in M2BValidators)
            {
                bool ok = EditorApplication.ExecuteMenuItem(menuPath);
                if (ok)
                {
                    Debug.Log($"[M3-03B] Invoked: {menuPath}");
                    validatorsInvoked++;
                }
                else
                {
                    Debug.LogError($"[M3-03B] FAIL to invoke: {menuPath}");
                    fail++;
                }
            }
            Debug.Log($"[M3-03B] Validators invoked: {validatorsInvoked} / {M2BValidators.Length}. Validator-internal results in Console.");

            // ── Summary
            Debug.Log($"[M3-03B] SUMMARY — {pass} PASS / {fail} FAIL on M3-03B's own checks. Review individual validator outputs above for M2-B internal results.");
            Debug.Log("[M3-03B] Smoke tests recommended next:");
            Debug.Log("  - Assets/Documentation/M2B_03_smoke_test.md (10 tests — single attack + buffer)");
            Debug.Log("  - Assets/Documentation/M2B_05_jump_smoke_test.md (10 tests — jump runtime)");
            Debug.Log("  - Assets/Documentation/M2B_07_combo_smoke_test.md (5 tests — combo extension)");
            Debug.Log("  Total ≈ 25 tests, ~15 minutes structured play in Player_M1_Test.unity.");
        }
    }
}
#endif
