// PlayerCapsuleTuner.cs — one-shot bump of Player CharacterController.radius
// from 0.3 → 0.4 for symmetric combat reach (M11 Q5).
//
// The CharacterController is the Player's authoritative collision shape.
// Unity treats it as non-static, so OnTriggerEnter from the Dummy's
// EnemyWeaponHitbox fires correctly against it without a separate
// CapsuleCollider. Bumping the radius widens the hit-reception window
// to match the Dummy's CapsuleCollider radius (0.4), giving consistent
// hits across the Attack01 swing arc.
//
// Tradeoff: marginally more catch-on-corner behavior in tight geometry.
// Documented in CLAUDE.md / Session_Handoff.md "Things to leave alone"
// for revisit if level-gen environments make movement feel sticky.
//
// Idempotent: re-running with radius >= 0.4 logs "already tuned, skipping"
// and exits without modifying the prefab.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class PlayerCapsuleTuner
    {
        private const string PrefabPath  = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const float  TargetRadius = 0.4f;

        [MenuItem("LevelGen/Player/Tune CharacterController for Hit Reception")]
        private static void TuneCharacterController()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[PlayerCapsuleTuner] {PrefabPath} not found. " +
                               "Run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[PlayerCapsuleTuner] LoadPrefabContents failed for {PrefabPath}.");
                return;
            }

            try
            {
                var cc = contents.GetComponent<CharacterController>();
                if (cc == null)
                {
                    Debug.LogError("[PlayerCapsuleTuner] Player_MaleHero is missing CharacterController. " +
                                   "Re-run 'LevelGen ▶ Player ▶ Build Player_MaleHero Prefab' first.");
                    return;
                }

                // SerializedObject path (asset-edit mode requires this for the
                // change to mark the prefab dirty for SaveAsPrefabAsset).
                var so   = new SerializedObject(cc);
                var prop = so.FindProperty("m_Radius");
                if (prop == null)
                {
                    Debug.LogError("[PlayerCapsuleTuner] CharacterController has no 'm_Radius' serialized property. " +
                                   "Unity API change?");
                    return;
                }

                float current = prop.floatValue;
                if (current >= TargetRadius)
                {
                    Debug.Log($"[PlayerCapsuleTuner] Player CharacterController.radius={current:F3} " +
                              $">= {TargetRadius} — already tuned, skipping.");
                    return;
                }

                prop.floatValue = TargetRadius;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cc);

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log($"[PlayerCapsuleTuner] Player_MaleHero CharacterController.radius " +
                          $"{current:F3} → {TargetRadius} (M11 Q5: symmetric combat reach with Dummy CapsuleCollider).");
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
