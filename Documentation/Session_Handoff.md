# Session Handoff — 2026-05-19

## 1. Session Summary

What shipped this session (a cleanup / quality-of-life pass — no new
milestone number; tracked in CLAUDE.md "Session log — 2026-05-18"):

- **Camera vertical-axis reset on Play** — `CinemachineAutoBind.TryBind()`
  now resets `CinemachineOrbitalFollow.VerticalAxis.Value` to its
  `Center` immediately after a successful bind. Cinemachine was
  serializing the last-used axis value into the scene, so Play started
  with the camera aimed at the ground. The reset forces a known-good
  starting elevation regardless of saved scene state.

- **Mouse-delta suppression on cursor lock** — `MouseLook` gained a
  `public static bool SuppressLookThisFrame` flag, set `true` inside
  `Lock()` each time the cursor is locked. `PlayerInputReader.OnLook`
  checks the flag, and if set, discards that frame's look value and
  clears the flag. This swallows the OS cursor-warp delta (reported by
  the Input System as a mouse-delta event) that otherwise snapped the
  camera on the frame cursor lock engaged. `using LevelGen.Input;`
  added to `PlayerInputReader.cs`.

- **Walk speed raised, sprint removed from movement, Roll rebound** —
  `PlayerController.walkSpeed` C# default raised 2.0 → 3.5; the
  serialized value on `Player_Hero.prefab` is 5 (Inspector), which
  overrides the C# default — effective walk speed is **5 m/s**. Sprint
  logic was removed from the movement pipeline: the `wantSprint` local,
  `sprintMultiplier` application, and the `_stamina` cached ref (field
  + Awake `GetComponent<PlayerStamina>()`, both added in M9) are all
  gone. `IsSprintingNow` is kept (always `false`) so `PlayerStamina` /
  `PlayerAnimator` dependents still compile. In
  `InputSystem_Actions.inputactions`: the Dodge action's `<Keyboard>/v`
  binding was rebound to `<Keyboard>/leftShift`; the Sprint action's
  `<Keyboard>/leftShift` binding was removed entirely (Sprint action +
  its Gamepad/XR bindings remain so `OnSprint` / `IsSprinting` compile).

- **LockIndicator follows moving enemies** — `LockIndicator.Init()`
  previously baked `enemyRoot.position + Vector3.up * _yOffset` into a
  world-space `_basePosition`, parented with `worldPositionStays: true`,
  and wrote `transform.position` every frame — pinning the indicator to
  the enemy's spawn point. Fixed to store `Vector3.up * _yOffset` as a
  **local** offset, parent with `worldPositionStays: false`, and use
  `transform.localPosition` in both `Init()` and `Update()`. The
  indicator now rides the parent enemy transform. Billboard rotation
  untouched.

- **Enemy layer + TargetLock scoping** — an `Enemy` layer was created
  (`ProjectSettings/TagManager.asset`); `Enemy_Grunt.prefab` is assigned
  to it; `TargetLock._targetLayer` is scoped to the `Enemy` layer only,
  so the lock-on sphere-cast no longer needs to filter dead/irrelevant
  colliders out by type. (Editor work — TagManager + prefab edits done
  in the Inspector.)

- **Sneak system (hold-V)** — hold V to sneak; movement drops to
  2.0 m/s; an Animator `Sneak` bool gates a sneak locomotion clip.
  - `InputSystem_Actions.inputactions` — new `Sneak` Button action
    (plain Button, no Hold interaction — a literal Hold interaction
    adds an activation delay; plain Button gives true-while-held /
    false-on-release) bound to `<Keyboard>/v`.
  - `PlayerInputReader` — `public bool IsSneaking { get; private set; }`
    + `OnSneak(InputAction.CallbackContext)` UnityEvent endpoint.
  - `PlayerController` — `_sneakSpeed = 2.0f` SerializeField; the
    movement pipeline applies `currentSpeed = _sneakSpeed` while
    `_input.IsSneaking` (overrides `walkSpeed` for that frame).
  - `PlayerAnimator` — `Sneak` bool param hash (`ParamSneak` const,
    `_hashSneak`) + public `SetSneak(bool)`, `_ready`-gated like the
    other param setters.
  - `PlayerSneak.cs` (NEW) — bridge component; `Update()` forwards
    `_input.IsSneaking` to `_animator.SetSneak(...)`. Owns no movement
    logic and no clip selection.
  - `PlayerHero` manifest — `[RequireComponent(typeof(PlayerSneak))]`,
    `_sneak` SerializeField + `Sneak` property + Awake fallback.
  - `PlayerHeroBuilder` — `AddIfMissing<PlayerSneak>`,
    `WireProp(so, "_sneak", ...)`, `("Sneak", "OnSneak")` in
    `s_Bindings`.
  - `SneakLocomotion` blend tree added to `PlayerBaseController` with
    `NoWeapon` clips; `OnSneak` UnityEvent wired on the prefab.
    (Animator graph + prefab UnityEvent are editor work — done in the
    Animator window / Inspector, not by code.)

## 2. Validator State Table

No validators were re-run this session — it was a code + editor pass.
The Player_Hero validator should be re-run after the user runs
`Build Player_Hero Prefab` (the Sneak component add + UnityEvent wiring
land then).

| Validator | Last-known | Re-run this session? |
|-----------|-----------|----------------------|
| LevelGen ▶ Player ▶ Validate Player_Hero | 67 PASS | No — re-run after Build Player_Hero Prefab |
| LevelGen ▶ Weapons ▶ Validate Weapon Prefabs | 114 PASS | No |
| LevelGen ▶ Combat ▶ Validate Enemy | 49 PASS | No |
| LevelGen ▶ Interaction ▶ Validate Interaction | 42 PASS | No |

PlayerHeroValidator has no Sneak-specific check yet — if a future pass
wants coverage, add a check for `PlayerSneak` presence on the prefab
root + the `_sneak` SerializeField ref being non-null (mirrors the
M20 `_equipmentVisuals` checks 64–67).

## 3. Deferred / Known Issues

- **Camera focus-click jitter** — the camera start angle is now
  correct, but clicking into the Game view (editor focus) still causes
  a brief one-frame camera jitter. This is an editor focus-change
  artifact, **not game code** — it will not occur in a player build.
  Left as-is.
- **EnemyHealthBar wiring** — `EnemyHealthBar` /
  `EnemyHealthBarProximityDriver` components are still not wired onto
  `Enemy_Grunt.prefab` (the M14 scripts shipped; the prefab Canvas +
  Image hierarchy + component placement remains pending).
- **EnemyBaseBuilder does not set the Enemy layer** — the new `Enemy`
  layer is assigned on `Enemy_Grunt.prefab` by hand. `EnemyBaseBuilder`
  still builds enemy prefabs on the default layer; it should be
  extended to stamp the `Enemy` layer on the built root so future
  archetypes are TargetLock-visible without manual Inspector work.
- **Weapon collider sizes** — all 57 weapon-prefab BoxCollider bounds
  are first-pass category estimates; per-weapon tuning in Prefab Mode
  is still needed (shields, spears, staves most affected).
- **Item icons blank** — `ItemData.Icon` (Sprite field) is unassigned
  on every `ItemData` asset; inventory UI shows text only.
- **Armor slot** — `EquipSlot.Armor` exists but is not surfaced in the
  `InventoryHUD` strip or the `InventoryPanel`.
- **Combo system not started** — see §4; this is the recommended next
  work item.

## 4. Open Milestone Candidates

| Milestone | Description | Recommended? |
|-----------|-------------|--------------|
| Combo system | Per-weapon-type attack combos (OHS, THS, Spear, Shield, Ranged, Unarmed) | **Yes — next** |
| M19 | Enemy AI — patrol routes, alert / search states, group awareness | |
| M22 | Loot drops — `EnemyData.lootTable` → spawn `WorldItem` on death | |

**Recommended next: the combo system.** The player has explicitly
asked for it, and the foundation is already in place — M2-B shipped a
3-hit combo on the single shared Attack clip; M17 made the equipped
`ItemData` readable; M20/M20c gave every weapon a slot-typed prefab.
The combo work is to branch attack chains by weapon type.

## 5. Architectural Reminders (relevant to combo work)

- **Single-writer-per-Animator-parameter invariant.** Each Animator
  parameter has exactly one script that writes it. For the player,
  `PlayerAnimator` is the sole script that calls `Animator.Set*`.
  Combo work must add any new triggers/ints (e.g. a `WeaponType` int,
  a `ComboStep` int) as new `PlayerAnimator` setter methods — never
  call `Animator.SetTrigger` from `PlayerCombat` directly.
- **PlayerCombat owns attack logic; PlayerAnimator owns Animator
  writes.** The dependency is one-directional:
  `PlayerInputReader → PlayerCombat → PlayerAnimator → Animator`.
  Combo state (current step, buffer window, which chain) lives in
  `PlayerCombat`. It already has the M2-B buffered-combo machine
  (`_attackBuffered`, `comboWindowOpen/Close`, `bufferConsumeAt`,
  `SetComboNext`) — extend that, don't replace it.
- **WeaponType must be readable at swing time.** The equipped weapon's
  type drives which combo set fires. `PlayerInventory.GetEquipped(
  EquipSlot.Melee)` returns the `ItemData`; `ItemData.Slot` is the
  coarse slot. A finer `WeaponType` (OHS / THS / Spear / Shield /
  Ranged / Unarmed) likely needs a new field on `ItemData` (or a
  derived enum) so `PlayerCombat` can pull it via the same pull
  pattern M17 used for damage — read it inside the attack handler,
  no event subscription. Unarmed = `GetEquipped(Melee) == null`.
- **Animator-graph caveat (Unity 6.4).** Never combine
  `Has Exit Time = true` with a Trigger condition on the same
  transition — it auto-fires at exit time regardless of the trigger
  (M2-B Step 6/7 lesson). Gate combo advancement in script
  (`PlayerCombat` already does this — `SetComboNext` is only called
  at `normalizedTime >= bufferConsumeAt`).
- **Input is UnityEvent dispatch.** No generated `InputSystem_Actions.cs`
  class. Every action is a `public void OnX(InputAction.CallbackContext)`
  endpoint on `PlayerInputReader`, wired via `PlayerHeroBuilder.s_Bindings`
  + the PlayerInput component on the prefab. Read `PlayerInputReader.cs`
  before writing any input code.
- **C# event vs UnityEvent endpoint naming.** Endpoint method is
  `OnX`; the paired C# event carries the `Performed` suffix
  (`OnXPerformed`) to avoid CS0102. See M18's `OnToggleInventory` /
  `OnToggleInventoryPerformed`.

## 6. Quick-Start for Next Session

Paste this at the start of the next chat:

```
# Hub & Hollow — Session Open

Read in order:
1. CLAUDE.md
2. Documentation/Session_Handoff.md

Last session was a cleanup pass: camera vertical-axis reset, mouse-delta
suppression on cursor lock, walk speed → 5 m/s, Roll rebound to Left
Shift (sprint removed from movement), LockIndicator local-space fix,
Enemy layer + TargetLock scoping, and a hold-V Sneak system.

Pending editor step: run LevelGen ▶ Player ▶ Build Player_Hero Prefab
to add PlayerSneak + wire its OnSneak UnityEvent, then re-run
Validate Player_Hero.

Recommended next work: the combo system — per-weapon-type attack
chains (OHS / THS / Spear / Shield / Ranged / Unarmed). Foundation is
in place: M2-B buffered 3-hit combo, M17 equipped-ItemData read,
M20c slot-typed weapon prefabs. See Session_Handoff §5 for the
architectural constraints.
```
