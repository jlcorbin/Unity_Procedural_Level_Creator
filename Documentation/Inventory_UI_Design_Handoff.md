# Inventory & Weapon-Upgrade UI — Design Handoff

**For:** a design session (mockups / visual design). You do NOT need to know
Unity or write code — this is a visual + UX design brief. Deliverables are at
the bottom.

**Attached references:** `inventory ui 1–4.png` (see §7 for what to take from each).

---

## 1. The game (context)

A **chibi action-RPG** for mobile (Android / iOS), third-person camera,
real-time melee + ranged combat. The hero and world use the **"RPG Tiny Hero"**
art style: **bright, saturated, cute, rounded, stylized PBR** — big-headed
cheerful characters, clean shapes, sunny outdoor colors (think a friendly
Saturday-morning fantasy, NOT a gritty dungeon crawler).

The player fights with **8 weapon stances** (sword, sword+shield, two-handed
sword, dual swords, spear, magic wand, bow, unarmed).

The UI in this brief supports the game's core loop:

> **kill enemies → collect gear + materials → equip gear → upgrade weapons with
> materials → take on harder enemies.**

---

## 2. Visual direction (the most important part)

**Lighter, brighter, friendlier than almost every RPG inventory you've seen.**

- Match the hero: **bright saturated colors, clean, rounded, cheerful,
  high-readability.** Soft light backgrounds, not black.
- The reference images (§7) are mostly dark/gritty — **take their LAYOUTS and
  FEATURES, but flip the TONE**: light panels, warm/bright accents, soft shadows
  instead of heavy grime.
- **Mobile-first:** large touch targets (min ~44 px), text legible on a phone,
  clean spacing. Assume **landscape** primary (matches gameplay orientation);
  call out if a portrait variant is worth it.
- Keep it **modular** — slots, grid cells, and list rows should read as
  repeatable components (it'll be built as reusable UI pieces).

---

## 3. Screens to design

### A. Inventory / Character screen
Elements (see references for arrangement — but brighter):
- **Character preview** — the chibi hero front-and-center (ideally rotatable).
- **Equipment slots** flanking the character. Current functional slots:
  **Melee weapon, Off-hand, Ranged, Armor.** Design should leave room to grow
  into more slots later (head / chest / hands / feet / accessories) — show a
  fuller slot layout but know only those four are wired today.
- **Item bag grid** — holds both **gear** and **materials**. Materials are
  **stackable** (show a count badge, e.g. "×12"). Every item cell is
  **rarity-colored** (border/frame + glow).
- **Category filter tabs** — e.g. **All / Weapons / Armor / Materials**
  (future: Potions). See ref 3's tab row.
- **Stats readout** — Damage, Armor/Defense, HP, Stamina (Attack speed etc.
  later). See refs 1 & 2.

### B. Weapon Upgrade / Forge screen
This is the heart of the progression loop. It must clearly express "spend
materials → weapon gets stronger."
- **Selected weapon**: large icon, name, **current level (+N)**, **current
  rarity**, **current damage**.
- **Next-level preview**: damage AFTER upgrade (e.g. `37 → 45`), and the **new
  rarity if it changes** (a Sword that becomes a Sword +3 should visibly jump to
  Legendary).
- **Material cost** for the next level: which materials + how many, shown as
  **have / need** (e.g. `Mat 1: 3 / 5`), with insufficient ones dimmed/red.
- **Upgrade button** — big, satisfying, enabled only when you have the mats.
- Leave **space for celebratory feedback** on a successful upgrade (glow / burst
  — actual VFX comes much later, but design the "moment").

---

## 4. The upgrade model (rules the UI must communicate)

- **Weapon + materials → Weapon +1.** Damage increases **linearly** per level.
- **Cost escalates**: each level needs **more** materials, and higher levels
  require **higher-tier ("special") materials**.
- **Rarity is driven by upgrade level.** A base weapon is **Common**; as it
  levels it climbs the rarity ladder — e.g. a **Sword** is Common, a **Sword +3**
  is **Legendary**. (Exact level→rarity thresholds are tunable later; design so
  the rarity of a weapon visibly changes as it levels.)
- **Materials come in 3 tiers** (names/art TBD — placeholders **Mat 1 / Mat 2 /
  Mat 3**):
  - **Mat 1 = Common**, **Mat 2 = Rare**, **Mat 3 = Legendary**.
  - Low weapon levels use Mat 1; high levels also demand Mat 2 / Mat 3.
- **Rarity is a shared visual language** across **both gear and materials** —
  same color system everywhere.

---

## 5. Rarity system (needs a bright-friendly palette)

Four tiers: **Common · Uncommon · Rare · Legendary.**

- Please propose a **color per tier that reads well on a BRIGHT/light UI.**
  (Most games tune rarity colors for dark backgrounds — we need versions that
  pop on light panels without looking muddy or neon-harsh.)
- Used for: item cell borders/frames, item name text, material tiers, and a
  weapon's current rarity as it levels up.

---

## 6. Constraints (so the design is buildable in Unity)

- Built in **Unity UGUI** (Canvas / Image / TextMeshPro) — keep it
  **panel / grid / button / list-row** based; avoid effects that can't be done
  with sprites + simple shaders.
- **Items:** equippable gear are **unique instances** (a weapon carries its own
  +level, so two identical swords can be different levels). **Materials are
  stacks** with a count.
- **Mobile:** touch targets, readable text, works at common phone aspect ratios.

---

## 7. What to take from each reference image

- **`inventory ui 1.png`** (Knight, wood frame): the **overall anatomy** —
  character center, equipment slots left/right, item bag grid on the right,
  Damage/Armor stats at the bottom, stack counts on items. Good structural
  starting point. → make it far brighter/cuter.
- **`inventory ui 2.png`** (survival, dark): the **stats list** treatment and a
  big clean **item grid** with counts. → too dark; borrow the clarity, drop the
  grime.
- **`inventory ui 3.png`** (sleek tablet): the **category tabs** (All / Weapon /
  Armor / …), **rarity-colored cells**, and a tidy bottom **stat row**. Closest
  to the clean/modern feel — just needs to be warmer/brighter, not cold blue.
- **`inventory ui 4.png`** (painterly mobile RPG): **ornate slot framing** and
  **rarity-bordered equipment slots**, character portrait energy. → keep the
  premium slot framing, lighten the palette.

**Net:** anatomy from #1, clarity from #2, tabs + rarity + modern polish from #3,
slot-framing charm from #4 — all rendered in the **bright, cute, chibi-matching**
tone from §2.

---

## 8. Deliverables

1. **Mockups of both screens** (Inventory/Character + Weapon Upgrade), showing
   key states:
   - Inventory: gear + materials in the grid, a category filter active, an item
     selected.
   - Upgrade: a weapon selected with **enough** materials (button active) AND a
     case with **not enough** (button disabled, missing mats highlighted); show
     the rarity changing on level-up.
2. **Rarity color palette** (Common / Uncommon / Rare / Legendary) tuned for a
   bright UI.
3. **Style direction** for slots, grid cells, rarity frames, buttons, and
   iconography.
4. *(Optional, very welcome)* an **interactive HTML prototype** to click through.

---

## 9. Out of scope for now (don't design these yet)

- Upgrade VFX / particles / final "look" (added much later).
- Final material names & art (use **Mat 1 / 2 / 3** placeholders).
- Drop rates / loot tables (later).
- Currency, vendors, potions/consumables (future).
- Player level / XP screens (the `requiredLevel` stat exists but isn't used yet).
