# Session Handoff — 2026-05-12

## Section 1 — Session Summary

### What shipped this session

**M15 — Target Lock**
- `TargetLock.cs` (NEW) — singleton lock manager, `[DefaultExecutionOrder(-40)]`, RMB input via `PlayerInputReader.OnLockOnPerformed`, sphere-cast acquire, `OnDied` auto-unlock, range-break check in Update (`_breakRange = 25f`), `SetVisible` call on `EnemyHealthBar` on lock/unlock.
- `LockIndicator.cs` (NEW) — procedural equilateral triangle mesh, URP/Unlit yellow material, vertical bob, camera billboard via `Quaternion.LookRotation(_cam.transform.forward, _cam.transform.up)` (positive forward — negative caused mirror-flip).
- `TargetLockValidator.cs` (NEW) — 12-check validator, menu `LevelGen ▶ Player ▶ Validate Target Lock`, all 12 PASS confirmed.
- `Assets/InputSystem_Actions.inputactions` — `LockOn` Button action, `<Mouse>/rightButton` binding.
- `PlayerInputReader.cs` — `OnLockOnPerformed` event.
- `PlayerController.cs` — `_lockFaceSpeed` SerializeField, additive strafe branch (free-look preserved as else).
- `PlayerHero.cs` — `[RequireComponent(typeof(TargetLock))]`, `InitFromPlayerHero` injection.
- `PlayerHeroBuilder.cs` — `AddIfMissing<TargetLock>`, `WireProp("_targetLock", ...)`, LockOn UnityEvent binding.

**M15b — Damage Number Polish**
- `DamageNumber.cs` — camera billboard (positive forward fix), lateral spawn jitter (`_lateralJitter = 0.3f`), rise distance increased 1.5 → 2.0, TMP alpha fade 1 → 0 over lifetime via `_tmp.alpha = 1f - smoothT`.
- `DamageNumberBuilder.cs` — font size reduced 6 → 4. Outline now applied via a sibling `DamageNumber_FontMat.mat` asset cloned from the TMP font's `sharedMaterial`, with `_OutlineWidth` / `_OutlineColor` set directly on the asset and assigned via `tmp.fontSharedMaterial`. Bypasses the `outlineWidth` setter's `renderer.material` instantiation path that was leaking instance materials into the prefab during edit-mode builds.
- Namespace corrected from `LevelGen.UI` back to `LevelGen` (was breaking prefab type reference).
- Prefabs rebuilt: `DamageNumber.prefab`, `DamageNumberSpawner.prefab`.

### What was scoped but not shipped
- Enemy health bar editor wiring onto `Enemy_Grunt.prefab` (still deferred from M14).
- Player `CapsuleCollider (IsTrigger=true)` not automated in `PlayerHeroBuilder` (still manual).

---

## Section 2 — Validator State Table

Check counts read from `CLAUDE.md`. These must be run in Unity to verify.

| Validator | Menu Path | Checks | Status |
|-----------|-----------|--------|--------|
| Validate Target Lock | `LevelGen ▶ Player ▶ Validate Target Lock` | 12 | ✅ 12/12 PASS (confirmed this session) |
| Validate Enemy | `LevelGen ▶ Combat ▶ Validate Enemy` | 49 | Run at session start to verify |
| Validate Player_Hero | `LevelGen ▶ Player ▶ Validate Player_Hero` | 63 | Run at session start to verify |
| Validate Interaction | `LevelGen ▶ Interaction ▶ Validate Interaction` | 16 | Run at session start to verify |
| Validate MouseLook | `LevelGen ▶ Input ▶ Validate MouseLook` | 7 | Run at session start to verify |

---

## Section 3 — Deferred / Known Issues

- **Player CapsuleCollider not automated** — `CapsuleCollider (IsTrigger=true)` on `Player_Hero` root was added manually. `PlayerHeroBuilder` does not add it. A fresh `Build Player_Hero Prefab` run will drop it. Should be automated.
- **Enemy health bar editor wiring** — `EnemyHealthBar` + `EnemyHealthBarProximityDriver` scripts exist and compile but are not on `Enemy_Grunt.prefab`. Validator checks 44a/44b will FAIL until wired.
- **EnemyAnimationEventForwarder `_combat` field wiring** — should be audited to confirm it fires correctly on a clean builder run.
- **EnemyDeath `_deathCollider` wiring** — not re-audited. `EnemyBaseBuilder` should confirm it wires root `CapsuleCollider`.
- **LockIndicator shader stripping** — `Shader.Find("Universal Render Pipeline/Unlit")` must be in Always Included Shaders before an IL2CPP build. Editor-only for now.
- **Target lock `_targetLayer`** — currently set to Default in the scene instance. Should be set on the `Player_Hero` prefab via `PlayerHeroBuilder` or documented as a per-scene setup step.
- **Multi-target cycling** — deferred. Re-press currently just unlocks.
- **Lock-on-aware dodge** — deferred. Dodge doesn't consider lock direction yet.
- **Strafing animation blend tree** — deferred. Lateral movement has no animator state.

---

## Section 4 — Open Milestone Candidates

In priority order:

1. **WeaponStats SO** (RECOMMENDED NEXT — clean scope, high value)
   Replace hardcoded `attackDamage = 10` on `PlayerCombat` and `_attackDamage = 10` on `EnemyCombat` with a per-weapon `ScriptableObject`. Mirrors `EnemyData` pattern exactly. Unlocks weapon variety from World Bundle's 8 weapon sets.

2. **Second enemy archetype** — duplicate `Enemy_Grunt` approach, new `EnemyData_X.asset`. Tests that `EnemyBase` generalizes. Low-risk validation milestone.

3. **Enemy health bar polish + editor wiring** — wire `EnemyHealthBar` onto `Enemy_Grunt.prefab` in Inspector, swap UISprite placeholder, automate in `EnemyBaseBuilder`.

4. **Player CapsuleCollider automation** — add `CapsuleCollider (IsTrigger=true, radius=0.4, height=1.8, center=(0,0.9,0))` to `PlayerHeroBuilder` so it survives prefab rebuilds.

5. **Level pipeline — whitebox end-to-end test** (separate track) — `PieceCatalogue` wiring, `LVL_Configurator` runs, generator end-to-end in `LevelGenerator.unity`.

---

## Section 5 — Key Architectural Reminders

- `PlayerHero` and `EnemyBase` are wiring manifests only — no gameplay logic. All future components added via `[RequireComponent]` + `InitFromX` pattern.
- `TargetLock` is `[DefaultExecutionOrder(-40)]` — after `EnemyBase (-50)`, before default (0). Do not change.
- `LockIndicator` billboard uses **positive** `_cam.transform.forward` (not negative) — negative causes mirror-flip showing text/mesh backwards.
- `DamageNumber` namespace is `LevelGen` (not `LevelGen.UI`) — the sub-namespace breaks prefab type references in this project.
- `Player_Hero` requires a `CapsuleCollider (IsTrigger=true)` on root for enemy `OnTriggerEnter`. `CharacterController` alone does not receive trigger events.
- `EnemyData` SO is single source of truth for enemy stats. `EnemyBase.Awake` pushes values into consumers.
- `ApplyDamage` convention: positive float = damage. Flat defense reduction inside `ApplyDamage`. Never pass negative values.
- Static events (`Targetable.AnyTargetableHit`, `CharacterStatsRuntime.OnDied`) must be paired with `OnEnable +=` / `OnDisable -=` in every subscriber.
- TMP outline in edit-mode prefab authoring must go through `fontSharedMaterial` (clone + asset-save + assign), NOT `tmp.outlineWidth` — the latter triggers `renderer.material` instantiation and leaks materials into prefabs.

---

## Section 6 — Quick-Start Instructions

> Read `Documentation/Session_Handoff.md` at start of new chat.
>
> No coding in chat — provide Claude Code prompts only.
>
> All prompts end with telling Claude Code to `/compact`.
>
> Picking up from 2026-05-12 session — M15 Target Lock + M15b damage number
> polish both shipped and committed. Recommended next milestone is WeaponStats SO.
> See Section 4 for full candidate list.
