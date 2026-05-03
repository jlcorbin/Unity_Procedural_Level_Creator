// PlayerInteractorPrefabAdder.cs — M6 prefab wiring (one-shot path).
//
// Single menu item:
//   LevelGen ▶ Player ▶ Add PlayerInteractor to Player_MaleHero Prefab
//
// Idempotent — adds PlayerInteractor to the prefab root iff it isn't
// already there. PlayerInteractor has no SerializeField references
// (resolves PlayerInputReader + PlayerDeath via GetComponent in Awake),
// so no explicit field wiring is needed — same authoring style as
// PlayerCombatPrefabAdder.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerInteractorPrefabAdder
    {
        private const string PrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Add PlayerInteractor to Player_MaleHero Prefab")]
        public static void Run()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[PlayerInteractorPrefabAdder] Prefab not found at {PrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (contents.GetComponent<PlayerInteractor>() != null)
                {
                    Debug.Log("[PlayerInteractorPrefabAdder] PlayerInteractor already on Player_MaleHero — no change.");
                    return;
                }

                var interactor = contents.AddComponent<PlayerInteractor>();
                if (interactor == null)
                {
                    Debug.LogError("[PlayerInteractorPrefabAdder] AddComponent<PlayerInteractor>() returned null. Aborting save.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);

                bool hasReader = contents.GetComponent<PlayerInputReader>() != null;
                bool hasDeath  = contents.GetComponent<PlayerDeath>() != null;
                Debug.Log($"[PlayerInteractorPrefabAdder] PlayerInteractor added to {PrefabPath}.\n" +
                          $"  PlayerInputReader sibling:  {(hasReader ? "OK" : "MISSING — Interact event won't reach the interactor")}\n" +
                          $"  PlayerDeath sibling:        {(hasDeath ? "OK (death-clears-prompt wired)" : "missing (no death-clear hook; non-fatal)")}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
#endif
