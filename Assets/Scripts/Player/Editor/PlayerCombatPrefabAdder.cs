// PlayerCombatPrefabAdder.cs — M2-B Step 3 prefab wiring.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Add PlayerCombat to Player_MaleHero Prefab
//
// Idempotent — adds PlayerCombat to the prefab root iff it isn't already
// there. Inspector defaults from PlayerCombat's SerializeField values are
// sufficient; no per-field wiring required.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerCombatPrefabAdder
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Add PlayerCombat to Player_MaleHero Prefab")]
        public static void Run()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[PlayerCombatPrefabAdder] Prefab not found at {PrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (contents.GetComponent<PlayerCombat>() != null)
                {
                    Debug.Log("[PlayerCombatPrefabAdder] PlayerCombat already present on Player_MaleHero — no change.");
                    return;
                }

                var combat = contents.AddComponent<PlayerCombat>();
                if (combat == null)
                {
                    Debug.LogError("[PlayerCombatPrefabAdder] AddComponent<PlayerCombat>() returned null. Aborting save.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[PlayerCombatPrefabAdder] PlayerCombat added to {PrefabPath}. Defaults: comboWindowOpen=0.40, comboWindowClose=0.80, bufferConsumeAt=0.85.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
#endif
