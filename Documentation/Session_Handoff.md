# Session Handoff — 2026-05-04 (M5/M6/M7 closed 2026-05-03; M8/M9/M10/M11 closed 2026-05-04)

> **Purpose of this doc.** This is the canonical "where we are right
> now" layer that sits on top of CLAUDE.md. CLAUDE.md is the architecture
> canon and milestone log; this file is what the project looked like at
> the end of the most recent session. Read this first at the start of
> every new chat — it supersedes general assumptions about current state.
> When the user says "good night", a new version of this file replaces
> the old one in place.
>
> **File path:** `Documentation/Session_Handoff.md` (project root)

---

## Where the project stands

Unity 6.4 URP mobile procedural level generator
(`Unity_Procedural_Level_Creator`), C# under `LevelGen` namespace,
IL2CPP for Android + iOS. V2 architecture is master.

Combat scaffolding for one enemy type (Dummy) is structurally
complete and the player can both die and interact. End-to-end
loop: Player attacks → Dummy HP applied → flinch → death anim →
cleanup → despawn (M4 chain). Player HP→0 → Die01 + "You Died"
overlay → Restart reloads scene (M5). Walk behind Dummy →
"Press [E] Assassinate" prompt → E kills via 99999-damage
override (M6).

### Just shipped today (2026-05-03)

Two milestones closed cleanly. All validators green.

**1. M5 — Player Death**

Mirrors M4-B for the Player. HP→0 plays `Die01_SwordAndShield`,
disables PlayerController + PlayerCombat (input still flows but
no subscribers act on it), raises `PlayerDeath.OnPlayerDied` for
UI. PlayerDeathOverlay shows a "You Died" canvas with a Restart
button that reloads `SceneManager.GetActiveScene().buildIndex`.

- `PlayerBaseControllerExtender.cs` (NEW, editor) — idempotent
  additive editor script that adds the Death state to the
  existing PlayerBaseController WITHOUT recreating it (preserves
  all M2-B / M2-C state and transitions). Adds `Death` Trigger
  param + terminal `Death` state (no outgoing transitions) +
  AnyState→Death (canTransitionToSelf=false, dur 0.05) only if
  missing. Skips silently on re-run.
- `PlayerAnimator.cs` — added `ParamDeath` const, `_hashDeath`
  field, hash assignment in Awake, public `SetDeathTrigger()`.
  Same pattern as the existing trigger-set methods.
- `PlayerDeath.cs` (NEW, `LevelGen.Player`) — sole owner of the
  death sequence. `[RequireComponent(CharacterStatsRuntime)]`,
  `[DisallowMultipleComponent]`. Three SerializeField refs
  (`_animator`, `_controller`, `_combat`). Subscribes to
  `_stats.OnDied` in OnEnable; `HandleDied` runs the sequence:
  disable PlayerController → disable PlayerCombat →
  `_animator.SetDeathTrigger()` → raise `OnPlayerDied(this)`.
  `_hasFired` guard against double-invoke.
- `PlayerCombat.cs` — gained cached `_stats` reference (null-
  tolerant, no `[RequireComponent]` — mirrors EnemyHitReaction)
  + `IsDead` guard at the top of `TakeHit()`. Belt-and-suspenders
  against same-frame OnHit/OnDied subscriber ordering.
- `PlayerDeathOverlay.cs` (NEW, `LevelGen.UI`) — passive observer
  on its prefab root with the Canvas as a child (canvas hide
  doesn't disable the overlay's OnPlayerDied subscription).
  Tag-based player lookup with retry coroutine (copies
  PlayerHUD's `TryBindToPlayer` + `PollForPlayer`). `HandlePlayerDied`
  shows the canvas, unlocks the cursor. **Three-layer Restart
  input handling** (the part of M5 that took the most iteration):
    1. **Keyboard fallback** in Update — R / Enter / Numpad-Enter
       call OnRestartClicked directly. Works regardless of
       EventSystem state.
    2. **Manual mouse-over-RectTransform check** in Update — left
       mouse + cursor inside the RestartButton's RectTransform
       (resolved via `RectTransformUtility.RectangleContainsScreenPoint`
       with null camera for ScreenSpaceOverlay) calls
       OnRestartClicked. Bypasses EventSystem +
       InputSystemUIInputModule entirely. **This is the
       bulletproof layer.**
    3. **Standard `Button.onClick`** — works only when the scene
       has an EventSystem with a properly-bound
       InputSystemUIInputModule.
  PlayerDeathOverlayBuilder also auto-adds an EventSystem with
  InputSystemUIInputModule on Place; PlayerDeathOverlay's Awake
  has a runtime ensure that creates one if missing OR replaces
  a legacy StandaloneInputModule with InputSystemUIInputModule.
  Both belt-and-suspenders; layer 2 above is what actually makes
  the click work.
- `PlayerDeathOverlay.prefab` (NEW) — built via
  `LevelGen ▶ UI ▶ Build PlayerDeathOverlay Prefab`. Canvas
  sortingOrder=100 (above HUD's 10).
- `PlayerDeath` added to Player_MaleHero.prefab via two paths:
  folded into `BuildPlayerMaleHeroPrefab` (rebuild path) +
  `LevelGen ▶ Player ▶ Add PlayerDeath to Player_MaleHero Prefab`
  (one-shot LoadPrefabContents path). Mirrors the M4-A "fold
  into the main builder" pattern.
- `PlayerDeathValidator.cs` — 16 checks, all PASS.

**2. M6 — Player Interact System + AssassinateInteractable**

Generic prefab-friendly Interact system. Press E to trigger
contextual actions on nearby Interactables. Ships one concrete
subclass (AssassinateInteractable) wired to the Dummy. Architecture
ready for Open / Pickup / Read subclasses with no edits to
the player or system core.

- `Interactable.cs` (NEW, `LevelGen.Interaction`) — abstract
  base. `[DisallowMultipleComponent]`. SerializeFields:
  `_priority` (InteractPriority), `_promptLabel`, `_promptAnchor`,
  `_playerTag`. Abstract: `IsEligible(GameObject)`,
  `Execute(GameObject)`. Owns trigger handling, register/deregister
  against PlayerInteractor, prompt-UI build/visibility.
  `EnsurePromptUI` builds a child World Space Canvas (scale 0.01)
  with TMP_Text rendering "Press [E] {label}". Idempotent.
- `InteractPriority` enum — `Pickup=10`, `Open=50`,
  `Assassinate=100`. PlayerInteractor picks highest-priority
  registered interactable; same-priority ties resolve to
  first-registered (documented but not relied on).
- `PlayerInteractor.cs` (NEW, `LevelGen.Player`) —
  `[RequireComponent(PlayerInputReader)]`,
  `[DisallowMultipleComponent]`, static `Instance` property
  (singleton-ish; project has one Player). HashSet<Interactable>
  for the registered set; `_active` is the highest-priority
  registered interactable. Subscribes to
  `PlayerInputReader.InteractPressed` (NEW event) and
  `PlayerDeath.OnPlayerDied` (clears registrations + hides
  prompts on death).
- `PlayerInputReader.cs` — added `event System.Action InteractPressed`;
  OnInteract now raises it on `ctx.performed`; M1 stub log
  removed. Mirrors the M2-B Step 3 pattern that wired AttackPressed
  / JumpPressed.
- `PlayerCombat.cs` — four additive surface changes (no
  refactor):
  - `_nextHitDamageOverride` int field (single-shot).
  - `IsBusy => IsActionLocked` public alias property.
  - `SetNextHitDamageOverride(int)` setter.
  - `RequestAttack()` public delegate to the existing private
    `OnAttackPressed` handler.
  - Inside `NotifyHitboxTriggered`, the override is consumed
    AFTER stats / hit-list checks (so warning + already-hit
    branches don't burn it) and is single-shot (cleared on
    first successful application).
- `AssassinateInteractable.cs` (NEW, `LevelGen.Interaction`) —
  `[RequireComponent(SphereCollider)]`. Subclasses Interactable.
  Eligibility = target alive AND
  `Vector3.Dot(target.forward, toPlayer) < -_backArcDot`
  (default 0.5 ≈ 60° back arc). Execute: snap-rotate the player
  to face the target, set `_assassinateDamage = 99999` override,
  call `combat.RequestAttack()`. Drops silently if `combat.IsBusy`.
- `DummyPrefabBuilder.cs` — extended to add `_AssassinateZone`
  child (SphereCollider trigger r=1.5, AssassinateInteractable)
  + `_PromptAnchor_Head` grandchild at local (0, 1.9, 0). Three
  SerializedObject refs wired explicitly. EnsurePromptUI called
  at build time so the prompt child is visible in the prefab
  inspector.
- `PlayerInteractor` added to Player_MaleHero.prefab via two
  paths (mirrors M5): folded into BuildPlayerMaleHeroPrefab +
  `LevelGen ▶ Player ▶ Add PlayerInteractor to Player_MaleHero
  Prefab` standalone adder.
- `InteractSystemValidator.cs` — 16 checks. Validator check 7
  (OnInteract stub-log gone) was patched mid-shipping after a
  fixed-width slice scan from `public void OnInteract` spilled
  past OnInteract's closing brace into OnCrouch (which still has
  its own M1-stub log). Now uses direct string match against the
  literal stub line `Debug.Log("[PlayerInputReader] Interact"`.

### Verified shipping at end of session

In the active test scene:
- Combat loop from prior sessions still works: walking up to
  Dummy + LMB combo damages 30 HP (3-hit at 10 dmg each); Dummy
  stagger plays each hit.
- Dummy now dies organically: it's at 50 HP (down from 999 —
  M4-B test convenience). Combo + 2nd hit kills, plays Die01,
  Targetable + Collider + EnemyHitReaction disabled, despawns
  after 5s.
- Player can die: right-click `CharacterStatsRuntime` →
  `Debug: Kill` → Die01 plays + parks, WASD / mouse / LMB silent,
  "You Died" overlay appears, cursor unlocks, R/Enter or
  click-Restart reloads scene fresh with HP 100/100.
- Walk behind a fresh Dummy → "Press [E] Assassinate" floats
  above head; E snap-rotates the Player and instantly kills the
  Dummy via the existing combo + hitbox path with the override
  applied.
- Walk in front of Dummy → no prompt (back-arc dot fails).
- Cursor locks/hides on Play start; Escape unlocks; Alt-Tab
  unlocks; PlayerDeathOverlay's HandlePlayerDied unlocks for
  the death screen; scene reload re-locks via MouseLook.

### Shipped post-2026-05-03 (M8 → M11)

Six additional milestones landed since the original handoff date.
Validators passed for M8 / M9 / M10. M11 validators pass; runtime
behavior still under diagnosis (see "What's broken / pending
observation" below).

**M8 — Damage numbers / floating combat text.** `Targetable.OnHit`
extended to `Action<Vector3, float>`; new static `AnyTargetableHit`
fan-out. `DamageNumber.cs` + `DamageNumberSpawner.cs` (singleton
manager subscribes once, spawns world-space TMP_Text per hit).
14/14 validator. Builder + spawner prefab idempotent.

**M9 — Stamina gameplay.** Sprint drains stamina; 0 stamina drops
to walk; regen lifts CanSprint back. CharacterStats SO gained
`_staminaDrainPerSecond` (25) + `_staminaRegenPerSecond` (33).
CharacterStatsRuntime `currentStamina` int → float internally;
public `CurrentStamina` returns `Mathf.CeilToInt`. `PlayerStamina`
on Player root owns drain/regen Update; PullCanSprint pattern.
PlayerController gained `IsSprintingNow` + cached `_stamina` ref;
animator passthrough swapped to IsSprintingNow. 12/12 validator.

**M10 — Basic Dummy AI.** EnemyBaseController gained MoveSpeed
(Float) + Attack (Trigger), Locomotion (1D blend tree
Idle@0/MoveFWD@1) + Attack (Attack01_SwordAndShiled —
typo'd sub-asset name preserved per M3 swap notes). EnemyAI
(NavMeshAgent + FSM Idle/Chase/Attack/Cooldown). NavMesh baked
via modern AI Navigation 2.x `NavMeshSurface` — `_NavMeshSurface`
GameObject in scene + NavMeshModifier(ignoreFromBuild=true) on
NavMeshAgent / CharacterController objects. EnemyAnimationEventAbsorber
(no-op) on MaleCharacterPBR child. 16/16 validator.

**M11 — Player takes damage.** EnemyAnimationEventAbsorber
deleted; replaced by EnemyAnimationEventForwarder (routes
events to EnemyCombat). EnemyCombat (mirror of PlayerCombat
hitbox path; IsDead + friendly-fire `CompareTag("Player")`
guards). EnemyHitboxRelay. EnemyWeaponHitbox child under
weapon_r (BoxCollider trigger+disabled, kinematic Rigidbody
required for OnTriggerEnter, EnemyHitboxRelay._combat wired).
PlayerHitReaction subscribes to its own Targetable.OnHit, calls
PlayerCombat.TakeHit (no stagger window per Q4). Player_MaleHero
gained Targetable + PlayerHitReaction; CharacterController.radius
bumped 0.3 → 0.4 (Q5) for symmetric combat reach via
`PlayerCapsuleTuner`. 17/17 validator (compile + wiring layer).

**M11 post-tune (post-validator pass).** EnemyAI defaults bumped:
`_attackRange` 1.8 → 1.3, `_stoppingDistance` 1.5 → 1.0.
DummyPrefabBuilder `agent.stoppingDistance` 1.5 → 1.0 (matches).
Original 1.8/1.5 placed Dummy beyond the EnemyWeaponHitbox arc
reach (~1.2m forward of Dummy pivot at peak swing); swings
landed in air. New defaults give 0.5m capsule-edge overlap at
peak swing for consistent hits. **Requires `LevelGen ▶ Combat ▶
Build Dummy Prefab` to rebuild with the new SerializeField
defaults** (existing prefab still carries the old serialized
1.8/1.5 until rebuilt).

### What's broken / pending observation

**M11 runtime damage routing — under diagnosis as of session
end.** User reports: post-tune the Dummy now closes to contact
range and the sword visibly passes through the Player body, but
no damage applies and no GetHit01 flinch animation plays.
EnemyCombatValidator passes 17/17 at the wiring layer, so the
build-time graph is correct — the failure is somewhere in the
runtime chain. Likely candidates (in diagnostic order):

1. User may not have run all M11 player-side adders. Required
   menus, in order:
   - `LevelGen ▶ Player ▶ Add Targetable to Player_MaleHero Prefab`
   - `LevelGen ▶ Player ▶ Add PlayerHitReaction to Player_MaleHero Prefab`
   - `LevelGen ▶ Player ▶ Tune CharacterController for Hit Reception`
   - `LevelGen ▶ Combat ▶ Build Dummy Prefab` (rebuilds with M11
     EnemyCombat + Forwarder + EnemyWeaponHitbox + post-tune ranges)
2. AnimationEvent dispatch on Dummy's Attack01 clip — if the
   `OnHitboxOpen` / `OnHitboxClose` events were lost from the
   FBX `.meta` somehow, the Forwarder never fires and the
   BoxCollider never enables.
3. EnemyWeaponHitbox child or its kinematic Rigidbody missing
   from the Dummy prefab post-rebuild — would cause OnTriggerEnter
   to silently never fire (M3 lesson: kinematic Rigidbody is
   required for trigger-on-moving-collider events).
4. Friendly-fire guard `stats.CompareTag("Player")` failing
   because the Player root tag is something other than "Player".

Next-session-CC's first move: ask user to run
`LevelGen ▶ Combat ▶ Validate Enemy Combat` and paste output;
confirm Inspector state of Player_MaleHero (Targetable +
PlayerHitReaction present?) and Dummy (EnemyCombat on root?
Forwarder on MaleCharacterPBR child? EnemyWeaponHitbox child
under weapon_r with all three components?). If all wiring is
correct, add temporary debug logs in
`EnemyAnimationEventForwarder.OnHitboxOpen` and
`EnemyCombat.NotifyHitboxTriggered` to pinpoint which link
fails at runtime.

---

## Open milestone candidates for next session

User picks at session start. No forced next step — the project
is at a clean checkpoint with two complete enemy-side action
loops (combat + assassinate) and a complete player-death loop.

**Combat / interact extensions (small to medium)**

- **OpenInteractable** — second concrete Interactable subclass
  (door open). Tests the abstract base's generality. Same
  pattern as AssassinateInteractable but with different
  eligibility (e.g., always-eligible if door is closed) and
  Execute (rotate the door 90°, swap collider state).
- **PickupInteractable** — third concrete subclass (item
  pickup). Removes the item from the world; a future Inventory
  system would receive the picked-up item.
- **WeaponStats SO** — replace `attackDamage = 10` hardcoded
  SerializeField on PlayerCombat with a ScriptableObject ref.
  Path forward for weapon variety (World Bundle has 8 weapon
  sets vs the wired SwordAndShield).
- **Damage numbers / floating combat text** — cosmetic.
  Subscribe to `Targetable.OnHit` → spawn TMP_Text at hit
  point, lerp upward, fade out.
- **Stamina gameplay** — wire sprint cost / attack cost / regen
  on top of the existing data layer.

**Combat / interact extensions (larger scope)**

- **Player takes damage** — Dummy or new enemy gets a small AI:
  face player, attack on cooldown, route damage back via the
  same hitbox pattern. Closes the combat loop.
- **More enemy types** — pick a second character prefab from
  the World Bundle's 24 MC* prefabs; apply the EnemyBaseController
  + EnemyHitReaction + EnemyDeath + AssassinateInteractable
  pattern. Tests whether the foundations generalize.
- **In-place respawn** (currently TODO in
  PlayerDeathOverlay.OnRestartClicked) — needs spawn-point
  architecture and respawn semantics.

**Procedural-generation / level work**

- **M2-D level integration** — LevelGenerator-driven runtime
  spawn (composite Player_RuntimeRig refactor). The remaining
  piece of M2 player work; needs RoomBuilder's PlayerSpawnPoint
  marker (already shipped).
- **V2 generator door-geometry placement** — door prefab
  placement at ExitPoint connections. Pairs naturally with
  OpenInteractable.
- **Whitebox `PieceCatalogue` end-to-end test** — wire the
  whitebox pack into PieceCatalogue, validate in Room Workshop,
  run through the V2 generator.

---

## What CC will likely need from you for the next prompt

Depends on the milestone picked. For an Interactable subclass:

1. Behavior table for the new interaction (eligibility,
   execute side effects).
2. Whether the prompt anchor needs a child Transform or can
   default to the GO itself.
3. Whether the new subclass needs its own [RequireComponent].

For a damage-routing change (WeaponStats SO, damage numbers):

1. Whether per-weapon damage values should ship with a
   SerializeField asset reference on PlayerCombat (single
   weapon) or via a slot system (future weapon swap).
2. Default damage value when WeaponStats is null (today's 10).

---

## Working preferences (unchanged)

- No coding in chat — all implementation goes back as Claude
  Code prompts (markdown files saved to
  `/mnt/user-data/outputs/`).
- All prompts end with telling Claude Code to compact.
- CLAUDE.md is canonical, updated each session (CC handles the
  append at the end of each prompt's deliverables).
- Behavior tables before code on complex logic.
- Empirical/direct: Inspector data over theoretical derivation;
  immediate misread correction.
- Project Knowledge sync: scripts + docs only. Asset packs and
  binary assets excluded; paste specific files into the chat
  if needed.
- One question at a time when narrowing scope; multi-choice
  over prose.
- "M5 / M6 numbering note": the user's M6 prompt was titled
  "M5 — Player Interact System" but Player Death had already
  shipped as M5 earlier the same day. CLAUDE.md uses M6 going
  forward to avoid duplicate `## M5` headings.

---

## Things to leave alone

- M1 + M2-A + M2-B + M2-C + M3 + M4-A + M4-B + M5 + M6 + M7 +
  M8 + M9 + M10 + M11 — verified working, do not refactor.
- The 7 V1 cleanup commits and their history — done, merged,
  stable.
- `Assets/Scripts/Experimental/` — dormant, don't reference
  from V2.
- `LVL_Configurator` — "complete, do not touch" per CLAUDE.md
  (const-string updates for folder reorg are the only
  acceptable touch).
- V2 generator (Phases A–D) — at a stable checkpoint.
- Combat foundation, HUD, damage routing, MouseLook,
  EnemyHitReaction, EnemyDeath, PlayerDeath, PlayerDeathOverlay,
  Interactable, PlayerInteractor, AssassinateInteractable,
  OpenInteractable, TestDoor — all tested and locked. Next
  milestone *adds to* them, doesn't modify them.
- Interactable abstract base — extend by subclassing only; do
  not modify the base. The M7 additive `RefreshPromptLabel()`
  helper is the only sanctioned post-Awake label-mutation path
  for subclasses; do not bypass it (e.g. by adding a public
  setter on `_promptLabel` or by writing to TMP_Text directly
  from a subclass).
- The triple-redundant Restart input handling on
  PlayerDeathOverlay — keep all three layers; layer 2 (manual
  mouse-over-RectTransform) is load-bearing.
- M7 — OpenInteractable + TestDoor — done, validators green
  (12/12), do not refactor. TestDoor lives in
  `Assets/Prefabs/TestRig/` as a diagnostic stand-in until M16
  ships real FDP `COMP_Door_*` prefabs.
- M8 — Damage numbers — done, validators green (14/14), do not
  refactor. `Targetable.OnHit` signature is now
  `Action<Vector3, float>` — adding more payload fields requires
  migrating all subscribers; weigh cost before changing again.
  When damage types arrive (fire/ice/etc.) introduce a small
  `DamageInfo` struct in a separate intentional milestone, do
  not bloat the event payload further.
- `Targetable.AnyTargetableHit` static event — must remain
  paired with `OnEnable += / OnDisable -=` in every subscriber.
  Static event lifetime survives domain reloads; missing
  unsubscribes leak across Play sessions.
- M9 — Stamina gameplay — done, validators green (12/12), do
  not refactor. CharacterStats SO carries per-character drain +
  regen rates — adding more rate fields requires updating
  CharacterStats_*.asset (via CharacterStatsAssetUpdater),
  OnValidate clamps, and PlayerStamina Update logic; weigh
  cost before adding more.
- `CharacterStatsRuntime.currentStamina` is a float internally
  (M9). Public `CurrentStamina` (int) returns
  `Mathf.CeilToInt(currentStamina)` so PlayerHUD's int display
  reads sensibly while sub-1 stamina remains. Don't change the
  field type back to int without revisiting the per-frame drain
  granularity (25/s × 0.016 = 0.4/frame would round to 0 each
  frame, stamina would never move).
- `PlayerController.IsSprintingNow` is the post-stamina-gate
  sprint state. The Animator (step 9) reads this, NOT
  `_input.IsSprinting` — passing raw input would play Sprint
  clip while physically walking when stamina is empty.
- M10 — Dummy AI FSM — done, validators green (16/16), do
  not refactor.
- `EnemyAnimationEventAbsorber` is a M10-only stub. M11 will
  replace it with `EnemyCombat`. Don't add hitbox / damage
  logic to the absorber — keep the bodies empty.
- `NavMeshAgent` on Dummy is authoritative for position. Don't
  also add a CharacterController or move via transform.position;
  agent owns it.
- `Attack01_SwordAndShiled` clip retains the publisher's typo
  (only Idle was renamed during M3 pack swap). Use the typo'd
  name when looking up the sub-asset, not "Attack01_SwordAndShield".
- The NavMesh bake (`LevelGen ▶ Combat ▶ Bake Test Scene NavMesh`)
  is edit-mode only and adds a `_NavMeshSurface` GameObject +
  NavMeshModifier(ignoreFromBuild=true) components to scene
  objects with NavMeshAgent or CharacterController. Don't delete
  these out of the test scene without re-baking.
- M11 — Player takes damage — done, validators green (16/16),
  do not refactor.
- `EnemyCombat` is per-enemy; new enemies copy the pattern.
  Do not generalize until the third duplicate exists (rule of
  three).
- The friendly-fire guard `stats.CompareTag("Player")` in
  `EnemyCombat.NotifyHitboxTriggered` is a hard-coded "Player
  only" filter. Removing it is a future M-Factions milestone.
  Don't soften it casually — without it, two Dummies in melee
  range damage each other on swings.
- The IsDead guard at the top of
  `EnemyCombat.NotifyHitboxTriggered` preserves M5's terminal-
  Death semantic (no corpse-flinch). Don't remove.
- `EnemyAnimationEventForwarder` REPLACED the M10 absorber.
  The absorber file is deleted. Don't recreate it — if you
  need a no-op stub for a new enemy clip, copy the Forwarder
  pattern with `_combat = null` tolerated.
- `Player_MaleHero.prefab` now carries Targetable +
  PlayerHitReaction. Targetable is bidirectional (Player and
  Enemy both publish OnHit / AnyTargetableHit) — code that
  filters "is this a player or enemy hit" must check tags or
  components, NOT the presence of Targetable.
- `Player_MaleHero.prefab` CharacterController.radius = 0.4
  (bumped from 0.3 in M11 Q5 for hit-reception reach matching
  the Dummy's CapsuleCollider). DON'T revert to 0.3 without
  first tuning the alternative — separate hit-reception
  CapsuleCollider while keeping CC at 0.3 for narrow-gap
  movement. EnemyCombatValidator check 17 enforces the lower
  bound (>= 0.35).

---

## Lessons from this session worth remembering

1. **InputSystemUIInputModule programmatic AddComponent
   doesn't bind actions.** When `InputSystemUIInputModule` is
   added via `AddComponent`, its `actionsAsset` field stays
   null and per-action references (point, leftClick, submit)
   point at nothing. The module looks fine in the Inspector
   and produces no error logs, but UGUI clicks silently don't
   dispatch. The editor's `GameObject ▶ UI ▶ Event System`
   menu auto-assigns the package's `DefaultInputActions.inputactions`;
   programmatic adds do not. Workaround: layer a manual mouse-
   over-RectTransform check in Update via
   `RectTransformUtility.RectangleContainsScreenPoint` with
   null camera for ScreenSpaceOverlay canvases. Bulletproof
   across InputSystem versions. See
   `PlayerDeathOverlay.Update` for the canonical pattern.

2. **Fixed-width slice scans on source code spill across method
   boundaries.** Validator check 7 (M6) used a 400-char slice
   from `public void OnInteract` to verify the M1 stub log was
   gone. Sibling M1-stub methods (OnCrouch / OnPrevious /
   OnNext) sit close enough that the slice ran past OnInteract's
   closing brace and matched OnCrouch's still-present
   `Debug.Log`, producing a false positive. For "is this
   specific stub gone" checks, prefer direct string match
   against the literal stub line (`Debug.Log("[PlayerInputReader]
   Interact"`) — exact, immune to neighbor noise. Bracket-
   matching the method body is more rigorous but adds parser
   complexity for a one-line check.

3. **Reset() doesn't fire on programmatic AddComponent**
   (carried forward from M5). It fires only when the user adds
   the component via the editor's Add Component menu OR clicks
   Reset in the Inspector context menu. For SerializeField refs
   that prefab builders need wired, use SerializedObject
   helpers explicitly (see PlayerPrefabBuilder.AssignPlayerDeathRefs
   + DummyPrefabBuilder.AssignAssassinateRefs).

4. **Carry-forward from previous sessions:** AnimationEvents
   persist via `.meta` (use `ModelImporter.clipAnimations`
   API); Unity dispatches AnimationEvents to the Animator's
   GameObject only (use a forwarder); triggers need a
   non-static collider partner (kinematic Rigidbody on child
   trigger if root has only CharacterController);
   programmatic UI Images need explicit sprite for `Filled` /
   `Sliced` clipping; World Bundle FBX filename ≠ AnimationClip
   sub-asset name (Idle "Shiled" → "Shield") — name loader
   constants for the sub-asset, not the FBX filename.

---

## File inventory at end of session

**M5 — Player Death**
```
Assets/Scripts/Player/PlayerDeath.cs (NEW)
Assets/Scripts/Player/Editor/PlayerBaseControllerExtender.cs (NEW)
Assets/Scripts/Player/Editor/PlayerDeathPrefabAdder.cs (NEW)
Assets/Scripts/Player/Editor/PlayerDeathValidator.cs (NEW)
Assets/Scripts/Player/PlayerAnimator.cs (Death hash + param + SetDeathTrigger)
Assets/Scripts/Player/PlayerCombat.cs (cached _stats + IsDead guard at top of TakeHit)
Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs (PlayerDeath fold-in + AssignPlayerDeathRefs helper)
Assets/Scripts/UI/PlayerDeathOverlay.cs (NEW; triple-redundant Restart input)
Assets/Scripts/UI/Editor/PlayerDeathOverlayBuilder.cs (NEW; auto-adds EventSystem with InputSystemUIInputModule on Place)
Assets/Animators/Player/PlayerBaseController.controller (extended in place via PlayerBaseControllerExtender)
Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab (gains PlayerDeath)
Assets/Prefabs/UI/PlayerDeathOverlay.prefab (NEW)
```

**M6 — Player Interact System + AssassinateInteractable**
```
Assets/Scripts/Interaction/Interactable.cs (NEW)
Assets/Scripts/Interaction/AssassinateInteractable.cs (NEW)
Assets/Scripts/Interaction/Editor/InteractSystemValidator.cs (NEW)
Assets/Scripts/Player/PlayerInteractor.cs (NEW)
Assets/Scripts/Player/Editor/PlayerInteractorPrefabAdder.cs (NEW)
Assets/Scripts/Player/PlayerInputReader.cs (InteractPressed event + OnInteract stub log removed)
Assets/Scripts/Player/PlayerCombat.cs (override field + setter + RequestAttack + IsBusy + consume in NotifyHitboxTriggered)
Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs (PlayerInteractor fold-in)
Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs (_AssassinateZone child + helpers)
Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab (gains _AssassinateZone child)
Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab (gains PlayerInteractor)
```

CLAUDE.md updated with M5 + M6 entries (M6 numbered as such to
avoid header collision with M5 — see the milestone-numbering
note above).

Memory: `feedback_inputsystem_ui_module_addcomponent.md` saved
during M5 verification (UGUI button-click dispatch chain).

---

## Quick-start instructions for next session

If the project rule "read Documentation/Session_Handoff.md at the
start of every new chat" is in place, the new chat will load this
file automatically. Otherwise paste:

> read Documentation/Session_Handoff.md at start of new chat
>
> no coding in the chat, provide Claude Code prompts
>
> all prompts end with telling claude code to compact
>
> Picking up from yesterday's handoff — combat + interact +
> death loops are all shipped and verified. No forced next
> step — see "Open milestone candidates" in the handoff for picks.
