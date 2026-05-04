// CharacterStatsAssetUpdater.cs — one-shot menu to write the M9 stamina
// rates onto existing CharacterStats_*.asset files.
//
// The new SerializeFields on CharacterStats default to 25 / 33 in code,
// so a fresh asset would already have those values; existing assets,
// however, were authored before the fields existed and need an explicit
// touch to materialize the values in YAML. This menu does that.
//
// Editor-script (rather than YAML edit) per CLAUDE.md convention —
// hand-edited .asset files are fragile.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LevelGen.Combat.EditorTools
{
    public static class CharacterStatsAssetUpdater
    {
        private const string PlayerAssetPath = "Assets/Data/CharacterStats/CharacterStats_Player.asset";
        private const string MasterAssetPath = "Assets/Data/CharacterStats/CharacterStats_Master.asset";
        private const string DummyAssetPath  = "Assets/Data/CharacterStats/CharacterStats_Dummy.asset";

        [MenuItem("LevelGen/Combat/Set Stamina Rates on CharacterStats Assets")]
        public static void SetRates()
        {
            // Player: real gameplay defaults — drain 25/s, regen 33/s.
            // 4s full→empty, ~3s empty→full at maxStamina=100.
            int touched = 0;
            touched += SetRates(PlayerAssetPath, drain: 25f, regen: 33f);

            // Master: template asset — same Player-friendly defaults so
            // any duplicate-and-tweak inherits sensible values.
            touched += SetRates(MasterAssetPath, drain: 25f, regen: 33f);

            // Dummy: doesn't sprint (no PlayerStamina-equivalent on
            // enemies yet). Values are harmless but honest — set both
            // to 0 so an inspector reader sees "no stamina gameplay
            // wired up" at a glance.
            touched += SetRates(DummyAssetPath,  drain: 0f,  regen: 0f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CharacterStatsAssetUpdater] Updated {touched} asset(s).");
        }

        private static int SetRates(string assetPath, float drain, float regen)
        {
            var asset = AssetDatabase.LoadAssetAtPath<CharacterStats>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[CharacterStatsAssetUpdater] {assetPath} not found — skipping.");
                return 0;
            }

            var so = new SerializedObject(asset);
            var drainProp = so.FindProperty("_staminaDrainPerSecond");
            var regenProp = so.FindProperty("_staminaRegenPerSecond");
            if (drainProp == null || regenProp == null)
            {
                Debug.LogError($"[CharacterStatsAssetUpdater] {assetPath} missing one or both serialized rate fields. " +
                               "Compile errors? Re-import scripts and re-run.");
                return 0;
            }
            drainProp.floatValue = drain;
            regenProp.floatValue = regen;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            Debug.Log($"[CharacterStatsAssetUpdater] {assetPath} → drain={drain}/s, regen={regen}/s.");
            return 1;
        }
    }
}
#endif
