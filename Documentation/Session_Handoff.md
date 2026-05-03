# Session Handoff Update — 2026-05-02

> **Read first:** `CLAUDE.md` and the existing
> `Documentation/Session_Handoff.md` (the one this prompt replaces).
>
> Replace `Documentation/Session_Handoff.md` in place with the
> contents below. Do NOT append — overwrite the entire file. The
> previous handoff (dated 2026-05-01) is no longer the current
> state.
>
> Commit the change with message:
> `docs(handoff): update Session_Handoff.md for 2026-05-02 (M-CursorLock + M4-A + M4-B)`

---

## File contents to write

Write the following to `Documentation/Session_Handoff.md`,
replacing existing contents entirely:

```markdown
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

Combat scaffolding for one enemy type (Dummy) is structurally
complete: Player attacks → HP applied → flinch animation → death
animation → cleanup → despawn. End-to-end loop verified. Cursor
no longer escapes the Game view during Play-mode testing.

### Just shipped (this session — 2026-05-02)

Three milestones closed cleanly. All validators green.

**1. M-CursorLock — Cursor lock during Play mode (QoL detour)**
- New folder `Assets/Scripts/Input/`.
- `MouseLook.cs` moved from `Assets/Scripts/MouseLook.cs` →
  `Assets/Scripts/Input/MouseLook.cs` preserving GUID via the
  `.meta` move (the file was empty pre-move; no references to
  break). Header comment clarifies it's a cursor-state controller,
  NOT a look-rotation script — `PlayerInput` still owns Look input.
- Behavior: `CursorLockMode.Locked` + `Cursor.visible = false` on
  Play start; Escape unlocks; click in Game view re-locks; focus
  loss (Alt-Tab) unlocks; OnDestroy/OnDisable restores cursor.
- Uses `UnityEngine.InputSystem.Keyboard.current` /
  `Mouse.current` (matches project's input stack), null-guarded
  for headless mode.
- `[DefaultExecutionOrder(-100)]` so it locks before other scripts
  initialize.
- `_MouseLock` GameObject placed in active Play-mode test scene.
- Validator `MouseLookValidator.cs` under `LevelGen ▶ Input ▶
  Validate MouseLook`: 7 checks, all PASS (the in-Play check
  SKIPs in edit mode, which is correct).
- Reason for the detour: misclicks outside the Game view were
  interrupting combat iteration. Cheap to ship, immediate quality
  improvement.

**2. M4-A — Enemy Hit Reaction (Dummy)**
- `Targetable` extended from pure marker → event publisher.
  Added `event Action<Vector3> OnHit` and public
  `RaiseHit(Vector3 hitPoint)`. Targetable still knows nothing
  about colliders, animation, or stats — caller passes the hit
  point; subscribers compute their own behavior.
- `PlayerCombat.NotifyHitboxTriggered` now calls
  `target.RaiseHit(hit.ClosestPoint(...))` immediately after
  `ApplyDamage`. One-line addition; no refactor.
- New `EnemyBaseController.controller` — minimal Animator with
  Idle (default) and Hit states. `Hit` trigger parameter.
  `AnyState → Hit` (canTransitionToSelf=false, fixed-duration
  0.05s). `Hit → Idle` exit-time-driven (0.95). Built via
  idempotent editor script `EnemyBaseControllerBuilder.cs`.
- `Dummy.prefab` swapped from `PlayerBaseController` →
  `EnemyBaseController`. Dummy no longer references Player's
  Animator setup (resolves the M2 architectural messiness noted
  in yesterday's Q1).
- New `EnemyHitReaction` MonoBehaviour
  (`[RequireComponent(typeof(Targetable))]`,
  `[DisallowMultipleComponent]`). Subscribes to sibling
  Targetable's OnHit, fires Hit trigger on child Animator. Sole
  writer to Dummy's Hit parameter.
- Stagger window: script-side cooldown,
  `[SerializeField] float staggerWindow = 0.3f`. Hits within
  window are swallowed (damage already applied upstream — only
  the visual reaction is gated). Default tuned to combo cadence:
  Attack01→02→03 each fires a fresh flinch; faster-than-combo
  spam swallows extras.
- Validator `EnemyHitReactionValidator.cs`: 14 checks, all PASS.

**3. M4-B — Enemy Death (Dummy)**
- `CharacterStatsRuntime` gains
  `event Action<CharacterStatsRuntime> OnDied` (payload = self,
  mirroring OnHit's payload-passing convention) and public
  `bool IsDead` property. Single-fire via `_hasDied` guard —
  re-damaging a corpse does not re-fire OnDied. `Heal` does NOT
  revive (defensive default; HP can rise above 0 again but
  `_hasDied` stays true).
- `EnemyBaseController` extended with terminal `Death` state
  (Die01_SwordAndShield, Loop=false, **no outgoing transitions**
  — Animator parks on last frame). New `Death` trigger.
  `AnyState → Death` (canTransitionToSelf=false, fixed-duration
  0.05s). EnemyBaseControllerBuilder updated, still idempotent.
- New `EnemyDeath` MonoBehaviour — sole owner of the death
  sequence. `[RequireComponent]` for CharacterStatsRuntime +
  Targetable. On OnDied: disables Targetable, disables Collider,
  disables EnemyHitReaction, fires Death trigger, schedules
  `Destroy(gameObject, despawnDelay)`. Default despawn = 5s,
  tunable; `<= 0` keeps the corpse forever.
- `EnemyHitReaction` gained an `IsDead` guard in HandleHit —
  belt-and-suspenders against same-frame OnHit/OnDied subscriber
  ordering. Without it, a hit that crosses HP=0 could fire Hit
  and Death on the same frame depending on subscriber order;
  with it, the order is irrelevant.
- `Dummy.prefab` gains `EnemyDeath` component, all refs wired
  (animator, deathCollider, hitReaction).
- Temporary `[ContextMenu("Kill")]` debug hook added to
  `CharacterStatsRuntime` (calls `ApplyDamage(99999)`), tagged
  `// TODO removeMe-after-...` like the existing damage/heal
  hooks. Necessary because Dummy has 999 HP — manual death
  testing was tedious.
- Validator `EnemyDeathValidator.cs`: 16 checks, all PASS.

**Single-writer-per-Animator-parameter invariant preserved.**
Dummy's Animator has two writers now — `EnemyHitReaction` owns
Hit, `EnemyDeath` owns Death. Each owns exactly one parameter,
no overlap. This is the established convention extended to
multi-writer-but-non-overlapping; not a violation of the original
rule.

---

## Open architectural picks (for next session)

None outstanding. The three Q1/Q2/Q3 questions from yesterday's
handoff were answered and shipped:
- Q1 → (b) EnemyBaseController, with Q1-followup (a)
  canTransitionToSelf=false on Hit.
- Q2 → (a) Targetable raises OnHit, with Q2-followup (b) payload
  Action<Vector3>.
- Q3 → (c) Stagger window, with Q3-followup (b) 0.3s default.
- Death scope question → split out as M4-B with its own
  decisions: Q1-a terminal Death state, Q2-a EnemyDeath as sole
  owner, Q3-b despawn after 5s, Q4-b OnDied payload = self.

---

## What's next — natural branches

The combat scaffolding is now structurally complete for one enemy
type. Pick one (or propose another):

1. **Enemy AI — basic chase + attack on Dummy.** Closes the damage
   loop the other direction (Player can be attacked). Currently
   Player has stats + HUD but nothing damages it.
2. **Second enemy type — exercises the override-controller
   pattern** noted yesterday as the right long-term pattern (Q1-c
   in the M4-A discussion, deferred at the time). Architecture is
   now ready to actually use.
3. **Player death.** Mirror M4-B for Player: OnDied event already
   exists on CharacterStatsRuntime, so it's mostly UI/restart
   flow rather than new combat code.
4. **Back to the level generator.** V2 was at a stable checkpoint
   before the combat detour started. Room connection logic
   (door prefab placement decisions) was the next priority. See
   "On the horizon" section below for the V2 backlog.
5. **Something else.**

No tentative recommendation — these are all reasonable. The combat
side has its own internal momentum (loop-closing via #1 is
satisfying); the V2 side is the older project trunk and arguably
the higher-value direction long-term.

---

## File inventory at end of session

New runtime scripts:
```
Assets/Scripts/Input/MouseLook.cs   (moved from Assets/Scripts/, GUID preserved)
Assets/Scripts/Combat/EnemyHitReaction.cs
Assets/Scripts/Combat/EnemyDeath.cs
```

New editor scripts:
```
Assets/Scripts/Input/Editor/MouseLookValidator.cs
Assets/Scripts/Combat/Editor/EnemyBaseControllerBuilder.cs
Assets/Scripts/Combat/Editor/EnemyHitReactionValidator.cs
Assets/Scripts/Combat/Editor/EnemyDeathValidator.cs
```

Modified runtime scripts:
```
Assets/Scripts/Combat/Targetable.cs
   (added OnHit event + RaiseHit method; was pure marker)
Assets/Scripts/Combat/CharacterStatsRuntime.cs
   (added OnDied event + IsDead property + _hasDied guard;
    added [ContextMenu("Kill")] debug hook tagged removeMe)
Assets/Scripts/Player/PlayerCombat.cs
   (added one line to NotifyHitboxTriggered: target.RaiseHit
    after ApplyDamage)
```

New / modified assets:
```
Assets/Animations/Controllers/EnemyBaseController.controller
   (Idle + Hit + Death states; Hit + Death triggers;
    AnyState→Hit, AnyState→Death, Hit→Idle transitions;
    Death is terminal)
```

Modified prefabs:
```
Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab
   (Animator now references EnemyBaseController instead of
    PlayerBaseController; gained EnemyHitReaction component;
    gained EnemyDeath component with all refs wired)
```

New scene objects:
```
_MouseLock GameObject in active Play-mode test scene
   (with MouseLook component)
```

CLAUDE.md updated with three dated entries under the existing
milestone-log structure (M-CursorLock, M4-A, M4-B).

---

## Validators current state

All green at end of session:

| Validator                         | Checks | Status   |
| ---                               | ---    | ---      |
| DummyAndStatsValidator            | 12/12  | PASS     |
| PlayerHUDValidator                | 11/11  | PASS     |
| DamageRoutingValidator            | 12/12  | PASS     |
| MouseLookValidator                |  7/7   | PASS*    |
| EnemyHitReactionValidator         | 14/14  | PASS     |
| EnemyDeathValidator               | 16/16  | PASS     |

*MouseLookValidator: the in-Play cursor-state check SKIPs in edit
mode, which is correct behavior. Counts as PASS.

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
> Combat scaffolding for Dummy is structurally complete (hit
> reactions, death, despawn, all validators green). No
> architectural picks outstanding. Five natural-next-branches
> listed in the handoff doc — please summarize them and I'll
> pick.

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

- M1 + M2-A + M2-B + M2-C + M3 — verified working, do not refactor
- M-CursorLock — done
- M4-A + M4-B — done, all validators green, do not refactor
- The 7 V1 cleanup commits and their history — done, merged, stable
- `Assets/Scripts/Experimental/` — dormant, don't reference from V2
- `LVL_Configurator` — "complete, do not touch" per CLAUDE.md
  (const-string updates for folder reorg are the only acceptable
  touch)
- V2 generator (Phases A–D) — at a stable checkpoint
- The combat foundation, HUD, damage routing, hit reaction, and
  death sequence — all tested and locked. Future enemy work
  *adds to* them, doesn't modify them.

---

## Lessons from this session worth remembering

1. **Move scripts via `git mv` (or filesystem move including the
   `.meta` file), never delete-and-recreate.** Even an empty file's
   GUID is worth preserving — it costs nothing and avoids future
   broken references if anything ever started pointing at it.
2. **Single-writer-per-Animator-parameter scales cleanly.** With
   Hit owned by EnemyHitReaction and Death owned by EnemyDeath,
   the rule held without compromise. The original "single writer
   to the Animator" formulation generalizes naturally.
3. **Same-frame event-ordering races are real but cheap to
   defend against.** OnHit and OnDied could both fire on the same
   damage call when HP crosses 0; subscriber order isn't
   guaranteed; an `IsDead` early-return in the Hit handler makes
   the order irrelevant. Belt-and-suspenders is worth it for
   cross-script event subscriptions.
4. **Targetable as a pure marker was the wrong abstraction.**
   Promoting it to event publisher (OnHit) made the Player→Enemy
   wiring obvious and kept the player-side code unchanged. Marker
   components that "just exist" tend to want at least an event.
5. **Animator state terminality matters.** Death state has zero
   outgoing transitions, not "outgoing transition with HasExitTime
   = false" or similar. A truly terminal state has no exits at
   all — any transition is a foot-gun.
6. **Temporary debug hooks should be tagged removeMe.** The
   `Apply 10 Damage` / `Heal 10` hooks from M3 were tagged this
   way and the `Kill` hook joined them. When real damage/heal
   sources exist, these get removed in one pass.
7. **QoL detours are worth it.** The cursor-lock fix was scoped at
   ~30 lines but improved every subsequent combat-iteration
   session. If something is annoying you during development, fix
   it before it costs more time than the fix.

---

## On the horizon (V2 generator backlog — unchanged from previous handoff)

Deferred while combat work happens:

- Room connection logic — actual door prefab placement decisions
  (some openings stay open, others get doors) now that doorways
  and ExitPoints are established
- Tier stacking — multi-tier room height support
- End-to-end generator testing with Whitebox PieceCatalogue +
  LVL_Configurator run

Deferred to later phase:
- Diamond and Circle room shapes (ShapeStamp methods are
  dormant/ready)
- ExitPoint auto-detection for non-straight LVL modules (Option A:
  geometry scanning for opening centers)
- Dress step (PropCatalogue / SpawnPoints)
- `RoomWorkshop.unity` and `LevelGenerator.unity` scene creation

Open git issue: Large batch commits on the E: drive produce
`Permission denied` on `.git/objects/` writes (likely AV
interference). Workaround: PowerShell script committing ~5 files
at a time with `git gc` on failure.
```

---

## Compact

After the file is written and committed, **compact the
conversation**.