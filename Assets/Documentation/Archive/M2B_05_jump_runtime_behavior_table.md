# M2-B Step 5 — Jump Runtime Behavior Table

**Date:** 2026-04-27
**Scope:** Behavior table for the runtime side of jump — the
script-side state machine that drives jump physics, IsGrounded
polling, and Animator parameter writes. The Animator graph itself was
locked in Step 4 (`M2B_04_jump_animator_behavior_table.md`); this
prompt does not touch it.
**Status:** Awaiting user confirmation. No `.cs` file has been
modified by Step 5 yet.

Source data:
- Step 4 graph: `Assets/Documentation/M2B_04_jump_animator_behavior_table.md`
- Step 4 clip lengths: `Assets/Documentation/M2B_04_jump_clip_survey.md`
- Existing scripts read this session (sole writer / event / locomotion / combat).

---

## Section 0: Field-name reconciliation (prompt → actual codebase)

The Step 5 prompt uses several nominal names that differ from the
real fields in `PlayerController.cs` / `PlayerAnimator.cs`. The
implementation should use the actual names below; the prompt's pseudo-
code is illustrative.

| Prompt nominal | Actual field |
|---|---|
| `_velocityY` | `_verticalVelocity` (PlayerController) |
| `_characterController` | `_cc` (PlayerController) |
| `_playerAnimator` | `_anim` (PlayerController) |
| `_input` | `_input` (matches) |
| `gravity` | `gravity` (matches; SerializeField, default -9.81) |
| sticky-ground constant `-2f` | `stickyGroundVelocity` SerializeField (already exists, default -2) |
| `_animatorComponent` field | new field, lazy-resolved (see Section 4) |

Sticky-ground clamp **already exists** in `PlayerController.ApplyGravity`
([PlayerController.cs:152-153](Assets/Scripts/Player/PlayerController.cs#L152-L153)).
No new sticky-ground code is needed.

---

## Section 1: Public API surface

### `PlayerAnimator.SetJumpTrigger()` *(new)*

```csharp
public void SetJumpTrigger();
```

- Caller: `PlayerController.OnJumpPressed`.
- Effect: `_animator.SetTrigger(_hashJump)` — only when `_ready` is
  true (silent no-op pre-Awake, matching `SetMove` / `SetSprinting` /
  `SetAttackTrigger` / `SetHitTrigger`).
- Preconditions: `_ready == true` (else silently dropped).
- Postconditions: Trigger queued on the Animator. N7/N8/N9 fire on
  the next Animator update if currently in Idle/Locomotion/Sprint
  AND `IsGrounded == true`.
- Idempotent — multiple calls within one Animator update are
  collapsed by Unity (a Trigger is a "fire once" flag, not a queue).

### `PlayerAnimator.SetGrounded(bool)` *(new)*

```csharp
public void SetGrounded(bool grounded);
```

- Caller: `PlayerController.Update` (edge-detected internally).
- Effect: `_animator.SetBool(_hashIsGrounded, grounded)` — only when
  `_ready` is true.
- Preconditions: `_ready == true` (else silently dropped).
- Postconditions: Animator's `IsGrounded` Bool param updated.
  Transitions N10 (`!IsGrounded` from JumpStart) and N12
  (`IsGrounded` from JumpAir) re-evaluate against this on the next
  Animator tick.
- Edge detection lives on the **caller** side (`PlayerController`) so
  the Animator's internal SetBool dedup is irrelevant — we just don't
  spam the call.

### `PlayerInputReader.JumpPressed` event *(new)*

```csharp
public event System.Action JumpPressed;
```

- Raised on `OnJump.ctx.performed` (button-down edge only).
- Subscribed to by `PlayerController` in `OnEnable`, unsubscribed in
  `OnDisable`. Mirrors the `AttackPressed` pattern from Step 3.
- The existing M1 stub `Debug.Log("[PlayerInputReader] Jump")` is
  removed in `OnJump` — real consumer replaces it. M1 stub logs on
  `OnInteract` / `OnCrouch` / `OnPrevious` / `OnNext` are preserved.

### `PlayerController.OnJumpPressed()` *(new private)*

```csharp
private void OnJumpPressed();
```

Behavior:
1. If `IsActionLocked()` → drop input. Return. (Attack/Hit block jump.)
2. If `!_isGrounded` → drop input. Return. (No double-jump / no air-jump.)
3. If `IsInJumpEndState()` → drop input. Return. (Wait for landing recovery.)
4. Apply jump velocity: `_verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity)`.
5. Fire `_anim.SetJumpTrigger()`.

The Animator-side gate (`Jump trigger AND IsGrounded == true` on
N7/N8/N9) is belt-and-suspenders — the script does the gameplay logic
and the Animator graph mirrors it for safety.

---

## Section 2: PlayerController state additions

### New SerializeField

```csharp
[Header("Jump")]
[Tooltip("Jump height in meters at peak. Fixed-height jump; gravity completes the arc.")]
[SerializeField] private float jumpHeight = 1.2f;
```

(Existing `gravity = -9.81f` is reused. Existing
`stickyGroundVelocity = -2f` is reused.)

### New private state

```csharp
// Animator state polling (lazy-resolved — see Section 4)
private static readonly int AttackStateHash   = Animator.StringToHash("Attack");
private static readonly int HitStateHash      = Animator.StringToHash("Hit");
private static readonly int JumpEndStateHash  = Animator.StringToHash("JumpEnd");

// IsGrounded edge detection
private bool _isGrounded;        // current frame's CharacterController.isGrounded
private bool _wasGrounded;       // previous frame's reading
private bool _groundedDirty = true;  // forces first SetGrounded write
```

`_verticalVelocity` already exists as the gravity channel — jump
velocity is applied to the same field.

---

## Section 3: Modified Update pipeline

The current 9-step Update pipeline (annotated in
[PlayerController.cs:76-125](Assets/Scripts/Player/PlayerController.cs#L76-L125))
is **preserved**. Modifications are additive only.

Concretely, three insertions and one method addition:

**A. Read `_isGrounded` once per frame.** Insert at the top of Update,
before the existing motion compose. Replaces the implicit read inside
`ApplyGravity` (which still consults `_cc.isGrounded` directly — both
reads are cheap, and keeping `ApplyGravity` self-contained avoids
restructuring it).

```csharp
_isGrounded = _cc.isGrounded;
```

**B. Edge-detect SetGrounded write.** Insert near the end of Update,
after `_cc.Move(...)` (existing step 6) and after
`SnapBodyToCameraYaw` (existing step 7), but before the existing
SetMove / SetSprinting writes (existing steps 8–9):

```csharp
if (_groundedDirty || _isGrounded != _wasGrounded)
{
    _anim.SetGrounded(_isGrounded);
    _wasGrounded   = _isGrounded;
    _groundedDirty = false;
}
```

The `_groundedDirty = true` initial value forces a SetGrounded call
on the first frame, ensuring the Animator's `IsGrounded` default
(`true`, set in Step 4) matches reality on frame 0 even if the player
spawns airborne.

**C. OnEnable / OnDisable subscriptions.** PlayerController doesn't
currently have these (PlayerCombat does). Add:

```csharp
private void OnEnable()
{
    if (_input != null) _input.JumpPressed += OnJumpPressed;
}

private void OnDisable()
{
    if (_input != null) _input.JumpPressed -= OnJumpPressed;
}
```

**D. Add `OnJumpPressed`, `IsActionLocked`, `IsInJumpEndState`.** New
private methods. See Section 1 + Section 4.

**Air control: free.** The existing camera-relative move-vector
build ([PlayerController.cs:135-143](Assets/Scripts/Player/PlayerController.cs#L135-L143))
does not gate on grounded state. The horizontal motion runs every
frame regardless of airborne state, so the player can change
direction in the air. Step 4's locked decision (full air control)
is satisfied by **doing nothing** to the locomotion path.

**Gravity during Hit / Attack: already correct.** `ApplyGravity` runs
unconditionally inside Update before the action-lock motion-zero
check at [PlayerController.cs:99-100](Assets/Scripts/Player/PlayerController.cs#L99-L100).
No change needed. Mid-air hit will continue to fall — Step 4's
airborne-hit caveat is satisfied by the existing structure.

---

## Section 4: Animator state polling

### Lazy resolution (deviation from the prompt)

The prompt's pseudocode caches `_animatorComponent = _playerAnimator.Animator`
in `Awake`. **This is the same trap that hit `PlayerCombat` in Step 3**:
sibling Awake order is non-deterministic, so `_anim.Animator` may be
null when `PlayerController.Awake` runs.

Solution (mirroring PlayerCombat): a lazy property.

```csharp
private Animator AnimatorComponent =>
    _anim != null ? _anim.Animator : null;
```

Callers (`OnJumpPressed`, `IsActionLocked`, `IsInJumpEndState`)
read through this property. Cheap — `_anim.Animator` is a simple
field getter.

### `IsActionLocked()`

```csharp
private bool IsActionLocked()
{
    var anim = AnimatorComponent;
    if (anim == null) return false;
    if (anim.IsInTransition(0)) return false;  // Section 6 Q4: allow during blends
    var info = anim.GetCurrentAnimatorStateInfo(0);
    return info.shortNameHash == AttackStateHash
        || info.shortNameHash == HitStateHash;
}
```

Returning `false` during transitions allows jump during the
Attack→Idle blend (the swing is functionally over). It also allows
jump during the Idle→Attack blend (0.10s) and Hit→Idle blend (0.10s)
— both narrow windows, both acceptable per the prompt's Q4
recommendation.

### `IsInJumpEndState()`

```csharp
private bool IsInJumpEndState()
{
    var anim = AnimatorComponent;
    if (anim == null) return false;
    var info = anim.GetCurrentAnimatorStateInfo(0);
    return info.shortNameHash == JumpEndStateHash;
}
```

We only need to check `JumpEnd` explicitly. JumpStart and JumpAir
are covered by `!_isGrounded` (the player is airborne in those
states). The grounded-during-JumpStart edge case (1 frame between
press and isGrounded flipping false) is harmless — the Animator
graph has no transition from JumpStart to JumpStart, so a re-fire
of the Jump trigger in that window is consumed and discarded by
Unity.

### Why two separate helpers (not one combined `bool CanJump()`)

`IsActionLocked` has independent semantics — a future caller (e.g.,
combat-cancel / dash-cancel) might want the same check without the
JumpEnd / grounded conditions. Splitting keeps each check
single-purpose. The call site in `OnJumpPressed` is three lines —
not enough to justify combining.

### Why poll Animator state instead of reading `_combat.IsActionLocked`

The existing locomotion-zero gate in `Update` does read
`_combat.IsActionLocked` (`PlayerController.cs:99`). That's a
back-channel from PlayerController to PlayerCombat — already
established. Step 5 *could* extend that pattern by adding a
`PlayerCombat.IsBusy` property and reading it from `OnJumpPressed`.

The prompt prefers **independent polling** because:
1. PlayerCombat.IsActionLocked checks current OR next state during
   transitions; the jump check wants `false` during transitions
   (per Q4) — semantics differ.
2. Adding more PlayerController → PlayerCombat coupling expands the
   back-channel surface. Each script reading the Animator directly
   keeps the dependency direction one-way (both → Animator), with
   no cross-script reads.
3. The duplication is minimal — three short helpers in
   PlayerController.

**Recommendation: independent polling per the prompt.** Accept
the small duplication.

---

## Section 5: Why this design

### 5.1 Why `IsGrounded` write goes through PlayerAnimator

Single-writer invariant (CLAUDE.md "PlayerAnimator is the sole writer
to Animator parameters"). PlayerController calling
`_animator.SetBool(...)` directly would create the V1-era pattern of
multi-script parameter writes that produced "who set this?" debugging
hell. Adding `SetGrounded` keeps the parameter name string in one
place and lets the `_ready` gate apply uniformly.

### 5.2 Why jump goes in PlayerController and not a new PlayerJump.cs

Jump is vertical motion. PlayerController already owns vertical
motion (`_verticalVelocity`, gravity, `_cc.Move`). A new script
would have to either (a) write to `_verticalVelocity` from outside —
two writers, ordering bugs guaranteed — or (b) move all vertical
motion out of PlayerController, which contradicts the M2 strafe
"don't refactor PlayerController" handoff.

PlayerCombat got its own script because triggers are orthogonal to
physics. Jump isn't; it's velocity.

### 5.3 Why no coyote time / jump buffering

Both compensate for input edge cases (jumping just after walking off
a ledge / pressing jump just before landing). They matter for
platformers; M2-B's goal is "jump exists and doesn't break the rest
of the system." If play-test reveals the absence, add as separate
concerns. Not in scope for "ship the foundation."

### 5.4 Why no JumpStart-clip-too-short workaround

Survey: 0.267 s. IsGrounded flips false within 1–2 physics frames of
jump press, which triggers N10 immediately. JumpStart's visible
portion is ~0.05–0.1 s in practice. This is the pack's authoring
choice — tuning would either delay IsGrounded artificially (magic
number) or override the pack's clip (out of scope). Accept and
observe in smoke test.

### 5.5 Why gravity unconditionally during Hit

Step 4 flagged this: N5 (Any State → Hit) covers airborne states. If
gravity stopped during Hit, a mid-air hit would freeze the player at
the air-pose Y until stagger ended. Existing PlayerController already
runs `ApplyGravity` before the action-lock motion-zero check
([PlayerController.cs:99-103](Assets/Scripts/Player/PlayerController.cs#L99-L103))
— motion.y is set to `_verticalVelocity` AFTER the horizontal-motion
zero clears, so vertical motion is preserved. Verified: no script
change needed for this requirement.

### 5.6 Why edge-detect SetGrounded

Avoids per-frame SetBool spam. The Animator deduplicates internally,
but skipping the call when the value didn't change saves the method
call entirely. One bool compare per frame vs. one Animator write per
frame — the comparison wins on hot-path cost. Also makes the call
site debuggable: a SetGrounded log would print only on transitions,
not every frame.

---

## Section 6: Open questions

Confirm or override each before Step ② proceeds.

1. **`jumpHeight = 1.2f` default.** At gravity = -9.81, peak ≈ 1.2 m
   above launch, total air-time ≈ 0.99 s (`2 * sqrt(2h/g)`). Higher
   (1.5–2.0 m) feels athletic; lower (0.8–1.0 m) feels grounded.
   **Recommendation: 1.2.**

2. **Sticky-ground clamp.** Already exists at
   [PlayerController.cs:152-153](Assets/Scripts/Player/PlayerController.cs#L152-L153)
   via `stickyGroundVelocity = -2f`. **No new code needed.**

3. **Edge-detect SetGrounded write.** Two-bool compare per frame to
   skip SetBool. **Recommendation: edge-detect** (per prompt).

4. **Allow jump during Animator transitions?** Returning `false` from
   `IsActionLocked()` when `IsInTransition(0)` is true allows jump
   during Attack→Idle, Idle→Attack, Hit→Idle blends. Attack-blend-out
   is the intended use case; the others are narrow incidental
   windows. **Recommendation: yes (allow during transitions).**

5. **Jump during JumpEnd — block (Option A) or chain (Option B)?**
   Option A: `IsInJumpEndState()` blocks; player must finish recovery.
   Option B: Animator transition JumpEnd → JumpStart re-jumps
   immediately. Option B is spammable and out of scope for Step 5
   (would need an Animator change). **Recommendation: Option A
   (block).** If play-test wants spammable, revisit in a later milestone.

6. **Lazy `AnimatorComponent` property over Awake-cached field.** The
   prompt's `_animatorComponent = _playerAnimator.Animator` in Awake
   has the same null-on-first-call risk that hit PlayerCombat in
   Step 3. **Recommendation: lazy property** (matches PlayerCombat
   pattern).

7. **Jump cannot consume the attack combo buffer.** Smoke test 10
   verifies. PlayerCombat's `_attackBuffered` is set only by
   `OnAttackPressed` and consumed only in PlayerCombat.Update —
   PlayerController writes nothing to it. No code change required;
   this is a behavioral assertion to be validated by smoke test, not
   a design choice.

8. **OnEnable / OnDisable not currently in PlayerController.** Need
   to add for JumpPressed subscribe / unsubscribe. Mirrors PlayerCombat
   pattern. **Recommendation: add as plain methods alongside Awake.**

---

## Stop point

**Per the prompt's instruction:** Do not proceed to Step ② / ③ / ④ / ⑤
/ ⑥ / ⑦ until the user confirms this table.

When confirming, please flag any changes to the open questions above
(or "all defaults are fine") so I know what to wire.

---

**Behavior table path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M2B_05_jump_runtime_behavior_table.md`
