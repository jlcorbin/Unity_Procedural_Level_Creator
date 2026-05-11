// CinemachineRigBuilder.cs — restores the M2-A camera-setup menu that
// was deleted along with PlayerPrefabBuilder.cs in M12-R.
//
// Builds the canonical Cinemachine 3.x rig in the active scene:
//   - CM Brain Camera (tagged MainCamera, with CinemachineBrain)
//   - CM Follow Camera (CinemachineCamera + OrbitalFollow [Body stage] +
//     RotationComposer [Aim stage] + Deoccluder + InputAxisController +
//     CinemachineAutoBind)
//
// Auto-bind is folded in by default so the rig survives prefab rebuilds
// without manual re-wiring (M12-R lesson).
//
// Configuration matches the M2-A tuning logged in CLAUDE.md:
//   - OrbitalFollow: Sphere, Radius=4
//   - HorizontalAxis: (-180,180) wrap=true, initial 0°
//   - VerticalAxis: (-10,70) wrap=false, initial 15°
//   - Reader Gain: ±10 (Y inverted)
//   - Deoccluder: MinDistance=1.0
//
// Menu: LevelGen ▶ Player ▶ Add Cinemachine Follow Camera to Active Scene

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

namespace LevelGen.Player.Editor
{
    public static class CinemachineRigBuilder
    {
        private const string ActionsAssetPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("LevelGen/Player/Add Cinemachine Follow Camera to Active Scene")]
        [MenuItem("LevelGen/Input/Add Cinemachine Follow Camera to Active Scene")]
        public static void Build()
        {
            // ── Preflight ────────────────────────────────────────────────────
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.path))
            {
                Debug.LogError("[CM Setup] Active scene must be saved on disk before running. Save the scene and re-try.");
                return;
            }

            // Idempotency guard
            var existingVcam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (existingVcam != null)
            {
                Debug.LogError(
                    $"[CM Setup] Scene already has a CinemachineCamera ('{existingVcam.name}'). " +
                    "Delete it (and any CM Brain Camera GameObject) before re-running this menu item. " +
                    "Or use 'Add Cinemachine Auto-Bind to Active Scene' to retrofit auto-binding only.");
                return;
            }

            // Find Player root (matches Player_Hero tag = "Player")
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[CM Setup] No GameObject tagged 'Player' in active scene. Drop Player_Hero.prefab in first.");
                return;
            }
            var camTarget = player.transform.Find("CameraTarget");
            if (camTarget == null)
            {
                Debug.LogError(
                    $"[CM Setup] '{player.name}' has no CameraTarget child. " +
                    "Re-run 'LevelGen ▶ Player ▶ Build Player_Hero Prefab' first.");
                return;
            }

            // Look up Look InputActionReference
            var lookRef = FindInputActionReference("Player", "Look");
            if (lookRef == null)
            {
                Debug.LogError(
                    "[CM Setup] No InputActionReference for Player/Look. " +
                    "Open Assets/InputSystem_Actions.inputactions in the editor once " +
                    "(auto-generates sub-asset references), save, then re-run.");
                return;
            }

            // ── Replace existing MainCamera ─────────────────────────────────
            int removed = 0;
            foreach (var go in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                Object.DestroyImmediate(go);
                removed++;
            }
            if (removed > 0)
                Debug.Log($"[CM Setup] Removed {removed} existing MainCamera-tagged GameObject(s).");

            // ── CM Brain Camera ─────────────────────────────────────────────
            var brainGO = new GameObject("CM Brain Camera");
            brainGO.tag = "MainCamera";
            brainGO.AddComponent<Camera>();
            brainGO.AddComponent<AudioListener>();
            brainGO.AddComponent<CinemachineBrain>();
            brainGO.transform.position = new Vector3(0f, 5f, -8f);
            brainGO.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

            // ── CM Follow Camera ────────────────────────────────────────────
            var vcamGO = new GameObject("CM Follow Camera");
            var vcam = vcamGO.AddComponent<CinemachineCamera>();
            vcam.Priority = new PrioritySettings { Value = 10, Enabled = true };
            vcam.Follow = camTarget;
            vcam.LookAt = camTarget;

            // Body stage: OrbitalFollow
            var orbital = vcamGO.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius     = 4.0f;

            var hAxis = orbital.HorizontalAxis;
            hAxis.Value  = 0f;
            hAxis.Range  = new Vector2(-180f, 180f);
            hAxis.Wrap   = true;
            hAxis.Center = 0f;
            orbital.HorizontalAxis = hAxis;

            var vAxis = orbital.VerticalAxis;
            vAxis.Value  = 15f;
            vAxis.Range  = new Vector2(-10f, 70f);
            vAxis.Wrap   = false;
            vAxis.Center = 15f;
            orbital.VerticalAxis = vAxis;

            // Aim stage: RotationComposer (OrbitalFollow only sets position;
            // without an Aim component the camera orbits but never turns).
            vcamGO.AddComponent<CinemachineRotationComposer>();

            // Collision: Deoccluder
            var deocc = vcamGO.AddComponent<CinemachineDeoccluder>();
            deocc.MinimumDistanceFromTarget = 1.0f;

            // Input: InputAxisController wired to Player/Look
            var inputCtrl = vcamGO.AddComponent<CinemachineInputAxisController>();
            inputCtrl.SynchronizeControllers();

            int wiredAxes = WireLookAxes(inputCtrl, lookRef);

            // Auto-bind: makes Follow/LookAt re-resolve on Play.
            // M12-R fold-in — manual scene refs no longer load-bearing.
            vcamGO.AddComponent<CinemachineAutoBind>();

            if (wiredAxes != 2)
                Debug.LogWarning($"[CM Setup] Expected to wire 2 Look axes (yaw + pitch), wired {wiredAxes}. " +
                                 "Inspect CinemachineInputAxisController.Controllers list and wire any unset axes manually.");

            // ── Save scene + reopen for verify ──────────────────────────────
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log(
                $"[CM Setup] Cinemachine rig installed in '{activeScene.name}'.\n" +
                $"  CM Brain Camera (MainCamera-tagged) at (0, 5, -8) rot (20, 0, 0)\n" +
                $"  CM Follow Camera: OrbitalFollow Sphere R=4, H(-180,180)wrap, V(-10,70)init=15\n" +
                $"  RotationComposer + Deoccluder (MinDistance=1.0)\n" +
                $"  InputAxisController: {wiredAxes}/2 axes wired (Player/Look, gain ±10, Y inverted)\n" +
                $"  CinemachineAutoBind: present — Follow + LookAt re-resolve on Play\n" +
                $"  Initial Follow/LookAt: {camTarget.name} (saved in scene; auto-bind also runs on Play)\n" +
                $"  Press Play to verify."
            );

            Selection.activeObject = vcamGO;
            EditorGUIUtility.PingObject(vcamGO);
        }

        private static int WireLookAxes(CinemachineInputAxisController inputCtrl,
                                        InputActionReference lookRef)
        {
            int wired = 0;
            System.Reflection.FieldInfo readerActionField = null;
            System.Reflection.FieldInfo readerGainField   = null;

            foreach (var c in inputCtrl.Controllers)
            {
                string n = c.Name ?? "";
                bool isRadial = n.Contains("Radial") || n.Contains("Scale") || n.Contains("Zoom");
                bool isXAxis  = !isRadial && (n.Contains(" X") || n.EndsWith("X") || n.Contains("Horizontal"));
                bool isYAxis  = !isRadial && (n.Contains(" Y") || n.EndsWith("Y") || n.Contains("Vertical"));
                bool wireThis = isXAxis || isYAxis;
                bool isYish   = isYAxis;
                if (!wireThis) continue;

                c.Enabled = true;

                if (readerActionField == null && c.Input != null)
                {
                    var rt = c.Input.GetType();
                    readerActionField = rt.GetField("Input") ?? rt.GetField("InputAction");
                    readerGainField   = rt.GetField("Gain");
                    if (readerActionField == null)
                    {
                        Debug.LogWarning($"[CM Setup] Reader on '{c.Name}' has neither 'Input' nor 'InputAction' field. CM API may have changed; wire by hand.");
                        break;
                    }
                }
                if (readerActionField != null && c.Input != null)
                {
                    readerActionField.SetValue(c.Input, lookRef);
                    if (readerGainField != null)
                        readerGainField.SetValue(c.Input, isYish ? -10.0f : 10.0f); // Y inverted; gain ±10 per Jason's playtest pref
                    wired++;
                }
            }

            // Defensive: null any reader on radial/zoom axes
            foreach (var c in inputCtrl.Controllers)
            {
                string n = c.Name ?? "";
                if (!(n.Contains("Radial") || n.Contains("Scale") || n.Contains("Zoom"))) continue;
                if (c.Input == null) continue;
                if (readerActionField == null) continue;
                var current = readerActionField.GetValue(c.Input) as InputActionReference;
                if (current != null) readerActionField.SetValue(c.Input, null);
            }
            return wired;
        }

        /// <summary>
        /// Walks the .inputactions asset's sub-assets to find the
        /// InputActionReference for the given map / action name. Returns
        /// null if not found (most likely cause: the asset hasn't been
        /// opened in the editor since import, so its auto-generated
        /// sub-asset references haven't been written).
        /// </summary>
        private static InputActionReference FindInputActionReference(string mapName, string actionName)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(ActionsAssetPath);
            foreach (var sub in subAssets)
            {
                if (sub is InputActionReference iar
                    && iar.action != null
                    && iar.action.name == actionName
                    && iar.action.actionMap != null
                    && iar.action.actionMap.name == mapName)
                {
                    return iar;
                }
            }
            return null;
        }
    }
}
#endif
