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
        public bool AddItem(ItemData item)
        {
            if (item == null) return false;
            if (_items.Count >= _capacity) return false;
            _items.Add(item);
            OnItemAdded?.Invoke(item);
            return true;
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

        /// <summary>Current number of items in the inventory.</summary>
        public int Count => _items.Count;
    }
}
