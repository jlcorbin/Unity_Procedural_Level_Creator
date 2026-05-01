// M4_SampleSceneSetup.cs — One-off scene-setup utility.
//
// Single menu item:
//   LevelGen ▶ Scene Setup ▶ SampleScene Ready-to-Play
//
// Prepares Assets/Scenes/SampleScene.unity for press-Play-and-it-works:
//   ②a Open SampleScene (single mode, prompt to save active first)
//   ②b Cleanup pass — remove legacy Main Camera + any leftover spawners /
//      stale Player_MaleHero / CM Brain Camera / CinemachineCamera
//   ②c Drop Player_MaleHero.prefab at world (5, 0, -5)
//   ②d Save scene, then invoke
//      "LevelGen/Player/Add Cinemachine Follow Camera to Active Scene"
//   ②e Final scene save
//   ③  Verification pass — log PASS/FAIL on six checks
//
// Read-only on prefab assets and on the existing room geometry. Safe to
// re-run (idempotent — second invocation does the same cleanup, replaces
// the player and camera).
//
// One-off scaffolding — can be deleted after the scene is set up
// (alongside M3_02A_PackSwapExecutor.cs and M3_03B_DuoReimportVerifier.cs).

#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelGen.Player.EditorTools
{
    public static class M4_SampleSceneSetup
    {
        private const string ScenePath  = "Assets/Scenes/SampleScene.unity";
        private const string PrefabPath = "Assets/Prefabs/Player/Player_MaleHero.prefab";
        private const string CmMenuPath = "LevelGen/Player/Add Cinemachine Follow Camera to Active Scene";

        private static readonly Vector3 PlayerSpawnPos = new Vector3(5f, 0f, -5f);

        [MenuItem("LevelGen/Scene Setup/SampleScene Ready-to-Play")]
        public static void Run()
        {
            Debug.Log("[SampleScene Setup] Starting.");

            // ── ②a · Open SampleScene ──────────────────────────────────────
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[SampleScene Setup] Cancelled — active scene not saved.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[SampleScene Setup] Failed to open {ScenePath}.");
                return;
            }
            Debug.Log($"[SampleScene Setup] Opened {scene.path}.");

            // ── ②b · Cleanup pass ──────────────────────────────────────────
            int removed = 0;
            removed += RemoveRootByName("PlayerSpawner",                "leftover spawner — replaced by direct prefab placement");
            removed += RemoveRootByName("Player_MaleHero (Spawned)",    "leftover runtime-spawn instance");
            removed += RemoveRootByName("Player_MaleHero",              "stale prefab instance — replacing with fresh");
            removed += RemoveRootByName("CinemachineCamera",            "stale CM vcam — recreating");
            removed += RemoveRootByName("CM Follow Camera",             "stale CM vcam — recreating");
            removed += RemoveRootByName("CM Brain Camera",              "stale CM brain — recreating");

            // Remove ALL MainCamera-tagged GameObjects (the legacy "Main Camera"
            // and any stragglers).
            var taggedCams = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (var cam in taggedCams)
            {
                Debug.Log($"[SampleScene Setup] Removing '{cam.name}' (MainCamera-tagged — CM Brain Camera will replace it).");
                Object.DestroyImmediate(cam);
                removed++;
            }
            Debug.Log($"[SampleScene Setup] Cleanup pass complete: {removed} removal(s).");

            // ── ②c · Drop the player prefab ───────────────────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"[SampleScene Setup] Player_MaleHero.prefab not found at {PrefabPath}. Aborting.");
                return;
            }

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.transform.SetPositionAndRotation(PlayerSpawnPos, Quaternion.identity);
            EditorUtility.SetDirty(playerInstance);
            Debug.Log($"[SampleScene Setup] Placed Player_MaleHero at {PlayerSpawnPos}.");

            // Sanity: CameraTarget child must exist on the placed instance
            // (the CM menu item depends on it).
            var camTargetCheck = playerInstance.transform.Find("CameraTarget");
            if (camTargetCheck == null)
            {
                Debug.LogError("[SampleScene Setup] Placed Player_MaleHero has no CameraTarget child. Aborting before CM step.");
                return;
            }

            // ── ②d · Save scene, then invoke CM menu item ────────────────
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SampleScene Setup] Scene saved before CM step.");

            bool ok = EditorApplication.ExecuteMenuItem(CmMenuPath);
            if (!ok)
            {
                Debug.LogError($"[SampleScene Setup] ExecuteMenuItem returned false for '{CmMenuPath}'. " +
                               "Menu item not registered or failed to invoke. Aborting.");
                return;
            }

            // ── ②e · Final scene save ─────────────────────────────────────
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SampleScene Setup] SampleScene.unity saved with player + Cinemachine.");

            // ── ③ · Verification ──────────────────────────────────────────
            Debug.Log("[SampleScene Setup] ── Verification pass ──");
            int pass = 0, fail = 0;

            void Check(string label, bool good, string detail)
            {
                if (good) { pass++; Debug.Log($"[SampleScene Setup] PASS — {label}: {detail}"); }
                else      { fail++; Debug.LogError($"[SampleScene Setup] FAIL — {label}: {detail}"); }
            }

            // (1) Active scene path
            var active = SceneManager.GetActiveScene();
            Check("active scene == SampleScene",
                active.path == ScenePath,
                $"path = '{active.path}'");

            // (2) Player_MaleHero placement
            // Re-fetch via SceneManager — the cached Scene struct from
            // OpenScene() can go stale across SaveScene + ExecuteMenuItem.
            GameObject playerRoot = null;
            int playerCount = 0;
            foreach (var go in active.GetRootGameObjects())
            {
                if (go.name == "Player_MaleHero")
                {
                    playerRoot = go;
                    playerCount++;
                }
            }
            Check("exactly one Player_MaleHero at scene root",
                playerCount == 1,
                $"count = {playerCount}");

            if (playerRoot != null)
            {
                bool atSpawn =
                    Mathf.Approximately(playerRoot.transform.position.x, PlayerSpawnPos.x) &&
                    Mathf.Approximately(playerRoot.transform.position.y, PlayerSpawnPos.y) &&
                    Mathf.Approximately(playerRoot.transform.position.z, PlayerSpawnPos.z) &&
                    playerRoot.transform.rotation == Quaternion.identity;
                Check("Player position (5, 0, -5) and rotation identity", atSpawn,
                    $"pos = {playerRoot.transform.position}, rot = {playerRoot.transform.rotation.eulerAngles}");

                var camTarget = playerRoot.transform.Find("CameraTarget");
                bool camTargetOk = camTarget != null
                    && Mathf.Approximately(camTarget.localPosition.x, 0f)
                    && Mathf.Approximately(camTarget.localPosition.y, 1.6f)
                    && Mathf.Approximately(camTarget.localPosition.z, 0f);
                Check("CameraTarget child at local (0, 1.6, 0)", camTargetOk,
                    camTarget != null ? $"local = {camTarget.localPosition}" : "missing");
            }

            // (3) Exactly one MainCamera-tagged GameObject named CM Brain Camera
            var taggedAfter = GameObject.FindGameObjectsWithTag("MainCamera");
            Check("exactly one MainCamera-tagged GameObject",
                taggedAfter.Length == 1,
                $"count = {taggedAfter.Length}");

            GameObject brain = taggedAfter.Length == 1 ? taggedAfter[0] : null;
            if (brain != null)
            {
                Check("MainCamera-tagged GameObject named 'CM Brain Camera'",
                    brain.name == "CM Brain Camera",
                    $"name = '{brain.name}'");
                Check("CM Brain Camera has Camera + AudioListener + CinemachineBrain",
                    brain.GetComponent<Camera>() != null
                    && brain.GetComponent<AudioListener>() != null
                    && brain.GetComponent<CinemachineBrain>() != null,
                    $"Camera={brain.GetComponent<Camera>() != null}, " +
                    $"AudioListener={brain.GetComponent<AudioListener>() != null}, " +
                    $"CinemachineBrain={brain.GetComponent<CinemachineBrain>() != null}");
            }

            // (4) Exactly one CinemachineCamera, Tracking Target on player CameraTarget
            var allVcams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude);
            Check("exactly one CinemachineCamera in scene",
                allVcams.Length == 1,
                $"count = {allVcams.Length}");

            if (allVcams.Length == 1 && playerRoot != null)
            {
                var vcam = allVcams[0];
                // CM 3.x's CinemachineCamera serializes the tracking target
                // under the nested Target struct (Target.TrackingTarget), so
                // SerializedObject lookups of "Follow" return null. Reading
                // the public Follow property delegates correctly.
                Transform followT = vcam.Follow;
                var expectedTarget = playerRoot.transform.Find("CameraTarget");
                bool trackingOk = followT != null && followT == expectedTarget;
                Check("CinemachineCamera Tracking Target → Player_MaleHero/CameraTarget",
                    trackingOk,
                    followT != null
                        ? $"resolved to '{followT.name}' (matches expected = {followT == expectedTarget})"
                        : "vcam.Follow is null");
            }

            // (5) Room geometry preserved (Starter_10x10 still at scene root)
            bool roomPresent = false;
            foreach (var go in active.GetRootGameObjects())
            {
                if (go.name == "Starter_10x10") { roomPresent = true; break; }
            }
            Check("Starter_10x10 room still at scene root", roomPresent,
                roomPresent ? "found" : "MISSING — room geometry was disturbed");

            // ── Summary
            Debug.Log($"[SampleScene Setup] SUMMARY — {pass} PASS / {fail} FAIL.");
            if (fail == 0)
                Debug.Log("[SampleScene Setup] All checks PASS. Open SampleScene and press Play.");
            else
                Debug.LogError("[SampleScene Setup] One or more checks FAILED. Inspect the scene before pressing Play.");
        }

        // ─────────────────────────────────────────────────────────────────────
        private static int RemoveRootByName(string name, string reason)
        {
            int count = 0;
            var scene = SceneManager.GetActiveScene();
            // Iterate over a snapshot — DestroyImmediate mutates the scene.
            var snapshot = new List<GameObject>(scene.GetRootGameObjects());
            foreach (var go in snapshot)
            {
                if (go.name == name)
                {
                    Debug.Log($"[SampleScene Setup] Removing root '{go.name}' — {reason}.");
                    Object.DestroyImmediate(go);
                    count++;
                }
            }
            return count;
        }
    }
}
#endif
