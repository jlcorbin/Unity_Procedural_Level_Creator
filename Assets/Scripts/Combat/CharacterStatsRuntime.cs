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
        [Tooltip("CharacterStats template (player path). Required if no EnemyData " +
                 "is pushed via InitFromEnemyData. Can be left null on enemy " +
                 "prefabs where EnemyBase pushes the values from an EnemyData asset.")]
        private CharacterStats stats;

        // M13-EnemyBase: when set, this runtime instance derives its
        // MaxHP / DisplayName from EnemyData instead of the CharacterStats
        // SO. Pushed by EnemyBase.Awake before this script's Awake fires
        // (EnemyBase runs at DefaultExecutionOrder(-50); this script
        // runs at the default 0).
        [HideInInspector] [SerializeField] private EnemyData _enemyData;

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
        // MaxHP / DisplayName: prefer EnemyData when present (enemy path),
        // fall back to CharacterStats (player path). MaxStamina has no
        // EnemyData counterpart yet — enemies don't sprint.
        public int            MaxHP          => _enemyData != null
                                                  ? _enemyData.maxHP
                                                  : (stats != null ? stats.maxHP : 0);
        public int            MaxStamina     => stats != null ? stats.maxStamina : 0;
        public string         DisplayName    => _enemyData != null
                                                  ? _enemyData.enemyName
                                                  : (stats != null ? stats.displayName : name);

        /// <summary>True after OnDied has been raised. Stays true even if Heal restores HP.</summary>
        public bool           IsDead         => _hasDied;

        /// <summary>
        /// True while incoming damage is being suppressed (M12 dodge
        /// i-frames). <see cref="ApplyDamage"/> returns early with no
        /// HP change and no <see cref="OnDied"/> emission while this
        /// is true. Sole writer: <see cref="SetInvulnerable"/>.
        /// </summary>
        public bool           IsInvulnerable { get; private set; }

        /// <summary>
        /// Toggles the i-frame flag. M12 PlayerDodge holds this true
        /// for the i-frame window. Generic so future systems (e.g.
        /// post-hit invuln, scripted cinematic invuln) can call too.
        /// </summary>
        public void SetInvulnerable(bool value) => IsInvulnerable = value;

        /// <summary>
        /// Flat defense value. effectiveDamage = max(0, damage - Defense).
        /// Pushed in from EnemyData via SetDefense, called by EnemyBase.Awake.
        /// </summary>
        public float Defense { get; private set; }

        /// <summary>
        /// Sets the flat defense value. Clamped to &gt;= 0.
        /// Sole writer: EnemyBase (via InitFromEnemyData path).
        /// </summary>
        public void SetDefense(float value) => Defense = Mathf.Max(0f, value);

        /// <summary>
        /// M13-EnemyBase entry point. Pushes an <see cref="EnemyData"/>
        /// asset into this runtime, overriding the <see cref="CharacterStats"/>
        /// values for HP and DisplayName. Called by
        /// <see cref="LevelGen.Enemy.EnemyBase"/> at its Awake (execution
        /// order -50), which runs before this script's Awake (order 0).
        /// Awake then reads <c>_enemyData</c> and initializes <c>currentHP</c>
        /// from it.
        /// </summary>
        public void InitFromEnemyData(EnemyData data)
        {
            _enemyData = data;
        }

        void Awake()
        {
            // M13-EnemyBase: if EnemyBase has pushed an EnemyData, init from it.
            // Stamina has no EnemyData equivalent — fall back to stats SO if
            // present, else 0 (enemies don't sprint).
            if (_enemyData != null)
            {
                currentHP      = _enemyData.maxHP;
                currentStamina = stats != null ? stats.maxStamina : 0;
                Debug.Log($"[CharacterStatsRuntime] {name} initialized as {DisplayName} " +
                          $"from EnemyData (HP={currentHP}/{MaxHP}, Stamina={CurrentStamina}/{MaxStamina})");
                return;
            }

            if (stats == null)
            {
                Debug.LogError($"[CharacterStatsRuntime] '{name}' has no CharacterStats asset assigned " +
                               $"and no EnemyData was pushed by an EnemyBase. Skipping init.", this);
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
            // Need at least one data source (CharacterStats for player,
            // EnemyData for enemies). Without either, MaxHP is 0 and
            // damage is meaningless. M13-EnemyBase: this guard was
            // previously `stats == null` only, which silently dropped
            // every damage event on enemies (which use the EnemyData
            // path with stats=null).
            if (stats == null && _enemyData == null) return;
            // M12: i-frames. Discard the entire damage event (no HP
            // delta, no OnDied) while invulnerable. Sole gate point —
            // do not duplicate this check elsewhere; route through here.
            if (IsInvulnerable) return;
            // Flat defense reduction. Defense is pushed by EnemyBase.Awake
            // via SetDefense; defaults to 0 on the player (no push-down).
            int effectiveDamage = Mathf.Max(0, amount - Mathf.RoundToInt(Defense));
            if (effectiveDamage <= 0) return;
            int prev = currentHP;
            currentHP = Mathf.Clamp(currentHP - effectiveDamage, 0, MaxHP);
            string defenseTag = Defense > 0f ? $" (after Defense {Defense:0.##})" : "";
            Debug.Log($"[CharacterStatsRuntime] {DisplayName} HP {prev} -> {currentHP}{defenseTag}");

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
            // Same dual-source guard as ApplyDamage (M13-EnemyBase).
            if (stats == null && _enemyData == null) return;
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
