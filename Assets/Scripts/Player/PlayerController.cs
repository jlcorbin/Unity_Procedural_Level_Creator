// PlayerController.cs
// Per-frame movement pipeline. Reads input intent from PlayerInputReader,
// builds a camera-relative move vector, applies sticky-grounded gravity,
// drives the CharacterController, snaps body yaw to camera yaw, and
// forwards (input.x, input.y) to PlayerAnimator so the 4-corner blend
// tree exercises FWD / BWD / LFT / RGT clips.
//
// Locomotion model: STRAFE with snap body alignment (RE4-Remake feel).
// Body yaw is locked to camera yaw every frame — strafing input maps
// cleanly to the body-relative LFT/RGT walk clips. Switched from M1's
// rotate-to-face on 2026-04-27; see "Design Course Correction" section
// in Documentation/Player_Animator_Design_2026-04-26.md.

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Converts input intent into <see cref="CharacterController.Move"/> calls
    /// each frame, snaps the body's yaw to the camera's yaw (strafe model),
    /// and forwards body-relative locomotion intent to
    /// <see cref="PlayerAnimator"/>.
    /// </summary>
    // Note: PlayerStamina is intentionally NOT [RequireComponent]'d here
    // even though we read it in Awake — PlayerStamina has the inverse
    // [RequireComponent(PlayerController)] for its `_controller` ref,
    // and bidirectional [RequireComponent] is rejected by Unity.
    // PlayerHero's manifest carries both, satisfying the contract from
    // the root level. The `_stamina` ref here remains null-tolerant.
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerDodge))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walk speed in m/s.")]
        [SerializeField] private float walkSpeed = 3.5f;

        [Tooltip("Sprint speed multiplier. Sprint logic has been removed from the movement pipeline; this field is retained for inspector visibility but no longer affects movement speed.")]
        [SerializeField] private float sprintMultiplier = 1.75f;

        [Tooltip("Gravity acceleration in m/s². Negative.")]
        [SerializeField] private float gravity = -9.81f;

        [Tooltip("Constant downward velocity while grounded to keep CharacterController pinned to floor.")]
        [SerializeField] private float stickyGroundVelocity = -2f;

        [Tooltip("Squared-magnitude threshold below which we don't bother rotating (avoids jitter at deadzone).")]
        [SerializeField] private float minMoveSqr = 0.0001f;

        [Header("Jump")]
        [Tooltip("Jump height in meters at the peak of the arc. Fixed-height jump; gravity completes the arc. Air-time ≈ 2 * sqrt(2h/|g|) ≈ 0.99s at default 1.2m / -9.81 gravity.")]
        [SerializeField] private float jumpHeight = 1.2f;

        [Header("Sneak")]
        [Tooltip("Movement speed in m/s while sneaking.")]
        [SerializeField] private float _sneakSpeed = 2.0f;

        [Header("Target Lock")]
        [Tooltip("Slerp rate per second used to rotate the player body toward a locked target. " +
                 "Higher values snap faster; lower values feel more fluid.")]
        [SerializeField] private float _lockFaceSpeed = 10f;

        [Header("References")]
        [Tooltip("Camera the input is interpreted relative to. Auto-resolves to Camera.main if null at Awake.")]
        [SerializeField] private Transform cameraTransform;

        // ── Cached refs ─────────────────────────────────────────────────────
        private CharacterController _cc;
        private PlayerInputReader _input;
        private PlayerAnimator _anim;
        private PlayerCombat _combat;        // optional — null tolerated (gate disabled)
        private PlayerDodge _dodge;          // optional — null tolerated (no dodge gate)
        private float _verticalVelocity;

        /// <summary>
        /// True the frame the player is actually moving at sprint speed
        /// (input held + forward-mostly input + stamina permits). Distinct
        /// from <see cref="PlayerInputReader.IsSprinting"/>, which is just
        /// "is Shift held" — the input may be held but stamina-empty, which
        /// gates this property false. PlayerStamina reads this to decide
        /// whether to drain or regen.
        /// </summary>
        public bool IsSprintingNow { get; private set; }

        // ── Jump / grounded state ───────────────────────────────────────────
        // Resolved lazily — sibling Awake order is non-deterministic, so
        // _anim.Animator may be null when our Awake runs. PlayerCombat hit
        // this exact trap in Step 3 and adopted the same pattern.
        private Animator AnimatorComponent => _anim != null ? _anim.Animator : null;

        private static readonly int AttackStateHash  = Animator.StringToHash("Attack");
        private static readonly int HitStateHash     = Animator.StringToHash("Hit");
        private static readonly int JumpEndStateHash = Animator.StringToHash("JumpEnd");

        private bool _isGrounded;
        private bool _wasGrounded;
        private bool _groundedDirty = true;  // forces SetGrounded write on frame 0

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _anim = GetComponent<PlayerAnimator>();
            _combat = GetComponent<PlayerCombat>();   // optional — null tolerated
            _dodge   = GetComponent<PlayerDodge>();   // optional — null tolerated (no dodge gate if missing)

            if (cameraTransform == null)
            {
                var main = Camera.main;
                if (main != null) cameraTransform = main.transform;
                else Debug.LogWarning($"[PlayerController] No cameraTransform set and Camera.main is null. Movement will be world-axis-aligned until a camera is found.", this);
            }
        }

        private void OnEnable()
        {
            if (_input != null) _input.JumpPressed += OnJumpPressed;
        }

        private void OnDisable()
        {
            if (_input != null) _input.JumpPressed -= OnJumpPressed;
        }

        // ── Input event handlers ────────────────────────────────────────────

        /// <summary>
        /// Subscribed to <see cref="PlayerInputReader.JumpPressed"/>. Routes
        /// the press through the gameplay-side gate before firing the Jump
        /// trigger. Drops the press during Attack / Hit (action lock), while
        /// airborne (no double-jump), or during JumpEnd (landing recovery).
        /// </summary>
        private void OnJumpPressed()
        {
            if (IsActionLocked())  return;  // Attack / Hit blocks jump
            if (!_isGrounded)       return;  // no air-jump, no double-jump
            if (IsInJumpEndState()) return;  // wait for landing recovery

            // Kinematic jump: v = sqrt(2 * h * |g|). Apply to the same
            // _verticalVelocity channel ApplyGravity uses — gravity then
            // decelerates the rise, peaks at jumpHeight, and accelerates
            // the fall until _cc.isGrounded re-fires.
            _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            _anim.SetJumpTrigger();
        }

        private void Update()
        {
            // 1) Read input.
            Vector2 input = _input.MoveInput;

            // 1.5) Cache grounded state. Used by step 7.5's edge-detected
            //      SetGrounded write and by OnJumpPressed's airborne gate.
            //      ApplyGravity (step 5) still reads _cc.isGrounded directly
            //      to keep itself self-contained — duplicate read is cheap.
            _isGrounded = _cc.isGrounded;

            // 2) Clamp magnitude to 1 (Q-5: WASD diagonal would be ~1.414 raw).
            if (input.sqrMagnitude > 1f) input.Normalize();

            // 3) Build world-space move direction (XZ plane only).
            //    When locked onto a target, WASD is body-relative (strafe toward
            //    the target); otherwise use the standard camera-relative path.
            Vector3 moveDirXZ = BuildMoveVector(input);

            // 4) Compose horizontal motion. Sprint logic has been removed from the
            //    movement pipeline — walkSpeed is the single movement speed.
            //    IsSprintingNow is kept inert (always false) so PlayerStamina
            //    and PlayerAnimator dependents continue to compile cleanly.
            IsSprintingNow = false;
            float currentSpeed = walkSpeed;
            if (_input.IsSneaking) currentSpeed = _sneakSpeed;
            Vector3 motion = moveDirXZ * currentSpeed;

            // 4.5) Root in place during Attack / Hit. Animator MoveX/MoveZ
            //      writes (step 8) keep firing so the locomotion blend tree
            //      stays primed; only the CharacterController translation is
            //      gated. Gravity still applies so the player doesn't float.
            if (_combat != null && _combat.IsActionLocked)
                motion = Vector3.zero;
            // 4.6) Same gate during a M12 dodge — PlayerDodge.RollCoroutine
            //      drives horizontal displacement via its own _cc.Move call
            //      in Update, so this Update's horizontal motion must yield.
            //      Two cc.Move calls per frame is well-defined in Unity;
            //      each call updates position before the next reads it.
            if (_dodge != null && _dodge.IsDodging)
                motion = Vector3.zero;

            // 5) Apply gravity (sticky-grounded).
            ApplyGravity(ref motion);

            // 6) Move.
            _cc.Move(motion * Time.deltaTime);

            // 7) Align body yaw. Two modes:
            //    a) Locked: slerp to face the target (TargetLock strafe model).
            //       WASD is already projected relative to body-facing in step 3,
            //       so the player strafes around the enemy as expected.
            //    b) Free-look: snap body to camera yaw (original M2 strafe model).
            RotateBody();

            // 7.5) Push grounded state to Animator on change. Drives N10
            //      (JumpStart → JumpAir on !IsGrounded) and N12
            //      (JumpAir → JumpEnd on IsGrounded). Edge-detected to avoid
            //      per-frame SetBool spam. _groundedDirty=true on construction
            //      forces the first SetGrounded call so the Animator's default
            //      (true) matches reality even if the player spawns airborne.
            if (_groundedDirty || _isGrounded != _wasGrounded)
            {
                _anim.SetGrounded(_isGrounded);
                _wasGrounded   = _isGrounded;
                _groundedDirty = false;
            }

            // 8) Push animator parameters. Body yaw is locked to camera yaw, so
            //    the input vector IS the body-relative move direction:
            //      input.x  → MoveX → strafe direction (positive = right)
            //      input.y  → MoveZ → forward/back direction (positive = forward)
            //    The 4-corner blend tree picks the right walk clip; magnitude
            //    drives Speed (computed inside SetMove).
            _anim.SetMove(input.x, input.y);

            // 9) Push sprint bool to animator. IsSprintingNow is always false
            //    (sprint logic removed from movement pipeline); this call is
            //    kept so the Animator's IsSprinting parameter is reliably
            //    cleared each frame and dependents compile unchanged.
            _anim.SetSprinting(IsSprintingNow);
        }

        // ── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// True when the Animator's current state is Attack or Hit, AND the
        /// Animator is not in the middle of a transition. The transition
        /// passthrough is intentional — it allows the player to jump during
        /// the Attack→Idle blend (the swing is functionally over) without
        /// waiting through the 0.10s blend window. The independent
        /// <see cref="PlayerCombat.IsActionLocked"/> property uses similar
        /// logic for locomotion gating but checks the next state during
        /// transitions; the two are intentionally not shared because the
        /// jump check wants permissive transition behavior and the
        /// locomotion gate does not.
        /// </summary>
        private bool IsActionLocked()
        {
            var anim = AnimatorComponent;
            if (anim == null) return false;
            if (anim.IsInTransition(0)) return false;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == AttackStateHash
                || info.shortNameHash == HitStateHash;
        }

        /// <summary>
        /// True when the Animator's current state is JumpEnd. JumpStart and
        /// JumpAir are covered by the <c>!_isGrounded</c> check in
        /// <see cref="OnJumpPressed"/> — only JumpEnd needs an explicit
        /// state-name check because the player IS grounded during landing
        /// recovery.
        /// </summary>
        private bool IsInJumpEndState()
        {
            var anim = AnimatorComponent;
            if (anim == null) return false;
            var info = anim.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == JumpEndStateHash;
        }

        /// <summary>
        /// Project camera forward and right onto the XZ plane and combine with
        /// input to produce a world-space move direction. Drives the
        /// CharacterController's translation (step 6). Body rotation is
        /// handled separately by <see cref="SnapBodyToCameraYaw"/> in step 7.
        /// </summary>
        private Vector3 BuildCameraRelativeMove(Vector2 input)
        {
            if (cameraTransform == null)
                return new Vector3(input.x, 0f, input.y);  // fallback: world axes

            Vector3 fwd = cameraTransform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = cameraTransform.right; right.y = 0f; right.Normalize();
            return fwd * input.y + right * input.x;
        }

        /// <summary>
        /// Sticky-grounded gravity. While grounded AND not currently rising,
        /// vertical velocity is clamped to a small constant negative value so
        /// isGrounded stays true frame-over-frame even on minor terrain bumps.
        /// The <c>_verticalVelocity &lt; 0</c> guard is critical for jump:
        /// without it, the same frame OnJumpPressed sets a positive velocity,
        /// this clamp would overwrite it back to <see cref="stickyGroundVelocity"/>
        /// and the player would never leave the ground. While airborne, or
        /// while grounded and rising (the takeoff frame), accumulate gravity
        /// normally.
        /// </summary>
        private void ApplyGravity(ref Vector3 motion)
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = stickyGroundVelocity;
            else
                _verticalVelocity += gravity * Time.deltaTime;
            motion.y = _verticalVelocity;
        }

        /// <summary>
        /// Snap the player's yaw to match the camera's yaw on the XZ plane.
        /// Pitch and roll stay at zero. Per design decision (α): snap is
        /// intentional — no smoothing, no delay. Tight strafe-style feel
        /// (RE4 Remake / FF7 Remake convention).
        /// </summary>
        private void SnapBodyToCameraYaw()
        {
            if (cameraTransform == null) return;

            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0f;
            // Edge case: camera looking straight up or down → near-zero XZ
            // vector after y-zero. Skip rotation this frame.
            if (camForward.sqrMagnitude < minMoveSqr) return;
            camForward.Normalize();

            transform.rotation = Quaternion.LookRotation(camForward, Vector3.up);
        }

        /// <summary>
        /// Builds the world-space XZ move vector from WASD input.
        /// When locked onto a target via <see cref="TargetLock"/>, input is
        /// projected relative to the player's current body-forward (which is
        /// being rotated toward the target by <see cref="RotateBody"/>), giving
        /// proper strafe-around-target movement. Otherwise falls back to the
        /// standard camera-relative projection used in free-look mode.
        /// </summary>
        private Vector3 BuildMoveVector(Vector2 input)
        {
            if (TargetLock.Instance != null
                && TargetLock.Instance.IsLocked
                && TargetLock.Instance.LockedTarget != null)
            {
                // Strafe mode: project WASD relative to the player's body axes.
                // RotateBody() (step 7) will slerp body toward the target this
                // same frame. Using the body's current forward + right means the
                // input reads correctly on the very first locked frame too.
                Vector3 fwd   = transform.forward; fwd.y = 0f;
                Vector3 right = transform.right;   right.y = 0f;
                if (fwd.sqrMagnitude   > minMoveSqr) fwd.Normalize();
                if (right.sqrMagnitude > minMoveSqr) right.Normalize();
                return fwd * input.y + right * input.x;
            }

            // Free-look: standard camera-relative move (original path).
            return BuildCameraRelativeMove(input);
        }

        /// <summary>
        /// Rotates the player's body each frame. When locked onto a target,
        /// smoothly slerps to face it at <see cref="_lockFaceSpeed"/> degrees
        /// per second. In free-look mode, snaps body to camera yaw (original
        /// M2 strafe model — no change to existing behavior).
        /// </summary>
        private void RotateBody()
        {
            if (TargetLock.Instance != null
                && TargetLock.Instance.IsLocked
                && TargetLock.Instance.LockedTarget != null)
            {
                // Lock-on: smoothly rotate to face the target on the XZ plane.
                Vector3 toTarget = TargetLock.Instance.LockedTarget.transform.position
                                   - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    Quaternion targetFacing = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetFacing,
                        Time.deltaTime * _lockFaceSpeed);
                }
            }
            else
            {
                // Free-look: snap body to camera yaw (original behavior).
                SnapBodyToCameraYaw();
            }
        }
    }
}
