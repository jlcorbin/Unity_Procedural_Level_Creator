# M2-B Step 2 — Animator Behavior Table (Attack & Hit)

**Date:** 2026-04-27
**Scope:** Behavior table for the Animator changes that wire Attack and Hit
states into `PlayerBaseController.controller`. **Reviewed before any
controller asset is touched** (CLAUDE.md "behavior tables before code"
rule).
**Status:** Awaiting user confirmation. No `.controller` /
`.overrideController` asset has been modified by Step 2 yet.

Source clip data: `Assets/Documentation/M2B_01_clip_survey_report.md`.

---

## Section 1: Parameters

`PlayerBaseController` parameters after this step. Existing rows verified
from the live `.controller` YAML.

| Name | Type | Default | Status | Purpose |
|---|---|---|---|---|
| MoveX | Float | 0 | existing | Locomotion blend tree X (strafe) |
| MoveZ | Float | 0 | existing | Locomotion blend tree Z (forward) |
| Speed | Float | 0 | existing | Locomotion magnitude; also drives Sprint state speed |
| IsSprinting | Bool | false | existing | Sprint state gate |
| **Attack** | **Trigger** | — | **NEW** | One-shot — fires Attack state |
| **Hit** | **Trigger** | — | **NEW** | One-shot — fires Hit state |

Total: 4 → 6 parameters.

---

## Section 2: States

`Base Layer` states after this step. Existing rows verified from the live
`.controller` YAML.

| State | Loop | Apply Root Motion | Speed | Speed Parameter | Motion (override slot) | Status |
|---|---|---|---|---|---|---|
| Idle | yes (clip self-loops) | off | 1 | — | `Idle_Battle_SwordAndShiled` | existing — default state |
| Locomotion (2D Simple Directional Blend Tree) | per-clip | off | 1 | — | FWD/BWD/LFT/RGT slots (4 corners) | existing |
| Sprint | yes (clip self-loops) | off | param-bound | `Speed` | `SprintFWD_Battle_InPlace_SwordAndShield` | existing |
| **Attack** | **off (state-level)** | **off** | 1 | — | `Attack01_SwordAndShiled` | **NEW** |
| **Hit** | **off (state-level)** | **off** | 1 | — | `GetHit01_SwordAndShield` | **NEW** |

Total: 3 → 5 states.

### Notes on the new states

- **Loop is off at the state level, not the clip.** The FBX import
  (`loopTime: 1` on both clips, per Step 1 survey) is left untouched.
  Animator state behavior governs whether the clip loops in this state —
  setting `m_WriteDefaultValues: 1` and adding an exit-time transition
  out of the state at <100% causes the state to play once and exit
  before the clip wraps. This is the cleaner approach: keeps FBX
  reimport stable and keeps the import flag noise out of the diff.
- **Apply Root Motion off** means Attack01's `keepOriginalPositionY: 0`
  (the only outlier from the survey) cannot translate the character
  vertically — root motion is suppressed at the state, so the FBX flag
  is irrelevant. No need to "fix" Attack01's import settings.
- **`m_Speed: 1.0f`** for both states. Sprint is the only state with a
  parameter-bound speed in the existing controller; Attack and Hit are
  fixed-rate.
- **`m_WriteDefaultValues: 1`** matches Idle/Locomotion/Sprint —
  consistency reduces parameter "ghost values" between state changes.
- Length reference (from survey, assuming 30 fps):
  Attack ≈ 0.533 s; Hit ≈ 0.467 s. State exit-time percentages below
  are computed against these.

---

## Section 3: Transitions

### Existing (6) — verified from current `.controller` YAML

| # | From | To | Conditions | Has Exit Time | Exit Time | Duration | Interruption |
|---|---|---|---|---|---|---|---|
| 1 | Idle | Locomotion | `Speed > 0.1` | OFF | — | 0.15 | None |
| 2 | Locomotion | Idle | `Speed < 0.1` | OFF | — | 0.15 | None |
| 3 | Locomotion | Sprint | `IsSprinting == true` AND `MoveZ > 0.7` AND `Speed > 0.1` | OFF | — | 0.10 | None |
| 4 | Sprint | Locomotion | `IsSprinting == false` | OFF | — | 0.15 | None |
| 5 | Sprint | Locomotion | `MoveZ < 0.7` | OFF | — | 0.15 | None |
| 6 | Sprint | Locomotion | `Speed < 0.1` | OFF | — | 0.15 | None |

> **Discrepancy from prompt text.** The prompt's existing-transition list
> says "6: Sprint → Idle (Speed < 0.1)". The actual `.controller` YAML
> has Sprint → **Locomotion** for all three Sprint exits — Locomotion
> → Idle (#2) catches the `Speed < 0.1` case the next frame. **No
> action needed; flagging only so Section 5 review is informed.**

### New (5 normal + 1 anyState)

| # | From | To | Conditions | Has Exit Time | Exit Time | Duration | Interruption | CanTransitionToSelf |
|---|---|---|---|---|---|---|---|---|
| N1 | Idle | Attack | `Attack` (If) | OFF | — | 0.10 | None | — |
| N2 | Locomotion | Attack | `Attack` (If) | OFF | — | 0.10 | None | — |
| N3 | Sprint | Attack | `Attack` (If) | OFF | — | 0.10 | None | — |
| N4 | Attack | Idle | (no condition) | **ON** | **0.90** | 0.10 | None | — |
| N5 | **AnyState** | Hit | `Hit` (If) | OFF | — | **0.05** | None | **ON** |
| N6 | Hit | Idle | (no condition) | **ON** | **0.85** | 0.10 | None | — |

Total transitions after this step:
- **State-to-state (`m_Transitions` on individual states):** 6 + 5 = 11
- **AnyState (`m_AnyStateTransitions` on the state machine):** 0 + 1 = 1
- (The Step ④ validation check 5 expects exactly these counts.)

### Per-state transition lists after this step

```
Idle.transitions       = [#1 → Locomotion,
                          N1 → Attack]                       (2 transitions)
Locomotion.transitions = [#2 → Idle,
                          #3 → Sprint,
                          N2 → Attack]                       (3 transitions)
Sprint.transitions     = [#4 → Locomotion,
                          #5 → Locomotion,
                          #6 → Locomotion,
                          N3 → Attack]                       (4 transitions)
Attack.transitions     = [N4 → Idle]                         (1 transition)
Hit.transitions        = [N6 → Idle]                         (1 transition)

stateMachine.anyStateTransitions = [N5 → Hit]                (1 anyState transition)
```

### Trigger-condition mode note

For Trigger params, Unity stores the condition as
`AnimatorConditionMode.If` (enum value 1). Bools use `If` for "true" and
`IfNot` for "false"; Triggers only support `If`. Step ③ wiring code must
use `If` for both Attack and Hit conditions.

---

## Section 4: Why this transition graph

### 4.1 Why Trigger params instead of Bools

Triggers are auto-cleared by the Animator when consumed by a transition.
This makes them ideal for one-shot events ("the player just pressed
attack") where the script's responsibility is to set, not maintain. Bools
require the script to explicitly set false again, which means tracking
input-state vs. animation-state separately and getting them to agree.
For combat events that map 1:1 with input presses, Triggers are simpler
and have fewer failure modes (e.g., a Bool left true would replay the
state on every transition check). The cost is that Triggers can be lost
if no transition fires within one Animator update — but our N1/N2/N3 fan
covers Idle/Locomotion/Sprint, and N5 (Any State → Hit) catches every
state, so the trigger always has a consumer.

### 4.2 Why `Any State → Hit` instead of per-state `→ Hit` transitions

Per-state would require five transitions
(Idle→Hit, Locomotion→Hit, Sprint→Hit, Attack→Hit, Hit→Hit) and stay
synchronized as new states are added. Any State is one transition that
covers every current state and every future state, automatically. The
"hit interrupts attack immediately" decision is satisfied because Any
State includes Attack. The "re-hitting during stagger restarts the
reaction" decision is satisfied via `canTransitionToSelf = true`. Total
graph cost is 1 transition vs. 5+, with monotonically better future
extensibility.

### 4.3 Why no explicit `Attack → Hit` transition

Any State → Hit covers it. Attack is a state; Any State includes it. The
N5 transition fires from Attack the same way it fires from any other
state, including the canTransitionToSelf re-entry from Hit itself.
Adding a redundant explicit transition would clutter the graph and have
no functional effect.

### 4.4 Why no `Attack → Locomotion` direct transition

Attack returns to Idle (N4). If the player is moving when Attack ends,
existing transition #1 (Idle → Locomotion when Speed > 0.1) fires on the
next Animator update. Two transitions chain naturally and the perceived
delay is one frame. Adding a direct Attack → Locomotion transition would
duplicate the Speed > 0.1 condition logic and create a parallel path
that has to stay in sync with #1. Same rationale for not adding
Attack → Sprint — the Sprint entry has stricter conditions
(IsSprinting && MoveZ > 0.7) and going Idle → Locomotion → Sprint over
two updates is correct.

### 4.5 Why no buffered-combo logic in this prompt

Combo extension is prompt 3+ territory. The Attack state has exactly one
exit (N4 → Idle). When prompt 3 adds combo logic, it can layer on by:
- adding an `Attack02` state with override slot,
- adding an `Attack → Attack02` transition with a window-condition
  parameter (e.g., `ComboNext` Bool set during the buffered window),
- adding `Attack02 → Idle` mirroring N4.

The current single-Attack foundation does not lock that path out. The
"buffered window" decision (Option A from prior chat) means the script
buffers a press during the swing and releases the combo trigger at the
window boundary — none of that runtime logic is built yet, but the
Animator is shaped to accept it without rework.

---

## Section 5: Open questions — review before applying

Confirm or override each before Step ② / Step ③ proceed.

1. **Trigger param names.** Currently `Attack` and `Hit`
   (capitalized). These get hashed via `Animator.StringToHash` in
   prompt 3 (alongside `MoveX`, `MoveZ`, `Speed`, `IsSprinting`).
   Alternative is `AttackTrigger` / `HitTrigger` — more verbose but
   self-documenting in inspector. **Recommendation: keep `Attack` /
   `Hit`** (matches existing terse param style; the Trigger type is
   already visible in the inspector).

2. **Attack exit time = 0.90.** At 0.533 s clip length, this means
   Attack runs for ~0.480 s before exit-time fires; the 0.10 s
   transition then blends into Idle, ending at 0.580 s total. Lower
   (0.80) feels snappier (~0.426 s + 0.10 = 0.526 s); higher (0.95)
   feels heavier (~0.506 s + 0.10 = 0.606 s). **Recommendation: 0.90**
   — leaves a clear "follow-through" frame at the end of the swing
   without dragging.

3. **Hit transition duration = 0.05.** Faster (0.02) feels punchier;
   slower (0.10) feels mushier. **Recommendation: 0.05** — snappy
   stagger, but still readable. Note: the 0.05 s blend-IN to Hit can
   look pop-y on a fast attack since the source pose may be mid-swing;
   if visual review later flags this, bump to 0.08.

4. **Hit exit time = 0.85.** At 0.467 s clip length, exit-time fires
   at ~0.397 s; +0.10 transition = 0.497 s total. **Recommendation:
   0.85** — leaves enough recovery to feel the stagger without locking
   the player out for a full half-second. Lower (0.75) for "flinch and
   keep moving" gameplay; higher (0.95) for "stagger as a real
   punishment."

5. **`Can Transition To Self: ON` for Hit.** Current setting allows
   re-hitting during stagger to restart the reaction (chip-damage
   model). **Recommendation: ON.** OFF is the alternative if the
   design wants brief invulnerability frames where a second hit during
   stagger is ignored — that's also a valid design but contradicts
   the "hit interrupts attack immediately" philosophy applied
   reflexively (i.e., if hit interrupts attack, why shouldn't hit
   interrupt hit?). PlayerCombat.cs in prompt 3 can add an iframe
   window separately if desired without changing the Animator graph.

6. **Attack `m_Speed = 1.0f` (no parameter bind).** Sprint is
   parameter-bound to `Speed` — this prompt does NOT extend that
   pattern to Attack/Hit. If the design later wants attack-speed to
   scale with a buff stat, add a `AttackSpeed` Float param and bind
   `m_SpeedParameter` then. Not needed now.

7. **Discrepancy noted in Section 3.** Existing transition #6 in the
   prompt text is labeled `Sprint → Idle`, but the live controller
   has `Sprint → Locomotion`. This table reflects the live state.
   Confirm no action is needed (Sprint → Locomotion → Idle still
   works correctly via existing #2). **Recommendation: leave as-is.**

---

## Stop point

**Per the prompt's instruction:** Do not proceed to Step ② / Step ③
until the user confirms this table.

When confirming, please flag any changes to the open questions above
(or "all defaults are fine") so I know what to wire.

---

**Behavior table path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_02_animator_behavior_table.md`
