using UnityEngine;

namespace LevelGen.Combat
{
    /// <summary>
    /// Runtime instance for a <see cref="CharacterStats"/> template. Copies
    /// max values into <see cref="CurrentHP"/> / <see cref="CurrentStamina"/>
    /// at Awake. Other systems read these via the public properties; the
    /// underlying SO is never mutated at runtime.
    /// </summary>
    /// <remarks>
    /// <see cref="ApplyDamage"/> and <see cref="Heal"/> are scaffolding —
    /// they compile but are not called from anywhere yet. Damage routing
    /// from PlayerCombat / Targetable is a future milestone.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CharacterStatsRuntime : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Template this character pulls its max values from. Required.")]
        private CharacterStats stats;

        [HideInInspector] [SerializeField] private int currentHP;
        [HideInInspector] [SerializeField] private int currentStamina;

        public CharacterStats Stats          => stats;
        public int            CurrentHP      => currentHP;
        public int            CurrentStamina => currentStamina;
        public int            MaxHP          => stats != null ? stats.maxHP      : 0;
        public int            MaxStamina     => stats != null ? stats.maxStamina : 0;
        public string         DisplayName    => stats != null ? stats.displayName : name;

        void Awake()
        {
            if (stats == null)
            {
                Debug.LogError($"[CharacterStatsRuntime] '{name}' has no CharacterStats asset assigned. " +
                               $"Skipping init.", this);
                return;
            }

            currentHP      = stats.maxHP;
            currentStamina = stats.maxStamina;

            Debug.Log($"[CharacterStatsRuntime] {name} initialized as {DisplayName} " +
                      $"(HP={currentHP}/{MaxHP}, Stamina={currentStamina}/{MaxStamina})");
        }

        /// <summary>
        /// Reduces <see cref="CurrentHP"/> by <paramref name="amount"/>,
        /// clamped to [0, MaxHP]. Marked internal — only systems in the
        /// LevelGen.Combat assembly may call it (no external callers yet).
        /// </summary>
        internal void ApplyDamage(int amount)
        {
            if (stats == null) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP - amount, 0, MaxHP);
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}");
        }

        /// <summary>
        /// Increases <see cref="CurrentHP"/> by <paramref name="amount"/>,
        /// clamped to [0, MaxHP]. Marked internal — scaffolding for future
        /// healing systems.
        /// </summary>
        internal void Heal(int amount)
        {
            if (stats == null) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP + amount, 0, MaxHP);
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}");
        }
    }
}
