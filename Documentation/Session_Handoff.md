# Session Handoff — 2026-07-19

## Status: M22 port done. Loot PICKUP loop done + play-verified. UI design in progress.

The UE5 player port (M22), bow/arrow separation, and cleanup are complete
(see `CLAUDE.md`). This session added the **first half of the loot/equip/upgrade
loop** — enemies drop items, player picks them up, materials stack. Details in
`CLAUDE.md` under "M-Loot — pickup loop (2026-07-19)".

## Done this session
- **Loot pickup loop (placeholder blocks).** Enemies roll a `LootTable` on death
  and drop runtime-tinted primitives (**cube=gear, sphere=material,
  color=rarity**) carrying `WorldItem`s. E to pick up; materials stack; gear
  bags. `LevelGen ▶ Combat ▶ Set Up Placeholder Loot (Grunt)` wires it all.
  Play-verified on Enemy_Grunt. Odds kept as-is (Mat1 80%, Mat2 30%, Mat3 5%,
  weapon 25%).
- **Data foundations:** `ItemKind {Gear, Material}`, `Legendary` rarity,
  `RarityColors`, material stacking in `PlayerInventory`, `WorldItem.Initialize`
  + count, auto-equip gated OFF (equip is UI-later).

## The big picture — where the loot/equip/upgrade loop stands
The loop is: **kill → collect gear + materials → equip gear → upgrade weapons
with materials → repeat.** We've built **collect**. Still to build:
1. **Inventory + upgrade UI** — DESIGN IN PROGRESS. Brief:
   `Documentation/Inventory_UI_Design_Handoff.md`; references: `Assets/images/`.
   Jason is getting mockups from a design session; when they land, CC implements
   them in Unity UGUI and wires to `PlayerInventory`.
2. **Weapon INSTANCES + leveling + the upgrade action.** ← the key remaining
   foundation. Right now `ItemData` is a shared SO, so a weapon can't carry its
   own +level. Before/with the upgrade UI we need a runtime **item-instance**
   model `{template: ItemData, level, ...}`; inventory becomes a list of
   instances. Upgrade model (decided): Weapon + materials → +1, linear damage;
   escalating cost + higher-tier "special" mats at higher levels; **rarity is
   driven by level** (Sword = Common … Sword +3 = Legendary). Materials:
   Mat 1/2/3 = Common/Rare/Legendary.

## ▶ Likely next steps (Jason picks)
- **Wait for UI mockups**, then implement the inventory/upgrade screens.
- OR build the **item-instance + weapon-leveling + upgrade logic** foundation
  now (headless, testable via Console/`SpendMaterial`), so the UI has something
  real to bind to when it arrives.
- Smaller: loot on other enemy types, drop animation/juice, auto-pickup.

## Load-bearing gotchas (don't undo)
- **Equipping is UI-later:** `WorldItem._autoEquipOnPickup` defaults OFF. Pickup
  only bags items. Q dev-cycle + the equip API still work for testing.
- **LootDropper is dropped by an EnemyBaseBuilder clean rebuild** — re-run
  `Set Up Placeholder Loot (Grunt)` after rebuilding Enemy_Grunt.
- **Materials are stacks, gear is unique** — `PlayerInventory` keeps them in
  separate structures; don't merge them without the item-instance refactor.
- Real loot art later = spawn `item.WorldPrefab` instead of the primitive in
  `LootDropper.SpawnPickup` (one line).

## Earlier deferrals still standing
- M22: `StanceDevCycler`/Q kept (retire near ship); corrective off-hand mount
  deferred until off-hand items are equipped via inventory.
- Spec: enemy-side parity, held-draw pose, wand VFX, lock-on bracket UI.
