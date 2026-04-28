# M2-B Step 3 — PlayerCombat Behavior Table

**Date:** 2026-04-27
**Scope:** Runtime behavior of `PlayerCombat.cs` (combat input → Animator
trigger writes) and the two new public methods on `PlayerAnimator.cs`. The
Animator graph itself is unchanged from Step 2 — this table covers the
script-side state machine that sits in front of it.
**Status:** Awaiting user confirmation. No `.cs`, prefab, or asset has
been modified yet.

Source data:
- `Assets/Documentation/M2B_01_clip_survey_report.md` (Attack01 = 0.533 s,
  GetHit01 = 0.467 s @ 30 fps).
- `Assets/Documentation/M2B_02_animator_behavior_table.md` (Animator
  graph: 6 params, 5 states, 11 + 1 transitions, exit-times 0.90/0.85).

---

## Section 1: Public API surface

### `PlayerAnimator.SetAttackTrigger()`

```csharp
public void SetAttackTrigger();
```

| Field | Value |
|---|---|
| Caller | `PlayerCombat.OnAttackPressed`, `PlayerCombat.Update` (buffer consume) |
| Effect | `_animator.SetTrigger(_hashAttack)` |
| Preconditions | `_ready == true` (Awake completed and Animator resolved) |
| Postconditions | Trigger queued on Animator. The Animator will fire N1/N2/N3 on next update if currently in Idle/Locomotion/Sprint, or be auto-cleared if no transition consumes it within one Animator update. |
| Idempotency | Safe to call repeatedly; Unity deduplicates within an update. Calling before Awake is a silent no-op (matches existing `SetMove` / `SetSprinting` pattern). |

### `PlayerAnimator.SetHitTrigger()`

```csharp
public void SetHitTrigger();
```

| Field | Value |
|---|---|
| Caller | `PlayerCombat.TakeHit` |
| Effect | `_animator.SetTrigger(_hashHit)` |
| Preconditions | `_ready == true` |
| Postconditions | Trigger queued on Animator. N5 (Any State → Hit) fires on next update, interrupting whatever state is currently active. `canTransitionToSelf=true` allows re-entry from Hit. |
| Idempotency | Same safety semantics as `SetAttackTrigger`. |

### `PlayerCombat.TakeHit()`

```csharp
[ContextMenu("Take Hit")]
public void TakeHit();
```

| Field | Value |
|---|---|
| Caller | External damage source (future enemy code). For M2-B Step 3 testing: Inspector context-menu on the PlayerCombat component. |
| Effect | Forwards to `_animator.SetHitTrigger()`. Clears any buffered attack press (`_attackBuffered = false`). |
| Preconditions | `_animator` resolved. |
| Postconditions | Hit reaction plays. Currently active state (including Attack) is interrupted. Any pending combo input is dropped. |
| Parameters | None — minimal viable API. Direction, damage, source attacker etc. are deferred until the damage system lands. |

### `PlayerCombat.OnAttackPressed()` *(private)*

```csharp
private void OnAttackPressed();
```

| Field | Value |
|---|---|
| Caller | `PlayerInputReader.AttackPressed` event (subscribed in `OnEnable`, unsubscribed in `OnDisable`). |
| Effect | Either fires Attack trigger immediately, sets `_attackBuffered = true`, or drops the input — depending on current Animator state (see Section 2). |
| Preconditions | `_animatorComponent` resolved (private cache of `_animator.Animator`). |
| Postconditions | Attack will play either now or at the buffer-consume threshold (`bufferConsumeAt = 0.85`). |
| Visibility | Private — exposed to `PlayerInputReader` exclusively via the C# event subscription, not via UnityEvent inspector wiring. |

---

## Section 2: Combat state machine

`PlayerCombat` tracks one piece of state beyond what the Animator owns:

```csharp
private bool _attackBuffered;
```

True iff an attack press has been registered during the current swing's
combo window and is waiting to fire when the swing reaches the consume
threshold. Cleared when consumed, or when interrupted by a Hit, or when
`OnAttackPressed` fires from a non-Attack state (since the immediate fire
makes the buffer redundant).

### State table — what `OnAttackPressed` does, by current Animator state

| Animator state (current) | Normalized time `n` | Action | `_attackBuffered` after |
|---|---|---|---|
| Idle / Locomotion / Sprint | n/a | Fire Attack trigger immediately | `false` (cleared) |
| Attack | `0 ≤ n < ComboWindowOpen` (0–0.40) | Drop input (too early) | unchanged |
| Attack | `ComboWindowOpen ≤ n < ComboWindowClose` (0.40–0.80) | Set buffer flag | `true` |
| Attack | `ComboWindowClose ≤ n` (0.80–1.0) | Drop input (too late, recovery frames) | unchanged |
| Hit | n/a | Drop input (no canceling out of stagger) | unchanged |
| Currently in transition | n/a | Drop input (ambiguous source state — wait one frame) | unchanged |

Where `n = info.normalizedTime % 1.0f` (modulo to handle the Animator's
post-completion behavior of letting normalizedTime accumulate past 1.0).

Constants — named, not magic:

```csharp
private const float ComboWindowOpen        = 0.40f;
private const float ComboWindowClose       = 0.80f;
private const float BufferConsumeThreshold = 0.85f;
```

The Attack→Idle transition's exit-time is 0.90 (per Step 2). The buffer
consume threshold is set 0.05 lower so the re-fire happens slightly
before the auto-exit, ensuring the new Attack trigger is queued while
the Animator is still inside the Attack state and N1/N2/N3 are still
the candidate transitions when it returns to Idle/Locomotion/Sprint.

---

## Section 3: Update loop in `PlayerCombat`

Per-frame logic, in pseudocode:

```
Update():
    1. If !_attackBuffered, return early (no work to do).
    2. Read Animator current state info on layer 0.
    3. If currently in transition, return (wait for stable state).
    4. If state.shortNameHash != AttackStateHash, return.
       (Buffer is only consumable while still inside Attack. If we've
        already exited to Idle, the consume window has been missed —
        but we still keep _attackBuffered so the next OnAttackPressed
        can re-evaluate. In practice the consume threshold (0.85) fires
        before exit-time (0.90), so this branch is rare.)
    5. Compute n = state.normalizedTime % 1.0f.
    6. If n >= BufferConsumeThreshold (0.85):
         _animator.SetAttackTrigger();
         _attackBuffered = false;
```

Implementation note — the buffer mechanism does **not** need to track
state changes via `OnStateExit`. Polling `GetCurrentAnimatorStateInfo`
each frame is sufficient given the simple Attack→Idle exit topology and
the small combo-length-of-1. If the design later adds Attack02/Attack03
the polling approach extends naturally — the buffer becomes "next combo
queued" and the consume target depends on which Attack state is current.

---

## Section 4: Why this design

### 4.1 Why polling instead of animation events

Animation events embed timing data inside the FBX clip — invisible to
the script reader, silent on misnames, lost when clips are swapped via
override controller. Polling keeps the timing constants in
`PlayerCombat.cs` where they're inspector-tunable, version-controllable,
and discoverable via grep. The cost is one Update call per frame doing
~3 hash compares; negligible. Bargain confirmed by prior chat
discussion (Option A vs B).

### 4.2 Why the buffer is a `bool`, not a counter

Combo length is 1 in M2-B Step 3. There is no "queue of three pending
attacks" scenario to model. When Attack02+ are added in a later
milestone, the buffer flag becomes "next attack in combo is queued" and
the combo length is naturally tracked by which Animator state is
currently active (Attack, Attack02, Attack03). A counter would imply an
unbounded combo, which the designed-in maximum (likely 3–4 swings)
already disallows.

### 4.3 Why drop input outside the combo window instead of always buffering

Always-buffering causes "I pressed attack two seconds ago and forgot,
and now my character swings unexpectedly during a careful traversal" —
classic poor combat feel. Window-gated buffering only honors recent
intent (during the swing's middle) and discards stale input (early or
late presses). The 0.40–0.80 window at 0.533 s clip length is 0.213 s
to 0.426 s of acceptance per swing — about 13 frames at 60 Hz — which
is generous enough that intent is registered without feeling laggy.

### 4.4 Why Hit doesn't need a script-side cancel

The Animator does it via N5 (Any State → Hit). PlayerCombat doesn't
need to "cancel the attack" — calling `SetHitTrigger()` queues the
trigger, the Animator's next update fires N5, and the current state
(Attack or otherwise) is left behind. PlayerCombat's only Hit-side
responsibility is clearing `_attackBuffered` so a buffered press
doesn't leak into the post-stagger Idle state.

### 4.5 Why `TakeHit()` is parameterless

Parameter design without callers is speculative architecture. There
is no enemy code, no health system, no damage values yet. When those
land, `TakeHit()` will gain parameters (direction, damage, source) and
forward them to `PlayerHealth.ApplyDamage()` — but designing the
signature without those callers means making up requirements. M2-B is
the *animation* milestone; getting the Hit state to play on demand is
the only deliverable here. Adding `TakeHit(Vector3 dir, float dmg,
GameObject src)` now would create stubs that the future damage system
might reject and rewrite anyway.

### 4.6 Why a private C# event subscription, not an Inspector UnityEvent

Three reasons:
1. **Compile-time validation.** A C# event subscription fails at
   compile time if the method name changes; an Inspector UnityEvent
   silently breaks at runtime.
2. **No prefab inspector setup required.** The PlayerCombat component
   "just works" when added to the player prefab — there's no
   "remember to wire OnAttack to PlayerCombat.OnAttackPressed in the
   Inspector" footgun.
3. **Visible in code review.** A `_input.AttackPressed += OnAttackPressed`
   line shows up in diffs; a UnityEvent change is YAML noise that's
   hard to review.

The M1 input wiring uses UnityEvents on `PlayerInputReader` (PlayerInput
component → reader's OnMove/OnAttack/etc.). PlayerCombat sits one layer
inward from that — it consumes the reader's processed events, not the
raw InputSystem callbacks. Code wiring at this boundary is appropriate.

---

## Section 5: Open questions — review before code

Confirm or override each before Step ② proceeds.

1. **`ComboWindowOpen` / `ComboWindowClose` / `BufferConsumeThreshold`
   defaults: 0.40 / 0.80 / 0.85.** At Attack01's 0.533 s length, that's
   a buffer-acceptance window from 0.213 s to 0.426 s into the swing
   (about 13 frames at 60 Hz). Buffer consume fires at 0.453 s. Tighter
   (0.50–0.75) feels punchier; wider (0.30–0.85) feels more forgiving.
   **Recommendation: 0.40 / 0.80 / 0.85** — middle ground. Inspector
   sliders are exposed so tuning doesn't require recompile.

2. **Input-wiring approach.** Three options were considered:
   - **(a)** UnityEvent in Inspector — standard Unity pattern, but
     no compile-time validation and easy to forget to wire.
   - **(b)** PlayerCombat subscribes to a public C# event on
     PlayerInputReader (added this step). Compile-time validation,
     no Inspector wiring.
   - **(c)** PlayerCombat polls a `PlayerInputReader.AttackPressedThisFrame`
     property in its Update. Edge-tracking moves into PlayerInputReader.
   - **Recommendation: (b)** — code-only wiring, validated at compile
     time, no Inspector setup. PlayerInputReader gets one new public
     event; OnAttack raises it on `ctx.performed`.

3. **Debug `TakeHit()` trigger for testing.** The prompt's three
   options:
   - **(α)** Inspector context-menu via `[ContextMenu("Take Hit")]`.
     Right-click the PlayerCombat component header → "Take Hit"
     during Play.
   - **(β)** Debug keybind ("H" key) via Input System action.
   - **(γ)** Both.
   - **Recommendation: (α)** — minimal surface, removable. Adding
     an "H" keybind requires editing `InputSystem_Actions.inputactions`
     which is heavy for a temporary hook.

4. **Should `OnAttackPressed` ignore input while the player is in the
   Hit state?** Section 2 says yes — drop input during stagger. This
   means a player buffering "attack out of the hit reaction" gets
   nothing; they have to wait for Hit→Idle to complete and press again.
   **Recommendation: yes** (drop input during Hit). The alternative
   (buffer presses during Hit and consume them when entering Idle)
   would require additional state tracking and the chip-damage
   philosophy of the Animator graph (canTransitionToSelf=true on Hit)
   already says "stagger is real punishment, not cancellable."

5. **`TakeHit()` rate limiting / iframes.** None in this step — the
   Animator's `canTransitionToSelf=true` on N5 means consecutive
   `TakeHit()` calls restart the Hit reaction, which is the
   chip-damage model. iframes are a damage-system concern (skip
   damage for N seconds after a hit), not an animation concern.
   **Recommendation: defer.**

6. **Buffer consume when current state is Hit-during-Attack.** Edge
   case: player is in Attack mid-swing, buffer is set (at n=0.5),
   then takes a hit. Hit trigger fires, Animator transitions
   Attack→Hit. The buffer flag `_attackBuffered` is *not* cleared
   automatically by N5's transition. The Update loop will see state
   = Hit, fail the `state.shortNameHash != AttackStateHash` check,
   and return without consuming. When Hit→Idle fires (N6), state =
   Idle, and the Update loop's check #4 (state must equal Attack)
   keeps blocking the consume — **good, the buffered press should
   not leak into post-stagger play**.
   - However, the next `OnAttackPressed` call (player presses again
     post-stagger) would see state=Idle and fire immediately, ignoring
     the orphaned `_attackBuffered=true`. The orphan is harmless but
     should be cleared explicitly on entering Hit, for hygiene.
   - **Recommendation: clear `_attackBuffered` in `TakeHit()`** (the
     scripted entry point for Hit). Future damage-system code will
     also call `TakeHit()`, so the cleanup happens consistently.
     This is reflected in Section 1 Postconditions for `TakeHit`.

---

## Stop point

**Per the prompt's Step ① instruction:** Do not write `PlayerCombat.cs`,
modify `PlayerAnimator.cs` / `PlayerInputReader.cs`, or touch the
prefab until the user confirms this table.

When confirming, please flag any changes to the open questions above
(or "all defaults are fine") so I know what to wire.

---

**Behavior table path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_03_combat_behavior_table.md`
