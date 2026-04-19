using System;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// All the data needed to reproduce a specific generated level.
    /// Serialized to JSON for saving, or stored directly in a LevelSequence asset.
    /// </summary>
    [Serializable]
    public class SeedData
    {
        [Tooltip("The random seed. Same seed + same prefabs = identical level every time.")]
        public int seed;

        [Tooltip("Total number of rooms to place, inclusive of the first room.")]
        public int roomCount = 5;

        [Tooltip("Optional display name shown in menus or level select.")]
        public string levelName = "Level";

        [Tooltip("Timestamp when this seed was saved.")]
        public string savedAt;

        /// <summary>
        /// Creates a new SeedData with a random seed and default settings.
        /// </summary>
        public static SeedData CreateRandom()
        {
            return new SeedData
            {
                seed      = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                roomCount = 5,
                levelName = "Level",
                savedAt   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        /// <summary>Serializes this SeedData to a JSON string.</summary>
        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        /// <summary>Deserializes a SeedData from a JSON string.</summary>
        public static SeedData FromJson(string json) =>
            JsonUtility.FromJson<SeedData>(json);

        public override string ToString() =>
            $"[SeedData] {levelName} | seed={seed} | roomCount={roomCount}";
    }
}
