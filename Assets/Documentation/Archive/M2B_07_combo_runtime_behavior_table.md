# M2-B Step 7 — Combo Runtime Behavior Table

**Date:** 2026-04-29
**Scope:** Runtime wiring for the 3-hit combo. Adds `SetComboNext()`
to PlayerAnimator, swaps the buffer-consume call in PlayerCombat
from `SetAttackTrigger` to `SetComboNext`, extends the
PlayerCombat state-gate to recognize Attack02, and adds an explicit
combo-cap drop for Attack03.
**Status:** Ready for review. Stop point per the prompt — do not
proceed past this table without user confirmation.

**Source data verified:**
- `PlayerAnimator.cs`: 6 public methods (`SetMove`, `SetSprinting`,
  `SetAttackTrigger`, `SetHitTrigger`, `SetJumpTrigger`,
  `SetGrounded`); 8 cached parameter hashes; `_ready` flag gates all
  writes pre-Awake.
- `PlayerCombat.cs`: 2 cached state hashes (`AttackStateHash`,
  `HitStateHash`); buffer-consume site at line 114
  (`_animator.SetAttackTrigger()`); single-attack handler at line
  147 (also `SetAttackTrigger`); `IsActionLocked` property reads
  current + next state; lazy `AnimatorComponent` property mirrors
  PlayerController's pattern.
- Animator graph (Step 6): Attack → Attack02 (N14, ComboNext If,
  exit 0.85) listed before Attack → Idle (N4, exit 0.90); same
  shape for Attack02 → Attack03 (N15) before Attack02 → Idle
  (N16); Attack03 → Idle (N17) is the single outgoing.

---

## Section 0 — Pseudocode-vs-actual reconciliation

The Step 7 prompt uses pseudocode names that differ slightly from
the live code. Mapping follows; Step ③ uses the actual names.

| Prompt name | Actual name | Where |
|---|---|---|
| `_animatorComponent` | `AnimatorComponent` (property) | PlayerCombat.cs:56 |
| `_animatorComponent` (local) | `anim` (local var inside method) | PlayerCombat.cs:104, 129 |
| `normTime` | `n` | PlayerCombat.cs:111, 153 |
| `_animator.SetComboNext()` | `_animator.SetComboNext()` | unchanged — `_animator` is the PlayerAnimator field, not the Animator component |

The structural shape of Update / OnAttackPressed differs from the
prompt's pseudocode in two ways:

1. **`anim.IsInTransition(0)` is a top-level gate** in the live
   `OnAttackPressed` (line 133), checked BEFORE the hash check. The
   prompt's pseudocode buries it inside the `inActiveAttack` boolean.
   Step ③ keeps the live code's structure (top-level gate stays).
2. **Order of returns in Update** is `_attackBuffered` early-out →
   `anim` null check → IsInTransition gate → hash gate → window
   threshold. Step ③ extends the hash gate, no other reorder.

---

## Section 1 — Public API surface (delta)

### `PlayerAnimator.SetComboNext()` (NEW)

```csharp
public void SetComboNext();
```

| Field | Value |
|---|---|
| Caller | `PlayerCombat.Update` (buffer-consume path only). NEVER from `OnAttackPressed`, NEVER from `TakeHit`. |
| Effect | `_animator.SetTrigger(_hashComboNext)` when `_ready == true`. Silent no-op pre-Awake (consistent with `SetAttackTrigger` / `SetHitTrigger` / `SetJumpTrigger`). |
| Preconditions | `_ready == true`. Caller has gated on `info.shortNameHash == AttackStateHash || == Attack02StateHash` (i.e., we're inside a combo-eligible state). |
| Postconditions | ComboNext trigger queued. Animator consumes via N14 (Attack → Attack02) or N15 (Attack02 → Attack03) on the next exit-time evaluation if the state's normalizedTime ≥ 0.85. Auto-clears within one Animator update if not consumed. |
| Idempotency | Same as other trigger setters: setting twice in one frame is a no-op (Animator deduplicates). |

**No other PlayerAnimator changes.** Existing 6 public methods, 8
hash fields, `_ready` flag — all unchanged.

### Constants added to PlayerAnimator

```csharp
private const string ParamComboNext = "ComboNext";
private int _hashComboNext;
```

Hashed in `Awake()` after the existing 8 assignments.

### Constants added to PlayerCombat

```csharp
private static readonly int Attack02StateHash = Animator.StringToHash("Attack02");
private static readonly int Attack03StateHash = Animator.StringToHash("Attack03");
```

Sit alongside existing `AttackStateHash` and `HitStateHash` (lines
49-50). Static readonly — initialized once at class-load, no
per-instance cost.

---

## Section 2 — PlayerCombat.Update change

### Before (Step 3 / current)

```csharp
private void Update()
{
    if (!_attackBuffered) return;
    var anim = AnimatorComponent;
    if (anim == null) return;
    if (anim.IsInTransition(0)) return;

    var info = anim.GetCurrentAnimatorStateInfo(0);
    if (info.shortNameHash != AttackStateHash) return;

    float n = info.normalizedTime % 1.0f;
    if (n >= bufferConsumeAt)
    {
        _animator.SetAttackTrigger();   // ← buffer-consume
        _attackBuffered = false;
    }
}
```

### After (Step 7)

```csharp
private void Update()
{
    if (!_attackBuffered) return;
    var anim = AnimatorComponent;
    if (anim == null) return;
    if (anim.IsInTransition(0)) return;

    var info = anim.GetCurrentAnimatorStateInfo(0);
    int hash = info.shortNameHash;
    if (hash != AttackStateHash && hash != Attack02StateHash) return;

    float n = info.normalizedTime % 1.0f;
    if (n >= bufferConsumeAt)
    {
        _animator.SetComboNext();        // ← was SetAttackTrigger
        _attackBuffered = false;
    }
}
```

**Two substantive changes:**

1. State gate extended: `hash != AttackStateHash && hash != Attack02StateHash`.
   Attack03 falls through to early-return (combo cap; nothing to consume into).
2. Buffer-consume call: `SetAttackTrigger()` → `SetComboNext()`.

`_attackBuffered = false` placement and timing are unchanged — the
flag clears on consume regardless of whether the trigger ultimately
fires a transition (Animator's responsibility).

---

## Section 3 — PlayerCombat.OnAttackPressed change

### Before (Step 3 / current, lines 127-157)

```csharp
private void OnAttackPressed()
{
    var anim = AnimatorComponent;
    if (anim == null) return;
    if (anim.IsInTransition(0)) return;

    var info = anim.GetCurrentAnimatorStateInfo(0);
    int hash = info.shortNameHash;

    if (hash == HitStateHash) return;

    if (hash != AttackStateHash)
    {
        _animator.SetAttackTrigger();
        _attackBuffered = false;
        return;
    }

    float n = info.normalizedTime % 1.0f;
    if (n >= comboWindowOpen && n < comboWindowClose)
        _attackBuffered = true;
}
```

### After (Step 7)

```csharp
private void OnAttackPressed()
{
    var anim = AnimatorComponent;
    if (anim == null) return;
    if (anim.IsInTransition(0)) return;

    var info = anim.GetCurrentAnimatorStateInfo(0);
    int hash = info.shortNameHash;

    if (hash == HitStateHash) return;

    // Combo cap — Attack03 has no further chain. Drop deliberately
    // rather than relying on the Animator graph having no outgoing
    // Attack-trigger transition from Attack03 (which is true today
    // but is not an invariant we should depend on).
    if (hash == Attack03StateHash) return;

    bool inActiveAttack = (hash == AttackStateHash || hash == Attack02StateHash);

    if (!inActiveAttack)
    {
        // Idle / Locomotion / Sprint / JumpStart / JumpAir / JumpEnd
        // — fire immediately. (Jump and Idle/Loco/Sprint cases routing
        // is shaped by the Animator graph; Idle/Loco/Sprint have N1/N2/N3
        // → Attack, the jump states have no Attack-trigger transition,
        // so the trigger silently auto-clears in the airborne case.)
        _animator.SetAttackTrigger();
        _attackBuffered = false;
        return;
    }

    // In Attack01 or Attack02 — buffer if the press is inside the window.
    float n = info.normalizedTime % 1.0f;
    if (n >= comboWindowOpen && n < comboWindowClose)
        _attackBuffered = true;
    // else: too early or too late — drop input.
}
```

**Three substantive changes:**

1. New explicit `Attack03StateHash` early-return after the Hit check.
2. The `(hash != AttackStateHash)` branch becomes `!inActiveAttack`
   where `inActiveAttack = hash == Attack || hash == Attack02`.
3. The window-buffer logic at the end now applies to Attack OR
   Attack02 (the `inActiveAttack` condition gates entry).

**The IsInTransition top-level gate stays.** Step 3's design that a
press during a state-transition blend is dropped is preserved — combo
presses landing exactly during the 0.10 s Attack→Attack02 transition
get dropped, same way they do during Idle→Attack today.

---

## Section 4 — How combo flow runs end-to-end (illustrative)

Combining Step 6's Animator graph + Step 7's runtime wiring:

**Frame 0:** Player in Idle. Press Attack.
- `OnAttackPressed`: hash=Idle, not transition, not Hit, not Attack03,
  `inActiveAttack=false` → `SetAttackTrigger()` fires immediately.
- Animator: N1 (Idle → Attack) fires. Attack01 plays.

**Frame ~16 (Attack n≈0.5):** Press Attack again.
- `OnAttackPressed`: hash=Attack, not transition, not Hit, not Attack03,
  `inActiveAttack=true` → fall through to window check. n=0.5, in
  [0.40, 0.80) → `_attackBuffered = true`.
- Animator: no change yet.

**Frame ~27 (Attack n≈0.85):** Buffer-consume threshold reached.
- `Update`: `_attackBuffered=true`, anim non-null, not in transition,
  hash=Attack (in {Attack, Attack02}), n=0.85 ≥ bufferConsumeAt →
  `SetComboNext()` fires; `_attackBuffered = false`.
- Animator: ComboNext set + Attack at exitTime 0.85 + N14 condition
  satisfied → N14 fires. Transition Attack → Attack02 begins (0.10 s
  blend). ComboNext consumed.

**Frame ~28 onward:** Attack02 plays. Player presses Attack again at
n≈0.5 → buffer set. Frame ~54: `Update` consume site fires
`SetComboNext()` (state Attack02 still in gate set). Animator: N15 fires
(Attack02 → Attack03). ComboNext consumed.

**Frame ~70 (Attack03 begins):** Attack03 plays. Player presses Attack
fourth time.
- `OnAttackPressed`: hash=Attack03 → explicit early-return. Press dropped.
- `_attackBuffered` remains false. `Update` early-outs on
  `!_attackBuffered`. No SetComboNext call.
- Animator: Attack03 runs to exitTime 0.90 → N17 fires (Attack03 → Idle).

**Frame ~88 (back in Idle):** Next Attack press starts a fresh
Attack01 (Section 4's "inActiveAttack=false" branch).

**Combo cap verified:** the cap is enforced in two redundant places:
1. The explicit `Attack03StateHash` drop in OnAttackPressed (this step).
2. The implicit Animator graph: even if a press did slip through and
   set ComboNext, Attack03 has no outgoing ComboNext transition — the
   trigger would auto-clear.

Both safety nets work; the explicit drop is cheaper and clearer.

---

## Section 5 — Why this design

### 5.1 Why one substantive line changes in the consume site

The buffer mechanism from Step 3 is correct. Window timing is correct.
Edge handling is correct. The single intentional change: which trigger
fires at the consume threshold. `SetAttackTrigger()` (re-fires same
state, looping at Attack01 via N1's idle re-entry path) becomes
`SetComboNext()` (routes to next combo state via N14 / N15).

The state-gate extension (`|| Attack02StateHash`) is a structural
prerequisite — without it, the consume site never fires from
Attack02 and the combo caps at 2 hits silently.

### 5.2 Why explicit Attack03 drop in OnAttackPressed

The "Attack trigger fires from Attack03 but has no outgoing
transition, so it's a no-op" path *works* but is fragile. If
someone authors an Attack03 → Idle transition on Attack trigger
later (for any reason — perhaps an early-cancel feature), the
silent drop turns into a state cancel. Bug.

Explicit drop in OnAttackPressed makes the design intent visible
and protects against that future regression.

### 5.3 Why static readonly hashes (not const)

Animator.StringToHash is a runtime call — can't be const. `static
readonly int` initialized at class-load matches the pattern already
in PlayerCombat (lines 49-50) and PlayerController (Step 5). Cost:
zero per access after initialization.

### 5.4 Why no smoke test for the Attack04 case

Attack03 has one outgoing transition (N17 → Idle). No combo-extension
wiring exists. Attack04's clip is reserved (Step 1 Section C
validated PASS) but not wired. Smoke Test 13 verifies "press during
Attack03 is dropped, combo returns to Idle, next press starts at
Attack01" — that implicitly verifies the cap.

If Attack04 ever wires up, this becomes Step 8's concern (add
ComboNext condition on N18, Attack03 → Attack04, and the gate in
PlayerCombat extends to include Attack03StateHash for the
buffer-consume; the explicit drop moves to Attack04StateHash).

### 5.5 Why no PlayerController changes

PlayerController.IsActionLocked reads from PlayerCombat.IsActionLocked.
PlayerCombat.IsActionLocked already considers `AttackStateHash` OR
`HitStateHash` (line 81). After Step 7, the combo extends through
Attack02 and Attack03 — which are NOT in IsActionState's gate.

**This is intentional.** During Attack02 / Attack03, IsActionLocked
returns false. PlayerController.IsActionLocked returns false too.
Player movement is NOT zeroed during combo state 2 and 3.

But — PlayerController's locomotion gate is already conservative:
`Step 4.5: if (_combat.IsActionLocked) horizontalMotion = Vector3.zero;`
Today only Attack01 and Hit lock movement. Attack02 / Attack03 do
not lock movement.

**Should they?** Open question (Section 6.7). Recommendation: keep
behavior as-is for Step 7. Locking only Attack01 means combo follow-
ups allow micro-positioning, which is actually a *desirable* feel
in many action games (the player can re-aim slightly between hits).
If a stronger root-on-combo feel is needed, change IsActionState to
also recognize Attack02 / Attack03 — but that's a tuning decision
deferred to a future polish prompt, not a Step 7 deliverable.

### 5.6 Why no PlayerInputReader changes

The AttackPressed event already routes the input. Step 7 changes
how PlayerCombat *interprets* the event, not how PlayerInputReader
*emits* it.

---

## Section 6 — Open questions (recommendations)

1. **Add `_hashComboNext` field + `ParamComboNext` constant in
   PlayerAnimator.** Required. **Recommendation: yes.**

2. **Add `Attack02StateHash` constant in PlayerCombat.** Required
   for the consume-site gate. **Recommendation: yes.**

3. **Add `Attack03StateHash` constant in PlayerCombat.** Required
   for the explicit OnAttackPressed drop. **Recommendation: yes.**

4. **Update consume-site gate to include Attack02.** Required.
   **Recommendation: yes** (per Section 2).

5. **Add explicit Attack03 drop in OnAttackPressed.** Belt-and-
   suspenders defensive coding (Animator graph already has no
   outgoing Attack-trigger from Attack03, so without the explicit
   drop the press is a no-op). **Recommendation: yes** (per Section 3
   and 5.2).

6. **Smoke test additions.** 5 new tests:
   - Test 11: Full 3-hit combo
   - Test 12: Combo drops if no buffered press
   - Test 13: Combo caps at Attack03 (press during Attack03 dropped)
   - Test 14: Hit mid-combo cancels; next press starts at Attack01
   - Test 15: Jump press during combo doesn't consume buffer
     (regression check from Step 5)

   **Recommendation: yes, all 5.**

7. **Should Attack02 / Attack03 also lock movement** (extend
   `IsActionState` to recognize them)? Today they don't —
   PlayerController.Step 4.5 horizontal-zero only fires for Attack01
   and Hit.
   **Recommendation: defer.** Keep Step 7 minimal-change; tune later
   if combo feel needs more rooting. The Animator clips are
   InPlace-mode root-locked so visual movement during the combo
   states is governed only by the player's own input, which is
   subjectively *good* (allows micro-positioning between hits).

8. **Smoke test doc location.** New file vs. append to Step 3 doc.
   **Recommendation: new file** (`M2B_07_combo_smoke_test.md`) per
   the prompt's Section 6.6 guidance. Step 3 doc stays as the
   "single attack" historical record; Step 7 doc covers the
   combo extension.

---

## Section 7 — Summary of work to follow this table

After user confirmation:

- **Step ②** PlayerAnimator.cs:
  - Add `ParamComboNext` const + `_hashComboNext` field.
  - Add hash assignment in `Awake`.
  - Add public `SetComboNext()` method with XML doc.
- **Step ③** PlayerCombat.cs:
  - Add `Attack02StateHash` and `Attack03StateHash` static readonly fields.
  - Update `Update()` consume-site gate (`hash != Attack && hash != Attack02`).
  - Replace `SetAttackTrigger()` with `SetComboNext()` in `Update()`.
  - Update `OnAttackPressed`: add Attack03 explicit drop;
    `inActiveAttack` includes Attack02; window-buffer logic unchanged.
- **Step ④** Reflection validator:
  - `PlayerAnimator.SetComboNext()` exists, public, void.
  - `_hashComboNext` field exists.
  - `PlayerCombat.Attack02StateHash` and `Attack03StateHash` exist
    as static readonly int.
  - Source scan: `SetAttackTrigger` count = 1 in PlayerCombat.cs;
    `SetComboNext` count = 1 in PlayerCombat.cs.
  - Compile clean.
- **Step ⑤** `M2B_07_combo_smoke_test.md` with 5 tests.
- **Step ⑥** Append Step 7 status block to `CLAUDE.md`.

**No PlayerController / PlayerInputReader / FBX / controller /
override controller / prefab modifications.**

---

**Stop here for user review. Steps ②–⑥ wait until the user confirms
this table.**

**Behavior table path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_07_combo_runtime_behavior_table.md`
