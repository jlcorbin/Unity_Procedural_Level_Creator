# LevelGen — Claude Code Project Brief

## What this is
A Unity 6.4 mobile game project using URP and pure C#.
No Blueprints, no visual scripting.
Renderer: URP (Universal Render Pipeline).
Target platforms: Android (IL2CPP, ARM64) and iOS.

## Level generator overview
The generator places 3D room and hall prefabs connected via exit sockets.

Key rules:
- Each prefab has a RoomPiece component and child ExitPoint components
- ExitPoints have a direction (North/South/East/West/Up/Down)
- Two exits connect only if their directions are opposite
- Before placing a piece, BoundsChecker verifies no overlap (AABB, no physics)
- Generation uses seeded System.Random — same seed = identical level

Generation behaviour (LevelGenerator.cs):
- No separate start room field. The generator picks a random prefab from
  roomPrefabs, places it at origin, and counts it as room #1.
- roomCount (int, Min=1, default=5) — total rooms to place including room #1.
- maxHalls = roomCount — halls can never exceed roomCount.
- At each open exit: if both types available, 70 % chance room / 30 % hall.
  If one type is exhausted, the other is used exclusively.
- If no placement succeeds after maxRetriesPerExit attempts, the exit is sealed
  and generation continues from the next queued exit.
- When roomCount is reached the loop stops. Remaining open exits stay open —
  no dead-end caps are placed.
- Log on completion: [LevelGenerator] Done. seed=X rooms=Y/Z halls=W/Z
- LevelSequence ScriptableObject still exists for storing SeedData records
  but is not auto-loaded by LevelGenerator at runtime.

## Whitebox pack

A pack-agnostic mirror of the Fantastic Dungeon Pack, generated procedurally,
living at Assets/Whitebox/. Art-free version of every FDP part, comp, and LVL
module so the project can be tested without the FDP dependency, and so any
third-party modular pack following the same folder conventions can be swapped in.

### Folder layout
Assets/Whitebox/
├── 3d/modular/          — mesh .asset files (Step 1 output)
├── Materials/           — per-category tinted URP/Lit materials (Step 2 output)
├── prefabs/
│   ├── modular/         — part prefabs, mirror of FDP 01_PARTS (Step 2)
│   ├── COMPS/           — composition prefabs, mirror of FDP 02_COMPS (Step 3)
│   └── LEVEL_MODULES/   — level module prefabs, mirror of FDP 03_LEVEL_MODULES (Step 4)

### Generator
Assets/Scripts/Editor/WhiteboxPackFactory.cs — four-step procedural factory,
run via LevelGen ▶ Whitebox ▶ … menu. Each step is idempotent and has a
dry-run variant.

- Step 1 — mirror meshes. Walks FDP 3d/modular/ recursively, extracts every
  Mesh sub-asset from each FBX, saves as standalone .asset files. Deep-copies
  geometry (vertices, normals, tangents, UVs, triangles) — does not approximate.

- Step 2 — wrap in prefabs. For each mesh, produces a single-GameObject prefab
  with MeshFilter + MeshRenderer. Applies a shared URP/Lit material tinted per
  top-level subfolder (Wall = off-white, Floor = grey, Gateway = pale blue,
  Column = pale green, Stairs = yellow, Railing = tan, Base = mid-grey).
  Cutout variants for alpha-clipped pieces. No colliders, no components.

- Step 3 — mirror comps. For each FDP comp, loads via LoadPrefabContents,
  finds every nested prefab-instance child (IsOutermostPrefabInstanceRoot),
  destroys-and-reinstantiates each as the whitebox equivalent. Preserves
  local transforms, rotations, scales, names.

- Step 4 — mirror LVL modules. Two-pass. Pass 1 generates every whitebox LVL,
  tagging cross-LVL references with the editor-only WhiteboxPendingLvlRef
  component on primitive cube placeholders. Pass 2 re-opens each LVL, resolves
  every pending reference against the now-complete whitebox LVL tree, swaps
  placeholders for real LVL instances, strips the marker component.

### Mapper
TryMapFdpReferenceToWhitebox — unified reference-lookup helper. Three tiers
based on which segment the FDP path contains:
- 01_PARTS/          → Assets/Whitebox/prefabs/modular/
- 02_COMPS/          → Assets/Whitebox/prefabs/COMPS/
- 03_LEVEL_MODULES/  → Assets/Whitebox/prefabs/LEVEL_MODULES/

Each tier does exact-path match first, then fuzzy filename match.
Fuzzy normalization: lowercase, strip leading p_/mod_/comp_/lvl_ prefixes,
strip trailing (N) Unity duplicate suffix, trim. Exactly one match wins;
zero → miss; two or more → ambiguous (surfaced, never silently resolved).

Fuzzy is the only tier currently firing, because Step 2 named whitebox
prefabs from FBX sub-mesh names (MOD_*) while FDP references use prefab
filenames (P_MOD_*). Intentional. When a future pack is swapped in with
consistent naming, exact-match tier will fire instead.

### Diagnostic
LevelGen ▶ Whitebox ▶ Diagnose Step 3 — introspects a test comp's hierarchy
and logs prefab-resolution details per child without writing. Kept in the
file for future debugging.

### Current state
Mirror is complete. Steps 1–4 produce a structurally faithful, untextured
version of FDP. No RoomPiece, ExitPoint, or other generator components have
been added to any whitebox prefab. PieceCatalogue integration not yet wired.

### Next steps
1. Create a whitebox PieceCatalogue asset, auto-populate from
   Assets/Whitebox/prefabs/modular/, validate in Room Workshop.
2. Run LVL_Configurator across Assets/Whitebox/prefabs/LEVEL_MODULES/
   to stamp RoomPiece + ExitPoint components from filename suffixes.
3. End-to-end test: drop configured whitebox LVLs into LevelGenerator.unity
   and verify connections work.

## Folder structure
Assets/
├── Scripts/
│   ├── Generation/
│   │   ├── ExitPoint.cs
│   │   ├── RoomPiece.cs
│   │   ├── BoundsChecker.cs
│   │   └── LevelGenerator.cs
│   ├── Data/
│   │   ├── SeedData.cs
│   │   └── LevelSequence.cs
│   ├── Workshop/
│   │   ├── PieceCatalogue.cs
│   │   ├── RoomDefinition.cs
│   │   ├── RoomBuilder.cs
│   │   ├── PropEntry.cs
│   │   ├── PropCatalogue.cs
│   │   ├── SpawnPoint.cs
│   │   ├── RoomContentGenerator.cs
│   │   ├── RoomPreset.cs
│   │   └── RoomPresetLibrary.cs
│   └── Editor/
│       ├── LevelGeneratorEditor.cs
│       ├── RoomWorkshopWindow.cs
│       ├── PieceCatalogueEditor.cs
│       ├── PlaceholderPrefabFactory.cs
│       ├── LevelGenSetup.cs
│       └── LVL_Configurator.cs
├── Prefabs/
│   ├── Rooms/
│   ├── Halls/
│   └── Curated/
├── PropCatalogues/
├── RoomPresets/
├── LevelSequences/
└── Scenes/
    ├── SampleScene (current working scene)
    ├── RoomWorkshop.unity (to be created)
    └── LevelGenerator.unity (to be created)

## Namespace
All scripts use namespace LevelGen.

## Key design decisions
- System.Random (not UnityEngine.Random) for deterministic seeds
- Bounds overlap uses AABB list check (not physics)
- BoundsChecker is static — no MonoBehaviour
- LevelSequence is a ScriptableObject
- Editor inspector works in Edit mode for preview

## Coding conventions
- XML doc comments on all public members
- [Tooltip(...)] on all inspector fields
- Gizmos for spatial debugging
- #if UNITY_EDITOR guards on editor-only code
- No magic numbers — named constants or inspector fields

## Three-scene pipeline
1. RoomWorkshop.unity — build and curate individual rooms
2. LevelGenerator.unity — assemble levels from room prefabs
3. Level_XX.unity — baked gameplay scenes

## Ground truth — VERIFIED from demoscene_dungeon_level_1_dungeon

Snap units (pivot-to-pivot distance between connected pieces):
  _small_ = 2 units
  _med_   = 4 units
  _large_ = 6 units

Verified from live scene measurements:
  LVL_01_O_rail_straight_angle_SE: Z=-4, Y=-2
  LVL_01_O_rail_straight_SEW:      Z=0,  Y=-2
  Z difference = 4 = snap unit for _small_ rail ✓
  Y=-2 = floor level offset from world origin

Vertical measurements:
  Wall height    = 6 units
  Half height    = 3 units
  Stair Y step   = -3 per section (down) / +3 (up)
  Floor Y offset = varies by level (-2 in level 1)

Pivot conventions:
  _O_ OneSided:    pivot at ONE EDGE of piece
  _M_ PivotMiddle: pivot at CENTER of piece
  _E_ PivotEdge:   pivot at ONE EDGE

USE ONLY _M_ PivotMiddle for our generator.

_M_ piece half-extents from pivot:
  _large_: ±3 units X and Z  (6 unit piece)
  _med_:   ±2 units X and Z  (4 unit piece)
  _small_: ±1 unit  X and Z  (2 unit piece)

ExitPoint positions on _M_ large module:
  North: (0, 0, +3)
  South: (0, 0, -3)
  East:  (+3, 0, 0)
  West:  (-3, 0, 0)
  Up:    (0, +6, 0)
  Down:  (0,  0, 0)

Connection math for two _M_ large modules (NS):
  Module 1 pivot at (0, 0, 0)
    North exit at (0, 0, +3)
  Module 2 pivot at (0, 0, +6)
    South exit at (0, 0, +6-3) = (0, 0, +3) ✓
  Both exits at same world point ✓
  Snap = 6 units ✓

Floor tile pivot:
  Confirmed corner pivot (not center)
  _med floor: bounds center (-2,0,+2), size (4,0,4)
  Tile extends -4 in -X and +4 in +Z from pivot
  Placement: startX = -halfWidth + FloorStep  ← CRITICAL offset
             startZ = -halfDepth
  Step = FloorStep = 4 for _med tiles
  For 12×12 room: tiles at X = -2, +2, +6  → covers X = -6 to +6 ✓
                  tiles at Z = -6, -2, +2  → covers Z = -6 to +6 ✓

## Asset pack
Fantastic Dungeon Pack (URP U6-1)
Location: Assets/Fantastic Dungeon Pack/prefabs/MODULAR/

01_PARTS:
  Base/
  Column/
  Floor/OneSided, Floor/PivotEdge
  Gateway/
  Railing/
  Stairs/BotCap, Stairs/Railing, Stairs/Stairs
  Wall/OneSided, Wall/PivotEdge, Wall/PivotMiddle
  WallTrim/
  Trim/WallCover, Trim/WallTrim

02_COMPS:
  Column/
  Floor/
  Gateway/
  Wall/OneSided, Wall/PivotEdge, Wall/PivotMiddle

03_LEVEL_MODULES:
  01/OneSided, 01/PivotMiddle
  (Wall large, Wall med, Wall small subfolders)

PROPS/ — decorative, NOT auto-catalogued
Placed manually via PropCatalogue only.

Pack naming conventions:
  Snap unit: 2/4/6 (_small_/_med_/_large_)
  Pivot types: _M_ = center, _E_ = edge, _O_ = edge
  Direction suffixes: _NS, _SEW, _NSEW = exit directions
  Use ONLY _M_ (PivotMiddle) variants in our system.

## Architecture — two room types

TYPE 1 — Standard rooms/halls (LVL_ modules):
  LVL_ prefabs are complete assembled rooms
  Add RoomPiece + ExitPoints from name suffix only
  Generator places them as-is, no assembly needed
  Tool: LVL_Configurator editor utility
  Save to: Assets/Prefabs/Rooms/ or Assets/Prefabs/Halls/

TYPE 2 — Custom rooms (COMP_ pieces):
  Boss rooms, treasure rooms, special areas
  Built in Room Workshop using COMP_ as snap unit
  SNAP_UNIT = 6 for large Comps, 4 for med Comps
  NOT built from individual Parts

## PieceCatalogue system
PieceType enum: Floor, Wall, Doorway, Corner, Column, Ceiling, Stair, None=99
  None = 99 — staging slot for pieces pending categorization.
  Explicit integer value 99 future-proofs against reordering of real types.
  Never used by the generator (GetRandom / CountOfType ignore it naturally).

PieceEntry inner class: GameObject prefab, PieceType,
  string subFolder, bool isExit (default false)
  isExit (bool, default false) — Doorway entries only.
  true = generator exit (spawns ExitPoint).
  false = decorative (no ExitPoint).
  Hidden in inspector for all non-Doorway piece types.
  Auto-populate sets isExit = false on new entries; preserves existing value
  on re-populate (matched by prefab reference).

Unified List<PieceEntry> pieces (not separate lists per type)
Method: GetPiecesByType(PieceType) → List<PieceEntry>

PieceCatalogueEditor — per-section ReorderableList architecture:
  One foldout per PieceType (Floor → Stair) each with its own ReorderableList
  backed by a List<int> realIndices (view-index → real index in pieces).
  Foldout state persisted via EditorPrefs keyed by asset GUID + type name.
  All real-type sections default to expanded; Skipped defaults to collapsed.
  Expand All / Collapse All buttons above the section list.

  Skipped section (PieceType.None):
    Rendered after the seven real-type sections with a yellow-tinted helpBox
    and "staging — not used by generator" mini-label.
    Per-row Destination popup (dropdown of real types).
    "Will move to: X" label + Move button appear when a destination is chosen.
    Move applies the type change via serializedObject and rebuilds all sections.
    Pieces stay in Skipped until Move is clicked — no auto-migration on type change.

  Filter UI:
    Piece Type dropdown: All / Floor..Stair / Skipped
      "Skipped" maps to PieceType.None; hides all other sections when selected.
    Prefab name dropdown: All / sorted names scoped to visible section(s).
    When prefab name filter is active:
      — entries not matching are hidden but section remains editable
      — + button replaced with helpbox "clear filter to add new entries"
      — ✕ delete still works on visible rows
    Type filter auto-expands the matching section.

  Per-section + button: new entry pre-set to that section's PieceType, isExit=false.
  ✕ per-row delete: deferred (pendingDeleteRealIndex) to fire after DoLayoutList.
  Drag-reorder within a section: swaps real entries at sorted slot positions,
    preserving relative order of all other types.
  Reorder across sections: not supported (change type via Destination + Move).

Auto-populate scans a root folder, maps subfolders to PieceType by name.
Unmapped prefabs (Trim, Railing, OneSided, etc.) are added as PieceType.None
instead of being discarded — they appear in the Skipped section for review.
Re-populate preserves existing pieceType (user promotions survive re-run).
Dialog shows: Added (real types) / Staged (None) / Skipped (duplicates + nulls).

Subfolder → PieceType mapping:
  contains "WallCover"       → PieceType.Ceiling  (checked before Trim)
  contains "Floor"           → PieceType.Floor
  contains "Wall" + "Middle" + "corner"/"angle"/"concave" in filename → PieceType.Corner
  contains "Wall" + "Middle" (straight) → PieceType.Wall
  contains "Gateway"         → PieceType.Doorway
  contains "Column"          → PieceType.Column  (freestanding decorative, NOT room corners)
  contains "Stair"           → PieceType.Stair
  contains "Trim"            → PieceType.None  (staged)
  contains "Railing"         → PieceType.None  (staged)
  contains "OneSided"        → PieceType.None  (staged)
  contains "PivotEdge"       → PieceType.None  (staged)
  (no match)                 → PieceType.None  (staged)

## Room Workshop System
Workshop is an EditorWindow (LevelGen → Room Workshop).
Builds CUSTOM rooms from COMP_ pieces only.
Standard rooms use LVL_ modules via LVL_Configurator.

Seven-step workflow:
⓪ Seed: optional foldout — int seed field + "Generate Room" button
  Deterministic room generation: clears room, randomises size/walls/corners/openings
  Uses System.Random(seed) — same seed = same room every time
  Disabled if no PieceCatalogue assigned
① Define: size preset (Small/Medium/Large/Custom)
  Custom: Width slider + Depth slider (multiples of 12) + Height slider (multiples of 6, 6–18 max 3 tiers)
② Catalogue: assign PieceCatalogue asset
  (should contain COMP_ pieces, not Parts)
③ Build: per-wall/corner size selectors + Build Room button
  Each of N/S/E/W walls and NW/NE/SW/SE corners has its own
  WallSize dropdown (Large / Med / Small / None).
  None = skip that wall or corner entirely (open side / open corner).
  Multi-tier rooms stack wall geometry vertically (WallTier = 6 units per tier).
  Buttons: Build Room, Rebuild Floor, Rebuild Walls, Clear Room.
④ Entry / Exit: wall side dropdown + Double Door checkbox + Opening Tier dropdown + Add Opening button
  isExit toggle: true = generator exit (ExitPoint stamped), false = decorative (no ExitPoint).
  Wall slot: randomly selected from eligible Wall_{side}_N pieces (not first/last).
  Doorway prefab: randomly selected from catalogue entries matching isExit value.
  Small (12×12) room fallback: ignores first/last exclusion when all slots are corner-adjacent.
  ExitPoint stamped on MOD root immediately when isExit = true — no separate step needed.
  Double Door: disabled for Small preset; three logic paths (see Double Door section).
  Opening Tier: dropdown shown only when tiers > 1; selects which wall tier receives the opening.
    Tier 0 pieces: named Wall_N_0 (no suffix)
    Tier 1 pieces: named Wall_N_0_t1
    Tier 2 pieces: named Wall_N_0_t2
⑤ Components: Apply Bounds button (was Apply Components).
  Stamps RoomPiece + bounds only (pieceType, boundsSize, boundsOffset).
  ExitPoints are NOT touched — they are created automatically by Add Opening
  at door-add time, at each door's exact local position.
  Rebuild Exits from Doors: separate button that clears all ExitPoints and
  re-stamps one per child in doors/ group using direction inference.
  Use after manually editing the doors/ hierarchy.
  Direction inference per door:
    XZ-nearest-wall for wall doors (N/S/E/W).
    Y-based for ceiling/floor doors (Up/Down) — detected when door is
    interior in XZ (>2 units from every wall face) AND near ceiling or floor.
  ExitPoint Y snapped to nearest WallTier boundary.
  Up exits forced to Y = FullHeight; Down exits forced to Y = 0.
⑥ Save: Type dropdown (Room / Hall) + name field + Save Prefab + Preset button
  Room → Assets/Prefabs/Rooms/Curated/{name}.prefab
  Hall  → Assets/Prefabs/Halls/{name}.prefab
  RoomPreset always → Assets/RoomPresets/{name}.asset
  Path preview label updates live as Type or Name changes.

Size presets — multiples of 12 (LCM of FloorStep=4 and WallStep=6):
  Small  = 12×12   (1 tier, fixed height)
  Medium = 24×24   (1 tier, fixed height)
  Large  = 36×36   (1 tier, fixed height)
  Custom = any multiple of 12, height = multiple of 6 (1–3 tiers)

WallSize enum: Large / Med / Small / None
  Used for both wall runs and corner pieces.
  None skips placement entirely for that side or corner.

Wall tier stacking — VERIFIED WORKING:
  WallTier = 6f constant (one wall piece height)
  tiers = Mathf.RoundToInt(FullHeight / WallTier)  (1, 2, or 3)
  BuildWalls loops over tiers; yOffset = tier * WallTier for each tier
  PlaceWallRun and PlaceCorners both accept yOffset and tier parameters
  TierName helper: tier 0 → baseName unchanged; tier N → "{baseName}_t{N}"
  ExitPoint Y carries the tier offset (not forced to 0)

## Corner floor tile placement — VERIFIED WORKING

When a corner is angle, concave, or convex, the floor tile at that corner uses a
special prefab and requires both a rotation and a position offset to stay
in place (the _O_ corner-pivot tile swings its geometry when rotated).

  corner piece name → floor tile selected:
    angle_[size]   (straight, no _2)  → P_MOD_Floor_01_O_angle_med   (base, must NOT be _3)
    angle_[size]_2 (name ends "_2")   → COMP_Floor_01_O_angle_med_3 (pivot-corrected _3 variant)
    concave_[size] (straight, no _2)  → P_MOD_Floor_01_O_convex_med  (base, must NOT be _3)
    concave_[size]_2 (name ends "_2") → COMP_Floor_01_O_convex_med_3 (pivot-corrected _3 variant)
    convex_[size]  (straight, no _2)  → P_MOD_Floor_01_O_concave_med (base, must NOT be _3)
    convex_[size]_2 (name ends "_2")  → concaveTiles with _3 suffix (pivot-corrected variant)

  Selection logic (CornerTile in RoomWorkshopWindow):
    Straight variants → explicitly find first catalogue entry NOT ending in "_3"
    _2 variants       → explicitly find first catalogue entry ending in "_3"
    Both fall back to index 0 if no match found in catalogue

  Rotation matches the corner piece rotation:
    SW → Y=-90°    SE → Y=180°    NW → Y=0°    NE → Y=90°

  Position offset (applied on top of grid position):
    SW: (0, 0, +4)      SE: (-4, 0, +4)
    NW: none            NE: (-4, 0, 0)

  Pivot convention — ALL _O_ floor tiles must have their pivot at the standard
  corner position so the tileOff values above apply correctly. The _3 suffix
  variants (COMP_Floor_01_O_convex_med_3, COMP_Floor_01_O_angle_med_3) had
  non-standard pivots and were fixed in the prefabs — do not revert those changes.

  Grid positions (36×36 room example):
    SW (ix=0,       iz=0      ): pivot (-14, 0, -18) + offset → (-14, 0, -14)
    SE (ix=countX-1,iz=0      ): pivot ( 18, 0, -18) + offset → ( 14, 0, -14)
    NW (ix=0,       iz=countZ-1): pivot (-14, 0,  14) + offset → (-14, 0,  14)
    NE (ix=countX-1,iz=countZ-1): pivot ( 18, 0,  14) + offset → ( 14, 0,  14)

## RoomBuilder — VERIFIED placement math

Constants:
  FloorStep = 4f   (floor tile snap, _med _O_ tile)
  WallStep  = 6f   (wall piece snap, large COMP_)

Floor grid (_O_ corner-pivot tile, extends -X and +Z):
  startX = -HalfWidth + FloorStep   ← offset so tiles cover to +HalfWidth
  startZ = -HalfDepth
  step   = FloorStep
  countX = Mathf.RoundToInt(FullWidth / FloorStep)
  countZ = Mathf.RoundToInt(FullDepth / FloorStep)

  Tile pools (BuildFloors):
    baseTiles    — Floor entries with "straight" in name  → fills the grid
    bonepiles    — Floor entries with "bonepile" in name  → 15 % random swap
    angleTiles   — Floor entries with "angle"             → corner positions only
    concaveTiles — Floor entries with "concave"           → corner positions only
    convexTiles  — Floor entries with "convex"            → corner positions only
  nonCornerTiles = all Floor entries without angle/convex/concave in name.
  Fallback baseTile (no "straight" found): uses nonCornerTiles[0], never a corner tile.
  angle / concave / convex tiles NEVER placed in the main floor fill grid.

Wall runs (edge-pivot pieces — extend one FloorStep in rotation direction):
  count per side = Mathf.RoundToInt(FullWidth / FloorStep) - 1
    (excludes the two corner slots)
  fixed coords (no inset):
    northZ = +HalfDepth   southZ = -HalfDepth
    eastX  = +HalfWidth   westX  = -HalfWidth
  start varies per wall due to edge-pivot direction:
    North (Y=0°):   startX = -HalfWidth + FloorStep * 1.5f
    South (Y=180°): startX = -HalfWidth + FloorStep * 0.5f
    East  (Y=90°):  startZ = -HalfDepth + FloorStep * 0.5f
    West  (Y=-90°): startZ = -HalfDepth + FloorStep * 1.5f
  step in PlaceWallRun = FloorStep

Wall rotations (face inward):
  North → Quaternion.identity        (Y=0°)
  South → Quaternion.Euler(0,180,0)
  East  → Quaternion.Euler(0, 90,0)
  West  → Quaternion.Euler(0,-90,0)

Corner pieces — Wall type, never Column:
  Each corner has its own WallSize setting (NW/NE/SW/SE).
  None = skip that corner.
  Prefab filter: name contains "corner" + size string, not "column".
  Prefer _2 suffix (solid corner variant).
  cx = HalfWidth, cz = HalfDepth
  SW (-cx,-cz): Y=90°   SE (+cx,-cz): Y=0°
  NE (+cx,+cz): Y=-90°  NW (-cx,+cz): Y=180°

Wall prefab filter helpers:
  GetStraightWallPrefabs(WallSize) → Wall entries with "straight" + size string
  GetHalfWallPrefabs(WallSize)     → Wall entries with "straight" + size string + "half"
  GetCornerPrefabs(WallSize)       → Wall entries with "corner" + size string
  WallSizeString(WallSize)         → "large" / "med" / "small" / ""

Half-wall logic for angle/concave/convex corners — VERIFIED WORKING:
  When a corner type is angle, concave, or convex (but NOT a _2 variant),
  the two adjacent wall pieces use a half-length variant:
    COMP_Wall_01_M_straight_large_half_R
  The _R suffix means geometry is always on local +X (right) side of piece.

  _2 suffix corners (e.g. angle_large_2) are solid-corner pieces that close
  their own gap — they always use full-length adjacent walls. CornerNeedsHalfWall
  returns false immediately for any corner whose name ends in "_2".

  Half flags (computed in BuildWalls before PlaceWallRun calls):
    nwHalf = cornerNW != None && CornerNeedsHalfWall(NW)
    neHalf = cornerNE != None && CornerNeedsHalfWall(NE)
    swHalf = cornerSW != None && CornerNeedsHalfWall(SW)
    seHalf = cornerSE != None && CornerNeedsHalfWall(SE)
  CornerNeedsHalfWall: name ends "_2" → false; else name contains
    "angle" OR "concave" OR "convex" → true.

  PlaceWallRun has 4 optional override parameters:
    startOverride / endOverride     — alternate prefab list for first/last slot
    flipStartRotation / flipEndRotation — adds 180° Y to rotation
    shiftStartToward / shiftEndToward   — moves pivot one FloorStep inward

  Per-wall half-piece rules (wall Y rotation → local +X direction):
    Y=0°  (North): +X = East  → geometry faces East
    Y=90° (East):  +X = South → geometry faces South
    Y=180°(South): +X = West  → geometry faces West
    Y=-90°(West):  +X = North → geometry faces North

  Eight affected slots and their correction:
    N_0  (NW corner): +X already faces away → no flip, no shift
    N_last(NE corner): +X faces INTO corner → flip + shiftEndToward
    S_0  (SW corner): +X faces INTO corner → flip + shiftStartToward
    S_last(SE corner): +X already faces away → no flip, no shift
    E_0  (SE corner): +X faces INTO corner → flip + shiftStartToward
    E_last(NE corner): +X already faces away → no flip, no shift
    W_0  (SW corner): +X already faces away → no flip, no shift
    W_last(NW corner): +X faces INTO corner → flip + shiftEndToward

Door placement:
  Add Opening picks a random Wall_{side}_N child from walls/ group,
  destroys it, and stamps an ExitPoint child on MOD root at the wall
  slot's local position (Y = tier * WallTier — already tier-snapped).
  ExitPoint direction maps directly from the WallSide enum.
  Named "Exit_{dir}_{n}" where n = count of same-direction ExitPoints
  already on MOD root. Multiple doors on same side produce Exit_N_0,
  Exit_N_1, etc.
  RoomPiece is auto-added to MOD root if not already present.

  Apply Bounds only updates boundsSize / boundsOffset on RoomPiece.
  Rebuild Exits from Doors re-stamps exits from the doors/ hierarchy
  using DetectExitDirection (XZ-nearest-wall + Up/Down interior check).
  ExitPoint Y snapped to nearest WallTier boundary.
  Vertical exits forced to Y=FullHeight (Up) or Y=0 (Down).

Bounds:
  boundsSize   = (HalfWidth, HalfHeight, HalfDepth)
  boundsOffset = (0, HalfHeight, 0)

## Double Door logic — VERIFIED WORKING

Disabled for Small (12×12) preset.
Dispatch based on candidates.Count and CornerNeedsHalfWall:

  candidates = wall run pieces on the chosen side (excludes corners)
  candidates.Count == 3 → Medium-like span (24-unit wall = 3 non-corner slots)
    CornerNeedsHalfWall returns false (_corner_ corners):
      DoDoubleDoorMediumStandard — destroys all 3 candidates
        flanks replaced with half walls (COMP_Wall_01_M_straight_large_half_R)
        flipped flank shifts FloorStep toward its corner along run axis:
          North flank hi (NE side): flip + shift (-FloorStep on X)
          South flank lo (SW side): flip + shift (+FloorStep on X)
          East  flank lo (SE side): flip + shift (+FloorStep on Z)
          West  flank hi (NW side): flip + shift (-FloorStep on Z)
        un-flipped flank stays at candidate position
    CornerNeedsHalfWall returns true (angle/concave/convex corners):
      DoDoubleDoorMediumSpecial — destroys all wall run pieces
        re-places full walls at corner-adjacent grid positions
        each replacement shifts 2 units toward 0 on the RUN axis:
          N/S walls (run axis = X): posFirst +2 X, posLast -2 X
          E/W walls (run axis = Z): posFirst +2 Z, posLast -2 Z

  candidates.Count > 3 → Large+ span
    span ≤ 60 units: removes 2 adjacent centre slots, ExitPoint at midpoint
    span > 60 units: two double openings at 1/3 and 2/3 of candidates

CornerNeedsHalfWall: returns false if name ends "_2"; otherwise true if
  name contains "angle", "concave", or "convex".

Half-wall _R suffix rule:
  Geometry always on local +X of piece.
  The flank whose +X points INTO the opening needs flip (180° Y) + pivot shift.
  The flank whose +X points AWAY from the opening needs no correction.

## LVL_Configurator
EditorWindow: LevelGen → LVL Configurator
Processes LVL_ prefabs into generator-ready prefabs.

Name parsing:
  Size suffix:
    _large_ → halfExtent=3, snapUnit=6
    _med_   → halfExtent=2, snapUnit=4
    _small_ → halfExtent=1, snapUnit=2
    _tiny_  → halfExtent=0.5, snapUnit=1

  Exit suffix (compass directions in name):
    N=North, S=South, E=East, W=West, U=Up, D=Down
    Examples: _NS, _SEW, _NSEW, _S, _SE

  PieceType detection:
    name contains "stair"         → Stair
    name contains "hall"          → Hall
    exits = N+S only (straight)   → Hall
    exits = S only                → Hall
    else                          → Room

RoomPiece settings:
  boundsSize   = (halfExtent, 3f, halfExtent)
  boundsOffset = (0, 3f, 0)

ExitPoint positions:
  North: (0, 0, +halfExtent)
  South: (0, 0, -halfExtent)
  East:  (+halfExtent, 0, 0)
  West:  (-halfExtent, 0, 0)
  Up:    (0, +6, 0)
  Down:  (0, 0, 0)

Save paths:
  Hall/Stair → Assets/Prefabs/Halls/[name]_LG.prefab
  Room       → Assets/Prefabs/Rooms/[name]_LG.prefab

Batch button: process entire folder at once.
Skip prefabs that already have RoomPiece component.

## Scripts status
Generation/:
  ExitPoint.cs ✓
  RoomPiece.cs ✓
  BoundsChecker.cs ✓
  LevelGenerator.cs ✓ (simplified: roomCount, 70/30 room/hall, no start room, no dead-end caps)

Data/:
  SeedData.cs ✓
  LevelSequence.cs ✓

Workshop/:
  PieceCatalogue.cs ✓
  RoomDefinition.cs ✓
  RoomBuilder.cs ⚠ (needs rebuild at COMP_ level)
  PropEntry.cs ✓
  PropCatalogue.cs ✓
  SpawnPoint.cs ✓
  RoomContentGenerator.cs ✓
  RoomPreset.cs ✓
  RoomPresetLibrary.cs ✓

Editor/:
  LevelGeneratorEditor.cs ✓
  RoomWorkshopWindow.cs ✓ (redesigned for actual pack workflow)
  PieceCatalogueEditor.cs ✓
  PlaceholderPrefabFactory.cs ✓
  LevelGenSetup.cs ✓
  LVL_Configurator.cs ✓

## Next CC task (run this when resuming)
Read CLAUDE.md fully.
RoomWorkshopWindow is verified and working through step ⑤. ALL features below confirmed working:
  - Floor grid, wall runs (per-wall WallSize), corners (per-corner WallSize) ✓
  - Door replacement: random wall slot, random doorway prefab, isExit toggle ✓
  - ExitPoint stamping (isExit=true) and decorative door (isExit=false) ✓
  - Half-wall logic for angle/concave/convex corners (not _2 variants) ✓
  - _2 suffix corners always use full-length adjacent walls ✓
  - Corner floor tiles (angle/concave/convex) with correct rotation and position offset ✓
  - Convex corners use concaveTiles pool; _2 variant uses _3-suffix concaveTile ✓
  - angle/concave/convex tiles excluded from main floor fill; fallback never picks corner tile ✓
  - Small (12×12) room door fallback (ignores first/last exclusion) ✓
  - Column is its own PieceType (freestanding decorative, never used as corners) ✓
  - Double Door — all four logic paths (Medium Standard, Medium Special, Large, Large>60) ✓
  - Custom Height / Wall Tiers — 1–3 tiers (multiples of 6), geometry stacks vertically ✓
  - Per-tier Opening Tier dropdown — shown only when tiers > 1 ✓
  - Seed Generator (⓪ step) — deterministic room generation from int seed ✓
  - Step ⑥ Save: Type dropdown (Room/Hall) routes prefab to correct folder ✓
    Not yet tested end-to-end in Unity.
  - Dress step (PropCatalogue / SpawnPoints) not yet implemented.

PieceCatalogueEditor is verified working (per-section ReorderableList architecture):
  - One foldout + ReorderableList per PieceType (Floor → Stair) ✓
  - Skipped section (PieceType.None) with yellow tint and Move button ✓
  - isExit field hidden for all non-Doorway entries ✓
  - Filter: PieceType dropdown (includes Skipped) + prefab name dropdown ✓
  - Name filter hides non-matching rows but keeps +/- enabled per section ✓
  - Per-entry ✕ delete works in all display modes ✓
  - Drag-reorder within a section preserves cross-type ordering ✓
  - Auto-populate: stages unmapped pieces as None, preserves type on re-populate ✓
  - Breakdown: Skipped row + Total (live) + Total (all) with divider ✓

PieceCatalogue.cs:
  - PieceType.None = 99 added (staging slot, never used by generator) ✓

Do not touch LVL_Configurator (it is complete).

Pending work (priority order):
  1. Test DoSave end-to-end (step ⑥) — both Room and Hall paths
  2. Implement Dress step (PropCatalogue / SpawnPoints)
  3. Create RoomWorkshop.unity scene
  4. Create LevelGenerator.unity scene

## Boss room analysis — VERIFIED ground truth

Source: demoscene_dungeon_level_5_bossroom

Room structure (MOD parent, no components):
  MOD/
  ├── ceiling/   P_MOD_WallCover_ pieces
  ├── columns/   COMP_Column_ pieces  
  ├── doors/     COMP_Door_01_large ← EXITS live here
  ├── floors/    P_MOD_Floor_01_O_straight_med pieces
  ├── railings/  P_MOD_Stairs_01_E_straight pieces
  ├── stairs/    LVL_01_O_stairs_ modules
  └── walls/     COMP_Wall_01_O_straight + P_MOD_Wall_

Floor piece confirmed: P_MOD_Floor_01_O_straight_med
  _O_ corner pivot
  Snap unit = 4 units (med size)

Wall pieces: COMP_Wall_01_O_straight_med/_large
  _O_ edge pivot

Exit piece: COMP_Door_01_large
  Position X=4, Y=3, Z=-26
  Y=3 = half wall height (doors at mid-height)
  THIS is where ExitPoints should be placed

Stairs: LVL_01_O_stairs_angle_HOLE_5
  LVL_ modules used ONLY for stair sections
  Not used for main room walls/floors

KEY INSIGHT:
  Doors/Gateways define exits, NOT wall sections
  ExitPoint position = COMP_Door_ world position
  ExitPoint direction = away from room center

OneSided (O) folder = wall sections and fills
  Used for room perimeters
  Edge pivot

PivotMiddle (M) folder = same pieces, center pivot
  Better for our generator placement math

## Revised Room Workshop approach — VERIFIED WORKING

Step 1: Define room size (multiples of 12)
  Room Workshop size presets: Small=12, Medium=24, Large=36

Step 2: Build Room — auto-fills:
  a) Floor grid   (P_MOD_Floor_01_O_straight_med, _O_ corner pivot)
     startX = -HalfWidth + FloorStep  startZ = -HalfDepth  step=4
  b) Wall perimeter — each side (N/S/E/W) has its own WallSize dropdown
     straight + size filter, edge pivot, per-wall start positions,
     step=FloorStep=4, no inset; None = skip that side entirely
  c) Corner pieces — each corner (NW/NE/SW/SE) has its own WallSize dropdown
     corner + size filter, Wall type; None = skip that corner

Step 3: Add Door — "Add Door" button on chosen WallSide:
  Randomly replaces one Wall_{side}_N piece with Doorway piece
  ExitPoint stamped on MOD root immediately (no separate step)

Step 4: Apply Components (optional refresh)
  Rebuilds RoomPiece + all ExitPoints from doors/ group

Step 5: Save MOD as prefab
  Select Type (Room / Hall) → saves to Rooms/Curated/ or Halls/ accordingly

## LVL_ modules — revised understanding
OneSided folder = wall/railing/stair sections
  Used to build room perimeters piece by piece
  NOT complete rooms

PivotMiddle folder = same, center pivot variants
  Better for generator — use these for halls

Complete rooms are ASSEMBLED from parts/comps
NOT selected as single pre-built pieces

## Cell-map room model — Phase 1 foundation

Three files in Assets/Scripts/LevelEditor/ form the Phase 1 foundation:

  TileType.cs   — TileType enum (Empty, Square, Triangle*, Quarter*, …) +
                  TileTypeInfo static lookup (edge occupancy, rotation helpers)
  CellMap.cs    — 2D grid of Cell structs; fixed-size, serializable.
                  Cell = (TileType, tier, rotSteps). CellSize = 4 units (matches
                  old FloorStep). ToAscii() for debug dumps.
  ShapeStamp.cs — Static utility that stamps pre-populated CellMaps for common
                  geometric shapes. Floor cells only — no wall, corner, or
                  prefab logic.

### ShapeStamp methods

All methods return a new CellMap with cells at tier 0, rotSteps 0 unless noted.
Invalid inputs are clamped (not thrown) and logged via Debug.LogWarning.

  Rectangle(int width, int depth)
    Fills every cell with TileType.Square. Grid is exactly width × depth.
    Clamps: width and depth to min 1.

  Diamond(int size)
    size × size grid. Cells with Manhattan distance ≤ (size-1)/2 from center
    become Square. Diagonal-edge border cells (off the cardinal axes, at exactly
    the radius boundary) use Triangle tiles — hypotenuse faces outward:
      NE-facing cut → TriangleSW   NW-facing cut → TriangleSE
      SE-facing cut → TriangleNW   SW-facing cut → TriangleNE
    Even sizes: float center at X.5 — no cells land exactly on the boundary,
    so no Triangle tiles are produced (pure Square diamond).
    Clamps: size to min 3.

  Circle(int radius)
    (2*radius+1) × (2*radius+1) grid, center at (radius, radius).
    Boundary ring = cells within 0.5 cell-units of the exact radius:
      dist ≤ radius-0.5 → Square (inner)
      radius-0.5 < dist ≤ radius+0.5 → Quarter tile (off-axis) or Square (on-axis)
      dist > radius+0.5 → Empty
    Quarter tile orientation (curve faces center):
      NE quadrant → QuarterSW   NW quadrant → QuarterSE
      SE quadrant → QuarterNW   SW quadrant → QuarterNE
    On-axis cells (dx==0 or dz==0) are always Square regardless of ring.
    Hot path uses squared-distance comparison — no sqrt.
    Clamps: radius to min 1.

### LevelEditor/Tests menu item

  LevelEditor → Tests → Dump Shape Stamps to Console
  Source: Assets/Scripts/LevelEditor/Editor/ShapeStamp_Test.cs
  Generates Rectangle(5,3), Diamond(5), and Circle(3), calls ToAscii() on each,
  and logs them. Smoke test only — confirms all three methods run without
  exception and produce plausible ASCII output.

LVL_ modules only used as-is for STAIRS

## Cell-map room model — Phase 2: EdgeSolver

Source: Assets/Scripts/LevelEditor/EdgeSolver.cs

### Purpose
EdgeSolver walks a CellMap and produces three ordered placement lists that
form the complete instruction set for the room builder. It is pure data:
  in  = CellMap
  out = SolveResult (floors, walls, corners, warnings)

No prefab references, no catalogue reads, no scene access.

### Types defined in EdgeSolver.cs (namespace LevelEditor)

  WallKind enum: Straight (emitted), Angle/Concave/Convex (deferred)
  CornerKind enum: Outward (emitted), Inward/Diagonal (deferred)

  FloorPlacement struct: worldPosition, rotation, tileType, tier, gridCoord
  WallPlacement struct:  worldPosition, rotation, kind, tier, edge, gridCoord
  CornerPlacement struct: worldPosition, rotation, kind, tier, gridCoord

  SolveResult class: List<FloorPlacement> floors, List<WallPlacement> walls,
    List<CornerPlacement> corners, List<string> warnings.
    Constructor initialises all lists. ToString() returns count summary:
    "Solve: N floors, N walls, N corners, N warnings"

### Public API
  EdgeSolver.Solve(CellMap map) → SolveResult
  Never null. Null or empty map returns empty lists + 1 warning. Does not throw.

### Passes
  Pass 1 — Floors: one FloorPlacement per filled Square cell (tier 0..N).
    Non-Square tile types → warn once per type per Solve call, skip cell.
  Pass 2 — Walls: one WallPlacement per filled Square/tier-0 cell edge where
    HasWallOnEdge is true (exterior edge or step-down to lower tier).
  Pass 3 — Corners: one CornerPlacement per outward 90° vertex junction.
    For each filled Square/tier-0 cell, all four corner vertices are checked
    (NE, NW, SE, SW). A vertex is emitted when both adjacent edge walls are
    present AND the diagonal cell across the vertex is empty.
    Deduplication via HashSet<long> of packed vertex grid positions so each
    world vertex is emitted at most once, even when shared by multiple cells.

### Wall rotation convention
  Local +Z points INTO the room (toward the cell interior) from each edge:
    North edge → Euler(0,180,0)   East edge  → Euler(0,270,0)
    South edge → Euler(0,  0,0)   West edge  → Euler(0, 90,0)

### Corner rotation convention
  Local +Z bisects the two wall faces and points into the room interior:
    NE outward corner (wallN+wallE) → Euler(0,225,0)   bisector=SW
    NW outward corner (wallN+wallW) → Euler(0,135,0)   bisector=SE
    SE outward corner (wallS+wallE) → Euler(0,315,0)   bisector=NW
    SW outward corner (wallS+wallW) → Euler(0, 45,0)   bisector=NE

### Expected output for Rectangle(5,3)
  floors=15, walls=16, corners=4, warnings=0
  First floor:  (-8, 0, -4) at grid (0,0)
  First wall:   (-8, 0, -6) at grid (0,0) edge South
  First corner: (-10, 0, -6) at grid (0,0)   ← SW outer corner

### Current scope
  Square cells and tier 0 only. The following are deferred (not emitted):
  - Triangle* and Quarter* tiles (warn + skip)
  - Tier > 0 cells in wall and corner passes
  - Inward (concave) corners for L-shapes and notches
  - Diagonal wall segments

### LevelEditor/Tests/Dump EdgeSolver Results menu item
  Source: Assets/Scripts/LevelEditor/Editor/EdgeSolver_Test.cs
  Builds Rectangle(5,3), runs EdgeSolver.Solve, logs the ToString() summary,
  any warnings, and the first entry from each placement list with coordinates.

### EdgeSolverGizmoPreview — scene-view visualiser

  Source: Assets/Scripts/LevelEditor/EdgeSolverGizmoPreview.cs
  MonoBehaviour (NOT editor-only). Add to any scene object, then select it to
  see the EdgeSolver output as Gizmos. Uses #if UNITY_EDITOR guards on all
  Handles.Label calls so the file compiles cleanly for mobile builds.

  Inspector fields:
    rectangleWidth / rectangleDepth — live-regenerates the preview on change
    drawFloors  — blue semi-transparent cubes (alpha 0.35)
    drawWalls   — yellow wire boxes, green +Z arrow pointing INWARD
    drawCorners — red wire pillars, orange +Z bisector arrow pointing INWARD
    drawLabels  — Handles.Label grid-coord text above each placement
    arrowLength — length of the directional arrows (default 1.5)

  Visual output for Rectangle(5,3):
    15 blue floor squares in a 5×3 grid
    16 yellow wall boxes around the perimeter; green arrows all point TOWARD
      the room interior (verify: south-face wall arrow → north, etc.)
    4 red corner pillars at the four outer vertices; orange arrows point
      diagonally toward the room center (SW corner → NE arrow, etc.)
    White wire sphere at transform.position marks the solver-space origin

  Create via: LevelEditor → Tests → Create Gizmo Preview in Scene
    Creates "EdgeSolver Gizmo Preview" at world origin with the component
    attached, or re-selects the existing one if already in the scene.