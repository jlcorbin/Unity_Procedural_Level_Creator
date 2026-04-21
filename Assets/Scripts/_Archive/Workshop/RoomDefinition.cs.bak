using System;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Plain serializable data class that fully describes a room's layout parameters.
    /// Used by RoomBuilder to construct the room and stored in RoomPreset for replay.
    /// </summary>
    [Serializable]
    public class RoomDefinition
    {
        // ── Size ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-defined size category.
        /// Small = 4×4, Medium = 8×8, Large = 12×12 (world units, snap = 2).
        /// Use Custom to enter exact tile counts.
        /// </summary>
        public enum SizePreset { Small, Medium, Large, Custom }

        [Tooltip("Room footprint preset. Custom enables the fields below.")]
        public SizePreset sizePreset = SizePreset.Medium;

        [Tooltip("Width in grid tiles (snap units). Used only when sizePreset = Custom.")]
        [Min(1)] public int customWidth = 4;

        [Tooltip("Depth in grid tiles (snap units). Used only when sizePreset = Custom.")]
        [Min(1)] public int customDepth = 4;

        // ── Exit walls ────────────────────────────────────────────────────────

        [Tooltip("The North wall has an exit connection point.")]
        public bool exitNorth;

        [Tooltip("The South wall has an exit connection point.")]
        public bool exitSouth;

        [Tooltip("The East wall has an exit connection point.")]
        public bool exitEast;

        [Tooltip("The West wall has an exit connection point.")]
        public bool exitWest;

        [Tooltip("Exit through the ceiling (vertical connection, upward).")]
        public bool exitUp;

        [Tooltip("Exit through the floor (vertical connection, downward).")]
        public bool exitDown;

        // ── Doorway visuals ───────────────────────────────────────────────────

        [Tooltip("Replace the centre wall tiles on the North exit with a Doorway prefab.")]
        public bool doorwayNorth;

        [Tooltip("Replace the centre wall tiles on the South exit with a Doorway prefab.")]
        public bool doorwaySouth;

        [Tooltip("Replace the centre wall tiles on the East exit with a Doorway prefab.")]
        public bool doorwayEast;

        [Tooltip("Replace the centre wall tiles on the West exit with a Doorway prefab.")]
        public bool doorwayWest;

        // ── Ceiling ───────────────────────────────────────────────────────────

        [Tooltip("Fill the ceiling with Ceiling prefab tiles at ROOM_HEIGHT above the floor.")]
        public bool includeCeiling = false;

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a deep copy of this definition.
        /// </summary>
        public RoomDefinition Clone()
        {
            return new RoomDefinition
            {
                sizePreset    = sizePreset,
                customWidth   = customWidth,
                customDepth   = customDepth,
                exitNorth     = exitNorth,
                exitSouth     = exitSouth,
                exitEast      = exitEast,
                exitWest      = exitWest,
                exitUp        = exitUp,
                exitDown      = exitDown,
                doorwayNorth  = doorwayNorth,
                doorwaySouth  = doorwaySouth,
                doorwayEast   = doorwayEast,
                doorwayWest   = doorwayWest,
                includeCeiling = includeCeiling,
            };
        }
    }
}
