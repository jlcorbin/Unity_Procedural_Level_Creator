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
        public int         RoomsPlaced;
        public int         HallsPlaced;
        public int         BacktrackCount;
        public double      ElapsedSeconds;
        public List<string> Log = new List<string>();
    }

    /// <summary>
    /// Phase B spine-only generator. Places Starter at world origin, walks down a
    /// linear spine of rooms (random pick from remaining Small/Medium/Large budget)
    /// connected by spine-size halls, ending with the Boss room. Backtracks up to
    /// <see cref="MaxBacktracks"/> times when a slot can't be placed.
    /// Branches, theme-aware selection, scene save, and manifest output are
    /// out of scope for this phase.
    /// </summary>
    public static class V2LevelGenerator
    {
        const int   MaxBacktracks    = 50;
        const float CollisionEpsilon = 0.01f;

        // ── Frame ─────────────────────────────────────────────────────────────

        class Frame
        {
            public RoomPiece    priorRoom;
            public ExitPoint    priorExit;
            public RoomPiece    hallPiece;
            public RoomPiece    roomPiece;
            public ExitPoint    hallEntry;
            public ExitPoint    hallExit;
            public ExitPoint    roomEntry;
            public RoomCategory? category;   // null for Boss; spine slots use Small/Medium/Large
            public int          slotIndex;
        }

        // ── Public entry ──────────────────────────────────────────────────────

        public static GenerationResult Generate(LevelGenSettings settings)
        {
            var result = new GenerationResult();
            var sw     = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                int resolvedSeed = settings.seed != 0
                    ? settings.seed
                    : new System.Random().Next(1, int.MaxValue);
                var rng = new System.Random(resolvedSeed);
                result.Log.Add($"Seed: {resolvedSeed}");

                // Load pools
                var starterPool = V2PrefabSource.GetRoomPrefabs(RoomCategory.Starter);
                var bossPool    = V2PrefabSource.GetRoomPrefabs(RoomCategory.Boss);
                var smallPool   = V2PrefabSource.GetRoomPrefabs(RoomCategory.Small);
                var mediumPool  = V2PrefabSource.GetRoomPrefabs(RoomCategory.Medium);
                var largePool   = V2PrefabSource.GetRoomPrefabs(RoomCategory.Large);
                var hallPool    = V2PrefabSource.GetHallPrefabs(settings.spineHallSize);

                if (starterPool.Count == 0)
                    return Fail(result, sw, "No prefabs in Assets/Prefabs/Rooms/Starter/ (folder missing or no RoomPiece prefabs).");
                if (bossPool.Count == 0)
                    return Fail(result, sw, "No prefabs in Assets/Prefabs/Rooms/Boss/ (folder missing or no RoomPiece prefabs).");
                if (hallPool.Count == 0)
                    return Fail(result, sw, $"No prefabs in Assets/Prefabs/Halls/{settings.spineHallSize}/ (folder missing or no RoomPiece prefabs).");
                if (settings.smallCount > 0 && smallPool.Count == 0)
                    return Fail(result, sw, $"smallCount={settings.smallCount} but Assets/Prefabs/Rooms/Small/ has no RoomPiece prefabs.");
                if (settings.mediumCount > 0 && mediumPool.Count == 0)
                    return Fail(result, sw, $"mediumCount={settings.mediumCount} but Assets/Prefabs/Rooms/Medium/ has no RoomPiece prefabs.");
                if (settings.largeCount > 0 && largePool.Count == 0)
                    return Fail(result, sw, $"largeCount={settings.largeCount} but Assets/Prefabs/Rooms/Large/ has no RoomPiece prefabs.");

                if (!string.IsNullOrEmpty(settings.themeName))
                    result.Log.Add($"Theme: {settings.themeName} (Phase B uses raw folder lookup; theme override deferred to Phase C+)");

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

                // Spine + Boss loop with backtracking
                int s = settings.smallCount;
                int m = settings.mediumCount;
                int l = settings.largeCount;
                int spineLen        = s + m + l;
                int targetSlotCount = spineLen + 1;     // +1 for Boss

                var slots        = new List<Frame>();
                var triedAtSlot  = new Dictionary<int, HashSet<RoomCategory>>();
                int backtracks   = 0;

                while (slots.Count < targetSlotCount)
                {
                    int  slotIdx = slots.Count;
                    bool isBoss  = (slotIdx == spineLen);

                    Frame frame = isBoss
                        ? TryPlaceBoss(slots, starterPiece, bossPool, hallPool, placed, root, rng, slotIdx)
                        : TryPlaceSpineSlot(slotIdx, slots, starterPiece,
                                            ref s, ref m, ref l,
                                            smallPool, mediumPool, largePool,
                                            hallPool, placed, root, rng, triedAtSlot);

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

                    // Restore popped category to budget and mark it tried at its slot
                    if (popped.category.HasValue)
                    {
                        switch (popped.category.Value)
                        {
                            case RoomCategory.Small:  s++; break;
                            case RoomCategory.Medium: m++; break;
                            case RoomCategory.Large:  l++; break;
                        }
                        if (!triedAtSlot.ContainsKey(popped.slotIndex))
                            triedAtSlot[popped.slotIndex] = new HashSet<RoomCategory>();
                        triedAtSlot[popped.slotIndex].Add(popped.category.Value);
                    }

                    // Reset the failed slot's tried set — next attempt is with a different prior
                    if (triedAtSlot.ContainsKey(slotIdx))
                        triedAtSlot[slotIdx].Clear();

                    // Destroy popped pieces
                    placed.Remove(popped.roomPiece);
                    placed.Remove(popped.hallPiece);
                    Object.DestroyImmediate(popped.roomPiece.gameObject);
                    Object.DestroyImmediate(popped.hallPiece.gameObject);
                    result.RoomsPlaced--;
                    result.HallsPlaced--;

                    // Reset prior exit so it can be reused
                    if (popped.priorExit != null) popped.priorExit.isConnected = false;
                }

                result.Success         = true;
                result.BacktrackCount  = backtracks;
                result.Log.Add($"Done: {result.RoomsPlaced} rooms, {result.HallsPlaced} halls, {backtracks} backtracks.");
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

        // ── Slot placement ────────────────────────────────────────────────────

        static Frame TryPlaceSpineSlot(
            int slotIdx,
            List<Frame> slots,
            RoomPiece starterPiece,
            ref int s, ref int m, ref int l,
            List<GameObject> smallPool,
            List<GameObject> mediumPool,
            List<GameObject> largePool,
            List<GameObject> hallPool,
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

            // Pick category from remaining budget (excluding tried), retry on category failure
            while (true)
            {
                RoomCategory? cat = PickWeightedCategory(s, m, l, triedAtSlot[slotIdx], rng);
                if (cat == null) return null;

                List<GameObject> pool = cat.Value switch
                {
                    RoomCategory.Small  => smallPool,
                    RoomCategory.Medium => mediumPool,
                    RoomCategory.Large  => largePool,
                    _                   => null,
                };

                if (pool == null || pool.Count == 0)
                {
                    triedAtSlot[slotIdx].Add(cat.Value);
                    continue;
                }

                Frame frame = TryPlaceRoomViaHall(prior, priorExit, pool, hallPool, placed, root, rng, slotIdx, cat.Value);
                if (frame != null)
                {
                    switch (cat.Value)
                    {
                        case RoomCategory.Small:  s--; break;
                        case RoomCategory.Medium: m--; break;
                        case RoomCategory.Large:  l--; break;
                    }
                    return frame;
                }

                triedAtSlot[slotIdx].Add(cat.Value);
            }
        }

        static Frame TryPlaceBoss(
            List<Frame> slots,
            RoomPiece starterPiece,
            List<GameObject> bossPool,
            List<GameObject> hallPool,
            List<RoomPiece> placed,
            GameObject root,
            System.Random rng,
            int slotIdx)
        {
            RoomPiece prior = (slots.Count > 0) ? slots[slots.Count - 1].roomPiece : starterPiece;

            var unusedExits = HorizontalExits(prior);
            if (unusedExits.Count == 0) return null;

            ExitPoint priorExit = unusedExits[rng.Next(unusedExits.Count)];

            return TryPlaceRoomViaHall(prior, priorExit, bossPool, hallPool, placed, root, rng, slotIdx, null);
        }

        // ── Core placement: hall + room ───────────────────────────────────────

        static Frame TryPlaceRoomViaHall(
            RoomPiece priorRoom,
            ExitPoint priorExit,
            List<GameObject> roomCandidates,
            List<GameObject> hallPool,
            List<RoomPiece> placed,
            GameObject root,
            System.Random rng,
            int slotIdx,
            RoomCategory? category)
        {
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

                    // Place candidate room at the iterated rotation
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

                    // Success — mark exits as connected
                    priorExit.isConnected = true;
                    hallEntry.isConnected = true;
                    hallExit.isConnected  = true;
                    roomEntry.isConnected = true;

                    placed.Add(hallPiece);
                    placed.Add(roomPiece);

                    return new Frame
                    {
                        priorRoom = priorRoom,
                        priorExit = priorExit,
                        hallPiece = hallPiece,
                        roomPiece = roomPiece,
                        hallEntry = hallEntry,
                        hallExit  = hallExit,
                        roomEntry = roomEntry,
                        category  = category,
                        slotIndex = slotIdx,
                    };
                }
            }

            return null;
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

        // ── Random / category helpers ─────────────────────────────────────────

        static RoomCategory? PickWeightedCategory(int s, int m, int l,
                                                  HashSet<RoomCategory> excluded,
                                                  System.Random rng)
        {
            int sw = excluded.Contains(RoomCategory.Small)  ? 0 : s;
            int mw = excluded.Contains(RoomCategory.Medium) ? 0 : m;
            int lw = excluded.Contains(RoomCategory.Large)  ? 0 : l;
            int total = sw + mw + lw;
            if (total <= 0) return null;

            int pick = rng.Next(total);
            if (pick < sw)       return RoomCategory.Small;
            if (pick < sw + mw)  return RoomCategory.Medium;
            return RoomCategory.Large;
        }

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
