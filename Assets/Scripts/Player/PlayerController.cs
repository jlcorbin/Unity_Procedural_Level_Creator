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

        [Header("References")]
        [Tooltip("Camera the input is interpreted relative to. Auto-resolves to Camera.main if null at Awake.")]
        [SerializeField] private Transform cameraTransform;

        // ── Cached refs ─────────────────────────────────────────────────────
        private CharacterController _cc;
        private PlayerInputReader _input;
        private PlayerAnimator _anim;
        private float _verticalVelocity;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _anim = GetComponent<PlayerAnimator>();

            if (cameraTransform == null)
            {
                var main = Camera.main;
                if (main != null) cameraTransform = main.transform;
                else Debug.LogWarning($"[PlayerController] No cameraTransform set and Camera.main is null. Movement will be world-axis-aligned until a camera is found.", this);
            }
        }

        private void Update()
        {
            // 1) Read input.
            Vector2 input = _input.MoveInput;

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

            // 5) Apply gravity (sticky-grounded).
            ApplyGravity(ref motion);

            // 6) Move.
            _cc.Move(motion * Time.deltaTime);

            // 7) Align body yaw to camera yaw. Snap (no smoothing) per design
            //    decision (α). The body's "forward" is whatever the camera is
            //    looking along on the XZ plane, so strafing input maps cleanly:
            //    A on the stick = move left relative to body = LFT walk clip.
            SnapBodyToCameraYaw();

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
        /// Sticky-grounded gravity. While grounded, vertical velocity is clamped
        /// to a small constant negative value so isGrounded stays true frame-over-
        /// frame even on minor terrain bumps. While airborne, accumulate normally.
        /// </summary>
        private void ApplyGravity(ref Vector3 motion)
        {
            if (_cc.isGrounded)
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
