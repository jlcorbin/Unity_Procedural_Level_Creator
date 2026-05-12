# Session Handoff — 2026-05-11 (Night)

## Section 1 — Session Summary

### What shipped this session

**M14 — Enemy health bar (world-space billboard)**
- `EnemyHealthBar.cs` — world-space bar billboard above enemy head. Lazy-caches `Camera.main`, billboards via `transform.forward = -cam.transform.forward`, fills via `CurrentHP / MaxHP`. Hides on `OnDied` event. Public `SetVisible(bool)` for external drivers.
- `EnemyHealthBarProximityDriver.cs` — `InvokeRepeating`-based distance check (poll interval 0.2s, show radius 12m). Drives `EnemyHealthBar.SetVisible`. Self-disables if player not found at Start (config bug surfacing, not silent retry).
- `EnemyBaseValidator.cs` extended with checks 41-44 covering `EnemyData_Grunt.Defense >= 0`, `CharacterStatsRuntime.Defense` getter, `SetDefense(float)` method, and component presence checks for `EnemyHealthBar` + `EnemyHealthBarProximityDriver` in `Enemy_Grunt` hierarchy.
- Editor wiring of `EnemyHealthBar` onto `Enemy_Grunt.prefab` is deferred — scripts compile-ready but component placement requires manual Unity editor work.

**M14 — Defense wired into ApplyDamage (flat reduction)**
- `EnemyData.cs` — `defense` field encapsulated as `[SerializeField] private float` with public `Defense` property (`get => defense; set => defense = Mathf.Max(0f, value)`). Field name preserved so `EnemyData_Grunt.asset` serialized value survives.
- `CharacterStatsRuntime.cs` — new `Defense` auto-property + `SetDefense(float)` mutator. `ApplyDamage` applies flat reduction: `int effectiveDamage = Mathf.Max(0, amount - Mathf.RoundToInt(Defense))` after IsInvulnerable guard, before HP delta. Debug log appends `(after Defense N.NN)` when Defense > 0.
- `EnemyBase.cs` — `Awake` calls `_stats.SetDefense(_data.Defense)` immediately after `_stats.InitFromEnemyData(_data)`. `[DefaultExecutionOrder(-50)]` ordering preserved.

**M14.fix — EnemyData.Defense property fix (CS0200)**
- M14's `defense` field encapsulation broke `EnemyBaseBuilder.EnsureGruntData` which set the field directly (`asset.defense = 2f`). Fix: reference via `asset.Defense = 2f`. Surfaced CS0200 because the initial `Defense` property was expression-bodied get-only. Promoted to full `{ get; set; }` property with clamped setter.

**M14.fix — EnemyBaseBuilder .defense → .Defense reference**
- `EnemyBaseBuilder` line 276 corrected from `.defense` to `.Defense` following the field encapsulation.

**DamageNumberSpawner — _spawnYOffset added**
- `_spawnYOffset = 1.5f` default SerializeField added to `DamageNumberSpawner.cs`. Lifts damage number spawn point above collider contact height. Resolves the deferred per-actor Y-offset issue from M8.

**M11.1 — Post-hit i-frame window on enemy hit**
- `EnemyCombat.cs` — `_iFrameDuration = 0.5f` SerializeField (tunable per-enemy). `GrantIFrames(CharacterStatsRuntime)` private coroutine: calls `stats.SetInvulnerable(true)`, yields `WaitForSeconds(_iFrameDuration)`, calls `SetInvulnerable(false)` (skipped if target died during window). Coroutine starts in `NotifyHitboxTriggered` after `ApplyDamage`, guarded on `!stats.IsDead && _iFrameDuration > 0f`. Added `using System.Collections` import.
- `EnemyBaseValidator.cs` extended with checks 45-47: `_iFrameDuration` SerializeField present (check 45), `EnemyAnimationEventAbsorber.cs` confirmed absent (check 46), `OnHitboxOpen()` / `OnHitboxClose()` public void methods present (checks 47a/47b).

**Enemy animation events confirmed**
- `EnemyBaseBuilder` gained a menu item to stamp `OnHitboxOpen` / `OnHitboxClose` AnimationEvents onto the `Attack01_SwordAndShiled` clip at `0.35` / `0.65` normalizedTime. This fixed enemy hits not registering.

**Enemy hits player — end-to-end wiring confirmed**
- Full path verified: `EnemyAnimationEventForwarder.OnHitboxOpen/Close` → `EnemyCombat.OnHitboxOpen/Close` → `EnemyHitboxRelay.OnTriggerEnter` → `EnemyCombat.NotifyHitboxTriggered` → `CharacterStatsRuntime.ApplyDamage` on player.

**Player_Hero CapsuleCollider (IsTrigger=true) added**
- A `CapsuleCollider` (radius=0.4, height=1.8, center=(0,0.9,0), `isTrigger=true`) was added to the `Player_Hero` prefab root. This is required for `EnemyHitboxRelay.OnTriggerEnter` to fire — `CharacterController` alone does not receive `OnTriggerEnter` events. Applied manually to prefab this session.

**Diagnostic logs added and removed**
- Temporary diagnostic `Debug.Log` calls were added to `EnemyCombat` and `EnemyAnimationEventForwarder` during debugging, then removed. No diagnostic logging remains in these files.

### What was scoped but not shipped

- **Target Lock** — recommended next milestone, not started.
- **Enemy health bar sprite polish** — placeholder `UISprite` pending art swap.
- **Editor wiring of EnemyHealthBar onto Enemy_Grunt.prefab** — scripts present, manual Inspector work deferred.
- **WeaponStats SO** — replaces hardcoded `attackDamage = 10` on `PlayerCombat`.
- **Second enemy archetype** — tests `EnemyBase` generality.

---

## Section 2 — Validator State Table

Check counts read from `CLAUDE.md`. These must be run in Unity to verify.

| Validator | Menu Path | Checks | Status |
|-----------|-----------|--------|--------|
| Validate Enemy | `LevelGen ▶ Combat ▶ Validate Enemy` | 49 | Run after session start to verify |
| Validate Player_Hero | `LevelGen ▶ Player ▶ Validate Player_Hero` | 63 | Run after session start to verify |
| Validate Interaction | `LevelGen ▶ Interaction ▶ Validate Interaction` | 16 | Run after session start to verify |
| Validate MouseLook | `LevelGen ▶ Input ▶ Validate MouseLook` | 7 | Run after session start to verify |

**Note on Validate Enemy check count:** M13-EnemyBase established 41 checks. M14 appended checks 41-44 (Defense + health bar component presence). M11.1 appended checks 45-47 (i-frame field, absorber absent, OnHitboxOpen/Close methods). Actual count expected: 49 (checks 1-47 plus 47a/47b counted as two).

---

## Section 3 — Deferred / Known Issues

- **Player CapsuleCollider not automated** — The `CapsuleCollider (IsTrigger=true)` on `Player_Hero` root was added manually this session. `EnemyBaseBuilder` and `PlayerHeroBuilder` do not add it automatically. A fresh `Build Player_Hero Prefab` run will drop the collider. Should be automated in `PlayerHeroBuilder` as an explicit step so it survives prefab rebuilds.

- **EnemyBaseBuilder should wire Player CapsuleCollider** — Or alternatively, `PlayerHeroBuilder` should add `CapsuleCollider (IsTrigger=true, radius=0.4, height=1.8, center=(0,0.9,0))` on the root as part of its build sequence. Currently neither builder does this.

- **Enemy health bar Y offset tuning** — The default `_barYOffset = 2.2f` is reasonable for Grunt height but will need per-archetype adjustment as more enemy types ship. Not a blocking issue.

- **Enemy health bar sprite is a placeholder** — `UISprite.psd` (Unity built-in) is used for the fill Image per the M8 sprite-fix lesson. Swap with custom health bar art in a future pass.

- **EnemyAnimationEventForwarder `_combat` field wiring** — The `_combat` SerializeField on `EnemyAnimationEventForwarder` (which lives on the `MaleCharacterPBR` child) was found to be `None` in the scene instance after builder runs in previous sessions. The `EnemyBaseBuilder`'s `SerializedObject` wiring of this field should be audited to confirm it fires correctly on a clean build.

- **CLAUDE.md M11 Q5 stale claim** — The M11 entry states CharacterController.radius was bumped 0.3→0.4 to give the enemy hitbox arc "enough overlap window." The underlying assumption (that `CharacterController` receives `OnTriggerEnter`) was empirically disproved this session — a separate `CapsuleCollider (IsTrigger=true)` is required. The M11 section is worth a correction note on next pass, though the radius bump itself is harmless.

- **EnemyDeath `_deathCollider` wiring** — Not re-audited this session. `EnemyBaseBuilder` should be confirmed to wire `_deathCollider` to the root `CapsuleCollider` on `Enemy_Grunt`. The IsDead guard in `EnemyCombat` covers the symptom but the collider-disable on death path needs verification.

---

## Section 4 — Open Milestone Candidates

In priority order:

1. **Target Lock** (RECOMMENDED NEXT — combat completion track)
   Sphere cast to nearest `Targetable`, soft camera follow, world-space lock indicator above enemy, auto-clear on enemy death. R key or RMB. Integrates with `EnemyHealthBar.SetVisible` (force-show while locked). Clean scope, no architecture changes required.

2. **WeaponStats SO** — Replace hardcoded `attackDamage = 10` on `PlayerCombat` and `_attackDamage = 10` on `EnemyCombat` with a per-weapon `ScriptableObject`. Unlocks weapon variety from World Bundle's 8 weapon sets. Clean SO pattern, mirrors `EnemyData`.

3. **Second enemy archetype** — Duplicate `Enemy_Grunt.prefab` approach, new `EnemyData_X.asset` with different HP/damage/range/speed. Tests that `EnemyBase` generalizes correctly. Low-risk validation of the M13-EnemyBase architecture.

4. **Enemy health bar polish + Editor wiring** — Wire `EnemyHealthBar` + `EnemyHealthBarProximityDriver` onto `Enemy_Grunt.prefab` in the Inspector. Swap `UISprite` placeholder for custom art. Tune `_barYOffset`. Automate in `EnemyBaseBuilder`.

5. **Level pipeline — whitebox end-to-end test** (separate track)
   Drop configured whitebox LVLs into `LevelGenerator.unity`, verify generator connections work. `PieceCatalogue` integration and `LVL_Configurator` end-to-end. Not on the combat critical path.

---

## Section 5 — Key Architectural Reminders

- `PlayerHero` and `EnemyBase` are wiring manifests only — no gameplay logic. All future player/enemy components added via `[RequireComponent]` on root + `InitFromX` method pattern.
- `EnemyData` SO is the single source of truth for enemy stats. `EnemyBase.Awake` pushes values into consumers — do not read `EnemyData` directly from `EnemyAI`, `EnemyCombat`, or `CharacterStatsRuntime`.
- `[DefaultExecutionOrder(-50)]` on `EnemyBase` is load-bearing. Do not remove.
- Canonical Input asset: `Assets/InputSystem_Actions.inputactions`
- `ApplyDamage` convention: positive float = damage. Flat defense reduction applied inside `ApplyDamage`. Never pass negative values.
- Single-writer-per-Animator-parameter invariant is in force for both player and enemy Animator controllers.
- `Player_Hero` requires a `CapsuleCollider (IsTrigger=true)` on root for enemy `OnTriggerEnter` to fire. `CharacterController` alone does not receive trigger events.
- `EnemyAnimationEventForwarder` must be on the same GameObject as the Animator (`MaleCharacterPBR` child), not on the root.

---

## Section 6 — Quick-Start Instructions

> read Documentation/Session_Handoff.md at start of new chat
>
> no coding in the chat, provide Claude Code prompts
>
> all prompts end with telling claude code to compact
>
> Picking up from 2026-05-11 session — full combat loop working both directions. Recommended next milestone is Target Lock. See handoff for full candidate list.
