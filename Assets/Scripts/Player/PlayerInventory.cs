// PlayerInventory.cs — player inventory component (M16).
//
// Holds the ordered list of items the player is carrying. Singleton-
// scoped (per-scene, no DontDestroyOnLoad) so WorldItem.Execute can
// reach it via PlayerInventory.Instance without a hard reference.
//
// [DefaultExecutionOrder(-30)] — runs Awake after EnemyBase (-50) and
// TargetLock (-40), but before default-order components (0), ensuring
// Instance is non-null when any sibling or spawned object queries it.

using System.Collections.Generic;
using UnityEngine;
using LevelGen.Items;

namespace LevelGen.Player
{
    /// <summary>
    /// Manages the player's item collection. Exposes <see cref="AddItem"/>,
    /// <see cref="RemoveItem"/>, and <see cref="Items"/> for external consumers.
    /// Fires <see cref="OnItemAdded"/> and <see cref="OnItemRemoved"/> events so
    /// inventory UI can react without polling.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30)]
    public class PlayerInventory : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        /// <summary>
        /// Per-scene singleton accessor. Set during Awake; cleared in
        /// OnDestroy. A second instance self-destructs with a warning —
        /// the project assumes a single Player.
        /// </summary>
        public static PlayerInventory Instance { get; private set; }

        // ── Inspector ──────────────────────────────────────────────────────────

        [Tooltip("Maximum number of items the inventory can hold.")]
        [SerializeField] private int _capacity = 20;

        // ── Runtime state ──────────────────────────────────────────────────────

        private readonly List<ItemData> _items = new List<ItemData>();
        private readonly Dictionary<EquipSlot, ItemData> _equipped =
            new Dictionary<EquipSlot, ItemData>();
        // Stackable upgrade materials (ItemKind.Material) — kept separate from the
        // unique gear list. Key = material ItemData, value = quantity held.
        private readonly Dictionary<ItemData, int> _materials =
            new Dictionary<ItemData, int>();

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired after an item is successfully added to the inventory.
        /// The payload is the item that was added.
        /// </summary>
        public event System.Action<ItemData> OnItemAdded;

        /// <summary>
        /// Fired after an item is successfully removed from the inventory.
        /// The payload is the item that was removed.
        /// </summary>
        public event System.Action<ItemData> OnItemRemoved;

        /// <summary>
        /// Fired when any slot's equipped item changes.
        /// Payload: the newly equipped item, or null if unequipped.
        /// </summary>
        public event System.Action<ItemData> OnWeaponEquipped;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerInventory] Duplicate instance detected — destroying.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to add <paramref name="item"/> to the inventory.
        /// Returns <c>true</c> on success, <c>false</c> if the item is null
        /// or the inventory is at capacity.
        /// Fires <see cref="OnItemAdded"/> on success.
        /// </summary>
        /// <param name="item">The item to add. Null is a silent no-op.</param>
        /// <param name="count">How many to add (materials stack; gear adds copies). Default 1.</param>
        public bool AddItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            // Materials stack in a separate dictionary (no per-slot capacity).
            if (item.IsMaterial)
            {
                _materials.TryGetValue(item, out int have);
                _materials[item] = have + count;
                OnItemAdded?.Invoke(item);
                return true;
            }

            // Gear is unique and capacity-limited.
            bool addedAny = false;
            for (int i = 0; i < count; i++)
            {
                if (_items.Count >= _capacity) break;
                _items.Add(item);
                OnItemAdded?.Invoke(item);
                addedAny = true;
            }
            return addedAny;
        }

        /// <summary>
        /// Attempts to remove the first occurrence of <paramref name="item"/>
        /// from the inventory. Returns <c>true</c> on success, <c>false</c>
        /// if the item is null or not present.
        /// Fires <see cref="OnItemRemoved"/> on success.
        /// </summary>
        /// <param name="item">The item to remove. Null is a silent no-op.</param>
        public bool RemoveItem(ItemData item)
        {
            if (item == null) return false;
            int idx = _items.IndexOf(item);
            if (idx < 0) return false;
            _items.RemoveAt(idx);
            OnItemRemoved?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="item"/> is currently
        /// in the inventory.
        /// </summary>
        /// <param name="item">The item to test.</param>
        public bool HasItem(ItemData item)
        {
            return item != null && _items.Contains(item);
        }

        /// <summary>
        /// Read-only view of the current inventory contents, in insertion order.
        /// </summary>
        public IReadOnlyList<ItemData> Items => _items;

        /// <summary>Current number of gear items in the inventory (materials excluded).</summary>
        public int Count => _items.Count;

        /// <summary>Quantity of a stackable material held (0 if none).</summary>
        /// <param name="item">The material to query.</param>
        public int GetMaterialCount(ItemData item)
            => (item != null && _materials.TryGetValue(item, out int n)) ? n : 0;

        /// <summary>Read-only view of all held materials and their counts.</summary>
        public IReadOnlyDictionary<ItemData, int> Materials => _materials;

        /// <summary>
        /// Consumes <paramref name="amount"/> of a material (for the future weapon
        /// upgrade system). Returns false if fewer than <paramref name="amount"/>
        /// are held. Fires <see cref="OnItemRemoved"/> on success.
        /// </summary>
        public bool SpendMaterial(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return false;
            if (!_materials.TryGetValue(item, out int have) || have < amount) return false;
            int left = have - amount;
            if (left > 0) _materials[item] = left;
            else _materials.Remove(item);
            OnItemRemoved?.Invoke(item);
            return true;
        }

        /// <summary>
        /// Equips the item into its slot. Replaces any previously equipped
        /// item in that slot (does not return it to inventory — caller's
        /// responsibility). Fires <see cref="OnWeaponEquipped"/>.
        /// </summary>
        /// <param name="item">Item to equip. Null is a silent no-op.</param>
        public void Equip(ItemData item)
        {
            if (item == null) return;
            _equipped[item.Slot] = item;
            OnWeaponEquipped?.Invoke(item);
        }

        /// <summary>
        /// Unequips the item in the given slot. No-op if slot is empty.
        /// Fires <see cref="OnWeaponEquipped"/> with null.
        /// </summary>
        /// <param name="slot">The slot to clear.</param>
        public void Unequip(EquipSlot slot)
        {
            if (_equipped.ContainsKey(slot))
            {
                _equipped.Remove(slot);
                OnWeaponEquipped?.Invoke(null);
            }
        }

        /// <summary>
        /// Returns the equipped item for the given slot, or null if empty.
        /// </summary>
        /// <param name="slot">The slot to query.</param>
        public ItemData GetEquipped(EquipSlot slot)
        {
            return _equipped.TryGetValue(slot, out var item) ? item : null;
        }

        /// <summary>
        /// Returns true if the given slot has an item equipped.
        /// </summary>
        /// <param name="slot">The slot to test.</param>
        public bool IsSlotEquipped(EquipSlot slot)
        {
            return _equipped.TryGetValue(slot, out var item) && item != null;
        }
    }
}
