// PlayerDeathPrefabAdder.cs — M5 prefab wiring (one-shot path).
//
// Single menu item:
//   LevelGen ▶ Player ▶ Add PlayerDeath to Player_MaleHero Prefab
//
// Idempotent — adds PlayerDeath to the prefab root iff it isn't
// already there, then explicitly wires the three SerializeField
// references (_animator, _controller, _combat) via SerializedObject.
//
// Mirrors PlayerCombatPrefabAdder's structure but with explicit
// field wiring (PlayerDeath has refs to siblings; PlayerCombat
// only had inspector-default values).
//
// Use this menu when:
//   - The user shipped M2-B / M2-C without M5 and wants to add
//     M5 without re-running BuildPlayerMaleHeroPrefab.
//   - PlayerCombatPrefabAdder was run after BuildPlayerMaleHeroPrefab
//     and PlayerDeath._combat needs re-pointing now that PlayerCombat
//     finally exists on the prefab root.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerDeathPrefabAdder
    {
        private const string PrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Add PlayerDeath to Player_MaleHero Prefab")]
        public static void Run()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[PlayerDeathPrefabAdder] Prefab not found at {PrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var death = contents.GetComponent<PlayerDeath>();
                bool addedNow = false;
                if (death == null)
                {
                    death = contents.AddComponent<PlayerDeath>();
                    if (death == null)
                    {
                        Debug.LogError("[PlayerDeathPrefabAdder] AddComponent<PlayerDeath>() returned null. Aborting save.");
                        return;
                    }
                    addedNow = true;
                }

                var animator   = contents.GetComponent<PlayerAnimator>();
                var controller = contents.GetComponent<PlayerController>();
                var combat     = contents.GetComponent<PlayerCombat>();

                // Warn but don't abort if PlayerCombat is missing — the user
                // may not have run PlayerCombatPrefabAdder yet. They can
                // re-run this menu after PlayerCombat lands.
                if (combat == null)
                    Debug.LogWarning("[PlayerDeathPrefabAdder] PlayerCombat is not on the prefab. " +
                                     "_combat will be left unassigned. Run " +
                                     "'LevelGen ▶ Player ▶ Add PlayerCombat to Player_MaleHero Prefab' " +
                                     "first, then re-run this menu to wire the ref.");

                if (animator == null)
                    Debug.LogError("[PlayerDeathPrefabAdder] PlayerAnimator missing on the prefab. " +
                                   "Field will be unassigned — death sequence won't play. " +
                                   "Re-build the player prefab via 'Build Player_MaleHero Prefab'.");

                if (controller == null)
                    Debug.LogError("[PlayerDeathPrefabAdder] PlayerController missing on the prefab. " +
                                   "Field will be unassigned — controller won't be disabled on death.");

                PlayerPrefabBuilder.AssignPlayerDeathRefs(death, animator, controller, combat);

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);

                string animState   = animator   != null ? "OK" : "MISSING";
                string ctrlState   = controller != null ? "OK" : "MISSING";
                string combatState = combat     != null ? "OK" : "MISSING (re-run after Combat adder)";
                Debug.Log($"[PlayerDeathPrefabAdder] {(addedNow ? "Added" : "Re-wired")} PlayerDeath on {PrefabPath}.\n" +
                          $"  _animator:   {animState}\n" +
                          $"  _controller: {ctrlState}\n" +
                          $"  _combat:     {combatState}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
#endif
