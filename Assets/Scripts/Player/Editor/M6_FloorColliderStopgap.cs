// M6_FloorColliderStopgap.cs — Scene-only floor collider fix.
//
// Single menu item:
//   LevelGen ▶ Scene Setup ▶ Add Floor Collider Stopgap to SampleScene
//
// Adds a thin BoxCollider GameObject at the SampleScene root covering the
// 10×10 starter room footprint at world Y=0, so the player has something
// to stand on. Follow-up to M5 diagnosis Conclusion A (no colliders in
// the manually-assembled FDP room).
//
// Idempotent: re-running replaces the existing 'Floor_Collider_Stopgap'
// root if found.
//
// One-off scaffolding — can be deleted after use. The collider stays in
// the saved scene; the script is no longer needed once it has run.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelGen.Player.EditorTools
{
    public static class M6_FloorColliderStopgap
    {
        private const string ScenePath        = "Assets/Scenes/SampleScene.unity";
        private const string ColliderRootName = "Floor_Collider_Stopgap";

        // Room footprint: NW corner at world origin, extending +X and -Z.
        // 10×10 starter room → center (5, _, -5).
        // Thin box: top surface at Y=0, bottom at Y=-0.1.
        private static readonly Vector3 ColliderPosition = new Vector3(5f, -0.05f, -5f);
        private static readonly Vector3 ColliderSize     = new Vector3(10f, 0.1f, 10f);

        [MenuItem("LevelGen/Scene Setup/Add Floor Collider Stopgap to SampleScene")]
        public static void Run()
        {
            Debug.Log("[M6 Floor Collider] Starting.");

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[M6 Floor Collider] Cancelled — active scene not saved.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            Debug.Log($"[M6 Floor Collider] Opened {scene.path}.");

            // Idempotency: remove any existing stopgap root so re-runs replace it.
            int removed = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == ColliderRootName)
                {
                    Debug.Log($"[M6 Floor Collider] Removing existing '{go.name}' root.");
                    Object.DestroyImmediate(go);
                    removed++;
                }
            }
            if (removed == 0)
                Debug.Log("[M6 Floor Collider] No prior stopgap collider — fresh add.");

            // Create the stopgap root.
            var floor = new GameObject(ColliderRootName);
            floor.transform.SetPositionAndRotation(ColliderPosition, Quaternion.identity);
            var box = floor.AddComponent<BoxCollider>();
            box.center    = Vector3.zero;
            box.size      = ColliderSize;
            box.isTrigger = false;
            Undo.RegisterCreatedObjectUndo(floor, "Add Floor Collider Stopgap");
            Debug.Log($"[M6 Floor Collider] Created '{ColliderRootName}' at {ColliderPosition} with size {ColliderSize}. " +
                      $"Top surface Y = {ColliderPosition.y + ColliderSize.y / 2f:F4}.");

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[M6 Floor Collider] Scene saved.");

            // Verification — downward raycast at spawn should now hit at Y≈0.
            Physics.SyncTransforms();
            var probe = Physics.RaycastAll(new Vector3(5f, 10f, -5f), Vector3.down, 50f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float topSolidY = float.NaN;
            foreach (var h in probe)
            {
                // Filter out the player's own CharacterController self-hit.
                if (h.collider == null) continue;
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (float.IsNaN(topSolidY) || h.point.y > topSolidY)
                    topSolidY = h.point.y;
            }

            if (float.IsNaN(topSolidY))
            {
                Debug.LogError("[M6 Floor Collider] FAIL — spawn raycast still found no non-player solid surface. " +
                               "Re-run M5 diagnosis to investigate.");
            }
            else
            {
                bool topOk = Mathf.Approximately(topSolidY, 0f);
                Debug.Log($"[M6 Floor Collider] {(topOk ? "PASS" : "WARN")} — spawn raycast top surface Y = {topSolidY:F4} (expected 0.0000).");
            }

            Debug.Log("[M6 Floor Collider] Done. Press Play in SampleScene to confirm the player stands on the floor.");
        }
    }
}
#endif
