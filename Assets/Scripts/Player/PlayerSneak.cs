// PlayerSneak.cs
// Single responsibility: reads IsSneaking from PlayerInputReader each frame
// and forwards it to PlayerAnimator as a Sneak bool parameter. Movement-speed
// reduction while sneaking lives in PlayerController (_sneakSpeed field).
// Animator clip assignment lives in PlayerOverride_MaleHero.overrideController
// (wired manually by the designer — not this script's concern).

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Bridges the Sneak hold-input from <see cref="PlayerInputReader.IsSneaking"/>
    /// to the Animator's Sneak bool parameter each frame via
    /// <see cref="PlayerAnimator.SetSneak"/>. Owns no movement logic and no
    /// Animator clip selection — those concerns belong to
    /// <see cref="PlayerController"/> and the override controller respectively.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAnimator))]
    [DisallowMultipleComponent]
    public class PlayerSneak : MonoBehaviour
    {
        // ── Cached refs ─────────────────────────────────────────────────────
        private PlayerInputReader _input;
        private PlayerAnimator    _animator;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _input    = GetComponent<PlayerInputReader>();
            _animator = GetComponent<PlayerAnimator>();
        }

        private void Update()
        {
            _animator.SetSneak(_input.IsSneaking);
        }
    }
}
