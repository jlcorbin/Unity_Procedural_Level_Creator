using System.Collections.Generic;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Core procedural level generator.
    ///
    /// Attach to an empty GameObject in the LevelGenerator scene.
    /// Assign roomPrefabs and hallPrefabs.
    ///
    /// Generation flow:
    ///   1. Seed System.Random (seed=0 picks a random seed each run).
    ///   2. Pick a random room from roomPrefabs, place it at origin — counts as room #1.
    ///   3. Add all its open exits to the queue.
    ///   4. Loop: pop an exit → weighted pick (70 % room / 30 % hall) → bounds check →
    ///      place or retry up to maxRetriesPerExit times.
    ///   5. Stop when roomCount rooms are placed. Halls are capped at roomCount.
    ///      Open exits left over stay open — no dead-end caps.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("All possible room prefabs the generator can pick from.")]
        public List<RoomPiece> roomPrefabs = new List<RoomPiece>();

        [Tooltip("All possible hall prefabs the generator can pick from.")]
        public List<RoomPiece> hallPrefabs = new List<RoomPiece>();

        [Header("Generation Settings")]
        [Tooltip("The seed to use. 0 = use a random seed each time.")]
        public int seed = 0;

        [Tooltip("Total number of rooms to place, inclusive of the first room.")]
        [Min(1)] public int roomCount = 5;

        [Tooltip("Maximum times the generator retries a single exit before giving up on it.")]
        [Min(1)] public int maxRetriesPerExit = 10;

        // ── Runtime state ─────────────────────────────────────────────

        /// <summary>All pieces currently placed in the scene.</summary>
        public List<RoomPiece> PlacedPieces { get; private set; } = new List<RoomPiece>();

        /// <summary>The seed actually used for the last generation run.</summary>
        public int LastUsedSeed { get; private set; }

        /// <summary>True while a generation run is in progress.</summary>
        public bool IsGenerating { get; private set; }

        /// <summary>Number of rooms placed in the last generation run.</summary>
        public int LastRoomsPlaced { get; private set; }

        /// <summary>Number of halls placed in the last generation run.</summary>
        public int LastHallsPlaced { get; private set; }

        private System.Random _rng;
        private Queue<ExitPoint> _openExits = new Queue<ExitPoint>();
        private int _roomsPlaced;
        private int _hallsPlaced;

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Generates a level using the current inspector settings.
        /// Clears any existing level first.
        /// </summary>
        public void Generate()
        {
            ClearLevel();

            int usedSeed = seed != 0 ? seed : Random.Range(int.MinValue, int.MaxValue);
            LastUsedSeed = usedSeed;
            _rng         = new System.Random(usedSeed);
            _roomsPlaced = 0;
            _hallsPlaced = 0;
            IsGenerating = true;

            int maxHalls = roomCount;

            if (roomPrefabs == null || roomPrefabs.Count == 0)
            {
                Debug.LogError("[LevelGenerator] No room prefabs assigned.");
                IsGenerating = false;
                return;
            }

            // Place the first room at origin
            RoomPiece firstPrefab = roomPrefabs[_rng.Next(roomPrefabs.Count)];
            RoomPiece firstRoom   = Instantiate(firstPrefab, Vector3.zero, Quaternion.identity, transform);
            firstRoom.RefreshExits();
            firstRoom.isPlaced        = true;
            firstRoom.generationDepth = 0;
            PlacedPieces.Add(firstRoom);
            _roomsPlaced++;

            EnqueueOpenExits(firstRoom);

            // Main generation loop
            while (_openExits.Count > 0)
            {
                if (_roomsPlaced >= roomCount && _hallsPlaced >= maxHalls) break;

                ExitPoint sourceExit = _openExits.Dequeue();
                if (sourceExit == null || sourceExit.isConnected || sourceExit.isSealed)
                    continue;

                bool placed = TryFillExit(sourceExit, roomCount, maxHalls);
                if (!placed)
                    sourceExit.isSealed = true;
            }

            LastRoomsPlaced = _roomsPlaced;
            LastHallsPlaced = _hallsPlaced;
            IsGenerating    = false;

            Debug.Log($"[LevelGenerator] Done. seed={usedSeed} rooms={_roomsPlaced}/{roomCount} halls={_hallsPlaced}/{roomCount}");
        }

        /// <summary>Destroys all generated pieces and resets state.</summary>
        public void ClearLevel()
        {
            foreach (var piece in PlacedPieces)
            {
                if (piece != null)
                    DestroyImmediate(piece.gameObject);
            }
            PlacedPieces.Clear();
            _openExits.Clear();
            _roomsPlaced = 0;
            _hallsPlaced = 0;
            IsGenerating = false;
        }

        /// <summary>Sets a new random seed value in the inspector field.</summary>
        public void RandomizeSeed()
        {
            seed = Random.Range(int.MinValue, int.MaxValue);
        }

        // ── Private generation methods ────────────────────────────────

        /// <summary>
        /// Tries to place a piece connecting to sourceExit.
        /// Picks room (70 %) or hall (30 %) based on what is still available.
        /// Returns true if placement succeeded.
        /// </summary>
        private bool TryFillExit(ExitPoint sourceExit, int maxRooms, int maxHalls)
        {
            bool canRoom = _roomsPlaced < maxRooms;
            bool canHall = _hallsPlaced < maxHalls;

            bool tryRoom;
            if (canRoom && canHall)
                tryRoom = _rng.NextDouble() < 0.7;
            else
                tryRoom = canRoom;

            List<RoomPiece> pool = tryRoom ? roomPrefabs : hallPrefabs;
            if (pool == null || pool.Count == 0)
            {
                // Preferred pool empty — try the other type
                if (tryRoom && canHall)       { tryRoom = false; pool = hallPrefabs; }
                else if (!tryRoom && canRoom) { tryRoom = true;  pool = roomPrefabs; }
                else return false;
                if (pool == null || pool.Count == 0) return false;
            }

            List<RoomPiece> shuffled = ShuffledCopy(pool);
            ExitPoint.Direction needed = sourceExit.OppositeDirection();

            for (int attempt = 0; attempt < maxRetriesPerExit; attempt++)
            {
                RoomPiece prefab = shuffled[attempt % shuffled.Count];
                if (prefab == null) continue;

                RoomPiece candidate = Instantiate(prefab);
                candidate.RefreshExits();

                ExitPoint entryExit = candidate.GetExitFacing(needed);
                if (entryExit == null)
                {
                    DestroyImmediate(candidate.gameObject);
                    continue;
                }

                Vector3    pos = BoundsChecker.CalculatePlacementPosition(sourceExit, prefab, entryExit);
                Quaternion rot = BoundsChecker.CalculatePlacementRotation(sourceExit, entryExit);

                candidate.transform.position = pos;
                candidate.transform.rotation = rot;

                if (BoundsChecker.Overlaps(candidate.GetWorldBounds(), PlacedPieces))
                {
                    DestroyImmediate(candidate.gameObject);
                    continue;
                }

                // Placement succeeded
                candidate.transform.SetParent(transform);
                candidate.isPlaced        = true;
                candidate.generationDepth = sourceExit.GetComponentInParent<RoomPiece>()?.generationDepth + 1 ?? 1;

                sourceExit.isConnected    = true;
                sourceExit.connectedPiece = candidate;
                entryExit.isConnected     = true;
                entryExit.connectedPiece  = sourceExit.GetComponentInParent<RoomPiece>();

                PlacedPieces.Add(candidate);

                if (tryRoom) _roomsPlaced++;
                else         _hallsPlaced++;

                EnqueueOpenExits(candidate);
                return true;
            }

            return false;
        }

        /// <summary>Shuffles open exits and enqueues them.</summary>
        private void EnqueueOpenExits(RoomPiece piece)
        {
            foreach (var exit in ShuffledCopy(piece.GetOpenExits()))
                _openExits.Enqueue(exit);
        }

        /// <summary>Returns a new shuffled copy of a list using the seeded RNG.</summary>
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
