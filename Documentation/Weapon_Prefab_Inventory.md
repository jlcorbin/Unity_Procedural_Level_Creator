# Weapon Prefab Inventory
*Generated: 2026-05-13*
*Source: `Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Prefab/Weapons/`*
*Do not edit manually — re-run `M16_WeaponPrefab_Inventory.md` CC prompt to regenerate.*

---

## Naming Conventions

| Prefix | Meaning |
|--------|---------|
| `OHS##` | One-Handed weapon (Sword / Axe / Hammer / Stick / Niddle) |
| `THS##` | Two-Handed Sword |
| `Spear##` | Spear / Polearm |
| `Shield##` | Shield (OffHand slot) |
| `Wand##` | Wand / Staff (Ranged slot) |
| `Bows` | Bow (Ranged slot) |
| `Arrows` | Arrows (Ranged slot) |

> **Note:** `Niddle` is the publisher's typo for "Needle" — a rapier/dagger-like one-handed weapon. `ItemData._id` authored as `needle_rapier`.

---

## Authoring Rule

Author all `ItemData` assets against the **Wave PBR prefabs** (canonical set).
The four Duo copies (`OHS03PBR`, `OHS06PBR`, `Shield05PBR`, `Shield08PBR`) exist only
to satisfy `MaleCharacterPBR.prefab` nested-instance references — **do not create
duplicate ItemData assets for them.**

---

## One-Handed Melee (OHS) — 16 items

Base path: `Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Prefab/Weapons/`

| Prefab Name | Slot | ItemData ID | Display Name |
|-------------|------|-------------|--------------|
| OHS01_Stick | Melee | `wooden_stick` | Wooden Stick |
| OHS02_Niddle | Melee | `needle_rapier` | Needle Rapier |
| OHS03_Sword | Melee | `iron_sword` | Iron Sword |
| OHS04_Sword | Melee | `bronze_sword` | Bronze Sword |
| OHS05_Sword | Melee | `steel_sword` | Steel Sword |
| OHS06_Sword | Melee | `knight_sword` | Knight Sword |
| OHS07_Sword | Melee | `noble_sword` | Noble Sword |
| OHS08_Sword | Melee | `ornate_sword` | Ornate Sword |
| OHS09_Sword | Melee | `royal_sword` | Royal Sword |
| OHS10_Axe | Melee | `hand_axe` | Hand Axe |
| OHS11_Hammer | Melee | `war_hammer` | War Hammer |
| OHS12_Sword | Melee | `curved_sword` | Curved Sword |
| OHS13_Axe | Melee | `battle_axe` | Battle Axe |
| OHS14_Hammer | Melee | `heavy_hammer` | Heavy Hammer |
| OHS15_Sword | Melee | `dark_sword` | Dark Sword |
| OHS16_Sword | Melee | `light_sword` | Light Sword |

---

## Two-Handed Swords (THS) — 7 items

| Prefab Name | Slot | ItemData ID | Display Name |
|-------------|------|-------------|--------------|
| THS01_Sword | Melee | `claymore` | Claymore |
| THS02_Sword | Melee | `greatsword` | Greatsword |
| THS03_Sword | Melee | `zweihander` | Zweihander |
| THS04_Sword | Melee | `flamberge` | Flamberge |
| THS05_Sword | Melee | `executioner_sword` | Executioner Sword |
| THS06_Sword | Melee | `katana` | Katana |
| THS07_Sword | Melee | `dark_blade` | Dark Blade |

---

## Spears — 5 items

| Prefab Name | Slot | ItemData ID | Display Name |
|-------------|------|-------------|--------------|
| Spear01 | Melee | `wooden_spear` | Wooden Spear |
| Spear02 | Melee | `iron_spear` | Iron Spear |
| Spear03 | Melee | `steel_spear` | Steel Spear |
| Spear04 | Melee | `pike` | Pike |
| Spear05 | Melee | `royal_halberd` | Royal Halberd |

---

## Shields / Off-Hand — 20 items

| Prefab Name | Slot | ItemData ID | Display Name |
|-------------|------|-------------|--------------|
| Shield01 | OffHand | `wooden_buckler` | Wooden Buckler |
| Shield02 | OffHand | `round_shield` | Round Shield |
| Shield03 | OffHand | `iron_shield` | Iron Shield |
| Shield04 | OffHand | `kite_shield` | Kite Shield |
| Shield05 | OffHand | `heater_shield` | Heater Shield |
| Shield06 | OffHand | `tower_shield` | Tower Shield |
| Shield07 | OffHand | `crusader_shield` | Crusader Shield |
| Shield08 | OffHand | `knight_shield` | Knight Shield |
| Shield09 | OffHand | `noble_shield` | Noble Shield |
| Shield10 | OffHand | `royal_shield` | Royal Shield |
| Shield11 | OffHand | `ornate_shield` | Ornate Shield |
| Shield12 | OffHand | `dragon_shield` | Dragon Shield |
| Shield13 | OffHand | `lion_shield` | Lion Shield |
| Shield14 | OffHand | `eagle_shield` | Eagle Shield |
| Shield15 | OffHand | `wolf_shield` | Wolf Shield |
| Shield16 | OffHand | `dark_shield` | Dark Shield |
| Shield17 | OffHand | `light_shield` | Light Shield |
| Shield18 | OffHand | `arcane_shield` | Arcane Shield |
| Shield19 | OffHand | `holy_shield` | Holy Shield |
| Shield20 | OffHand | `aegis` | Aegis |

---

## Ranged (Bows, Arrows, Wands) — 9 items

| Prefab Name | Slot | ItemData ID | Display Name |
|-------------|------|-------------|--------------|
| Bows | Ranged | `bow` | Bow |
| Arrows | Ranged | `arrows` | Arrows |
| Wand01 | Ranged | `apprentice_wand` | Apprentice Wand |
| Wand02 | Ranged | `mage_wand` | Mage Wand |
| Wand03 | Ranged | `crystal_wand` | Crystal Wand |
| Wand04 | Ranged | `fire_wand` | Fire Wand |
| Wand05 | Ranged | `ice_wand` | Ice Wand |
| Wand06 | Ranged | `shadow_wand` | Shadow Wand |
| Wand07 | Ranged | `arcane_staff` | Arcane Staff |

---

## RPG Tiny Hero Duo — Overlapping Prefabs

These 4 prefabs are duplicates of Wave PBR entries by GUID family.
**Do not author separate ItemData assets for these.**

| Duo Prefab | Wave PBR Equivalent | Notes |
|------------|---------------------|-------|
| OHS03PBR | OHS03_Sword | Used as nested PrefabInstance by MaleCharacterPBR.prefab |
| OHS06PBR | OHS06_Sword | Duo-renamed copy |
| Shield05PBR | Shield05 | Duo-renamed copy |
| Shield08PBR | Shield08 | Used by MaleCharacterPBR.prefab |

---

## Summary

| Slot | Count |
|------|-------|
| Melee (OHS + THS + Spear) | 28 |
| OffHand (Shields) | 20 |
| Ranged (Bow + Arrows + Wands) | 9 |
| **Total Wave PBR** | **57** |
| Duo duplicates (do not author) | 4 |