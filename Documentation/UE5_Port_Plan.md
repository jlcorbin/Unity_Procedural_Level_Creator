# M22 — UE5 Player Parity Port

**Goal:** bring the Unity player character to behavioral parity with the UE5
`BP_RPG_PlayerCharacter` described in
`Documentation/PlayerCharacter_MechanicsSpec_ForUnity.md`, while preserving the
shipped Unity-only systems where they don't conflict.

Source of truth for target behavior: the UE5 spec (values verified 2026-07-18).
Source of truth for what already exists: the M1–M21 milestone log in `CLAUDE.md`.

---

## Locked decisions (from session kickoff, 2026-07-18)

1. **Fidelity = "match feel, keep extras."** Adopt UE5 values, input map, and
   direction models, but RETAIN the shipped Unity-only features that don't
   contradict the spec:
   - Dodge keeps its **stamina cost (25)** and **i-frames (0.5 s)** — UE5 has
     neither, but they're kept.
   - **Sneak** stays (UE5 has no sneak).
   - Inventory / equip / HUD / player-death / enemy systems stay intact.
2. **Stance system COEXISTS with inventory (Q-cycle is DEV-ONLY).**
   - The 8-stance model is layered on top of the existing inventory `Equip`
     path. Equipping an item also sets the matching stance.
   - The **`Q` cycle key is a development-only affordance** for fast stance
     testing. It lives in an isolated, easily-removable component
     (`StanceDevCycler`) with a clear `// DEV-ONLY` banner. Nothing else in the
     game depends on `Q` existing — removing that one file must not break the
     build.
3. **Melee combo = keep the shipped 3-hit chain; DROP heavy hits 4 & 5.**
   - The UE5 `ComboIndex % 5` light/heavy split is **NOT** ported.
   - No upper-body avatar-mask layer is added.
   - Existing `PlayerCombat` 3-hit buffered combo (Attack → Attack02 →
     Attack03) is retained. Work in P4 is limited to routing that chain to each
     of the 8 stances' clip sets and aligning damage.
   - Existing weapon-collider hit detection is kept (already integrated with
     damage numbers + enemy hit reactions). UE5's `OverlapSphere(front, 150)`
     is NOT swapped in.
4. **Camera = keep the Cinemachine ORBITAL rig; make it over-the-shoulder.**
   - Do NOT rebuild as a UE-style spring-arm.
   - Tune the existing `CinemachineOrbitalFollow` + `RotationComposer` to sit
     over the right shoulder (lateral screen/target offset) instead of directly
     behind. UE camera numbers are adapted to the orbital rig, not ported
     literally.
5. **Target lock stays on RMB** (not rebound to Tab). Everything else in the
   UE input map is adopted (Q = stance, Left Ctrl = dodge).

---

## Unit conversion — UE centimeters → Unity meters

The UE5 spec is authored in **centimeters**; this project is in **meters**.
Canonical rule: **÷ 100** for all distances/speeds. Angles and scalar rates are
unchanged. Centralized in `Assets/Scripts/Player/UnrealUnits.cs`.

| UE5 value | Meaning | Unity value |
|---|---|---|
| 600 | max walk speed | 6.0 m/s |
| 2048 | max accel / braking | 20.48 m/s² |
| 420 | jump launch velocity Z | 4.2 m/s → height ≈ 0.90 m |
| 0.05 | air control | 0.05 (unitless, unchanged) |
| 500°/s | yaw turn rate | 500°/s (unchanged) |
| 88 / 34 | capsule half-height / radius | 1.76 / 0.34 m (full height 1.76) |
| 350 | camera boom length | 3.5 m |
| 60 / 25 | boom socket offset Y (right) / Z (up) | 0.60 / 0.25 m |
| 90 | camera FOV | 90 |
| 150 | melee overlap radius (NOT ported — kept for reference) | 1.5 m |
| 20 | melee damage | 20 |
| 3000 / 125 | lock spherecast dist / radius | 30 / 1.25 m |
| 100 | lock aim Z offset | 1.0 m |
| 6000 | arrow speed | 60 m/s |
| 0.4 | arrow gravity scale | 0.4 (unchanged) |
| 5 | arrow collision radius | 0.05 m |
| 5000 | free-aim camera ray length | 50 m |
| 30 | arrow damage | 30 |

---

## The 8 stances (spec §6) → Unity asset mapping

Weapon prefabs live at `Assets/Prefabs/Weapons/WeaponPrefab_<name>.prefab`
(57 built in M20c). Per-hand euler rotations from spec §6.

| # | Stance | Right hand prefab | Left hand prefab | R euler | L euler | Ranged | Combo WeaponType |
|---|---|---|---|---|---|---|---|
| 0 | NoWeapon | — | — | 0 | 0 | no | Unarmed |
| 1 | SingleSword | `WeaponPrefab_OHS03` | — | 0 | 0 | no | OHS |
| 2 | TwoHandsSword | `WeaponPrefab_THS01` | — | 0 | 0 | no | THS |
| 3 | SwordAndShield | `WeaponPrefab_OHS03` | `WeaponPrefab_Shield04` | 0 | (0,−180,0) | no | OHSShield |
| 4 | DoubleSword | `WeaponPrefab_OHS03` | `WeaponPrefab_OHS03` | 0 | (0,−180,−90) | no | *(new: DoubleSword)* |
| 5 | Spear | `WeaponPrefab_Spear01` | — | (0,0,+10) | 0 | no | Spear |
| 6 | MagicWand | `WeaponPrefab_Wand01` | — | 0 | 0 | **yes** | *(ranged)* |
| 7 | BowAndArrow | — | `WeaponPrefab_Bows` (skinned rig) | 0 | (0,170,0) | **yes** | *(ranged)* |

Notes:
- Bow (7) is a **skinned** mesh with its own idle-flex controller + a nocked
  arrow (`WeaponPrefab_Arrows`); it mounts to the **left** hand with an extra
  +90 yaw on its own transform (stacked with the −? left-hand 170 yaw).
- The Animator `WeaponType` int is driven by the **stance index (0–7)**. The
  existing enum (`Unarmed/OHS/OHSShield/THS/Spear`) is a subset; per-stance
  Attack states for DoubleSword / SwordAndShield / (ranged) are added in the
  Animator graph (manual step P10).

---

## Phase breakdown

| Phase | Scope | Primary agent | Status |
|---|---|---|---|
| P0 | Design doc + `UnrealUnits` + `Stance` enum + `StanceDefinition` SO | (orchestrator) | **in progress** |
| P1 | Cinemachine orbital → over-the-shoulder offset | unity-specialist | pending |
| P2 | Movement retune to spec values | gameplay-programmer | pending |
| P3 | Stance system + dev-only Q cycler + equip→stance bridge | gameplay-programmer / unity-specialist | pending |
| P4 | Extend 3-hit combo to all 8 stances; align damage | gameplay-programmer | pending |
| P5 | Ranged charge/release + ArrowProjectile + camera-ray aim + bow rig + reticle | gameplay-programmer / unity-ui-specialist | pending |
| P6 | Dodge: round(dir/90)+backstep + per-stance clips; keep stamina/i-frames; Left Ctrl | gameplay-programmer | pending |
| P7 | Target lock retune (keep RMB); camera-fwd spherecast 30/1.25; aim −1.0 Z | gameplay-programmer | pending |
| P8 | Input asset edits + `CanDamage` layer/tag | (orchestrator) | pending |
| P9 | Validators + code-review + CLAUDE.md log + handoff | qa-tester | pending |
| P10 | Manual Unity-Editor checklist (delivered at end) | (Jason) | pending |

---

## P10 — Manual Editor checklist (populated as phases complete)

> This section is the batched "content you must do yourself" list. It grows as
> each phase lands script-side work that needs Editor wiring. Exact clip names
> (including the `Shiled` / `Spining` typos and `_THS` / `DashRHT` quirks) will
> be listed per item.

- [ ] (P1) Run `LevelGen ▶ Player ▶ Set Over-the-Shoulder Framing` on the test
      scene's `CM Follow Camera` (or rebuild the rig). Verify framing in Play
      mode; nudge `RotationComposer ▸ Composition ▸ Screen Position` (default
      −0.18 / 0.05) — negative X flips shoulders.
- [ ] (P2) **Serialization gotcha:** `walkSpeed` and `jumpHeight` already have
      stored values on `Player_Hero.prefab` (3.5 and 1.2), so the new C# defaults
      (6.0 / 0.9) will NOT auto-apply. On `Player_Hero.prefab` set
      `walkSpeed = 6`, `jumpHeight = 0.9`. The brand-new fields (acceleration
      20.48, brakingDeceleration 20.48, airControl 0.05, turnRate 500) DO take
      their defaults automatically. Tune `walkSpeed` down toward 3.5 if the
      in-place walk clips foot-slide at 6 m/s.
- [ ] (P3) Author 8 `StanceDefinition` assets (`Create ▶ Hub & Hollow ▶ Stance
      Definition`) — one per stance; fill right/left weapon prefabs + eulers per
      the table above. Put them in an array on the `StanceController` component.
- [ ] (P3) Add `StanceController` (permanent) + `StanceDevCycler` (DEV-only,
      delete before ship) to `Player_Hero.prefab`. Wire `StanceController`'s
      `_rightHandSocket`/`_leftHandSocket` to the `weapon_r` / off-hand bones and
      `_rangedCrosshair` to the reticle (P5). `PlayerHero` manifest should gain a
      `[RequireComponent(typeof(StanceController))]` if you want it enforced.
- [ ] (P4) **ONE-CLICK for steps 12–14:** run
      `LevelGen ▶ Player ▶ Build Stance Animator (M22 12-14)`
      (`PlayerBaseControllerStanceBuilder`). It stamps the per-stance melee
      chains (tagged Attack1/2/3 + ComboNext/fallback transitions + WeaponType
      renumbering), the ranged single-shot states (6,7), the nested locomotion
      blend tree, and the per-stance dodge blend trees — loading clips by
      tolerant search (handles the `Shiled`/`_THS`/Battle quirks). It's
      idempotent (re-run any time) and prints any clips it couldn't find so you
      can drop those in by hand. This SUPERSEDES the manual steps below; it also
      re-tags its own states, so the manual "step 11" tagging is not required.
      Requires a `Locomotion` (or `Idle`) state + `RollFWD/BWD/LFT/RGT` states to
      already exist as the swap targets.
- [ ] (P4, manual alt) Or hand-wire per-stance Attack/Attack02/Attack03 states
      (melee stances 0–5), tagged Attack1/2/3, entered off `WeaponType` == stance
      index. Ranged stances (6,7) use the P5 ranged path, not combos.
- [ ] (P4) **Serialization gotcha:** `_fallbackDamage` on `Player_Hero.prefab`
      keeps its stored value (10). Set it to 20 on the prefab to match UE5, or
      leave it — equipped weapons already use their own `ItemData.Damage`.
- [ ] (P4) Loco blend trees per stance (Direction × Speed, 7 samples) — the
      Animator selects them off the `WeaponType` int (= stance index 0–7). Clip
      names have quirks: TwoHandSword uses `_THS`, SwordAndShield idle is
      `Idle_Battle_SwordAndShiled` (typo), `Attack04_Spining_THS` (typo),
      right-dash is `DashRHT`.
- [ ] (P5) Run `LevelGen ▶ Player ▶ Build Arrow Prefab` and
      `Build Ranged Reticle Prefab`.
- [ ] (P5) Add `RangedCombat` to `Player_Hero.prefab`; assign its `_arrowPrefab`
      (the built arrow) and `_muzzle` (the bow's NockArrow point / a hand
      transform). Set `_aimMask` to exclude the Player layer.
- [ ] (P5) Place `RangedReticle` under the HUD canvas, set it INACTIVE, and wire
      it to `StanceController._rangedCrosshair`.
- [ ] (P5) Bow rig: the skinned bow (`WeaponPrefab_Bows`) + nocked arrow
      (`WeaponPrefab_Arrows`) flex-idle is visual — mount on the bow stance and
      point `_muzzle` at its nock. (Optional refinement: fire the arrow from an
      animation event on the shot clip instead of on release for frame accuracy.)
- [ ] (P6) Per-stance dodge/roll states in the Animator (entered off
      `DodgeDirection` + the stance int). Scripted roll is kept — no root motion
      needed unless you later flip the dodge states to Apply Root Motion.
- [ ] (P7) **Serialization gotcha:** `Player_Hero.prefab` keeps its stored
      TargetLock values (`_lockRange` 20, `_sphereCastRadius` 3, `_breakRange`
      25). Set them to 30 / 1.25 / 35 on the prefab to match spec (or tune to
      taste — 3 is more forgiving than 1.25).
- [ ] (P8) On the `PlayerInput` component (Invoke Unity Events), wire the new
      **SwitchStance** action's event to `PlayerInputReader.OnSwitchStance`.
      (Dodge → Left Ctrl and the Q binding are already in the asset.)
- [ ] (validator) `LevelGen ▶ Player ▶ Validate UE5 Port` — expect 20 PASS / 0 FAIL.
- [ ] (full-rebuild note) After any `Build Player_Hero Prefab`, re-run the
      post-rebuild cascade in CLAUDE.md, then re-add `StanceController`,
      `StanceDevCycler` (dev), and `RangedCombat`.

### Recommended play-test order (P9→P10 done)
1. Movement feel (6 m/s, 500°/s turn, air control, jump 0.9 m).
2. Over-the-shoulder framing (`Set Over-the-Shoulder Framing`).
3. Q-cycle all 8 stances (dev) — meshes + loco set swap; ranged stances show
   the reticle.
4. Melee 3-hit combo per melee stance; damage lands (20 unarmed / weapon dmg).
5. Ranged: hold LMB (charge) → release (fire); arrow arcs, hits, 30 dmg;
   lock-on aims at target, free-aim converges on crosshair.
6. Dodge (Left Ctrl): directional + standing backstep; stamina + i-frames.
7. Target lock (RMB): acquire/strafe/break.

### Deferred (after M22 play-verifies)
- **Bow mesh separation:** the bow rig / `WeaponPrefab_Bows` ships with many
  meshes layered together; split them so only the intended bow shows in the
  BowAndArrow stance. To be done after the port works.
- `_aimMask` needs no Player layer — RangedCombat skips the shooter by
  hierarchy; leave the mask as Everything.
