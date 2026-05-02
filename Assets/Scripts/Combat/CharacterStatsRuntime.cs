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
    /// <see cref="ApplyDamage"/> is the public damage entry point —
    /// PlayerCombat's hitbox routing calls it on hit. <see cref="Heal"/>
    /// has no caller yet; it'll get one when potions / regen ship.
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
        /// clamped to [0, MaxHP]. Public so any damage source (PlayerCombat
        /// hitbox, traps, projectiles) can route into it.
        /// </summary>
        public void ApplyDamage(int amount)
        {
            if (stats == null) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP - amount, 0, MaxHP);
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}");
        }

        /// <summary>
        /// Increases <see cref="CurrentHP"/> by <paramref name="amount"/>,
        /// clamped to [0, MaxHP]. Public so future heal sources (potions,
        /// regen, abilities) can call it.
        /// </summary>
        public void Heal(int amount)
        {
            if (stats == null) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP + amount, 0, MaxHP);
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}");
        }

        // TODO removeMe-after-stamina-and-heal-sources-exist: Inspector
        // hooks for HUD verification. The HUD's lerp-on-heal currently has
        // no other test surface, and a self-contained Inspector path for
        // damage is convenient when the player isn't swinging at the test
        // dummy. Both go away when stamina + heal sources land.
        [ContextMenu("Debug: Apply 10 Damage")]
        private void DebugApplyDamage10() { ApplyDamage(10); }

        [ContextMenu("Debug: Heal 10")]
        private void DebugHeal10() { Heal(10); }
    }
}
