# M3-02A — Pre-Swap Player Rig Snapshot

**Date:** 2026-04-30
**Scope:** Snapshot of `Player_MaleHero.prefab` structure and the
`MaleCharacterPBR` child's Animator settings, captured **before** the
Duo→World Bundle pack swap deletes the Duo's MaleCharacterPBR.prefab.
Used by prompt M3-02B (rig swap) to know exactly what Animator
configuration to re-apply when the user picks a replacement character
variant.

**Source file:** `Assets/Prefabs/Player/Player_MaleHero.prefab`
(407 lines).

---

## Root GameObject (Player_MaleHero)

- Name: `Player_MaleHero`
- Tag: `Player`
- LocalPosition: (0, 0, 0)
- LocalRotation: identity
- Direct children (2):
  - `MaleCharacterPBR` (PrefabInstance — see below)
  - `CameraTarget`

### Components on root GameObject

| Component | Notes |
|---|---|
| `Transform` | identity transform |
| `CharacterController` (Unity) | height=1.8, radius=0.3, center=(0, 0.9, 0), skinWidth=0.08, slopeLimit=45, stepOffset=0.3 |
| `PlayerInput` (Unity InputSystem) | Actions asset GUID `052faaac586de48259a63d0c4782560b`, Behavior=InvokeUnityEvents (mode=2), 9 ActionEvents wired to PlayerInputReader (OnMove, OnLook, OnAttack, OnInteract, OnCrouch, OnJump, OnPrevious, OnNext, OnSprint) |
| `PlayerInputReader` (LevelGen) | script GUID `c8e4a3d50e23337459f70679120dfc8c` |
| `PlayerAnimator` (LevelGen) | script GUID `bc3feda790b068147b9fc581fc2190f6` |
| `PlayerController` (LevelGen) | script GUID `2138059eb31c63c4490187df3bb99bd0`, walkSpeed=2, sprintMultiplier=1.75, gravity=-9.81, stickyGroundVelocity=-2, minMoveSqr=0.0001, jumpHeight=1.2 (default), comboWindowOpen=0.4, comboWindowClose=0.8, bufferConsumeAt=0.85 (note: `rotationSpeed: 900` is also serialized but the field was removed from the script in the cleanup pass; Unity will silently strip it on next prefab save) |
| `PlayerCombat` (LevelGen) | script GUID `f9b05604b1a58294f92fff6ba4a79a46`, comboWindowOpen=0.4, comboWindowClose=0.8, bufferConsumeAt=0.85 |

---

## MaleCharacterPBR child (PrefabInstance — TO BE BROKEN BY SWAP)

`MaleCharacterPBR` is a PrefabInstance, not a regular child GameObject.
It references the Duo's source prefab and applies overrides:

- **Source prefab GUID:** `2dfbb63c9cdf7504faf4ff26b0581598`
  → `Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab`
- **PrefabInstance fileID:** `971996757509957834`

### PrefabInstance modifications applied to MaleCharacterPBR

| Property | Value | Notes |
|---|---|---|
| `m_Name` (display) | `MaleCharacterPBR` | (override of the source prefab's name) |
| Root LocalPosition | (0, 0, 0) | reset |
| Root LocalRotation | identity | reset (cleared the small `x: -0.025`/`z: 0.0013` source-prefab values) |
| Root LocalEulerAnglesHint | (0, 0, 0) | reset |
| Animator `m_Controller` | `c993ed7be6122a74cbde747bec82edcd` (type 2) → `Assets/Animators/Player/PlayerOverride_MaleHero.overrideController` | **OVERRIDE controller, not base** — this is the runtime controller; override resolves base via its own `m_Controller` field |
| Animator `m_ApplyRootMotion` | `0` (false) | root motion disabled — translation comes from `PlayerController.Move` only |

### Implicit Animator settings (from source prefab; preserved unless overridden)

| Setting | Expected value (from Duo prefab) |
|---|---|
| `m_Avatar` | GUID `0308cf4e83cf517488b60af58b290fe0` (fileID 9000000) — Humanoid Avatar from `Idle_Battle_SwordAndShiled.fbx` |
| `m_UpdateMode` | 0 (Normal — animator runs in Update phase) |
| `m_CullingMode` | 0 (Always Animate, default) |
| `m_HasTransformHierarchy` | 1 |

The new pack's MC* prefabs reference the same Avatar GUID (verified
in M3-01 Section F), so retargeting is identical post-swap.

---

## CameraTarget child (UNAFFECTED BY SWAP)

`CameraTarget` is a regular GameObject (not a PrefabInstance):
- LocalPosition: (0, 1.6, 0) — load-bearing for Cinemachine vcam follow
- No components beyond Transform
- Used by `Player_M1_Test.unity` scene's vcam as the Follow target

This GameObject is internal to `Player_MaleHero.prefab` and survives
the swap unchanged.

---

## What breaks after the Duo deletion

After Step ③ (delete Duo) of M3-02A:
- The PrefabInstance at fileID `971996757509957834` will reference a
  missing source prefab (GUID `2dfbb63c9cdf7504faf4ff26b0581598` no
  longer resolves).
- Unity will show "Missing Prefab" warnings in the Inspector when
  `Player_MaleHero.prefab` is opened.
- In Play mode, the player will run with no visible mesh (the
  CharacterController + PlayerController scripts function fine; only
  the visual rig is missing).
- The Animator parameters and override controller wiring still work
  in the abstract sense — they're on the missing PrefabInstance, so
  there's nothing to drive.

---

## What M3-02B (rig swap) needs to do

1. Pick one of the 24 MC* prefabs (or MaskTint01) from
   `Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Prefab/ModularCharacters/MC??.prefab`.

2. Replace the broken PrefabInstance under `Player_MaleHero.prefab`
   with a new PrefabInstance pointing at the chosen MC* prefab.

3. Apply the same overrides:
   - `m_Name`: `MaleCharacterPBR` (preserve the name for backward
     compatibility, OR rename to match the new chosen prefab — design
     decision for 2B).
   - Root LocalPosition / LocalRotation / LocalEulerAnglesHint: zero
     out (same as Duo).
   - Animator `m_Controller`:
     `c993ed7be6122a74cbde747bec82edcd` (PlayerOverride_MaleHero,
     unchanged).
   - Animator `m_ApplyRootMotion`: 0.

4. The `m_Avatar` does not need an override — the new MC* prefab
   already references the correct Avatar GUID (matches the Duo's by
   M3-01 Section F).

5. Re-run all 6 M2-B validators + 3 smoke tests.

---

**File path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M3_02A_preswap_player_rig.md`
