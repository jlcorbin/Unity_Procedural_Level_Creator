# V1 Generator Audit — for V2 Generator Design

Date: 2026-04-25
Scope: V1 generator pipeline (LVL_Configurator, ExitPoint, RoomPiece,
RoomPieceClassification, PieceCatalogue, RoomBuilder)

---

## CRITICAL FINDING

**V1 has no placement engine.** `LevelGenerator.cs`, `BoundsChecker.cs`,
`SeedData.cs`, and `LevelSequence.cs` — all described as complete (✓) in
`CLAUDE.md` — do not exist anywhere under `Assets/Scripts/`. The Workshop
scripts listed in CLAUDE.md (`RoomDefinition.cs`, `PropEntry.cs`,
`PropCatalogue.cs`, `SpawnPoint.cs`, `RoomContentGenerator.cs`,
`RoomPreset.cs`, `RoomPresetLibrary.cs`) are also absent.

What exists is a **data model and two authoring tools**, but zero
graph-building or placement code. The V2 plan should read:
**build the engine from scratch**, using V1's data model as the foundation.

---

## Files in scope

| File | Role |
|------|------|
| `Assets/Scripts/Generation/ExitPoint.cs` | Connection socket MonoBehaviour — position + direction + state flags |
| `Assets/Scripts/Generation/RoomPiece.cs` | Room/hall metadata MonoBehaviour — bounds, exit list, depth, category |
| `Assets/Scripts/Editor/LVL_Configurator.cs` | EditorWindow: stamps RoomPiece + ExitPoints onto FDP LVL_ prefabs |
| `Assets/Scripts/Workshop/PieceCatalogue.cs` | ScriptableObject: lists all modular prefabs by PieceType |
| `Assets/Scripts/LevelEditor/RoomPieceClassification.cs` | Namespace-level enums: PieceType (Room/Hall), RoomCategory, HallCategory |
| `Assets/Scripts/LevelEditor/RoomBuilder.cs` | Cell-map room authoring tool — produces MOD_Room with RoomPiece + ExitPoints |

**Absent files (described in CLAUDE.md but not present):**
`LevelGenerator.cs`, `BoundsChecker.cs`, `SeedData.cs`, `LevelSequence.cs`,
`LevelGeneratorEditor.cs`, `RoomWorkshopWindow.cs`, `PlaceholderPrefabFactory.cs`,
`LevelGenSetup.cs`, and all Workshop/ scripts except `PieceCatalogue.cs`.

---

## A. Inputs

### ExitPoint
- No public inputs. Stateful MonoBehaviour populated by `LVL_Configurator` or
  `RoomBuilder` at authoring time.
- Inspector: `exitDirection` (Direction enum: North/South/East/West/Up/Down).
- Runtime write fields: `isConnected` (bool), `isSealed` (bool),
  `connectedPiece` (RoomPiece ref) — all `[HideInInspector]`, intended for
  a generator to set.

### RoomPiece
- Inspector: `pieceType` (StartRoom/Room/Hall/DeadEnd), `boundsSize` (Vector3),
  `boundsOffset` (Vector3).
- Runtime write fields: `isPlaced` (bool), `generationDepth` (int),
  `categoryName` (string) — all `[HideInInspector]`.
- No seed, no catalogue reference, no generation parameters.

### LVL_Configurator
- Single-prefab mode: drag a LVL_ prefab into the ObjectField, press Configure.
- Batch mode: folder-picker dialog; processes all prefabs in the chosen folder.
- No seed, no theme, no placement parameters.

### PieceCatalogue
- `List<PieceEntry> pieces` — each entry: (GameObject prefab, PieceType,
  string subFolder, bool isExit).
- `List<Theme> themes` — named bundles pairing one prefab per PieceType.
- `VisualTheme theme` — enum (Dungeon only currently).
- No generator parameters. Pure asset registry.

### RoomBuilder
- Shape: `rectangleWidth`, `rectangleDepth` (cells).
- Prefabs: `floorPrefab`, `wallPrefab`, `cornerPrefab`, `halfWallLPrefab`,
  `halfWallRPrefab`.
- `pieceType` (PieceType.Room or PieceType.Hall) — routes the save folder.
- `categoryName` (serialized string) — e.g. "Starter", "Small".
- No seed, no catalogue reference for room authoring.

---

## B. Outputs

### ExitPoint
- Pure component data. No scene output of its own.
- Gizmo: sphere at socket position + colored ray for direction.

### RoomPiece
- Pure component data. No scene output of its own.
- Gizmo: semi-transparent wire cube at bounds position.

### LVL_Configurator
- Writes prefab assets to `Assets/Prefabs/Rooms/` (Room) or
  `Assets/Prefabs/Halls/` (Hall) with `_LG` suffix.
- Stamps: `RoomPiece` component on prefab root + `Exit_{dir}` child
  GameObjects with `ExitPoint` components.
- Does NOT touch the active scene.

### PieceCatalogue
- ScriptableObject asset. No scene output.

### RoomBuilder
- Builds geometry into the active scene under a `MOD_Room` child.
- `PopulateRoomPiece(map)` stamps `RoomPiece` on `MOD_Room` and creates
  `V2_ExitPoint_{edge}_{x}_{z}` children.
- Save methods write the `MOD_Room` subtree as a prefab asset to
  `Assets/Prefabs/Rooms/` or `Assets/Prefabs/Halls/` routed by category.

---

## C. Algorithm

**No algorithm exists.** There is no graph builder, no placement loop,
no connection math, and no scene assembly code anywhere in the non-Archive
codebase.

The data model is clearly designed for such an algorithm. `ExitPoint` carries
`isConnected`, `isSealed`, and `connectedPiece` fields — exactly what a
BFS/DFS generator queue would need. `RoomPiece.GetOpenExits()` returns
unconnected exits for iteration. `GetWorldBounds()` returns an AABB for
overlap detection. `generationDepth` exists for difficulty scaling. But
none of this machinery is wired together.

---

## D. Piece selection logic

**None implemented.** `PieceCatalogue.CountOfType()` and `GetTheme()` /
`GetThemeNames()` exist, and `RoomBuilder.V2_SampleThemeBuilder.cs` has
a test that calls `ResolvePrefab()` per PieceType — but this is room
authoring selection, not generator piece selection. There is no code that
picks a room or hall prefab from a category folder to place next.

`RoomPiece.PieceType` has a `StartRoom` value and a `DeadEnd` value — both
intended for generator weighting — but neither is ever assigned by either
authoring tool (LVL_Configurator assigns Room/Hall only; RoomBuilder
assigns Room/Hall only).

---

## E. ExitPoint / RoomPiece usage

### What exists

`RoomPiece.RefreshExits()` uses `GetComponentsInChildren<ExitPoint>()` —
name-agnostic, finds all ExitPoint components regardless of naming.

`RoomPiece.GetOpenExits()` filters for `!isConnected && !isSealed`.

`RoomPiece.GetExitFacing(Direction)` returns the first unconnected exit
matching a given direction.

`RoomPiece.GetWorldBounds()` returns `new Bounds(transform.position +
boundsOffset, boundsSize * 2f)`.

### V2 conventions currently in place

- `boundsOffset = (0, height/2, 0)` — center is above the floor pivot.
  Consistent with floor-anchored placement.
- `boundsSize = (halfWidth, halfHeight, halfDepth)` — half-extents, so
  `boundsSize * 2f` = full AABB.
- LVL_Configurator ExitPoints: `Exit_{dir}`, Y=0 for horizontal,
  Y=6 for Up, Y=0 for Down.
- RoomBuilder ExitPoints: `V2_ExitPoint_{edge}_{x}_{z}`, Y = tier *
  TierHeight for horizontal.

For single-tier rooms, both tools produce Y=0 exits — compatible. A
generator using `GetWorldBounds()` and `GetOpenExits()` would work against
both prefab sources without modification.

### Key caveat

`RoomPiece.RefreshExits()` is called from `Awake()`. In edit-time
generation (no Play mode), `Awake()` never fires. A generator that places
prefabs at edit time must call `piece.RefreshExits()` explicitly after
instantiation before reading `piece.exits`.

---

## F. Coordinate system

### LVL_Configurator rooms
Prefab pivot is at the piece center (LVL_ `_M_` PivotMiddle convention).
`boundsOffset = (0, 3, 0)` — center 3 units above pivot. Exit local positions
are at half-extent on each axis, Y=0. A generator placing these pieces must
offset the new piece's pivot so its entry exit matches the source piece's
exit in world space.

Connection math for two `_M_` large rooms (NS example):
```
Room1 pivot at (0,0,0).  North exit at (0,0,+3).
Room2 South exit must land at (0,0,+3).
Room2 South exit local pos = (0,0,-3).
Room2 pivot = Room1_north_exit_world - Room2_south_exit_local
            = (0,0,+3) - (0,0,-3) = (0,0,+6).
```
This is the standard exit-socket snap formula.

### RoomBuilder rooms
Prefab pivot is the `MOD_Room` world origin. `boundsOffset = (0,
height/2, 0)`. Exit positions are at the cell-edge midpoints, Y=0 for
ground-floor doorways. Same socket-snap formula applies.

### Rotation
When exits face opposite directions (e.g. Room1 North exit → Room2 South
exit), no rotation is needed — the pieces snap in a straight line. For a
corner connection (e.g. Room1 East → Room2 North), the incoming piece must
be rotated 90° around Y so its entry exit faces the source exit. The
generator is responsible for computing this rotation.

---

## G. Difficulty / depth / branching

### What the data model supports

`RoomPiece.generationDepth` (int, [HideInInspector]) exists for depth
tracking, but is never set or read by any current code.

`ExitPoint.isSealed` (bool) and `isConnected` (bool) exist for dead-end
and connection state.

`RoomPiece.PieceType.DeadEnd` exists as an enum value.

`RoomPiece.PieceType.StartRoom` exists as an enum value.

### What is NOT implemented

No branching factor control. No dead-end cap placement. No boss room
logic. No secret room conditions. No linear vs grid layout parameter.
All of these are V2-new requirements.

---

## H. Reproducibility

No seed input exists anywhere. `System.Random` is mentioned in CLAUDE.md
as the intended RNG, but no usage exists in the current generator-related
files. (PieceCatalogue's removed `GetRandom()` took a `System.Random rng`
parameter — that was the intended seeded path, but it was deleted as unused.)

---

## I. Theme / catalogue integration

`PieceCatalogue` has a fully-designed `Theme` system (named bundles,
`GetTheme(string)`, `GetThemeNames()`). RoomBuilder's V2_SampleThemeBuilder
tests the theme resolution path for room authoring.

For the generator, no integration exists. No code selects room or hall
prefabs from a catalogue or theme for placement. This is a V2-new requirement.

---

## J. Known gaps and TODOs

Zero TODO/FIXME/HACK/XXX markers found across all in-scope files.

The CLAUDE.md backlog captures the missing pieces:
- `LevelGenerator.cs` — never written
- `BoundsChecker.cs` — never written
- `LevelSequence.cs` / `SeedData.cs` — never written
- Dress step (PropCatalogue / SpawnPoints) — not implemented
- `LevelGenerator.unity` scene — not created

---

## V2 Compatibility — Bridge Needed

1. **No engine to bridge to.** The plan "V2 input feeds V1 engine" is not
   viable. Build the placement engine from scratch. The data model
   (ExitPoint + RoomPiece) is the only V1 contribution.

2. **Edit-mode instantiation: `RefreshExits()` must be called manually.**
   `RoomPiece.Awake()` populates `exits` but Awake never fires in edit mode.
   The generator must call `piece.RefreshExits()` after `Instantiate()` /
   `PrefabUtility.InstantiatePrefab()`.

3. **ExitPoint naming divergence: safe to ignore.**
   LVL_Configurator: `Exit_{dir}`. RoomBuilder: `V2_ExitPoint_{edge}_{x}_{z}`.
   `GetComponentsInChildren<ExitPoint>()` finds both — no name dependency.
   No bridge needed if the generator uses the component API.

4. **`RoomPiece.PieceType.StartRoom` and `.DeadEnd` are never assigned.**
   Both authoring tools only assign Room or Hall. The generator must assign
   StartRoom to the seed room and DeadEnd to sealed dead-end pieces itself.

5. **`RoomPiece.generationDepth` is never set.**
   Must be assigned by the generator after placement if depth-based
   difficulty scaling is desired.

6. **No seed RNG in the data model.**
   A generator seed must be added as a parameter. Using `System.Random(seed)`
   throughout is the established project convention.

7. **`PieceCatalogue.GetRandom()` was deleted (dead code at time of deletion).**
   The generator will need a method to randomly select a prefab by category
   from the prefabs on disk. Options: add a new method to PieceCatalogue,
   or scan `Assets/Prefabs/Rooms/{category}/` directly via AssetDatabase.

8. **Piece source mismatch: LVL_ rooms vs RoomBuilder rooms.**
   LVL_Configurator rooms are saved to `Assets/Prefabs/Rooms/` flat.
   RoomBuilder rooms are saved to `Assets/Prefabs/Rooms/{Category}/`
   sub-folders. The generator's prefab loader must handle both layouts,
   or a decision must be made to standardize on one source.

9. **boundsOffset for LVL_ rooms: Y=3 (hardcoded half-wall-height).**
   For RoomBuilder rooms: Y = height/2 (correct for any tier count).
   AABB overlap check via `GetWorldBounds()` is correct for both if the
   generator places room pivots at floor level (Y=0). If Y placement ever
   differs, the offset must be accounted for.

---

## Open questions for JC

**Q1.** Was LevelGenerator.cs ever written and then deleted, or was it
always planned but never started? This affects whether any placement
logic lives in git history worth reviewing.

**Q2.** Target execution model: edit-time bake (generator runs in editor,
saves result as a `.unity` scene) or runtime generation (runs on device,
builds level into the active scene at startup)?

**Q3.** Prefab source for the generator: LVL_-derived prefabs only, or
RoomBuilder-derived prefabs only, or both? The sub-folder layout differs
and the generator's loader needs a consistent convention.

**Q4.** Connection math with rotation: when two exits require a rotated
join (e.g. North exit → North exit, 180° rotation needed), does the
generator rotate the incoming prefab, or is the exit convention designed
so all connections are straight-line only?

**Q5.** Dead-end handling: when the generator seals an exit (no placement
fits), should it place a cap prefab (wall/door covering the opening) or
leave it open? This drives whether `PieceType.DeadEnd` prefabs are needed
in the folders.
