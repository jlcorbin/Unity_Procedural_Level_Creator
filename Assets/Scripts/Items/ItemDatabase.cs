// ItemDatabase.cs — collection ScriptableObject for all ItemData assets (M16).
//
// One database asset per project (or per logical grouping if needed).
// Populate the _items list in the Inspector by dragging in ItemData assets.
//
// No singleton — multiple databases are allowed (e.g. one per dungeon
// tier, one for quest items). Callers hold a serialized reference to
// whichever database they need.
//
// Create via: Hub & Hollow / Item Database

using System.Collections.Generic;
using UnityEngine;

namespace LevelGen.Items
{
    /// <summary>
    /// Collection of <see cref="ItemData"/> assets with fast lookup helpers.
    /// Populate the <c>_items</c> list in the Inspector. Supports
    /// duplicate-ID entries in the list (returns the first match from
    /// <see cref="GetById"/>); it is the designer's responsibility to keep
    /// IDs unique within a database.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Hub & Hollow/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [Tooltip("All items in the game. Populate in the Inspector.")]
        [SerializeField] private List<ItemData> _items = new List<ItemData>();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first <see cref="ItemData"/> whose <see cref="ItemData.Id"/>
        /// matches <paramref name="id"/>, or <c>null</c> if not found.
        /// Null entries in the list are skipped defensively.
        /// </summary>
        /// <param name="id">The snake_case ID to search for (e.g. "light_sword").</param>
        public ItemData GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || _items == null) return null;
            foreach (var item in _items)
            {
                if (item != null && item.Id == id) return item;
            }
            return null;
        }

        /// <summary>
        /// Returns a new <see cref="List{T}"/> containing all
        /// <see cref="ItemData"/> entries whose <see cref="ItemData.Slot"/>
        /// equals <paramref name="slot"/>. Null entries are skipped.
        /// The returned list is a fresh copy — modifying it does not
        /// affect the database.
        /// </summary>
        /// <param name="slot">The equipment slot to filter by.</param>
        public List<ItemData> GetBySlot(EquipSlot slot)
        {
            var result = new List<ItemData>();
            if (_items == null) return result;
            foreach (var item in _items)
            {
                if (item != null && item.Slot == slot) result.Add(item);
            }
            return result;
        }

        /// <summary>Total number of entries in the database (including nulls).</summary>
        public int Count => _items != null ? _items.Count : 0;
    }
}
