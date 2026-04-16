using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Core procedural level generator.
    ///
    /// Attach to an empty GameObject in your scene.
    /// Assign room prefabs, hall prefabs, and optionally a LevelSequence.
    ///
    /// Generation flow:
    ///   1. Seed System.Random
    ///   2. Place starting room at origin
    ///   3. Add all its open exits to the queue
    ///   4. Loop: pop a random exit → pick a piece → bounds check → place or retry
    ///   5. Seal any exits that couldn't be filled
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("The room placed first. Must have at least one ExitPoint.")]
        public RoomPiece startRoomPrefab;

        [Tooltip("All possible room prefabs the generator can pick from.")]
        public List<RoomPiece> roomPrefabs = new List<RoomPiece>();

        [Tooltip("All possible hall prefabs the generator can pick from.")]
        public List<RoomPiece> hallPrefabs = new List<RoomPiece>();

        [Tooltip("Prefab placed on exits that couldn't be filled. Can be null.")]
        public RoomPiece deadEndPrefab;

        [Header("Generation Settings")]
        [Tooltip("The seed to use. 0 = use a random seed each time.")]
        public int seed = 0;

        [Tooltip("Maximum number of rooms to place (excluding halls and start room).")]
        [Min(1)] public int maxRooms = 10;

        [Tooltip("Maximum number of halls to place.")]
        [Min(0)] public int maxHalls = 15;

        [Tooltip("Probability of placing a room vs a hall at each open exit. " +
                 "1 = always room, 0 = always hall.")]
        [Range(0f, 1f)]
        public float roomToHallRatio = 0.6f;

        [Tooltip("Maximum times the generator retries a single exit before sealing it.")]
        [Min(1)] public int maxRetriesPerExit = 10;

        [Header("Sequence (optional)")]
        [Tooltip("If assigned, levels are loaded from this sequence by index.")]
        public LevelSequence levelSequence;

        [Tooltip("Which level in the sequence to load on Start. -1 = use manual seed above.")]
        public int sequenceIndex = -1;

        [Header("Dressing")]
        [Tooltip("If true, each placed room/hall is dressed with props immediately after placement " +
                 "using defaultPropCatalogue. Requires SpawnPoints to be set up on the prefabs.")]
        public bool dressRoomsOnGenerate = false;

        [Tooltip("Prop catalogue used when dressRoomsOnGenerate is enabled.")]
        public PropCatalogue defaultPropCatalogue;

        [Tooltip("Theme tag passed to RoomContentGenerator when dressing. Leave empty to allow all themes.")]
        public string dressingTheme = "";

        [Header("Layer")]
        [Tooltip("Physics layer used by room/hall colliders for overlap checks. " +
                 "Only needed if using physics-based overlap detection.")]
        public LayerMask roomLayerMask;

        // ── Runtime state ─────────────────────────────────────────────

        /// <summary>All pieces currently placed in the scene.</summary>
        public List<RoomPiece> PlacedPieces { get; private set; } = new List<RoomPiece>();

        /// <summary>The seed actually used for the last generation run.</summary>
        public int LastUsedSeed { get; private set; }

        /// <summary>True while a generation run is in progress.</summary>
        public bool IsGenerating { get; private set; }

        private System.Random _rng;
        private Queue<ExitPoint> _openExits = new Queue<ExitPoint>();
        private int _roomCount;
        private int _hallCount;

        // ── Unity messages ────────────────────────────────────────────

        private void Start()
        {
            // If a sequence index is set, load that seed and generate
            if (sequenceIndex >= 0 && levelSequence != null)
            {
                var data = levelSequence.GetLevel(sequenceIndex);
                if (data != null)
                    GenerateFromSeed(data);
            }
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Generates a level using the current inspector settings.
        /// Clears any existing level first.
        /// </summary>
        public void Generate()
        {
            int usedSeed = seed != 0 ? seed : Random.Range(int.MinValue, int.MaxValue);
            GenerateWithSeed(usedSeed, maxRooms, maxHalls, roomToHallRatio);
        }

        /// <summary>
        /// Generates a level from a SeedData object.
        /// </summary>
        public void GenerateFromSeed(SeedData data)
        {
            if (data == null)
            {
                Debug.LogError("[LevelGenerator] GenerateFromSeed called with null SeedData.");
                return;
            }
            GenerateWithSeed(data.seed, data.maxRooms, data.maxHalls, data.roomToHallRatio);
        }

        /// <summary>
        /// Generates a level with explicit parameters.
        /// </summary>
        public void GenerateWithSeed(int usedSeed, int rooms, int halls, float ratio)
        {
            ClearLevel();

            LastUsedSeed = usedSeed;
            _rng         = new System.Random(usedSeed);
            _roomCount   = 0;
            _hallCount   = 0;
            IsGenerating = true;

            Debug.Log($"[LevelGenerator] Starting generation. seed={usedSeed} maxRooms={rooms} maxHalls={halls}");

            // Place the starting room
            if (startRoomPrefab == null)
            {
                Debug.LogError("[LevelGenerator] startRoomPrefab is not assigned.");
                IsGenerating = false;
                return;
            }

            RoomPiece startRoom = PlacePiece(startRoomPrefab, Vector3.zero, Quaternion.identity, depth: 0);
            if (startRoom == null)
            {
                Debug.LogError("[LevelGenerator] Failed to place start room.");
                IsGenerating = false;
                return;
            }

            TryDress(startRoom);

            // Seed the queue with the starting room's exits
            EnqueueOpenExits(startRoom);

            // Main generation loop
            while (_openExits.Count > 0 && (_roomCount < rooms || _hallCount < halls))
            {
                ExitPoint sourceExit = _openExits.Dequeue();

                // Skip exits that got connected while sitting in the queue
                if (sourceExit == null || sourceExit.isConnected || sourceExit.isSealed)
                    continue;

                bool placed = TryFillExit(sourceExit, rooms, halls, ratio);

                if (!placed)
                    sourceExit.isSealed = true;
            }

            // Seal all remaining open exits
            SealRemainingExits();

            IsGenerating = false;
            Debug.Log($"[LevelGenerator] Generation complete. " +
                      $"Rooms={_roomCount} Halls={_hallCount} Total={PlacedPieces.Count}");
        }

        /// <summary>
        /// Destroys all generated pieces and resets state.
        /// </summary>
        public void ClearLevel()
        {
            foreach (var piece in PlacedPieces)
            {
                if (piece != null)
                    DestroyImmediate(piece.gameObject);
            }
            PlacedPieces.Clear();
            _openExits.Clear();
            _roomCount   = 0;
            _hallCount   = 0;
            IsGenerating = false;
        }

        /// <summary>
        /// Captures the current generation settings as a SeedData.
        /// Call after Generate() to save the last run.
        /// </summary>
        public SeedData CaptureCurrentSeed(string levelName = "")
        {
            return new SeedData
            {
                seed            = LastUsedSeed,
                maxRooms        = maxRooms,
                maxHalls        = maxHalls,
                roomToHallRatio = roomToHallRatio,
                levelName       = string.IsNullOrEmpty(levelName) ? $"Level_{LastUsedSeed}" : levelName,
                savedAt         = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        /// <summary>
        /// Saves the current seed to the assigned LevelSequence asset.
        /// Does nothing if no sequence is assigned.
        /// </summary>
        public void SaveCurrentSeedToSequence(string levelName = "")
        {
            if (levelSequence == null)
            {
                Debug.LogWarning("[LevelGenerator] No LevelSequence assigned. Cannot save.");
                return;
            }
            var data = CaptureCurrentSeed(levelName);
            levelSequence.AddLevel(data);
            Debug.Log($"[LevelGenerator] Saved seed {data.seed} to sequence as '{data.levelName}'.");
        }

        /// <summary>
        /// Sets a new random seed value in the inspector field.
        /// Call Generate() after this to use it.
        /// </summary>
        public void RandomizeSeed()
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }

        // ── Private generation methods ────────────────────────────────

        /// <summary>
        /// Attempts to place a piece connecting to the given exit.
        /// Retries up to maxRetriesPerExit times with different prefabs.
        /// Returns true if a piece was successfully placed.
        /// </summary>
        private bool TryFillExit(ExitPoint sourceExit, int maxR, int maxH, float ratio)
        {
            // Decide whether to try a room or a hall
            bool tryRoom = (_rng.NextDouble() < ratio) && (_roomCount < maxR);
            bool tryHall = !tryRoom && (_hallCount < maxH);

            // Fall back to the other type if the preferred one is maxed out
            if (!tryRoom && _roomCount < maxR)  tryRoom = true;
            if (!tryHall && _hallCount < maxH)  tryHall = true;

            if (!tryRoom && !tryHall) return false;

            List<RoomPiece> candidates = tryRoom ? roomPrefabs : hallPrefabs;
            if (candidates == null || candidates.Count == 0) return false;

            // Shuffle a copy of candidates so we don't bias toward index 0
            List<RoomPiece> shuffled = ShuffledCopy(candidates);

            for (int attempt = 0; attempt < maxRetriesPerExit; attempt++)
            {
                RoomPiece prefab = shuffled[attempt % shuffled.Count];
                if (prefab == null) continue;

                // Find a compatible entry exit on the prefab (opposite direction)
                ExitPoint.Direction needed = sourceExit.OppositeDirection();

                // Temporarily instantiate to inspect exits — we'll destroy if it fails
                // (Alternatively, cache exit data from prefabs at startup for better perf)
                RoomPiece candidate = Instantiate(prefab);
                candidate.RefreshExits();

                ExitPoint entryExit = candidate.GetExitFacing(needed);
                if (entryExit == null)
                {
                    DestroyImmediate(candidate.gameObject);
                    continue;
                }

                // Calculate placement transform
                Vector3    pos = BoundsChecker.CalculatePlacementPosition(sourceExit, prefab, entryExit);
                Quaternion rot = BoundsChecker.CalculatePlacementRotation(sourceExit, entryExit);

                candidate.transform.position = pos;
                candidate.transform.rotation = rot;

                // Bounds check
                Bounds proposed = candidate.GetWorldBounds();
                if (BoundsChecker.Overlaps(proposed, PlacedPieces))
                {
                    DestroyImmediate(candidate.gameObject);
                    continue;
                }

                // Placement succeeded — finalise
                candidate.transform.SetParent(transform);
                candidate.isPlaced        = true;
                candidate.generationDepth = sourceExit.GetComponentInParent<RoomPiece>()?.generationDepth + 1 ?? 1;

                // Connect the exits
                sourceExit.isConnected        = true;
                sourceExit.connectedPiece     = candidate;
                entryExit.isConnected         = true;
                entryExit.connectedPiece      = sourceExit.GetComponentInParent<RoomPiece>();

                PlacedPieces.Add(candidate);

                if (tryRoom) _roomCount++;
                else         _hallCount++;

                TryDress(candidate);

                // Add new open exits to the queue
                EnqueueOpenExits(candidate);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Places a piece directly without an overlap check.
        /// Used for the starting room only.
        /// </summary>
        private RoomPiece PlacePiece(RoomPiece prefab, Vector3 pos, Quaternion rot, int depth)
        {
            RoomPiece piece = Instantiate(prefab, pos, rot, transform);
            piece.RefreshExits();
            piece.isPlaced        = true;
            piece.generationDepth = depth;
            PlacedPieces.Add(piece);
            return piece;
        }

        /// <summary>
        /// Adds all open exits from a piece to the queue in random order.
        /// </summary>
        private void EnqueueOpenExits(RoomPiece piece)
        {
            var open = piece.GetOpenExits();
            // Shuffle before enqueuing so exit order is randomised
            open = ShuffledCopy(open);
            foreach (var exit in open)
                _openExits.Enqueue(exit);
        }

        /// <summary>
        /// After the main loop, seals all exits still in the queue
        /// and optionally places dead-end caps.
        /// </summary>
        private void SealRemainingExits()
        {
            foreach (var exit in _openExits)
            {
                if (exit == null || exit.isConnected) continue;
                exit.isSealed = true;

                if (deadEndPrefab != null)
                {
                    ExitPoint.Direction needed = exit.OppositeDirection();
                    RoomPiece cap = Instantiate(deadEndPrefab);
                    cap.RefreshExits();

                    ExitPoint capEntry = cap.GetExitFacing(needed);
                    if (capEntry != null)
                    {
                        cap.transform.position = BoundsChecker.CalculatePlacementPosition(exit, deadEndPrefab, capEntry);
                        cap.transform.rotation = BoundsChecker.CalculatePlacementRotation(exit, capEntry);
                        cap.transform.SetParent(transform);
                        cap.isPlaced = true;
                        PlacedPieces.Add(cap);

                        exit.isConnected      = true;
                        capEntry.isConnected  = true;
                    }
                    else
                    {
                        DestroyImmediate(cap.gameObject);
                    }
                }
            }
            _openExits.Clear();
        }

        // ── Dressing ──────────────────────────────────────────────────

        /// <summary>
        /// Dresses a newly placed piece if dressRoomsOnGenerate is enabled.
        /// No-ops if the toggle is off, no catalogue is assigned, or the piece has no SpawnPoints.
        /// </summary>
        private void TryDress(RoomPiece piece)
        {
            if (!dressRoomsOnGenerate) return;
            if (defaultPropCatalogue == null) return;
            RoomContentGenerator.DressRoom(piece.transform, defaultPropCatalogue, dressingTheme, _rng);
        }

        // ── Utilities ─────────────────────────────────────────────────

        /// <summary>
        /// Returns a new shuffled copy of a list using the seeded RNG.
        /// Does not modify the original list.
        /// </summary>
        private List<T> ShuffledCopy<T>(List<T> source)
        {
            var copy = new List<T>(source);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }
    }
}
