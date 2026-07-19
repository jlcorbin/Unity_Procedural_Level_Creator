# Session Handoff — 2026-07-18 (end of day)

## Status: M22 UE5 Player Parity Port — COMPLETE & PLAY-VERIFIED

The full UE5 `BP_RPG_PlayerCharacter` port is working in Play mode. Details +
the integration fixes are in `CLAUDE.md` under **"M22 … COMPLETE + PLAY-VERIFIED"**;
the full plan/decisions/unit table are in `Documentation/UE5_Port_Plan.md`.

Verified working: over-the-shoulder orbital camera, 6 m/s strafe movement,
8-stance **Q** dev-cycle with correct weapon rotations (both hands), per-stance
3-hit melee combo dealing damage, ranged charge/release with frame-accurate
arrow spawn, dodge (Left Ctrl), RMB target lock. All `[M22-DIAG]` logs removed.

## ▶ START HERE TOMORROW: bow + arrow mesh separation

**Goal:** `WeaponPrefab_Bows` (and the arrow rig) ship with *many* meshes layered
together, so the BowAndArrow stance shows a pile of bows/arrows instead of one.
Separate them so only the intended single bow + nocked arrow render.

Starting context for that task:
- The bow is a **skinned mesh** (`BowsSkinnedMesh.fbx`) with its own flex-idle
  controller (`BowsCTRL.controller`), not a static mesh like the other weapons.
  Wrapper prefab: `Assets/Prefabs/Weapons/WeaponPrefab_Bows.prefab`. Arrow rig:
  `WeaponPrefab_Arrows.prefab` (`ArrowsSkinnedMesh.fbx`, `ArrowsCTRL.controller`).
- Pack source: `Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/`
  — `Mesh/Weapons/BowsSkinnedMesh.fbx`, `Mesh/Weapons/ArrowsSkinnedMesh.fbx`,
  and static arrow projectiles under `Mesh/Weapons/Projectile/Arrow01–05Projectile.fbx`.
- The bow mounts to the LEFT hand in stance 7 (`BowAndArrow.asset`,
  `leftHandEuler = (0,-90,180)`). The fired projectile is the separate
  `Arrow_Projectile.prefab` (built by `RangedSetupBuilder`), independent of the
  visual nocked-arrow rig.
- Likely approach: the skinned FBX contains all bow variants as sub-meshes/bones;
  isolate the one intended bow (and one arrow) — either by deleting the extra
  SkinnedMeshRenderers/sub-objects in the wrapper prefab, or authoring a wrapper
  that references only the target mesh. Investigate the FBX hierarchy first.

## THEN: cleanup pass (before next phase)

After bow/arrow is sorted, do a cleanup pass. Candidates:
- **Delete `StanceDevCycler`** if stance-testing is done (Q cycle was always
  dev-only; nothing depends on it — the inventory equip→stance bridge is the
  real path). Also remove the `SwitchStance` input action/binding if Q is retired.
- Consider the **corrective off-hand mount** so off-hand rotations are also
  correct in the inventory equip path (currently `LeftHandEuler` fixes only the
  dev-cycle path). Offered builder: an empty child under the off-hand bone with
  the corrective rotation, wired as both `StanceController._leftHandSocket` and
  `PlayerEquipmentVisuals._offHandSocket`; then zero the `LeftHandEuler` values.
- Prune any now-unused scaffolding (e.g. the `StanceDefinition.rightHandEuler`
  field is no longer applied; `TargetLock._eyeHeight` still used).
- Re-run `LevelGen ▶ Player ▶ Validate UE5 Port` (expect 20/0) and the other
  domain validators as a regression sweep.

## Key gotchas to remember
- **Input:** LockOn/SwitchStance use DIRECT action subscription in
  PlayerInputReader, NOT UnityEvent wiring (the UnityEvent path silently didn't
  fire). Don't "fix" this back to UnityEvents.
- **Blend trees** must use the float `StanceBlend` param, not the int `WeaponType`.
- **Melee damage** needs the OnHitboxOpen/Close events on clips — re-run
  `Add Hitbox Events to Stance Attack Clips` after any clip reimport.
- Re-running `Build Stance Animator (M22 12-14)` rebuilds the melee chains
  (idempotent) — re-apply any hand-tweaks to attack states afterward.
- Serialized prefab values that C# defaults can't reach are set on
  `Player_Hero.prefab` (walkSpeed 6, jumpHeight 0.9, TargetLock 30/1.25/35).

## Deferred (unchanged from spec)
Enemy-side parity, root-motion dodge, held-draw pose + charge→power scaling,
wand cast VFX, lock-on bracket UI.
