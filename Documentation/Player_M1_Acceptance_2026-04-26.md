# Player M1 Acceptance Checklist — 2026-04-26

The ten test items below are pulled verbatim from
[Player_Animator_Design_2026-04-26.md](Player_Animator_Design_2026-04-26.md) §7.
M1 ships when **all ten boxes** check off in
[Assets/Scenes/Test/Player_M1_Test.unity](../Assets/Scenes/Test/Player_M1_Test.unity).

**Scene setup** (already authored by `LevelGen ▶ Player ▶ Create M1 Test Scene`):
- Floor plane at origin, scaled (2, 1, 2) for a 20×20 m surface
- Main Camera at `(0, 5, -8)`, rotation `(25, 0, 0)`, FOV 60 (static, does not follow)
- Directional Light from `NewSceneSetup.DefaultGameObjects`
- `Player_MaleHero.prefab` instance at world origin

---

## Acceptance items

- [ ] **T1** `Player_MaleHero.prefab` drops cleanly into a scene with no
  missing-component errors in the inspector.

  *If fails:* check Console for missing-script errors. Most common cause:
  a `LevelGen.Player.*` script failed to compile silently. Re-run prompt 04
  verification. Second-most-common: the InputSystem package was removed —
  confirm `com.unity.inputsystem` in `Packages/manifest.json`.

- [ ] **T2** Press Play. No console errors during scene load or first frame.

  *If fails:* the most likely culprit is the InputSystem PlayerInput
  component not finding the actions asset. Re-open the prefab in inspector
  and confirm the Actions field shows `InputSystem_Actions`, not None. If
  Actions is set but errors persist, the `m_ActionEvents` array may not have
  rebuilt — re-run `LevelGen ▶ Player ▶ Build Player_MaleHero Prefab`.

- [ ] **T3** Character renders standing in the Idle pose; the
  `Idle_Battle_SwordAndShield` clip plays and visibly loops (slight
  weight-shift breathing motion at ~0.667 s cycle).

  *If fails:* If the character is in T-pose, the override controller didn't
  bind the Idle clip. Open
  `Assets/Animators/Player/PlayerOverride_MaleHero.overrideController` and
  confirm the Idle slot points at `Idle_Battle_SwordAndShield`. If the
  clip is wired but doesn't play, check the nested
  `MaleCharacterPBR/Animator` Controller field — it must be the override
  controller, not the pack's `SwordAndShieldStance`.

- [ ] **T4** With gamepad left-stick pushed forward (or W key held), the
  character rotates toward the camera-forward direction and walks at ~2 m/s.

  *If fails:* If nothing happens on input, check that the Game view has
  focus (InputSystem only reads keyboard/mouse from the focused window).
  If the character plays a walk anim but doesn't translate, `CharacterController`
  may be misconfigured — confirm `radius=0.3, height=1.8, center=(0,0.9,0)`
  on the prefab root. If translation works but no anim, verify the
  `Speed` parameter is being written (Animator window during Play should
  show `Speed > 0.1`).

- [ ] **T5** With left-stick pushed in any other direction (or A/S/D),
  character rotates to face that camera-relative direction and walks at
  ~2 m/s in that direction.

  *If fails:* If the character walks but doesn't rotate, check
  `PlayerController.rotationSpeed` on the prefab root (default 900 °/s).
  If it walks in world axes instead of camera-relative, the `cameraTransform`
  field on `PlayerController` didn't auto-resolve to `Camera.main` — assign
  the Main Camera transform to the field directly.

- [ ] **T6** Releasing input causes the character to return to Idle
  visually within ~0.15 s of input deadzone trigger (`Speed < 0.1`).

  *If fails:* If the character keeps walking-in-place after release, the
  `MoveInput` property isn't returning to zero — confirm
  `PlayerInputReader.OnMove` is wired in the PlayerInput inspector
  (Behavior must be `Invoke Unity Events`, and the Move action must list
  `PlayerInputReader.OnMove` under its event slot). If the transition
  never fires, verify the `Speed` parameter and the two transitions in
  `PlayerBaseController.controller`.

- [ ] **T7** `CharacterController.isGrounded` reads `true` while standing
  on the floor; character does not fall through.

  *If fails:* If the character falls forever, the floor plane is missing
  a Mesh Collider — `GameObject.CreatePrimitive(Plane)` should ship one;
  inspect the `Floor` GameObject and confirm. If it's there but `isGrounded`
  is false, `stickyGroundVelocity` may be too small — try `-5f` instead of `-2f`.
  If the character clips through, `radius` may be too small relative to
  `skinWidth` (current values: 0.3 / 0.08 — both reasonable).

- [ ] **T8** Walking the character outside the camera frustum does NOT
  error. Camera follow is explicitly deferred — character may exit view
  and that's expected.

  *If fails:* This shouldn't be able to error in M1. If it does, something
  unrelated is broken — capture the stack trace and investigate.

- [ ] **T9** Inspector check: the runtime prefab's `MaleCharacterPBR` child
  has `applyRootMotion = false` on its Animator. (Failure mode: visual
  pinning at origin while collider walks.)

  *If fails:* The pack ships `applyRootMotion: true`, so this is the most
  likely-to-regress override. Re-run `LevelGen ▶ Player ▶ Build
  Player_MaleHero Prefab` — that build path explicitly sets the field to
  false. The prompt-03 lesson applies: in-memory wiring that doesn't
  survive save/reload is a real Unity trap.

- [ ] **T10** UnityEvent stub actions log a trace when triggered: press
  jump (space), attack (mouse1), interact (E) — each emits one log line
  per press, no more, no errors.

  *If fails:* If presses produce no log, `PlayerInput → PlayerInputReader`
  wiring is missing. Inspect the Unity `PlayerInput` component on the
  prefab root, expand the actionEvents list, and confirm each action has
  one entry pointing at `PlayerInputReader.On<Action>`. If presses produce
  three logs each, the `if (ctx.performed)` gate was dropped from the
  stub method — restore it per prompt 04 spec.

---

## When all ten check off

M1 is done. Update [CLAUDE.md](../CLAUDE.md)'s `Player (M1 — COMPLETE)` block
if any item differs from the spec, and proceed to M2 planning.

---

## M2-C Sprint Acceptance (added 2026-04-27)

Verified after CC Prompt 06 ran. Test in `Player_M1_Test.unity`.

- [ ] **S1** Hold LeftShift (or gamepad L3) while pressing W: character
      visibly speeds up and switches to a faster leg cycle.
      *If fails:* check Console for `IsSprinting` parameter writes;
      open Animator window with player selected at runtime and verify
      Sprint state activates.

- [ ] **S2** Release LeftShift: character smoothly returns to walk pace
      within ~0.15s.
      *If fails:* check the three Sprint→Locomotion transitions exist
      and `IsSprinting==false` is one of them.

- [ ] **S3** While sprinting forward, swing the stick 90° to strafe-left:
      character drops to walk pace and plays the strafe-left walk clip,
      not sprint.
      *If fails:* the `MoveZ < 0.7` Sprint→Locomotion transition isn't
      firing. Check the threshold in the controller.

- [ ] **S4** While sprinting, release the stick entirely: character
      stops, returns to Idle. (Sprint→Locomotion→Idle chain.)
      *If fails:* the `Speed < 0.1` transition isn't firing on the
      Sprint state. Verify it exists, separate from the IsSprinting
      transition.

- [ ] **S5** Press LeftShift while standing still (no W): nothing
      visible happens. Character stays in Idle. (Sprint requires
      Speed > 0.1 to enter.)
      *If fails:* the Locomotion→Sprint transition's `Speed > 0.1`
      condition isn't being respected. Note: this is the *entry*
      condition — Sprint can't be entered from Idle directly; it
      requires Locomotion as a stepping stone, which itself requires
      Speed > 0.1.

- [ ] **S6** Console clean. No errors during sprint engage/release.

If S1-S6 pass, M2-C ships.
