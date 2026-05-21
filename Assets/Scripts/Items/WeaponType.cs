// WeaponType.cs — combat animation category for per-weapon combo routing (M21).
//
// Resolved at swing time from the player's equipped ItemData via
// WeaponTypeResolver.Resolve. Drives which Animator combo chain fires.

namespace LevelGen.Items
{
    /// <summary>
    /// Combat animation category resolved from the equipped weapon at swing
    /// time. Drives which Animator combo chain fires.
    /// </summary>
    public enum WeaponType
    {
        /// <summary>No melee weapon equipped (bare fists / shield only).</summary>
        Unarmed    = 0,

        /// <summary>One-handed weapon, no shield in off-hand.</summary>
        OHS        = 1,

        /// <summary>One-handed weapon + shield in off-hand.</summary>
        OHSShield  = 2,

        /// <summary>Two-handed sword.</summary>
        THS        = 3,

        /// <summary>Spear / polearm.</summary>
        Spear      = 4,
    }
}
