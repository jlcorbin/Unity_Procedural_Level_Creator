# Session Handoff — 2026-05-02

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

### Just shipped (this session — 2026-05-02)

Two milestones, both validated and play-tested.

**1. M-CursorLock — cursor lock during Play mode**

- `MouseLook.cs` (`LevelGen.Input`) at `Assets/Scripts/Input/MouseLook.cs`
  (moved from `Assets/Scripts/MouseLook.cs`, GUID preserved).
- Locks + hides cursor on enable; Escape unlocks; left-click in Game
  view re-locks; Alt-Tab unlocks; OnDisable/OnDestroy unlock as clean
  teardown. `[DefaultExecutionOrder(-100)]`. Uses
  `UnityEngine.InputSystem.Keyboard.current` / `Mouse.current` with
  null-guards for headless mode.
- Despite the legacy filename, this does NOT rotate the camera or
  read mouse delta — `PlayerInput` owns Look input.
- Editor menu items under `LevelGen ▶ Input ▶`: Validate MouseLook
  (7 checks) / Place `_MouseLock` in Active Scene (idempotent).
- Validator: 7/7 PASS in Play mode (6 PASS + 1 SKIP in edit mode).

**2. M4-A — enemy hit reaction (Dummy)**

First reactive enemy behavior. Dummy now visibly reacts when struck.
Death is explicitly deferred.

- `Targetable` extended from pure marker → marker + event publisher.
  Added `event Action<Vector3> OnHit` and `public RaiseHit(Vector3)`.
  AimPoint resolution preserved.
- `PlayerCombat.NotifyHitboxTriggered` calls `targetable.RaiseHit(hitPoint)`
  immediately after `ApplyDamage` in the stats-found branch. Hit point
  computed via `other.ClosestPoint(hitbox.bounds.center)`.
  Misconfiguration warning branch (Targetable without
  CharacterStatsRuntime) does NOT raise the event — firing it there
  would mislead subscribers.
- `EnemyBaseController.controller` (NEW) at
  `Assets/Animators/Enemy/EnemyBaseController.controller`, built
  procedurally via `LevelGen ▶ Combat ▶ Build EnemyBaseController`.
  Idle (default) + Hit, one Trigger param, AnyState→Hit
  `canTransitionToSelf=false` dur 0.05, Hit→Idle exitTime 0.95
  dur 0.10. Idempotent (delete + recreate on each build, so the
  controller's GUID changes — the Dummy prefab must be rebuilt
  after each controller build to refresh its reference).
- `EnemyHitReaction.cs` (NEW) at `Assets/Scripts/Combat/EnemyHitReaction.cs`.
  `[RequireComponent(Targetable)]` + `[DisallowMultipleComponent]`.
  Subscribes to `OnHit` in OnEnable, fires Animator's Hit trigger
  with a script-side stagger window (default 0.3s). Sole writer to
  the Hit parameter (single-writer-to-Animator invariant preserved).
- `Dummy.prefab` rebuilt: now references `EnemyBaseController` (NOT
  PlayerBaseController), carries `EnemyHitReaction`, retains
  `CapsuleCollider` (folded into builder this session — see lesson 2
  below).
- `DummyPrefabBuilder` updated: `BaseCtrlPath` swapped to
  EnemyBaseController; CapsuleCollider folded into the build (was
  previously a separate `Add Collider to Dummy` menu);
  EnemyHitReaction added to root with `animator` field bound to the
  child Animator via `AssignAnimatorField` SerializedObject helper.
- Validator: `LevelGen ▶ Combat ▶ Validate EnemyHitReaction` — 14/14
  PASS. `Validate Damage Routing` — 12/12 PASS (sanity re-run).

### Verified shipping at end of session

In `Player_M1_Test.unity`:
- Combat loop from yesterday still works (combo damages Dummy 30 HP total).
- Dummy now plays GetHit01 reaction on each hit. Combo cadence triggers
  reactions on every swing (combo interval > 0.3s stagger window).
- Cursor locks/hides on Play mode entry; Escape releases; left-click
  re-locks; Alt-Tab releases; cursor restored on Play exit.
- Player HP unchanged (Dummy doesn't fight back yet; combat is one-way).

### What's broken / pending observation

Nothing flagged.

---

## Open milestone candidates for next session

User picks at session start. No single forced next step — combat is
in a stable, playable loop. Candidates roughly by scope:

**Combat-adjacent (small to medium scope)**

- **Death state for Dummy.** Natural follow-on to M4-A. Add Death
  state to `EnemyBaseController`, wire `Die01_SwordAndShield` clip
  (already in pack). Add HP-zero hook on `CharacterStatsRuntime` →
  fires a `OnDied` event on Targetable. `EnemyHitReaction` (or a
  new `EnemyDeath` script) consumes it; disable Targetable + collider
  on death; despawn timer. Probably 1 session.
- **Stamina gameplay.** Stamina is data-only today (visible in HUD,
  doesn't move). Wire sprint cost, attack cost, regen. Stat-based
  gating on PlayerController.OnAttackPressed and sprint hold.
- **WeaponStats SO + per-weapon damage values.** Replace
  `attackDamage = 10` hardcoded SerializeField on PlayerCombat with
  a WeaponStats ScriptableObject. Path forward for weapon variety
  (the World Bundle has 8 weapon sets vs the wired SwordAndShield).
- **Damage numbers / floating combat text.** Cosmetic. Subscribe to
  `Targetable.OnHit` → spawn TMP_Text at hit point, lerp upward,
  fade out. Self-contained system.
- **Attack04 / heavy-attack / finisher.** Clip is in pack and validated
  (M2-B Step 1 survey); just needs Animator wiring + input binding.

**Combat-adjacent (larger scope)**

- **Player takes damage (Dummy fights back).** Dummy gets a small
  AI: face player, attack on cooldown, route damage back into
  PlayerCombat → CharacterStatsRuntime via the same hitbox pattern
  as the Player. Closes the combat loop.
- **More enemy types.** Pick a second character prefab from the World
  Bundle (24 MC* prefabs available); apply the EnemyBaseController +
  EnemyHitReaction pattern. Tests whether the foundation generalizes.

**Procedural-generation / level work**

- **M2-D level integration** — LevelGenerator-driven runtime spawn
  (composite Player_RuntimeRig refactor). The remaining piece of
  M2 player work; needs RoomBuilder's PlayerSpawnPoint marker (already
  shipped).
- **V2 generator door-geometry placement.** Door prefab placement at
  ExitPoint connections instead of open passages.
- **Whitebox `PieceCatalogue` end-to-end test.** Wire the whitebox
  pack (Steps 1-4 complete) into PieceCatalogue, validate in Room
  Workshop, run through V2 generator.

---

## What CC will likely need from you for the next prompt

Depends on the milestone picked. For the Death state continuation:

1. Confirm event name / location: `OnDied` on `Targetable` (mirrors
   `OnHit`) vs `OnDied` on `CharacterStatsRuntime` (closer to the
   data source).
2. Confirm despawn behavior: stay-as-corpse for N seconds vs immediate
   destroy vs disable-collider-and-leave.
3. Confirm whether to bundle weapon-damage SO with Death (so weapon
   damage actually matters before the Dummy dies) or keep them as
   separate milestones.

For other milestones, CC will pose milestone-specific architectural
questions before coding (project convention).

---

## Working preferences (unchanged)

- No coding in chat — all implementation goes back as Claude Code
  prompts (markdown files saved to `/mnt/user-data/outputs/`)
- All prompts end with telling Claude Code to compact
- CLAUDE.md is canonical, updated each session (CC handles the
  append at the end of each prompt's deliverables)
- Behavior tables before code on complex logic
- Empirical/direct: Inspector data over theoretical derivation;
  immediate misread correction
- Project Knowledge sync: scripts + docs only. Asset packs and
  binary assets excluded; paste specific files into the chat if
  needed
- One question at a time when narrowing scope; multi-choice over
  prose

---

## Things to leave alone

- M1 + M2-A + M2-B + M2-C + M3 + M4-A — verified working, do not refactor
- The 7 V1 cleanup commits and their history — done, merged, stable
- `Assets/Scripts/Experimental/` — dormant, don't reference from V2
- `LVL_Configurator` — "complete, do not touch" per CLAUDE.md
  (const-string updates for folder reorg are the only acceptable
  touch)
- V2 generator (Phases A–D) — at a stable checkpoint
- Combat foundation, HUD, damage routing, MouseLook, EnemyHitReaction
  — all tested and locked. Next milestone *adds to* them, doesn't
  modify them.

---

## Lessons from this session worth remembering

1. **World Bundle FBX filename ≠ AnimationClip sub-asset name.**
   The Idle FBX is `Idle_Battle_SwordAndShiled.fbx` but the
   AnimationClip sub-asset inside is named `Idle_Battle_SwordAndShield`
   (publisher corrected the typo internally during the M3 pack swap,
   kept FBX filenames + GUIDs identical for relink continuity).
   `LoadAllAssetRepresentationsAtPath` returns clips by sub-asset
   name. When writing an AnimatorController builder, name the loader
   constant for the sub-asset. Verify by grepping the FBX `.meta` for
   `clipAnimations[].name`. CLAUDE.md M3-02A had flagged this exact
   mismatch — overlooked during M4-A first-build attempt.

2. **Fold authoring steps into the main builder.** During M3, the
   Dummy's CapsuleCollider was added by a separate
   `Add Collider to Dummy` menu (`PlayerCombatHitboxBuilder.cs`).
   When M4-A re-ran `Build Dummy Prefab` (idempotent rebuild from
   scratch), the externally-added capsule was dropped — caught by
   DamageRoutingValidator check 10 going from PASS → FAIL during
   verification. Fix: the capsule is now built into
   `DummyPrefabBuilder` itself. The standalone menu still works
   (idempotent and harmless), but the main builder is now
   self-contained. **Pattern**: any authoring step a future rebuild
   would need to replay belongs IN the builder, not as a sibling
   menu the author has to remember to re-run.

3. **Cursor-lock is one-way for InputSystem-based projects.**
   `Cursor.lockState` and `Cursor.visible` are owned by a single
   small MonoBehaviour (`MouseLook.cs`); PlayerInput owns Look
   input separately. The legacy filename "MouseLook" is misleading
   — the script does NOT rotate the camera or read mouse delta.

4. **Single-writer-to-Animator invariant generalizes.** Each
   Animator on a character has exactly ONE script writing to its
   parameters: PlayerAnimator (player), EnemyHitReaction (Dummy).
   Other systems route through that single writer's public API.
   Preserved across M2-B (player combat / jump) and M4-A (enemy
   hit reaction). Future enemy AI will subscribe to the same
   Targetable.OnHit event but route through EnemyHitReaction (or
   sibling writers using the same pattern).

5. **Carry-forward from previous sessions:** AnimationEvents persist
   via `.meta` (use `ModelImporter.clipAnimations` API); Unity
   dispatches AnimationEvents to the Animator's GameObject only
   (use a forwarder); triggers need a non-static collider partner
   (kinematic Rigidbody on child trigger if root has only
   CharacterController); programmatic UI Images need explicit
   sprite for `Filled` / `Sliced` clipping.

---

## File inventory at end of session

**M-CursorLock**
```
Assets/Scripts/Input/MouseLook.cs (filled in; was empty stub at old path)
Assets/Scripts/Input/Editor/MouseLookValidator.cs (NEW)
```

**M4-A**
```
Assets/Scripts/Combat/Targetable.cs (event publisher; was pure marker)
Assets/Scripts/Combat/EnemyHitReaction.cs (NEW)
Assets/Scripts/Combat/Editor/EnemyBaseControllerBuilder.cs (NEW)
Assets/Scripts/Combat/Editor/EnemyHitReactionValidator.cs (NEW)
Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs (controller swap +
   EnemyHitReaction wiring + CapsuleCollider folded in)
Assets/Scripts/Player/PlayerCombat.cs (RaiseHit call after ApplyDamage
   in NotifyHitboxTriggered)
Assets/Animators/Enemy/EnemyBaseController.controller (NEW, produced
   by builder)
Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab (rebuilt by
   DummyPrefabBuilder against EnemyBaseController)
```

CLAUDE.md updated with two dated entries (M-CursorLock, M4-A) under
the existing milestone-log structure.

Memory: `feedback_world_bundle_clip_name_typo.md` added to capture
the FBX-vs-subasset name lesson for future sessions.

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
> Picking up from yesterday's handoff — combat foundation, HUD,
> damage routing, cursor lock, and enemy hit reaction are all
> shipped and verified. No forced next step — see "Open milestone
> candidates" in the handoff for picks.
