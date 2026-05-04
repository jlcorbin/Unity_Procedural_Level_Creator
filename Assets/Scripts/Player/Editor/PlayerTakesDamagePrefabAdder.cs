// PlayerTakesDamagePrefabAdder.cs — one-shot adders for the M11 player-side
// hit-receive components. Two menu items folded into one file because they
// must run in order (PlayerHitReaction [RequireComponent]s Targetable):
//
//   LevelGen ▶ Player ▶ Add Targetable to Player_MaleHero Prefab
//   LevelGen ▶ Player ▶ Add PlayerHitReaction to Player_MaleHero Prefab
//
// Both are idempotent. Mirrors the M5 PlayerDeathPrefabAdder pattern
// (LoadPrefabContents → check-and-add → SaveAsPrefabAsset).
//
// No SerializeField references on either component need wiring —
// Targetable resolves AimPoint via child name lookup at Awake;
// PlayerHitReaction resolves Targetable + PlayerCombat +
// CharacterStatsRuntime via GetComponent in Awake.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player.Editor
{
    public static class PlayerTakesDamagePrefabAdder
    {
        private const string PrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        // ════════════════════════════════════════════════════════════════════
        // Menu: add Targetable to Player_MaleHero
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Player/Add Targetable to Player_MaleHero Prefab")]
        private static void AddTargetable()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[PlayerTakesDamagePrefabAdder] {PrefabPath} not found. " +
                               "Run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[PlayerTakesDamagePrefabAdder] LoadPrefabContents failed for {PrefabPath}.");
                return;
            }

            try
            {
                if (contents.GetComponent<Targetable>() != null)
                {
                    Debug.Log("[PlayerTakesDamagePrefabAdder] Targetable already on Player_MaleHero — no change.");
                    return;
                }

                contents.AddComponent<Targetable>();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[PlayerTakesDamagePrefabAdder] Added Targetable to {PrefabPath}. " +
                          "Now run 'LevelGen ▶ Player ▶ Add PlayerHitReaction to Player_MaleHero Prefab'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu: add PlayerHitReaction to Player_MaleHero
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Player/Add PlayerHitReaction to Player_MaleHero Prefab")]
        private static void AddPlayerHitReaction()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[PlayerTakesDamagePrefabAdder] {PrefabPath} not found. " +
                               "Run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[PlayerTakesDamagePrefabAdder] LoadPrefabContents failed for {PrefabPath}.");
                return;
            }

            try
            {
                if (contents.GetComponent<PlayerHitReaction>() != null)
                {
                    Debug.Log("[PlayerTakesDamagePrefabAdder] PlayerHitReaction already on Player_MaleHero — no change.");
                    return;
                }

                // [RequireComponent(Targetable)] — Unity refuses the AddComponent
                // silently if Targetable isn't present yet. Bail early with a
                // clear message instead.
                if (contents.GetComponent<Targetable>() == null)
                {
                    Debug.LogError("[PlayerTakesDamagePrefabAdder] Player_MaleHero is missing Targetable. " +
                                   "Run 'LevelGen ▶ Player ▶ Add Targetable to Player_MaleHero Prefab' first, " +
                                   "then re-run this menu.");
                    return;
                }
                if (contents.GetComponent<PlayerCombat>() == null)
                {
                    Debug.LogError("[PlayerTakesDamagePrefabAdder] Player_MaleHero is missing PlayerCombat. " +
                                   "Run 'LevelGen ▶ Player ▶ Add PlayerCombat to Player_MaleHero Prefab' first.");
                    return;
                }

                contents.AddComponent<PlayerHitReaction>();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[PlayerTakesDamagePrefabAdder] Added PlayerHitReaction to {PrefabPath}. " +
                          "No SerializeField refs to wire — resolves siblings via GetComponent in Awake.");
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
