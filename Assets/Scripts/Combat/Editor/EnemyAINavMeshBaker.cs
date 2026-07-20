// EnemyAINavMeshBaker.cs — bake NavMesh in the active scene (M10).
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Bake Test Scene NavMesh
//
// Uses the Unity 6 AI Navigation package (com.unity.ai.navigation 2.x;
// confirmed in Packages/manifest.json). Spawns or refreshes a single
// `_NavMeshSurface` GameObject in the active scene, bakes via
// NavMeshSurface.BuildNavMesh().
//
// Dynamic-collider objects (those with a CharacterController or
// NavMeshAgent) are tagged with NavMeshModifier { ignoreFromBuild=true }
// at bake time so they don't carve holes in the NavMesh. Without this,
// the Player's CharacterController would create a Player-shaped hole
// at its Play-mode start position.
//
// Idempotent: re-running rebuilds.
//
// NOT play-mode safe — the user must be in edit mode. Logs an error
// and bails if Application.isPlaying.

#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LevelGen.Combat.EditorTools
{
    public static class EnemyAINavMeshBaker
    {
        private const string SurfaceGOName = "_NavMeshSurface";

        [MenuItem("LevelGen/Combat/Bake Test Scene NavMesh")]
        public static void Bake()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[EnemyAINavMeshBaker] NavMesh bake is edit-time only. " +
                               "Exit Play mode and re-run.");
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[EnemyAINavMeshBaker] No active scene to bake into. " +
                               "Open a scene and re-run.");
                return;
            }

            // ── Step 1: tag dynamic-collider objects so they don't bake ─────
            // Any GameObject in the scene with a NavMeshAgent or
            // CharacterController gets a NavMeshModifier { ignoreFromBuild=true }
            // so the bake skips them. Idempotent — re-running just sets
            // the flag again on the same components.
            int taggedAgents = 0;
            int taggedCC     = 0;
            // Unity 6: the FindObjectsSortMode overload is deprecated — sort order
            // is irrelevant here (we just tag every match), so use the 1-arg form.
            foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include))
            {
                if (EnsureIgnoreModifier(agent.gameObject)) taggedAgents++;
            }
            foreach (var cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include))
            {
                if (EnsureIgnoreModifier(cc.gameObject)) taggedCC++;
            }

            // ── Step 2: ensure a _NavMeshSurface in the scene ───────────────
            var surfaceGO = GameObject.Find(SurfaceGOName);
            if (surfaceGO == null)
            {
                surfaceGO = new GameObject(SurfaceGOName);
                Undo.RegisterCreatedObjectUndo(surfaceGO, "Create _NavMeshSurface");
            }
            surfaceGO.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            surfaceGO.transform.localScale = Vector3.one;

            var surface = surfaceGO.GetComponent<NavMeshSurface>();
            if (surface == null) surface = surfaceGO.AddComponent<NavMeshSurface>();

            // CollectObjects=All walks the whole scene. UseGeometry=PhysicsColliders
            // matches the M2-D mental model (the floor's MeshCollider on the Plane
            // primitive is what defines walkable area; mesh visuals don't matter).
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry    = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea    = 0; // Walkable

            // ── Step 3: bake ────────────────────────────────────────────────
            try
            {
                surface.BuildNavMesh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnemyAINavMeshBaker] BuildNavMesh failed: {ex.Message}");
                return;
            }

            EditorSceneManager.MarkSceneDirty(activeScene);

            int triCount = surface.navMeshData != null ? 1 : 0;
            string dataState = surface.navMeshData != null ? "baked" : "EMPTY (no walkable surfaces found)";
            Debug.Log(
                $"[EnemyAINavMeshBaker] NavMesh bake complete on '{activeScene.name}'.\n" +
                $"  Surface: '{SurfaceGOName}' (CollectObjects=All, useGeometry=PhysicsColliders)\n" +
                $"  navMeshData: {dataState}\n" +
                $"  Tagged dynamic colliders for ignore: {taggedAgents} NavMeshAgent + {taggedCC} CharacterController.\n" +
                $"  Open Window ▶ AI ▶ Navigation to visually inspect the bake."
            );

            if (surface.navMeshData == null)
            {
                Debug.LogWarning("[EnemyAINavMeshBaker] navMeshData is null after bake — likely no Floor / " +
                                 "no walkable colliders in the scene. Make sure the scene contains a " +
                                 "Plane (or other mesh-collider geometry) for the agent to walk on.");
            }
        }

        /// <summary>
        /// Adds (or finds) a NavMeshModifier on <paramref name="go"/> with
        /// <c>ignoreFromBuild=true</c>. Returns true if the modifier was
        /// newly created, false if it was already present (idempotent).
        /// </summary>
        private static bool EnsureIgnoreModifier(GameObject go)
        {
            var mod = go.GetComponent<NavMeshModifier>();
            bool created = false;
            if (mod == null)
            {
                mod = Undo.AddComponent<NavMeshModifier>(go);
                created = true;
            }
            mod.ignoreFromBuild = true;
            EditorUtility.SetDirty(mod);
            return created;
        }
    }
}
#endif
