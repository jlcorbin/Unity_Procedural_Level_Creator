// CinemachineAutoBindAdder.cs — places CinemachineAutoBind on the
// scene's existing CinemachineCamera. Idempotent — re-runnable.
//
// Menu: LevelGen ▶ Player ▶ Add Cinemachine Auto-Bind to Active Scene

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

namespace LevelGen.Player.Editor
{
    public static class CinemachineAutoBindAdder
    {
        [MenuItem("LevelGen/Player/Add Cinemachine Auto-Bind to Active Scene")]
        public static void Add()
        {
            var vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (vcam == null)
            {
                Debug.LogError(
                    "[CinemachineAutoBindAdder] No CinemachineCamera found in the active scene. " +
                    "Set up the Cinemachine rig first (CM Brain Camera + CM Follow Camera), then re-run this menu.");
                return;
            }

            var existing = vcam.GetComponent<CinemachineAutoBind>();
            if (existing != null)
            {
                Debug.Log($"[CinemachineAutoBindAdder] CinemachineAutoBind already on '{vcam.name}'. No change.");
                return;
            }

            Undo.AddComponent<CinemachineAutoBind>(vcam.gameObject);
            EditorUtility.SetDirty(vcam.gameObject);
            EditorSceneManager.MarkSceneDirty(vcam.gameObject.scene);

            Debug.Log(
                $"[CinemachineAutoBindAdder] Added CinemachineAutoBind to '{vcam.name}'.\n" +
                "  On Play, it will find the GameObject tagged 'Player' and bind\n" +
                "  the vcam's Follow + LookAt to its CameraTarget child.\n" +
                "  Save the scene to persist the component.");

            Selection.activeObject = vcam.gameObject;
            EditorGUIUtility.PingObject(vcam.gameObject);
        }
    }
}
#endif
