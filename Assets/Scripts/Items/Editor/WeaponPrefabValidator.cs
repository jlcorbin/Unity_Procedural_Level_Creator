// WeaponPrefabValidator.cs — read-only validator for all 57 weapon prefabs (M20c).
//
// Verifies that every weapon in the AllWeapons table has:
//   1. A WeaponPrefab_{PrefabName}.prefab at Assets/Prefabs/Weapons/
//   2. An ItemData_{PrefabName}.asset at Assets/Data/Items/ with a non-null
//      _worldPrefab field pointing at the weapon prefab
//
// Read-only — no asset writes, no scene modifications.
//
// Menu: LevelGen ▶ Weapons ▶ Validate Weapon Prefabs
//
// The weapon table is owned by WeaponPrefabBuilder.AllWeapons (internal static).
// This file reads that table so both scripts share a single source of truth —
// adding a new weapon to the builder automatically extends this validator.
//
// Expected output: [WeaponPrefabValidator] X PASS / Y FAIL out of 114 checks
// (57 weapons × 2 checks each = 114 total)

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen.Items;

namespace LevelGen.Items.Editor
{
    /// <summary>
    /// Read-only validator that confirms every weapon in
    /// <see cref="WeaponPrefabBuilder.AllWeapons"/> has a corresponding prefab
    /// and <see cref="ItemData"/> asset on disk.
    /// </summary>
    public static class WeaponPrefabValidator
    {
        [MenuItem("LevelGen/Weapons/Validate Weapon Prefabs")]
        public static void Run()
        {
            int pass = 0, fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok)
                {
                    pass++;
                    Debug.Log($"[WeaponPrefabValidator] PASS — {label}: {detail}");
                }
                else
                {
                    fail++;
                    Debug.LogError($"[WeaponPrefabValidator] FAIL — {label}: {detail}");
                }
            }

            int weaponIndex = 0;
            foreach (var w in WeaponPrefabBuilder.AllWeapons)
            {
                weaponIndex++;
                string weaponPrefabName = $"WeaponPrefab_{w.PrefabName}";
                string prefabPath       = $"{WeaponPrefabBuilder.PrefabFolder}/{weaponPrefabName}.prefab";
                string itemDataPath     = $"{WeaponPrefabBuilder.ItemDataFolder}/ItemData_{w.PrefabName}.asset";

                // ── Check 1: WeaponPrefab_{PrefabName}.prefab exists on disk ──

                var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Check(
                    $"{weaponIndex * 2 - 1} {w.PrefabName} prefab exists",
                    weaponPrefab != null,
                    weaponPrefab != null
                        ? prefabPath
                        : $"missing at {prefabPath} — run 'Build All Weapon Prefabs'");

                // ── Check 2: ItemData exists and _worldPrefab is non-null ──────

                var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(itemDataPath);
                if (itemData == null)
                {
                    Check(
                        $"{weaponIndex * 2} {w.PrefabName} ItemData exists + _worldPrefab wired",
                        false,
                        $"ItemData missing at {itemDataPath} — run 'Build All Weapon Prefabs'");
                }
                else
                {
                    // Read _worldPrefab via SerializedObject to avoid relying on the
                    // public property (which is expression-bodied and might not
                    // reflect stale serialised values before a domain reload).
                    var so = new SerializedObject(itemData);
                    var worldPrefabProp = so.FindProperty("_worldPrefab");
                    bool worldPrefabWired = worldPrefabProp != null
                                        && worldPrefabProp.objectReferenceValue != null;
                    Check(
                        $"{weaponIndex * 2} {w.PrefabName} ItemData exists + _worldPrefab wired",
                        worldPrefabWired,
                        worldPrefabWired
                            ? $"{itemDataPath} → {worldPrefabProp.objectReferenceValue.name}"
                            : $"{itemDataPath} found but _worldPrefab is null — re-run 'Build All Weapon Prefabs'");
                }
            }

            // ── Summary ────────────────────────────────────────────────────────
            int totalChecks = WeaponPrefabBuilder.AllWeapons.Length * 2;
            if (fail == 0)
                Debug.Log($"[WeaponPrefabValidator] {pass} PASS / {fail} FAIL out of {totalChecks} checks — all weapons validated.");
            else
                Debug.LogError($"[WeaponPrefabValidator] {pass} PASS / {fail} FAIL out of {totalChecks} checks — {fail} issue(s) found.");
        }
    }
}
#endif
