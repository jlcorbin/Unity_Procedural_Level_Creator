# M2-B Step 4 — Jump Animator Behavior Table

**Date:** 2026-04-27
**Scope:** Behavior table for the Animator changes that wire the
three-state Jump arc (`JumpStart` → `JumpAir` → `JumpEnd`) into
`PlayerBaseController.controller`. **Reviewed before any controller
asset is touched** (CLAUDE.md "behavior tables before code" rule).
**Status:** Awaiting user confirmation. No `.controller` /
`.overrideController` asset has been modified by Step 4 yet.

Source clip data: `Assets/Documentation/M2B_04_jump_clip_survey.md`.
Prior Animator state: `Assets/Documentation/M2B_02_animator_behavior_table.md`
(6 params, 5 states, 11 + 1 transitions — Attack/Hit complete).

---

## Section 1: Parameters (delta from Step 2)

`PlayerBaseController` parameters after this step. Existing rows
verified from the live `.controller` YAML.

| Name | Type | Default | Status | Purpose |
|---|---|---|---|---|
| MoveX | Float | 0 | existing | Locomotion blend tree X (strafe) |
| MoveZ | Float | 0 | existing | Locomotion blend tree Z (forward) |
| Speed | Float | 0 | existing | Locomotion magnitude; also drives Sprint state speed |
| IsSprinting | Bool | false | existing | Sprint state gate |
| Attack | Trigger | — | existing (Step 2) | Fires Attack state |
| Hit | Trigger | — | existing (Step 2) | Fires Hit state |
| **Jump** | **Trigger** | — | **NEW** | Fires JumpStart state |
| **IsGrounded** | **Bool** | **true** | **NEW** | Drives JumpStart→Air and Air→JumpEnd transitions |

Total: 6 → 8 parameters.

### Notes on the new params

- **`IsGrounded` (Bool, default true) over `IsJumping` (Bool):**
  IsGrounded reflects the physical world — the script reads
  `CharacterController.isGrounded` each frame and writes it. IsJumping
  would be a derived state the script tracks separately, doubling the
  state-of-truth. Animator transitions on IsGrounded compose better
  with future ground-related logic (fall-from-ledge, knockback into
  air, etc.) — none of those need a Jump-specific flag.
- **Default `IsGrounded = true`:** Player starts on the ground in any
  test scene. The first frame after Awake, PlayerController will
  re-confirm it.
- **`Jump` (Trigger):** Same rationale as Attack/Hit — jump-press is
  a one-shot event. The Animator consumes the trigger when N7/N8/N9
  fires. Triggers also avoid the "Bool stays true → state replays"
  failure mode.

---

## Section 2: States (delta from Step 2)

`Base Layer` states after this step.

| State | Loop | Apply Root Motion | Speed | Speed Parameter | Motion (override slot) | Status |
|---|---|---|---|---|---|---|
| Idle | yes (clip self-loops) | off | 1 | — | `Idle_Battle_SwordAndShiled` | existing |
| Locomotion (2D Simple Directional Blend Tree) | per-clip | off | 1 | — | FWD/BWD/LFT/RGT slots | existing |
| Sprint | yes (clip self-loops) | off | param-bound | `Speed` | `SprintFWD_Battle_InPlace_SwordAndShield` | existing |
| Attack | off (state) | off | 1 | — | `Attack01_SwordAndShiled` | existing (Step 2) |
| Hit | off (state) | off | 1 | — | `GetHit01_SwordAndShield` | existing (Step 2) |
| **JumpStart** | **off (state)** | **off** | **1** | — | **`JumpStart_Normal_InPlace_SwordAndShield`** | **NEW** |
| **JumpAir** | **on (state, loops)** | **off** | **1** | — | **`JumpAir_Normal_InPlace_SwordAndShield`** | **NEW** |
| **JumpEnd** | **off (state)** | **off** | **1** | — | **`JumpEnd_Normal_InPlace_SwordAndShield`** | **NEW** |

Total: 5 → 8 states.

### Notes on the new states

- **Apply Root Motion off** on all three. Vertical motion is driven by
  script (CharacterController.Move + gravity in Step 5). Even though
  the InPlace clips have all three root-motion flags locked at the FBX
  level (per Step 4 survey), state-level off is belt-and-suspenders:
  ensures no future "I forgot to lock the FBX" bug leaks Y into the
  transform.
- **JumpStart loop = off (state).** The clip itself has `loopTime: 0`
  in the FBX. Either source-of-truth alone is sufficient; both is
  consistent with how Attack/Hit are wired.
- **JumpAir loop = on (state).** This is the one state in the
  controller where state-level loop is needed — the Air state must
  continue cycling while the player is airborne. The FBX has
  `loopTime: 1` already (per Step 4 survey), so state-level is
  technically redundant — but for a state whose entire purpose is to
  loop, both belts and suspenders are warranted.
- **JumpEnd loop = off (state).** Clip has `loopTime: 0` in the FBX.
  Exit-time on N13 fires before the natural clip end anyway.
- **`m_Speed: 1.0f`** for all three. None of the jump states are
  speed-parameter-bound (Sprint is the only state with that wiring).
- **`m_WriteDefaultValues: 1`** matches Idle/Locomotion/Sprint/Attack/
  Hit — consistency reduces parameter ghost-values between state
  changes.
- Length reference (from Step 4 survey, 30 fps):
  JumpStart ≈ 0.267 s; JumpAir ≈ 0.500 s (looping); JumpEnd ≈ 0.400 s.
  N11 / N13 exit-time percentages below are computed against these.

---

## Section 3: Transitions (delta)

Existing 11 + 1 transitions stay untouched. Adds 7 new state-to-state
transitions (no new AnyState transitions). Naming continues from Step 2
(N6 was the last) — these are N7 through N13.

| # | From | To | Conditions | Has Exit Time | Exit Time | Duration | Interruption | CanTransitionToSelf |
|---|---|---|---|---|---|---|---|---|
| N7  | Idle       | JumpStart | `Jump` (If) **AND** `IsGrounded == true` (If)  | OFF | — | 0.05 | None | — |
| N8  | Locomotion | JumpStart | `Jump` (If) **AND** `IsGrounded == true` (If)  | OFF | — | 0.05 | None | — |
| N9  | Sprint     | JumpStart | `Jump` (If) **AND** `IsGrounded == true` (If)  | OFF | — | 0.05 | None | — |
| N10 | JumpStart  | JumpAir   | `IsGrounded == false` (IfNot)                  | OFF | — | 0.10 | None | — |
| N11 | JumpStart  | JumpAir   | (no condition — fallback)                      | **ON** | **0.95** | 0.10 | None | — |
| N12 | JumpAir    | JumpEnd   | `IsGrounded == true` (If)                      | OFF | — | 0.05 | None | — |
| N13 | JumpEnd    | Idle      | (no condition)                                 | **ON** | **0.85** | 0.10 | None | — |

Total transitions after this step:
- **State-to-state (`m_Transitions` on individual states):** 11 + 7 = **18**
- **AnyState (`m_AnyStateTransitions` on the state machine):** 1 + 0 = **1**
- (Step ⑤ validation expects exactly these counts.)

### Per-state transition lists after this step

```
Idle.transitions       = [#1 → Locomotion,
                          N1 → Attack,
                          N7 → JumpStart]                    (3 transitions)
Locomotion.transitions = [#2 → Idle,
                          #3 → Sprint,
                          N2 → Attack,
                          N8 → JumpStart]                    (4 transitions)
Sprint.transitions     = [#4 → Locomotion,
                          #5 → Locomotion,
                          #6 → Locomotion,
                          N3 → Attack,
                          N9 → JumpStart]                    (5 transitions)
Attack.transitions     = [N4 → Idle]                         (1 transition)
Hit.transitions        = [N6 → Idle]                         (1 transition)
JumpStart.transitions  = [N10 → JumpAir,
                          N11 → JumpAir (fallback)]          (2 transitions)
JumpAir.transitions    = [N12 → JumpEnd]                     (1 transition)
JumpEnd.transitions    = [N13 → Idle]                        (1 transition)

stateMachine.anyStateTransitions = [N5 → Hit]                (1 anyState transition)
```

State-to-state total: 3 + 4 + 5 + 1 + 1 + 2 + 1 + 1 = **18** ✓
AnyState total: **1** ✓

### Trigger / Bool / IfNot mode reference

- N7/N8/N9 — **two conditions per transition.** Both use
  `AnimatorConditionMode.If`:
  - `Jump` (Trigger): mode `If` (Triggers only support `If`).
  - `IsGrounded` (Bool, gate is "true"): mode `If`.
- N10 — one condition, `IsGrounded == false`: mode
  `AnimatorConditionMode.IfNot` (Bool gate "false").
- N11 — no conditions; only `hasExitTime: true, exitTime: 0.95`.
- N12 — one condition, `IsGrounded == true`: mode `If`.
- N13 — no conditions; only `hasExitTime: true, exitTime: 0.85`.

---

## Section 4: Why this transition graph

### 4.1 Why `IsGrounded == true` on N7/N8/N9 (jump-start gate)

Jump is blocked unless grounded. Without the second condition, pressing
Jump while airborne would re-trigger JumpStart. The script in Step 5
will also gate this on the input side (don't fire SetJumpTrigger while
airborne) — but Animator-side gating is belt-and-suspenders: if the
script ever forgets to check, the Animator still does.

### 4.2 Why N7/N8/N9 fan from only Idle/Locomotion/Sprint (not Attack or Hit)

Locked decision from prior chat: **jump is blocked during Attack/Hit.**
Attack-state and Hit-state have no outgoing Jump transition. The
Animator graph structurally enforces the design — even if the script's
input handler accidentally fires SetJumpTrigger during an attack, no
transition will consume it, and the trigger naturally clears on the
next Animator update. (Triggers don't accumulate; an unconsumed
trigger that doesn't fire any transition just gets discarded.)

### 4.3 Why N10 fires on `!IsGrounded`

The takeoff phase ends when the player physically leaves the ground.
The Step 5 script will write `IsGrounded = false` after the
jump-velocity impulse propagates through `CharacterController.Move` on
the first post-jump frame. As soon as that flag flips, the
JumpStart→JumpAir transition fires.

### 4.4 Why N11 (fallback exit-time on JumpStart at 0.95)

If for any reason the script doesn't flip `IsGrounded` to false during
the JumpStart clip — e.g., a very low jump that lands before the start
clip ends, or a glitch where `CharacterController.isGrounded` returns
true through the takeoff frames — the state must still exit. Fallback
to JumpAir on `exitTime = 0.95` (i.e., 95% through the 0.267 s clip ≈
0.254 s in) catches this. JumpAir then immediately transitions to
JumpEnd via N12 (since `IsGrounded` is still true) and the state
machine recovers within ~one frame.

### 4.5 Why N10 + N11 ordering matters

When two transitions on the same state both qualify, Unity uses the
order in `m_Transitions`. N10 (conditional, fires on `!IsGrounded`)
must be ordered **before** N11 (unconditional fallback) so the
"airborne" path wins when the script has correctly flipped the flag.
The wiring code in Step 4 must add N10 first, then N11.

### 4.6 Why N12 fires on `IsGrounded == true`

The Air state ends when the player lands. Script's per-frame
IsGrounded write (`anim.SetBool(IsGrounded, _cc.isGrounded)`) handles
this. No exit-time fallback is added on JumpAir — if for some reason
the player is genuinely "stuck airborne forever" the design wants the
Animator to remain in JumpAir, not auto-land into JumpEnd.

### 4.7 Why no `Jump → Attack` or `Attack → Jump` direct transition

Locked design: jump is blocked during Attack/Hit, and no canceling
into a jump from mid-attack. Step 5 will route the AttackPressed event
during JumpStart/JumpAir/JumpEnd as **drop the input** — same handling
as during Hit. Adding direct transitions would contradict this.

### 4.8 Why no explicit `Hit cancels Jump` transition

N5 (AnyState → Hit) from Step 2 already covers it. AnyState includes
JumpStart, JumpAir, and JumpEnd. The N5 transition fires from any of
the three jump states the same way it fires from Idle/Locomotion/
Sprint/Attack.

**Consequence to flag for Step 5:** A hit while airborne plays the
GetHit01 stagger in mid-air with no fall-through animation back to
ground. That's a known limitation of the single Generic Hit clip; the
Step 5 script will need to **continue applying gravity even during
Hit**, so the player still falls. Hit→Idle (N6) then resolves on the
next ground frame. This is a script concern (PlayerController gravity
gating), not an Animator concern.

### 4.9 Why JumpEnd → Idle (N13), not direct to Locomotion

Same reasoning as N4 (Attack → Idle): JumpEnd returns to Idle, and if
the player is moving when JumpEnd ends, the existing transition #1
(Idle → Locomotion when Speed > 0.1) fires on the next Animator
update. Two transitions chain naturally and the perceived delay is
one frame. Adding a direct JumpEnd → Locomotion path would duplicate
the Speed > 0.1 condition logic and create a parallel path that has
to stay in sync with #1.

---

## Section 5: Open questions — review before applying

Confirm or override each before Step ③ proceeds.

1. **Trigger param name `Jump` (capitalized).** Matches the existing
   terse style (`Attack`, `Hit`). Alternative is `JumpTrigger` —
   self-documenting in inspector but verbose. **Recommendation: keep
   `Jump`.**

2. **Bool param name `IsGrounded` (capital-I prefix).** Matches the
   existing `IsSprinting` style. Alternative `Grounded` (no prefix)
   reads slightly cleaner but breaks the convention that Bools start
   with `Is`. **Recommendation: keep `IsGrounded`.**

3. **N7/N8/N9 transition duration = 0.05.** Same as N5
   (Any State → Hit). Snappy — jump press should feel instant.
   Alternative 0.02 is even snappier; 0.10 (Attack/Locomotion default)
   feels mushy on a jump press. **Recommendation: 0.05.**

4. **N10 transition duration = 0.10.** Slightly longer than N7-9
   because the takeoff-to-airborne blend is visually larger (legs
   come off the ground, body goes from windup pose to mid-air pose).
   **Recommendation: 0.10.**

5. **N11 fallback exit-time = 0.95.** At 0.267 s clip length, fires
   at ~0.253 s. Should rarely fire in practice; just a safety net for
   the "script didn't flip IsGrounded" edge case.
   **Recommendation: 0.95.**

6. **N12 transition duration = 0.05.** Landing should be visually
   sharp; 0.05 keeps the foot-plant tight without popping the pose.
   **Recommendation: 0.05.**

7. **N13 exit time = 0.85.** Same as Hit→Idle. At 0.400 s clip length,
   fires at ~0.340 s; +0.10 transition = ~0.440 s total recovery
   window before locomotion can resume. Lower (0.75) for "land and
   keep moving"; higher (0.95) for "feel the landing harder."
   **Recommendation: 0.85.**

8. **JumpAir state-level loop = on (in addition to FBX `loopTime: 1`).**
   The FBX flag alone is sufficient for the clip to loop in this state,
   but setting the state-level loop too is consistent with how other
   states are configured (defensive symmetry). **Recommendation: set
   both.** No-op if anything ever changes the FBX flag.

9. **Apply Root Motion off** on all three jump states. Even though all
   three clips have all-axes-locked root motion at the FBX level, the
   Step 5 script will be the sole driver of vertical position via
   CharacterController. **Recommendation: keep state-level off.**

10. **N12 has no fallback exit-time.** If the player gets stuck
    "airborne forever" (e.g., script bug, clipping geometry, or
    deliberate hover state), the Animator stays in JumpAir indefinitely
    and the JumpAir clip just keeps looping. This is the correct
    behavior — the script is the source of truth for ground state, not
    a timer. Adding a fallback would mask script bugs.
    **Recommendation: no fallback.**

---

## Stop point

**Per the prompt's instruction:** Do not proceed to Step ③ (override
controller slots), Step ④ (controller modifications), Step ⑤
(validation), or Step ⑥ (CLAUDE.md update) until the user confirms this
table.

When confirming, please flag any changes to the open questions above
(or "all defaults are fine") so I know what to wire.

---

**Behavior table path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_04_jump_animator_behavior_table.md`
