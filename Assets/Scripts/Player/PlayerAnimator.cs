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
using LevelGen.Items;

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
        private const string ParamComboNext   = "ComboNext";
        private const string ParamDeath       = "Death";
        private const string ParamDodgeTrigger   = "DodgeTrigger";
        private const string ParamDodgeDirection = "DodgeDirection";
        private const string ParamSneak          = "Sneak";
        private const string ParamWeaponType     = "WeaponType";
        // M22: float mirror of WeaponType. Blend trees (loco / dodge) require a
        // FLOAT blend parameter, but the Attack-entry transitions need an INT for
        // "Equals" conditions — so we drive both from the stance index.
        private const string ParamStanceBlend    = "StanceBlend";

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
        private int _hashComboNext;
        private int _hashDeath;
        private int _hashDodgeTrigger;
        private int _hashDodgeDirection;
        private int _hashSneak;
        private int _hashWeaponType;
        private int _hashStanceBlend;
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

        /// <summary>
        /// Fires the ComboNext trigger. The Animator routes this via N14
        /// (Attack → Attack02) or N15 (Attack02 → Attack03) at the next
        /// exit-time evaluation if the current state's normalizedTime has
        /// reached 0.85. Auto-clears within one Animator update if not
        /// consumed — discarding stale combo intent if the state changes
        /// (e.g., interrupted by Hit). Called only by PlayerCombat.Update
        /// at the buffer-consume threshold; never from input handlers or
        /// TakeHit. Safe to call before Awake — silently dropped if the
        /// Animator is not yet resolved.
        /// </summary>
        public void SetComboNext()
        {
            if (!_ready) return;
            _animator.SetTrigger(_hashComboNext);
        }

        /// <summary>
        /// Fires the Death trigger. The Animator transitions from Any State
        /// to the terminal Death state and plays Die01 once, parking on the
        /// last frame (Death has no outgoing transitions).
        /// canTransitionToSelf is false so a leaked second trigger can't
        /// restart the clip; PlayerDeath also guards with `_hasFired`.
        /// Safe to call before Awake — silently dropped if the Animator is
        /// not yet resolved.
        /// </summary>
        public void SetDeathTrigger()
        {
            if (!_ready) return;
            _animator.SetTrigger(_hashDeath);
        }

        /// <summary>
        /// Writes the DodgeDirection int (0=FWD, 1=BWD, 2=LFT, 3=RGT)
        /// to the Animator. Called by PlayerDodge immediately before
        /// <see cref="SetDodgeTrigger"/> so the AnyState→Roll{X}
        /// transitions evaluate their direction condition correctly.
        /// Single-writer-per-parameter invariant: only PlayerDodge
        /// drives this through here. Safe to call before Awake.
        /// </summary>
        /// <param name="direction">0=FWD, 1=BWD, 2=LFT, 3=RGT.</param>
        public void SetDodgeDirection(int direction)
        {
            if (!_ready) return;
            _animator.SetInteger(_hashDodgeDirection, direction);
        }

        /// <summary>
        /// Fires the DodgeTrigger. With DodgeDirection set, the
        /// Animator picks one of AnyState→RollFWD/BWD/LFT/RGT and
        /// plays that clip; auto-clears within one Animator update
        /// if not consumed. Safe to call before Awake — silently
        /// dropped if the Animator is not yet resolved.
        /// </summary>
        public void SetDodgeTrigger()
        {
            if (!_ready) return;
            _animator.SetTrigger(_hashDodgeTrigger);
        }

        /// <summary>
        /// Sets the Sneak bool on the Animator. Called each frame by
        /// <see cref="PlayerSneak"/> to keep the Animator in sync with
        /// the sneak input state. Safe to call before Awake — silently
        /// dropped if the Animator is not yet resolved.
        /// </summary>
        /// <param name="isSneaking">True while the Sneak key is held.</param>
        public void SetSneak(bool isSneaking)
        {
            if (!_ready) return;
            _animator.SetBool(_hashSneak, isSneaking);
        }

        /// <summary>
        /// Writes the <c>WeaponType</c> int parameter so the Animator routes
        /// the Attack trigger to the correct per-type combo chain.
        /// Call once at swing-start, before <see cref="SetAttackTrigger"/>.
        /// </summary>
        /// <param name="weaponType">The resolved weapon type for this swing.</param>
        public void SetWeaponType(WeaponType weaponType)
        {
            if (!_ready) return;
            _animator.SetInteger(_hashWeaponType, (int)weaponType);
        }

        /// <summary>
        /// Writes the raw stance index (0–7, see <see cref="Stance"/>) to the
        /// same <c>WeaponType</c> Animator int parameter.
        ///
        /// M22: the stance system (spec §6) generalizes the 5-value
        /// <see cref="WeaponType"/> routing to 8 stances. <see cref="StanceController"/>
        /// is the sole writer of the stance index via this method; the per-swing
        /// <see cref="SetWeaponType"/> path is migrated to defer to the active
        /// stance in P4. The Animator param name stays <c>WeaponType</c> so the
        /// existing per-type Attack sub-states keep resolving.
        /// </summary>
        /// <param name="stanceIndex">Canonical <see cref="Stance"/> value cast to int, 0–7.</param>
        public void SetStanceIndex(int stanceIndex)
        {
            if (!_ready) return;
            _animator.SetInteger(_hashWeaponType, stanceIndex);   // int → Attack-entry Equals conditions
            _animator.SetFloat(_hashStanceBlend, stanceIndex);    // float → loco / dodge blend trees
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
            _hashComboNext   = Animator.StringToHash(ParamComboNext);
            _hashDeath       = Animator.StringToHash(ParamDeath);
            _hashDodgeTrigger   = Animator.StringToHash(ParamDodgeTrigger);
            _hashDodgeDirection = Animator.StringToHash(ParamDodgeDirection);
            _hashSneak          = Animator.StringToHash(ParamSneak);
            _hashWeaponType     = Animator.StringToHash(ParamWeaponType);
            _hashStanceBlend    = Animator.StringToHash(ParamStanceBlend);
            _ready = true;
        }
    }
}
