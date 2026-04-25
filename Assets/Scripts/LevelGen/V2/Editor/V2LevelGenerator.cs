#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelGen.V2
{
    /// <summary>
    /// Result returned by <see cref="V2LevelGenerator.Generate"/>.
    /// </summary>
    public class GenerationResult
    {
        public bool        Success;
        public string      FailureReason;
        public GameObject  Root;
        public int         Seed;
        public int         RoomsPlaced;
        public int         HallsPlaced;
        public int         BranchesPlaced;
        public int         BranchesRequested;
        public int         BacktrackCount;
        public double      ElapsedSeconds;
        public List<string> Log = new List<string>();
    }

    /// <summary>
    /// Phase B + C generator. Places Starter at world origin, walks down a linear
    /// spine of rooms (Small/Medium/Large/Special drawn from a single combined pool
    /// weighted by remaining counts) connected by spine-size halls, ends with the
    /// Boss room. After Boss, attaches branches (same combined pool, branch-size
    /// halls) onto random rooms with unused exits — including earlier branches.
    /// Spine backtracks up to <see cref="MaxBacktracks"/>; branches degrade
    /// gracefully (skip the slot with a warning) instead of aborting.
    /// </summary>
    public static class V2LevelGenerator
    {
        const int   MaxBacktracks    = 50;
        const float CollisionEpsilon = 0.01f;

        // ── State types ───────────────────────────────────────────────────────

        /// <summary>
        /// Combined remaining-count pool for Small + Medium + Large + Special.
        /// Both spine slots and branch slots draw from this single bucket.
        /// </summary>
        class CategoryPool
        {
            public int small;
            public int medium;
            public int large;
            public int special;

            public int Total => small + medium + large + special;

            public bool DrawRandom(System.Random rng, out RoomCategory cat)
            {
                cat = default;
                int total = Total;
                if (total <= 0) return false;
                int roll = rng.Next(total);
                if (roll < small)            { cat = RoomCategory.Small;   return true; }
                roll -= small;
                if (roll < medium)           { cat = RoomCategory.Medium;  return true; }
                roll -= medium;
                if (roll < large)            { cat = RoomCategory.Large;   return true; }
                cat = RoomCategory.Special;  return true;
            }

            public bool DrawRandom(System.Random rng, HashSet<RoomCategory> excluded, out RoomCategory cat)
            {
                cat = default;
                int sw = excluded.Contains(RoomCategory.Small)   ? 0 : small;
                int mw = excluded.Contains(RoomCategory.Medium)  ? 0 : medium;
                int lw = excluded.Contains(RoomCategory.Large)   ? 0 : large;
                int xw = excluded.Contains(RoomCategory.Special) ? 0 : special;
                int total = sw + mw + lw + xw;
                if (total <= 0) return false;
                int roll = rng.Next(total);
                if (roll < sw)            { cat = RoomCategory.Small;   return true; }
                roll -= sw;
                if (roll < mw)            { cat = RoomCategory.Medium;  return true; }
                roll -= mw;
                if (roll < lw)            { cat = RoomCategory.Large;   return true; }
                cat = RoomCategory.Special; return true;
            }

            public void Decrement(RoomCategory cat)
            {
                switch (cat)
                {
                    case RoomCategory.Small:   small--;   break;
                    case RoomCategory.Medium:  medium--;  break;
                    case RoomCategory.Large:   large--;   break;
                    case RoomCategory.Special: special--; break;
                }
            }

            public void Increment(RoomCategory cat)
            {
                switch (cat)
                {
                    case RoomCategory.Small:   small++;   break;
                    case RoomCategory.Medium:  medium++;  break;
                    case RoomCategory.Large:   large++;   break;
                    case RoomCategory.Special: special++; break;
                }
            }
        }

        struct PlacementOutput
        {
            public RoomPiece HallPiece;
            public RoomPiece RoomPiece;
            public ExitPoint HallEntry;
            public ExitPoint HallExit;
            public ExitPoint RoomEntry;
        }

        class Frame
        {
            public RoomPiece     priorRoom;
            public ExitPoint     priorExit;
            public RoomPiece     hallPiece;
            public RoomPiece     roomPiece;
            public ExitPoint     hallEntry;
            public ExitPoint     hallExit;
            public ExitPoint     roomEntry;
            public RoomCategory? category;   // null for Boss; spine slots use one of the four
            public int           slotIndex;
        }

        // ── Public entry ──────────────────────────────────────────────────────

        public static GenerationResult Generate(LevelGenSettings settings)
        {
            var result = new GenerationResult { BranchesRequested = settings.branchSlotCount };
            var sw     = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                int resolvedSeed = settings.seed != 0
                    ? settings.seed
                    : new System.Random().Next(1, int.MaxValue);
                var rng     = new System.Random(resolvedSeed);
                result.Seed = resolvedSeed;
                result.Log.Add($"Seed: {resolvedSeed}");

                // Load pools
                var starterPool    = V2PrefabSource.GetRoomPrefabs(RoomCategory.Starter);
                var bossPool       = V2PrefabSource.GetRoomPrefabs(RoomCategory.Boss);
                var smallPool      = V2PrefabSource.GetRoomPrefabs(RoomCategory.Small);
                var mediumPool     = V2PrefabSource.GetRoomPrefabs(RoomCategory.Medium);
                var largePool      = V2PrefabSource.GetRoomPrefabs(RoomCategory.Large);
                var specialPool    = V2PrefabSource.GetRoomPrefabs(RoomCategory.Special);
                var spineHallPool  = V2PrefabSource.GetHallPrefabs(settings.spineHallSize);
                var branchHallPool = V2PrefabSource.GetHallPrefabs(settings.branchHallSize);

                // Validate pools — fail fast with a clear error pointing at the empty folder
                if (starterPool.Count == 0)
                    return Fail(result, sw, "No prefabs in Assets/Prefabs/Rooms/Starter/ (folder missing or no RoomPiece prefabs).");
                if (bossPool.Count == 0)
                    return Fail(result, sw, "No prefabs in Assets/Prefabs/Rooms/Boss/ (folder missing or no RoomPiece prefabs).");
                if (spineHallPool.Count == 0)
                    return Fail(result, sw, $"No prefabs in Assets/Prefabs/Halls/{settings.spineHallSize}/ (folder missing or no RoomPiece prefabs).");
                if (settings.smallCount > 0 && smallPool.Count == 0)
                    return Fail(result, sw, $"smallCount={settings.smallCount} but Assets/Prefabs/Rooms/Small/ has no RoomPiece prefabs.");
                if (settings.mediumCount > 0 && mediumPool.Count == 0)
                    return Fail(result, sw, $"mediumCount={settings.mediumCount} but Assets/Prefabs/Rooms/Medium/ has no RoomPiece prefabs.");
                if (settings.largeCount > 0 && largePool.Count == 0)
                    return Fail(result, sw, $"largeCount={settings.largeCount} but Assets/Prefabs/Rooms/Large/ has no RoomPiece prefabs.");
                if (settings.specialCount > 0 && specialPool.Count == 0)
                    return Fail(result, sw, $"specialCount={settings.specialCount} but Assets/Prefabs/Rooms/Special/ has no RoomPiece prefabs.");
                if (settings.branchSlotCount > 0 && branchHallPool.Count == 0)
                    return Fail(result, sw, $"branchSlotCount={settings.branchSlotCount} but Assets/Prefabs/Halls/{settings.branchHallSize}/ has no RoomPiece prefabs.");

                if (!string.IsNullOrEmpty(settings.themeName))
                    result.Log.Add($"Theme: {settings.themeName} (Phase C uses raw folder lookup; theme override deferred to Phase D+)");

                // Category → prefab list lookup
                var roomPools = new Dictionary<RoomCategory, List<GameObject>>
                {
                    { RoomCategory.Small,   smallPool   },
                    { RoomCategory.Medium,  mediumPool  },
                    { RoomCategory.Large,   largePool   },
                    { RoomCategory.Special, specialPool },
                };

                // Create root
                var root = new GameObject("GeneratedLevel");
                Undo.RegisterCreatedObjectUndo(root, "Generate V2 Level");
                result.Root = root;

                // Place Starter
                var starterPrefab = starterPool[rng.Next(starterPool.Count)];
                var starterGO     = (GameObject)PrefabUtility.InstantiatePrefab(starterPrefab, root.transform);
                starterGO.transform.localPosition = Vector3.zero;
                starterGO.transform.localRotation = Quaternion.identity;
                var starterPiece = starterGO.GetComponent<RoomPiece>();
                starterPiece.RefreshExits();

                var placed = new List<RoomPiece> { starterPiece };
                result.RoomsPlaced = 1;

                // ── Spine + Boss loop with backtracking ────────────────────────
                var pool = new CategoryPool
                {
                    small   = settings.smallCount,
                    medium  = settings.mediumCount,
                    large   = settings.largeCount,
                    special = settings.specialCount,
                };

                int spineLen        = settings.SpineLength;
                int targetSlotCount = spineLen + 1;     // +1 for Boss

                var slots        = new List<Frame>();
                var triedAtSlot  = new Dictionary<int, HashSet<RoomCategory>>();
                int backtracks   = 0;

                while (slots.Count < targetSlotCount)
                {
                    int  slotIdx = slots.Count;
                    bool isBoss  = (slotIdx == spineLen);

                    Frame frame = isBoss
                        ? TryPlaceBoss(slots, starterPiece, bossPool, spineHallPool, placed, root, rng, slotIdx)
                        : TryPlaceSpineSlot(slotIdx, slots, starterPiece,
                                            pool, roomPools, spineHallPool,
                                            placed, root, rng, triedAtSlot);

                    if (frame != null)
                    {
                        slots.Add(frame);
                        result.RoomsPlaced++;
                        result.HallsPlaced++;
                        continue;
                    }

                    // ── Failure: backtrack ──────────────────────────────────
                    if (slots.Count == 0)
                    {
                        DestroyRootAndClearSelection(root);
                        result.Root = null;
                        return Fail(result, sw,
                            $"Slot 0 placement failed — no candidate fits any exit on Starter (or no halls in {settings.spineHallSize} pool match).");
                    }

                    backtracks++;
                    if (backtracks > MaxBacktracks)
                    {
                        DestroyRootAndClearSelection(root);
                        result.Root = null;
                        return Fail(result, sw,
                            $"Backtrack cap ({MaxBacktracks}) exceeded at slot {slotIdx}.");
                    }

                    var popped = slots[slots.Count - 1];
                    slots.RemoveAt(slots.Count - 1);

                    // Restore popped category to pool and mark it tried at its slot
                    if (popped.category.HasValue)
                    {
                        pool.Increment(popped.category.Value);
                        if (!triedAtSlot.ContainsKey(popped.slotIndex))
                            triedAtSlot[popped.slotIndex] = new HashSet<RoomCategory>();
                        triedAtSlot[popped.slotIndex].Add(popped.category.Value);
                    }

                    if (triedAtSlot.ContainsKey(slotIdx))
                        triedAtSlot[slotIdx].Clear();

                    placed.Remove(popped.roomPiece);
                    placed.Remove(popped.hallPiece);
                    Object.DestroyImmediate(popped.roomPiece.gameObject);
                    Object.DestroyImmediate(popped.hallPiece.gameObject);
                    result.RoomsPlaced--;
                    result.HallsPlaced--;

                    if (popped.priorExit != null) popped.priorExit.isConnected = false;
                }

                // ── Branch pass ─────────────────────────────────────────────────
                // Snapshot of room pieces (Starter + spine + Boss). Branches placed
                // during this loop are also eligible attach candidates for
                // subsequent branches, so the list grows.
                var attachRooms = new List<RoomPiece> { starterPiece };
                foreach (var f in slots) attachRooms.Add(f.roomPiece);

                int branchesPlaced = 0;
                for (int b = 0; b < settings.branchSlotCount; b++)
                {
                    if (pool.Total == 0)
                    {
                        result.Log.Add($"Branch slot {b} skipped: combined pool empty.");
                        Debug.LogWarning($"[V2 Gen] Branch slot {b} skipped: combined pool empty.");
                        break;
                    }

                    var triedAttachRooms = new HashSet<RoomPiece>();
                    bool placedThisSlot  = false;

                    while (!placedThisSlot)
                    {
                        var available = attachRooms
                            .Where(r => r != null
                                        && !triedAttachRooms.Contains(r)
                                        && HorizontalExits(r).Count > 0)
                            .ToList();

                        if (available.Count == 0)
                        {
                            result.Log.Add($"Branch slot {b} skipped: no attach room with an unused exit.");
                            Debug.LogWarning($"[V2 Gen] Branch slot {b} skipped: no attach room with an unused exit.");
                            break;
                        }

                        var attachRoom  = available[rng.Next(available.Count)];
                        var attachExits = HorizontalExits(attachRoom);
                        var attachExit  = attachExits[rng.Next(attachExits.Count)];

                        if (!pool.DrawRandom(rng, out RoomCategory cat))
                        {
                            result.Log.Add($"Branch slot {b} skipped: pool emptied mid-attempt.");
                            Debug.LogWarning($"[V2 Gen] Branch slot {b} skipped: pool emptied mid-attempt.");
                            break;
                        }

                        var candidates = roomPools[cat];

                        if (TryPlaceConnectedRoom(attachRoom, attachExit, candidates,
                                                  branchHallPool, placed, root, rng,
                                                  out PlacementOutput output))
                        {
                            pool.Decrement(cat);
                            branchesPlaced++;
                            attachRooms.Add(output.RoomPiece);
                            result.RoomsPlaced++;
                            result.HallsPlaced++;
                            placedThisSlot = true;
                        }
                        else
                        {
                            triedAttachRooms.Add(attachRoom);
                        }
                    }
                }

                result.BranchesPlaced = branchesPlaced;
                result.Success        = true;
                result.BacktrackCount = backtracks;
                result.Log.Add($"Done: {result.RoomsPlaced} rooms, {result.HallsPlaced} halls, " +
                               $"{branchesPlaced}/{settings.branchSlotCount} branches, {backtracks} backtracks.");
            }
            catch (System.Exception e)
            {
                if (result.Root != null)
                {
                    DestroyRootAndClearSelection(result.Root);
                    result.Root = null;
                }
                return Fail(result, sw, $"Exception: {e.Message}");
            }
            finally
            {
                sw.Stop();
                result.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            }

            return result;
        }

        // ── Spine slot placement ──────────────────────────────────────────────

        static Frame TryPlaceSpineSlot(
            int slotIdx,
            List<Frame> slots,
            RoomPiece starterPiece,
            CategoryPool pool,
            Dictionary<RoomCategory, List<GameObject>> roomPools,
            List<GameObject> spineHallPool,
            List<RoomPiece> placed,
            GameObject root,
            System.Random rng,
            Dictionary<int, HashSet<RoomCategory>> triedAtSlot)
        {
            if (!triedAtSlot.ContainsKey(slotIdx))
                triedAtSlot[slotIdx] = new HashSet<RoomCategory>();

            RoomPiece prior = (slots.Count > 0) ? slots[slots.Count - 1].roomPiece : starterPiece;

            var unusedExits = HorizontalExits(prior);
            if (unusedExits.Count == 0) return null;

            ExitPoint priorExit = unusedExits[rng.Next(unusedExits.Count)];

            // Try categories in random-weighted order, excluding ones already tried at this slot.
            while (true)
            {
                if (!pool.DrawRandom(rng, triedAtSlot[slotIdx], out RoomCategory cat))
                    return null;

                var candidates = roomPools.TryGetValue(cat, out var p) ? p : null;
                if (candidates == null || candidates.Count == 0)
                {
                    triedAtSlot[slotIdx].Add(cat);
                    continue;
                }

                if (TryPlaceConnectedRoom(prior, priorExit, candidates, spineHallPool,
                                          placed, root, rng, out PlacementOutput output))
                {
                    pool.Decrement(cat);
                    return new Frame
                    {
                        priorRoom = prior,
                        priorExit = priorExit,
                        hallPiece = output.HallPiece,
                        roomPiece = output.RoomPiece,
                        hallEntry = output.HallEntry,
                        hallExit  = output.HallExit,
                        roomEntry = output.RoomEntry,
                        category  = cat,
                        slotIndex = slotIdx,
                    };
                }

                triedAtSlot[slotIdx].Add(cat);
            }
        }

        static Frame TryPlaceBoss(
            List<Frame> slots,
            RoomPiece starterPiece,
            List<GameObject> bossPool,
            List<GameObject> spineHallPool,
            List<RoomPiece> placed,
            GameObject root,
            System.Random rng,
            int slotIdx)
        {
            RoomPiece prior = (slots.Count > 0) ? slots[slots.Count - 1].roomPiece : starterPiece;

            var unusedExits = HorizontalExits(prior);
            if (unusedExits.Count == 0) return null;

            ExitPoint priorExit = unusedExits[rng.Next(unusedExits.Count)];

            if (!TryPlaceConnectedRoom(prior, priorExit, bossPool, spineHallPool,
                                       placed, root, rng, out PlacementOutput output))
                return null;

            return new Frame
            {
                priorRoom = prior,
                priorExit = priorExit,
                hallPiece = output.HallPiece,
                roomPiece = output.RoomPiece,
                hallEntry = output.HallEntry,
                hallExit  = output.HallExit,
                roomEntry = output.RoomEntry,
                category  = null,    // Boss is not drawn from the pool
                slotIndex = slotIdx,
            };
        }

        // ── Core placement: hall + room ───────────────────────────────────────

        /// <summary>
        /// Instantiates a hall + room pair, snaps the hall's entry exit to
        /// <paramref name="priorExit"/>, then snaps the room's entry exit to the hall's
        /// other exit. Iterates room candidates × {0/90/180/270} rotations × random
        /// hall pick. On success, marks the four involved exits as connected and
        /// appends both pieces to <paramref name="placed"/>. On failure, destroys any
        /// partial instantiation and leaves all state untouched. Used by both spine
        /// and branch placement.
        /// </summary>
        static bool TryPlaceConnectedRoom(
            RoomPiece priorRoom,
            ExitPoint priorExit,
            List<GameObject> roomCandidates,
            List<GameObject> hallPool,
            List<RoomPiece> placed,
            GameObject root,
            System.Random rng,
            out PlacementOutput output)
        {
            output = default;
            if (priorExit == null) return false;
            if (roomCandidates == null || roomCandidates.Count == 0) return false;
            if (hallPool == null || hallPool.Count == 0) return false;

            var candidates = ShuffleCopy(roomCandidates, rng);
            var rotations  = new[] { 0f, 90f, 180f, 270f };

            foreach (var roomPrefab in candidates)
            {
                var roomRotations = ShuffleCopy(rotations, rng);

                foreach (var roomRotY in roomRotations)
                {
                    var hallPrefab = hallPool[rng.Next(hallPool.Count)];
                    var hallGO     = (GameObject)PrefabUtility.InstantiatePrefab(hallPrefab, root.transform);
                    hallGO.transform.localPosition = Vector3.zero;
                    hallGO.transform.localRotation = Quaternion.identity;

                    var hallPiece = hallGO.GetComponent<RoomPiece>();
                    hallPiece.RefreshExits();

                    var hallExits = HorizontalExits(hallPiece);
                    if (hallExits.Count < 2)
                    {
                        Object.DestroyImmediate(hallGO);
                        continue;
                    }

                    var hallEntry = hallExits[rng.Next(hallExits.Count)];
                    SnapExitToExit(hallGO, hallEntry, priorExit);

                    if (Overlaps(hallPiece, placed))
                    {
                        Object.DestroyImmediate(hallGO);
                        continue;
                    }

                    var hallExitOptions = hallExits.Where(e => e != hallEntry).ToList();
                    var hallExit       = hallExitOptions[rng.Next(hallExitOptions.Count)];

                    var roomGO = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, root.transform);
                    roomGO.transform.localPosition = Vector3.zero;
                    roomGO.transform.localRotation = Quaternion.Euler(0f, roomRotY, 0f);

                    var roomPiece = roomGO.GetComponent<RoomPiece>();
                    roomPiece.RefreshExits();

                    Vector3   desiredFwd = -hallExit.transform.forward;
                    ExitPoint roomEntry  = FindExitWithWorldForward(roomPiece, desiredFwd);

                    if (roomEntry == null)
                    {
                        Object.DestroyImmediate(roomGO);
                        Object.DestroyImmediate(hallGO);
                        continue;
                    }

                    Vector3 offset = hallExit.transform.position - roomEntry.transform.position;
                    roomGO.transform.position += offset;
                    SnapToCardinalY(roomGO.transform);

                    var combined = new List<RoomPiece>(placed) { hallPiece };
                    if (Overlaps(roomPiece, combined))
                    {
                        Object.DestroyImmediate(roomGO);
                        Object.DestroyImmediate(hallGO);
                        continue;
                    }

                    // Success — mark exits as connected, append, return
                    priorExit.isConnected = true;
                    hallEntry.isConnected = true;
                    hallExit.isConnected  = true;
                    roomEntry.isConnected = true;

                    placed.Add(hallPiece);
                    placed.Add(roomPiece);

                    output = new PlacementOutput
                    {
                        HallPiece = hallPiece,
                        RoomPiece = roomPiece,
                        HallEntry = hallEntry,
                        HallExit  = hallExit,
                        RoomEntry = roomEntry,
                    };
                    return true;
                }
            }

            return false;
        }

        // ── Snap math ─────────────────────────────────────────────────────────

        /// <summary>
        /// Rotates and translates <paramref name="incoming"/> so that
        /// <paramref name="entryExit"/>'s world transform faces the negation of
        /// <paramref name="targetExit"/>'s world forward and shares its world
        /// position. Final Y rotation is snapped to the nearest 90°.
        /// </summary>
        static void SnapExitToExit(GameObject incoming, ExitPoint entryExit, ExitPoint targetExit)
        {
            Vector3 desiredForward = -targetExit.transform.forward;
            Quaternion desired     = Quaternion.LookRotation(desiredForward, Vector3.up);
            Quaternion current     = entryExit.transform.rotation;
            Quaternion delta       = desired * Quaternion.Inverse(current);
            incoming.transform.rotation = delta * incoming.transform.rotation;

            SnapToCardinalY(incoming.transform);

            Vector3 offset = targetExit.transform.position - entryExit.transform.position;
            incoming.transform.position += offset;
        }

        static void SnapToCardinalY(Transform t)
        {
            Vector3 e        = t.eulerAngles;
            float   ySnapped = Mathf.Round(e.y / 90f) * 90f;
            t.rotation = Quaternion.Euler(0f, ySnapped, 0f);
        }

        // ── Bounds / collision ────────────────────────────────────────────────

        /// <summary>
        /// Returns <see cref="RoomPiece.GetWorldBounds"/> with X/Z extents swapped
        /// when the piece's Y rotation is a 90° or 270° quarter turn.
        /// <see cref="RoomPiece.GetWorldBounds"/> itself does not account for rotation.
        /// </summary>
        static Bounds GetRotationAwareWorldBounds(RoomPiece piece)
        {
            Bounds raw = piece.GetWorldBounds();
            float  yEulerNormalized = Mathf.Repeat(piece.transform.eulerAngles.y, 360f);
            bool   quarterTurn = Mathf.Approximately(yEulerNormalized, 90f) ||
                                 Mathf.Approximately(yEulerNormalized, 270f);
            if (!quarterTurn) return raw;

            var sz = raw.size;
            return new Bounds(raw.center, new Vector3(sz.z, sz.y, sz.x));
        }

        static bool Overlaps(RoomPiece candidate, List<RoomPiece> placed)
        {
            Bounds a = GetRotationAwareWorldBounds(candidate);
            // Tiny shrink to avoid edge-touching false positives at shared wall planes.
            a.size = new Vector3(
                Mathf.Max(0f, a.size.x - CollisionEpsilon * 2f),
                Mathf.Max(0f, a.size.y - CollisionEpsilon * 2f),
                Mathf.Max(0f, a.size.z - CollisionEpsilon * 2f));

            foreach (var p in placed)
            {
                if (p == candidate) continue;
                Bounds b = GetRotationAwareWorldBounds(p);
                if (a.Intersects(b)) return true;
            }
            return false;
        }

        // ── Exit helpers ──────────────────────────────────────────────────────

        static List<ExitPoint> HorizontalExits(RoomPiece piece)
        {
            var result = new List<ExitPoint>();
            foreach (var ep in piece.exits)
            {
                if (ep == null) continue;
                if (ep.exitDirection == ExitPoint.Direction.Up ||
                    ep.exitDirection == ExitPoint.Direction.Down) continue;
                if (ep.isConnected || ep.isSealed) continue;
                result.Add(ep);
            }
            return result;
        }

        static ExitPoint FindExitWithWorldForward(RoomPiece piece, Vector3 desiredForward)
        {
            Vector3 d = desiredForward.normalized;
            foreach (var ep in piece.exits)
            {
                if (ep == null) continue;
                if (ep.exitDirection == ExitPoint.Direction.Up ||
                    ep.exitDirection == ExitPoint.Direction.Down) continue;
                if (ep.isConnected || ep.isSealed) continue;
                if (Vector3.Dot(ep.transform.forward.normalized, d) > 0.99f)
                    return ep;
            }
            return null;
        }

        // ── Random helpers ────────────────────────────────────────────────────

        static List<T> ShuffleCopy<T>(IEnumerable<T> source, System.Random rng)
        {
            var list = new List<T>(source);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        // ── Failure helpers ───────────────────────────────────────────────────

        static GenerationResult Fail(GenerationResult result,
                                     System.Diagnostics.Stopwatch sw,
                                     string reason)
        {
            sw.Stop();
            result.Success        = false;
            result.FailureReason  = reason;
            result.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            result.Log.Add($"FAIL: {reason}");
            return result;
        }

        static void DestroyRootAndClearSelection(GameObject root)
        {
            if (root == null) return;
            if (Selection.activeGameObject == root)
                Selection.activeGameObject = null;
            Object.DestroyImmediate(root);
        }
    }
}
#endif
