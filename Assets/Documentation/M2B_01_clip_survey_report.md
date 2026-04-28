# M2-B Step 1 — Attack & Hit Clip Survey

**Date:** 2026-04-27
**Scope:** Read-only inventory of Attack and Hit/Reaction/Death clips for the
`MaleCharacterPBR` character. Drives prompts 2 (Animator wiring) and 3
(`PlayerCombat.cs`).
**Status:** No FAIL on Check 1 (rig type). No FAIL on Check 2 (loadability) or
Check 3 (length range) for any candidate. Several WARNs on Check 4 (loop flag).
Does NOT trigger the ⚠ FAILURES section.

---

## Pack root resolution

Searched `Assets/` for FBXs containing `Idle_Battle_SwordAndShiled`. Single
match:

```
Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx
```

**Pack root (animation):** `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/`
**Pack origin (from AssetOrigin):** `RPG Tiny Hero Duo PBR Polyart` (Asset Store
ID 225148, v2.0)
**MaleCharacterPBR is a prefab name**, not a folder:
`Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab`

Resolution is unambiguous. All survey work below is rooted at the
`Animation/SwordAndShield/` folder, including its `InPlace/` and `RootMotion/`
subfolders.

---

## Frame-rate caveat (read this before trusting Check 3)

`AnimationClip.frameRate` is encoded inside the FBX binary, **not** the `.meta`.
This survey reads frame counts (`firstFrame`, `lastFrame`) directly from each
meta but cannot read the FBX-side sampleRate without loading the asset in
Unity. **All length values below assume 30 fps**, which is the most common
default for these character packs. If a clip plays unexpectedly fast or slow
in Unity, verify `clip.frameRate` in the Inspector and re-evaluate Check 3.

---

## Section A: FBX inventory

All 38 FBXs under `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/`,
sorted by full path. All have a sibling `.meta` (sanity-checked via Glob).

```
SwordAndShield/
  Attack01_SwordAndShiled.fbx
  Attack02_SwordAndShiled.fbx
  Attack03_SwordAndShiled.fbx
  Attack04_Spinning_SwordAndShield.fbx
  Attack04_Start_SwordAndShield.fbx
  Attack04_SwordAndShiled.fbx
  DefendHit_SwordAndShield.fbx
  Defend_SwordAndShield.fbx
  Die01_Stay_SwordAndShield.fbx
  Die01_SwordAndShield.fbx
  Dizzy_SwordAndShield.fbx
  GetHit01_SwordAndShield.fbx
  GetUp_SwordAndShield.fbx
  Idle_Battle_SwordAndShiled.fbx
  Idle_Normal_SwordAndShield.fbx
  Levelup_Battle_SwordAndShield.fbx
  Victory_Battle_SwordAndShield.fbx
  InPlace/
    JumpAir_Double_InPlace_SwordAndShield.fbx
    JumpAir_Normal_InPlace_SwordAndShield.fbx
    JumpAir_Spin_InPlace_SwordAndShield.fbx
    JumpEnd_Normal_InPlace_SwordAndShield.fbx
    JumpFull_Normal_InPlace_SwordAndShield.fbx
    JumpFull_Spin_InPlace_SwordAndShield.fbx
    JumpStart_Normal_InPlace_SwordAndShield.fbx
    MoveBWD_Battle_InPlace_SwordAndShield.fbx
    MoveFWD_Battle_InPlace_SwordAndShield.fbx
    MoveFWD_Normal_InPlace_SwordAndShield.fbx
    MoveLFT_Battle_InPlace_SwordAndShield.fbx
    MoveRGT_Battle_InPlace_SwordAndShield.fbx
    SprintFWD_Battle_InPlace_SwordAndShield.fbx
  RootMotion/
    JumpFull_Normal_RM_SwordAndShield.fbx
    JumpFull_Spin_RM_SwordAndShield.fbx
    MoveBWD_Battle_RM_SwordAndShield.fbx
    MoveFWD_Battle_RM_SwordAndShield.fbx
    MoveFWD_Normal_RM_SwordAndShield.fbx
    MoveLFT_Battle_RM_SwordAndShield.fbx
    MoveRGT_Battle_RM_SwordAndShield.fbx
    SprintFWD_Battle_RM_SwordAndShield.fbx
```

**Note on FBX naming inconsistency:** The pack mixes `_SwordAndShiled` (typo,
no second `e`) and `_SwordAndShield` (correct). Both spellings are real on
disk — the existing override controller already uses
`Idle_Battle_SwordAndShiled` (typo form). Match exact filenames when wiring
new override slots.

---

## Section B: Candidate clips

10 clips passed the name filter. None of the FBX inventory contained
`hit`, `gethit`, `take_hit`, `damage`, `damaged`, `stagger`, `reaction`,
`knockback`, `knock_back`, `knock`, `knockdown`, `knock_down`, `flinch`,
`death`, `die`, `dead`, or `recoil` outside the entries below.

Each FBX contains exactly one `AnimationClip` sub-asset (per its
`internalIDToNameTable`, the single mapping `74 → 1827226128182048838`
returns the clip whose name matches the FBX stem).

### Attack
| Clip name | FBX |
|---|---|
| `Attack01_SwordAndShiled` | `Attack01_SwordAndShiled.fbx` |
| `Attack02_SwordAndShiled` | `Attack02_SwordAndShiled.fbx` |
| `Attack03_SwordAndShiled` | `Attack03_SwordAndShiled.fbx` |
| `Attack04_SwordAndShiled` | `Attack04_SwordAndShiled.fbx` |
| `Attack04_Spinning_SwordAndShield` | `Attack04_Spinning_SwordAndShield.fbx` |
| `Attack04_Start_SwordAndShield` | `Attack04_Start_SwordAndShield.fbx` |

### HitFamily
| Clip name | FBX |
|---|---|
| `DefendHit_SwordAndShield` | `DefendHit_SwordAndShield.fbx` |
| `GetHit01_SwordAndShield` | `GetHit01_SwordAndShield.fbx` |

### Death
| Clip name | FBX |
|---|---|
| `Die01_SwordAndShield` | `Die01_SwordAndShield.fbx` |
| `Die01_Stay_SwordAndShield` | `Die01_Stay_SwordAndShield.fbx` |

### Other
None. `Dizzy_SwordAndShield` matched no filter token (no `stagger`/`flinch`/
`hit`/`recoil`/etc.), so it was not collected. `Defend_SwordAndShield` (no
`hit`) likewise excluded. `GetUp_SwordAndShield` excluded.

---

## Section C: Validation results

Source data: each clip's `clipAnimations[0]` block in the FBX `.meta`. Lengths
are `(lastFrame − firstFrame) / 30 fps` (see frame-rate caveat above).

Root-motion column reports the three "Bake Into Pose" flags (in `.meta` they
appear as `keepOriginalOrientation` / `keepOriginalPositionY` /
`keepOriginalPositionXZ`). When ALL three are `1`, the clip is fully rooted —
motion stays in pose, root stays put. When any is `0`, root motion can
escape on that axis. For attacks the user wants rooted, all three should be
`1`.

| Clip | Frames | Length @30fps | Check 1 (Rig) | Check 2 (Load) | Check 3 (Length) | Check 4 (Loop) | Check 5 (RootMotion: Orient/Y/XZ) |
|---|---|---|---|---|---|---|---|
| Attack01_SwordAndShiled | 0–16 | 0.533 s | PASS (Humanoid) | PASS | PASS | **WARN** (loopTime=1) | 1 / **0** / 1 — Y unlocked, may translate vertically |
| Attack02_SwordAndShiled | 0–16 | 0.533 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |
| Attack03_SwordAndShiled | 0–16 | 0.533 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |
| Attack04_SwordAndShiled | 0–16 | 0.533 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |
| Attack04_Spinning_SwordAndShield | 0–10 | 0.333 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |
| Attack04_Start_SwordAndShield | 0–6 | 0.200 s | PASS | PASS | PASS | PASS (loopTime=0) | 1 / 1 / 1 — fully rooted |
| DefendHit_SwordAndShield | 0–10 | 0.333 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |
| GetHit01_SwordAndShield | 0–14 | 0.467 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |
| Die01_SwordAndShield | 0–15 | 0.500 s | PASS | PASS | PASS | PASS (loopTime=0) | 1 / 1 / 1 — fully rooted |
| Die01_Stay_SwordAndShield | 0–20 | 0.667 s | PASS | PASS | PASS | **WARN** (loopTime=1) | 1 / 1 / 1 — fully rooted |

**No FAIL on any check.** All candidates are Humanoid (matches the player
avatar) and all have a length in the sane range. No `⚠ FAILURES` section is
needed.

**WARN summary:**
- Eight clips have `loopTime: 1` (Loop Time checked at import). Attacks and
  hit reactions should be one-shot. The Animator state should set Loop
  behaviour on the state itself rather than relying on the import flag, OR
  the user can fix the import flags before adding to the override controller.
  Either approach works; the latter is cleaner.
- `Attack01_SwordAndShiled` has `keepOriginalPositionY: 0` (Bake Into Pose
  unchecked for Y). This is the **only** outlier in the set — Attack02-04
  and all hit/death clips lock all three. If Attack01 is selected as
  Attack01, expect a small vertical wobble unless the controller's state
  has Apply Root Motion off (or the Y flag is corrected at import).

---

## Section D: Hit-family directional grouping

Hit-family candidates: `DefendHit_SwordAndShield`, `GetHit01_SwordAndShield`.

| Clip | Front | Back | Left | Right | Generic |
|---|---|---|---|---|---|
| `DefendHit_SwordAndShield` | — | — | — | — | ✓ |
| `GetHit01_SwordAndShield` | — | — | — | — | ✓ |

**Both clips are Generic.** No directional variants exist anywhere in the
pack — no `_F`/`_B`/`_L`/`_R` or `front`/`back`/`left`/`right` tokens were
found in any candidate name.

This **constrains the hit-reaction design to non-directional**. A directional
1D blend tree on a hit-direction parameter (Section F option 3b) is **not
viable from this pack alone** unless additional clips are sourced or
directional reactions are authored manually. The cheapest viable design is
3a (single Generic Hit state).

---

## Section E: Recommended picks for prompt 2

### Attack01 candidate — TIE between Attack01 and Attack02

Two clips are dead-even on the rubric. **The user must pick one.**

**Option α — `Attack01_SwordAndShiled`**
Rationale:
- Filename matches the override-slot naming convention (`Attack01` =
  "first attack").
- Length 0.533 s — slightly under the 0.6–1.5 s preferred range, but
  close enough for a fast melee swing.
- **Caveat:** `keepOriginalPositionY: 0`. Either disable Apply Root Motion
  on the Animator state, or fix the import flag before wiring. Otherwise
  expect minor Y drift during the swing.

**Option β — `Attack02_SwordAndShiled`**
Rationale:
- All three root-motion flags locked → cleanest "rooted attack" out of
  the box, no Inspector tweaks needed.
- Length 0.533 s — same as Attack01.
- **Caveat:** Filename is `Attack02`, which feels off-pattern if the slot
  is conceptually "Attack01."

The choice depends on whether the user prefers convention (Attack01) or
zero-config rooting (Attack02). All three of Attack02/Attack03/Attack04
are interchangeable on the rubric — Attack02 is recommended over Attack03/04
purely because it's the lowest-numbered clean option.

`Attack04_Spinning_SwordAndShield` (0.333 s) and `Attack04_Start_SwordAndShield`
(0.200 s) are too short for a primary attack slot; they look like a combo
spinner and a wind-up frame respectively.

### Hit01 candidate — `GetHit01_SwordAndShield`

Rationale:
- Filename matches the override-slot naming convention exactly
  (`GetHit01` = "first hit reaction").
- Length 0.467 s — squarely in the 0.3–0.9 s stagger range.
- All three root-motion flags locked → no positional drift.
- Generic direction → fits the simplest hit-reaction model.
- **Caveat:** `loopTime: 1`. Set the Animator state to non-looping or
  fix the import flag.

`DefendHit_SwordAndShield` is shorter (0.333 s) and reads as a "shield-block
flinch" rather than a generic damage reaction. Reserve it for a future
defend/block state instead of using it as the hit-reaction primary.

---

## Section F: Open questions

1. **Attack01 slot:** Use Option α (`Attack01_SwordAndShiled`, matches name
   convention, has Y root-motion caveat) or Option β
   (`Attack02_SwordAndShiled`, fully rooted, but off-pattern name)?

2. **Hit01 slot:** Confirm `GetHit01_SwordAndShield` (recommended) — or
   substitute `DefendHit_SwordAndShield` if the design wants a
   shield-flinch flavour instead.

3. **Hit-reaction model.** Section D establishes that no directional
   variants exist in this pack. Therefore:
   - **(a)** Single Generic Hit state (simplest, only viable option from
     the pack alone).
   - **(b)** Directional 1D blend tree on a hit-direction parameter —
     **not viable** without sourcing additional clips or authoring
     directional reactions. Skip unless Jason is willing to add clips.
   - **(c)** Defer — start with Generic, plan to add directional later.

4. **Section B "Other" category is empty.** No clips matched the filter
   but landed outside Attack/Hit/Death. No special-case decisions needed.

5. **Strong root motion concerns.** Only `Attack01_SwordAndShiled` has
   `keepOriginalPositionY: 0`. All other 9 candidates are fully rooted
   on all three axes. If Option α is selected for Attack01, decide:
   - Disable Apply Root Motion on the Attack state in the Animator, or
   - Fix the import flag (`keepOriginalPositionY: 1`) before adding to
     the override controller.

   The pack contains no clip whose root motion is intended to be kept
   (e.g., a knockback that should physically translate the character) —
   so the user does not need to make a "preserve vs suppress" call on
   any clip beyond Attack01's Y flag.

6. **`loopTime: 1` on most candidates.** All eight non-PASS clips on
   Check 4 will require either:
   - Animator state with Loop = false (decision lives in the state, not
     the clip), or
   - Pre-fix the import setting before wiring (`loopTime: 0`).

   Recommend Animator-state approach: it's lossless on the FBX side and
   leaves the door open to mirror or remix clips later.

7. **Frame rate confirmation.** Section C lengths assume 30 fps. Before
   prompt 2 commits to Animator state lengths or transition exit-times,
   confirm `clip.frameRate` in the Inspector for at least
   `Attack01_SwordAndShiled` and `GetHit01_SwordAndShield`. If the value
   is 60 fps, halve all reported lengths and re-evaluate Check 3.

---

## Done condition checklist

- [x] `Assets/Documentation/M2B_01_clip_survey_report.md` exists.
- [x] All six sections (A–F) present.
- [x] No `⚠ FAILURES` section needed (no FAIL on Check 1, 2, or 3).
- [x] Pack root path resolved unambiguously and printed.
- [x] No Animator, override controller, or FBX file modified.
- [x] No new `.cs` file created.

---

**Report path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_01_clip_survey_report.md`
