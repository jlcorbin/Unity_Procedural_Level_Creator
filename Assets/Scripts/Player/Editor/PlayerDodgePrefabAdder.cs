// PlayerDodgePrefabAdder.cs — M12 prefab wiring (one-shot path).
//
// Single menu item:
//   LevelGen ▶ Player ▶ Add PlayerDodge to Player_MaleHero Prefab
//
// Idempotent — adds PlayerDodge to the prefab root iff it isn't
// already there. No SerializeField references to wire (PlayerDodge
// resolves siblings via GetComponent in Awake), so this adder is
// short relative to PlayerDeathPrefabAdder.
//
// Use this menu when:
//   - The user shipped M11 without M12 and wants to add M12 without
//     re-running BuildPlayerMaleHeroPrefab.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerDodgePrefabAdder
    {
        private const string PrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Add PlayerDodge to Player_MaleHero Prefab")]
        public static void Run()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[PlayerDodgePrefabAdder] Prefab not found at {PrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool addedNow = false;
                var dodge = contents.GetComponent<PlayerDodge>();
                if (dodge == null)
                {
                    dodge = contents.AddComponent<PlayerDodge>();
                    if (dodge == null)
                    {
                        Debug.LogError("[PlayerDodgePrefabAdder] AddComponent<PlayerDodge>() returned null. Aborting save.");
                        return;
                    }
                    addedNow = true;
                }

                // RequireComponent prerequisites — flag clearly if missing.
                if (contents.GetComponent<CharacterController>() == null)
                    Debug.LogError("[PlayerDodgePrefabAdder] CharacterController missing on prefab. " +
                                   "Re-run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");

                if (contents.GetComponent<LevelGen.Combat.CharacterStatsRuntime>() == null)
                    Debug.LogError("[PlayerDodgePrefabAdder] CharacterStatsRuntime missing on prefab. " +
                                   "Run 'LevelGen ▶ UI ▶ Add CharacterStatsRuntime to Player_MaleHero' first.");

                if (contents.GetComponent<PlayerInputReader>() == null)
                    Debug.LogError("[PlayerDodgePrefabAdder] PlayerInputReader missing on prefab. " +
                                   "Re-run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);

                Debug.Log($"[PlayerDodgePrefabAdder] {(addedNow ? "Added" : "Already present")} PlayerDodge on {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
#endif
