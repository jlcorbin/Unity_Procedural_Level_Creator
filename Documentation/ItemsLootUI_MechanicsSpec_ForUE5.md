# Items, Loot & Inventory UI — Mechanics Spec (engine-agnostic)

**Purpose:** the mirror of `PlayerCharacter_MechanicsSpec_ForUnity.md`, pointing the other way. That spec described the UE5 player so Unity could rebuild it (shipped as M22). **This one describes everything the Unity project has that UE5 does not**, so UE5 can be brought to parity. It describes **what the systems do and the values that drive them** — not Unity component wiring. Unity file paths are given for direct lookup.

Values pulled live from the project on 2026-07-25.

---

## 0. Scope — what "added after the fact" means here

The UE5 port (M22) moved the **player character only**: locomotion, camera, 8 stances, melee combo, ranged charge/release, dodge, target lock. Everything below is game content the UE5 project has **no equivalent for**, because in UE5 a stance owns a hard-coded weapon mesh and there are no items at all.

**IN scope (this document):**

| System | Unity milestone | Built |
|---|---|---|
| Item data layer (ItemData / rarity / kinds / slots) | M16, extended M-Loot | ✔ |
| Player inventory + equip (gear list, material stacks) | M16 / M17 / M-Loot | ✔ |
| World pickup + generic interaction prompt | M6 / M16 | ✔ |
| Loot tables + on-death drops | M-Loot | ✔ |
| Equip → weapon mesh mount (+ weapon-as-hitbox) | M20 / M20b | ✔ |
| Equip → damage source | M17 | ✔ |
| Equip → **stance** bridge (ties items to the ported stance system) | M22 coexist | ✔ |
| Inventory / Character screen ("Candy Cloud") | M-InventoryUI | ✔ binds real data |
| Weapon Forge screen | M-InventoryUI | ⚠ **layout real, data stubbed** |
| Weapon-instance / upgrade model | — | ✘ **not built in either engine** |

Items 1–7 mostly predate the port on the Unity calendar but were never ported — they are listed because §6's consumers and §7's UI cannot exist without them. Port order is in §12.

**OUT of scope:** the player character itself (covered by the earlier spec), enemies/AI/health (UE5 has its own — only the *death event* is needed, §5), the procedural level generator, damage numbers, HUD bars.

---

## 1. Core design pillars

1. **Items are pure data.** One asset per archetype; zero logic on the asset. Every behaviour lives in a consumer that reads it.
2. **Two item kinds.** **Gear** is unique and occupies one of four equipment slots. **Material** is a stackable upgrade resource that is never equipped.
3. **Rarity is a 4-tier ladder** (Common → Uncommon → Rare → Legendary) that drives loot value, every UI colour, and — once the upgrade system lands — the progression a weapon climbs as it levels.
4. **One equip event, many consumers.** Equipping fires a single event; the weapon mesh mount, the damage source, the animation stance, and the UI all react independently. Nothing chains through anything else.
5. **Pickup is an interaction, not a walk-over.** Loot goes through the same generic prompt system as doors and assassinations, so priority arbitration is free.

---

## 2. Item data model

One asset per item archetype (`Assets/Data/items/ItemData_*.asset`).

| Field | Type | Meaning |
|---|---|---|
| `Id` | string | Stable primary key (snake_case, e.g. `ohs04_sword`). Never change after save data exists. |
| `DisplayName` | string | UI name. |
| `Description` | string | Tooltip flavour text. |
| `Kind` | enum | `Gear` \| `Material`. |
| `Slot` | enum | `Melee` \| `OffHand` \| `Ranged` \| `Armor`. Gear only. |
| `Damage` | int | Base damage. Read by the melee swing (§6b). |
| `RequiredLevel` | int | 0 = no requirement. **Authored but not enforced** — no player-level system exists. |
| `Icon` | sprite | Inventory icon. **Currently unset on every asset** — UI falls back to name initials. |
| `WorldPrefab` | prefab ref | The mesh mounted on equip *and* the intended world pickup art. |
| `Rarity` | enum | `Common` \| `Uncommon` \| `Rare` \| `Legendary`. |

`IsMaterial` is derived (`Kind == Material`).

**Weapon damage by category** (defaults stamped on the 57 generated weapon assets):

| Category | Slot | Damage |
|---|---|---|
| OHS (one-hand sword) | Melee | 15 |
| THS (two-hand sword) | Melee | 25 |
| Spear | Melee | 20 |
| Shield | OffHand | 5 |
| Bow | Ranged | 18 |
| Wand | Ranged | 18 |
| Arrows | Ranged | 0 |

**UE5:** a `UPrimaryDataAsset` subclass (or a DataTable row struct if you want the whole catalogue in one file). `Id` maps to the asset's `FPrimaryAssetId` — if you use that, drop the string field rather than keeping two keys. `WorldPrefab` → `TSoftObjectPtr<UStaticMesh>`/actor class, soft-referenced so the catalogue doesn't drag every mesh into memory.

---

## 3. Rarity palettes

Rarity drives three colours everywhere: **frame border (`main`)**, **soft background (`soft`)**, **label text**. Both palettes below are approved; **Candy Warm is the chosen one**, Classic Bright is the fallback that matches the exported reference PNGs. Held in one asset with a dropdown; every view reads through it.

| Tier | Candy Warm — main / soft / text | Classic Bright — main / soft / text |
|---|---|---|
| Common | `#A99B86` `#F3EFE8` `#7C7060` | `#7C8698` `#EEF1F5` `#5A6474` |
| Uncommon | `#54C97E` `#E6F8ED` `#2F9455` | `#43B55F` `#E7F6EC` `#2E7D46` |
| Rare | `#9B6BE6` `#F0E9FB` `#6E42C0` | `#2E90E0` `#E4F1FC` `#1E6FB8` |
| Legendary | `#FF9A3D` `#FFEEDC` `#D26A0E` | `#F0A32E` `#FDF1DC` `#B9740A` |

Non-rarity states: **empty slot** `#DBD5C9` / `#F1EDE4` · **locked slot** `#D9D3C6` / `#F3EFE7`.

A separate, simpler rarity→colour function drives the 3D placeholder loot blocks (§5): Common `0.85,0.85,0.85` · Uncommon `0.35,0.82,0.38` · Rare `0.34,0.58,1.0` · Legendary `1.0,0.74,0.20`.

**UE5:** a `UDataAsset` holding a `TMap<EItemRarity, FRarityColorSet>` plus the two non-rarity pairs, referenced by every widget. Do **not** hard-code hex in widgets — the two-palette swap must stay a one-field change.

---

## 4. Inventory & equipment

One component on the player. Three containers, deliberately separate:

| Container | Shape | Rules |
|---|---|---|
| Gear bag | ordered list | capacity **20**; duplicates allowed; materials never enter it |
| Materials | map `item → count` | **no capacity limit**; stacks without bound |
| Equipped | map `slot → item` | exactly one item per slot, 4 slots |

**API**

| Call | Behaviour |
|---|---|
| `AddItem(item, count = 1)` | Material → `stack += count`. Gear → appends `count` copies, stopping at capacity. Returns false if nothing was added. Fires `OnItemAdded` per add. |
| `RemoveItem(item)` | Removes the first gear occurrence. Fires `OnItemRemoved`. |
| `HasItem(item)` / `Count` / `Items` | Gear-only queries. |
| `GetMaterialCount(item)` / `Materials` | Stack queries. |
| `SpendMaterial(item, amount)` | Fails if `have < amount`; otherwise decrements (removes the key at 0). **The forge's future spend path.** |
| `Equip(item)` | Writes `equipped[item.Slot] = item`. Fires `OnWeaponEquipped(item)`. |
| `Unequip(slot)` | Clears the slot, fires `OnWeaponEquipped(null)`. |
| `GetEquipped(slot)` / `IsSlotEquipped(slot)` | Lookup. |

**Known gaps to fix rather than replicate:** `Equip` **replaces without returning the previous item to the bag** (it is silently dropped from the world), and `OnWeaponEquipped(null)` on unequip carries no slot, so consumers can't tell *which* slot emptied — §6a compensates by re-reading state. In UE5, make the delegate `OnEquipChanged(EEquipSlot Slot, UItemData* NewItem, UItemData* OldItem)` and push the old item back into the bag.

**UE5:** `UInventoryComponent : UActorComponent` on the player pawn. `TArray<UItemData*>` + `TMap<UItemData*, int32>` + `TMap<EEquipSlot, UItemData*>`, with `DYNAMIC_MULTICAST` delegates so Blueprint widgets can bind. Unity reaches the component through a per-scene singleton; in UE5 get it off the pawn/PlayerState instead — do not port the singleton.

---

## 5. Pickup & the interaction system

### Generic interaction contract

Every interactable object carries its own trigger volume and self-registers with the player while eligible. The player's interactor holds the registered set and dispatches **E** to the highest-priority one.

| Property | Value |
|---|---|
| Priority ladder | `Pickup = 10`, `Open = 50`, `Assassinate = 100` (highest wins) |
| Prompt text | `Press [E] {label}` on a world-space widget above the object |
| Eligibility | re-evaluated per frame; ineligible objects deregister and hide their prompt |
| Player detection | trigger overlap, then walk up the hierarchy to the `Player`-tagged root |

### World item (the pickup)

| Property | Value |
|---|---|
| Trigger radius | **1.5 m** sphere |
| Prompt label | `Pick Up {DisplayName}`, or `Pick Up {DisplayName} x{count}` for stacked materials |
| Eligible when | it has an item assigned |
| On execute | `AddItem(item, count)` → optional auto-equip → destroy self |
| Auto-equip on pickup | **OFF by default.** Equipping is a UI action. |
| Inventory full | logs and **aborts** — the pickup stays in the world |

**UE5:** a `UInteractableComponent` (or `IInteractable` interface) + sphere overlap; the prompt is a `UWidgetComponent` in screen-space-facing mode. The player holds a `TSet` of in-range interactables and sorts by priority on input. UE has no direct equivalent to Unity's "configure while inactive, then activate" trick — use `SpawnActorDeferred` → set the item + count → `FinishSpawning`, so `BeginPlay` sees a configured actor.

---

## 6. What equipping drives (three independent consumers)

All three listen to the same `OnWeaponEquipped` event.

### a) Weapon mesh mount

| Rule | Value |
|---|---|
| Melee / Ranged / Armor items | mount under the **right-hand socket** |
| OffHand items | mount under the **off-hand socket** |
| Transform | the prefab's **authored local transform** (identity relative to socket); off-hand gets a corrective rotation, see below |
| On unequip | destroy that socket's children only |
| Hitbox wiring | **Melee items only** — the mounted weapon's own collider becomes the attack hitbox |

**Weapon-as-hitbox** is the current model: there is no static hitbox on the character. Every weapon asset carries a disabled trigger collider + kinematic rigidbody + a relay; on mount, the melee code takes ownership of that collider and enables it only during the active frames of a swing. Unarmed = no hitbox and a warning-level log, not an error.

Off-hand corrective rotation `(0, -90, 180)` is applied for the three left-hand stances (SwordAndShield, DoubleSword, BowAndArrow) because the off-hand bone is mirrored. **UE5 already solves this** with per-stance per-hand attach rotations (see the original spec §6) — keep UE's table and ignore Unity's corrective hack.

### b) Damage source

Melee damage is **pulled at swing time**, not cached:

```
damage = equipped[Melee]?.Damage ?? fallbackDamage      // fallback = 20 (unarmed)
if (oneShotOverride > 0) damage = oneShotOverride       // assassinate = 99999, consumed
```

Pulling per hit means an equip swap mid-combo is reflected on the very next hit with no invalidation logic. Ranged damage is **not** item-driven yet — the arrow carries its own 30 (as in UE5).

### c) Stance bridge (the tie-in to the ported player)

The 8-stance system and the inventory **coexist**: the Q dev-cycle stays for testing, but equipping resolves the stance from the equipped set. On any equip/unequip the stance is re-derived and applied **without** re-mounting meshes (the mount already happened in 6a).

| Equipped state | Resolved stance |
|---|---|
| Ranged slot holds a bow | `BowAndArrow` (7) |
| Ranged slot holds a wand | `MagicWand` (6) |
| Melee empty | `NoWeapon` (0) |
| Melee = two-hand sword | `TwoHandsSword` (2) |
| Melee = spear | `Spear` (5) |
| Melee = one-hand + OffHand shield | `SwordAndShield` (3) |
| Melee = one-hand + OffHand one-hand | `DoubleSword` (4) |
| Melee = one-hand, off-hand empty/other | `SingleSword` (1) |

**Ranged wins over melee** when both are equipped.

⚠ **Port this differently.** Unity classifies weapons by **prefab-name prefix** (`WeaponPrefab_OHS*`, `THS*`, `Spear*`, `Shield*`, `Wand*`, `Bows`) because the category was never authored as data. In UE5, put an explicit `EWeaponCategory` field on the item asset and switch on that. Same table, no string matching.

---

## 7. Inventory / Character screen

Design source of truth: `Documentation/Asset and inventory UI design/design_handoff_candy_cloud/README.md` + the three reference PNGs beside it. Landscape, reference canvas **980 × 600**.

### Behaviour

| Rule | Value |
|---|---|
| Toggle | **I** key |
| Pause | sets time scale to **0** on open, restores the **captured previous value** on close (not a hard 1.0 — keeps future slow-mo compatible) |
| Cursor | a **counted** request/release so several screens can be open without one closing re-locking the cursor early; the gameplay click-to-relock rule is suppressed while any request is live |
| Open/close guards | double-open and double-close are no-ops so the cursor request count stays balanced; a teardown while open releases |
| Data | live — no snapshot, refreshed on open and on tab change |

**UE5:** `Add/RemoveFromViewport` + `SetInputModeGameAndUI` + `bShowMouseCursor` replaces the whole cursor-counting apparatus, and `UGameplayStatics::SetGamePaused` replaces the time-scale dance — but **widgets must be set to tick while paused** or the screen freezes. Unity needed an always-active host object with a toggled child so the input subscription survived hiding; UE binds input on the controller, so that whole pattern is unnecessary — do not port it.

### Layout

- **Title bar:** gold currency pill (left) · `Knight · Lv 8` (centre) · close ✕ (right).
- **Left "Character" panel** (flex 1.25): `[left slot column] [hero preview] [right slot column]`, then a 4-up stats row, then Skins / Stats buttons.
- **Right "Items Bag" panel** (flex 1): title · category tabs · 5-column grid · selected-item detail strip · "Add 5 slots" footer.
- Panel gap 14 · grid gap 8 · slot column gap 16 · slot 58×58 · radius: cells 12–14, slots 14, panels 16–26, pills 20 · touch targets ≥44.

### Equipment slots — 8 shown, **4 wired**

| Wired | Future (locked) |
|---|---|
| Melee, Off-hand, Ranged, Armor | Head, Chest, Feet, Charm |

Three states: **filled** (2px rarity border + rarity soft fill + icon) · **empty** (muted frame) · **locked** (muted frame, icon at 30% opacity, lock badge). Locked slots are non-interactive and never read inventory state.

### Bag grid

| Rule | Value |
|---|---|
| Tabs | `All · Weapons · Armor · Materials · Potions` — Potions is present but **disabled/future** |
| Filtering | Weapons = gear whose slot ≠ Armor · Armor = gear whose slot = Armor · Materials = the stack map · All = both |
| Padding | grid pads with empty cells up to a **minimum of 20** so the layout never collapses |
| Cell | 2px rarity border, rarity soft fill, square |
| Stack badge | `xN`, **materials only, only when N > 1**, bottom-right |
| Selection | dark inset ring; clicking a cell populates the detail strip; empty cells are not selectable |
| Icons | `Icon` sprite when set, otherwise **name initials** (all assets currently fall back) |

### Stats row & detail strip

| Readout | Source |
|---|---|
| Damage | equipped Melee item's `Damage` (0 unarmed) |
| Armor | player's `Defense` stat, rounded |
| HP | player's `MaxHP` |
| Stamina | player's `MaxStamina` |
| Title | player's `DisplayName` |

Detail strip shows name · rarity label (rarity text colour) · damage (`-` for materials) · rarity-tinted frame/fill/icon tile.

**Hero preview is an intentional placeholder** in both the mock and the build. The real version needs a render-target camera on a preview model with drag-to-rotate. **Open decision, unchanged from the Unity side:** a *separate preview instance* (recommended — always idle, well-lit, independent of gameplay state) vs the *live player model*. Decide once, build in whichever engine, mirror.

---

## 8. Weapon Forge screen — layout real, **data stubbed**

The whole screen exists and renders both states, driven by authored stub view-models. It is not connected to any real weapon.

**Everything renders through one entry point — `Show(viewModel)`.** That single seam is the entire integration surface: build the view-model from real data and the screen goes live untouched. Keep that property in UE5 (one BP-callable function taking a struct).

### View-model contract

| Field | Meaning |
|---|---|
| `weaponName` | display name (both cards) |
| `level`, `rarity`, `currentDamage` | the "current" card |
| `nextLevel`, `nextRarity`, `nextDamage` | the "after upgrade" card |
| `materials[]` | rows of `{ displayName, rarity, have, need }` |
| `CanUpgrade` (derived) | true iff **every** row has `have ≥ need` |
| `Delta` (derived) | `nextDamage − currentDamage` |

### Rendering rules

| Element | Rule |
|---|---|
| State pill | `Ready to forge` on `#E7F6EC`/`#2E7D46`, else `Not enough` on `#FBE7E7`/`#D14343` |
| Current / next cards | frame+fill+icon tile tinted by that card's rarity; level badge `+N`; next card shows the delta as `+N` |
| Rarity jump | conveyed by **bold + colour** on the next card's rarity pill (a ▲ glyph is unavailable in the default font) |
| Rarity ladder | 4 pills Common→Legendary; current and next at full alpha, all others at **0.4**; next is bold |
| Material row | rarity tile · `Name - Tier` · `have/need` · status dot (green ok / red short); short rows fade the tile to **0.55** and redden the count |
| Button enabled | label `UPGRADE  +N DMG` on `#38A85E`, white text |
| Button disabled | label `NEED MORE MATERIALS` on `#E7E3DB`, text `#A9A192` |
| Sub-note | on enabled, reserves the anchor for the success VFX; on disabled, `Missing Nx {material} - defeat harder enemies to collect` |

### The missing piece — weapon instances

The forge cannot go live in **either** engine until this exists. Spec it once and build it in both:

- **Per-weapon instance:** `{ template: ItemData, level, baseDamage, damagePerLevel }`.
- **Derived:** `currentDamage = baseDamage + level × damagePerLevel`; `rarity = f(level)` by tunable thresholds.
- **Cost table:** per-level `{ materialId → needed }`, escalating in both quantity and tier (Mat 1 Common → Mat 2 Rare → Mat 3 Legendary).
- **Upgrade action:** verify all costs → spend materials → `level += 1` → recompute damage and rarity → play the celebration.
- **Gear becomes unique instances** (two identical swords can differ by level); materials stay stacks. This is the one change that ripples: the inventory's gear list must hold *instances*, not shared templates, and every UI read of `Damage`/`Rarity` must come from the instance.

---

## 9. Loot

### Loot table (one asset per enemy archetype)

Each entry rolls **independently** — this is not a weighted single pick, so one kill can drop several entries or none.

| Entry field | Meaning |
|---|---|
| `item` | gear or material |
| `dropChance` | 0..1, rolled per entry |
| `minCount` / `maxCount` | inclusive quantity range when it drops |

**Grunt table (live values):**

| Item | Rarity | Chance | Count |
|---|---|---|---|
| Mat 1 | Common | 80% | 1–3 |
| Mat 2 | Rare | 30% | 1–2 |
| Mat 3 | Legendary | 5% | 1 |
| THS01 sword | Common | 25% | 1 |

The three materials are id `mat1/mat2/mat3`, display `Mat 1/2/3`, kind Material, damage 0, no icon, no world prefab.

### Dropper

Sits on the enemy, subscribes to its **death event**, and is **single-fire** (a re-raised death does not double-drop). On death it rolls the table and spawns one pickup per drop.

| Placeholder visual | Value |
|---|---|
| Shape | **cube = gear**, **sphere = material** |
| Colour | rarity tint (the block palette in §3) |
| Scatter | random point in a **0.6 m** radius circle around the corpse |
| Height | **0.6 m** above the corpse |
| Scale | **0.3** uniform |

Real art is a **one-line swap**: spawn `item.WorldPrefab` instead of the primitive. The primitive path exists only because item icons/pickup art aren't authored yet.

⚠ **The bug worth carrying over as a lesson:** killing an enemy with an arrow means the whole death chain runs *inside a physics trigger callback*. Unity forbids immediate destruction there, and deferred destruction left the pickup with no collider. The fix was to build the block with **no collider at all** so nothing ever needs removing. The UE5 equivalent hazard is mutating actors during overlap/collision events — spawn deferred and finish outside the callback.

**UE5:** loot table as a `UDataTable` (`FLootEntry` rows) or a `UDataAsset` per archetype; dropper as a component bound to the health component's death delegate.

---

## 10. Design tokens (shared by both screens)

| Token | Value |
|---|---|
| Screen background | `linear-gradient(160deg, #DCF1FF, #F0E4FF)` |
| Panels | `#FFFFFF`, radius 26 (inventory) / 16 (forge inner), shadow `0 10px 26px rgba(90,120,200,.16)` |
| Headings | `#4B57C9` indigo |
| Primary button | gradient `#5BD08A → #37B56B`, white text, chunky bottom shadow `0 6px 0 #2E8B4E` (collapses on press) |
| Text | strong `#2A2A2E` · muted `#7A7062` / `#8A8172` · hint `#9A9284` |
| Stat accents | Damage `#E0872E` · Armor `#2E90E0` · HP `#E0524E` · Stamina `#43B55F` |
| Semantic | success `#38A85E` · error `#D14343` · gold `#F0A32E` / `#B9740A` |
| Tabs | active `#2E2A26` bg + white text; idle `rgba(0,0,0,.05)` bg + `#7A7062` text |
| Type | **Fredoka** 500/600/700 (titles, stat values, damage numbers, buttons) · **Nunito** 400–800 (body, labels, tabs, counts) |
| Key sizes | screen title 22 · panel title 17–20 · stat value 20 · damage number 32 · tab 12 · label 8–10 · count badge 11 · button 13–16 |

Neither font is imported yet on the Unity side — every glyph that the default font lacks (`✓ ✕ × · — ▲ ✦ ➜`) was swapped to ASCII with a comment at each site. **UE5 has no such constraint** if the fonts are imported up front; use the real glyphs and don't inherit the ASCII fallbacks.

---

## 11. Unity → UE5 cheat-sheet

| Unity concept | UE5 equivalent |
|---|---|
| `ScriptableObject` item/table/palette asset | `UPrimaryDataAsset` / `UDataAsset`, or `UDataTable` row struct |
| Per-scene singleton (`Instance`) | component on the pawn / PlayerState — **do not port the singleton** |
| C# `event Action<T>` | `DYNAMIC_MULTICAST_DELEGATE` (Blueprint-bindable) |
| Trigger `SphereCollider` + overlap | `USphereComponent` + `OnComponentBeginOverlap` |
| World-space prompt canvas | `UWidgetComponent` |
| UGUI Canvas / Image / TMP | UMG `UUserWidget` / `UImage` / `UTextBlock` |
| `GridLayoutGroup` (5-col bag) | `UUniformGridPanel` or `UWrapBox` |
| Prefab (`Cell_Item`, `Slot_Equipment`, `Row_ForgeMaterial`) | `UUserWidget` sub-widget class |
| `Time.timeScale = 0` + restore | `SetGamePaused` (+ widgets must tick while paused) |
| Cursor request counting | `SetInputModeGameAndUI` + `bShowMouseCursor` |
| Active-host + toggled-child pattern | unnecessary — input lives on the controller |
| Configure-inactive → activate | `SpawnActorDeferred` → set → `FinishSpawning` |
| Prefab-name-prefix weapon classification | explicit `EWeaponCategory` field on the item asset |
| Weapon collider as the attack hitbox | same model works — collider on the equipped weapon actor, enabled by anim notify |

---

## 12. Suggested port order

Each step is independently testable; nothing later is needed to verify anything earlier.

1. **Item data asset + enums + rarity palette asset** (§2, §3). Author ~5 items by hand.
2. **Inventory component** (§4) with the fixed equip semantics. Verify via console commands.
3. **Interaction system + world pickup** (§5). Place items by hand; verify prompt, priority, bag contents.
4. **Equip consumers** (§6) — mount first (visible), then damage, then the stance bridge. The stance bridge is where this meets the already-ported player; expect it to be the fiddliest step.
5. **Loot tables + dropper** (§9), wired to the existing UE5 enemy death path. Placeholder blocks are fine.
6. **Inventory / Character screen** (§7). Equip-from-UI is the action that finally closes the loop — it is *not* implemented on the Unity side either (Unity can only auto-equip at pickup, and that's off by default), so UE5 can lead here.
7. **Weapon-instance model** (§8) → then the Forge screen goes live through `Show(vm)`.

---

## 13. Known-deferred / not built (so UE5 can match "as-is" or improve)

**Unity-side gaps** — the UE5 port can fix rather than replicate:
- `Equip` discards the previously equipped item instead of returning it to the bag; `OnWeaponEquipped(null)` doesn't say which slot emptied.
- **No equip-from-UI action** — clicking an equipment slot or bag cell does not equip. Only selection is wired.
- Weapon category inferred from prefab name prefix (§6c).
- `RequiredLevel` authored but never checked (no player-level system).
- No item icons authored — every cell shows initials.
- No save/load of any of this. `Id` is the intended stable key.

**Deferred by decision (both engines):**
- Weapon instances + upgrade/forge data (§8) — the biggest single piece.
- Hero preview render (decision pending, §7).
- Drop animations / pickup juice / VFX / SFX.
- Loot on enemy types other than the Grunt; auto-pickup on walk-over.
- Currency, vendors, potions/consumables (the Potions tab is a visible placeholder).
- Bag sorting, search, drag-drop reordering, tooltips.
- Head/Chest/Feet/Charm equipment slots (shown locked).
- Ranged damage from the item (arrow still carries a hard 30).
- Off-hand items equipped from inventory still need the bone-level orientation fix on the Unity side; UE5's per-hand attach rotation table already covers this.

---

*Unity source of truth (verified 2026-07-25):*
`Assets/Scripts/Items/{ItemData,EquipSlot,ItemDatabase}.cs` ·
`Assets/Scripts/Player/{PlayerInventory,PlayerEquipmentVisuals,StanceController,PlayerCombat}.cs` ·
`Assets/Scripts/Interaction/{Interactable,WorldItem}.cs` ·
`Assets/Scripts/Combat/{LootTable,LootDropper}.cs` ·
`Assets/Scripts/UI/Inventory/{InventoryScreen,ForgeScreen,InventoryItemCell,EquipmentSlotView,ForgeMaterialRow,RarityPalette}.cs` ·
`Assets/Scripts/Input/MouseLook.cs` ·
`Assets/Data/{items,Loot,UI}/` ·
design: `Documentation/Asset and inventory UI design/design_handoff_candy_cloud/`.
Player character parity is covered by `PlayerCharacter_MechanicsSpec_ForUnity.md` (UE5 → Unity) and `UE5_Port_Plan.md`; enemies, HUD bars, damage numbers and level generation are out of scope here.
