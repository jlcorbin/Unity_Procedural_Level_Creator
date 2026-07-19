// LootSetupBuilder.cs — one-click placeholder loot setup (M-Loot).
//
// Creates Mat 1/2/3 material items + a Grunt loot table, then adds LootDropper
// to Enemy_Grunt.prefab and wires the table. Idempotent: existing material /
// table assets are reused (not overwritten), the table entries are rebuilt, and
// the dropper is added only if missing.
//
// Menu: LevelGen ▶ Combat ▶ Set Up Placeholder Loot (Grunt)
//
// NOTE: if you later rebuild Enemy_Grunt via EnemyBaseBuilder (clean rebuild),
// re-run this to re-add the LootDropper.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen.Items;

namespace LevelGen.Combat.Editor
{
    public static class LootSetupBuilder
    {
        private const string ItemsFolder = "Assets/Data/items";
        private const string LootFolder  = "Assets/Data/Loot";
        private const string TablePath   = "Assets/Data/Loot/LootTable_Grunt.asset";
        private const string GruntPrefab = "Assets/Prefabs/Character Prefabs/Enemy/Enemy_Grunt.prefab";
        // A sample gear drop — any existing weapon ItemData. Skipped if absent.
        private const string SampleWeapon = "Assets/Data/items/ItemData_THS01_Sword.asset";

        [MenuItem("LevelGen/Combat/Set Up Placeholder Loot (Grunt)")]
        public static void SetUp()
        {
            EnsureFolder(ItemsFolder);
            EnsureFolder(LootFolder);

            var m1 = CreateMaterial("mat1", "Mat 1", ItemRarity.Common);
            var m2 = CreateMaterial("mat2", "Mat 2", ItemRarity.Rare);
            var m3 = CreateMaterial("mat3", "Mat 3", ItemRarity.Legendary);
            var weapon = AssetDatabase.LoadAssetAtPath<ItemData>(SampleWeapon);

            var table = BuildGruntTable(m1, m2, m3, weapon);
            AssetDatabase.SaveAssets();

            bool wired = WireDropper(table);

            Debug.Log("[LootSetup] Mat 1/2/3 + LootTable_Grunt ready; LootDropper " +
                      (wired ? "wired on" : "COULD NOT be added to") + " Enemy_Grunt. " +
                      (weapon == null ? "(sample weapon not found — table is materials-only.)" : ""));
            Selection.activeObject = table;
        }

        private static ItemData CreateMaterial(string id, string displayName, ItemRarity rarity)
        {
            string path = $"{ItemsFolder}/ItemData_{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (existing != null) return existing;   // preserve any tuning

            var asset = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(asset, path);

            var so = new SerializedObject(asset);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_kind").enumValueIndex = (int)ItemKind.Material;
            so.FindProperty("_rarity").enumValueIndex = (int)rarity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static LootTable BuildGruntTable(ItemData m1, ItemData m2, ItemData m3, ItemData weapon)
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(TablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            var so = new SerializedObject(table);
            var entries = so.FindProperty("_entries");
            entries.ClearArray();
            AddEntry(entries, m1, 0.80f, 1, 3);   // common mats — frequent
            AddEntry(entries, m2, 0.30f, 1, 2);   // rare mats
            AddEntry(entries, m3, 0.05f, 1, 1);   // legendary mats — rare
            if (weapon != null) AddEntry(entries, weapon, 0.25f, 1, 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private static void AddEntry(SerializedProperty arr, ItemData item, float chance, int min, int max)
        {
            int i = arr.arraySize;
            arr.InsertArrayElementAtIndex(i);
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("item").objectReferenceValue = item;
            e.FindPropertyRelative("dropChance").floatValue = chance;
            e.FindPropertyRelative("minCount").intValue = min;
            e.FindPropertyRelative("maxCount").intValue = max;
        }

        private static bool WireDropper(LootTable table)
        {
            var root = PrefabUtility.LoadPrefabContents(GruntPrefab);
            if (root == null)
            {
                Debug.LogError($"[LootSetup] Enemy_Grunt prefab not found at '{GruntPrefab}'.");
                return false;
            }

            var dropper = root.GetComponent<LootDropper>();
            if (dropper == null) dropper = root.AddComponent<LootDropper>();

            var so = new SerializedObject(dropper);
            so.FindProperty("_lootTable").objectReferenceValue = table;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GruntPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
