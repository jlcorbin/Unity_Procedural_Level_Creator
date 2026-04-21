using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// ScriptableObject listing all props available for procedural room dressing.
    /// Each entry in the list is a <see cref="PropEntry"/> with type, size, weight, and theme tags.
    ///
    /// Create via: Assets ▶ Create ▶ LevelGen ▶ Prop Catalogue
    /// </summary>
    [CreateAssetMenu(menuName = "LevelGen/Prop Catalogue", fileName = "PropCatalogue")]
    public class PropCatalogue : ScriptableObject
    {
        // ── Inspector fields ──────────────────────────────────────────────────

        [Tooltip("All props available for procedural room dressing. " +
                 "Use PropEntry fields to tag each with type, size, weight, and themes.")]
        public List<PropEntry> props = new List<PropEntry>();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a weighted-random prop prefab filtered by spawn type and theme.
        /// Returns null if no eligible entries exist.
        /// </summary>
        /// <param name="type">Required spawn type.</param>
        /// <param name="theme">Theme filter (empty string = any theme).</param>
        /// <param name="rng">Seeded RNG for deterministic results.</param>
        public GameObject GetRandom(PropEntry.SpawnType type, string theme, System.Random rng)
        {
            return GetRandomInternal(type, null, theme, rng);
        }

        /// <summary>
        /// Returns a weighted-random prop prefab filtered by spawn type, size, and theme.
        /// Returns null if no eligible entries exist.
        /// </summary>
        /// <param name="type">Required spawn type.</param>
        /// <param name="size">Required spawn size.</param>
        /// <param name="theme">Theme filter (empty string = any theme).</param>
        /// <param name="rng">Seeded RNG for deterministic results.</param>
        public GameObject GetRandom(PropEntry.SpawnType type, PropEntry.SpawnSize size,
                                     string theme, System.Random rng)
        {
            return GetRandomInternal(type, size, theme, rng);
        }

        /// <summary>
        /// Returns the total number of valid entries for the given type.
        /// </summary>
        public int CountForType(PropEntry.SpawnType type)
        {
            int n = 0;
            foreach (var e in props)
                if (e.spawnType == type && e.prefab != null) n++;
            return n;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private GameObject GetRandomInternal(PropEntry.SpawnType type,
                                              PropEntry.SpawnSize? size,
                                              string theme,
                                              System.Random rng)
        {
            var candidates  = new List<PropEntry>();
            float totalWeight = 0f;

            foreach (var entry in props)
            {
                if (entry.prefab  == null)            continue;
                if (entry.weight  <= 0f)              continue;
                if (entry.spawnType != type)          continue;
                if (size.HasValue && entry.spawnSize != size.Value) continue;
                if (!entry.MatchesTheme(theme))       continue;

                candidates.Add(entry);
                totalWeight += entry.weight;
            }

            if (candidates.Count == 0 || totalWeight <= 0f) return null;

            float roll       = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;

            foreach (var entry in candidates)
            {
                cumulative += entry.weight;
                if (roll <= cumulative) return entry.prefab;
            }

            return candidates[candidates.Count - 1].prefab;
        }
    }
}
