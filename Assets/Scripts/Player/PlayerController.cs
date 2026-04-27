// PlayerController.cs
// Per-frame movement pipeline. Reads input intent from PlayerInputReader,
// builds a camera-relative move vector, applies sticky-grounded gravity,
// drives the CharacterController, rotates the body to face the move
// direction, and forwards (MoveX=0, MoveZ=Speed) to PlayerAnimator.
//
// In M1 the body rotates to face the move direction, so the Animator's
// MoveX is always 0 and MoveZ always equals input magnitude — the
// Locomotion blend tree only ever evaluates at (0, +Speed). The remaining
// directional clips stay authored for a M2 strafe-mode swap.

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Converts input intent into <see cref="CharacterController.Move"/> calls
    /// each frame, rotates the body to face the camera-relative move
    /// direction, and forwards locomotion intent to <see cref="PlayerAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Walk speed in m/s. Tuned for milestone 1.")]
        [SerializeField] private float walkSpeed = 2.0f;

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

            // 4) Compose horizontal motion.
            Vector3 motion = moveDirXZ * walkSpeed;

            // 5) Apply gravity (sticky-grounded).
            ApplyGravity(ref motion);

            // 6) Move.
            _cc.Move(motion * Time.deltaTime);

            // 7) Rotate towards move direction (skip if effectively zero).
            if (moveDirXZ.sqrMagnitude > minMoveSqr)
                RotateTowardsMoveDir(moveDirXZ);

            // 8) Push animator parameters. With rotate-to-face, MoveX is always 0
            //    and MoveZ equals the input magnitude (the "Speed" the blend
            //    tree's state-speed-multiplier picks up).
            _anim.SetMove(0f, input.magnitude);
        }

        // ── Private helpers ─────────────────────────────────────────────────

        /// <summary>Project camera forward and right onto the XZ plane and combine with input.</summary>
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

        /// <summary>Slerp body yaw toward move direction at <see cref="rotationSpeed"/> deg/sec.</summary>
        private void RotateTowardsMoveDir(Vector3 moveDirXZ)
        {
            Quaternion target = Quaternion.LookRotation(moveDirXZ, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }
    }
}
