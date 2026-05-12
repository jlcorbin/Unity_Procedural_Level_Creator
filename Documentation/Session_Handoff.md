# Session Handoff — 2026-05-11

## Session summary

A high-productivity session. No new gameplay milestones shipped but
significant structural work completed: a game concept was adopted,
the player character was codified as a self-configuring prefab,
the enemy foundation was established, the menu was audited and
cleaned, and three bugs were fixed. The project is in its cleanest
state to date.

---

## What shipped this session

### Game concept adopted
- `game-concept.md` (Unreal/UE5 origin) reviewed and rewritten as
  `Documentation/Hub & Hollow — Game Concept Document.md` — fully
  aligned with Unity 6.4 URP, FDP + World Bundle art, current
  milestone state, and all decisions made this session.
- UE5 builder handoff doc reviewed. Delta analysis confirmed:
  everything built through M11 maps cleanly to the UE5 prototype.
  Missing systems identified: Dodge (M12, shipped), Target Lock,
  Enemy health bar, WeaponStats SO, loot, town hub, extraction.
- Dungeon layout confirmed as a **separate pipeline** — revisit
  after combat + enemies complete.

### M12 — Player Dodge (shipped, verified prior session, synced this session)
- V key, 4-way directional roll, scripted impulse via CharacterController
- 0.5s i-frames via `IsInvulnerable` on `CharacterStatsRuntime`
- 25 stamina cost, 0.8s cooldown, cancels active attack
- `PlayerDodge.cs`, `PlayerBaseControllerDodgeExtender.cs`,
  `PlayerDodgePrefabAdder.cs`, `PlayerDodgeValidator.cs`
- Lesson: canonical Input asset is `Assets/InputSystem_Actions.inputactions`
  (not `Assets/Input/PlayerInputActions.inputactions`)

### M12-R — PlayerHero refactor
- `PlayerHero.cs` — root wiring manifest, `[RequireComponent]` chain
  for all 14 player-side components, zero gameplay logic
- `PlayerHeroBuilder.cs` — single idempotent builder replacing all
  individual adders
- `PlayerHeroValidator.cs` — consolidated validator (was ~50 checks,
  now 63 after absorbing HUD + DamageNumber checks this session)
- `Player_MaleHero.prefab` renamed → `Player_Hero.prefab` (GUID preserved)
- All individual adder scripts and `PlayerPrefabBuilder.cs` deleted
- `CinemachineRigBuilder.cs` restored as standalone after being
  accidentally caught in the deletion pass

### M13-EnemyBase — Enemy foundation
- `EnemyData.cs` ScriptableObject — per-enemy HP, attack, defense,
  move speed, detection/attack/leash ranges, cooldown
- `EnemyBase.cs` — root wiring manifest, `[RequireComponent]` chain,
  `[DefaultExecutionOrder(-50)]` pushes `EnemyData` into all consumers
  at Awake before siblings run
- `EnemyBaseBuilder.cs` — builds `Enemy_Grunt.prefab` from scratch,
  wires `_AssassinateZone` child (canonical assassinate target now
  that Dummy is retired)
- `EnemyBaseValidator.cs` — 40 checks
- `EnemyData_Grunt.asset` — 80 HP, 10 damage, detection 6m, attack 1.3m
- `Enemy_Grunt.prefab` — production enemy prefab
- `Dummy.prefab` + `CharacterStats_Dummy.asset` — **deleted**
- Lesson: `[DefaultExecutionOrder(-50)]` on `EnemyBase` is load-bearing.
  Without it, sibling Awakes at order 0 run first and overwrite the
  `InitFromEnemyData` push-down values.

### M-MenuCleanup + M-MenuDelete — Menu consolidation
Full audit of 48 MenuItem attributes across 29 files. Result:

**Deleted (17 editor scripts + 2 assets):**
`PlayerCombatHitboxBuilder`, `DummyPrefabBuilder`,
`EnemyBaseControllerBuilder`, `TestDoorBuilder`,
`CinemachineAutoBindAdder`, `PlayerCombatPrefabAdder`,
all M2-B step validators (Combat/Combo/Jump Animator/Runtime),
`PlayerDeathOverlayBuilder`, `PlayerHUDBuilder`,
`DamageNumberValidator`, `PlayerHUDValidator`,
`V2_SampleThemeBuilder`, `Dummy.prefab`, `CharacterStats_Dummy.asset`

**Final menu (23 items):**
```
LevelGen
├── Combat (4): Bake NavMesh, Build Enemy_Grunt, Place Enemy_Grunt, Validate Enemy
├── Input (3): Add Cinemachine Follow Camera, Place _MouseLock, Validate MouseLook
├── Interaction (1): Validate Interaction
├── Player (3): Add Cinemachine Follow Camera, Build Player_Hero, Validate Player_Hero
├── UI (3): Build DamageNumber, Build DamageNumberSpawner, Place DamageNumberSpawner
├── Whitebox [Complete] (7) — do not touch
├── LVL Configurator — do not touch
└── V2 Level Generator
```

### Bugfix — self-damage + enemy HP not depleting
- `PlayerCombat.NotifyHitboxTriggered` — added self-hit guard
  (`CompareTag("Player")` early return), mirrors `EnemyCombat` convention
- Enemy HP not depleting — root cause was log reading `maxHP` instead
  of `currentHP` (Bug B). Fixed.
- `PlayerCombat.cs:331-336` — IsDead guard added, mirrors M11 EnemyCombat
  guard. Subsequent swings into a corpse are silent no-ops.

---

## Validator state (end of session)

| Validator | Checks | Status |
|-----------|--------|--------|
| `Validate Player_Hero` | 63 | ✅ |
| `Validate Enemy` | 40 | ✅ |
| `Validate Interaction` | 16 | ✅ |
| `Validate MouseLook` | 7 | ✅ |

---

## Deferred / known issues

- **`EnemyDeath` collider-disable on Enemy_Grunt** — not verified.
  The IsDead guard in `PlayerCombat` covers the symptom (no damage
  to dead enemies) but `EnemyDeath._deathCollider` wiring in
  `EnemyBaseBuilder` should be audited. Next step: read
  `EnemyBaseBuilder.cs` and confirm `_deathCollider` SerializedObject
  ref is wired to the root CapsuleCollider.
- **Target Lock** — not yet started. Next combat milestone.
- **Enemy health bar** — world-space billboard above enemy head,
  Defense stat wired into `ApplyDamage`. Pairs with Target Lock.
- **WeaponStats SO** — replace hardcoded `attackDamage = 10` on
  `PlayerCombat`. Unlocks weapon variety from World Bundle's 8 sets.

---

## Open milestone candidates for next session

**Recommended next (combat completion):**
- **Target Lock** — sphere cast to nearest enemy, camera follow,
  world-space lock indicator on enemy, auto-clear on death.
  R key or RMB. Scoped and ready to prompt.

**Also on deck:**
- **Enemy health bar + Defense wired** — quick milestone, pairs
  naturally after Target Lock
- **WeaponStats SO** — enables weapon variety, clean SO pattern
  mirroring EnemyData
- **Second enemy archetype** — duplicate Enemy_Grunt, new
  `EnemyData_X.asset`, different stats. Tests EnemyBase generality.

**Deferred (level pipeline — separate track):**
- Whitebox PieceCatalogue end-to-end test
- V2 generator door-geometry placement
- Hand-crafted dungeon layout (3 zones, ~25-35 rooms)

---

## Key architectural reminders

- `PlayerHero` and `EnemyBase` are wiring manifests only — no gameplay
  logic. All future player/enemy components added via `[RequireComponent]`
  on the root + `InitFromX` method pattern.
- `EnemyData` SO is the single source of truth for enemy stats.
  `EnemyBase.Awake` pushes values into consumers — do not read
  `EnemyData` directly from `EnemyAI`, `EnemyCombat`, or
  `CharacterStatsRuntime`.
- `[DefaultExecutionOrder(-50)]` on `EnemyBase` is load-bearing.
  Do not remove or change it.
- Canonical Input asset: `Assets/InputSystem_Actions.inputactions`
- `IncomingDamage` convention: positive float = damage (processing
  step subtracts from HP). Never pass negative values to `ApplyDamage`.
- Single-writer-per-Animator-parameter invariant is in force for both
  player and enemy Animator controllers.

---

## Quick-start instructions for next session

> read Documentation/Session_Handoff.md at start of new chat
>
> no coding in the chat, provide Claude Code prompts
>
> all prompts end with telling claude code to compact
>
> Picking up from 2026-05-11 session — PlayerHero + EnemyBase
> foundations established, menu cleaned, bugs fixed. Recommended
> next milestone is Target Lock. See handoff for full candidate list.
