using System;
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
    /// PlayerCombat's hitbox routing calls it on hit. It also raises
    /// <see cref="OnDied"/> exactly once when HP transitions from > 0
    /// to <= 0. <see cref="Heal"/> has no caller yet; it'll get one
    /// when potions / regen ship. Heal does NOT revive — death is
    /// one-way for now.
    ///
    /// <see cref="SpendStamina"/> / <see cref="RegenStamina"/> are the
    /// stamina mutation entry points (M9). Stored internally as float
    /// so per-frame deltas (drainRate * Time.deltaTime ≈ 0.4 at 25/s
    /// and 60fps) accumulate cleanly. <see cref="CurrentStamina"/> is
    /// reported as <c>Mathf.CeilToInt</c> so PlayerHUD's int display
    /// shows "1/100" while any stamina remains, and "0/100" only at
    /// true zero — preserving the "sprintable while non-zero" semantic.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CharacterStatsRuntime : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Template this character pulls its max values from. Required.")]
        private CharacterStats stats;

        [HideInInspector] [SerializeField] private int   currentHP;
        // M9: stamina is stored as float so sub-1 per-frame drain ticks
        // accumulate without rounding-to-zero each frame.
        [HideInInspector] [SerializeField] private float currentStamina;

        private bool _hasDied;

        /// <summary>
        /// Raised exactly once when HP transitions from &gt; 0 to &lt;= 0.
        /// Payload is this runtime instance (mirrors Targetable.OnHit's
        /// pass-self pattern). Single-fire — subsequent ApplyDamage calls
        /// post-death do not re-raise.
        /// </summary>
        public event Action<CharacterStatsRuntime> OnDied;

        public CharacterStats Stats          => stats;
        public int            CurrentHP      => currentHP;
        // CeilToInt: any stamina > 0.0 reports as >= 1, so PlayerHUD
        // shows non-zero while sprintable; only true zero reports as 0.
        public int            CurrentStamina => Mathf.CeilToInt(currentStamina);
        public int            MaxHP          => stats != null ? stats.maxHP      : 0;
        public int            MaxStamina     => stats != null ? stats.maxStamina : 0;
        public string         DisplayName    => stats != null ? stats.displayName : name;

        /// <summary>True after OnDied has been raised. Stays true even if Heal restores HP.</summary>
        public bool           IsDead         => _hasDied;

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
                      $"(HP={currentHP}/{MaxHP}, Stamina={CurrentStamina}/{MaxStamina})");
        }

        /// <summary>
        /// Reduces <see cref="CurrentHP"/> by <paramref name="amount"/>,
        /// clamped to [0, MaxHP]. Raises <see cref="OnDied"/> the first
        /// time HP reaches 0. Public so any damage source (PlayerCombat
        /// hitbox, traps, projectiles) can route into it.
        /// </summary>
        public void ApplyDamage(int amount)
        {
            if (stats == null) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP - amount, 0, MaxHP);
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}");

            if (currentHP <= 0 && !_hasDied)
            {
                // Set the flag BEFORE invoking so subscribers reading
                // IsDead from inside the handler see the post-death state.
                _hasDied = true;
                OnDied?.Invoke(this);
            }
        }

        /// <summary>
        /// Increases <see cref="CurrentHP"/> by <paramref name="amount"/>,
        /// clamped to [0, MaxHP]. Public so future heal sources (potions,
        /// regen, abilities) can call it. Does NOT revive — if called
        /// post-death, HP rises but <see cref="IsDead"/> stays true and
        /// <see cref="OnDied"/> does not re-fire.
        /// </summary>
        public void Heal(int amount)
        {
            if (stats == null) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP + amount, 0, MaxHP);
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}");
        }

        /// <summary>
        /// Reduce current stamina by <paramref name="amount"/>, clamped to
        /// >= 0. No-op if amount &lt;= 0. Mutates the underlying float
        /// directly — callers can pass per-frame deltas like
        /// <c>drainRate * Time.deltaTime</c> without rounding artifacts.
        /// Sole writer: PlayerStamina (M9).
        /// </summary>
        public void SpendStamina(float amount)
        {
            if (stats == null) return;
            if (amount <= 0f) return;
            currentStamina = Mathf.Max(0f, currentStamina - amount);
        }

        /// <summary>
        /// Increase current stamina by <paramref name="amount"/>, clamped
        /// to maxStamina. No-op if amount &lt;= 0. Mutates the float
        /// directly. Sole writer: PlayerStamina (M9).
        /// </summary>
        public void RegenStamina(float amount)
        {
            if (stats == null) return;
            if (amount <= 0f) return;
            currentStamina = Mathf.Min(MaxStamina, currentStamina + amount);
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

        // TODO removeMe-after-real-damage-sources-kill-things: forces
        // immediate death for smoke-testing the M4-B Death pipeline
        // (Dummy is currently 999 HP). Removable once an enemy AI / heavy
        // weapon makes organic death easy to reproduce.
        [ContextMenu("Debug: Kill")]
        private void DebugKill() { ApplyDamage(99999); }
    }
}
