using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// A ScriptableObject asset that holds an ordered list of seeds.
    /// Create one via Assets → Create → LevelGen → Level Sequence.
    /// Assign it to the LevelGenerator to play through a fixed set of levels.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewLevelSequence",
        menuName  = "LevelGen/Level Sequence",
        order     = 1)]
    public class LevelSequence : ScriptableObject
    {
        [Tooltip("Ordered list of levels. Index 0 is the first level.")]
        public List<SeedData> levels = new List<SeedData>();

        // ── Accessors ─────────────────────────────────────────────────

        /// <summary>
        /// Total number of levels in this sequence.
        /// </summary>
        public int Count => levels.Count;

        /// <summary>
        /// Returns the SeedData at the given index, or null if out of range.
        /// </summary>
        public SeedData GetLevel(int index)
        {
            if (index < 0 || index >= levels.Count)
            {
                Debug.LogWarning($"[LevelSequence] Index {index} out of range (count={levels.Count})");
                return null;
            }
            return levels[index];
        }

        /// <summary>
        /// Adds a new seed entry to the end of the sequence.
        /// </summary>
        public void AddLevel(SeedData data)
        {
            if (data == null) return;
            levels.Add(data);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Removes the level at the given index.
        /// </summary>
        public void RemoveLevel(int index)
        {
            if (index < 0 || index >= levels.Count) return;
            levels.RemoveAt(index);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Replaces the seed at the given index with new data.
        /// </summary>
        public void UpdateLevel(int index, SeedData data)
        {
            if (index < 0 || index >= levels.Count) return;
            levels[index] = data;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
