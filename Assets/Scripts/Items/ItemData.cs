// ItemData.cs — per-item ScriptableObject data template (M16).
//
// One asset per item archetype. Holds all static data: identity,
// stats, rarity, icon, and world prefab reference. No gameplay logic
// lives here — consumers (WorldItem, PlayerInventory, future
// WeaponStats) read the public properties.
//
// Create via: Hub & Hollow / Item Data
// (Assets/Data/Items/ is the recommended asset folder.)

using UnityEngine;

namespace LevelGen.Items
{
    /// <summary>
    /// Rarity tier for an item. Controls loot table weights, UI colour
    /// coding, and the placeholder-pickup block colour. Also the ladder a
    /// weapon climbs as it is upgraded (Sword → Sword +3 = Legendary).
    /// </summary>
    public enum ItemRarity
    {
        /// <summary>Standard drop — most common.</summary>
        Common,

        /// <summary>Above-average drop — moderate frequency.</summary>
        Uncommon,

        /// <summary>High-value drop — rarely found.</summary>
        Rare,

        /// <summary>Top tier — very rare.</summary>
        Legendary,
    }

    /// <summary>
    /// Broad category of an item. Gear is equippable and unique; Material is a
    /// stackable upgrade resource (Mat 1/2/3) consumed by the weapon-upgrade
    /// system and never equipped.
    /// </summary>
    public enum ItemKind
    {
        /// <summary>Equippable gear (weapon, off-hand, armor) — unique.</summary>
        Gear,

        /// <summary>Stackable upgrade material — never equipped.</summary>
        Material,
    }

    /// <summary>Bright, light-UI-friendly colours per rarity (placeholder blocks + future UI).</summary>
    public static class RarityColors
    {
        /// <summary>Returns the block/UI colour for a rarity tier.</summary>
        public static Color For(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common    => new Color(0.85f, 0.85f, 0.85f), // light grey
            ItemRarity.Uncommon  => new Color(0.35f, 0.82f, 0.38f), // green
            ItemRarity.Rare      => new Color(0.34f, 0.58f, 1.00f), // blue
            ItemRarity.Legendary => new Color(1.00f, 0.74f, 0.20f), // gold
            _                    => Color.white,
        };
    }

    /// <summary>
    /// Immutable data record for a single item archetype. Create one
    /// asset per item type (e.g. <c>ItemData_LightSword.asset</c>).
    /// The <see cref="Id"/> field is the stable key used by
    /// <see cref="ItemDatabase.GetById"/> — treat it like a primary key;
    /// do not change it after items are saved to scene data.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemData_New", menuName = "Hub & Hollow/Item Data")]
    public class ItemData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Tooltip("Unique string key used by ItemDatabase.GetById. " +
                 "Use snake_case (e.g. 'light_sword'). " +
                 "Do NOT change after items have been persisted to save data.")]
        [SerializeField] private string _id;

        [Tooltip("Name shown in UI (e.g. 'Light Sword').")]
        [SerializeField] private string _displayName;

        [Tooltip("Flavour text shown in item tooltip.")]
        [SerializeField] private string _description;

        // ── Category ──────────────────────────────────────────────────────────

        [Tooltip("Gear (equippable, unique) or Material (stackable upgrade resource).")]
        [SerializeField] private ItemKind _kind = ItemKind.Gear;

        // ── Equipment (Gear only) ─────────────────────────────────────────────

        [Tooltip("Equipment slot this item occupies (Gear only).")]
        [SerializeField] private EquipSlot _slot;

        [Tooltip("Base damage value. Used by WeaponStats in the next milestone.")]
        [SerializeField] private int _damage;

        [Tooltip("Minimum player level to equip (0 = no requirement).")]
        [SerializeField] private int _requiredLevel;

        // ── Presentation ──────────────────────────────────────────────────────

        [Tooltip("2D icon shown in inventory UI slots.")]
        [SerializeField] private Sprite _icon;

        [Tooltip("Prefab instantiated in the world as a pickup. " +
                 "Assigned by level designers on WorldItem prefabs; " +
                 "may be null if the item has no world representation.")]
        [SerializeField] private GameObject _worldPrefab;

        [Tooltip("Common / Uncommon / Rare.")]
        [SerializeField] private ItemRarity _rarity;

        // ── Public read-only properties ────────────────────────────────────────

        /// <summary>
        /// Unique string key for database lookups. Treat as immutable after
        /// save-data has been written.
        /// </summary>
        public string Id => _id;

        /// <summary>Human-readable name displayed in the inventory UI.</summary>
        public string DisplayName => _displayName;

        /// <summary>Flavour / lore text displayed in the item tooltip.</summary>
        public string Description => _description;

        /// <summary>Gear (equippable, unique) or Material (stackable upgrade resource).</summary>
        public ItemKind Kind => _kind;

        /// <summary>True when this item is a stackable upgrade material (never equipped).</summary>
        public bool IsMaterial => _kind == ItemKind.Material;

        /// <summary>The equipment slot this item occupies on the character (Gear only).</summary>
        public EquipSlot Slot => _slot;

        /// <summary>
        /// Base damage value. Consumed by WeaponStats (next milestone);
        /// ignored by inventory and pickup logic.
        /// </summary>
        public int Damage => _damage;

        /// <summary>
        /// Minimum player level required to equip. 0 means no restriction.
        /// </summary>
        public int RequiredLevel => _requiredLevel;

        /// <summary>2D sprite icon displayed in inventory slot UI.</summary>
        public Sprite Icon => _icon;

        /// <summary>
        /// Optional world-space pickup prefab. May be null for items that
        /// are granted programmatically and never exist as world objects.
        /// </summary>
        public GameObject WorldPrefab => _worldPrefab;

        /// <summary>Rarity tier — drives loot table weights and UI colour.</summary>
        public ItemRarity Rarity => _rarity;
    }
}
