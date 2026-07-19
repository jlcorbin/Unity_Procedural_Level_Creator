namespace LevelGen.Player
{
    /// <summary>
    /// The eight weapon stances ported from the UE5 <c>BP_RPG_PlayerCharacter</c>
    /// (spec §6). The integer values are canonical: they are written directly to
    /// the Animator's <c>WeaponType</c> int parameter to select each stance's
    /// locomotion / attack / dodge sub-states, and index the
    /// <see cref="StanceDefinition"/> table.
    ///
    /// Order and values MUST match the UE5 spec — <c>Q</c> cycles
    /// <c>(CurrentStance + 1) % 8</c>, and stance 7 (Bow) is treated specially
    /// (skinned bow rig). Stances 6 (Wand) and 7 (Bow) are ranged.
    /// </summary>
    public enum Stance
    {
        /// <summary>0 — unarmed.</summary>
        NoWeapon = 0,
        /// <summary>1 — one-handed sword (default on spawn per spec).</summary>
        SingleSword = 1,
        /// <summary>2 — two-handed sword (clips use the <c>_THS</c> suffix).</summary>
        TwoHandsSword = 2,
        /// <summary>3 — sword + shield (idle clip has the <c>Shiled</c> pack typo).</summary>
        SwordAndShield = 3,
        /// <summary>4 — dual wield.</summary>
        DoubleSword = 4,
        /// <summary>5 — spear (right-hand roll +10 fix).</summary>
        Spear = 5,
        /// <summary>6 — magic wand (ranged; charge/release).</summary>
        MagicWand = 6,
        /// <summary>7 — bow (ranged; skinned bow rig + nocked arrow).</summary>
        BowAndArrow = 7
    }

    /// <summary>Helpers for <see cref="Stance"/>.</summary>
    public static class StanceExtensions
    {
        /// <summary>Total stance count — the modulus for the <c>Q</c> cycle.</summary>
        public const int Count = 8;

        /// <summary>Ranged stances (spec §8): MagicWand (6) and BowAndArrow (7).</summary>
        public static bool IsRanged(this Stance s) =>
            s == Stance.MagicWand || s == Stance.BowAndArrow;

        /// <summary>Advance one step with wraparound, matching UE's <c>(n + 1) % 8</c>.</summary>
        public static Stance Next(this Stance s) =>
            (Stance)(((int)s + 1) % Count);
    }
}
