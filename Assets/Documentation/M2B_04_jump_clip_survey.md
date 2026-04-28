# M2-B Step 4 — Jump Clip Survey

**Date:** 2026-04-27
**Scope:** Read-only inventory of the three jump clips required for the
three-state Jump arc (`JumpStart` → `JumpAir` → `JumpEnd`). Drives Step 4
Animator wiring (this prompt) and Step 5 (`PlayerController.cs` jump
physics).
**Status:** No FAIL on any check. No WARN on any check. Does not trigger
the ⚠ FAILURES section.

Prior survey: Step 1 (`M2B_01_clip_survey_report.md`) inventoried all
SwordAndShield clips and identified jump-family candidates in Section A;
this survey validates only the three locked-in picks for the
`_Normal_InPlace_` variants.

---

## Locked picks (per prompt)

| Slot | Clip name | FBX path |
|---|---|---|
| JumpStart | `JumpStart_Normal_InPlace_SwordAndShield` | `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpStart_Normal_InPlace_SwordAndShield.fbx` |
| JumpAir   | `JumpAir_Normal_InPlace_SwordAndShield`   | `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpAir_Normal_InPlace_SwordAndShield.fbx` |
| JumpEnd   | `JumpEnd_Normal_InPlace_SwordAndShield`   | `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpEnd_Normal_InPlace_SwordAndShield.fbx` |

`InPlace` variants are the correct choice for the three-state arc:
vertical Y motion is driven by script (CharacterController + jumpVelocity
+ gravity in Step 5). The `RootMotion` variants exist in the pack
(`RootMotion/JumpFull_*_RM_*.fbx`) but would double-apply Y motion if
wired against script-driven physics.

The `JumpFull_*` clips and the Spin/Double variants under `InPlace/` are
deferred (single-jump foundation only this milestone).

---

## Frame-rate caveat

Lengths assume **30 fps** sampling (the most common default for this
character pack and consistent with Step 1's caveat). If runtime playback
looks faster or slower than the table below indicates, verify
`clip.frameRate` in the Inspector.

---

## Validation results

Source data: each clip's `clipAnimations[0]` block + `animationType` /
`avatarSetup` fields in the FBX `.meta`.

| Clip | Frames | Length @30fps | Check 1 (Rig) | Check 2 (Load) | Check 3 (Length) | Check 4 (Loop) | Check 5 (RootMotion: Orient/Y/XZ) |
|---|---|---|---|---|---|---|---|
| JumpStart_Normal_InPlace_SwordAndShield | 0–8 | 0.267 s | **PASS** (Humanoid, animationType=3, avatarSetup=2) | **PASS** (74 → 1827226128182048838 → name match) | **PASS** (≤ 0.5 s expected for takeoff) | **PASS** (loopTime=0, one-shot expected) | 1 / 1 / 1 — fully rooted |
| JumpAir_Normal_InPlace_SwordAndShield   | 0–15 | 0.500 s | **PASS** (Humanoid, animationType=3, avatarSetup=2) | **PASS** (74 → 1827226128182048838 → name match) | **PASS** (≤ 1.0 s expected for air loop) | **PASS** (loopTime=1, looping required for Air) | 1 / 1 / 1 — fully rooted |
| JumpEnd_Normal_InPlace_SwordAndShield   | 0–12 | 0.400 s | **PASS** (Humanoid, animationType=3, avatarSetup=2) | **PASS** (74 → 1827226128182048838 → name match) | **PASS** (≤ 0.5 s expected for landing) | **PASS** (loopTime=0, one-shot expected) | 1 / 1 / 1 — fully rooted |

**No FAIL on any check.** **No WARN on any check.**

Notable contrast vs. Step 1's Attack/Hit set: every jump clip has all
three root-motion flags locked AND each clip's loopTime matches its
intended use at the FBX level — the surveyed Attack/Hit clips needed
state-level loop overrides, but the jump set's import flags are already
correct. No FBX repair work is required for Step 4 wiring.

---

## Per-clip detail

### JumpStart_Normal_InPlace_SwordAndShield
- GUID: `c2b2e4c79d87c3045838cbc5935d8a98`
- Frames: 0–8 (9 frames) → 0.267 s @30fps. Brief takeoff windup; matches
  expectations for a pre-airborne pose blend.
- Loop: `loopTime: 0` — one-shot. Animator state will exit to JumpAir
  via `IsGrounded == false` (or the 0.95 fallback exit-time) before the
  clip naturally ends.
- Root motion: 1 / 1 / 1. State-level Apply Root Motion will be `off`
  in addition (belt-and-suspenders).

### JumpAir_Normal_InPlace_SwordAndShield
- GUID: `8be8f9bf3f16f184fb9719bd233874e6`
- Frames: 0–15 (16 frames) → 0.500 s @30fps. The clip plays through one
  cycle in 0.5 s and loops cleanly while the player remains airborne.
- Loop: `loopTime: 1` — looping at the FBX level. State-level loop is
  redundant but will be set anyway for clarity in the Animator graph.
- Root motion: 1 / 1 / 1. Player Y motion comes entirely from script's
  CharacterController.Move + gravity accumulation.

### JumpEnd_Normal_InPlace_SwordAndShield
- GUID: `8b662f6fbb996ba429182e54857361d3`
- Frames: 0–12 (13 frames) → 0.400 s @30fps. Brief landing recovery
  (knee bend + pose return).
- Loop: `loopTime: 0` — one-shot. Animator state will exit to Idle on
  exit-time 0.85 (~0.340 s in) + 0.10 s blend → ~0.440 s total stagger
  before locomotion resumes.
- Root motion: 1 / 1 / 1.

---

## Ground-truth file references for Step 4 wiring

When Step 4 adds slots to the override controller, all three clips must
be loaded GUID-free via `AssetDatabase.LoadAllAssetsAtPath` + name
filter. Expected FBX path constants:

```
Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpStart_Normal_InPlace_SwordAndShield.fbx
Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpAir_Normal_InPlace_SwordAndShield.fbx
Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/JumpEnd_Normal_InPlace_SwordAndShield.fbx
```

Expected internal clip names (case-sensitive, must match the FBX stem
exactly — note these use `_SwordAndShield` not the `_SwordAndShiled` typo
form found on the Idle/Attack01 clips):

```
JumpStart_Normal_InPlace_SwordAndShield
JumpAir_Normal_InPlace_SwordAndShield
JumpEnd_Normal_InPlace_SwordAndShield
```

---

## Done condition

- [x] All three clips validated against Checks 1–5.
- [x] No FAIL → no `⚠ FAILURES` section needed.
- [x] No FBX or .meta modified.
- [x] No controller / override controller modified.
- [x] No `.cs` file created or modified.

---

**Survey path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_04_jump_clip_survey.md`
