// PlayerAnimator.cs
// Sole writer to the Player's Animator parameters. Translates a
// (moveX, moveZ) vector into typed Animator parameter writes, with
// parameter hash IDs cached in Awake so the per-frame hot path uses
// Animator.SetFloat(int, float) rather than the slower string overload.
//
// The Animator component lives on the MaleCharacterPBR child, NOT on
// the prefab root where this script sits — Awake resolves it via
// GetComponentInChildren so we don't need RequireComponent.

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Sole writer to the Player's Animator parameters. Other scripts call
    /// <see cref="SetMove(float, float)"/>; this class is the only place a
    /// parameter name string appears.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        // ── Parameter name constants (single source of truth) ───────────────
        private const string ParamMoveX       = "MoveX";
        private const string ParamMoveZ       = "MoveZ";
        private const string ParamSpeed       = "Speed";
        private const string ParamIsSprinting = "IsSprinting";
        private const string ParamAttack      = "Attack";
        private const string ParamHit         = "Hit";
        private const string ParamJump        = "Jump";
        private const string ParamIsGrounded  = "IsGrounded";

        // ── Cached state ────────────────────────────────────────────────────
        private Animator _animator;
        private int _hashMoveX;
        private int _hashMoveZ;
        private int _hashSpeed;
        private int _hashIsSprinting;
        private int _hashAttack;
        private int _hashHit;
        private int _hashJump;
        private int _hashIsGrounded;
        private bool _ready;

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Read-only access to the resolved Animator. Null if Awake hasn't
        /// run yet or no Animator was found in children.
        /// </summary>
        public Animator Animator => _animator;

        /// <summary>
        /// Writes (MoveX, MoveZ, Speed = sqrt(x² + z²)) to the Animator in a
        /// single call. Safe to call before Awake — silently dropped if the
        /// Animator is not yet resolved.
        /// </summary>
        /// <param name="moveX">Body-local strafe axis, range [-1, 1].</param>
        /// <param name="moveZ">Body-local forward axis, range [-1, 1].</param>
        public void SetMove(float moveX, float moveZ)
        {
            if (!_ready) return;
            float speed = Mathf.Sqrt(moveX * moveX + moveZ * moveZ);
            _animator.SetFloat(_hashMoveX, moveX);
            _animator.SetFloat(_hashMoveZ, moveZ);
            _animator.SetFloat(_hashSpeed, speed);
        }

        /// <summary>
        /// Writes the IsSprinting bool to the Animator. Read by the
        /// Locomotion → Sprint and Sprint → Locomotion transitions.
        /// Safe to call before Awake — silently dropped if the Animator
        /// is not yet resolved.
        /// </summary>
        /// <param name="value">True while the player is holding sprint.</param>
        public void SetSprinting(bool value)
        {
            if (!_ready) return;
            _animator.SetBool(_hashIsSprinting, value);
        }

        /// <summary>
        /// Fires the Attack trigger. The Animator transitions from
        /// Idle / Locomotion / Sprint to the Attack state and plays Attack01
        /// once. Auto-cleared by the Animator if no transition consumes it
        /// within one update. Safe to call before Awake — silently dropped
        /// if the Animator is not yet resolved.
        /// </summary>
        public void SetAttackTrigger()
        {
            if (!_ready) return;
            _animator.SetTrigger(_hashAttack);
        }

        /// <summary>
        /// Fires the Hit trigger. The Animator transitions from Any State
        /// to the Hit state and plays GetHit01 once, interrupting whatever
        /// state is currently active (including Attack). canTransitionToSelf
        /// is true on the Hit transition, so consecutive calls restart the
        /// reaction. Safe to call before Awake — silently dropped if the
        /// Animator is not yet resolved.
        /// </summary>
        public void SetHitTrigger()
        {
            if (!_ready) return;
            _animator.SetTrigger(_hashHit);
        }

        /// <summary>
        /// Fires the Jump trigger. The Animator transitions from
        /// Idle / Locomotion / Sprint to JumpStart via N7/N8/N9 if
        /// <c>IsGrounded</c> is true. Auto-cleared by the Animator if no
        /// transition consumes it within one update — so a press during
        /// JumpStart / JumpAir / JumpEnd / Attack / Hit produces no state
        /// change and no leaked queued trigger. Safe to call before Awake —
        /// silently dropped if the Animator is not yet resolved.
        /// </summary>
        public void SetJumpTrigger()
        {
            if (!_ready) return;
            _animator.SetTrigger(_hashJump);
        }

        /// <summary>
        /// Writes the IsGrounded bool to the Animator. PlayerController calls
        /// this with the current <c>CharacterController.isGrounded</c>
        /// reading, edge-detected on the caller side to avoid per-frame
        /// SetBool spam. Drives N10 (JumpStart→JumpAir on
        /// <c>!IsGrounded</c>) and N12 (JumpAir→JumpEnd on
        /// <c>IsGrounded</c>). Safe to call before Awake — silently dropped
        /// if the Animator is not yet resolved.
        /// </summary>
        /// <param name="grounded">True while CharacterController.isGrounded is true.</param>
        public void SetGrounded(bool grounded)
        {
            if (!_ready) return;
            _animator.SetBool(_hashIsGrounded, grounded);
        }

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null)
            {
                Debug.LogError($"[PlayerAnimator] No Animator found in children of '{name}'. Player will not animate.", this);
                return;
            }

            _hashMoveX       = Animator.StringToHash(ParamMoveX);
            _hashMoveZ       = Animator.StringToHash(ParamMoveZ);
            _hashSpeed       = Animator.StringToHash(ParamSpeed);
            _hashIsSprinting = Animator.StringToHash(ParamIsSprinting);
            _hashAttack      = Animator.StringToHash(ParamAttack);
            _hashHit         = Animator.StringToHash(ParamHit);
            _hashJump        = Animator.StringToHash(ParamJump);
            _hashIsGrounded  = Animator.StringToHash(ParamIsGrounded);
            _ready = true;
        }
    }
}
