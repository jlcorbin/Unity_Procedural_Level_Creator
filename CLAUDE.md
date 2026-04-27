# LevelGen — Claude Code Project Brief

## Version
V2 is the canonical and only active architecture. V1 was retired on 2026-04-26
via the cleanup tracked in `Documentation/V1_CLEANUP_AUDIT.md`. The pre-cleanup
state is recoverable from git history (HEAD a929e1d on branch v2).

## What this is
A Unity 6.4 mobile game project using URP and pure C#.
No Blueprints, no visual scripting.
Renderer: URP (Universal Render Pipeline).
Target platforms: Android (IL2CPP, ARM64) and iOS.

## Project dependencies (Unity packages)
Notable additions beyond the URP/InputSystem baseline:
- `com.unity.cinemachine` 3.1.6 — added 2026-04-27 for M2-A player camera follow.
  Note: Cinemachine 3.x namespace is `Unity.Cinemachine` (not `Cinemachine` as in 2.x).

## Level generator overview
The V2 generator places saved RoomPiece + Hall prefabs into a `GeneratedLevel`
root in the active scene, then optionally saves the result to a user-chosen
`.unity` file with a sibling manifest. EditorWindow: `LevelGen → V2 Level
Generator`.

Key rules:
- Each prefab has a RoomPiece component and child ExitPoint components
- ExitPoints have a direction (North/South/East/West/Up/Down)
- Two exits connect only if their directions are opposite (after rotation)
- Collision is an AABB check via `RoomPiece.GetWorldBounds()` plus a
  rotation-aware X/Z swap on 90°/270° turns (the bounds field itself is
  not rotation-aware — see V2_LevelGenerator_DesignSpec.md)
- Generation uses seeded `System.Random` — same seed = identical level
- After every `PrefabUtility.InstantiatePrefab`, `RoomPiece.RefreshExits()`
  is called to populate the cached exit list (Awake doesn't fire in edit mode)

Generation flow:
1. Place Starter at world origin
2. Build a linear spine of rooms drawn at random from a single combined
   `Small + Medium + Large + Special` pool, weighted by remaining counts
3. Append Boss
4. Attach branches to random rooms with unused exits (including earlier
   branches), drawing from the same pool. Branch failures degrade
   gracefully — the slot is skipped with a warning, generation continues
5. Backtracking cap = 50 spine attempts; branches don't count
6. **Save Generated Level** button (separate from Generate) opens a
   save-as dialog; the chosen path determines where the `.unity` and
   `_manifest.txt` are written (all-or-nothing)

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
│   ├── Editor/
│   │   ├── LVL_Configurator.cs        (complete — do not touch)
│   │   ├── PieceCatalogueEditor.cs
│   │   └── WhiteboxPackFactory.cs     (pipeline complete)
│   ├── Experimental/                  (#if FALSE — dormant)
│   │   ├── README.md
│   │   └── ShapeStamp_Shapes.cs       (Diamond + Circle)
│   ├── Generation/
│   │   ├── ExitPoint.cs
│   │   └── RoomPiece.cs               (rotation-aware gizmo via Gizmos.matrix)
│   ├── LevelEditor/
│   │   ├── CellMap.cs                 (HasWallOnEdge + AddDoorway)
│   │   ├── EdgeSolver.cs
│   │   ├── EdgeSolverGizmoPreview.cs  (V2 diagnostic)
│   │   ├── RoomBuilder.cs             (V2 cell-map RoomBuilder)
│   │   ├── RoomPieceClassification.cs
│   │   ├── ShapeStamp.cs              (Rectangle only)
│   │   ├── TileType.cs
│   │   └── Editor/
│   │       └── (Doorway_Test, EdgeSolver_Test, RoomBuilderEditor,
│   │            RoomPiece_Test, ShapeStamp_Test, V2_SampleThemeBuilder)
│   ├── LevelGen/V2/
│   │   ├── LevelGenSettings.cs
│   │   └── Editor/
│   │       ├── V2LevelGeneratorWindow.cs
│   │       ├── V2LevelGenerator.cs
│   │       └── V2PrefabSource.cs
│   └── Workshop/
│       └── PieceCatalogue.cs
├── Prefabs/
│   ├── Rooms/         (Starter / Boss / Small / Medium / Large / Special / Curated)
│   └── Halls/         (Small / Medium / Large / Special)
├── Levels/
│   └── Generated/     (V2-saved .unity scenes + manifests)
└── Scenes/
    ├── SampleScene.unity
    ├── RoomWorkshop.unity     (empty placeholder; populate next session)
    └── LevelGenerator.unity   (empty placeholder; populate next session)

## Namespace
All scripts use namespace LevelGen.

## Key design decisions
- `System.Random` (not `UnityEngine.Random`) for deterministic seeds
- Bounds overlap uses AABB list check (not physics)
- All wall-emission decisions flow through a single `CellMap.HasWallOnEdge(x, z, edge)` method — **no per-side branching anywhere in placement code** (architectural invariant)
- Categories, rotations, and exits are iterated by table/loop, never per-side if/else chains
- Editor windows work in Edit mode (no Play mode required for V2 generator)
- V2 placement is editor-time only via `PrefabUtility.InstantiatePrefab`

## Coding conventions
- XML doc comments on all public members
- [Tooltip(...)] on all inspector fields
- Gizmos for spatial debugging
- #if UNITY_EDITOR guards on editor-only code
- No magic numbers — named constants or inspector fields

## Three-scene pipeline
1. `RoomWorkshop.unity` — build and curate individual rooms (placeholder, not yet populated)
2. `LevelGenerator.unity` — assemble levels from room prefabs (placeholder, not yet populated)
3. Level_XX.unity — baked gameplay scenes generated by the V2 Level Generator into `Assets/Levels/Generated/`

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

## Scripts status (post V1-cleanup, 2026-04-26)

Generation/:
  ExitPoint.cs ✓
  RoomPiece.cs ✓ (gizmo rotation-aware via Gizmos.matrix)

Workshop/:
  PieceCatalogue.cs ✓ (PieceType.None = 99 staging slot)

LevelEditor/:
  CellMap.cs ✓ (V2 cell-grid with HasWallOnEdge + AddDoorway)
  TileType.cs ✓ (TileType + TileTypeInfo lookup)
  ShapeStamp.cs ✓ (Rectangle only; Diamond/Circle moved to Experimental)
  EdgeSolver.cs ✓
  EdgeSolverGizmoPreview.cs ✓ (V2 diagnostic, JC confirmed KEEP)
  RoomBuilder.cs ✓ (V2 cell-map RoomBuilder)
  RoomPieceClassification.cs ✓ (PieceType / RoomCategory / HallCategory enums)

LevelEditor/Editor/:
  Doorway_Test.cs, EdgeSolver_Test.cs, RoomBuilderEditor.cs,
  RoomPiece_Test.cs, ShapeStamp_Test.cs, V2_SampleThemeBuilder.cs ✓

LevelGen/V2/:
  LevelGenSettings.cs ✓
LevelGen/V2/Editor/:
  V2LevelGeneratorWindow.cs, V2LevelGenerator.cs, V2PrefabSource.cs ✓

Editor/:
  PieceCatalogueEditor.cs ✓
  WhiteboxPackFactory.cs ✓ (pipeline complete; menu under [Complete] submenu)
  LVL_Configurator.cs ✓ (complete — do not touch)

Experimental/:
  ShapeStamp_Shapes.cs (Diamond + Circle, #if FALSE)
  README.md

Player (M1 + M2-C + M2-A COMPLETE — see Documentation/Player_Animator_Design_2026-04-26.md):
  M1 (idle + walk locomotion): all assets shipped & verified
    PlayerBaseController.controller ✓ (4 params, 3 states, 6 transitions post-Sprint)
    PlayerOverride_MaleHero.overrideController ✓ (6 slots post-Sprint)
    PlayerInputReader.cs, PlayerController.cs, PlayerAnimator.cs, PlayerSpawner.cs ✓
    Player_MaleHero.prefab ✓ (9 persistent UnityEvent listeners + CameraTarget child at local (0,1.6,0))
    Test scene: Assets/Scenes/Test/Player_M1_Test.unity ✓
    Acceptance: Documentation/Player_M1_Acceptance_2026-04-26.md
    Builder: Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs
             (LevelGen ▶ Player ▶ Build Player_MaleHero Prefab / Create M1 Test Scene
              / Add CameraTarget to Player_MaleHero Prefab
              / Add Cinemachine Follow Camera to Active Scene)
  M2-C (sprint state): Animator state added, scripts extended ✓ (visual verification pending in Player_M1_Test.unity)
    IsSprinting Bool parameter, Sprint state with SprintFWD_Battle_InPlace clip
    Locomotion → Sprint when (IsSprinting && MoveZ>0.7 && Speed>0.1), 0.10s blend
    Three Sprint → Locomotion transitions (IsSprinting==false / MoveZ<0.7 / Speed<0.1), 0.15s blend
    sprintMultiplier=1.75 in PlayerController (3.5 m/s effective at default 2.0 walk)
    Hold-to-sprint (ctx.ReadValueAsButton), state-speed-bound to Speed parameter
  M2-A (camera follow): Cinemachine 3.x installed, behind-the-back follow camera in
    Player_M1_Test.unity, deoccluder enabled, Look input wired, Y-inverted by default
    ✓ (visual verification pending in test scene)
    Component combo (canonical, post-08-A-2 revert):
                     CinemachineCamera + CinemachineOrbitalFollow (Sphere, R=4) +
                     CinemachineRotationComposer + CinemachineDeoccluder (Min=1) +
                     CinemachineInputAxisController (yaw/pitch only; radial unwired)
    M2-A camera fix history (2026-04-27): 08-A removed RotationComposer on a wrong
                     diagnosis (claimed it overrode OrbitalFollow input-aim);
                     08-A-2 restored it after empirical evidence that
                     OrbitalFollow runs Body-stage only and needs an Aim-stage
                     component for rotation. Body+Aim are complementary in CM 3.x.
                     Reader Gain final tune: ±10 (per Jason's playtest preference;
                     iterated 0.2 → 1.0 → 10). At ±10, mouse / right-stick movement
                     produces snappy responsive camera orbit. Locked into both the
                     test scene's vcam and PlayerPrefabBuilder.cs camera setup.
  M2 strafe locomotion (2026-04-27): switched from rotate-to-face to strafe with
    snap body alignment. Body yaw locked to camera yaw every frame. SetMove now
    writes (input.x, input.y) so blend tree exercises all 4 corners. Sprint still
    gated to forward-mostly input (MoveZ > 0.7). PlayerController.cs steps 7+8
    rewritten; RotateTowardsMoveDir → SnapBodyToCameraYaw. rotationSpeed inspector
    field unused under snap model — kept to avoid prefab churn, removable in cleanup.
    Documentation/Player_Animator_Design_2026-04-26.md gained "Design Course
    Correction — 2026-04-27" section.
    Verification: L1–L8 in Documentation/Player_M1_Acceptance_2026-04-26.md.
  MoveRGT repair (2026-04-27): two-stage fix. The FINAL root cause was that
    the MoveRGT FBX's ModelImporter was set to Generic rig
    (animationType=2, avatarSetup=NoAvatar, sourceAvatar=null) instead of
    Humanoid like every other clip in the pack. Generic-rig clips can't
    retarget to the player's Humanoid avatar — pressing D played the clip
    but produced a "bunched up ball" pose because the bone tracks didn't
    map to the Humanoid skeleton. The "Missing (Motion)" Inspector
    symptom + empty internalIDToNameTable + stale fileID were all
    DOWNSTREAM consequences of the wrong rig type, not separate bugs.
    Fix: copy ModelImporter settings from MoveLFT to MoveRGT (animationType
    Human, avatarSetup CopyFromOther, sourceAvatar = shared Humanoid
    Avatar), SaveAndReimport, then rewrite blend tree (1,0) with the new
    post-reimport fileID. Override controller auto-redivved cleanly to
    6 slots.
    Lessons:
    - When a clip plays but produces a wrong pose on a Humanoid character,
      check FBX animationType FIRST. Generic vs Humanoid retargeting is
      the most common cause.
    - Inspector resolution ≠ correct retargeting. A motion reference can
      look fine in the controller while playing as Generic on a Humanoid
      rig (broken result, no error logged).
    - When repairing one FBX's import settings, copy from a known-good
      sibling. Don't trust reimport-with-defaults to re-derive correct
      settings — whatever's already in the .meta is preserved.
    See Documentation/Player_M1_Acceptance_2026-04-26.md "MoveRGT Repair —
    Correction #2" for the full diagnostic history including dead ends.
    HorizontalAxis range (-180,180) wrap; VerticalAxis range (-10,70) initial 15°
    Substituted CinemachineOrbitalFollow for the design doc's prompted CinemachineFollow
    because Follow doesn't expose IInputAxisOwner axes — InputAxisController has nothing
    to drive on Follow+RotationComposer alone. OrbitalFollow exposes HorizontalAxis +
    VerticalAxis + RadialAxis; we wire only yaw + pitch and defensively null the
    radial Reader.Input (CM 3.1.6 auto-populates it with Player/Look on AddComponent).
    Player scripts byte-identical post-M2-A — Cinemachine drives Camera.main.transform,
    which PlayerController.BuildCameraRelativeMove already projects from.
  M2-D (level integration): pending — refactor to Player_RuntimeRig composite prefab
        during this milestone
  M2-B (combat: jump, attack, hit): pending

V1 retired: BoundsChecker, V1 LevelGenerator (runtime), SeedData,
LevelSequence, RoomDefinition, V1 RoomBuilder (COMP_-based),
PropEntry, PropCatalogue, SpawnPoint, RoomContentGenerator, RoomPreset,
RoomPresetLibrary, LevelGeneratorEditor, RoomWorkshopWindow,
PlaceholderPrefabFactory, LevelGenSetup. All recoverable from git
history pre-cleanup.

## Next CC task (run this when resuming)
Read CLAUDE.md fully.

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

V2 generator is on a stable checkpoint and not under active development.
Returning to Room Workshop next session — items below in priority order:

  1. Openings/doorway workflow (V1 failure point — primary V2
     Room Workshop focus)
  2. Tier stacking
  3. Room connection logic — door geometry vs. open passages
  4. Player integration — M1 + M2-C + M2-A COMPLETE.
     M2 remaining: level integration (M2-D), combat (M2-B: jump, attack, hit).
  5. Test DoSave end-to-end (step ⑥) — both Room and Hall paths
  6. Implement Dress step (PropCatalogue / SpawnPoints)
  7. Whitebox `PieceCatalogue` wiring + `LVL_Configurator` end-to-end
  8. ExitPoint misalignment on non-straight LVL modules (Option A:
     geometry scanning via `DetectExitPosition`)
  9. Create RoomWorkshop.unity scene
 10. Create LevelGenerator.unity scene
 11. Diamond / Circle room shapes (deferred indefinitely)

Within V2 generator, deferred for later (post-Room-Workshop):
  - Theme-aware prefab selection (currently logged-only)
  - Difficulty-signal influence on category pick (currently logged-only)
  - Layout styles beyond Linear-with-branches (Grid / Organic /
    Corridor stubs)
  - Player spawn / boss trigger / save-point objects in saved scenes
  - Multi-floor stacking (one .unity = one floor for now)

Menu cleanup (2026-04-25):
  - Renamed `LevelGen/Whitebox/` submenu to `LevelGen/Whitebox [Complete]/`
    to signal pipeline is finished. Items unchanged.
  - Consolidated Doorway tests: removed `Manual 5x3 with 2 Doorways` and
    `doorCount=2 equivalent on 5x3` test methods. Kept `Combined paths
    on 5x3` as the single Doorway test entry point.

V2 Level Generator (2026-04-25):
  - Phase A complete: LevelGenSettings data class and V2LevelGeneratorWindow
    EditorWindow with all params and validation. MenuItem at
    `LevelGen/V2 Level Generator`. Generate click logs settings; placement
    logic deferred to Phase B.
  - V1 audit confirmed no placement engine existed — engine built from scratch.
  - Phase B complete: V2LevelGenerator + V2PrefabSource. Spine-only generator
    places Starter at world origin, walks down a linear spine of rooms
    (random pick from remaining Small/Medium/Large budget) connected by
    spine-size halls, ends with Boss. Backtracking cap = 50.
    Uses `System.Random`, `PrefabUtility.InstantiatePrefab`, and
    `RoomPiece.RefreshExits()` after every spawn (Awake-bypass-in-edit-mode
    bridge from the audit). Collision uses a rotation-aware AABB helper
    that swaps X/Z extents on 90°/270° turns. Branches, theme-aware
    selection, scene save, and manifest output deferred to Phase C/D.
  - RoomPiece gizmo fixed: `OnDrawGizmos()` now uses `Gizmos.matrix` so
    the bounds box rotates with the GameObject. Previously axis-aligned —
    misled debugging for non-square rooms (e.g. Medium_3x8) at Y=±90°.
    `boundsOffset` is now interpreted as a local-space offset; safe for
    all current prefabs because every authored offset is `(0, Y, 0)` and
    Y is invariant under Y rotation.
  - Phase C (branches) complete: spine and branches now both draw from a
    single combined Small+Medium+Large+Special pool, weighted by remaining
    counts. SpineLength = max(0, S+M+L+Special − branchSlotCount); Starter
    and Boss are not in the pool. After spine+Boss placement, branches
    attach to random rooms with unused exits (including earlier branches),
    using the user's `branchHallSize` for connector halls. Branch failures
    degrade gracefully — the slot is skipped with a console warning, no
    abort. EditorWindow validation rule changed from
    `branches > SpineLength-1` to `branches > pool` (the old rule became
    self-contradicting under the new SpineLength formula). Connect-with-
    hall code extracted into a shared `TryPlaceConnectedRoom` helper used
    by spine, Boss, and branches. Theme-aware prefab selection still
    deferred.
  - Phase D (scene save + manifest) complete: new `saveToSceneFile`
    setting (default ON). When ON, generation creates a fresh additive
    scene with Main Camera + Directional Light, generates directly into
    it, frames the camera over the dungeon, saves to
    `{outputFolder}/{sceneName}.unity`, and closes the scene — leaving
    the user's active scene untouched. Overwrite dialog gates re-saves;
    cancellation falls back to active-scene mode. When OFF, behaves
    exactly like Phase B/C (root in active scene). A `_manifest.txt` is
    always written next to the scene (or with a `Dungeon_<seed>` fallback
    name when sceneName is empty) — contains seed, all input params,
    placement order with prefab/position/rotation, and run stats. New
    `PlacementRecord` class tracks placements during generation; on
    backtrack the last 2 records (hall + room) are popped in lockstep
    with the placement stack. Validation now gates `sceneName` /
    `outputFolder` requirements on `saveToSceneFile == true`.
  - CS0104 ambiguity fix: 9 calls to `Object.DestroyImmediate(...)` in
    `V2LevelGenerator.cs` fully qualified to `UnityEngine.Object.
    DestroyImmediate(...)`. Conflict was introduced by `using System;`
    in Phase D (added for `DateTime.UtcNow` in the manifest header),
    which made bare `Object` ambiguous between `UnityEngine.Object` and
    `System.Object`.
  - Save refactor (replaces Phase D auto-save): the EditorWindow no
    longer has Output / Scene Name / Output Folder / Save-to-scene-file
    fields. Generate places `GeneratedLevel` in the active scene and
    stops. A new `Save Generated Level` button below Generate opens
    `EditorUtility.SaveFilePanelInProject` anchored at
    `Assets/Levels/Generated/` with default name `Dungeon_<seed>` —
    user picks any path under `Assets/`, can create new folders in the
    dialog. The chosen path's directory becomes `outputFolder`,
    filename-without-extension becomes `sceneName`, and the manifest
    writes alongside as `{sceneName}_manifest.txt`. Cancellation
    aborts both the scene write and the manifest (all-or-nothing).
    `LevelGenSettings.saveToSceneFile` removed; `sceneName` and
    `outputFolder` marked `[NonSerialized]`. New public types
    `SaveOutcome` and static `LastPlacements` on V2LevelGenerator;
    `EnsureAssetFolder` promoted to public so the window can pre-
    create the dialog's anchor folder.
  - SaveLevelToScene helper note: the `EditorSceneManager.
    SaveCurrentModifiedScenesIfUserWantsTo()` call from Phase D was
    removed in the save-refactor. In the new flow the active scene
    has been modified by the Generate click; prompting the user about
    those modifications mid-Save would let "Don't Save" revert the
    active scene, destroying our root before `MoveGameObjectToScene`
    can run.
  - CS0618 fix in `V2_SampleThemeBuilder.cs`: `FindFirstObjectByType`
    swapped to `FindAnyObjectByType` (Unity 6.4 deprecation).
  - CS0426 fix in `V2LevelGeneratorWindow.cs`: line 17 referenced
    `V2LevelGenerator.GenerationResult` as a nested type, but
    `GenerationResult`, `SaveOutcome`, and `PlacementRecord` are all
    top-level inside `LevelGen.V2`. Window now uses bare names
    (resolved via the enclosing-namespace lookup since the window's
    own namespace is `LevelGen.V2.Editor`). The compile error was
    masking the entire save-refactor — Unity was falling back to the
    Phase D auto-save assembly until this was resolved.
  - New: Assets/Scripts/LevelGen/V2/LevelGenSettings.cs
         Assets/Scripts/LevelGen/V2/Editor/V2LevelGeneratorWindow.cs
         Assets/Scripts/LevelGen/V2/Editor/V2PrefabSource.cs
         Assets/Scripts/LevelGen/V2/Editor/V2LevelGenerator.cs

## Cell-map room model — Phase 1 foundation

Three files in Assets/Scripts/LevelEditor/ form the Phase 1 foundation:

  TileType.cs   — TileType enum (Empty, Square, Triangle*, Quarter*, …) +
                  TileTypeInfo static lookup (edge occupancy, rotation helpers)
  CellMap.cs    — 2D grid of Cell structs; fixed-size, serializable.
                  Cell = (TileType, tier, rotSteps). CellSize = 4 units (matches
                  old FloorStep). ToAscii() for debug dumps.
  ShapeStamp.cs — Static utility that stamps pre-populated CellMaps for common
                  geometric shapes. Floor cells only — no wall, corner, or
                  prefab logic. Contains Rectangle() in the live tree;
                  Diamond() and Circle() were moved to
                  Assets/Scripts/Experimental/ShapeStamp_Shapes.cs behind
                  #if FALSE on 2026-04-26 (V1 cleanup). Live class is now
                  declared `partial` so the experimental partial folds back
                  in cleanly when revived.

### ShapeStamp methods

All methods return a new CellMap with cells at tier 0, rotSteps 0 unless noted.
Invalid inputs are clamped (not thrown) and logged via Debug.LogWarning.

  Rectangle(int width, int depth)
    Fills every cell with TileType.Square. Grid is exactly width × depth.
    Clamps: width and depth to min 1.

  Diamond(int size) and Circle(int radius)
    DORMANT — moved to Assets/Scripts/Experimental/ShapeStamp_Shapes.cs
    behind #if FALSE on 2026-04-26 (V1 cleanup). Reviving requires also
    extending EdgeSolver and RoomBuilder to handle Triangle / Quarter tile
    types in their wall and corner passes, which they do not today.

### LevelEditor/Tests menu item

  LevelEditor → Tests → Dump Shape Stamps to Console
  Source: Assets/Scripts/LevelEditor/Editor/ShapeStamp_Test.cs
  Generates Rectangle(5,3), calls ToAscii() on it, and logs the result.
  Smoke test only — confirms the active V2 shape runs without exception.

## Cell-map room model — Phase 2: EdgeSolver

Source: Assets/Scripts/LevelEditor/EdgeSolver.cs

### Purpose
EdgeSolver walks a CellMap and produces three ordered placement lists that
form the complete instruction set for the room builder. It is pure data:
  in  = CellMap
  out = SolveResult (floors, walls, corners, warnings)

No prefab references, no catalogue reads, no scene access.

### Types defined in EdgeSolver.cs (namespace LevelEditor)

  WallKind enum: Straight / HalfL / HalfR / Angle (emitted), Concave/Convex (deferred)
    HalfL = half-wall whose mesh extends to the LEFT of its pivot (local -X side)
    HalfR = half-wall whose mesh extends to the RIGHT of its pivot (local +X side)
    Both HalfL and HalfR are placed at the normal edge-midpoint position (no offset).
    The _L/_R prefab itself handles the visual shift via its mesh authoring.
    Angle = diagonal hypotenuse wall for Triangle tiles; placed at cell center.
  CornerKind enum: Outward (emitted), Inward/Diagonal (deferred)
  CornerArmLength enum: Full / Half / Column
    Full   — corner arms are 4 units; fully replaces the two adjacent walls
    Half   — corner arms are 2 units; adjacent walls replaced with HalfL or HalfR
             variants. Requires map at least 3×3; smaller maps return empty + warning.
    Column — no arms (decorative); adjacent walls remain full

  FloorPlacement struct: worldPosition, rotation, tileType, tier, gridCoord
  WallPlacement struct:  worldPosition, rotation, kind, tier, edge, gridCoord
  CornerPlacement struct: worldPosition, rotation, kind, tier, gridCoord

  SolveResult class: List<FloorPlacement> floors, List<WallPlacement> walls,
    List<CornerPlacement> corners, List<string> warnings.
    Constructor initialises all lists. ToString() returns count summary:
    "Solve: N floors, N walls, N corners, N warnings"

### Public API
  EdgeSolver.Solve(CellMap map, CornerArmLength cornerArms = CornerArmLength.Full) → SolveResult
  Never null. Null or empty map returns empty lists + 1 warning. Does not throw.

### Supported tile types
  Square only. All other tile types warn once per Solve call and are skipped.

### Passes (corners run before walls)
  Pass 1 — Floors: one FloorPlacement per filled Square cell (tier 0..N).
    tileType preserved in FloorPlacement. Non-Square cells warn and are skipped.
  Pass 2 — Corners (runs FIRST): one CornerPlacement per outward 90° vertex junction.
    Square tier-0 cells only. For each filled cell, all four corner vertices are
    checked (NE, NW, SE, SW). A vertex is emitted when both adjacent edge walls are
    present AND the diagonal cell across the vertex is empty.
    Deduplication via HashSet<long> of packed vertex grid positions.
    Claim sets populated during corner pass:
      fullyClaimedEdges — edges fully suppressed (Full mode)
      halfCornerEdges   — Dictionary<long, EdgeEndpoint> recording which endpoint
                          of the edge has a Half corner arm (Start = −X/−Z, End = +X/+Z)
  Pass 3 — Cardinal walls (runs AFTER corners, consults claim sets):
    Square tier-0 cells only.
    For each edge where HasWallOnEdge is true:
      - If fullyClaimedEdges contains the edge → emit nothing
      - If halfCornerEdges contains the edge → emit HalfL or HalfR at edge midpoint
      - Otherwise → emit a full Straight wall at edge midpoint

### Half-wall L/R decision rule
  HalfKindForCornerEnd(edge, cornerEnd) maps the corner endpoint to HalfL or HalfR:
    Corner at wall's local +X end → HalfL (mesh extends -X, filling toward the corner)
    Corner at wall's local -X end → HalfR (mesh extends +X, filling toward the corner)
  Local +X orientation per edge (wall rotation → local +X direction):
    South (0°)  → +X = east  = End   → End   → HalfL
    East  (270°)→ +X = north = End   → End   → HalfL
    North (180°)→ +X = west  = Start → Start → HalfL
    West  (90°) → +X = south = Start → Start → HalfL

### Claim endpoint per vertex
  NE vertex (x,z): N edge End,   E edge End
  NW vertex (x,z): N edge Start, W edge End
  SE vertex (x,z): S edge End,   E edge Start
  SW vertex (x,z): S edge Start, W edge Start
  (Start = −X or −Z end of the edge; End = +X or +Z end)

### Wall rotation convention
  Local +Z points INTO the room (toward the cell interior) from each edge:
    North edge → Euler(0,180,0)   East edge  → Euler(0,270,0)
    South edge → Euler(0,  0,0)   West edge  → Euler(0, 90,0)

### Corner rotation convention
  FDP convention: pivot at inner elbow of the L-shape. At rotation 0, arms extend
  toward -X (west) and +Z (north). Rotating CW by N*90° re-aligns the arms to each
  room corner's two walls, pointing INTO the room. Confirmed empirically 2025-04-20.

  | Room corner | Arms point into room      | Rotation       |
  |-------------|---------------------------|----------------|
  | SE          | west (-X) and north (+Z)  | Euler(0,  0,0) |
  | SW          | north (+Z) and east (+X)  | Euler(0, 90,0) |
  | NW          | east (+X) and south (-Z)  | Euler(0,180,0) |
  | NE          | south (-Z) and west (-X)  | Euler(0,270,0) |

### Expected output for Rectangle(5,3)
  CornerArmLength.Full   : floors=15, walls=8,  corners=4, warnings=0
  CornerArmLength.Half   : floors=15, walls=16, corners=4, warnings=0  (8 Straight + 8 Half)
  CornerArmLength.Column : floors=15, walls=16, corners=4, warnings=0  (all Straight)
  First floor:  (-8, 0, -4) at grid (0,0)
  First wall:   (-8, 0, -6) at grid (0,0) edge South
  First corner: (-10, 0, -6) at grid (0,0)   ← SW outer corner

### Current scope
  Square cells only, tier 0. The following are deferred (not emitted):
  - All non-Square tile types (warn + skip)
  - Tier > 0 cells in wall and corner passes
  - Inward (concave) corners for L-shapes and notches
  - Custom shapes (Diamond/Circle) and their Triangle/Angle/Quarter tile support
    (removed 2026-04-21 to focus on rectangle rooms; ShapeStamp still contains
    Diamond() and Circle() as scaffolding for future work)

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
    rectangleWidth  — cells wide (default 5)
    rectangleDepth  — cells deep (default 3)
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

## Cell-map room model — Phase 3: RoomBuilder

Source: Assets/Scripts/LevelEditor/RoomBuilder.cs
Custom editor: Assets/Scripts/LevelEditor/Editor/RoomBuilderEditor.cs
Menu item added to: Assets/Scripts/LevelEditor/Editor/EdgeSolver_Test.cs

### Purpose
MonoBehaviour that turns a SolveResult from EdgeSolver into real scene geometry.
Pure geometry pass — no catalogue, no RoomPiece, no ExitPoints.

### Inspector fields

  [Header("Shape")]
    rectangleWidth  — room width in cells (min 1, default 5)
    rectangleDepth  — room depth in cells (min 1, default 3)

  [Header("Prefabs")]
    floorPrefab     — prefab used for every Square floor cell
    wallPrefab      — prefab used for every straight wall segment
    cornerPrefab    — prefab used for every outward corner
    halfWallLPrefab — half-wall with mesh extending LEFT of pivot (_L variant).
                      Used when a Half corner sits at the wall's right (local +X) end.
    halfWallRPrefab — half-wall with mesh extending RIGHT of pivot (_R variant).
                      Used when a Half corner sits at the wall's left (local -X) end.
                      Both slots required in Half mode; Build aborts if either is null.
    wallPivot       — WallPivotPosition enum: where the wall prefab's pivot sits along
                      its local X axis. Center = no shift. StartX = pivot at -X end,
                      mesh extends +X (default). EndX = pivot at +X end.
    floorPivot      — FloorPivotPosition enum: where the floor prefab's pivot sits
                      relative to its tile footprint. Center = no shift.
                      PivotNW/NE/SW/SE = corner pivots (default PivotNW).
    cornerArmLength — CornerArmLength enum (Full / Half / Column, default Full).
                      Full: corner arms 4 units, suppresses the 2 adjacent walls.
                      Half: corner arms 2 units, replaces adjacent walls with HalfL/HalfR.
                      Column: decorative corner, adjacent walls remain full.

  wallPivot shift is rotated by each wall's quaternion (follows wall's local X).
  HalfL pivot is ALWAYS EndX-equivalent (−2 on local X), hardcoded in WallPivotShift —
    _L prefabs have mirror authoring vs. _R: pivot at +X end, mesh extends -X.
    This override fires regardless of the wallPivot inspector setting.
  HalfR and Straight walls use the wallPivot field normally.
  Floor pivot shift applied in world space (identity rotation at tier 0).
  Corners have no pivot shift.

  [Header("Output")]
    rootName — name of the root GameObject created by Build (default "MOD_Room")

### Current working values (default prefab library)
  wallPivot        = StartX   — Straight and HalfR: pivot at -X end, mesh extends +X
  floorPivot       = PivotNW  — _E_ floors: pivot at NW corner, mesh extends +X and -Z
  cornerArmLength  = Full     — default; adjust to match actual corner prefab arm length
  HalfL override   = always EndX (−2 local X), hardcoded — not affected by wallPivot

### Per-mode expected placement counts for Rectangle(5,3)
  Full   : 15 floors,  8 walls (all Straight),       4 corners
  Half   : 15 floors, 16 walls (8 Straight + 8 Half), 4 corners
  Column : 15 floors, 16 walls (all Straight),        4 corners

### Public methods

  Build()
    Guards: floorPrefab + wallPrefab + cornerPrefab required (aborts with error if any null).
    Half mode additionally requires halfWallLPrefab + halfWallRPrefab (aborts with error if either null).
    Destroys previous root by name (Undo-safe in editor).
    Builds CellMap via switch on shape: Rectangle → ShapeStamp.Rectangle(rectangleWidth, rectangleDepth);
    Diamond → ShapeStamp.Diamond(shapeSize); Circle → ShapeStamp.Circle(shapeSize).
    Runs EdgeSolver.Solve(map, cornerArmLength). Logs all solver warnings.
    Creates root at world origin (not at component's transform).
    Three child grouping GameObjects: Floors / Walls / Corners.
    Instantiates each placement under its group via PrefabUtility.InstantiatePrefab
    (editor) or plain Instantiate (runtime). Registers Undo for all created objects.
    Wall naming: Straight → Wall_{x}_{z}_{edge}; Half → Wall_{x}_{z}_{edge}_{kind}.
    Corner naming: Corner_{x}_{z}. Floor naming: Floor_{x}_{z}.
    Logs summary: "[RoomBuilder] Built N floors, N walls, N corners under 'MOD_Room'."

  Clear()
    Finds root by name; destroys it (Undo-safe). Logs what was removed or that
    nothing was found.

### RoomBuilderEditor
  [CustomEditor(typeof(RoomBuilder))]
  Draws default inspector, then two action buttons (height 30):
    [ Build ]  — calls Build(), wrapped in Undo.RecordObject + SetDirty
    [ Clear ]  — calls Clear(), wrapped in Undo.RecordObject + SetDirty

### Menu item
  LevelEditor → Tests → Create RoomBuilder in Scene
    Looks for existing "RoomBuilder" GameObject; if found, selects and returns.
    Otherwise creates an empty "RoomBuilder" at origin, adds RoomBuilder component,
    selects it. Prefabs must be assigned by hand via the inspector.

### Current scope
  Rectangle shape / tier 0 / one prefab per category (floor, wall, corner, halfWallL, halfWallR).

### Deferred work
  - Catalogue-based prefab selection (per-tile-type prefab pools), replacing halfWallLPrefab/halfWallRPrefab slots
  - Per-tile-type variant selection (triangle floors, angle/concave/convex walls)
  - Tier stacking (tiers 1 and 2)
  - RoomPiece bounds stamping
  - ExitPoint placement (door workflow)
  - Inward (concave) corners for L-shapes and notches