# Handoff: Inventory & Weapon-Upgrade UI — "Candy Cloud" look

## Overview
Two mobile (landscape) screens for a chibi action-RPG built in **Unity UGUI**:

1. **Inventory / Character screen** — character preview, equipment slots, item bag grid (gear + stackable materials), category tabs, stat readout.
2. **Weapon Upgrade / Forge screen** — spend materials to level a weapon; damage rises linearly and rarity climbs the ladder. Shown in two states: **enough materials** (button active) and **not enough** (button disabled, missing mat highlighted).

Both use the **Candy Cloud** visual direction: soft sky→lavender gradient background, pure-white pillowy panels, rounded corners, a chunky 3D green primary button, and a bright rarity color language shared across gear and materials.

## About the Design Files
The files in `assets/` and the `.dc.html` files in this bundle are **design references created in HTML** — prototypes showing the intended look, layout, and behavior. **They are not production code to copy.** The task is to **recreate these designs in Unity** using UGUI (Canvas / Image / TextMeshPro) and the project's existing prefab/component patterns. The HTML is only the source of truth for measurements, colors, type, and states.

## Fidelity
**High-fidelity.** Colors, type, spacing, rarity system, and both forge states are final-intent. Recreate pixel-close, then swap the emoji placeholders for real chibi item art (see Assets).

## Reference Images
- `assets/inventory_candy_cloud.png` — Inventory/Character screen, "All" tab active, Ember Sword selected.
- `assets/forge_enough_candy_cloud.png` — Forge, enough materials, button ACTIVE, Rare→Legendary jump.
- `assets/forge_short_candy_cloud.png` — Forge, Legendary material short (1/3), button DISABLED.
- `assets/chibi_hero_reference.png` — target "RPG Tiny Hero" art style (bright, cute, rounded, saturated).

---

## Screens / Views

### 1. Inventory / Character screen
**Purpose:** view/equip gear, browse the item bag, read core stats.
**Orientation:** landscape. Reference canvas 980 × ~600 px (scale to device; UGUI anchors below).

**Layout (top → bottom):**
- **Title bar** (full width): left = gold pill `🪙 2,584`; center = `Knight · Lv 8`; right = close `✕` button.
- **Body** = two panels in a horizontal flex row, `gap: 14px`:
  - **Left "Character" panel** (`flex: 1.25`): a row of `[left slot column] [hero preview] [right slot column]`, then a 4-up stats row, then two buttons (Skins / Stats).
  - **Right "Items Bag" panel** (`flex: 1`): title, category tabs, 5-column item grid, selected-item detail strip, "Add 5 slots" footer.

**Components**

*Title bar*
- Panel: `#FFFFFF`, radius 14px, shadow `0 10px 26px rgba(90,120,200,.16)`, padding `10px 14px`.
- Gold pill: bg `#FDF1DC`, border `1.5px #F0A32E`, text `#B9740A`, weight 800, 13px, radius 9px.
- Title: Fredoka 600, 22px, color `#4B57C9`; suffix "· Lv 8" at 16px, opacity .5.
- Close: 32×32, radius 9px, bg `#F5E3E3`, border `1.5px #E3A6A6`, glyph `#C05A5A`.

*Equipment slots* (8 total; **only 4 are wired today** — Melee, Off-hand, Ranged, Armor. Head/Chest/Feet/Charm are future, shown dimmed with 🔒)
- Left column: **ARMOR** (Rare, filled), **HEAD** (future), **CHEST** (future), **FEET** (future).
- Right column: **MELEE** (Legendary, filled), **OFF-HAND** (Uncommon, filled), **RANGED** (Common, filled), **CHARM** (future).
- Each slot: 58×58, radius 14px, **2px border = rarity color**, bg = rarity soft tint, icon 25px. Future slot: border `#D9D3C6`, bg `#F3EFE7`, icon opacity .30, 🔒 top-right at 10px.
- Label chip below each slot (overlapping bottom edge): Nunito 800, 7px, `#8A8172`, white bg, radius 5px, tiny shadow.
- Column gap 16px, vertically centered.

*Hero preview* (center, `flex: 1`, min-height 300px)
- Placeholder in mock: dashed border `2px rgba(0,0,0,.14)`, radius 16px, subtle diagonal-stripe fill, monospace caption "CHIBI HERO / rotatable 3D preview", bottom hint "◀ ⟲ drag to rotate ▶".
- **In Unity:** replace with a live 3D render (RenderTexture of the chibi hero) or a RawImage; support drag-to-rotate.

*Stats row* (4 equal cells, gap 8px, `margin-top: 16px`)
- Cell: bg `rgba(0,0,0,.035)`, radius 12px, padding `9px 6px`, centered. Icon 15px; value Fredoka 600, 20px; label Nunito 800, 8px uppercase `#8A8172`.
- Values: **Damage** `37` `#E0872E` (⚔️) · **Armor** `194` `#2E90E0` (🛡️) · **HP** `820` `#E0524E` (❤️) · **Stamina** `60` `#43B55F` (⚡).

*Skins / Stats buttons* (2 equal, gap 8px, `margin-top: 10px`)
- Candy Cloud primary: bg `linear-gradient(180deg,#5BD08A,#37B56B)`, text `#FFFFFF`, weight 700, 13px, radius 11px, padding 11px, drop shadow `0 4px 0 rgba(0,0,0,.12)` (the chunky 3D edge). Min touch height ≥44px.

*Items Bag panel*
- Header "Items Bag": Fredoka 600, 17px, `#4B57C9`.
- **Category tabs** (flex, gap 6px, wrap): `All · Weapons · Armor · Materials · Potions`. Active ("All") = bg `#2E2A26`, text `#fff`; inactive = bg `rgba(0,0,0,.05)`, text `#7A7062`; Potions = future, opacity .7. Each: radius 9px, padding `7px 12px`, Nunito 700, 12px.
- **Item grid**: CSS `grid`, 5 columns, gap 8px, square cells (`aspect-ratio:1`). Each cell: radius 12px, **2px rarity border**, rarity soft bg, soft glow `0 2px 6px rgba(rarity,.2)`, icon 23px.
  - **Selected** cell adds a ring: `inset -4px` frame, `2.5px #2A2A2E` + `2px #fff` inset, radius 15px.
  - **Stack count badge** (materials only): bottom-right, bg `rgba(30,28,26,.82)`, text `#fff`, Nunito 800, 11px, radius 7px, format `×N`.
  - Grid contents (in order): ⚔️ Legendary *(selected)*, 🗡️ Rare, 🏹 Common, 🛡️ Uncommon, 🪄 Rare, 🪵 Common ×13, 🪨 Rare ×4, 💎 Legendary ×3, 🧵 Common ×22, 🍎 Common ×11, 🥕 Common ×8, 🥚 Common ×5, 🌿 Uncommon ×6, 🔩 Common ×30, 🥩 Uncommon ×4, 🧪 Rare ×2, 🪖 Uncommon, 👢 Common, then 2 empty slots (border `#DBD5C9`, bg `#F1EDE4`).
- **Selected-item detail strip** (`margin-top: 12px`): bg = rarity soft, 2px rarity border, radius 12px, padding `8px 10px`. Left: 40×40 white icon tile w/ rarity border. Center: name (Nunito 800, 13px `#2A2A2E`) + rarity label (Nunito 800, 10px, rarity text color). Right: "DMG" label + value (Fredoka 600, 18px, `#E0872E`). Example: **Ember Sword +3 · LEGENDARY · DMG 45**.
- **Footer** (`margin-top: 12px`): "ADD 5 SLOTS" left (Nunito 800, 12px `#7A7062`) + "💎 50" right (`#2E90E0`); bg `rgba(0,0,0,.035)`, radius 10px, padding `9px 12px`.

### 2. Weapon Upgrade / Forge screen
**Purpose:** spend materials → weapon +1. Communicate damage gain and rarity jump clearly.
**Orientation:** landscape. Reference card 620 × ~470 px.

**Layout:** single white panel (radius 16px, shadow `0 10px 26px rgba(90,120,200,.16)`), padding 16px, containing top → bottom:
1. **Header row:** "⚒️ Weapon Forge" (Fredoka 600, 20px, `#4B57C9`) + right-aligned state pill.
   - Enough: text `Ready to forge ✓`, `#2E7D46` on `#E7F6EC`.
   - Short: text `Not enough ✕`, `#D14343` on `#FBE7E7`.
2. **Before → After comparison** (flex row, gap 10px):
   - **Current card** (`flex:1`): bg = current rarity soft (`#E4F1FC`), 2px border current rarity (`#2E90E0`), radius 16px. Level badge `+2` top-left (white on rarity color). 56×56 white icon tile w/ rarity border, weapon icon 30px. Name "Ember Sword" (Nunito 800, 13px). Rarity pill "Rare". "CURRENT DMG" label + `37` (Fredoka 600, 32px, `#7A7062`).
   - **Arrow** `➜` (26px, next-rarity color) with "UPGRADE" label above.
   - **Next card** (`flex:1`): bg = next rarity soft (`#FDF1DC`), 2px border next rarity (`#F0A32E`) **+ glow** `0 0 0 3px rgba(240,163,46,.16), 0 8px 22px rgba(240,163,46,.22)`. Level badge `+3`, ✨ top-right. Icon tile has extra glow `0 0 14px rgba(240,163,46,.4)`. Rarity pill "Legendary ▲" (white on gold). "AFTER UPGRADE" + `45` in green `#38A85E`, with delta `+8`.
3. **Rarity ladder** (flex, centered, gap 6px): pills `Common · Uncommon · Rare · Legendary`. Non-active = opacity .4. **Current** (Rare) = ring `0 0 0 2px #2A2A2E`. **Next** (Legendary) = glow ring `0 0 0 2px #fff, 0 0 12px #F0A32E`. Pill: rarity soft bg, 2px rarity border, rarity text, Nunito 800, 10px, radius 20px.
4. **Materials required** (bg `rgba(0,0,0,.025)`, radius 12px, padding `10px 12px`): section label "MATERIALS REQUIRED" (Nunito 800, 9px, `#8A8172`). Rows (gap 9px), each: 34×34 tile (rarity soft bg + 2px rarity border, icon 18px) · name "Material · <tier>" · **have / need** count (Fredoka 600, 15px) · status dot (20px circle, ✓ green `#43B55F` / ✕ red `#D14343`).
   - **Enough state:** 🪵 Common 8/5 ✓ · 🪨 Rare 4/3 ✓ · 💎 Legendary 3/2 ✓.
   - **Short state:** 🪵 Common 8/5 ✓ · 🪨 Rare 4/3 ✓ · 💎 Legendary **1/3 ✕** (count red, tile opacity .55).
5. **Upgrade button** (full width, padding 16px, radius 14px, Fredoka 700, 16px):
   - **Enabled:** label "UPGRADE · +8 DMG", bg `linear-gradient(180deg,#5BCB7E,#38A85E)`, text `#fff`, shadow `0 6px 0 #2E8B4E, 0 12px 20px rgba(56,168,94,.35)`. Sub-note (green): "✦ celebratory glow + burst plays here on success" — **reserve space/anchor for upgrade VFX** (particles/flash come later).
   - **Disabled:** label "NEED MORE MATERIALS", bg `#E7E3DB`, text `#A9A192`, no shadow. Sub-note (red `#D14343`): "Missing 2× Legendary material — defeat harder enemies to collect".

---

## Interactions & Behavior
- **Category tabs:** filter the bag grid by type; single active tab. Potions tab present but disabled (future).
- **Item cell tap:** selects the cell (shows dark selection ring) and populates the selected-item detail strip.
- **Equipment slot tap:** opens equip flow (out of scope here). Future slots are non-interactive/locked.
- **Upgrade button:** enabled only when every material `have ≥ need`. On success: apply level+1, recompute damage (linear) and rarity (by level threshold), play the celebration moment in the reserved anchor.
- **Rarity is driven by upgrade level:** base weapon Common; climbs the ladder as it levels (thresholds tunable). The before/after cards and ladder must visibly reflect a rarity change on level-up.
- **Cost escalates:** higher levels need more materials and higher tiers (Mat 1 Common → Mat 2 Rare → Mat 3 Legendary).
- Animations to add later: count-up on damage numbers, ladder pip advance, button press depress (the 3D bottom shadow collapses), success burst. Keep to sprite/shader-doable effects (UGUI constraint).

## State Management
- `selectedItemId`, `activeCategory` (All/Weapons/Armor/Materials).
- Per-weapon instance: `level (+N)`, `baseDamage`, `damagePerLevel`, derived `currentDamage`, derived `rarity` (from level thresholds).
- Materials: stacks keyed by material id with `count`; per-level upgrade cost table `{matId: needed}`.
- Derived: `canUpgrade = costs.every(c => inventory[c.id] >= c.need)`; `nextDamage`, `nextRarity`.
- **Gear are unique instances** (each carries its own +level — two identical swords can differ). **Materials are stacks** with a count.

## Design Tokens

### Rarity palette (Candy Warm — chosen) — border / soft-bg / text
| Tier | Border (main) | Soft bg | Text |
|---|---|---|---|
| Common | `#A99B86` | `#F3EFE8` | `#7C7060` |
| Uncommon | `#54C97E` | `#E6F8ED` | `#2F9455` |
| Rare | `#9B6BE6` | `#F0E9FB` | `#6E42C0` |
| Legendary | `#FF9A3D` | `#FFEEDC` | `#D26A0E` |

> NOTE: the exported reference PNGs currently render rarity with the **Classic Bright** palette below (grey/green/blue/gold). Both palettes are approved — **use Candy Warm** for final Candy Cloud build for a sweeter, chibi-matched feel; swap the four border/soft/text triples accordingly. Classic is provided as the fallback.

### Rarity palette (Classic Bright — fallback, matches current PNGs)
| Tier | Border | Soft bg | Text |
|---|---|---|---|
| Common | `#7C8698` | `#EEF1F5` | `#5A6474` |
| Uncommon | `#43B55F` | `#E7F6EC` | `#2E7D46` |
| Rare | `#2E90E0` | `#E4F1FC` | `#1E6FB8` |
| Legendary | `#F0A32E` | `#FDF1DC` | `#B9740A` |

### Candy Cloud theme
- Screen background: `linear-gradient(160deg, #DCF1FF, #F0E4FF)`.
- Panels: `#FFFFFF`, no border, shadow `0 10px 26px rgba(90,120,200,.16)`, radius **26px** (inventory panels) / 16px (forge inner panel).
- Title / heading color: `#4B57C9` (indigo).
- Primary button: `linear-gradient(180deg,#5BD08A,#37B56B)`, white text, chunky bottom shadow.
- Neutral text: `#2A2A2E` (strong), `#7A7062` / `#8A8172` (muted), `#9A9284` (hint/mono).
- Stat accent colors: Damage `#E0872E`, Armor `#2E90E0`, HP `#E0524E`, Stamina `#43B55F`.
- Success green: `#38A85E` (+ dark edge `#2E8B4E`). Error red: `#D14343`. Gold currency: `#F0A32E`/`#B9740A`.

### Typography
- **Fredoka** (500/600/700) — titles, stat values, damage numbers, button labels.
- **Nunito** (400/600/700/800) — body, labels, tabs, counts.
- Both on Google Fonts. In Unity import as TMP font assets. Key sizes: screen title 22, panel title 17–20, stat value 20, damage number 32, tab 12, label 8–10, count badge 11, button 13–16.

### Radii / spacing / shadow
- Radius: cells/tiles 12–14px, slots 14px, panels 16–26px, pills 20px, buttons 11–14px.
- Panel gap 14px; grid gap 8px; slot column gap 16px; stat gap 8px.
- Slot 58×58; stat/detail icon tiles 34–40px; forge weapon tile 56px.
- Touch targets ≥ 44px.

## Unity / UGUI notes
- Build from **panel / grid / button / list-row** prefabs. Item cell = one reusable prefab (Image frame + icon Image + TMP count + selection ring child). Equipment slot = one prefab (empty/filled/locked variants). Material row = one prefab. Rarity tint driven by a single `Rarity` enum → color lookup (one ScriptableObject holding both palettes).
- Use a **GridLayoutGroup** for the 5-col bag, **Horizontal/VerticalLayoutGroup** for slot columns, tabs, stats, material list.
- The "chunky 3D button" = button image + a darker offset shadow image behind (or 9-slice sprite); on press, reduce the offset.
- Rarity glow = additive sprite/soft shadow, not a post effect (mobile-safe).
- Anchor everything for landscape; use safe-area padding.

## Assets
- Emoji in the mocks are **placeholders**. Replace with real chibi PBR item icons rendered to sprite atlases: weapons (sword/shield/bow/wand), armor pieces, and the 3 material tiers (Mat 1/2/3). Keep them in the same framed cell so rarity framing stays consistent.
- Hero preview: RenderTexture of the live chibi model (rotatable), per `assets/chibi_hero_reference.png` style.
- Currency/rarity gem `💎`, gold `🪙` → real UI sprites.

## Files (design references in this bundle)
- `assets/inventory_candy_cloud.png`, `assets/forge_enough_candy_cloud.png`, `assets/forge_short_candy_cloud.png` — hi-fi reference renders.
- `assets/chibi_hero_reference.png` — target art style.
- `Inventory & Forge UI.dc.html` — full canvas (all options + palettes). Candy Cloud = option `1b` (inventory) and turn `t2` / `2a`,`2b` (forge).
- `InventoryScreen.dc.html`, `ForgeScreen.dc.html` — the source markup/values for each screen (read for exact structure, data, and computed colors).
