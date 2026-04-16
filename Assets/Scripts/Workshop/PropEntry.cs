using System;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// One entry in a PropCatalogue — a prop prefab tagged with type, size, weight,
    /// and optional theme strings for filtered selection.
    /// </summary>
    [Serializable]
    public class PropEntry
    {
        // ── Enums ─────────────────────────────────────────────────────────────

        /// <summary>Gameplay role of this prop.</summary>
        public enum SpawnType
        {
            Decoration,
            Enemy,
            Trap,
            Chest,
        }

        /// <summary>Physical footprint of this prop, used to match SpawnPoint sizes.</summary>
        public enum SpawnSize
        {
            Small,
            Med,
            Large,
        }

        // ── Fields ────────────────────────────────────────────────────────────

        [Tooltip("The prop prefab to instantiate.")]
        public GameObject prefab;

        [Tooltip("Gameplay role — matched against SpawnPoint.spawnType when dressing.")]
        public SpawnType spawnType = SpawnType.Decoration;

        [Tooltip("Physical size — matched against SpawnPoint.spawnSize when dressing.")]
        public SpawnSize spawnSize = SpawnSize.Small;

        [Tooltip("Relative spawn probability within its type+size group. " +
                 "Higher = more likely. Must be > 0 to be eligible.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Optional theme tags (e.g. 'dungeon', 'library', 'crypt'). " +
                 "Leave empty to allow in any themed room.")]
        public string[] themes = Array.Empty<string>();

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this entry is compatible with the given theme string.
        /// An entry with no themes is compatible with every theme.
        /// </summary>
        /// <param name="theme">Theme to test, or null/empty to always match.</param>
        public bool MatchesTheme(string theme)
        {
            if (themes == null || themes.Length == 0) return true;
            if (string.IsNullOrEmpty(theme))          return true;

            foreach (var t in themes)
            {
                if (string.Equals(t, theme, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
