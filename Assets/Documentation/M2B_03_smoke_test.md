# M2-B Step 3 — PlayerCombat Smoke Test

**Date:** 2026-04-27
**Scope:** Manual verification of buffered-combo and Hit-reaction behavior.
Run after `LevelGen ▶ Player ▶ Validate PlayerCombat Wiring (M2-B Step 3)`
returns all PASS.

---

## Setup

1. Open `Assets/Scenes/Test/Player_M1_Test.unity`.
2. Confirm `Player_MaleHero` instance is in the scene.
3. Confirm Inspector shows **PlayerCombat** component on the prefab root
   alongside CharacterController, PlayerInput, PlayerInputReader,
   PlayerAnimator, PlayerController.
4. Inspector defaults on PlayerCombat:
   - comboWindowOpen = 0.40
   - comboWindowClose = 0.80
   - bufferConsumeAt = 0.85
5. Enter Play mode.

Default attack binding (per `InputSystem_Actions.inputactions` Player map):
gamepad **West** button (Square / X) and mouse **Left Click**.

---

## Test 1 — Attack from Idle

- [ ] Stand still. Press attack.
- [ ] Attack01 plays. Character does not translate.
- [ ] After ~0.5 s, returns to Idle.

## Test 2 — Attack from Locomotion

- [ ] Hold W (or push stick forward). Press attack.
- [ ] Locomotion stops; Attack01 plays. Character roots in place.
- [ ] After Attack ends, if movement is still held, Locomotion resumes.

## Test 3 — Attack from Sprint

- [ ] Hold Sprint + W. Press attack.
- [ ] Sprint stops; Attack01 plays. Character roots in place.
- [ ] After Attack, if Sprint + forward still held, Sprint resumes.

## Test 4 — Buffered combo (single-attack edition)

- [ ] Press attack. During the swing's middle (≈ 0.21 – 0.43 s in,
      i.e. the "the swing has started but isn't recovering yet" window),
      press attack a second time.
- [ ] Near the end of the first swing (≈ 0.45 s in), Attack01 plays
      again. **Two Attack01 plays back-to-back** — proves the buffer.
- [ ] If only one swing plays, the second press landed outside the
      0.40–0.80 normalized window. Re-time the second press and try
      again, or widen the window via the Inspector for testing.

## Test 5 — Drop early input

- [ ] Press attack. Within the first ≈ 0.21 s of the swing, press
      attack again.
- [ ] Second press is dropped. Only one Attack01 plays.

## Test 6 — Drop late input

- [ ] Press attack. After ≈ 0.43 s into the swing (recovery frames),
      press attack again.
- [ ] Second press is dropped. Only one Attack01 plays.

## Test 7 — TakeHit interrupts Attack

- [ ] Press attack. While the swing is mid-play, switch to the
      Inspector. Right-click the **PlayerCombat** component header →
      **Take Hit**.
- [ ] Attack interrupts immediately. Hit reaction plays. Returns to
      Idle after ~0.4 s.

## Test 8 — TakeHit from Idle

- [ ] Stand still. Right-click PlayerCombat → Take Hit.
- [ ] Hit reaction plays. Returns to Idle.

## Test 9 — Re-hit during Hit (canTransitionToSelf)

- [ ] Right-click → Take Hit. While the Hit reaction is mid-play,
      right-click → Take Hit again.
- [ ] Hit reaction restarts from the beginning. Returns to Idle after
      the second reaction ends.

## Test 10 — Hit blocks Attack input

- [ ] Right-click → Take Hit. While in stagger, press the attack
      input.
- [ ] Attack input is dropped. Hit completes; player returns to Idle
      without a queued Attack playing.

---

## Pass criteria

All 10 tests check off without unexpected behavior. Specifically:
- No "phantom" Attack plays from buffered-but-untriggered presses.
- No drift / jitter on the player during Attack or Hit (Apply Root
  Motion is off on both states per Step 2).
- Locomotion / Sprint resume cleanly when their input is still held
  through the end of an Attack.

If anything fails, capture the failing test number + Animator state at
failure (Window ▶ Animation ▶ Animator) before debugging.

---

## Out of scope for this milestone

- Damage application (TakeHit doesn't reduce health — there is no
  health system yet).
- Enemy AI driving TakeHit calls.
- Attack02+ combo states.
- Defend / block / parry.
- Jump.
- Death / respawn (Die clip is in the override controller's source
  pack but is not yet wired into the Animator graph).

---

**Test doc path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_03_smoke_test.md`
