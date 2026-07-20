# Session Handoff — 2026-07-19 (end of day)

## Status
- **M22 UE5 player port** — complete, play-verified.
- **Loot pickup loop** — complete, play-verified (drops → E → inventory, mats stack).
- **Inventory UI (Candy Cloud)** — BUILT, binds real data. **Forge = stubbed by
  decision.** Details in `CLAUDE.md` → "M-InventoryUI".

Design source of truth:
`Documentation/Asset and inventory UI design/design_handoff_candy_cloud/README.md`

## Done this session
- Built the **Inventory / Character screen** (real `PlayerInventory` data: gear +
  stacked materials, rarity framing, 5 filter tabs, selection → detail strip,
  stats, I-key toggle, timeScale pause) and the **Forge screen** (full layout +
  both states, **stub data**).
- `RarityPalette` SO (Candy Warm active / Classic Bright fallback), reusable
  `Cell_Item` / `Slot_Equipment` / `Row_ForgeMaterial` prefabs, and the
  `LevelGen ▶ UI ▶ Build Inventory + Forge UI` builder.
- **Cursor in UI:** `MouseLook` gained a counted `RequestUiCursor()` /
  `ReleaseUiCursor()` API so the cursor is usable in menus and isn't stolen back
  by the click-to-relock rule.
- Fixes: 4× CS0618 in `EnemyAINavMeshBaker`; TMP missing-glyph warnings (ASCII
  swap); **LootDropper `DestroyImmediate`-inside-trigger crash** on bow kills.

## ▶ Next session — pick up here
1. **DECISION PENDING → hero preview.** The preview panel is an intentional
   placeholder. To build it for real I need one answer: show a **separate preview
   instance** of the chibi hero (recommended — always idle, well-lit, independent
   of gameplay state) or the **live player model**? Then: preview layer + model,
   camera → RenderTexture, RawImage, drag-to-rotate.
2. **Scene cleanup for the new UI** (quick, do first):
   - Place `Assets/Prefabs/UI/Inventory/InventoryScreen.prefab` in the test scene.
   - **Delete the old M18 `InventoryPanel` + `InventoryHUD`** objects — they also
     listen to the I key and will double-toggle.
   - Ensure the scene has an **EventSystem** created via `GameObject ▶ UI ▶ Event
     System` (a programmatically-added `InputSystemUIInputModule` has no actions
     asset and is inert — M5 lesson). Without it, UGUI clicks do nothing.
3. **Polish passes available:** import **Fredoka** + **Nunito** as TMP font
   assets (then the real ✓/✕/×/· glyphs can be restored — every site is
   commented); author `ItemData.Icon` sprites (cells currently show initials);
   optional bag **sorting**; optional toggle to quiet the
   `[CharacterStatsRuntime] HP ...` combat logs.
4. **Forge real data** ← the big one. Needs the **weapon-instance model**:
   `{template: ItemData, level, baseDamage, damagePerLevel}` + derived
   currentDamage/rarity + a per-level material cost table. Then build a
   `ForgeScreen.ForgeViewModel` from it and call **`Show(vm)`** — that single
   seam is the only integration point; no other UI change needed. Also unlocks
   the real Upgrade action (spend materials via `PlayerInventory.SpendMaterial`,
   level+1, recompute damage/rarity).

## Load-bearing gotchas (don't undo)
- **Input:** LockOn (RMB) + SwitchStance (Q) use DIRECT action subscription in
  `PlayerInputReader`, NOT UnityEvent wiring.
- **Blend trees** use the float `StanceBlend` param, not the int `WeaponType`.
- **Melee damage** needs OnHitboxOpen/Close events on clips — re-run
  `Add Hitbox Events to Stance Attack Clips` after any clip reimport.
- **Equipping is UI-later:** `WorldItem._autoEquipOnPickup` is OFF; pickup only bags.
- **LootDropper is dropped by an EnemyBaseBuilder clean rebuild** — re-run
  `Set Up Placeholder Loot (Grunt)` after rebuilding Enemy_Grunt.
- **Never `DestroyImmediate` in a physics/animation callback** — the loot block is
  built without a default collider specifically to avoid needing it.
- Real loot art later = spawn `item.WorldPrefab` instead of the block in
  `LootDropper.SpawnPickup` (one line).

## Earlier deferrals still standing
- M22: `StanceDevCycler`/Q kept (retire near ship); corrective off-hand mount
  deferred until off-hand items are equipped via inventory.
- Spec: enemy-side parity, held-draw pose, wand VFX, lock-on bracket UI.
- Loot: drop animations/juice, loot on other enemy types, auto-pickup, currency.
