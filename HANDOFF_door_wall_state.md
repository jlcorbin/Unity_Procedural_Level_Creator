# Door/Wall Placement — State Handoff

**Context for:** new Claude chat, tomorrow
**Project:** Unity LevelGen RoomWorkshopWindow.cs
**Upload with this file:** the current `RoomWorkshopWindow.cs` and `CLAUDE.md`

---

## Current symptom

Door and wall placement **works partly** but something is broken even after rolling back today's changes. The file has been restored to the April 14 known-good baseline (confirmed — zero April 19 artifacts present), yet behavior is not fully correct. Something else changed that is not part of the April 19 cascade.

## What is known-good (April 14 baseline — currently in the file)

This was explicitly confirmed working three times:
- **April 13, 03:02** — *"great this is progress"* after the small-preset fix landed
- **April 13, 04:17** — *"everything up to this point is working like I want"*
- **April 14, 02:30** — *"great. this works. update CLAUDE.md"*

CLAUDE.md at that point listed (and still lists) under verified working:
```
- Door replacement: random wall slot, random doorway prefab, isExit toggle ✓
- ExitPoint stamping (isExit=true) and decorative door (isExit=false) ✓
- Half-wall logic for angle/concave/convex corners (not _2 variants) ✓
- _2 suffix corners always use full-length adjacent walls ✓
- Small (12×12) room door fallback (ignores first/last exclusion) ✓
- Double Door — all four logic paths (Medium Standard, Medium Special, Large, Large>60) ✓
- Per-tier Opening Tier dropdown — shown only when tiers > 1 ✓
```

## Verified markers in the current file

| Feature | Location | State |
|---|---|---|
| Small-preset single-wall branch (4 walls) | Lines 1025, 1043, 1061, 1079 | ✓ intact |
| North rotation = `Quaternion.identity` | Lines 1026, 1029 | ✓ original (pre-flip) |
| South rotation = `Euler(0, 180, 0)` | Lines 1044, 1047 | ✓ original |
| East rotation = `Euler(0, 90, 0)` | Line 1061 area | ✓ original |
| West rotation = `Euler(0, -90, 0)` | Line 1079 area | ✓ original |
| `startN = -HalfWidth + FloorStep * 1.5f` | Line 1001 | ✓ original |
| `startS = -HalfWidth + FloorStep * 0.5f` | Line 1002 | ✓ original |
| `startE = -HalfDepth + FloorStep * 0.5f` | Line 1003 | ✓ original |
| `startW = -HalfDepth + FloorStep * 1.5f` | Line 1004 | ✓ original |
| `DoAddDoor` fallback to all walls when candidates empty | Lines 1430-1431 | ✓ present |
| `GetHalfWallPrefabs(WallSize)` — single overload, no `HalfSide` enum | throughout | ✓ pre-L |
| `flipStart/End` + `shiftStart/End` pattern for `_R + flip + shift` | Line 1129, 1132 | ✓ original |

## What the April 14 logic is supposed to do

### BuildWalls — four walls, same shape

```csharp
// North: N_0=NW no flip/no shift; N_last=NE flip+shift toward centre
var p = GetWallEntriesForSide(_wallNEnabled, _wallNDirIdx);
if (p.Count > 0)
{
    var half = GetHalfWallPrefabs(InferWallSize(p));
    if (nwHalf && neHalf && perSideX == 2)
        // Small-preset + both corners angle/concave/convex: one full wall centered
        PlaceInGroup(wallsGrp, p,
            new Vector3(startN + FloorStep * 0.5f, yOff, northZ),
            Quaternion.identity, TierName("Wall_N_0", tier));
    else
        PlaceWallRun(wallsGrp, p, perSideX, startN, northZ, true,
            Quaternion.identity, "N",
            startOverride:   nwHalf ? half : null,
            endOverride:     neHalf ? half : null,
            flipEndRotation: neHalf,
            shiftEndToward:  neHalf,
            yOffset: yOff, tier: tier);
}
```

Each of the four walls follows this pattern with per-side rotation and per-side `halfXY` booleans. The `perSideX/Z == 2 && both-corners-half` branch only fires on Small preset.

### DoAddDoor — Small-preset fallback

```csharp
int minIdx = allSideWalls[0].idx;
int maxIdx = allSideWalls[^1].idx;
var candidates = allSideWalls.FindAll(w => w.idx != minIdx && w.idx != maxIdx);
if (candidates.Count == 0)
    candidates = new List<(Transform t, int idx)>(allSideWalls);
```

On Small (12×12) with all-half corners: BuildWalls produces one wall per side, `candidates.Count == 0` after the first/last exclusion, fallback kicks in and uses the one wall. Exit gets stamped at its pivot.

### Half-wall slot rules (for angle/concave/convex corners, non-_2)

Eight slots, four need `_R + flip + shift`, four do not:

| Slot | Flip | Shift |
|---|---|---|
| N_0  (NW) | no | no |
| N_last (NE) | flip | shift |
| S_0  (SW) | flip | shift |
| S_last (SE) | no | no |
| E_0  (SE) | flip | shift |
| E_last (NE) | no | no |
| W_0  (SW) | no | no |
| W_last (NW) | flip | shift |

Rule: `_R` geometry is always on local `+X` of pivot. After wall rotation:
- Y=0° (N): +X = East
- Y=90° (E): +X = South
- Y=180° (S): +X = West
- Y=-90° (W): +X = North

The slot needs flip+shift if its +X points INTO the corner (would overlap).

## What could be causing "partly working" now

Since the current file matches April 14 exactly, the breakage is likely in one of these places *outside* `RoomWorkshopWindow.cs`:

### 1. Catalogue / prefab changes
- A wall or corner prefab may have been reimported with a different pivot or rotation
- A new half-wall variant (like `_L`) may have been added to the catalogue and `GetHalfWallPrefabs` is now picking it up instead of `_R`
- Check: `GetHalfWallPrefabs` returns entries whose name contains `"straight"` + size + `"half"` — if both `_L` and `_R` exist, it returns both and random selection may pick `_L` which places geometry on the wrong side

### 2. Corner prefab classification
- A corner prefab's name may have changed (e.g. lost or gained `_2` suffix)
- `CornerNeedsHalfWall` depends on exact name matching: ends `_2` → false; contains `angle|concave|convex` → true
- If a corner is now misclassified, `nwHalf/neHalf/swHalf/seHalf` flags flip and wall selection takes the wrong branch

### 3. PieceCatalogue.cs changes
- Fields added/removed (e.g. `isExit`) that affect filtering
- `PieceType.None` staging slot — pieces pending categorization don't get picked up
- `GetEntriesByType` behavior change

### 4. CLAUDE.md vs. file drift
- CLAUDE.md may list features as ✓ that no longer match the code behavior
- Check: the "Half-wall _R suffix rule" section in CLAUDE.md — if new prefabs don't follow that rule the logic breaks

### 5. PlaceWallRun or PlaceInGroup changes
- If a helper method signature or default-parameter behavior shifted, the per-slot override logic may misfire

## What to do in the new chat tomorrow

Ask the new Claude to:

1. **Read CLAUDE.md fully first**
2. **Read the uploaded RoomWorkshopWindow.cs fully**
3. **Identify the exact symptom** — get the user to describe what is wrong (which preset, which corner types, which walls, which coordinates are wrong)
4. **Check these specific call sites** before proposing any code changes:
   - `GetHalfWallPrefabs(WallSize)` — what does it return for each size in the current catalogue? Are there `_L` variants now?
   - `CornerNeedsHalfWall(int dirIdx)` — what does it return for each corner in the current room?
   - `InferWallSize(List<PieceCatalogue.PieceEntry>)` — is it returning what the code expects?
5. **Do not reintroduce any of the April 19 changes** listed below unless the user explicitly asks for them.

## Do NOT reintroduce (April 19 cascade)

None of these exist in the current file and none should be added:
- `HalfSide` enum or `GetHalfWallPrefabs(WallSize, HalfSide)` overload
- `HasHalfL`, `useLEnd`, `halfL` / `halfR` variable splits
- 180° rotation flip on walls (do NOT change N from `identity` to `Euler(0,180,0)` etc.)
- `startN/S/E/W` multiplier swap (keep `1.5/0.5/0.5/1.5`)
- `DoorCenterFromWallPivot` helper
- `DoAddDoor_Small_SingleWall`, `DoAddDoor_Small_TwoWall`, `DoSingleDoorSmall`, `DoSingleDoorSmallCorner2`
- `BothAdjacentCornersAre2Variant`, `CornerIs2Variant` helpers

If any of these are present in the uploaded file, something is wrong — the current file should be clean of them.

## The April 19 story (for context only)

On April 19 a series of ~15 patches attempted to flip wall `+Z` to face into the room for future prop placement. Each patch introduced a new regression that the next patch tried to fix. The cascade eventually broke door placement entirely, including the Small-preset path that had been confirmed working on April 14.

The final state at the end of April 20 had deleted the original small-preset fix (`nwHalf && neHalf && perSideX == 2`) and replaced it with `perSideX == 2` alone, plus a new `DoAddDoor_Small_SingleWall` helper.

**This entire cascade was rolled back.** The current file is the April 14 pre-cascade version.

## Known-good assertion

**The file currently in the project is byte-level equivalent to the April 14 known-good state for the door/wall placement code paths.** If behavior is wrong now but was correct then, the cause is external to `RoomWorkshopWindow.cs` — look at prefabs, catalogue contents, or corner/wall name conventions first.

---

*Generated from session log analysis of four Claude Code .jsonl files spanning April 10 – April 20, 2026.*

## V2 Level Generator note

Phase A complete (2026-04-25). `LevelGenSettings` data class and
`V2LevelGeneratorWindow` EditorWindow in place at
`Assets/Scripts/LevelGen/V2/`. Generate click logs settings and stops;
placement logic is Phase B.
