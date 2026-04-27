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

---

## M2-A Camera Follow Acceptance (added 2026-04-27)

Verified after CC Prompt 07 ran. Test in `Player_M1_Test.unity`.

Setup (already authored by `LevelGen ▶ Player ▶ Add Cinemachine Follow Camera to Active Scene`):
- `CM Brain Camera` (tagged MainCamera) replaces the old static camera
- `CM Follow Camera` (CinemachineCamera) — Follow + LookAt both on the
  player's `CameraTarget` child (local `(0, 1.6, 0)`)
- `CinemachineOrbitalFollow` Sphere mode — Radius 4, HorizontalAxis
  range (-180, 180) wrap, VerticalAxis range (-10, 70) initial 15°
- `CinemachineRotationComposer` (default composition: target centered)
- `CinemachineDeoccluder` MinDistance 1.0
- `CinemachineInputAxisController` — yaw + pitch wired to Player/Look
  (gain 0.2 X / -0.2 Y inverted); radial axis explicitly unwired

Note: the design doc's prompted "CinemachineFollow + RotationComposer"
combo doesn't expose input axes for InputAxisController. The
implementation substitutes `CinemachineOrbitalFollow` for the position
component (it owns the HorizontalAxis/VerticalAxis the input controller
drives). RotationComposer stays for look-at framing.

- [ ] **C1** Press Play. Camera renders behind and slightly above the
      player. Player visible in lower-center of frame.
      *If fails:* check `CinemachineCamera.Follow` and `LookAt` are
      both set to the player's `CameraTarget` child. If null, the
      builder didn't find the player at scene-build time.

- [ ] **C2** Push left stick / WASD. Player walks in the direction the
      camera is facing. (Push W and the player moves toward the back
      of the frame, then turns around to face that direction; rotate-
      to-face kicks in.)
      *If fails:* `PlayerController.cameraTransform` may be unset
      and the controller is falling back to world-axis movement.
      Check Console for the "No cameraTransform set" warning.

- [ ] **C3** Move right stick / mouse. Camera rotates around the
      player. Y-axis: up-stick tilts camera up; X-axis: right-stick
      orbits clockwise around player.
      *If fails:* check `CinemachineInputAxisController.Controllers`
      list — both Look Orbit X and Look Orbit Y should have
      `Reader.Input` set to the Look action reference, with non-zero
      `Gain` (±0.2). If Look input is zooming the camera in/out, the
      "Orbit Scale" axis got wired by mistake — re-run the menu item
      to defensively unwire it.

- [ ] **C4** With camera rotated 180° (looking at player's face),
      push W. Player still moves "toward where the camera is
      pointing" (i.e. away from screen center, toward player's back
      from the camera's POV). Body rotates to face that direction.
      *If fails:* the camera-relative forward math in
      `BuildCameraRelativeMove` has somehow regressed. Bisect
      git to find the change.

- [ ] **C5** Walk into a wall. Camera does not pop through the wall;
      it slides forward toward the player to maintain line of sight.
      *If fails:* `CinemachineDeoccluder` not on the vcam, or
      `CollideAgainst` layermask doesn't include the wall's layer.
      The default test scene has only a floor (no walls), so this
      item requires either adding a temporary wall via primitive
      cube or testing later in a level scene.

If C1-C5 pass, M2-A ships.

---

### M2-A Camera Fix history (2026-04-27)

**Fix attempt #1 (08-A) — REVERTED**

Hypothesis: `CinemachineRotationComposer` was overriding OrbitalFollow's
input-driven aim. Removed RotationComposer from the vcam. **This was
the wrong fix.** OrbitalFollow runs at the Body stage (position only)
and does NOT set `RawOrientation`. With no Aim-stage component, camera
position orbited correctly on input but rotation never updated — the
camera moved around the player but didn't turn to face them. C3
appeared visually broken even though OrbitalFollow's HorizontalAxis was
receiving input correctly.

**Fix attempt #2 (08-A-2) — name mismatch hypothesis — DROPPED**

Considered destroying and re-adding InputAxisController on a suspected
auto-populated-name mismatch. Empirical observation in Play mode
disproved this: `OrbitalFollow.HorizontalAxis.Value` varied with mouse
motion, confirming input WAS reaching the axis. Names matched fine —
the bug was further downstream.

**Actual fix (08-A-2 revert): restore RotationComposer**

Re-added `CinemachineRotationComposer` to the vcam. Final canonical
combo for behind-the-back follow with input-driven orbit:

| Stage | Component | Role |
|---|---|---|
| Body | `CinemachineOrbitalFollow` | Position — orbital math driven by input axes |
| Aim  | `CinemachineRotationComposer` | Rotation — keeps LookAt centered |
| —    | `CinemachineDeoccluder` | Wall collision avoidance |
| —    | `CinemachineInputAxisController` | Reads Look action, drives orbital axes |

Body and Aim are complementary in CM 3.x — they don't conflict.

**Tune note (08-A-2 sensitivity bump)**

After restoration, C3 was technically working (Brain transform was
updating with input) but visually subtle in the featureless test scene.
Bumped Reader Gain from `0.2 / -0.2` to `1.0 / -1.0`. 5 pixels of mouse
motion now produces 5° of camera rotation instead of 1°.
PlayerPrefabBuilder.cs's defaults updated to `1.0 / -1.0` so future
re-runs ship with responsive sensitivity.

Re-test C3, C4 in `Player_M1_Test.unity`. If the orbit still feels off,
tune the Gain values in the Inspector — there's no canonical "right"
value, only feel.

---

## M2 Strafe Locomotion Acceptance (added 2026-04-27)

Verified after CC Prompt 08-B ran. Test in `Player_M1_Test.unity`.
Pre-requisite: prompt 08-A has been run and C1-C5 (camera) all pass.

- [ ] **L1** Press W. Player walks toward where the camera is pointing.
      Body faces the camera's forward direction (you see the player's
      back). Forward walk clip (`MoveFWD_Battle_InPlace`) plays.
      *If fails:* check that step 8 in PlayerController.Update is
      `SetMove(input.x, input.y)`, not `SetMove(0, input.magnitude)`.

- [ ] **L2** Press A. Player walks **left relative to where the camera
      is pointing**. Body still faces camera-forward (you still see
      the player's back). Strafe-left clip (`MoveLFT_Battle_InPlace`)
      plays — feet do a sideways shuffle, body does NOT pivot to face
      left.
      *If fails:* if the body pivots, step 7 still has the rotate-
      to-face logic. If the wrong clip plays, the blend tree's LFT
      slot is mis-mapped in the override controller.

- [ ] **L3** Press D. Mirror of L2 — strafe right, MoveRGT clip plays.

- [ ] **L4** Press S. Player walks backward (away from camera). Body
      faces camera-forward (you see the player's back walking toward
      the camera). MoveBWD clip plays.
      *If fails:* check the BWD slot in the override controller. If
      the body flips around to face away from the camera, the snap-
      to-camera-yaw logic isn't running every frame.

- [ ] **L5** Hold LeftShift + W. Player accelerates to sprint speed,
      Sprint state activates, sprint clip plays.
      *If fails:* re-verify M2-C transition gates didn't regress
      (see `S1`-`S6`). Most likely: the IsSprinting write or the
      sprint multiplier was lost in the rewrite.

- [ ] **L6** Hold LeftShift + A (or D, or S). Player walks normally
      at walk speed, no sprint clip, no acceleration.
      *If fails:* the `input.y > 0.7` sprint gate isn't being
      respected. Verify step 4 in PlayerController.Update is using
      `input.y` not `input.magnitude`.

- [ ] **L7** Press W + A (diagonal forward-left). Player moves at a
      forward-left angle relative to the camera. Blend tree blends
      between FWD and LFT walk clips — feet show a hybrid stride
      (not a clean forward stride and not a clean strafe).
      *If fails:* the blend tree type may need to be Simple
      Directional 2D (not Cartesian); inspect the Locomotion state's
      blend tree configuration.

- [ ] **L8** Rotate camera 180° via right stick / mouse. Player's body
      instantly snaps to face the new camera direction. No delay, no
      smooth chase.
      *If fails:* the snap is using `Quaternion.RotateTowards` (which
      smooths) instead of `Quaternion.LookRotation` directly. Re-read
      `SnapBodyToCameraYaw` for the right pattern.

If L1-L8 pass, M2 strafe locomotion ships.

---

## MoveRGT Repair (added 2026-04-27)

Diagnosed: base controller's Locomotion blend tree (1, 0) slot was
`Missing (Motion)` in the Inspector even though the YAML had a
structurally valid reference (fileID + GUID + type 3). The override
controller had only 5 derived slots (no MoveRGT) because slots are
only derived from clips that resolve at the AssetDatabase layer.

**Corrected root cause** (the prompt's "stale GUID at Prompt 03
authoring time" hypothesis was wrong):

The reference at line 36 of `PlayerBaseController.controller`
pointed at `{fileID: 1827226128182048838, guid: f531fd2d…, type: 3}`
— the GUID was correct, the fileID is the standard Unity ID for the
first AnimationClip in an FBX. **But Unity's AssetDatabase couldn't
resolve that fileID against this specific FBX's currently-indexed
sub-assets.** When path-based lookup ran (`LoadAllAssetsAtPath`),
Unity opened the FBX and gave the AnimationClip a different
deterministic fileID (`-1574218436586762272`). Writing that new
fileID into the blend tree made the reference resolve.

**Reimport hypothesis was wrong**: forcing a reimport
(`AssetDatabase.ImportAsset` with `ForceUpdate`) did NOT regenerate
`internalIDToNameTable` for this particular FBX (still empty
post-reimport). Other FBXs in the same folder have populated tables;
this one doesn't. The actual fix was the path-based clip lookup +
writing the new fileID into the blend tree.

Fix steps:
1. `LoadAllAssetsAtPath` on the FBX → AnimationClip object resolves
   regardless of meta state (Unity reads FBX directly)
2. Write the clip into the blend tree's (1, 0) slot via the
   AnimationController API → blend tree m_Motion now uses the new
   working fileID
3. Re-derive override controller from updated base → exposes 6 slots
4. Wire the new MoveRGT override slot to itself (matches the
   semantic-no-op pattern of the other 5 slots)
5. Manually edit override YAML to remove a stale orphan slot left
   over from before the fix (old fileID `1827226128182048838`)

Lessons:
- Don't trust prompt-embedded GUID literals (Prompt 06 lesson holds).
  Re-resolve from the inventory at execution time.
- **Don't trust the AnimationController API to fail loud on broken
  motion references.** It will silently store
  `{fileID: ..., guid: ..., type: 3}` references that look valid in
  YAML but don't resolve at the AssetDatabase layer. Always verify
  by reloading from disk + iterating `BlendTree.children` and
  checking each `c.motion != null`.
- AnimatorOverrideController doesn't auto-clean orphan slots when
  the base's referenced clips change. Re-deriving via
  `runtimeAnimatorController = ...` only ADDS missing slots, never
  removes stale ones. Manual YAML cleanup or a clear-then-rederive
  pass is needed for hygiene.

Verified after save/reload:
- Base blend tree: 4 children, all resolve. (0,1) FWD, (0,-1) BWD,
  (-1,0) LFT, (1,0) RGT.
- Override controller: 6 slots, all resolve. No orphans.

Re-test L3 in `Player_M1_Test.unity`: hold D, expect strafe-right
walk clip (legs do sideways shuffle to the right, body still faces
camera-forward).

### MoveRGT Repair — Correction #2 (2026-04-27, FINAL)

The previous diagnosis above was incomplete. After applying the
fileID-rewrite fix, L3 was *partially* working — the clip appeared
in the Inspector and the blend tree's (1, 0) slot was populated —
but pressing D produced a **bunched-up "ball" pose** instead of a
proper strafe-right walk, while pressing A worked correctly.

**Actual root cause**: the MoveRGT FBX's `ModelImporter` was set to
**Generic rig** instead of Humanoid:

| Field | MoveLFT (working) | MoveRGT (broken) |
|---|---|---|
| `animationType` | 3 (Human) | **2 (Generic)** |
| `hasExtraRoot` | 1 | 0 |
| `avatarSetup` | 2 (CopyFromOther) | 0 (NoAvatar) |
| `sourceAvatar` | Idle_Battle…Avatar | (null) |
| `internalIDToNameTable` | populated | empty (downstream symptom) |

A Generic-rig AnimationClip's bone tracks don't retarget to a
Humanoid avatar — Unity tries to play the clip on the player's
Humanoid skeleton and the rig collapses, producing the
characteristic "bunched up" silhouette.

**Final fix**: copy `ModelImporter` settings from MoveLFT to
MoveRGT (`animationType` Human, `avatarSetup` CopyFromOther,
`sourceAvatar` = the shared Idle_Battle Humanoid avatar),
`SaveAndReimport`, then re-resolve the AnimationClip and re-write
the base controller's (1, 0) blend tree slot with the new
post-reimport fileID. The override controller automatically
re-derived to expose 6 clean slots.

**Earlier diagnoses in retrospect:**

1. *"GUID is wrong"* — incorrect. GUID was always right.
2. *"FileID didn't resolve"* — partial truth. The fileID didn't
   resolve because the FBX was imported as Generic with no proper
   Humanoid sub-asset structure, so Unity's deterministic ID
   generation produced different IDs than what the controller had
   stored. Rewriting with the new fileID made the reference
   *resolve* but the clip itself still couldn't *retarget* — that's
   the bunched-up pose.
3. *"`ForceUpdate` doesn't regenerate `internalIDToNameTable`"* —
   incorrect cause-and-effect. The table was empty *because* the
   FBX was imported as Generic. Once the rig type was fixed and
   reimport ran, the table populated.

**Lessons (final):**

- When an AnimationClip plays but produces a wrong pose on a
  Humanoid character, **check the FBX's `animationType` import
  setting first**. Generic vs Humanoid is the most common cause
  of "the clip is wired but the pose is broken" symptoms.
- When fixing one FBX whose import settings are wrong, **copy
  settings from a known-good sibling** rather than relying on
  reimport-with-defaults to re-derive them. Unity's import
  pipeline uses whatever settings are currently in the .meta;
  a wrong setting that's already there will be preserved by
  `SaveAndReimport`.
- The "AnimationController silently stores broken motion
  references" lesson from earlier still holds — but the deeper
  observation is that *valid-looking* motion references can also
  produce *wrong-looking* output if the underlying clip's rig
  type is wrong. Inspector resolution ≠ correct retargeting.

Verified after fix:
- MoveRGT FBX `animationType` = Human, `avatarSetup` = CopyFromOther,
  `sourceAvatar` = shared Idle_Battle Humanoid Avatar
- Base blend tree: 4/4 children resolve
- Override controller: 6/6 slots resolve
- L3 in Player_M1_Test.unity: D produces proper strafe-right walk,
  matching A's behavior on the opposite side ✓
