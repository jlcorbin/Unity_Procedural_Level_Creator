// WeaponTypeResolver.cs — derives WeaponType from equipped ItemData at swing
// time without requiring a new field on ItemData (M21).
//
// Resolution is prefix-based on the WorldPrefab's GameObject.name:
//   THS*    → WeaponType.THS
//   Spear*  → WeaponType.Spear
//   OHS*    → WeaponType.OHS (or OHSShield if off-hand carries a shield)
//   null / unknown prefix → WeaponType.Unarmed (with warning on unknown)

using UnityEngine;

namespace LevelGen.Items
{
    /// <summary>
    /// Derives a <see cref="WeaponType"/> from the player's equipped
    /// <see cref="ItemData"/> at swing time. Uses a prefix-match on the
    /// melee weapon's <c>WorldPrefab.name</c> so no extra field is needed on
    /// <see cref="ItemData"/>. Call once per swing-start from
    /// <c>PlayerCombat.OnAttackPressed</c>.
    /// </summary>
    public static class WeaponTypeResolver
    {
        /// <summary>
        /// Resolves the <see cref="WeaponType"/> from the player's current
        /// equipment at swing time. Reads the world-prefab name prefix to
        /// determine category — no new field on ItemData required.
        /// </summary>
        /// <param name="meleeItem">
        /// The item in the Melee slot, or null if unarmed.
        /// </param>
        /// <param name="offHandItem">
        /// The item in the OffHand slot, or null if empty.
        /// </param>
        /// <returns>The resolved <see cref="WeaponType"/>.</returns>
        public static WeaponType Resolve(ItemData meleeItem, ItemData offHandItem)
        {
            // 1. No melee weapon → unarmed
            if (meleeItem == null)
                return WeaponType.Unarmed;

            // 2. Derive prefab name (empty string when no WorldPrefab assigned)
            string prefabName = meleeItem.WorldPrefab != null
                ? meleeItem.WorldPrefab.name
                : string.Empty;

            // Strip builder prefix if present
            if (prefabName.StartsWith("WeaponPrefab_"))
            prefabName = prefabName.Substring("WeaponPrefab_".Length);
    
            // 3. Prefix match — THS and Spear before OHS so no overlap risk
            if (prefabName.StartsWith("THS"))
                return WeaponType.THS;

            if (prefabName.StartsWith("Spear"))
                return WeaponType.Spear;

            if (prefabName.StartsWith("OHS"))
            {
                // 4. OHS — promote to OHSShield when an off-hand shield is equipped
                if (offHandItem != null && offHandItem.Slot == EquipSlot.OffHand)
                    return WeaponType.OHSShield;

                return WeaponType.OHS;
            }

            // Unknown prefix — fall back to Unarmed with a warning so designers
            // know the weapon is missing a recognised prefix in its prefab name
            Debug.LogWarning($"[WeaponTypeResolver] Unknown weapon prefix on '{prefabName}' — falling back to Unarmed.");
            return WeaponType.Unarmed;
        }
    }
}
