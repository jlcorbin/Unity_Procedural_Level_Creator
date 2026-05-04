using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Reusable character data template. Holds max HP / max Stamina /
    /// stamina drain + regen rates / display name / free-form description.
    /// Created as an asset and duplicated per character type — the master
    /// asset is never assigned directly, only duplicated and tweaked.
    /// </summary>
    /// <remarks>
    /// Pure config asset — no runtime mutation. Spawned characters reference
    /// the SO via <see cref="CharacterStatsRuntime"/>, which copies the max
    /// values into runtime fields at Awake. Multiple characters can safely
    /// share one asset.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "LevelGen/Combat/Character Stats",
        fileName = "CharacterStats_New",
        order    = 100)]
    public class CharacterStats : ScriptableObject
    {
        [Tooltip("Inspector label / future UI source.")]
        public string displayName = "Character";

        [Tooltip("Max health. Current health lives on the runtime component, not here.")]
        public int maxHP = 100;

        [Tooltip("Max stamina. Current stamina lives on the runtime component.")]
        public int maxStamina = 100;

        [TextArea(3, 6)]
        [Tooltip("Free-form notes for the author.")]
        public string description = "";

        [Header("Stamina (gameplay)")]
        [Tooltip("Stamina spent per second while actively draining (e.g. sprinting). " +
                 "0 = no drain. Per-character tunable; starting default 25 = 4s from " +
                 "full-to-empty at maxStamina=100.")]
        [SerializeField] private float _staminaDrainPerSecond = 25f;

        [Tooltip("Stamina regenerated per second while not draining. 0 = no regen. " +
                 "Per-character tunable; starting default 33 = ~3s from empty-to-full " +
                 "at maxStamina=100. Asymmetric (faster than drain) on purpose — " +
                 "matches modern action-game conventions.")]
        [SerializeField] private float _staminaRegenPerSecond = 33f;

        public float StaminaDrainPerSecond => _staminaDrainPerSecond;
        public float StaminaRegenPerSecond => _staminaRegenPerSecond;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (maxHP < 1)
            {
                Debug.LogWarning($"[CharacterStats] '{name}' maxHP was {maxHP}; clamped to 1.");
                maxHP = 1;
            }
            if (maxStamina < 1)
            {
                Debug.LogWarning($"[CharacterStats] '{name}' maxStamina was {maxStamina}; clamped to 1.");
                maxStamina = 1;
            }
            // Negative regen would be a bug; negative drain would semantically
            // invert the system. Clamp both at floor 0.
            if (_staminaDrainPerSecond < 0f) _staminaDrainPerSecond = 0f;
            if (_staminaRegenPerSecond < 0f) _staminaRegenPerSecond = 0f;
        }
#endif
    }
}
