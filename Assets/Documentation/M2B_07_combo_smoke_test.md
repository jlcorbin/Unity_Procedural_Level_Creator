# M2-B Step 7 — 3-Hit Combo Smoke Test

**Date:** 2026-04-29
**Scope:** Manual verification of the runtime 3-hit combo extension
(Attack01 → Attack02 → Attack03). Builds on Step 3's 10-test single-
attack smoke run; this doc adds 5 combo-specific cases. Run after
`LevelGen ▶ Player ▶ Validate Combo Runtime (M2-B Step 7)` reports
all PASS.

---

## Setup

1. Open `Assets/Scenes/Test/Player_M1_Test.unity`.
2. Confirm `Player_MaleHero` is in the scene with a ground plane and
   the M2-A camera setup intact (Cinemachine vcam, etc.).
3. Open the Animator window (Window ▶ Animation ▶ Animator) and dock
   it where you can see it during play. Select Player_MaleHero so the
   live state highlight follows the player. This is essential for
   diagnosing combo flow — you want to *see* Attack → Attack02 →
   Attack03 transitions land.
4. Inspector defaults on PlayerCombat:
   - comboWindowOpen = 0.40
   - comboWindowClose = 0.80
   - bufferConsumeAt = 0.85
5. Default attack binding (per `InputSystem_Actions.inputactions`
   Player map): mouse left click + gamepad West button (square / X).
6. Enter Play mode.

---

## Test 11 — Full 3-hit combo

- [ ] Stand still in Idle. Press Attack three times rapidly. Time each
      press during the previous swing's middle (~0.3 s into each
      ~0.5 s swing — anywhere in the [0.40, 0.80] window).
- [ ] Animator transitions visibly: Idle → Attack → Attack02 →
      Attack03 → Idle.
- [ ] All three attack clips play (Attack01_SwordAndShiled,
      Attack02_SwordAndShiled, Attack03_SwordAndShiled). They look
      visually distinct — different swing arcs, weapon trajectories.
- [ ] Total combo end-to-end ≈ 3 × 0.5 s + transition blends ≈ 1.6 s.
- [ ] After Attack03, lands cleanly back in Idle (NOT Attack01,
      NOT Attack02).
- [ ] Pass criterion: Animator window shows the full chain;
      no warnings or errors in Console.

## Test 12 — Combo drops if not buffered

- [ ] Stand still in Idle. Press Attack ONCE. Do NOT press again.
- [ ] Attack01 plays. Animator transitions Attack → Idle at the 0.90
      exit-time of N4.
- [ ] **Animator never enters Attack02.** Verifies the fallback path
      (N4) wins when ComboNext is not set.
- [ ] Press Attack a second time well after Attack01 has finished
      (back in Idle). Same single-attack behavior — Idle → Attack →
      Idle. Confirms the buffer logic fully reset between presses.

## Test 13 — Combo caps at Attack03

- [ ] Press Attack three times to walk the full combo (Test 11
      success first, then re-trigger).
- [ ] During Attack03 (the third swing), press Attack a fourth time
      anywhere — early, middle, late. Whatever timing.
- [ ] **Fourth press is dropped.** No Animator state change. Attack03
      plays through to Idle as normal via N17.
- [ ] After Attack03 → Idle completes, immediately press Attack again.
- [ ] **The next press starts a NEW combo at Attack01** (not Attack02
      or Attack03). Verifies combo state is fully reset by the
      Attack03 → Idle transition.

## Test 14 — Hit cancels combo mid-chain

- [ ] Press Attack twice to land in Attack02 (combo step 2).
- [ ] While Attack02 is playing, switch to the Inspector and right-
      click `PlayerCombat` component header → **Take Hit**.
- [ ] Animator transitions Attack02 → Hit immediately (N5 wins —
      AnyState → Hit has no exit-time).
- [ ] Hit clip plays (GetHit01_SwordAndShield). Combo is cancelled.
- [ ] Hit completes via N6 (exit-time 0.85) → Idle.
- [ ] Press Attack from Idle. **The next press starts FRESH at
      Attack01** (not Attack02 or Attack03). Confirms the Hit
      interrupt cleared `_attackBuffered` and the Animator state
      is fully reset.

## Test 15 — Jump during combo doesn't consume buffer (regression)

- [ ] Press Attack once. During the combo window (~0.3 s into Attack01,
      i.e. n in [0.40, 0.80]), press JUMP (not attack).
- [ ] Jump press is dropped (PlayerController's `IsActionLocked`
      blocks jump while in Attack — Step 5 design).
- [ ] **Press Attack a second time before the window closes.** This
      sets `_attackBuffered = true`.
- [ ] At the buffer-consume threshold (~0.85 normalized), Attack02
      fires via SetComboNext. Combo continues normally.
- [ ] Verifies: jump press did NOT corrupt the attack buffer or fire
      ComboNext early. PlayerCombat and PlayerController are
      independent (Step 5 single-direction architecture preserved).

---

## Pass criteria

All 5 tests check off cleanly. Specifically:

- **Test 11:** 3-hit combo plays end-to-end with visible Animator
  state walks. The headline test for Step 7.
- **Test 12:** No-buffered-press path falls back to Idle correctly
  (N4 wins). Validates the conditional-routing exit-time priority
  from Step 6's design.
- **Test 13:** Combo cap at Attack03 holds. Validates the explicit
  drop in OnAttackPressed (Step 7 Section 3 of the behavior table).
- **Test 14:** Hit-cancels-combo works on Attack02 (and by
  extension Attack03 — the AnyState transition covers both for
  free per Step 6 Section 6.8).
- **Test 15:** Step 5's independent input routing survives the
  combo extension. No cross-channel interference between jump and
  attack inputs.

If anything fails, capture: which test number, the Animator window
state at failure, and the value of `_attackBuffered` (add a
`[SerializeField]` temporarily to PlayerCombat for inspector
visibility if needed).

---

## Out of scope for this milestone

- Attack04 / heavy-attack / finisher. Clip is validated in pack
  (Step 1 Section C) and reserved but not wired.
- Per-hit damage values, hitbox enable/disable timing, hit reactions
  per direction, knockback. Combat is animation-only at this stage.
- Combo branching (e.g., if you press a different button during the
  window, route to Attack02_Heavy instead of Attack02). Linear
  3-hit combo only.
- Movement during Attack02 / Attack03. Per Step 7 behavior table
  Section 5.5: only Attack01 and Hit lock movement today; Attack02
  and Attack03 allow input movement (which is generally desirable
  feel — micro-positioning between hits). Locking those states is a
  future tuning decision.

---

## Test doc paths

- Step 3 (single attack): `Assets/Documentation/M2B_03_smoke_test.md`
- Step 7 (combo extension, this doc):
  `Assets/Documentation/M2B_07_combo_smoke_test.md`
- Step 5 (jump runtime): `Assets/Documentation/M2B_05_jump_smoke_test.md`

Run all three when validating M2-B as a whole.
