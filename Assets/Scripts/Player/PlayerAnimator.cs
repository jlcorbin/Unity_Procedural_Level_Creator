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

        // ── Cached state ────────────────────────────────────────────────────
        private Animator _animator;
        private int _hashMoveX;
        private int _hashMoveZ;
        private int _hashSpeed;
        private int _hashIsSprinting;
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
            _ready = true;
        }
    }
}
