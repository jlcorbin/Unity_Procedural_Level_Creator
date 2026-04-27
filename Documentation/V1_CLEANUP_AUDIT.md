# V1 Cleanup Audit

Generated: 2026-04-25
Branch: v2
HEAD: a929e1d

---

## Summary

- **Files to DELETE:** 36 entries under `Assets/Scripts/_Archive/` (17 `.bak` source files + their 17 `.meta` siblings + 4 retained subfolder `.meta` files would also need cleanup if the folder is removed wholesale).
- **Files/methods to MOVE to Experimental:** 2 methods in `ShapeStamp.cs` (`Diamond`, `Circle`), plus 2 corresponding lines in `ShapeStamp_Test.cs`.
- **Methods/types/comments to DELETE within KEPT files:** 2 stale doc-comment references to "RoomPreset" inside `Workshop/PieceCatalogue.cs`.
- **Ambiguous items needing review:** 3 (two existing scene files + the strategic question of whether to keep `_Archive/` as an on-disk graveyard or rely on git history).

**Key finding:** the live (non-_Archive) code tree is already clean of V1. Every V1 sentinel string searched (`HalfSide`, `DoAddDoor_Small`, `DoorCenterFromWallPivot`, `BothAdjacentCornersAre2Variant`, `CornerIs2Variant`, `PlaceWallRun`, `GetHalfWallPrefabs`, `CornerNeedsHalfWall`, `RoomWorkshopWindow`, plus all V1 type names: `PropCatalogue`, `PropEntry`, `RoomDefinition`, `RoomContentGenerator`, `RoomPresetLibrary`, `SpawnPoint`, `LevelSequence`, `SeedData`, `BoundsChecker`, `LevelGenSetup`, `PlaceholderPrefabFactory`, `LevelGeneratorEditor`) returns **zero matches** outside `_Archive/`. The cleanup work is therefore concentrated in `_Archive/` rather than the live tree.

---

## Section A — Files to DELETE entirely

All entries below live in `Assets/Scripts/_Archive/`. They use a `.bak` extension and are therefore not compiled by Unity (only `.cs` files compile). They describe the V1 architecture: room workshop window with per-side branching, V1 LevelGenerator, V1 RoomBuilder, V1 prop/spawn/preset systems. Every type, method, or symbol in these files has **zero references** in the live `Assets/Scripts/` tree (verified — see Section E).

| Path | Reason | V2 references |
|------|--------|--------------|
| `Assets/Scripts/_Archive/Data/LevelSequence.cs.bak` | V1 ScriptableObject for SeedData records, replaced by V2 manifest text | 0 (live) |
| `Assets/Scripts/_Archive/Data/SeedData.cs.bak` | V1 seed metadata wrapper, replaced by `LevelGenSettings.seed` | 0 (live) |
| `Assets/Scripts/_Archive/Editor/LevelGenSetup.cs.bak` | V1 scene-setup wizard, replaced by V2 generator's inline scene save flow | 0 (live) |
| `Assets/Scripts/_Archive/Editor/LevelGeneratorEditor.cs.bak` | V1 inspector for V1 `LevelGenerator` MonoBehaviour, no V2 equivalent needed | 0 (live) |
| `Assets/Scripts/_Archive/Editor/PlaceholderPrefabFactory.cs.bak` | V1 placeholder prefab generator, V2 uses real prefabs from `Assets/Prefabs/Rooms/{Category}/` | 0 (live) |
| `Assets/Scripts/_Archive/Editor/RoomWorkshopWindow.cs.bak` | The main V1 workshop window — entire per-side wall/door logic, `HalfSide` enum, `DoAddDoor_Small`, etc. Superseded by V2 `RoomBuilder` + `EdgeSolver` + `CellMap.HasWallOnEdge` | 0 (live) |
| `Assets/Scripts/_Archive/Generation/BoundsChecker.cs.bak` | V1 AABB collision helper, replaced by V2 `GetRotationAwareWorldBounds` + `Bounds.Intersects` inline in `V2LevelGenerator.cs` | 0 (live) |
| `Assets/Scripts/_Archive/Generation/LevelGenerator.cs.bak` | V1 runtime generator MonoBehaviour, replaced by V2 static `V2LevelGenerator.Generate` | 0 (live) |
| `Assets/Scripts/_Archive/Workshop/PropCatalogue.cs.bak` | V1 prop ScriptableObject, deferred (Dress step not yet implemented in V2) | 0 (live) |
| `Assets/Scripts/_Archive/Workshop/PropEntry.cs.bak` | V1 prop entry record, same as above | 0 (live) |
| `Assets/Scripts/_Archive/Workshop/RoomBuilder.cs.bak` | V1 RoomBuilder (different from V2 `Assets/Scripts/LevelEditor/RoomBuilder.cs`) — V1 version was COMP_-based, V2 is cell-map based | 0 (live; the V2 RoomBuilder lives at a different path) |
| `Assets/Scripts/_Archive/Workshop/RoomContentGenerator.cs.bak` | V1 prop placement, deferred | 0 (live) |
| `Assets/Scripts/_Archive/Workshop/RoomDefinition.cs.bak` | V1 room metadata wrapper, replaced by V2 `RoomPiece` + `RoomPieceClassification` enums | 0 (live) |
| `Assets/Scripts/_Archive/Workshop/RoomPreset.cs.bak` | V1 preset, deferred | 0 (live; only stale doc-comments in `PieceCatalogue.cs` mention it — see Section C) |
| `Assets/Scripts/_Archive/Workshop/RoomPresetLibrary.cs.bak` | V1 preset library, deferred | 0 (live) |
| `Assets/Scripts/_Archive/Workshop/SpawnPoint.cs.bak` | V1 spawn marker, deferred | 0 (live) |

Each file above has a sibling `.meta` file with the same name + `.meta` suffix that must be deleted alongside (Unity convention). The four subfolder `.meta` files (`_Archive/Data.meta`, `_Archive/Editor.meta`, `_Archive/Generation.meta`, `_Archive/Workshop.meta`) plus `_Archive.meta` itself become removable once all leaf files are gone.

**Search proving zero V2 references** (run as a single sweep — each term scoped to live code only, with `_Archive` excluded):

```
HalfSide                          → 0 files (live)
DoAddDoor_Small                   → 0 files (live)
DoorCenterFromWallPivot           → 0 files (live)
BothAdjacentCornersAre2Variant    → 0 files (live)
CornerIs2Variant                  → 0 files (live)
PlaceWallRun                      → 0 files (live)
GetHalfWallPrefabs                → 0 files (live)
CornerNeedsHalfWall               → 0 files (live)
RoomWorkshopWindow                → 0 files (live)
PropCatalogue                     → 0 files (live)
PropEntry                         → 0 files (live)
RoomDefinition                    → 0 files (live)
RoomContentGenerator              → 0 files (live)
RoomPresetLibrary                 → 0 files (live)
SpawnPoint                        → 0 files (live)
LevelSequence                     → 0 files (live)
SeedData                          → 0 files (live)
BoundsChecker                     → 0 files (live)
LevelGenSetup                     → 0 files (live)
PlaceholderPrefabFactory          → 0 files (live)
LevelGeneratorEditor              → 0 files (live)
```

---

## Section B — Files/methods to MOVE to Experimental

The live ShapeStamp.cs file contains three methods. `Rectangle` is V2-active (used by `EdgeSolver_Test.cs`, `RoomPiece_Test.cs`, `Doorway_Test.cs`, and `RoomBuilder.Build`). `Diamond` and `Circle` are dormant per CLAUDE.md (called out as "scaffolding for future work" — explicitly removed from EdgeSolver scope on 2026-04-21). The current ShapeStamp_Test still calls them in its diagnostic dump, so any move must update the test in lockstep.

### Item B-1 — `ShapeStamp.Diamond` and `ShapeStamp.Circle`

**Current path:** `Assets/Scripts/LevelEditor/ShapeStamp.cs:59-122` (Diamond) and `:125-167` (Circle)

**Proposed new path:** `Assets/Scripts/Experimental/ShapeStamp_Shapes.cs`

**Why dormant:** CLAUDE.md (Phase 1 foundation section) explicitly says: *"Diamond/Circle are scaffolding for future work."* And later, under Phase 2 EdgeSolver scope: *"Custom shapes (Diamond/Circle) and their Triangle/Angle/Quarter tile support (removed 2026-04-21 to focus on rectangle rooms; ShapeStamp still contains Diamond() and Circle() as scaffolding for future work)."* Production V2 code path uses Rectangle only.

**Recommended wrapper:** wrap the new file's contents in `#if EXPERIMENTAL_SHAPES` (or `#if FALSE` for total exclusion). Reasoning:
- `#if FALSE` is the safest — guarantees the methods don't compile, won't drift with API changes, and avoids accidental use. Reviving them means defining the symbol or stripping the guard.
- A `.asmdef` exclusion would be heavier-weight; the project has no `.asmdef` files today (verified — `find Assets -name "*.asmdef"` returns zero). Introducing one just for this is overkill.
- Keep the file in the project tree (not deleted) so the future revival has source to start from.

### Item B-2 — `ShapeStamp_Test.cs` Diamond/Circle calls

**Current path:** `Assets/Scripts/LevelEditor/Editor/ShapeStamp_Test.cs:18-23`

**Why touched:** The test calls `ShapeStamp.Diamond(5)` and `ShapeStamp.Circle(3)` in its diagnostic dump. After moving the methods behind `#if FALSE`, those calls will fail to compile.

**Recommended action:** strip the Diamond/Circle lines from the test (lines 18-19 + their two `Debug.Log` lines at 22-23). The test becomes Rectangle-only, matching the V2 active scope. No need to preserve them — the moved file at `Experimental/` carries enough authoring context for a future revival to write a fresh test.

---

## Section C — Methods/types/comments to DELETE within KEPT files

### Item C-1 — Stale doc-comment references to `RoomPreset` in `PieceCatalogue.cs`

**File:** `Assets/Scripts/Workshop/PieceCatalogue.cs`
**Lines:** 18 and 105
**Current text:**
- Line 18: `/// <summary>Visual style tag for this catalogue — used for RoomPreset matching.</summary>`
- Line 105: `[Tooltip("Visual style tag for this catalogue — used for matching with RoomPreset.")]`

**Reason V1-only:** `RoomPreset` was a V1 ScriptableObject (now in `_Archive/Workshop/RoomPreset.cs.bak`). It has no corresponding V2 type. The doc text describes a feature that no longer exists. Functionally harmless (these are just strings in attributes/comments) but misleading to readers.

**Recommended action:** rewrite both to a V2-accurate description, e.g. *"Visual style tag for this catalogue. Used to match against the EditorWindow's Theme dropdown when the Theme name on a `LevelGenSettings` resolves to one of `themes`."* or simpler: drop the cross-reference entirely.

**Confirm zero V2 references for `RoomPreset`:** `grep "RoomPreset" --include="*.cs"` outside `_Archive` returns **only these two doc-comment matches** in `PieceCatalogue.cs`. No actual code references.

---

## Section D — Ambiguous (Jason must decide)

### Item D-1 — `Assets/Scenes/RoomWorkshop.unity`

**Why classification is unclear:** CLAUDE.md states under "Pending work" that a `RoomWorkshop.unity` scene needs to be created. But the file already exists in `Assets/Scenes/`. It may be:
- An empty/stub scene Jason already started populating
- A V1-era leftover from when `RoomWorkshopWindow` was a real EditorWindow with a sample scene
- An auto-generated empty scene

**What would resolve it:** Jason opens the scene in Unity and confirms whether it contains content he wants to keep or it's an empty placeholder. If empty: KEEP, will be populated next session per CLAUDE.md. If V1-era content: DELETE.

### Item D-2 — `Assets/Scenes/LevelGenerator.unity`

**Why classification is unclear:** Same situation as D-1. CLAUDE.md says it needs to be created; the file already exists.

**What would resolve it:** same as D-1 — Jason inspects.

### Item D-3 — Strategic: keep `_Archive/` on disk vs. rely on git history

**Why classification is unclear:** Section A recommends deleting `_Archive/` wholesale. The argument for retention is *"on-disk grep-able graveyard for the V1 cascade"*. The argument for deletion: git already preserves every `.bak` in commit `a929e1d` and prior; an on-disk graveyard adds no recoverability that git doesn't already provide, and creates noise during searches and IDE indexing.

**What would resolve it:** Jason picks one of:
- **DELETE** `_Archive/` entirely (recommended; preserves nothing not already in git, reduces grep noise, signals V1 is truly retired).
- **KEEP** `_Archive/` as-is (no action; accept the grep-noise cost).
- **MOVE** `_Archive/` to `Documentation/V1_Reference/` outside the `Assets/` tree (preserves on-disk reference without polluting Asset paths). Note: would need to remove the `.bak.meta` files first — Unity will complain if they reference assets outside `Assets/`.

---

## Section E — Verification searches performed

All searches scoped to `Assets/Scripts/`, with `_Archive/` filtered out using `grep -v _Archive` where applicable. Directory layout confirmed via `find Assets/Scripts -name "*.cs" -not -path "*/_Archive/*"`.

| Term | Live matches | Where |
|------|-------------|-------|
| `HalfSide` | 0 | — |
| `DoAddDoor_Small` | 0 | — |
| `DoorCenterFromWallPivot` | 0 | — |
| `BothAdjacentCornersAre2Variant` | 0 | — |
| `CornerIs2Variant` | 0 | — |
| `GetHalfWallPrefabs` | 0 | — |
| `CornerNeedsHalfWall` | 0 | — |
| `PlaceWallRun` | 0 | — |
| `RoomWorkshopWindow` | 0 | — |
| `PropCatalogue` | 0 | — (only `Workshop/PieceCatalogue.cs` uses the unrelated name `PieceCatalogue`) |
| `PropEntry` | 0 | — |
| `RoomDefinition` | 0 | — |
| `RoomContentGenerator` | 0 | — |
| `RoomPresetLibrary` | 0 | — |
| `SpawnPoint` | 0 | — |
| `LevelSequence` | 0 | — |
| `SeedData` | 0 | — |
| `BoundsChecker` | 0 | — |
| `LevelGenSetup` | 0 | — |
| `PlaceholderPrefabFactory` | 0 | — |
| `LevelGeneratorEditor` | 0 | — |
| `RoomPreset` | 2 | `PieceCatalogue.cs:18`, `PieceCatalogue.cs:105` (doc comments only — Section C-1) |
| `HasWallOnEdge` (V2 marker — present check) | 2 | `LevelEditor/CellMap.cs`, `LevelEditor/EdgeSolver.cs` (V2 active) |
| `CellMap` | many | V2 active across `EdgeSolver`, `RoomBuilder`, tests |
| `RoomBuilder` (live, no `_Archive`) | 9 | All V2 — `Editor/PieceCatalogueEditor.cs`, `Generation/RoomPiece.cs`, `LevelEditor/RoomBuilder.cs`, `LevelEditor/Editor/*Test*.cs`, `LevelEditor/Editor/V2_SampleThemeBuilder.cs`, `Editor/RoomBuilderEditor.cs`, `Workshop/PieceCatalogue.cs` |
| `RoomPiece` | many | V2 active across the generator and tests |
| `ExitPoint` | many | V2 active |
| `EdgeSolverGizmoPreview` | 2 | `LevelEditor/EdgeSolverGizmoPreview.cs`, `LevelEditor/Editor/EdgeSolver_Test.cs` (V2 diagnostic — JC confirmed KEEP per `MENU_AUDIT.md`) |
| `ShapeStamp.Diamond` and `ShapeStamp.Circle` | 1 + 1 | `LevelEditor/Editor/ShapeStamp_Test.cs` (the test calls — Section B-2) |
| `LVL_Configurator` | 1 | `Editor/LVL_Configurator.cs` (KEEP — CLAUDE.md says "complete, do not touch") |
| `PieceCatalogue` | many | V2 shared (referenced by `LevelGenSettings`, `PieceCatalogueEditor`, `V2_SampleThemeBuilder`) |
| Live `.cs` file count | 24 | All listed at top of audit |
| `_Archive/*.bak` file count | 17 | All listed in Section A |
| `.asmdef` files in project | 0 | Confirmed via `find Assets -name "*.asmdef"` |

---

## Recommended deletion-prompt scope (Jason's reference)

When the follow-up deletion prompt runs, the scope is:
1. Apply Section A — remove `_Archive/` wholesale (after Jason resolves D-3).
2. Apply Section B — extract Diamond/Circle to `Experimental/` under `#if FALSE`, prune the test calls.
3. Apply Section C — rewrite the two stale doc comments in `PieceCatalogue.cs`.
4. Apply Section D-1 / D-2 only if Jason confirms the scene files are V1-era leftovers.
5. Update `CLAUDE.md` to reflect the cleanup (this audit doesn't touch it; the deletion prompt should).
6. Update `MENU_AUDIT.md` if any menu items disappear (none expected — `_Archive/` files don't expose menus).

No production V2 code paths need changes for this cleanup. The live tree is already clean of V1 logic.
