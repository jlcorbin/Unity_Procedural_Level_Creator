using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// A ScriptableObject index of all authored <see cref="RoomPreset"/> assets.
    ///
    /// Populate this list with every RoomPreset you want the generator to be able to
    /// choose from. The LevelGenerator can optionally reference a RoomPresetLibrary
    /// to select prefabs by theme, spawn weight, or PieceType instead of maintaining
    /// individual lists.
    ///
    /// Create via: Assets ▶ Create ▶ LevelGen ▶ Room Preset Library
    /// </summary>
    [CreateAssetMenu(menuName = "LevelGen/Room Preset Library", fileName = "RoomPresetLibrary")]
    public class RoomPresetLibrary : ScriptableObject
    {
        // ── Inspector fields ──────────────────────────────────────────────────

        [Tooltip("All room presets available to the level generator.")]
        public List<RoomPreset> rooms = new List<RoomPreset>();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns every preset whose <see cref="RoomPreset.themeTags"/> contains
        /// <paramref name="tag"/> (case-insensitive).
        /// If <paramref name="tag"/> is null or empty, returns all presets.
        /// </summary>
        public List<RoomPreset> GetByTag(string tag)
        {
            var result = new List<RoomPreset>();
            bool anyTag = string.IsNullOrEmpty(tag);

            foreach (var preset in rooms)
            {
                if (preset == null) continue;

                if (anyTag)
                {
                    result.Add(preset);
                    continue;
                }

                foreach (var t in preset.themeTags)
                {
                    if (string.Equals(t, tag, System.StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(preset);
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a weighted-random <see cref="RoomPreset"/> from the full list,
        /// or null if the library is empty.
        /// Uses <see cref="RoomPreset.spawnWeight"/> for probability weighting.
        /// </summary>
        /// <param name="rng">Seeded RNG for deterministic results.</param>
        public RoomPreset GetRandom(System.Random rng)
        {
            return GetRandomFrom(rooms, rng);
        }

        /// <summary>
        /// Returns a weighted-random <see cref="RoomPreset"/> whose
        /// <see cref="RoomPreset.themeTags"/> contains <paramref name="tag"/>.
        /// Returns null if no matching presets exist.
        /// </summary>
        /// <param name="tag">Theme tag to filter by (case-insensitive).</param>
        /// <param name="rng">Seeded RNG for deterministic results.</param>
        public RoomPreset GetRandom(string tag, System.Random rng)
        {
            return GetRandomFrom(GetByTag(tag), rng);
        }

        /// <summary>
        /// Returns all presets matching the given <see cref="RoomPiece.PieceType"/>
        /// (as stored in <see cref="RoomPreset.pieceType"/>).
        /// </summary>
        public List<RoomPreset> GetByPieceType(RoomPiece.PieceType type)
        {
            var result = new List<RoomPreset>();
            foreach (var preset in rooms)
            {
                if (preset != null && preset.pieceType == type)
                    result.Add(preset);
            }
            return result;
        }

        /// <summary>
        /// Returns the total number of non-null presets in the library.
        /// </summary>
        public int Count()
        {
            int n = 0;
            foreach (var p in rooms)
                if (p != null) n++;
            return n;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Weighted random selection from a list of presets.
        /// Weight is taken from <see cref="RoomPreset.spawnWeight"/>; entries with weight ≤ 0
        /// are skipped. Returns null if the list is empty or all weights are zero.
        /// </summary>
        private static RoomPreset GetRandomFrom(List<RoomPreset> candidates, System.Random rng)
        {
            if (candidates == null || candidates.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var p in candidates)
                if (p != null && p.spawnWeight > 0f) totalWeight += p.spawnWeight;

            if (totalWeight <= 0f) return null;

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;

            foreach (var p in candidates)
            {
                if (p == null || p.spawnWeight <= 0f) continue;
                cumulative += p.spawnWeight;
                if (roll <= cumulative) return p;
            }

            // Fallback (floating-point edge case)
            return candidates[candidates.Count - 1];
        }
    }
}
