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

        [Tooltip("Maximum total rooms to place (not counting halls).")]
        public int maxRooms = 10;

        [Tooltip("Maximum total halls to place.")]
        public int maxHalls = 15;

        [Tooltip("0 = all halls, 1 = all rooms. 0.6 = 60% rooms.")]
        [Range(0f, 1f)]
        public float roomToHallRatio = 0.6f;

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
                seed            = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                maxRooms        = 10,
                maxHalls        = 15,
                roomToHallRatio = 0.6f,
                levelName       = "Level",
                savedAt         = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        /// <summary>
        /// Serializes this SeedData to a JSON string.
        /// </summary>
        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        /// <summary>
        /// Deserializes a SeedData from a JSON string.
        /// </summary>
        public static SeedData FromJson(string json) =>
            JsonUtility.FromJson<SeedData>(json);

        public override string ToString() =>
            $"[SeedData] {levelName} | seed={seed} | rooms={maxRooms} halls={maxHalls} ratio={roomToHallRatio}";
    }
}
