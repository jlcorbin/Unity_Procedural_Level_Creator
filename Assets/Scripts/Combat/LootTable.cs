// LootTable.cs — what an enemy can drop (M-Loot).
//
// A ScriptableObject list of possible drops, each with an independent drop
// chance and a min/max count. LootDropper rolls this on enemy death. Weapons
// drop as-is; materials drop with a rolled quantity.

using System.Collections.Generic;
using UnityEngine;
using LevelGen.Items;

namespace LevelGen.Combat
{
    /// <summary>
    /// A weighted set of possible drops for an enemy archetype. Each entry rolls
    /// independently against its <see cref="Entry.dropChance"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable_", menuName = "Hub & Hollow/Loot Table")]
    public class LootTable : ScriptableObject
    {
        /// <summary>One possible drop: an item, its chance, and a count range.</summary>
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Item that can drop (gear or material).")]
            public ItemData item;

            [Tooltip("Independent chance this entry drops, 0..1.")]
            [Range(0f, 1f)] public float dropChance = 1f;

            [Tooltip("Minimum quantity when it drops.")]
            [Min(1)] public int minCount = 1;

            [Tooltip("Maximum quantity when it drops.")]
            [Min(1)] public int maxCount = 1;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        /// <summary>The configured drop entries (read-only).</summary>
        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// Rolls the table (UnityEngine.Random) and returns the concrete drops —
        /// each a resolved (item, count) pair. Empty if nothing rolled.
        /// </summary>
        public List<(ItemData item, int count)> Roll()
        {
            var drops = new List<(ItemData, int)>();
            foreach (var e in _entries)
            {
                if (e == null || e.item == null) continue;
                if (Random.value > e.dropChance) continue;

                int min = Mathf.Min(e.minCount, e.maxCount);
                int max = Mathf.Max(e.minCount, e.maxCount);
                int count = Random.Range(min, max + 1);
                drops.Add((e.item, count));
            }
            return drops;
        }
    }
}
