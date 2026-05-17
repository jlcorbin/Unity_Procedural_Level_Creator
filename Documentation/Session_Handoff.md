# Session Handoff — 2026-05-17

## 1. Session Summary

What shipped this session:

- **M20 — Visual Weapon Mesh Swap**: `PlayerEquipmentVisuals.cs` (new) — subscribes to `PlayerInventory.OnWeaponEquipped`, destroys existing `weapon_r` children on equip/unequip, instantiates `ItemData.WorldPrefab` as child under `weapon_r`. Inspector `_weaponSocket` ref (drag weapon_r bone). `PlayerHero` gained `[RequireComponent]` + `_equipmentVisuals` ref. `PlayerHeroBuilder` and `PlayerHeroValidator` updated (checks 64–67 added, 67 total). Player starts with empty hand — no sword baked into prefab.

- **M20b — Weapon Prefab as Hitbox**: Static WeaponHitbox child removed from prefab. `PlayerCombat.hitbox` is now a runtime-assigned `public Collider Hitbox` property (no longer SerializeField). `PlayerEquipmentVisuals` wires `PlayerCombat.Hitbox` and `HitboxRelay.Combat` at runtime on equip, clears on unequip. `HitboxRelay` gained public `Combat` get/set property. `OnHitboxOpen` logs warning (not error) when unarmed. Validator check 41 retired with self-documenting label. Every weapon WorldPrefab must carry: BoxCollider (isTrigger=true, disabled), kinematic Rigidbody, HitboxRelay (Combat=null in prefab, wired at runtime).

- **M20c — Full Weapon Library**: `WeaponPrefabBuilder.cs` rebuilt — 57 weapons across OHS (16), THS (7), Spear (5), Shield (20), Ranged (9). Mesh source fixed to use publisher's textured `Prefab/Weapons/` prefabs instead of raw FBX. Menu collapsed to two items: `Build All Weapon Prefabs` + `Validate Weapon Prefabs`. `WeaponPrefabValidator.cs` (new) — 114 checks (57 × prefab exists + ItemData exists with non-null worldPrefab). All 114 PASS confirmed. All 57 `ItemData` assets created in `Assets/Data/Items/`. All 57 `WeaponPrefab_*.prefab` assets created in `Assets/Prefabs/Weapons/`. OHS04 ItemData recreated fresh with canonical id `bronze_sword` / "Bronze Sword". Scene pickup wired to `ItemData_OHS04_Sword`.

## 2. Validator State Table

| Validator | Checks | Status |
|-----------|--------|--------|
| LevelGen ▶ Player ▶ Validate Player_Hero | 67 | PASS |
| LevelGen ▶ Weapons ▶ Validate Weapon Prefabs | 114 | PASS |
| LevelGen ▶ Combat ▶ Validate Enemy | last known 49 | not re-run this session |
| LevelGen ▶ Interaction ▶ Validate Interaction | last known 37 | not re-run this session |

## 3. Deferred / Known Issues

- Collider sizes on all 57 weapon prefabs are first-pass estimates — need per-weapon tuning in Prefab Mode (especially shields, spears, staves).
- `EnemyHealthBar` editor wiring on `Enemy_Grunt` prefab — pending since M14.
- Item icons on `ItemData` assets (Sprite field blank) — deferred.
- Armor slot not yet in HUD or InventoryPanel.
- Visual weapon pivot/rotation may need per-weapon offset tweaks once animation is playing (sword may sit at wrong angle in hand during swings).

## 4. Open Milestone Candidates

| Milestone | Description | Recommended? |
|-----------|-------------|--------------|
| M19 | Enemy AI — patrol, alert states, group awareness | |
| M21 | Armor slot — HUD strip, InventoryPanel, EquipSlot | |
| M22 | Loot drops — EnemyData.lootTable → spawn WorldItem on death | |
| M23 | Weapon pivot/rotation tuning pass — per-weapon offset on mesh child | |

**Recommended next:** M19 (Enemy AI) for gameplay depth, or M22 (Loot drops) to close the full pickup→equip→drop loop. M22 is shorter and the hooks already exist on EnemyData.

## 5. Architectural Reminders

- `PlayerEquipmentVisuals` owns `weapon_r` socket entirely at runtime. Never add children to `weapon_r` in the prefab — it will be destroyed on first equip.
- `PlayerCombat.Hitbox` is runtime-assigned. It is null when unarmed — `OnHitboxOpen` warning is expected and not a fault.
- Every weapon WorldPrefab: BoxCollider (isTrigger, disabled) + kinematic Rigidbody + HitboxRelay (Combat=null). This is the authoring standard for ALL equipment: melee, shields, wands, ranged.
- `HitboxRelay.Combat` is wired by `PlayerEquipmentVisuals` at runtime — never pre-fill it in the prefab.
- `PlayerInputReader` uses UnityEvent dispatch only. No `_input.Player.X.performed`.
- `PlayerHero` and `EnemyBase` are wiring manifests only — no logic.
- All damage flows through `CharacterStatsRuntime.ApplyDamage`.

## 6. Quick-Start for Next Session

Paste this at the start of the next chat:

```
# Hub & Hollow — Session Open

Read in order:
1. CLAUDE.md
2. Documentation/Session_Handoff.md

M20/M20b/M20c (Visual weapon mesh swap + weapon-prefab-as-hitbox +
full weapon library) shipped last session. 67/67 player validator,
114/114 weapon validator. Recommended next: M19 (Enemy AI) or M22
(Loot drops on enemy death).
```
