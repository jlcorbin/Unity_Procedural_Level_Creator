using System;
using UnityEngine;

namespace LevelGen.V2
{
    public enum LayoutStyle { LinearWithBranches, Grid, Organic, Corridor }

    [Serializable]
    public class LevelGenSettings
    {
        // Output (set by the Save dialog at save time, not surfaced in the EditorWindow).
        // [NonSerialized] keeps these out of any serialized blob so they default cleanly each session.
        [NonSerialized] public string sceneName    = "";
        [NonSerialized] public string outputFolder = "Assets/Levels/Generated";

        // Source
        public PieceCatalogue catalogue;
        public string         themeName = "";  // empty = no theme

        // Room budget
        public int starterCount  = 1;
        public int bossCount     = 1;
        public int smallCount    = 3;
        public int mediumCount   = 2;
        public int largeCount    = 1;
        public int specialCount  = 0;

        // Hall sizing
        public HallCategory spineHallSize  = HallCategory.Medium;
        public HallCategory branchHallSize = HallCategory.Small;

        // Layout
        public LayoutStyle layoutStyle   = LayoutStyle.LinearWithBranches;
        public int branchSlotCount       = 2;

        // Difficulty signals (params recorded; not yet used by generator)
        [Range(0f, 1f)] public float branchingFactor = 0.5f;
        public int deadEndCount    = 0;
        public int secretRoomCount = 0;

        // Reproducibility
        public int seed = 0;  // 0 = pick a random seed at generate time

        // ── Derived ──────────────────────────────────────────────────────────

        public int TotalRoomCount =>
            starterCount + bossCount + smallCount + mediumCount + largeCount + specialCount;

        /// <summary>
        /// Number of rooms on the main path between Starter and Boss (excludes both).
        /// Spine and branches both draw from the combined Small+Medium+Large+Special
        /// pool; branches reduce spine length.
        /// </summary>
        public int SpineLength =>
            Mathf.Max(0, smallCount + mediumCount + largeCount + specialCount - branchSlotCount);
    }
}
