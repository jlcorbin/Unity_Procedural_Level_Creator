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

        /// <summary>Visual theme this catalogue represents.</summary>
        public enum Theme
        {
            Dungeon,
            // Additional themes can be added here for future expansion.
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

        [Tooltip("Visual theme for this catalogue — used for matching with RoomPreset.")]
        public Theme theme = Theme.Dungeon;

        [Tooltip("All modular pieces available for room construction. " +
                 "Add Floor, Wall, Doorway, Corner, Ceiling, Column and Stair prefabs here. " +
                 "Pieces with PieceType.None are in the Skipped staging slot — not used by the generator.")]
        public List<PieceEntry> pieces = new List<PieceEntry>();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a uniformly-random prefab of the given type, or null if none available.
        /// </summary>
        /// <param name="type">Structural role to filter by.</param>
        /// <param name="rng">Seeded RNG for deterministic results.</param>
        public GameObject GetRandom(PieceType type, System.Random rng)
        {
            var candidates = new List<GameObject>();
            foreach (var e in pieces)
            {
                if (e.pieceType == type && e.prefab != null)
                    candidates.Add(e.prefab);
            }
            if (candidates.Count == 0) return null;
            return candidates[rng.Next(candidates.Count)];
        }

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
    }
}
