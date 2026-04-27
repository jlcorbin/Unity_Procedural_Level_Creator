using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// ScriptableObject listing all modular prefabs available for procedural room construction.
    /// Populate with pieces from the Fantastic Dungeon Pack (or any modular set) before building.
    ///
    /// Create via: Assets ▶ Create ▶ LevelGen ▶ Piece Catalogue
    /// </summary>
    [CreateAssetMenu(menuName = "LevelGen/Piece Catalogue", fileName = "PieceCatalogue")]
    public class PieceCatalogue : ScriptableObject
    {
        // ── Enums ─────────────────────────────────────────────────────────────

        /// <summary>Visual style tag for this catalogue. Used to match against the Theme name resolved from a LevelGenSettings asset.</summary>
        public enum VisualTheme
        {
            Dungeon,
            // Additional themes can be added here for future expansion.
        }

        // ── Theme (prefab bundle) ─────────────────────────────────────────────

        /// <summary>
        /// A named bundle that pairs one prefab to each category.
        /// Assign to a RoomBuilder (via <see cref="PieceCatalogue.GetTheme"/>) so its
        /// Build pass pulls prefabs from the bundle instead of the direct inspector slots.
        /// </summary>
        [Serializable]
        public class Theme
        {
            [Tooltip("Display name used to select this theme from a RoomBuilder.")]
            public string name = "Untitled";

            [Tooltip("Floor prefab for this theme.")]
            public GameObject floor;

            [Tooltip("Straight wall prefab for this theme.")]
            public GameObject wall;

            [Tooltip("Doorway prefab. Reserved for future use — OK to leave null.")]
            public GameObject doorway;

            [Tooltip("Outward corner prefab for this theme.")]
            public GameObject corner;

            [Tooltip("Freestanding column prefab for this theme.")]
            public GameObject column;
        }

        /// <summary>Structural role of a modular piece.</summary>
        public enum PieceType
        {
            /// <summary>Horizontal tile placed at floor level (y = 0).</summary>
            Floor,
            /// <summary>Vertical wall tile placed at the room perimeter.</summary>
            Wall,
            /// <summary>Archway or open passage that replaces a wall on exit sides.</summary>
            Doorway,
            /// <summary>90-degree corner piece placed at the four outer corners of the room (wall-shaped variants: corner/angle/concave).</summary>
            Corner,
            /// <summary>Freestanding decorative column (COMP_Column_ pieces). Never used for room corners.</summary>
            Column,
            /// <summary>Horizontal tile placed at ceiling height (y = ROOM_HEIGHT).</summary>
            Ceiling,
            /// <summary>Stair piece for vertical connections between floors.</summary>
            Stair,

            /// <summary>
            /// Staging slot for pieces pending categorization. Never used by the generator.
            /// Pieces auto-populated from skipped subfolders (Trim, Railing, OneSided, etc.)
            /// land here so the user can review and promote them to a real type via the editor.
            /// </summary>
            None = 99, // Explicit value future-proofs against accidental reordering of real types.
        }

        // ── Entry ─────────────────────────────────────────────────────────────

        /// <summary>
        /// One entry in the catalogue — a prefab, its structural role, and the source sub-folder.
        /// </summary>
        [Serializable]
        public class PieceEntry
        {
            [Tooltip("The modular prefab to place.")]
            public GameObject prefab;

            [Tooltip("Structural role. RoomBuilder filters by this when placing pieces.")]
            public PieceType pieceType = PieceType.Floor;

            [Tooltip("Asset pack sub-folder this prefab was loaded from. " +
                     "Set automatically by PieceCatalogueEditor auto-populate.")]
            public string subFolder = "";

            [Tooltip("Doorway pieces only — true = generator exit/entrance (spawns ExitPoint), " +
                     "false = decorative (no ExitPoint). Ignored for all other piece types.")]
            public bool isExit = false;
        }

        // ── Inspector fields ──────────────────────────────────────────────────

        [Tooltip("Visual style tag for this catalogue. Used to match against the Theme name resolved from a LevelGenSettings asset.")]
        public VisualTheme theme = VisualTheme.Dungeon;

        [Tooltip("All modular pieces available for room construction. " +
                 "Add Floor, Wall, Doorway, Corner, Ceiling, Column and Stair prefabs here. " +
                 "Pieces with PieceType.None are in the Skipped staging slot — not used by the generator.")]
        public List<PieceEntry> pieces = new List<PieceEntry>();

        [Tooltip("Named prefab bundles. Pick one by name from a RoomBuilder to override its direct prefab slots.")]
        public List<Theme> themes = new List<Theme>();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the number of valid (non-null prefab) entries for the given type.
        /// </summary>
        public int CountOfType(PieceType type)
        {
            int n = 0;
            foreach (var e in pieces)
                if (e.pieceType == type && e.prefab != null) n++;
            return n;
        }

        /// <summary>
        /// Returns the <see cref="Theme"/> whose name matches <paramref name="themeName"/>,
        /// or null if none found.
        /// </summary>
        public Theme GetTheme(string themeName)
        {
            return themes.Find(t => t != null && t.name == themeName);
        }

        /// <summary>
        /// Returns an array of all theme names in list order.
        /// Returns an empty array when no themes are defined.
        /// </summary>
        public string[] GetThemeNames()
        {
            var arr = new string[themes.Count];
            for (int i = 0; i < themes.Count; i++)
                arr[i] = themes[i]?.name ?? "(null)";
            return arr;
        }
    }
}
