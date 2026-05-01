# M2-B Step 5 — Jump Runtime Smoke Test

**Date:** 2026-04-27
**Scope:** Manual verification of jump physics, IsGrounded polling,
input wiring, and interactions with Attack / Hit / Locomotion / Sprint.
Run after `LevelGen ▶ Player ▶ Validate Jump Runtime (M2-B Step 5)`
returns all PASS.

---

## Setup

1. Open `Assets/Scenes/Test/Player_M1_Test.unity`.
2. Confirm `Player_MaleHero` instance is in the scene with a ground
   plane the player can land on.
3. Inspector defaults on PlayerController (root):
   - walkSpeed = 2.0, sprintMultiplier = 1.75
   - gravity = -9.81, stickyGroundVelocity = -2
   - **jumpHeight = 1.2 (NEW)**
4. Inspector defaults on PlayerCombat:
   - comboWindowOpen = 0.40, comboWindowClose = 0.80, bufferConsumeAt = 0.85
5. Enter Play mode.

Default jump binding (per `InputSystem_Actions.inputactions` Player map):
gamepad **South** button (cross / A) and keyboard **Space**.

---

## Test 1 — Jump from Idle

- [ ] Stand still. Press jump.
- [ ] JumpStart plays briefly (visible portion ≈ 0.05–0.1 s — expected;
      JumpStart clip is 0.267 s and IsGrounded flips false in 1–2
      physics frames).
- [ ] Player rises ≈ 1.2 m, JumpAir loops.
- [ ] Player falls and lands.
- [ ] JumpEnd plays, returns to Idle within ≈ 0.4 s.

## Test 2 — Jump from Locomotion

- [ ] Hold W (or push stick forward). Press jump.
- [ ] Jump arc plays. Forward motion **continues during airborne phase**.
- [ ] Lands while still moving forward. Locomotion resumes
      automatically (JumpEnd → Idle → Locomotion via existing
      `Speed > 0.1` transition).

## Test 3 — Jump from Sprint

- [ ] Hold Sprint + W. Press jump.
- [ ] Jump arc plays. Sprint horizontal speed maintained during air.
- [ ] Lands. JumpEnd → Idle → Locomotion → Sprint chain completes
      within a few frames if Sprint + forward still held.

## Test 4 — Air control

- [ ] Jump from Idle. While airborne, push movement stick in any
      direction (left, right, back).
- [ ] Player **changes horizontal direction in air**. Lands at a
      different horizontal position than launch.
- [ ] Sub-test: jump while moving forward; mid-air, push stick back.
      Player decelerates / reverses horizontal motion.

## Test 5 — Jump blocked during Attack

- [ ] Press attack. During the swing, press jump.
- [ ] Jump press is dropped. Attack completes normally.
- [ ] Player stays on ground; no JumpStart plays.
- [ ] **Sub-test (Q4 transition passthrough):** Press attack. Wait
      until the swing is nearly over and you can see the Animator
      starting to blend back to Idle. Press jump during that
      Attack→Idle blend window. Jump should fire (this is the
      intentional permissive transition behavior).

## Test 6 — Jump blocked during Hit

- [ ] Stand still. Right-click `PlayerCombat` component header → **Take Hit**.
- [ ] During the Hit reaction, press jump. Jump press is dropped.
- [ ] Hit completes, returns to Idle.
- [ ] Press jump again — fires normally on next press.

## Test 7 — Jump blocked during JumpEnd (Q5 Option A)

- [ ] Jump. Just as the player lands and JumpEnd starts playing,
      press jump again immediately.
- [ ] Second press is dropped. JumpEnd completes its ≈ 0.34 s
      visible portion + 0.10 s blend → Idle.
- [ ] Press jump again from Idle — fires normally.

## Test 8 — No double-jump / no air-jump

- [ ] Jump. While airborne (mid-rise or mid-fall), press jump again.
- [ ] Second press is dropped. Player completes the current arc.
- [ ] Lands cleanly into JumpEnd.

## Test 9 — Mid-air hit (gravity continues)

- [ ] Jump. While at the peak of the arc (≈ 0.5 s after press),
      switch to the Inspector and right-click `PlayerCombat` →
      **Take Hit**.
- [ ] GetHit01 plays (in mid-air pose — known limitation, no
      fall-through clip exists in the pack).
- [ ] **CRITICAL:** Player **continues to fall** during the Hit
      reaction. Does NOT freeze in mid-air.
- [ ] Player lands while still in Hit. Animator stays in Hit until
      its exit-time fires; then Hit → Idle.
- [ ] JumpEnd does NOT play (Animator was in Hit, not JumpAir, when
      grounding occurred — N12 didn't fire).

## Test 10 — Jump press cannot consume the attack combo buffer

- [ ] Press attack. Wait for the swing's middle (≈ 0.21 – 0.43 s in,
      i.e. comboWindowOpen 0.40 to comboWindowClose 0.80 normalized).
- [ ] Press JUMP (not attack) during this window.
- [ ] Jump is dropped (Attack state blocks via IsActionLocked).
- [ ] If you THEN press attack a second time during the same window,
      that attack press buffers normally and re-fires Attack01 at
      0.85 normalized, just like Step 3's smoke test 4.
- [ ] Confirms: jump presses do not consume the attack combo buffer
      (no PlayerCombat state corruption).

---

## Pass criteria

All 10 tests check off. Specifically:

- **Test 1 + 2 + 3:** Jump from any locomotion state plays the full
  three-state arc cleanly.
- **Test 4:** Full air control (locked design decision per Step 4).
- **Test 5 + 6:** Action lock blocks jump.
- **Test 7:** JumpEnd recovery is sacred.
- **Test 8:** No air-jump.
- **Test 9 (CRITICAL):** Mid-air hit still falls — validates
  Step 4's airborne-Hit caveat. Failure here means
  PlayerController.ApplyGravity is gating on something that shouldn't
  be gated.
- **Test 10:** PlayerCombat ↔ PlayerController are independent. Jump
  presses route to OnJumpPressed; attack presses route to
  OnAttackPressed. No cross-channel interference.

If anything fails, capture: which test number, which state the
Animator was in at failure (Window ▶ Animation ▶ Animator), and the
Inspector view of `_verticalVelocity` if visible (it isn't a
SerializeField — add `[SerializeField]` temporarily for debugging).

---

## Out of scope for this milestone

- Coyote time (jump-just-after-walking-off-ledge grace). Deferred.
- Jump buffering (jump-just-before-landing grace). Deferred.
- Variable jump height (release-jump-button-early to peak lower).
  Deferred.
- Mid-air-hit fall-through animation. Pack does not contain one.
- Wall jump / double jump / dash. Out of scope.
- Death state. Pending — Die01 clip is in the pack but not yet
  wired into the Animator graph.

---

**Test doc path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_05_jump_smoke_test.md`
