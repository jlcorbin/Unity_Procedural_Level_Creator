// PlayerStaminaPrefabAdder.cs — one-shot adder for the M9 PlayerStamina
// component. Mirrors PlayerDeathPrefabAdder / PlayerInteractorPrefabAdder.
//
// Folded into PlayerPrefabBuilder for clean rebuilds; this adder lets
// the user incrementally upgrade the existing prefab without re-running
// the full build path.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player.Editor
{
    public static class PlayerStaminaPrefabAdder
    {
        private const string PrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Add PlayerStamina to Player_MaleHero Prefab")]
        private static void AddPlayerStamina()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[PlayerStaminaPrefabAdder] {PrefabPath} not found. " +
                               "Run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[PlayerStaminaPrefabAdder] LoadPrefabContents failed for {PrefabPath}.");
                return;
            }

            try
            {
                if (contents.GetComponent<PlayerStamina>() != null)
                {
                    Debug.Log("[PlayerStaminaPrefabAdder] PlayerStamina already on Player_MaleHero — no change.");
                    return;
                }

                // PlayerStamina [RequireComponent]s CharacterStatsRuntime + PlayerController.
                // Bail with a clear message if either is missing — Unity would refuse the
                // AddComponent silently otherwise.
                if (contents.GetComponent<CharacterStatsRuntime>() == null)
                {
                    Debug.LogError("[PlayerStaminaPrefabAdder] Player_MaleHero is missing CharacterStatsRuntime. " +
                                   "Run 'LevelGen ▶ UI ▶ Add CharacterStatsRuntime to Player_MaleHero' first, " +
                                   "then re-run this menu.");
                    return;
                }
                if (contents.GetComponent<PlayerController>() == null)
                {
                    Debug.LogError("[PlayerStaminaPrefabAdder] Player_MaleHero is missing PlayerController. " +
                                   "Re-run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");
                    return;
                }

                contents.AddComponent<PlayerStamina>();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[PlayerStaminaPrefabAdder] Added PlayerStamina to {PrefabPath}. " +
                          "No SerializeField refs to wire — PlayerStamina resolves siblings via GetComponent in Awake.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
