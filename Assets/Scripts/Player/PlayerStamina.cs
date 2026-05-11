// PlayerStamina.cs — drains stamina while sprinting, regenerates otherwise (M9).
//
// Per-frame Update reads PlayerController.IsSprintingNow:
//   sprinting + stamina>0 → SpendStamina(drainRate * dt); flip CanSprint
//                           false on hitting 0
//   not sprinting         → RegenStamina(regenRate * dt); flip CanSprint
//                           true once stamina rises above 0
//
// Single-direction wiring: PlayerController PULLS CanSprint from this
// component each frame; this component PULLS sprint state from
// PlayerController. PlayerStamina never writes to the controller; the
// controller never writes to stamina.
//
// Single-writer-to-stamina invariant: CharacterStatsRuntime is the
// only field-mutator. PlayerStamina calls SpendStamina/RegenStamina;
// it does not write `currentStamina` directly.

using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Owns the per-frame stamina drain / regen for the player. Exposes
    /// <see cref="CanSprint"/> so <see cref="PlayerController"/> can gate
    /// sprint engagement on stamina availability without coupling to the
    /// stats layer directly.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerDeath))]
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        private CharacterStatsRuntime _stats;
        private PlayerController      _controller;
        private PlayerDeath           _death; // optional — null-tolerant

        /// <summary>
        /// True when the player has stamina to spend. Goes false the
        /// moment stamina hits 0 mid-sprint; goes back true the moment
        /// regen lifts stamina above 0. Read by
        /// <see cref="PlayerController"/> as the third clause of the
        /// sprint-engagement check.
        /// </summary>
        public bool CanSprint { get; private set; } = true;

        private void Awake()
        {
            _stats      = GetComponent<CharacterStatsRuntime>();
            _controller = GetComponent<PlayerController>();
            _death      = GetComponent<PlayerDeath>();
        }

        private void OnEnable()
        {
            // Belt-and-suspenders cleanup on death — PlayerDeath also
            // disables PlayerController + PlayerCombat, but stopping
            // PlayerStamina explicitly avoids stamina silently
            // regenerating while the death overlay is up.
            if (_death != null) _death.OnPlayerDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            if (_death != null) _death.OnPlayerDied -= HandlePlayerDied;
        }

        private void Update()
        {
            if (_stats == null || _stats.Stats == null) return;
            var so = _stats.Stats; // ScriptableObject ref
            float drain = so.StaminaDrainPerSecond;
            float regen = so.StaminaRegenPerSecond;

            bool sprintingNow = _controller != null && _controller.IsSprintingNow;
            // CurrentStamina is CeilToInt of the precise float, so > 0
            // here matches the float-precise "any stamina remaining"
            // semantic. At true 0 both report 0.
            if (sprintingNow && _stats.CurrentStamina > 0)
            {
                _stats.SpendStamina(drain * Time.deltaTime);
                if (_stats.CurrentStamina <= 0)
                {
                    // Hit empty — flag the controller to drop sprint
                    // next frame even if Shift is still held.
                    CanSprint = false;
                }
            }
            else
            {
                _stats.RegenStamina(regen * Time.deltaTime);
                if (_stats.CurrentStamina > 0 && !CanSprint)
                {
                    CanSprint = true;
                }
            }
        }

        private void HandlePlayerDied(PlayerDeath _)
        {
            // Disable self so Update stops running while dead.
            // CharacterStatsRuntime is preserved (HUD still binds);
            // we just stop mutating its stamina.
            enabled = false;
        }
    }
}
