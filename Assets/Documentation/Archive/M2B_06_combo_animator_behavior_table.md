# M2-B Step 6 — 3-Hit Combo Animator Behavior Table

**Date:** 2026-04-29
**Scope:** Animator-only changes to extend the existing single-Attack
state to a 3-hit combo (Attack01 → Attack02 → Attack03). Adds one
parameter, two states, four transitions, two override slots. No
runtime script changes — `PlayerCombat.cs` modifications come in
Step 7.
**Status:** Ready for review. Stop point per the prompt — do not
proceed past this table without user confirmation.

**Source data verified:**
- `PlayerBaseController.controller`: 8 parameters, 8 states, 18
  state-to-state transitions + 1 anyStateTransition (= 19 total).
- `PlayerOverride_MaleHero.overrideController`: 11 self-mapped slots.
- Attack02 + Attack03 clips: validated PASS in Step 1 survey
  (Section C). Both 0.533 s @ 30 fps, Humanoid, all three root-motion
  flags locked (1/1/1). No FBX repair needed before wiring.

---

## Section 0 — Naming reconciliation

The pack uses two spellings. Both real on disk; neither is a typo to
fix:

- `Attack01_SwordAndShiled` (typo, **no** second "e") — used by
  Attack01/02/03/04, Idle_Battle, all the existing override slot
  loaders.
- `_SwordAndShield` (correct) — used by Hit, Move, Jump, Sprint,
  Defend, Die, Dizzy, GetUp, Levelup, Victory.

**Step 6 implication:** the new override slots load
`Attack02_SwordAndShiled.fbx` and `Attack03_SwordAndShiled.fbx` (typo
form). Match exact filenames or the AssetDatabase load returns null
and the validator fails Check 4.

---

## Section 1 — Parameters (delta from Step 4)

`PlayerBaseController` parameters after Step 6:

| Name | Type | Default | Status |
|---|---|---|---|
| MoveX | Float | 0 | unchanged |
| MoveZ | Float | 0 | unchanged |
| Speed | Float | 0 | unchanged |
| IsSprinting | Bool | false | unchanged |
| Attack | Trigger | — | unchanged (Step 2) |
| Hit | Trigger | — | unchanged (Step 2) |
| Jump | Trigger | — | unchanged (Step 4) |
| IsGrounded | Bool | true | unchanged (Step 4) |
| **ComboNext** | **Trigger** | **—** | **NEW** |

Total: 8 → **9 parameters**.

**Why a Trigger and not a Bool:** the auto-clear semantics of a
Trigger map directly to the gameplay design.

- A buffered ComboNext press that doesn't get consumed within one
  Animator update auto-clears, so a queued combo input is naturally
  discarded if the player is hit mid-window. A Bool would need
  explicit clearing — either in `TakeHit()`, or via a
  StateMachineBehaviour on `OnStateExit`. Both work but add a
  side-channel that the Trigger's default behavior already provides.
- "Trigger lost if not consumed" is the *correct* semantic for a
  buffered combo input.

---

## Section 2 — States (delta from Step 4)

After Step 6:

| State | Loop | Apply Root Motion | Speed | Motion (override slot) | Status |
|---|---|---|---|---|---|
| Idle | yes (clip) | off (Animator-component) | 1 | Idle slot | existing |
| Locomotion | per-clip | off | 1 | FWD/BWD/LFT/RGT | existing |
| Sprint | yes (clip) | off | param Speed | SprintFWD slot | existing |
| Attack | off (state) | off | 1 | Attack01 slot | existing (Step 2) |
| Hit | off (state) | off | 1 | Hit01 slot | existing (Step 2) |
| JumpStart | off (state) | off | 1 | JumpStart slot | existing (Step 4) |
| JumpAir | yes (clip) | off | 1 | JumpAir slot | existing (Step 4) |
| JumpEnd | off (state) | off | 1 | JumpEnd slot | existing (Step 4) |
| **Attack02** | **off (state)** | **off** | **1** | **Attack02 slot** | **NEW** |
| **Attack03** | **off (state)** | **off** | **1** | **Attack03 slot** | **NEW** |

Total: 8 → **10 states**.

**Apply Root Motion** is a global Animator-component setting, not a
per-state YAML field — already off on the prefab's Animator from
Step 4. Both new clips have FBX-level root motion locked
(1/1/1) so the runtime effect is also off regardless. Same as the
existing Attack state — no inspector or component change needed.

**Naming:** existing Attack state stays named `Attack` (NOT renamed
to `Attack01`). PlayerCombat caches
`AttackStateHash = Animator.StringToHash("Attack")` and reads the
state name in multiple places. Renaming would cascade through
PlayerCombat hash invalidation, smoke test docs, validators. The
*clip* in the slot is `Attack01_SwordAndShiled`; the *state* is
`Attack`. New states are `Attack02` and `Attack03` — full names
matching the existing per-state convention.

**State YAML-field details (verbatim parity with Attack):**
- `m_Speed: 1`
- `m_IKOnFeet: 0`
- `m_WriteDefaultValues: 1`
- `m_Mirror: 0`
- `m_CycleOffset: 0`
- `m_MirrorParameterActive: 0`
- `m_CycleOffsetParameterActive: 0`
- `m_TimeParameterActive: 0`
- `m_Position` arbitrary (visual only). Recommendation: align below
  the existing Attack state in the graph layout, e.g. `(450, 100)`
  and `(450, 200)`.

---

## Section 3 — Transitions (delta from Step 4)

Existing 18 + 1 transitions stay untouched. New transitions:

| # | From | To | Conditions | Has Exit Time | Exit Time | Duration | Notes |
|---|---|---|---|---|---|---|---|
| **N14** | Attack | Attack02 | `ComboNext` (If) | ON | **0.85** | 0.10 | Combo fires if buffered |
| **N15** | Attack02 | Attack03 | `ComboNext` (If) | ON | **0.85** | 0.10 | Combo fires if buffered |
| **N16** | Attack02 | Idle | (none) | ON | **0.90** | 0.10 | Default exit if no buffer |
| **N17** | Attack03 | Idle | (none) | ON | **0.90** | 0.10 | Combo finisher always exits |

All four transitions: `m_HasFixedDuration: 1`,
`m_InterruptionSource: 0`, `m_OrderedInterruption: 1`,
`m_CanTransitionToSelf: 0`, `m_Solo: 0`, `m_Mute: 0`.

**Existing N4 (Attack → Idle) is preserved.** It now serves as the
**fallback** path:

- N14 has exit-time 0.85, condition `ComboNext`. If the trigger is
  set, N14 fires at the 0.85 frame.
- N4 has exit-time 0.90, no condition. If N14 didn't fire (because
  ComboNext wasn't set), N4 fires at 0.90.

**Order matters in `Attack.transitions`:** N14 must be **listed
before** N4 so the Animator evaluates the combo branch first. Same
on `Attack02.transitions` for N15 before N16. Unity evaluates
transitions in their source-state list order each frame.

Total transitions after Step 6:
- State-to-state: 18 + 4 = **22**
- AnyState: unchanged at **1**

---

## Section 4 — Per-state transition lists after Step 6

```
Idle.transitions       = [#1 → Locomotion, N1 → Attack, N7 → JumpStart]    (3)
Locomotion.transitions = [#2 → Idle, #3 → Sprint, N2 → Attack, N8 → JumpStart] (4)
Sprint.transitions     = [#4 → Locomotion (IsSprinting==false),
                          #5 → Locomotion (MoveZ<0.7),
                          #6 → Locomotion (Speed<0.1),
                          N3 → Attack,
                          N9 → JumpStart]                                  (5)
Attack.transitions     = [N14 → Attack02, N4 → Idle]                       (2)  ← N14 BEFORE N4
Hit.transitions        = [N6 → Idle]                                       (1)
JumpStart.transitions  = [N10 → JumpAir, N11 → JumpAir (fallback)]         (2)
JumpAir.transitions    = [N12 → JumpEnd]                                   (1)
JumpEnd.transitions    = [N13 → Idle]                                      (1)
Attack02.transitions   = [N15 → Attack03, N16 → Idle]                      (2)  ← N15 BEFORE N16
Attack03.transitions   = [N17 → Idle]                                      (1)

stateMachine.anyStateTransitions = [N5 → Hit]                              (1)
```

State-to-state total: 3 + 4 + 5 + 2 + 1 + 2 + 1 + 1 + 2 + 1 = **22**.
AnyState total: 1.

---

## Section 5 — How the combo flow runs (illustrative trace)

This trace anchors the design. Frame numbers assume 60 Hz simulation;
Animator state normalizedTime is `n` for the current state's clip
(both Attack01/02/03 are 0.533 s ≈ 32 frames).

**Frame 0:** Player in Idle. Player presses Attack.
- PlayerCombat.OnAttackPressed sees state=Idle → calls
  `_anim.SetAttackTrigger()`.
- Animator: Attack trigger fires, N1 (Idle → Attack, dur 0.10)
  transitions, Attack state begins. Attack01_SwordAndShiled clip plays.

**Frame ~16 (n ≈ 0.5):** Player presses Attack again, in combo
window.
- PlayerCombat sees state=Attack, n in [0.40, 0.80] window. Sets
  `_attackBuffered = true`. Does NOT fire any trigger yet.

**Frame ~27 (n ≈ 0.85):** Buffer-consume threshold (`bufferConsumeAt
= 0.85`) reached.
- PlayerCombat sees state=Attack, n ≥ 0.85, `_attackBuffered=true`.
- **Step 7 will change this**: instead of re-firing Attack (current
  Step 3 behavior), PlayerCombat calls `_anim.SetComboNext()` which
  fires the new ComboNext trigger.
- Animator: at the same frame, Attack state's exit-time 0.85 for N14
  is satisfied AND the ComboNext condition is satisfied. N14 fires,
  transition to Attack02 begins (0.10 s blend). ComboNext auto-clears
  on transition consumption.

**Frame ~28+ (Attack02 begins):** Attack02_SwordAndShiled clip plays.
Same shape as Attack01 — 0.533 s duration, same combo window
defaults.

**Frame ~43 (in Attack02, n ≈ 0.5):** Player presses Attack again.
Same buffer behavior. Sets `_attackBuffered = true`.

**Frame ~54 (in Attack02, n ≈ 0.85):** N15 fires (Attack02 →
Attack03). ComboNext consumed. Attack03 begins.

**Frame ~80 (Attack03 ends, n ≈ 0.90):** N17 fires (Attack03 →
Idle).
- Attack03 has only one outgoing transition (no further combo).
- Even if the player has been mashing Attack, no Attack04 wiring
  exists — the buffer is harmless because `SetComboNext()` won't get
  called from Attack03 in Step 7's logic.

**Alternate path A — combo dropped:** Player presses Attack01 once
but never presses inside the combo window.
- `_attackBuffered` stays false. `SetComboNext()` is never called.
- Attack runs to its 0.90 exit-time. N14's condition (ComboNext) is
  unsatisfied at 0.85; N14 does not fire.
- N4 (no condition, exit-time 0.90) fires. Attack → Idle. Player
  back in Idle; next press starts the combo at Attack01.

**Alternate path B — hit mid-combo:** Player is in Attack02 when
damage lands.
- Some external script calls `PlayerCombat.TakeHit()`. Hit trigger
  fires. N5 (Any State → Hit, canTransitionToSelf) wins because it
  has no exit-time.
- `_attackBuffered` is cleared in `TakeHit()` (existing Step 3
  behavior). If a ComboNext trigger was set inside the same frame
  but not consumed by N15 yet, it auto-clears within one Animator
  update.
- Animator: Attack02 → Hit. Hit clip plays. After Hit's exit-time
  (0.85), N6 fires (Hit → Idle). Combo is fully reset.

**Alternate path C — buffered press too late (after window):**
Player presses inside `[comboWindowClose, bufferConsumeAt)` —
between 0.80 and 0.85.
- PlayerCombat sees window already closed (n > comboWindowClose).
  Press is dropped.
- N14 evaluates at 0.85 with ComboNext NOT set; doesn't fire.
- N4 fires at 0.90. Attack → Idle.
- This is the "combo windowed" feel — late presses don't extend the
  combo.

---

## Section 6 — Why this design

### 6.1 Why ComboNext is a Trigger, not a Bool

Trigger auto-clear handles the hit-cancels-combo case for free. A
Bool requires explicit clearing — where? In `TakeHit()`? In a
StateMachineBehaviour on Hit's `OnStateEnter`? Both are extra code
paths the Trigger's built-in semantics already cover.

The "trigger lost if not consumed" property is the *correct*
behavior here: an unconsumed ComboNext means a combo press that got
cancelled. Discarding it matches the player's intuition that
"getting hit interrupts my combo."

### 6.2 Why N14 is a separate transition rather than modifying N4

N4 (Attack → Idle, exit-time 0.90, no condition) does its job. Adding
N14 in parallel preserves N4 unchanged; if combo logic ever needs to
be removed, deleting N14 is a clean reversal.

Unity's transition system has one target per transition — there's
no way to express "if condition then go to A else go to B" in a
single transition. Two transitions with priority via list-order is
the idiomatic pattern.

### 6.3 Why exit-times 0.85 / 0.90 and not the other way around

The earlier-exit-time transition is checked first. Placing the
combo-routing transition (N14) at 0.85 with the ComboNext condition,
and the fallback (N4) at 0.90 with no condition, gives clean
priority: combo wins if buffered, fallback otherwise.

Reversing them (combo at 0.90, fallback at 0.85) would never let the
combo fire — N4 would always fire first.

### 6.4 Why `bufferConsumeAt = 0.85` aligns with N14 exit-time = 0.85

PlayerCombat fires `SetComboNext()` at `bufferConsumeAt` (currently
0.85 from Step 3). The Animator evaluates N14 at 0.85 exit-time. The
trigger needs to be set at the moment the transition is evaluated,
which means setting it on the same frame as `n >= 0.85` is reached.

The Animator processes triggers in the same update where they're
set, so `SetComboNext()` followed by transition evaluation in the
same frame works. (Empirically confirmed with the Step 3 buffered
re-fire: `SetAttackTrigger()` at `n >= bufferConsumeAt` reliably
re-enters Attack via N1's behavior.)

### 6.5 Why Attack03 has only one outgoing transition

Three-hit combo is the locked decision. Attack03 is the finisher; it
always returns to Idle.

Adding an Attack03 → Attack04 transition would extend to a four-hit
combo. Attack04's clip is reserved (validated in Step 1 survey but
not wired). That extension is a future prompt's concern.

### 6.6 Why the existing Attack state is not renamed to Attack01

Hash stability and minimal blast-radius change.

PlayerCombat already caches
`AttackStateHash = Animator.StringToHash("Attack")` and uses it in
multiple places: state polling in `Update()`,
`OnAttackPressed` routing decisions, and (in Step 7) the buffer
window evaluation. Renaming would cascade through PlayerCombat
hash invalidation, smoke test docs all referencing "Attack" the
state.

The clip in the slot is Attack01; the state name stays `Attack`.
This is semantically clean: "state Attack plays clip
Attack01_SwordAndShiled, with Attack02 and Attack03 as combo
follow-ups."

### 6.7 Why no clip-side `loopTime` fix on Attack02/Attack03 FBX

Step 1 Section C reports `loopTime: 1` on Attack02/03 (WARN). Same
as the existing Attack01. The state-level `Loop=off` setting (and
specifically the Attack state's `m_LoopBlend: 0` shape) governs
runtime looping behavior; the FBX flag is essentially ignored when
the state has finite duration.

The existing Attack01 state has been running since Step 2 with this
exact configuration and no looping issues. Same pattern for
Attack02/03 — no FBX edits needed.

### 6.8 Why the Hit interrupt path (N5) doesn't need updating

N5 is `AnyState → Hit` with `m_CanTransitionToSelf: 1`. AnyState
includes the new Attack02 and Attack03 states automatically — no
new transitions need to be authored from those states to Hit.

Combined with Section 5's Alternate Path B, this means hit-cancels
work on the new combo states for free.

---

## Section 7 — Open questions (recommendations)

1. **N14 / N15 exit-time = 0.85.** Earlier than N16/N17 (0.90) so
   the combo branch wins if ComboNext is set. Tighter (0.80) feels
   snappier; looser (0.88) feels heavier. **Recommendation: 0.85.**
   Aligns with `PlayerCombat.bufferConsumeAt` default.

2. **N16 / N17 / N4 exit-time = 0.90.** Existing N4 is already 0.90
   from Step 2. Matching N16 (Attack02 → Idle) and N17 (Attack03 →
   Idle) keeps consistency. **Recommendation: 0.90.**

3. **Transition durations: 0.10 across all 4 new transitions.** Same
   as N4 and the Step 4 Idle/Locomotion/Sprint → Attack transitions.
   **Recommendation: 0.10.**

4. **N14 / N15 ordering in transition list.** Listed before the
   fallback transition on the same source state.
   **Recommendation: explicit ordering in YAML;** validator checks
   the order in Step ④ check 7 and 8.

5. **Attack02 and Attack03 state property values.** Match existing
   Attack: `motion = clip`, `speed = 1.0`, `iKOnFeet = false`,
   `writeDefaultValues = true`, `mirror = false`.
   **Recommendation: identical to Attack** for consistency.

6. **`m_LoopBlend` on Attack02/03 states.** Existing Attack state has
   `m_LoopBlend: 0`. **Recommendation: same on Attack02/03** — these
   are one-shot states.

7. **State `m_Position` in the graph layout.** Visual only, no
   runtime effect. Default values: Attack02 at `(450, 100)`,
   Attack03 at `(450, 200)`. The user can rearrange in the editor
   afterward without affecting validators.

8. **YAML fileID allocation.** Continue the
   `1100000000000000xxx` convention from Steps 2 and 4.
   - Attack02 state: `1100000000000000006`
   - Attack03 state: `1100000000000000007`
   - N14 transition: `1100000000000000114`
   - N15 transition: `1100000000000000115`
   - N16 transition: `1100000000000000116`
   - N17 transition: `1100000000000000117`

   No collision risk with hash-style fileIDs (negative or large
   positive integers from existing transitions).

---

## Section 8 — Summary of work to follow this table

After user confirmation:

- **Step ②** Add 2 self-mapped slots to
  `PlayerOverride_MaleHero.overrideController` (11 → 13). Loaded by
  filename via `AssetDatabase.LoadAllAssetsAtPath` + name filter.
- **Step ③** Edit `PlayerBaseController.controller` YAML:
  - Add 1 parameter (ComboNext, type 9 = Trigger).
  - Add 2 states (Attack02, Attack03 at fileIDs 006/007).
  - Add 4 transitions (N14/N15/N16/N17 at fileIDs 114-117).
  - Insert N14 BEFORE N4 in `Attack.m_Transitions`.
  - Append N15 BEFORE N16 in `Attack02.m_Transitions`.
  - Append N17 in `Attack03.m_Transitions`.
  - Add states to `Base Layer.m_ChildStates`.
- **Step ④** Reflection validator:
  - Param presence (ComboNext = Trigger).
  - State presence (Attack02, Attack03).
  - Motion-resolves on both new states.
  - Override resolution by name (Attack02_SwordAndShiled,
    Attack03_SwordAndShiled).
  - Transition counts (22 + 1).
  - Per-state transition counts (Attack=2, Attack02=2, Attack03=1).
  - Per-state transition order (combo before fallback).
  - Exit-time spot checks (N14/15 = 0.85; N16/17 = 0.90).
- **Step ⑤** Append Step 6 status block to `CLAUDE.md`.

**No PlayerCombat / PlayerController / PlayerInputReader /
PlayerAnimator / FBX / prefab modifications.** Step 7 handles
runtime wiring.

---

**Stop here for user review. Steps ②–⑤ wait until the user confirms
this table.**

**Behavior table path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_06_combo_animator_behavior_table.md`

---

## Design Correction — 2026-04-29 (post-Step 7 runtime test)

**Symptom:** During Step 7 smoke testing, the combo chained Attack →
Attack02 every time the player pressed Attack from Idle, even without
a buffered press during the combo window. The Animator was firing N14
(Attack → Attack02) at the exit time without the `ComboNext` trigger
ever being set (verified via runtime logging in PlayerCombat.Update
and PlayerAnimator.SetComboNext — neither logged before the state
transition).

**Root cause:** Unity 6.4's Animator transition evaluation for
"`Has Exit Time = true` + Trigger condition" does not behave per the
documented "AND" semantics in this configuration. Empirically, the
transition fires automatically when normalizedTime crosses the exit
time, regardless of whether the trigger is set. This is consistent
across N14 (Attack → Attack02) and N15 (Attack02 → Attack03) —
both fired at 0.85 without `ComboNext` being set, so the Animator
auto-routed the combo without runtime gating.

**Fix:** Remove `Has Exit Time` from N14 and N15 (set
`m_HasExitTime: 0`). The transitions now fire purely on the
`ComboNext` trigger condition. `PlayerCombat.Update` already gates
`SetComboNext()` at `n >= bufferConsumeAt` (0.85), so the *effective*
fire time is unchanged — but the gate is now enforced by script
(PlayerCombat) rather than by Animator exit-time.

**Final transitions after correction:**

| # | From | To | Conditions | Has Exit Time | Exit Time | Duration |
|---|---|---|---|---|---|---|
| **N14** | Attack | Attack02 | `ComboNext` (If) | **OFF** | (n/a) | 0.10 |
| **N15** | Attack02 | Attack03 | `ComboNext` (If) | **OFF** | (n/a) | 0.10 |
| **N16** | Attack02 | Idle | (none) | ON | 0.90 | 0.10 |
| **N17** | Attack03 | Idle | (none) | ON | 0.90 | 0.10 |

N16 and N17 retain `Has Exit Time = true` because they have no
conditions (the auto-fire-at-exit-time semantics is exactly what we
want for those).

The transition list-order priority pattern (combo branch listed
before fallback) remains unchanged — Unity's evaluation walks the
list each frame, so a condition-only transition listed before a
no-condition exit-time transition still wins when its condition is
met.

**Validator update:** `PlayerComboAnimatorValidator.cs` Check 9
updated to expect `hasExitTime=false` on N14/N15 and unchanged
`hasExitTime=true` + `exitTime≈0.90` on N16/N17.

**Lesson for future steps:** Don't combine `Has Exit Time = true`
with a `Trigger` condition in Unity 6.4. Use one or the other:
- For a deterministic exit at a specific time → `Has Exit Time` only.
- For a conditional gate at any time → condition-only.
- For "fire conditionally, but only after a certain point in the
  source clip" → gate the condition-set call in script (which
  is what PlayerCombat does for `SetComboNext`).

