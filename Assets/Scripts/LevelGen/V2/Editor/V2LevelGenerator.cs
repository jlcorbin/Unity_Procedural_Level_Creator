#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelGen.V2
{
    /// <summary>One row in the manifest's Placements table.</summary>
    [Serializable]
    public class PlacementRecord
    {
        public int     order;              // 1-based, assigned at end of generation
        public string  kind;               // "Starter" | "Spine" | "Boss" | "Branch" | "Hall:Spine" | "Hall:Branch"
        public string  prefabName;         // file name without extension
        public string  roomCategory;       // "Starter"/"Boss"/"Small"/"Medium"/"Large"/"Special"/"-" for halls
        public Vector3 worldPosition;
        public float   yRotation;          // 0 / 90 / 180 / 270
        public float?  hallSlackOptional;  // hall-only: gap-vs-length (always 0 in V2; null for non-halls)
    }

    /// <summary>Result returned by <see cref="V2LevelGenerator.Generate"/>.</summary>
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
        public string      ScenePath;       // null until SaveGeneratedLevel succeeds
        public string      ManifestPath;    // null until SaveGeneratedLevel succeeds
        public List<string> Log = new List<string>();
    }

    /// <summary>Result returned by <see cref="V2LevelGenerator.SaveGeneratedLevel"/>.</summary>
    public class SaveOutcome
    {
        public bool   Success;
        public string ScenePath;     // null on failure or cancel
        public string ManifestPath;  // null on failure or cancel
        public string FailureReason; // null on success
    }

    /// <summary>
    /// Phase B + C generator. Places Starter at world origin, walks down a linear
    /// spine of rooms (Small/Medium/Large/Special drawn from a single combined pool
    /// weighted by remaining counts) connected by spine-size halls, ends with the
    /// Boss. After Boss, attaches branches (same pool, branch-size halls) onto
    /// random rooms with unused exits — including earlier branches. Phase D-revision
    /// drops the auto-save: the generator always parents to the active scene, and a
    /// separate Save button writes the .unity file + manifest at user-chosen path.
    /// </summary>
    public static class V2LevelGenerator
    {
        const int    MaxBacktracks = 50;
        const float  CollisionEpsilon = 0.01f;

        /// <summary>
        /// Snapshot of the placement records from the most recent successful Generate.
        /// Read by the EditorWindow when it calls SaveGeneratedLevel. A subsequent
        /// Generate overwrites this — you save what's currently in the scene.
        /// </summary>
        public static List<PlacementRecord> LastPlacements { get; private set; }
            = new List<PlacementRecord>();

        // ── State types ───────────────────────────────────────────────────────

        /// <summary>Combined remaining-count pool for the four pool categories.</summary>
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
            public RoomCategory? category;
            public int           slotIndex;
        }

        // ── Public entry: Generate ────────────────────────────────────────────

        public static GenerationResult Generate(LevelGenSettings settings)
        {
            var result = new GenerationResult { BranchesRequested = settings.branchSlotCount };
            var sw     = System.Diagnostics.Stopwatch.StartNew();

            GameObject root       = null;
            var        placements = new List<PlacementRecord>();

            // Reset cached placements at the start of every attempt so the window's
            // Save button never operates on stale data after a failed/aborted run.
            LastPlacements = new List<PlacementRecord>();

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
                    result.Log.Add($"Theme: {settings.themeName} (theme-aware selection deferred — pulling from raw folders)");

                var roomPools = new Dictionary<RoomCategory, List<GameObject>>
                {
                    { RoomCategory.Small,   smallPool   },
                    { RoomCategory.Medium,  mediumPool  },
                    { RoomCategory.Large,   largePool   },
                    { RoomCategory.Special, specialPool },
                };

                // Destroy any prior GeneratedLevel root in the active scene so successive
                // Generate clicks don't pile up duplicates.
                var prior = GameObject.Find("GeneratedLevel");
                if (prior != null)
                {
                    if (Selection.activeGameObject == prior)
                        Selection.activeGameObject = null;
                    UnityEngine.Object.DestroyImmediate(prior);
                }

                // Create root in the currently active scene
                root = new GameObject("GeneratedLevel");
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
                placements.Add(MakeRoomRecord(starterPiece, "Starter", "Starter"));

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
                        placements.Add(MakeHallRecord(frame.hallPiece, "Hall:Spine"));
                        placements.Add(MakeRoomRecord(frame.roomPiece,
                            isBoss ? "Boss" : "Spine",
                            isBoss ? "Boss" : frame.category.ToString()));
                        continue;
                    }

                    // ── Failure: backtrack ──────────────────────────────────
                    if (slots.Count == 0)
                    {
                        DestroyRoot(root);
                        result.Root = null;
                        return Fail(result, sw,
                            $"Slot 0 placement failed — no candidate fits any exit on Starter (or no halls in {settings.spineHallSize} pool match).");
                    }

                    backtracks++;
                    if (backtracks > MaxBacktracks)
                    {
                        DestroyRoot(root);
                        result.Root = null;
                        return Fail(result, sw,
                            $"Backtrack cap ({MaxBacktracks}) exceeded at slot {slotIdx}.");
                    }

                    var popped = slots[slots.Count - 1];
                    slots.RemoveAt(slots.Count - 1);

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
                    UnityEngine.Object.DestroyImmediate(popped.roomPiece.gameObject);
                    UnityEngine.Object.DestroyImmediate(popped.hallPiece.gameObject);
                    result.RoomsPlaced--;
                    result.HallsPlaced--;

                    if (placements.Count >= 2)
                    {
                        placements.RemoveAt(placements.Count - 1);
                        placements.RemoveAt(placements.Count - 1);
                    }

                    if (popped.priorExit != null) popped.priorExit.isConnected = false;
                }

                // ── Branch pass ─────────────────────────────────────────────────
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

                            placements.Add(MakeHallRecord(output.HallPiece, "Hall:Branch"));
                            placements.Add(MakeRoomRecord(output.RoomPiece, "Branch", cat.ToString()));
                        }
                        else
                        {
                            triedAttachRooms.Add(attachRoom);
                        }
                    }
                }

                result.BranchesPlaced = branchesPlaced;
                result.BacktrackCount = backtracks;

                // Final ordering for the manifest
                for (int i = 0; i < placements.Count; i++)
                    placements[i].order = i + 1;

                // Cache placements for the Save button
                LastPlacements = new List<PlacementRecord>(placements);

                // Select root for user visibility — same as Phase B/C UX
                Selection.activeGameObject = root;

                result.Success = true;
                result.Log.Add($"Done: {result.RoomsPlaced} rooms, {result.HallsPlaced} halls, " +
                               $"{branchesPlaced}/{settings.branchSlotCount} branches, {backtracks} backtracks.");
            }
            catch (System.Exception e)
            {
                if (root != null)
                {
                    DestroyRoot(root);
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

        // ── Public entry: Save ────────────────────────────────────────────────

        /// <summary>
        /// Saves the generated GeneratedLevel root (currently in the active scene)
        /// to a brand-new .unity file at <paramref name="scenePath"/>, with a sibling
        /// <c>{sceneName}_manifest.txt</c>. Updates <paramref name="settings"/>'s
        /// sceneName/outputFolder to match the chosen path so the manifest reflects
        /// where it was saved. Returns Success=false (and writes nothing) if the
        /// user cancels the existing-file overwrite prompt.
        /// </summary>
        public static SaveOutcome SaveGeneratedLevel(
            GameObject root,
            GenerationResult lastResult,
            LevelGenSettings settings,
            List<PlacementRecord> placements,
            string scenePath)
        {
            var outcome = new SaveOutcome();

            if (root == null)
            {
                outcome.FailureReason = "No GeneratedLevel root to save (was it deleted?).";
                return outcome;
            }
            if (lastResult == null)
            {
                outcome.FailureReason = "No prior generation result to save.";
                return outcome;
            }
            if (settings == null)
            {
                outcome.FailureReason = "Settings reference is null.";
                return outcome;
            }
            if (string.IsNullOrEmpty(scenePath))
            {
                outcome.FailureReason = "Save path is empty.";
                return outcome;
            }

            string normalizedPath = scenePath.Replace('\\', '/');
            if (!normalizedPath.EndsWith(".unity"))
            {
                outcome.FailureReason = $"Save path must end with .unity (got '{scenePath}').";
                return outcome;
            }
            if (!normalizedPath.StartsWith("Assets/"))
            {
                outcome.FailureReason = $"Save path must be under Assets/ (got '{scenePath}').";
                return outcome;
            }

            string outputFolder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/') ?? "Assets";
            string sceneName    = Path.GetFileNameWithoutExtension(normalizedPath);

            settings.outputFolder = outputFolder;
            settings.sceneName    = sceneName;

            bool saved = SaveLevelToScene(root, normalizedPath, lastResult.Log);
            if (!saved)
            {
                outcome.FailureReason = "Scene save failed (or user cancelled overwrite). Manifest not written.";
                return outcome;
            }

            // Record save success on the result before WriteManifest reads it.
            lastResult.ScenePath = normalizedPath;

            string manifestPath = $"{outputFolder}/{sceneName}_manifest.txt";
            EnsureAssetFolder(outputFolder);
            WriteManifest(settings, lastResult, placements ?? new List<PlacementRecord>(), manifestPath);

            lastResult.ManifestPath  = manifestPath;
            outcome.Success          = true;
            outcome.ScenePath        = normalizedPath;
            outcome.ManifestPath     = manifestPath;
            return outcome;
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
                category  = null,
                slotIndex = slotIdx,
            };
        }

        // ── Core placement: hall + room ───────────────────────────────────────

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
                        UnityEngine.Object.DestroyImmediate(hallGO);
                        continue;
                    }

                    var hallEntry = hallExits[rng.Next(hallExits.Count)];
                    SnapExitToExit(hallGO, hallEntry, priorExit);

                    if (Overlaps(hallPiece, placed))
                    {
                        UnityEngine.Object.DestroyImmediate(hallGO);
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
                        UnityEngine.Object.DestroyImmediate(roomGO);
                        UnityEngine.Object.DestroyImmediate(hallGO);
                        continue;
                    }

                    Vector3 offset = hallExit.transform.position - roomEntry.transform.position;
                    roomGO.transform.position += offset;
                    SnapToCardinalY(roomGO.transform);

                    var combined = new List<RoomPiece>(placed) { hallPiece };
                    if (Overlaps(roomPiece, combined))
                    {
                        UnityEngine.Object.DestroyImmediate(roomGO);
                        UnityEngine.Object.DestroyImmediate(hallGO);
                        continue;
                    }

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

        static Bounds ComputeBoundsUnion(GameObject root)
        {
            var pieces = root.GetComponentsInChildren<RoomPiece>();
            if (pieces.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = GetRotationAwareWorldBounds(pieces[0]);
            for (int i = 1; i < pieces.Length; i++)
                bounds.Encapsulate(GetRotationAwareWorldBounds(pieces[i]));
            return bounds;
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

        // ── Placement-record helpers ──────────────────────────────────────────

        static PlacementRecord MakeHallRecord(RoomPiece hall, string kind) => new PlacementRecord
        {
            kind              = kind,
            prefabName        = CleanPrefabName(hall.gameObject.name),
            roomCategory      = "-",
            worldPosition     = hall.transform.position,
            yRotation         = NormalizeRotY(hall.transform.eulerAngles.y),
            hallSlackOptional = 0f,
        };

        static PlacementRecord MakeRoomRecord(RoomPiece room, string kind, string category) => new PlacementRecord
        {
            kind              = kind,
            prefabName        = CleanPrefabName(room.gameObject.name),
            roomCategory      = category,
            worldPosition     = room.transform.position,
            yRotation         = NormalizeRotY(room.transform.eulerAngles.y),
            hallSlackOptional = null,
        };

        static string CleanPrefabName(string goName)
        {
            if (string.IsNullOrEmpty(goName)) return "(unnamed)";
            int idx = goName.IndexOf("(Clone)");
            return idx >= 0 ? goName.Substring(0, idx).Trim() : goName;
        }

        static float NormalizeRotY(float yEuler)
        {
            float y       = Mathf.Repeat(yEuler, 360f);
            float snapped = Mathf.Round(y / 90f) * 90f;
            return Mathf.Repeat(snapped, 360f);
        }

        // ── Scene save / folder ───────────────────────────────────────────────

        /// <summary>
        /// Moves <paramref name="root"/> from the active scene into a brand-new
        /// scene saved at <paramref name="scenePath"/>. The new scene contains
        /// Main Camera + Directional Light (default) + GeneratedLevel. Closes the
        /// new scene after save so the user's active scene is the only one open.
        /// Returns false on overwrite cancellation or save failure.
        ///
        /// Note: does NOT call <see cref="EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo"/>
        /// — the user clicked "Save Generated Level" with the explicit intent of
        /// saving the generated content. Prompting about the active scene at that
        /// point would interrupt them, and a "Don't Save" response would discard
        /// the active scene's modifications, including our root.
        /// </summary>
        static bool SaveLevelToScene(GameObject root, string scenePath, List<string> log)
        {
            if (root == null)
            {
                log.Add("SaveLevelToScene: root is null.");
                return false;
            }
            if (string.IsNullOrEmpty(scenePath) || !scenePath.EndsWith(".unity"))
            {
                log.Add($"SaveLevelToScene: invalid path '{scenePath}'.");
                return false;
            }

            string folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/') ?? "Assets";
            if (!string.IsNullOrEmpty(folder))
                EnsureAssetFolder(folder);

            if (File.Exists(scenePath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Scene already exists",
                    $"{scenePath}\n\nOverwrite this scene?",
                    "Overwrite", "Cancel");
                if (!ok)
                {
                    log.Add($"User cancelled overwrite of {scenePath}.");
                    return false;
                }
            }

            // Create new additive scene with default Main Camera + Directional Light
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);

            // Move root from active scene to the new scene (root must be parentless,
            // which it is — see Generate)
            EditorSceneManager.MoveGameObjectToScene(root, newScene);

            FrameCameraInScene(newScene, root);

            bool saved = EditorSceneManager.SaveScene(newScene, scenePath);

            if (saved) log.Add($"Scene saved: {scenePath}");
            else       log.Add($"SaveScene returned false for {scenePath}.");

            EditorSceneManager.CloseScene(newScene, removeScene: true);
            AssetDatabase.Refresh();

            return saved;
        }

        static void FrameCameraInScene(Scene scene, GameObject root)
        {
            Camera mainCam = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                var cam = go.GetComponent<Camera>();
                if (cam != null) { mainCam = cam; break; }
            }
            if (mainCam == null) return;

            var bounds = ComputeBoundsUnion(root);
            if (bounds.size == Vector3.zero) return;

            var center = bounds.center;
            float yOff = Mathf.Max(bounds.extents.y * 4f, 50f);
            float zOff = Mathf.Max(bounds.extents.z * 2f + 30f, 50f);
            mainCam.transform.position = center + new Vector3(0f, yOff, -zOff);
            mainCam.transform.LookAt(center);
        }

        public static void EnsureAssetFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath)) return;
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/') ?? "Assets";
            string leaf   = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrEmpty(leaf)) return;
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ── Manifest writer ───────────────────────────────────────────────────

        static void WriteManifest(LevelGenSettings settings,
                                  GenerationResult result,
                                  List<PlacementRecord> placements,
                                  string manifestPath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("V2 Level Generator — Manifest");
            sb.AppendLine("==============================");
            sb.AppendLine($"Generated:   {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
            sb.AppendLine($"Seed:        {result.Seed}");
            sb.AppendLine($"Elapsed:     {result.ElapsedSeconds:F2}s");
            sb.AppendLine();

            sb.AppendLine("Output");
            sb.AppendLine("------");
            sb.AppendLine($"sceneName:        {(string.IsNullOrEmpty(settings.sceneName) ? "(none)" : settings.sceneName)}");
            sb.AppendLine($"outputFolder:     {(string.IsNullOrEmpty(settings.outputFolder) ? "(none)" : settings.outputFolder)}");
            sb.AppendLine($"sceneSaved:       {result.ScenePath != null}");
            sb.AppendLine($"scenePath:        {result.ScenePath ?? "(not saved)"}");
            sb.AppendLine();

            sb.AppendLine("Source");
            sb.AppendLine("------");
            sb.AppendLine($"catalogue:        {(settings.catalogue != null ? settings.catalogue.name : "(none)")}");
            sb.AppendLine($"themeName:        {(string.IsNullOrEmpty(settings.themeName) ? "(none)" : settings.themeName)}");
            sb.AppendLine();

            sb.AppendLine("Room Budget");
            sb.AppendLine("-----------");
            sb.AppendLine($"Starter:   {settings.starterCount}");
            sb.AppendLine($"Boss:      {settings.bossCount}");
            sb.AppendLine($"Small:     {settings.smallCount}");
            sb.AppendLine($"Medium:    {settings.mediumCount}");
            sb.AppendLine($"Large:     {settings.largeCount}");
            sb.AppendLine($"Special:   {settings.specialCount}");
            sb.AppendLine($"Total:     {settings.TotalRoomCount}");
            sb.AppendLine();

            sb.AppendLine("Hall Budget");
            sb.AppendLine("-----------");
            sb.AppendLine($"Spine hall size:   {settings.spineHallSize}");
            sb.AppendLine($"Branch hall size:  {settings.branchHallSize}");
            sb.AppendLine();

            sb.AppendLine("Layout");
            sb.AppendLine("------");
            sb.AppendLine($"Style:               {settings.layoutStyle}");
            sb.AppendLine($"Branch slot count:   {settings.branchSlotCount}");
            sb.AppendLine($"Branching factor:    {settings.branchingFactor:F2}     (logged only — not used in V2)");
            sb.AppendLine($"Dead-end count:      {settings.deadEndCount}        (logged only — not used in V2)");
            sb.AppendLine($"Secret-room count:   {settings.secretRoomCount}        (logged only — not used in V2)");
            sb.AppendLine();

            sb.AppendLine("Results");
            sb.AppendLine("-------");
            sb.AppendLine($"Rooms placed:        {result.RoomsPlaced}");
            sb.AppendLine($"Halls placed:        {result.HallsPlaced}");
            sb.AppendLine($"Branches placed:     {result.BranchesPlaced} / {result.BranchesRequested}");
            sb.AppendLine($"Backtrack count:     {result.BacktrackCount}");
            sb.AppendLine();

            sb.AppendLine("Placements (in order)");
            sb.AppendLine("---------------------");
            sb.AppendLine("#    Kind             Category   Prefab                              Pos                       RotY  Slack");
            foreach (var p in placements)
            {
                string posStr   = $"({p.worldPosition.x,7:F2},{p.worldPosition.y,7:F2},{p.worldPosition.z,7:F2})";
                string rotStr   = ((int)p.yRotation).ToString().PadLeft(4);
                string slackStr = p.hallSlackOptional.HasValue
                    ? p.hallSlackOptional.Value.ToString("F1").PadLeft(4)
                    : "  - ";

                sb.AppendLine(
                    p.order.ToString().PadRight(5) +
                    Truncate(p.kind, 16).PadRight(17) +
                    Truncate(p.roomCategory, 9).PadRight(11) +
                    Truncate(p.prefabName, 35).PadRight(36) +
                    posStr.PadRight(26) +
                    rotStr + "  " +
                    slackStr);
            }

            File.WriteAllText(manifestPath, sb.ToString());
            AssetDatabase.Refresh();
        }

        static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));

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

        static void DestroyRoot(GameObject root)
        {
            if (root == null) return;
            if (Selection.activeGameObject == root)
                Selection.activeGameObject = null;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif
