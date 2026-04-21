using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Snapshot of a fully built and dressed room.
    /// Stores the layout definition, the finished prefab, the catalogues used,
    /// and an exact record of every prop placed (for perfect reconstruction).
    ///
    /// Drop <see cref="roomPrefab"/> into <see cref="LevelGenerator.roomPrefabs"/>
    /// or <see cref="LevelGenerator.hallPrefabs"/> to add the room to the generator pool.
    ///
    /// Create via: Assets ▶ Create ▶ LevelGen ▶ Room Preset
    /// </summary>
    [CreateAssetMenu(menuName = "LevelGen/Room Preset", fileName = "RoomPreset")]
    public class RoomPreset : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Tooltip("Human-readable name for this room (also used as the prefab/asset filename).")]
        public string roomName = "New Room";

        [Tooltip("Visual theme — used for prop filtering and future generator weighting.")]
        public PieceCatalogue.Theme theme = PieceCatalogue.Theme.Dungeon;

        // ── Prefab reference ──────────────────────────────────────────────────

        /// <summary>
        /// The finished room prefab (RoomPiece root with ExitPoints and dressed props).
        /// Drop this into LevelGenerator.roomPrefabs or hallPrefabs.
        /// </summary>
        [Tooltip("The finished room prefab produced by the Room Workshop.")]
        public GameObject roomPrefab;

        // ── Layout snapshot ───────────────────────────────────────────────────

        [Tooltip("The RoomDefinition used to build this room. " +
                 "Stored so the room can be reconstructed or edited later.")]
        public RoomDefinition definition = new RoomDefinition();

        [Tooltip("Piece catalogue used during construction.")]
        public PieceCatalogue pieceCatalogue;

        [Tooltip("Prop catalogue used during dressing.")]
        public PropCatalogue propCatalogue;

        // ── Placed props snapshot ─────────────────────────────────────────────

        /// <summary>
        /// Exact record of every prop placed during the workshop dressing pass.
        /// Allows perfect reconstruction without re-running the RNG.
        /// </summary>
        [Tooltip("Snapshot of every prop placed in the last dressing pass.")]
        public List<PlacedProp> placedProps = new List<PlacedProp>();

        // ── Generator metadata ────────────────────────────────────────────────

        [Tooltip("What RoomPiece type to assign when this room is instantiated by the generator.")]
        public RoomPiece.PieceType pieceType = RoomPiece.PieceType.Room;

        [Tooltip("Relative spawn weight. Higher = more likely to be chosen by the generator " +
                 "when multiple rooms are eligible. Default = 1.")]
        [Min(0f)] public float spawnWeight = 1f;

        [Tooltip("Free-form theme tags for future generator filtering. " +
                 "Examples: 'treasure', 'combat', 'boss'.")]
        public List<string> themeTags = new List<string>();

        // ── Nested types ──────────────────────────────────────────────────────

        /// <summary>
        /// Records the prefab and local transform of a single placed prop.
        /// </summary>
        [Serializable]
        public class PlacedProp
        {
            [Tooltip("The prop prefab that was instantiated.")]
            public GameObject prefab;

            [Tooltip("Local position relative to the room root.")]
            public Vector3 localPosition;

            [Tooltip("Local Euler rotation relative to the room root.")]
            public Vector3 localEulerAngles;
        }
    }
}
