// EquipSlot.cs — equipment slot enum for the M16 item data layer.
//
// Pure enum — no MonoBehaviour, no ScriptableObject.
// Used by ItemData to declare which character slot an item occupies.

namespace LevelGen.Items
{
    /// <summary>
    /// Identifies which equipment slot an <see cref="ItemData"/> occupies.
    /// Each value maps to a distinct character equipment slot.
    /// </summary>
    public enum EquipSlot
    {
        /// <summary>Right-hand melee weapon (sword, axe, etc.).</summary>
        Melee,

        /// <summary>Off-hand item — shield or secondary weapon.</summary>
        OffHand,

        /// <summary>Ranged weapon — bow, crossbow, or thrown weapon.</summary>
        Ranged,

        /// <summary>Body armour / chest piece.</summary>
        Armor,
    }
}
