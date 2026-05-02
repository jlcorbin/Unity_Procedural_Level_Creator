# Session Handoff — 2026-05-01

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

### Just shipped (this session — 2026-05-01)

Three back-to-back milestones closed cleanly. All validators green.

**1. Dummy + CharacterStats foundation**
- `CharacterStats` ScriptableObject (`LevelGen.Combat`) — duplicate-
  and-tweak template for HP/Stamina/displayName/description.
  CreateAssetMenu under `LevelGen/Combat/Character Stats`.
- `CharacterStatsRuntime` MonoBehaviour — references one stats SO,
  copies max → current at Awake. Public read-only properties.
  `[DisallowMultipleComponent]`. ApplyDamage/Heal initially shipped
  as `internal` scaffolding; promoted to `public` in milestone 3
  below.
- `Targetable` marker component — empty identifier with optional
  `AimPoint` child convention. Future hook for enemy AI / target
  lock / damage application.
- `CharacterStats_Master.asset` (100/100, the template, never
  assigned directly) and `CharacterStats_Dummy.asset` (999/100,
  sandbox crutch — high HP keeps the dummy soaking hits forever
  while we build the rest of combat).
- `Dummy.prefab` — same MaleCharacterPBR model as Player, references
  `PlayerBaseController` directly (NOT the override), no player
  control scripts. Plays Idle, stands still indefinitely.
- Editor menu items under `LevelGen ▶ Combat ▶`: Build Dummy
  Prefab / Place Dummy in Active Scene. Idempotent; auto-creates
  the two stats assets on first run.
- `DummyAndStatsValidator`: 12 read-only checks, all PASS.

**2. Player HP / Stamina HUD**
- `PlayerHUD` MonoBehaviour (`LevelGen.UI`) — passive observer
  reading `CharacterStatsRuntime` each frame. Tag-based player
  lookup with retry coroutine for deferred-spawn scenarios (M2-D).
- `PlayerHUD.prefab` — Canvas root, Screen Space Overlay, bottom-
  left, HP (red) over Stamina (yellow), TMP_Text labels. Snap on
  damage, `Mathf.MoveTowards`-based lerp on heal (constant rate,
  frame-independent).
- `CharacterStats_Player.asset` (100/100) shipped.
- `Player_MaleHero` prefab gained `CharacterStatsRuntime`
  pointing at the Player stats asset.
- Two `[ContextMenu]` debug hooks on `CharacterStatsRuntime`:
  `Apply 10 Damage` / `Heal 10`. Originally tagged
  `// TODO M-DamageRouting`; retagged to
  `// TODO removeMe-after-stamina-and-heal-sources-exist` in
  milestone 3 because heals still have no real source.
- Editor menu items under `LevelGen ▶ UI ▶`: Build / Place /
  Add Stats (to Player_MaleHero).
- `PlayerHUDValidator`: 11/11 PASS.
- **Bug caught and fixed mid-session**: programmatically-created
  UI Image with no sprite assigned cannot clip when type=Filled,
  so `fillAmount` had no visual effect. Fix: assign
  `AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`
  to every bar (background = Sliced, fill = Filled).

**3. Damage routing — hitbox colliders**
- `HitboxRelay` (`LevelGen.Combat`) — bridge from a child trigger
  collider to `PlayerCombat.NotifyHitboxTriggered`. Reset() auto-
  resolves parent ref. No state, no Update.
- `AnimationEventForwarder` (`LevelGen.Combat`) — sits on the
  Animator's GameObject (MaleCharacterPBR child) and forwards
  `OnHitboxOpen` / `OnHitboxClose` to `PlayerCombat` on the parent.
  **Required** because Unity dispatches AnimationEvents to the
  Animator's own GameObject only — does NOT walk the hierarchy.
- `PlayerCombat` extended with `attackDamage` (int=10, hardcoded
  inspector field), `hitbox` (Collider ref), `_currentAttackHitList`
  (HashSet<Targetable>), `OnHitboxOpen()` / `OnHitboxClose()` /
  `NotifyHitboxTriggered(Collider)` public methods. Per-attack hit
  list prevents double-hits within one swing; cleared on each
  HitboxOpen.
- `CharacterStatsRuntime.ApplyDamage` / `.Heal` promoted from
  `internal` to `public`.
- `Player_MaleHero` gained `WeaponHitbox` child under `weapon_r`
  bone — BoxCollider (trigger, default disabled), HitboxRelay,
  **kinematic Rigidbody** (see resolution below).
- `Dummy.prefab` gained CapsuleCollider (radius=0.4, height=1.8,
  center=(0,0.9,0), isTrigger=false).
- AnimationEvents added to Attack01-03 at `clip.length * 0.35`
  (Open) and `clip.length * 0.65` (Close) via
  `ModelImporter.clipAnimations` + `SaveAndReimport`. Survives
  FBX reimport because events live in the .meta, not the FBX.
- Editor menu items under `LevelGen ▶ Combat ▶`: Add Weapon Hitbox
  to Player_MaleHero / Add Collider to Dummy / Add Animation Events
  to Attack Clips. All idempotent.
- `DamageRoutingValidator`: 12/12 PASS.
- **Two bugs caught and resolved mid-session:**
  - Hitbox box rotation was wrong on first build — `weapon_r` is
    oriented with local +Y or -Z as the blade direction, NOT +Z
    as the prompt's default assumed. Tuning is now baked into
    the prefab; the builder is idempotent and won't touch
    existing rotation/size on re-runs.
  - Trigger events did not fire on first playthrough. Root cause:
    `OnTriggerEnter` requires at least one of the colliding pair
    to have a non-static collider (Rigidbody, kinematic Rigidbody,
    or CharacterController). The CharacterController on the prefab
    root does NOT promote deeply-nested child colliders to "moving",
    so the WeaponHitbox needs its own kinematic Rigidbody. Builder
    now adds it automatically.

### Verified shipping at end of session

In `Player_M1_Test.unity`:
- Player + Dummy in scene, Player HUD bottom-left
- Walking up to Dummy and pressing attack:
  - Attack01 swing → Dummy HP drops by 10 (visible in inspector)
  - Combo 01→02→03 → 30 total HP gone
  - No double-hits within a single swing
  - Multi-target sweep works (place a second Dummy, both take damage)
- Player HUD's lerp-on-heal still works via the debug ContextMenu
- Player HP stays at 100/100 (Dummy doesn't fight back yet)

### What's broken / pending observation

Nothing flagged. Combat loop is live and stable.

---

## Next milestone — Hit reactions on Dummy

**Why this is next.** Combat damage routes through cleanly, but
hits are visually invisible — the Dummy stands still while its HP
drops in the Inspector. Adding the visible flinch is the cheapest
win available: Hit state already exists on PlayerBaseController,
and Dummy already references that controller. The architectural
work is in event routing and Animator ownership, not new clips or
graph design.

**The three open design questions** (paused at end of session,
need fresh thought tomorrow):

### Q1. Animator setup

Dummy currently uses PlayerBaseController for Idle. Three options:

- **(a) Reuse PlayerBaseController** — Dummy uses the same Animator
  setup. Hit state already exists, AnyState→Hit is already wired
  with `canTransitionToSelf=true`. Cheapest path; ships fastest.
  Cost: Dummy's Animator carries 9 unused params and 9 unused
  states (Locomotion, Sprint, JumpStart/Air/End, Attack01-03).
  Conceptually messy.
- **(b) Create EnemyBaseController** — separate, leaner Animator.
  Just Idle and Hit, maybe Death later. Two states, one trigger,
  one bool. Clean separation, future enemies inherit clean
  scaffolding. Cost: more upfront work this milestone.
- **(c) Override controller per-enemy** — Dummy uses a base Enemy
  controller, with override controllers swapping clips per enemy
  type. Cost: premature for now (one enemy type), but the right
  long-term pattern.

**Tentative recommendation**: (b). The Hit state is the entire
point of EnemyBaseController; future enemies will need it; the
work is small. (a) ships sooner but locks Dummy's Animator
identity to Player's, which is messy when AI work starts.
Override-per-enemy (c) can still happen later on top of (b).

### Q2. Event routing — who knows about the Animator?

Three options:

- **(a) Targetable raises an OnHit event; CharacterStatsRuntime
  fires it on damage.** PlayerCombat doesn't change. The Dummy
  has a small `EnemyHitReaction` script that listens and fires
  the Hit trigger on its own Animator.
- **(b) PlayerCombat directly calls a method on Dummy's Animator.**
  Hardest coupling. Bad architectural choice — listed only for
  completeness.
- **(c) `ApplyDamage` on CharacterStatsRuntime fires an event the
  Animator subscribes to.** Similar to (a) but the event lives on
  CharacterStatsRuntime rather than Targetable. Conceptually
  cleaner (damage → reaction is a tighter pairing than hit-
  detection → reaction).

**Tentative recommendation**: (c). `CharacterStatsRuntime` already
has `ApplyDamage` as the public damage entry point; an `OnDamaged`
event published from there is the natural place. Targetable stays
a pure marker (its job is "exists"), CharacterStatsRuntime owns
"data + events". A new `EnemyHitReaction` MonoBehaviour subscribes
to it and fires the Hit trigger.

This preserves the single-writer-to-Animator invariant: each
Animator has exactly one script writing to it.

### Q3. Stagger semantics

Should rapid combo hits chain hit reactions, or play through?

- **(a) Yes — interrupt** (`canTransitionToSelf=true` on Hit,
  restart clip on each new hit). Combo01-02-03 produces three
  visible flinches. Bad if the Hit clip is long.
- **(b) No — first hit must finish.** Subsequent hits visually
  swallowed even though damage applies. Bad for a punching-bag
  dummy whose entire purpose is feedback.
- **(c) Stagger window** — short cooldown (e.g. 0.15s) before a
  new Hit can interrupt. Combo hits at typical swing intervals
  each trigger; very fast double-strikes get one visual.

**Tentative recommendation**: (a) for now, on the assumption that
GetHit01_SwordAndShield is short enough (~0.467s per M2-B Step 1
clip survey) that interrupting feels right at combo cadence.
Revisit if it looks bad. (c) is the "right" answer eventually but
adds tunable state; (b) is wrong for a sandbox dummy.

---

## What CC will likely need from you for the next prompt

Once Q1-Q3 are answered:

1. Confirmation of the three architectural picks above
2. Permission to add a kinematic Rigidbody to the Dummy if needed
   (depends on whether Hit-reaction routing also needs collision
   work — probably not, but check)
3. Whether the `EnemyHitReaction` script should also handle Death
   in this milestone or stay scoped to just Hit. Death is small
   (clip exists in pack: `Die01_SwordAndShield`) but has knock-on
   work (disable Targetable on death, despawn timer, etc.) —
   easier to defer.

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
- The 7 V1 cleanup commits and their history — done, merged, stable
- `Assets/Scripts/Experimental/` — dormant, don't reference from V2
- `LVL_Configurator` — "complete, do not touch" per CLAUDE.md
  (const-string updates for folder reorg are the only acceptable
  touch)
- V2 generator (Phases A–D) — at a stable checkpoint
- The just-shipped combat foundation, HUD, and damage routing —
  all tested and locked. Next milestone *adds to* them, doesn't
  modify them.

---

## Lessons from this session worth remembering

1. **AnimationEvents on FBX clips persist via `.meta`, not the
   FBX itself.** Use `ModelImporter.clipAnimations` API +
   `SaveAndReimport`.
2. **Unity dispatches AnimationEvents to the Animator's own
   GameObject only.** Methods on parent GameObjects never fire
   from AnimationEvents. Use a forwarder component on the
   Animator's GameObject if you want to handle the events
   higher in the hierarchy.
3. **Triggers need a non-static collider partner.** A
   CharacterController on the root does NOT promote deeply-
   nested child colliders to "moving". A child trigger collider
   needs its own Rigidbody (kinematic is fine) for
   `OnTriggerEnter` to fire.
4. **Programmatically-created UI Images need an explicit sprite.**
   `Filled` and `Sliced` types both rely on the sprite for
   clipping/9-slice math; no sprite means no visual effect.
   Use `AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`.
5. **Rig local axes are pack-specific.** `weapon_r` blade
   direction is +Y or -Z, not +Z. Default hitbox orientations
   from a prompt are guesses; the validator will pass even when
   hits don't register, because that's a tuning question.
6. **Idempotent prefab builders are gold.** All three Combat
   menu items today are re-runnable without breaking existing
   tuning. Once `weapon_r` rotation was tuned manually, the
   builder respected it on every subsequent run.

---

## File inventory at end of session

New runtime scripts:
```
Assets/Scripts/Combat/CharacterStats.cs
Assets/Scripts/Combat/CharacterStatsRuntime.cs
Assets/Scripts/Combat/Targetable.cs
Assets/Scripts/Combat/HitboxRelay.cs
Assets/Scripts/Combat/AnimationEventForwarder.cs
Assets/Scripts/UI/PlayerHUD.cs
```

New editor scripts:
```
Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs
Assets/Scripts/Combat/Editor/DummyAndStatsValidator.cs
Assets/Scripts/Combat/Editor/PlayerCombatHitboxBuilder.cs
Assets/Scripts/Combat/Editor/DamageRoutingValidator.cs
Assets/Scripts/UI/Editor/PlayerHUDBuilder.cs
Assets/Scripts/UI/Editor/PlayerHUDValidator.cs
```

Modified runtime scripts:
```
Assets/Scripts/Player/PlayerCombat.cs (added attackDamage, hitbox,
   3 public methods, hit-list tracking)
```

New assets:
```
Assets/Data/CharacterStats/CharacterStats_Master.asset
Assets/Data/CharacterStats/CharacterStats_Dummy.asset
Assets/Data/CharacterStats/CharacterStats_Player.asset
```

New prefabs:
```
Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab
Assets/Prefabs/UI/PlayerHUD.prefab
```

Modified prefabs:
```
Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
   (CharacterStatsRuntime added; WeaponHitbox child added under
    weapon_r with collider + relay + kinematic Rigidbody)
```

Modified animation imports:
```
Attack01_SwordAndShiled.fbx.meta
Attack02_SwordAndShiled.fbx.meta
Attack03_SwordAndShiled.fbx.meta
   (each with 2 AnimationEvents at 35% / 65% of clip duration)
```

CLAUDE.md updated with three dated entries under the existing
milestone-log structure.

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
> and damage routing are all shipped and verified. Next milestone
> is hit reactions on Dummy. Three architectural questions are
> open from the handoff doc — please pose them and I'll answer.