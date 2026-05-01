# M3-01 — Pre-Import Inventory of RPG Tiny Hero World Bundle PBR

**Date:** 2026-04-30
**Scope:** Read-only inventory of the new asset pack archive without
importing it into the Unity project. Cross-references every asset by
intended path against the 13 currently-wired clips from M2-B and the
existing player rig structure. Surfaces what's safe, what needs
re-wiring, and what's net-new content.

**Status:** Read-only research. No project state, asset, or git
changes. Temp scratch directory used (`/tmp/rpg-tiny-world-bundle-inventory`)
and cleaned up at end.

**Headline outcome:** Cleanest possible swap scenario. **All 13
currently-wired clip GUIDs are identical between the Duo pack and
the new World Bundle pack** — Unity's GUID-based reference
resolution will automatically re-point the override controller +
state motions to the new pack after import, **with zero re-wiring
required**. The only non-trivial element is the player-rig
character prefab: the new pack ships 24 modular character prefabs
(MC01–MC24) instead of a `MaleCharacterPBR.prefab`. Same Avatar
source though, so retargeting is identical.

---

## Section A — Package file metadata

| Field | Value |
|---|---|
| Path | `C:\Users\Jason\AppData\Roaming\Unity\Asset Store-5.x\Dungeon Mason\3D ModelsEnvironmentsFantasy\RPG Tiny Hero World Bundle PBR.unitypackage` |
| Publisher | Dungeon Mason (same as the existing Fantastic Dungeon Pack) |
| Size | 354,592,787 bytes (≈ 339 MiB) |
| SHA-256 | `81872f739b722f74c728d932950bd4c09d6ada19998ae8f75846e21b2f78509b` |
| Total assets | 1607 |

Size is within the expected 200–800 MB range for an RPG Tiny Hero
pack. SHA-256 captured for reproducibility.

---

## Section B — Pack folder structure

The package extracts under `Assets/RPGTinyHeroWorldBundlePBR/`
(the publisher chose a flat folder name, no spaces). Three
sub-packs:

| Sub-pack | Asset count | Purpose |
|---|---|---|
| `RPGTinyHeroWavePBR/` | 1075 | Characters, animations, weapons, head parts |
| `RPG Tiny Fantasy World 01 PBR/` | 528 | Environment art (rocks, mountains, water, vegetation) |
| `HDRP_BuiltIN/` | 3 | HDRP / Built-In RP variant sub-packages (URP project doesn't need these) |
| (root) | 1 | Root folder marker |

Top-level structure under `RPGTinyHeroWavePBR/`:

```
RPGTinyHeroWavePBR/
├── Animation/
│   ├── BowAndArrow/        (84 FBX + InPlace/ + RootMotion/ subfolders)
│   ├── DoubleSword/        (73)
│   ├── MagicWand/          (74)
│   ├── NoWeapon/           (94)
│   ├── SingleSword/        (73)
│   ├── Spear/              (72)
│   ├── SwordAndShield/     (74) ← currently used
│   └── TwoHandSword/       (73)
├── Animator/
│   ├── (~14 weapon-stance controllers + ForShowcasing/ subfolder)
│   ├── SwordAndShieldStance.controller   ← pack's example controller (we use our own)
│   └── ...
├── Material/               (21 .mat)
├── Mesh/                   (FBX meshes — body parts, weapons, projectiles, head parts)
├── Prefab/
│   ├── HeadParts/          (115 head/face parts: hair, eyes, mouths, ears)
│   ├── ModularCharacters/  (24 prefabs: MC01–MC24)
│   ├── MaskTintCharacters/ (1 prefab: MaskTint01)
│   ├── Weapons/            (57 weapon prefabs across all 8 weapon sets)
│   └── ...
├── Scene/                  (5 demo scenes incl. MainScene.unity, Extra Scenes/)
├── Shader/                 (URPMasktTintPBR.ShaderGraph)
└── Texture/                (mask + default PBR textures)
```

The `RPG Tiny Fantasy World 01 PBR/` sub-pack is environment-only
and unrelated to player work — relevant only if Jason wants the
demo island/landscape art for level mockups.

---

## Section C — Asset type breakdown

| Type | Extension(s) | Count | Notes |
|---|---|---|---|
| FBX (animations + meshes) | `.fbx` | 1006 | bulk of pack |
| Prefabs | `.prefab` | 431 | characters, weapons, head parts, world props |
| Animator controllers | `.controller` | 24 | pack examples — none referenced by us |
| Materials | `.mat` | 21 | URP-Lit + custom MaskTint |
| Textures | `.png` | 20 | character + world textures |
| Scenes | `.unity` | 14 | demo/showcase scenes |
| Shaders | `.shadergraph` | 7 | URP target — see flag below |
| Sub-packages | `.unitypackage` | 2 | HDRP + Built-In RP variants (under HDRP_BuiltIN/) |
| Lighting / Mask / Asset / Preset | various | 9 | misc |

### Flagged concerns

**No `.cs` scripts in the pack.** Confirmed via grep — clean. No
script collisions with our `LevelGen` namespace possible.

**7 `.shadergraph` files.** All target URP (file extensions confirm
this) and look pack-internal: `URPMasktTintPBR.ShaderGraph`,
`Portal`, `Water_Final`, `Water_River`, `Grass`, `Water_Fall`,
`Fire`. Our project is on URP 6.4 with Shader Graph compatibility,
so these *should* work — but pack ShaderGraph compatibility across
URP versions has been a friction point in the past. If we don't
plan to use the World 01 environment art, we can safely skip
re-importing those shaders.

**No `.overrideController` files in the pack.** All 24 controllers
are base controllers (the publisher's example animator graphs).

**Embedded `.unitypackage` files** (`HDRP.unitypackage` and
`BuiltIn.unitypackage` under `HDRP_BuiltIN/`) are alternate-pipeline
variants. We don't need them for URP. Will skip during import.

---

## Section D — Character + weapon catalog

### Weapon sets (8)

The new pack expands from the Duo's single weapon set (SwordAndShield)
to **8 distinct weapon sets**, each with full locomotion / attack /
hit / jump / move animation suites:

| Weapon set | Total FBX |
|---|---|
| **SwordAndShield** | 74 (currently used) |
| BowAndArrow | 84 |
| DoubleSword | 73 |
| MagicWand | 74 |
| NoWeapon | 94 |
| SingleSword | 73 |
| Spear | 72 |
| TwoHandSword | 73 |

SwordAndShield breakdown verified:
- 26 root-folder FBXs (Idle, Attack01-04, GetHit01, Defend, Die, etc.)
- 27 InPlace/ FBXs (Move, Sprint, Jump variants — root-motion-locked)
- 21 RootMotion/ FBXs (same moves, with translation enabled)

All 13 currently-wired clip filenames preserved exactly (typos and
all — see Section E).

### Characters (25)

The new pack does **NOT** ship a `MaleCharacterPBR.prefab` (the
character prefab the existing Duo uses). Instead it ships:

| Folder | Count | Naming |
|---|---|---|
| `Prefab/ModularCharacters/` | 24 | `MC01.prefab` through `MC24.prefab` |
| `Prefab/MaskTintCharacters/` | 1 | `MaskTint01.prefab` |

The "modular" name reflects that each MC* prefab has multiple
SkinnedMeshRenderers for swappable body parts (Body02, etc., with
`m_IsActive: 0` for inactive variants). 115 head parts under
`Prefab/HeadParts/` swap into the head slot. This is a richer
character-customization framework than the Duo's flat prefab.

For player-rig replacement, any of the 24 MC* prefabs (or
MaskTint01) will work — they all share the same Avatar source (see
Section F).

---

## Section E — Currently-used clip cross-reference

All 13 clips currently wired through `PlayerOverride_MaleHero.overrideController`:

| # | Clip | New pack path | GUID match? | Verdict |
|---|---|---|---|---|
| 1 | `Idle_Battle_SwordAndShiled.fbx` | `Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx` | ✓ `0308cf4e83cf517488b60af58b290fe0` | **MATCH** |
| 2 | `MoveFWD_Battle_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/MoveFWD_Battle_InPlace_SwordAndShield.fbx` | ✓ `7d4f9e9da55a3bd4f958a63308a522a1` | **MATCH** |
| 3 | `MoveBWD_Battle_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/MoveBWD_Battle_InPlace_SwordAndShield.fbx` | ✓ `4897d9e1e93439744a78d1cebdef17ff` | **MATCH** |
| 4 | `MoveLFT_Battle_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/MoveLFT_Battle_InPlace_SwordAndShield.fbx` | ✓ `048a541568c52514c9996fea7b37d6e0` | **MATCH** |
| 5 | `MoveRGT_Battle_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/MoveRGT_Battle_InPlace_SwordAndShield.fbx` | ✓ `f531fd2d5a6a8a440b5d450e029c4041` | **MATCH** |
| 6 | `SprintFWD_Battle_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/SprintFWD_Battle_InPlace_SwordAndShield.fbx` | ✓ `5eee3d6dbfbcef04ab20b548575d7b9d` | **MATCH** |
| 7 | `Attack01_SwordAndShiled.fbx` | `Animation/SwordAndShield/Attack01_SwordAndShiled.fbx` | ✓ `db509ad77f9b4f84a8eb1989f589b24c` | **MATCH** |
| 8 | `GetHit01_SwordAndShield.fbx` | `Animation/SwordAndShield/GetHit01_SwordAndShield.fbx` | ✓ `c98546b8d8d3ab046afc8acfe706361f` | **MATCH** |
| 9 | `JumpStart_Normal_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/JumpStart_Normal_InPlace_SwordAndShield.fbx` | ✓ `c2b2e4c79d87c3045838cbc5935d8a98` | **MATCH** |
| 10 | `JumpAir_Normal_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/JumpAir_Normal_InPlace_SwordAndShield.fbx` | ✓ `8be8f9bf3f16f184fb9719bd233874e6` | **MATCH** |
| 11 | `JumpEnd_Normal_InPlace_SwordAndShield.fbx` | `Animation/SwordAndShield/InPlace/JumpEnd_Normal_InPlace_SwordAndShield.fbx` | ✓ `8b662f6fbb996ba429182e54857361d3` | **MATCH** |
| 12 | `Attack02_SwordAndShiled.fbx` | `Animation/SwordAndShield/Attack02_SwordAndShiled.fbx` | ✓ `8283fadf2c89507469495f30db8680db` | **MATCH** |
| 13 | `Attack03_SwordAndShiled.fbx` | `Animation/SwordAndShield/Attack03_SwordAndShiled.fbx` | ✓ `9a6c3585df66f2e4782635fc7a23494c` | **MATCH** |

**13 of 13 MATCH at identical relative paths AND identical GUIDs.**

The pack's typo split is preserved (Attack/Idle clips use
`_SwordAndShiled`, others use `_SwordAndShield`).

### Why GUID identity matters

Unity's asset references are stored by GUID, not path. Both
`PlayerBaseController.controller` (state.motion fields) and
`PlayerOverride_MaleHero.overrideController` (m_Clips slots) reference
each clip via its GUID. When the Duo's pack folder is deleted from
`Assets/`, those GUIDs become unresolved (yellow warnings in the
Inspector). When the new pack is imported, the FBXes come in *with
those same GUIDs at new paths* — Unity automatically re-resolves
all references.

**Net effect:** zero re-wiring of the override controller, zero
re-wiring of the base controller's state motions, zero changes to
PlayerCombat / PlayerAnimator / PlayerController scripts. The clips
just "move" to their new paths from Unity's perspective.

This is the cleanest possible swap scenario. The publisher
deliberately preserved GUIDs across packs (a known-good Dungeon
Mason convention).

---

## Section F — Character prefab analysis

### Avatar + Controller references in new pack characters

Both `MC01.prefab` and `MaskTint01.prefab` were inspected (sample of
modular and mask-tint variants):

| Field | Value | Notes |
|---|---|---|
| Avatar GUID | `0308cf4e83cf517488b60af58b290fe0` | fileID 9000000 — Avatar sub-asset of `Idle_Battle_SwordAndShiled.fbx` |
| Controller GUID | `2be64f57d7d213648aa9b2e5e8e0a39b` | `Animator/SwordAndShieldStance.controller` (pack's example, we don't use it) |

### Comparison with existing Duo `MaleCharacterPBR.prefab`

```
MaleCharacterPBR.prefab (existing, Duo):
  m_Avatar:     guid: 0308cf4e83cf517488b60af58b290fe0  ← IDENTICAL
  m_Controller: guid: 2be64f57d7d213648aa9b2e5e8e0a39b  ← IDENTICAL
```

**Same Avatar source, same example-controller reference.** The
Humanoid Avatar embedded in `Idle_Battle_SwordAndShiled.fbx` is the
canonical retargeting source for both the old and new packs. No
retargeting differences expected at runtime.

### Prefab-structure differences

The new pack's modular characters have a different internal
hierarchy (multiple SkinnedMeshRenderers with `m_IsActive: 0` for
swappable body parts; head-slot socket for HeadParts/ swap-ins).
This is RICHER than the Duo's flat MaleCharacterPBR — but
functionally compatible:

- Same Humanoid skeleton at the root.
- Same Animator component slot.
- Same Avatar source.
- Same retarget behavior for all 13 clips.

For the player rig replacement (if Jason wants to migrate fully),
any of the 24 MC* prefabs can drop into Player_MaleHero's child
slot in place of MaleCharacterPBR. The Animator's `m_Controller`
will need to be repointed from `SwordAndShieldStance.controller`
(pack default) to our own `PlayerBaseController.controller` — same
swap we already made for the Duo's MaleCharacterPBR. Override
controller GUID also gets repointed to ours.

**No structural blockers.** This is a soft consideration for which
character variant to use, not a swap-procedure obstacle.

---

## Section G — Swap procedure recommendation

### Recommended approach: GUID-preserving swap

Given that all 13 clip GUIDs are identical, the cleanest swap is:

1. **Backup the existing override controller** to a separate
   location (or just commit current state to git first) — defensive
   safety net.

2. **Delete `Assets/AssetPacks/RPG Tiny Hero Duo/`** entirely.
   References to the 13 clips become temporarily unresolved.
   `PlayerBaseController.controller` and `PlayerOverride_MaleHero
   .overrideController` will show "Missing" yellow warnings in
   Unity for a moment.

3. **Import the new pack** to its target folder
   `Assets/RPGTinyHeroWorldBundlePBR/` (the pack's own root path).
   Skip the embedded `HDRP.unitypackage` and `BuiltIn.unitypackage`
   (URP-only project). Optionally skip the
   `RPG Tiny Fantasy World 01 PBR/` sub-pack if level art isn't
   wanted yet (528 assets, 50%+ of the pack's footprint).

4. Unity automatically re-resolves the 13 clip GUIDs to their new
   paths. No file edits needed in
   `PlayerBaseController.controller`,
   `PlayerOverride_MaleHero.overrideController`,
   `PlayerAnimator.cs`,
   `PlayerCombat.cs`,
   `PlayerController.cs`,
   or `Player_MaleHero.prefab`.

5. **Player rig (MaleCharacterPBR child) needs replacement.** The
   Duo's `MaleCharacterPBR.prefab` is gone after step 2. The
   `Player_MaleHero.prefab` will have a missing child reference.
   Pick one of the 24 MC* prefabs (or MaskTint01) and re-parent it
   under Player_MaleHero. Re-point its Animator's `m_Controller`
   to our `PlayerBaseController.controller` and `m_Avatar` is
   already correct (same GUID). Re-add the override controller
   reference.

6. **Re-run all 5 M2-B validators** to confirm the wiring survived:
   - Validate Combat Animator (Step 2)
   - Validate PlayerCombat Wiring (Step 3)
   - Validate Jump Animator (Step 4)
   - Validate Jump Runtime (Step 5)
   - Validate Combo Animator (Step 6)
   - Validate Combo Runtime (Step 7)

7. **Run the smoke tests** from M2B_03, M2B_05, M2B_07 to confirm
   end-to-end behavior.

### Why "delete then import" beats "import then delete"

Unity's asset import refuses to place a new asset at the same GUID
when an existing asset already has that GUID — this would either
silently skip the import, or worse, replace the existing asset with
the new content but at the new path (losing the old folder
structure intentions). Cleaner to delete first and let GUID
resolution re-find the assets on import.

### Cost of the swap

- **Re-wiring code/asset edits:** zero on the script and animator
  sides (GUIDs preserved).
- **Player rig prefab edit:** one (swap MaleCharacterPBR child →
  one of MC01–MC24).
- **Risk:** low. If the validators pass after swap, the runtime
  should match the Duo behavior exactly because the underlying clip
  data + Avatar are the same.

---

## Section H — Open questions for the user

1. **New pack target folder.** Recommend importing to the pack's
   own `Assets/RPGTinyHeroWorldBundlePBR/` (preserving the
   publisher's structure). This is different from the Duo's
   `Assets/AssetPacks/RPG Tiny Hero Duo/` parent. Confirm, or
   prefer to relocate under `Assets/AssetPacks/...` for symmetry?

2. **Old pack folder retention.** Recommend deleting
   `Assets/AssetPacks/RPG Tiny Hero Duo/` *before* importing the
   new pack to avoid GUID collision (both packs ship the same 13
   clip GUIDs). If you want a fallback, commit current state to git
   first; the recovery path is `git checkout` + `unitypackage`
   re-import.

3. **Sub-pack scope.** Recommend importing only the
   `RPGTinyHeroWavePBR/` sub-pack (1075 assets — characters +
   animations + weapons). Skip:
   - `RPG Tiny Fantasy World 01 PBR/` (528 environment assets) —
     not needed unless you want the demo island art for level
     mockups.
   - `HDRP_BuiltIN/` (3 assets — alternate-pipeline variants) —
     not needed for URP.

4. **Player rig character choice.** With 24 modular character
   prefabs available, which one should the player rig use? The
   prompt 2 (actual swap) needs to know:
   - Pick a specific `MC01`–`MC24` (any one will work — same
     Avatar source, same retarget behavior; choice is purely
     visual).
   - Or use `MaskTint01` (the mask-tint variant for color
     variation).
   - Or skip the rig swap and ship without a visible player mesh
     (animations still play, just on an invisible skeleton — not
     recommended).

5. **Validator + smoke-test re-run.** Should prompt 2 (the actual
   swap) automatically run all 6 M2-B validators after import, or
   leave that for manual run? Recommend automatic — they're
   cheap, deterministic, and catch the most common post-swap
   issues.

6. **Smoke-test scope.** The 3 existing smoke test docs (Step 3,
   5, 7) cover ~25 manual checks total. Recommend running:
   - M2B_03 (single attack, 10 tests) — tier-1 priority.
   - M2B_05 (jump runtime, 10 tests) — tier-1 priority.
   - M2B_07 (combo, 5 tests) — tier-1 priority.

   In total, ~25 manual test items. ~15 minutes of structured
   play. Confirms the pack swap didn't introduce a regression.

---

## Cleanup

Scratch directory `/tmp/rpg-tiny-world-bundle-inventory/` will be
deleted at end of this session. No project state was modified.
1607 manifest entries enumerated, 13 GUID comparisons completed,
2 prefab YAML inspections — all read-only.

---

**Report path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M3_01_pack_swap_inventory.md`
