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
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walk speed in m/s. Tuned for milestone 1.")]
        [SerializeField] private float walkSpeed = 2.0f;

        [Tooltip("Sprint speed multiplier applied to walkSpeed when IsSprinting && MoveZ > 0.7. Default 1.75 (3.5 m/s at 2.0 walk).")]
        [SerializeField] private float sprintMultiplier = 1.75f;

        [Tooltip("Rotation rate in degrees/sec when re-aligning body to move direction.")]
        [SerializeField] private float rotationSpeed = 900f;

        [Tooltip("Gravity acceleration in m/s². Negative.")]
        [SerializeField] private float gravity = -9.81f;

        [Tooltip("Constant downward velocity while grounded to keep CharacterController pinned to floor.")]
        [SerializeField] private float stickyGroundVelocity = -2f;

        [Tooltip("Squared-magnitude threshold below which we don't bother rotating (avoids jitter at deadzone).")]
        [SerializeField] private float minMoveSqr = 0.0001f;

        [Header("Jump")]
        [Tooltip("Jump height in meters at the peak of the arc. Fixed-height jump; gravity completes the arc. Air-time ≈ 2 * sqrt(2h/|g|) ≈ 0.99s at default 1.2m / -9.81 gravity.")]
        [SerializeField] private float jumpHeight = 1.2f;

        [Header("References")]
        [Tooltip("Camera the input is interpreted relative to. Auto-resolves to Camera.main if null at Awake.")]
        [SerializeField] private Transform cameraTransform;

        // ── Cached refs ─────────────────────────────────────────────────────
        private CharacterController _cc;
        private PlayerInputReader _input;
        private PlayerAnimator _anim;
        private PlayerCombat _combat;        // optional — null tolerated (gate disabled)
        private float _verticalVelocity;

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

            // 3) Build camera-relative world-space move direction (XZ plane only).
            Vector3 moveDirXZ = BuildCameraRelativeMove(input);

            // 4) Compose horizontal motion. Sprint multiplier kicks in when:
            //    - the player is holding Sprint, AND
            //    - input is mostly forward (matches Animator gate of MoveZ > 0.7)
            float currentSpeed = walkSpeed;
            if (_input.IsSprinting && input.y > 0.7f)
                currentSpeed *= sprintMultiplier;
            Vector3 motion = moveDirXZ * currentSpeed;

            // 4.5) Root in place during Attack / Hit. Animator MoveX/MoveZ
            //      writes (step 8) keep firing so the locomotion blend tree
            //      stays primed; only the CharacterController translation is
            //      gated. Gravity still applies so the player doesn't float.
            if (_combat != null && _combat.IsActionLocked)
                motion = Vector3.zero;

            // 5) Apply gravity (sticky-grounded).
            ApplyGravity(ref motion);

            // 6) Move.
            _cc.Move(motion * Time.deltaTime);

            // 7) Align body yaw to camera yaw. Snap (no smoothing) per design
            //    decision (α). The body's "forward" is whatever the camera is
            //    looking along on the XZ plane, so strafing input maps cleanly:
            //    A on the stick = move left relative to body = LFT walk clip.
            SnapBodyToCameraYaw();

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

            // 9) Push sprint bool to animator. Read by the
            //    Locomotion → Sprint and Sprint → Locomotion transitions.
            _anim.SetSprinting(_input.IsSprinting);
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
    }
}
