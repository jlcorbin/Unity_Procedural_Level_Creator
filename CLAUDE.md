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
│   ├── Character Prefabs/
│   │   └── Player/    (Player_MaleHero.prefab)
│   └── Level Prefabs/
│       ├── Rooms/     (Starter / Boss / Small / Medium / Large / Special / Curated)
│       └── Halls/     (Small / Medium / Large / Special)
└── Scenes/
    ├── Levels/
    │   └── Generated/ (V2-saved .unity scenes + manifests)
    ├── Test/          (Player_M1_Test.unity etc.)
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
3. Level_XX.unity — baked gameplay scenes generated by the V2 Level Generator into `Assets/Scenes/Levels/Generated/`

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
  Save to: Assets/Prefabs/Level Prefabs/Rooms/ or Assets/Prefabs/Level Prefabs/Halls/

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
  Hall/Stair → Assets/Prefabs/Level Prefabs/Halls/[name]_LG.prefab
  Room       → Assets/Prefabs/Level Prefabs/Rooms/[name]_LG.prefab

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
    field removed 2026-04-29 (was orphaned post-strafe-redesign; CS0414 warning fix).
    Player_MaleHero.prefab YAML still has the serialized value but Unity ignores
    fields with no matching script-side declaration on next save.
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
  M2-D (level integration): partial. PlayerSpawnPoint marker
        auto-placed by RoomBuilder in Starter rooms; PlayerSpawner
        reads the marker at runtime; Cinemachine target binding
        wires automatically on spawn. LevelGenerator-driven runtime
        spawn (composite Player_RuntimeRig refactor) is the
        remaining piece.
  M2-B Step 1 (clip survey, 2026-04-27): COMPLETE.
    Read-only inventory of Attack and Hit/Reaction/Death clips on the
    SwordAndShield pack. 10 candidates surveyed; all Humanoid rig, all
    loadable, all in-range length. No FAIL. WARN on 8/10 for loopTime=1
    (handled at Animator state level). Survey: Assets/Documentation/
    M2B_01_clip_survey_report.md. Locked picks: Attack01_SwordAndShiled
    (Option α) and GetHit01_SwordAndShield. Pack-name correction surfaced:
    the user-facing "MaleCharacterPBR" is a prefab name; the actual asset
    pack is "RPG Tiny Hero Duo PBR Polyart" with animations under
    Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/.
    FBX naming inconsistency: Attack/Idle clips use _SwordAndShiled
    (typo); Hit/Die/Move clips use _SwordAndShield. Match exact filenames
    when wiring overrides.
  M2-B Step 2 (Animator wiring, 2026-04-27): COMPLETE.
    Behavior table reviewed and approved at Assets/Documentation/
    M2B_02_animator_behavior_table.md. All Section 5 defaults accepted.
    PlayerBaseController.controller: 6 params (added Attack, Hit triggers
    with type=9), 5 states (added Attack at fileID 1100000000000000001
    motion=Attack01_SwordAndShiled, Hit at fileID 1100000000000000002
    motion=GetHit01_SwordAndShield), 11 state-to-state transitions
    + 1 anyStateTransition (N5: AnyState → Hit, canTransitionToSelf=true).
    Both new states: Loop=off (state-level), ApplyRootMotion=off,
    writeDefaultValues=1, m_Speed=1.0. The clip-side loopTime=1 import
    flag is left untouched — Animator state behavior governs looping.
    Attack root motion off makes Attack01's keepOriginalPositionY=0 a
    non-issue (no FBX import edits needed).
    PlayerOverride_MaleHero.overrideController: 8 self-mapped slots
    (added Attack01 + GetHit01).
    Transitions wired:
      N1: Idle → Attack          (Trigger Attack, dur 0.10, instant)
      N2: Locomotion → Attack    (Trigger Attack, dur 0.10, instant)
      N3: Sprint → Attack        (Trigger Attack, dur 0.10, instant)
      N4: Attack → Idle          (no cond, exitTime 0.90, dur 0.10)
      N5: AnyState → Hit         (Trigger Hit, dur 0.05,
                                  canTransitionToSelf=ON)
      N6: Hit → Idle             (no cond, exitTime 0.85, dur 0.10)
    YAML-edit fileIDs allocated in the 1100000000000000xxx range to
    avoid collision with existing hash-style fileIDs.
    Validator: Assets/Scripts/Player/Editor/PlayerCombatAnimatorValidator.cs
      (LevelGen ▶ Player ▶ Validate Combat Animator (M2-B Step 2)) —
      runs the six checks specified in the prompt, reads back via
      AnimatorController API, GUID-free clip lookup. Read-only.
    PlayerCombat.cs not yet written — that's M2-B Step 3 (will add
    SetAttackTrigger / SetHitTrigger methods on PlayerAnimator and
    wire the OnAttack input endpoint). PlayerInputReader.cs,
    PlayerController.cs, PlayerAnimator.cs unchanged this step.
  M2-B Step 3 (combat script + buffered combo, 2026-04-27): COMPLETE.
    Behavior table reviewed at Assets/Documentation/M2B_03_combat_behavior_table.md;
    all Section 5 defaults accepted.
    PlayerAnimator.cs: SetAttackTrigger() and SetHitTrigger() public methods,
      cached _hashAttack and _hashHit alongside existing param hashes; _ready
      gating preserved (silent no-op pre-Awake, matches SetMove/SetSprinting).
    PlayerInputReader.cs: public C# event AttackPressed (System.Action). OnAttack
      now raises it on ctx.performed; the M1 Debug.Log was removed (real consumer
      replaces stub log). M1-stub logs preserved on OnInteract/OnCrouch/OnJump/
      OnPrevious/OnNext.
    PlayerCombat.cs (NEW): namespace LevelGen.Player. RequireComponent
      PlayerInputReader + PlayerAnimator. Subscribes to AttackPressed in
      OnEnable, unsubscribes in OnDisable (event-based wiring; no UnityEvent
      inspector setup). Buffered-combo state machine — OnAttackPressed routes
      based on current Animator state: fires immediately from Idle/Locomotion/
      Sprint, sets _attackBuffered within combo window during Attack, drops
      input during Hit / outside window / mid-transition. Update polls
      GetCurrentAnimatorStateInfo each frame; consumes the buffered press by
      re-firing Attack trigger when Attack normalizedTime ≥ bufferConsumeAt.
      Public TakeHit() — parameterless, [ContextMenu("Take Hit")] for
      paused-game inspector testing. Clears _attackBuffered to prevent
      orphan combo input leaking past stagger.
    Combo window defaults (inspector-tunable, Range[0,1]):
      comboWindowOpen   = 0.40   (buffer-eligible from 40% into Attack clip)
      comboWindowClose  = 0.80   (buffer window closes at 80%; recovery after)
      bufferConsumeAt   = 0.85   (re-fire next-attack trigger at 85%; before
                                  the controller's 0.90 Attack→Idle exit time)
    Player_MaleHero.prefab: PlayerCombat added to root via
      LevelGen ▶ Player ▶ Add PlayerCombat to Player_MaleHero Prefab
      (Assets/Scripts/Player/Editor/PlayerCombatPrefabAdder.cs). Idempotent —
      no-op if PlayerCombat is already present. Inspector defaults from
      SerializeField are sufficient; no per-field wiring needed.
    Validator: Assets/Scripts/Player/Editor/PlayerCombatValidator.cs
      (LevelGen ▶ Player ▶ Validate PlayerCombat Wiring (M2-B Step 3)) —
      reflection checks on PlayerAnimator/InputReader/Combat API surface +
      prefab component-presence sanity. Read-only.
    Smoke test: Assets/Documentation/M2B_03_smoke_test.md — 10 manual checks
      covering Attack from Idle/Locomotion/Sprint, buffer fire, drop-early,
      drop-late, TakeHit interrupts Attack, TakeHit from Idle, re-hit during
      Hit (canTransitionToSelf), Hit blocks Attack input.
    Architecture: single-direction dependency preserved.
      PlayerInputReader → (C# event) → PlayerCombat → (public API) →
      PlayerAnimator → Animator. PlayerCombat never calls Animator.SetTrigger
      directly; only PlayerAnimator writes parameters.
    PlayerController.cs unchanged this step (locomotion locked).
    No FBX, controller, or override-controller modifications this step.
    M2-B "single attack + hit reaction" target: COMPLETE.
  M2-B Step 4 (jump animator wiring, 2026-04-27): COMPLETE.
    Three-state Jump arc wired into the Animator graph: JumpStart → JumpAir
    → JumpEnd. Survey + behavior table reviewed at
    Assets/Documentation/M2B_04_jump_clip_survey.md and
    Assets/Documentation/M2B_04_jump_animator_behavior_table.md;
    all Section 5 defaults accepted. Survey was clean — no FAIL, no WARN.
    Every jump clip is Humanoid + InPlace + 1/1/1 root-motion-locked at the
    FBX level; JumpAir already has loopTime=1 and the start/end clips
    already have loopTime=0, so no FBX repairs were needed.
    Locked clip picks (all from
      Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/):
      JumpStart_Normal_InPlace_SwordAndShield (~0.267 s, 9 frames)
      JumpAir_Normal_InPlace_SwordAndShield   (~0.500 s, 16 frames, loops)
      JumpEnd_Normal_InPlace_SwordAndShield   (~0.400 s, 13 frames)
    PlayerBaseController.controller: 8 params (added Jump trigger,
      IsGrounded bool with default true), 8 states (added JumpStart at
      fileID 1100000000000000003, JumpAir at 1100000000000000004,
      JumpEnd at 1100000000000000005), 18 state-to-state transitions
      + 1 anyStateTransition (unchanged from Step 2 — jump is structurally
      blocked during Attack/Hit because no Attack→JumpStart or
      Hit→JumpStart transitions exist).
    Transitions wired:
      N7:  Idle → JumpStart       (Jump AND IsGrounded==true,    dur 0.05)
      N8:  Locomotion → JumpStart (Jump AND IsGrounded==true,    dur 0.05)
      N9:  Sprint → JumpStart     (Jump AND IsGrounded==true,    dur 0.05)
      N10: JumpStart → JumpAir    (IsGrounded==false,            dur 0.10)
      N11: JumpStart → JumpAir    (fallback, exitTime 0.95,      dur 0.10)
      N12: JumpAir → JumpEnd      (IsGrounded==true,             dur 0.05)
      N13: JumpEnd → Idle         (no cond, exitTime 0.85,       dur 0.10)
    All three jump states: WriteDefaults=1, m_Speed=1.0, IKOnFeet=0.
    Apply Root Motion is a global Animator-component setting in Unity,
    not a per-state YAML field; the prefab's existing Animator already has
    it off, and the InPlace clips lock all three root-motion axes anyway,
    so vertical motion will come exclusively from script in Step 5
    (CharacterController.Move + jumpVelocity + gravity).
    YAML-edit fileIDs allocated in the 1100000000000000xxx range
    continuing from Step 2: states 003/004/005 + transitions 107-113.
    PlayerOverride_MaleHero.overrideController: 11 self-mapped slots
    (added 3 jump clips alongside the 8 from Steps M1/M2-C/Step 2).
    Validator: Assets/Scripts/Player/Editor/PlayerJumpAnimatorValidator.cs
      (LevelGen ▶ Player ▶ Validate Jump Animator (M2-B Step 4)) —
      seven checks per the Step 4 prompt: param presence + IsGrounded
      default-true, state presence, motion-resolves (M2 strafe lesson),
      override resolution (GUID-free name lookup), transition counts
      (18 + 1), specific N7-N13 condition/exit-time checks, JumpAir
      clip.isLooping (PASS or WARN with FBX path on false). Read-only.
    Architecture: jump is Animator-side only this step. PlayerController.cs,
    PlayerInputReader.cs, PlayerAnimator.cs, PlayerCombat.cs all unchanged.
    Step 5 will add SetJumpTrigger() / SetGrounded(bool) on PlayerAnimator,
    raise a JumpPressed event from PlayerInputReader, and add jump physics
    + IsGrounded polling + airborne-gravity-during-Hit handling to
    PlayerController.
    Mid-air-hit caveat (flagged for Step 5): N5 (AnyState → Hit) covers
    JumpStart/JumpAir/JumpEnd, so a hit while airborne plays GetHit01 in
    place. PlayerController must continue applying gravity during the
    Hit state so the player still falls; otherwise they'd stick to the
    air-pose Y until the stagger ended.
  M2-B Step 5 (jump physics + IsGrounded polling + input wiring,
    2026-04-27): COMPLETE.
    Behavior table reviewed at
      Assets/Documentation/M2B_05_jump_runtime_behavior_table.md;
      all Section 6 defaults accepted (Q4 yes / allow during transitions,
      Q5 Option A / block during JumpEnd, Q6 lazy AnimatorComponent
      property, Q7 verify in smoke test, Q8 add OnEnable/OnDisable).
      Field-name reconciliation per Section 0: prompt's nominal
      _velocityY / _characterController / _playerAnimator map to
      actual _verticalVelocity / _cc / _anim.
    PlayerAnimator.cs: added SetJumpTrigger() and SetGrounded(bool)
      public methods; cached _hashJump and _hashIsGrounded alongside
      existing param hashes; _ready gating preserved (silent no-op
      pre-Awake, matches SetMove / SetSprinting / SetAttackTrigger /
      SetHitTrigger).
    PlayerInputReader.cs: public C# event JumpPressed (System.Action).
      OnJump now raises it on ctx.performed; the M1 Debug.Log was
      removed (real consumer replaces stub log; matches OnAttack
      precedent from Step 3). M1-stub logs preserved on OnInteract /
      OnCrouch / OnPrevious / OnNext.
    PlayerController.cs: significant additive changes (no refactor).
      - [Header("Jump")] jumpHeight = 1.2f SerializeField. Air-time
        ≈ 0.99s at default 1.2m / -9.81 gravity.
      - Lazy AnimatorComponent property (mirrors PlayerCombat Step 3
        fix — sibling Awake order is non-deterministic).
      - Static state hashes: AttackStateHash, HitStateHash,
        JumpEndStateHash. Jump START / AIR are not polled — the
        airborne path is covered by !_isGrounded.
      - Grounded edge-detect state: _isGrounded, _wasGrounded,
        _groundedDirty=true (forces frame-0 SetGrounded write so the
        Animator default true matches reality even on airborne spawn).
      - OnEnable / OnDisable subscribes / unsubscribes
        _input.JumpPressed → OnJumpPressed.
      - OnJumpPressed handler: drops press during IsActionLocked()
        (Attack/Hit, but NOT during transitions per Q4),
        !_isGrounded (no air-jump), or IsInJumpEndState() (landing
        recovery). Otherwise applies kinematic jump velocity
        v = sqrt(2*h*|g|) to _verticalVelocity and fires SetJumpTrigger.
      - IsActionLocked() / IsInJumpEndState() private helpers; both
        read through the lazy AnimatorComponent property.
      - Update pipeline modified additively (no refactor):
          step 1.5 — _isGrounded = _cc.isGrounded
          step 7.5 — edge-detected SetGrounded write between
                     SnapBodyToCameraYaw and SetMove
        Existing 1–9 numbering preserved, 1.5 / 7.5 inserted.
      - **ApplyGravity bug fix:** the existing sticky-ground clamp
        was unguarded — `if (_cc.isGrounded) _verticalVelocity =
        stickyGroundVelocity;`. This would clobber the positive jump
        velocity in OnJumpPressed before it ever applied (jump pressed
        on frame N; Update runs same frame; ApplyGravity overwrites
        +4.85 → -2; Move propagates -2 → no rise). Fixed to gate on
        `_cc.isGrounded && _verticalVelocity < 0f` so the clamp only
        fires when grounded AND not currently rising. M1 never exposed
        this bug because there was no jump.
    Player_MaleHero.prefab: NO component changes. Jump piggybacks on
      existing PlayerController.
    Architecture: single-direction dependency preserved.
      PlayerInputReader → (C# event JumpPressed) → PlayerController →
      (public API SetJumpTrigger / SetGrounded) → PlayerAnimator →
      Animator. PlayerController never calls Animator.SetTrigger /
      SetBool directly; only PlayerAnimator writes parameters.
    Air control: full (locked decision). Existing camera-relative
      move-vector build does not gate on grounded state, so horizontal
      motion runs every frame regardless of airborne state.
    Gravity-during-Hit: correct by existing code structure.
      ApplyGravity sets motion.y AFTER step 4.5's horizontal-motion
      zero clears — vertical motion is preserved during action lock.
      Mid-air hit therefore continues to fall (validates Step 4's
      airborne-hit caveat).
    Coyote time / jump buffering / variable jump height: deferred.
      None in M2-B. Add separately if play-test requests.
    JumpStart visible portion ≈ 0.05–0.1s in practice — pack-authored
      0.267s clip plus 1–2 frame IsGrounded flip means N10
      (JumpStart→JumpAir on !IsGrounded) fires almost immediately.
      Accepted as-is per Section 5.4.
    Validator: Assets/Scripts/Player/Editor/PlayerJumpRuntimeValidator.cs
      (LevelGen ▶ Player ▶ Validate Jump Runtime (M2-B Step 5)) —
      reflection checks on PlayerAnimator/InputReader/Controller API
      surface + prefab component-presence sanity. Read-only.
    Smoke test: Assets/Documentation/M2B_05_jump_smoke_test.md —
      10 manual checks covering jump from Idle/Locomotion/Sprint,
      air control, action-lock blocking, JumpEnd-recovery blocking,
      no-double-jump, mid-air-hit gravity continuity (CRITICAL), and
      jump-press-does-not-consume-attack-combo-buffer.
    M2-B "combat: jump, attack, hit" target: COMPLETE.
  M2-B Step 6 (combo Animator wiring, 2026-04-29): COMPLETE.
    Behavior table reviewed at Assets/Documentation/
      M2B_06_combo_animator_behavior_table.md; all Section 7 defaults
      accepted (including YAML fileID allocation per Q8). Animator-only
      step — no PlayerCombat / PlayerController / PlayerInputReader /
      PlayerAnimator / FBX / prefab modifications. Step 7 will route
      buffered presses to the new ComboNext trigger.
    PlayerBaseController.controller: 9 params (added ComboNext Trigger
      with type=9), 10 states (added Attack02 at fileID
      1100000000000000006 motion=Attack02_SwordAndShiled, Attack03 at
      fileID 1100000000000000007 motion=Attack03_SwordAndShiled), 22
      state-to-state transitions + 1 anyStateTransition.
    PlayerOverride_MaleHero.overrideController: 13 self-mapped slots
      (added Attack02 + Attack03 from RPG Tiny Hero Duo).
    Transitions added (continuing 1100000000000000xxx convention from
    Steps 2 and 4):
      N14 (fileID 114): Attack → Attack02
                        condition ComboNext (If), exitTime 0.85, dur 0.10
                        — listed BEFORE N4 in Attack.m_Transitions
      N15 (fileID 115): Attack02 → Attack03
                        condition ComboNext (If), exitTime 0.85, dur 0.10
                        — listed BEFORE N16 in Attack02.m_Transitions
      N16 (fileID 116): Attack02 → Idle
                        no conditions, exitTime 0.90, dur 0.10 (fallback)
      N17 (fileID 117): Attack03 → Idle
                        no conditions, exitTime 0.90, dur 0.10 (always)
    Combo routing: at Attack/Attack02 exit-time 0.85 the Animator first
      checks the ComboNext-conditioned transition; if the trigger is
      set, combo wins. Otherwise the no-condition fallback at 0.90 fires
      and routes to Idle. Existing N4 (Attack → Idle) is preserved as
      the Attack-state fallback alongside N14.
    Attack03 → Idle (N17) is the only outgoing transition from Attack03;
      no further combo wiring (3-hit combo locked — Attack04 reserved
      for future finisher / heavy attack but unwired).
    All four new transitions: m_HasFixedDuration=1, m_InterruptionSource=0,
      m_OrderedInterruption=1, m_CanTransitionToSelf=0, m_Solo=0, m_Mute=0.
    Attack02/Attack03 state YAML matches existing Attack: m_Speed=1,
      m_IKOnFeet=0, m_WriteDefaultValues=1, m_Mirror=0,
      m_SpeedParameterActive=0, m_TimeParameterActive=0. Apply Root Motion
      remains a global Animator-component setting (off on prefab from
      Step 4); FBX-side root motion locked 1/1/1 on both new clips per
      Step 1 Section C.
    ComboNext is a Trigger (not Bool): auto-clears if unconsumed within
      one Animator update. Hit-cancels-combo handled for free via N5
      (Any State → Hit) — pending ComboNext is discarded. Avoids the
      need for explicit clearing in TakeHit() or a StateMachineBehaviour.
    Existing Attack state name preserved (NOT renamed to Attack01) for
      PlayerCombat hash stability (AttackStateHash =
      Animator.StringToHash("Attack")).
    Validator: Assets/Scripts/Player/Editor/PlayerComboAnimatorValidator.cs
      (LevelGen ▶ Player ▶ Validate Combo Animator (M2-B Step 6)) — nine
      checks: param presence, state presence, motion-resolves (M2 strafe
      lesson), override resolution (GUID-free name lookup), transition
      counts (22 + 1), per-state transition counts (Attack=2, Attack02=2,
      Attack03=1), per-state transition order (combo before fallback on
      Attack and Attack02), exit-time spot checks (N14/N15=0.85,
      N16/N17=0.90). Read-only.
  M2-B Step 7 (combo runtime wiring, 2026-04-29): COMPLETE.
    Behavior table reviewed at Assets/Documentation/
      M2B_07_combo_runtime_behavior_table.md; all Section 6 defaults
      accepted (recommendations 1–8). Section 0 reconciled the prompt's
      pseudocode names to the live code (`AnimatorComponent` property +
      local `anim` / `n`).
    PlayerAnimator.cs: SetComboNext() public method added; cached
      _hashComboNext alongside existing 8 param hashes; ParamComboNext
      const "ComboNext"; hash assignment in Awake. _ready gating
      preserved (silent no-op pre-Awake, matches all sibling trigger
      setters).
    PlayerCombat.cs:
      - Added Attack02StateHash and Attack03StateHash static readonly
        ints alongside existing AttackStateHash + HitStateHash.
      - Update() consume-site state gate: `hash != AttackStateHash &&
        hash != Attack02StateHash` (Attack03 falls through to early-
        return — combo cap, nothing to consume into).
      - Update() consume-site call: SetAttackTrigger → SetComboNext.
      - OnAttackPressed: explicit `if (hash == Attack03StateHash)
        return;` early-drop after the Hit check (combo cap; defensive
        against future Animator graph changes that might add an
        Attack-trigger transition out of Attack03).
      - OnAttackPressed: `inActiveAttack` boolean now covers both
        Attack and Attack02 (was Attack-only); window-buffer logic
        unchanged.
      - Class XML doc updated to describe the 3-hit combo (was "single
        Attack state, mechanism in place for future Attack02+").
    Player_MaleHero.prefab: NO component changes. Combo runs on the
      existing Step 3 PlayerCombat.
    Architecture: single-direction dependency preserved.
      PlayerInputReader → (C# event AttackPressed) → PlayerCombat →
      (public API SetAttackTrigger / SetComboNext / SetHitTrigger) →
      PlayerAnimator → Animator. PlayerCombat never calls
      Animator.SetTrigger directly; only PlayerAnimator writes
      parameters.
    Combo cap enforcement is belt-and-suspenders:
      1. Explicit Attack03 early-return in OnAttackPressed (this step).
      2. Implicit: Animator graph has no outgoing ComboNext or Attack
         transition from Attack03 (Step 6).
      Both paths drop a 4th press; the explicit drop is cheaper and
      makes design intent visible in script.
    Movement-during-combo intentionally unrestricted: PlayerCombat
      .IsActionLocked still gates only on Attack/Hit state hashes
      (line 83). Attack02 and Attack03 do NOT lock horizontal motion,
      so the player can micro-position between hits. Step 7 Section 5.5
      called this out as Q7 with explicit defer recommendation —
      tuning concern, not a Step 7 deliverable.
    Validator: Assets/Scripts/Player/Editor/PlayerComboRuntimeValidator.cs
      (LevelGen ▶ Player ▶ Validate Combo Runtime (M2-B Step 7)) —
      eight checks: SetComboNext public/void, _hashComboNext field,
      Attack02StateHash + Attack03StateHash static-readonly fields,
      hash values match StringToHash output, source-scan for exactly 1
      SetAttackTrigger call + 1 SetComboNext call in PlayerCombat.cs,
      compile-clean by transitivity. Read-only.
    Smoke test: Assets/Documentation/M2B_07_combo_smoke_test.md —
      5 manual checks covering full 3-hit combo, drop-if-unbuffered,
      cap-at-Attack03, hit-cancels-combo-mid-chain, jump-during-combo-
      doesn't-corrupt-buffer (regression of Step 5 single-direction
      architecture).
    M2-B "combat: jump, attack, hit + 3-hit combo" target: COMPLETE.
  M2-B (remaining work): Attack04 / heavy-attack / finisher (clip is
    validated in Step 1 survey but unwired). Death state (Die01 clip
    is in the pack but not yet wired into the Animator graph). Both
    can ship after M2-D level integration since neither is on the
    critical path.
  M2-B Step 6/7 design correction (2026-04-29, post-runtime-test):
    During Step 7 smoke testing, the combo chained Attack→Attack02
    every time without requiring a buffered press during the combo
    window. Diagnostic logging in PlayerCombat.Update and
    PlayerAnimator.SetComboNext confirmed the trigger was never being
    set before the state transition fired. Root cause: Unity 6.4's
    Animator transition evaluation for "Has Exit Time = true + Trigger
    condition" empirically auto-fires at the exit time regardless of
    whether the trigger is set, contrary to the documented AND
    semantics. Both N14 (Attack→Attack02) and N15 (Attack02→Attack03)
    were affected. Fix: removed Has Exit Time from N14 and N15 (now
    condition-only on ComboNext). Effective timing unchanged because
    PlayerCombat.Update only calls SetComboNext at n >= bufferConsumeAt
    (0.85) — the gate moves from Animator exit-time to script-side.
    N16 (Attack02→Idle) and N17 (Attack03→Idle) keep Has Exit Time =
    true because they have no conditions (auto-fire is the desired
    semantics). PlayerComboAnimatorValidator Check 9 updated to expect
    hasExitTime=false on N14/N15. Lesson logged in
    M2B_06_combo_animator_behavior_table.md "Design Correction"
    section: don't combine Has Exit Time with a Trigger condition in
    Unity 6.4 — use one or the other (or gate the condition-set call
    in script, which is what PlayerCombat does).
  M3-02A (pack swap, 2026-04-30): Duo retired, World Bundle imported.
    Driven by the inventory in Assets/Documentation/M3_01_pack_swap_inventory.md
    (M3-01 confirmed all 13 currently-wired clip GUIDs were byte-
    identical between Duo and World Bundle — Dungeon Mason convention).
    Executed via three menu items (LevelGen ▶ Pack Swap ▶ ...) from
    Assets/Scripts/Player/Editor/M3_02A_PackSwapExecutor.cs (one-off
    scaffolding, can be deleted post-swap):
      Step 1: AssetDatabase.DeleteAsset("Assets/AssetPacks/RPG Tiny Hero Duo")
      Step 2: AssetDatabase.ImportPackage(unitypackage, interactive=false)
              + delete embedded HDRP_BuiltIN/HDRP.unitypackage and
              HDRP_BuiltIN/BuiltIn.unitypackage (alt-pipeline; URP-only
              project) + AssetDatabase.MoveAsset to relocate from
              publisher default Assets/RPGTinyHeroWorldBundlePBR/
              to Assets/AssetPacks/RPG Tiny Hero World Bundle/
      Step 3: GUID-resolution verification (13/13 PASS, 1 cosmetic
              WARN) + ExecuteMenuItem chain over all 6 M2-B validators.
    Pack details: SHA-256 81872f73...09b2f78509b, 339 MiB, 1607 assets.
    Imported sub-packs: RPGTinyHeroWavePBR (1075 — characters,
    animations, weapons; 8 weapon sets vs Duo's 1: SwordAndShield
    [used], BowAndArrow, DoubleSword, MagicWand, NoWeapon, SingleSword,
    Spear, TwoHandSword) and RPG Tiny Fantasy World 01 PBR (528
    environment assets — usable for level mockups).
    GUID-preserving auto-relink result: ALL 13 clips resolved to new
    pack paths without any controller/override/script edits. The
    publisher renamed only ONE clip-subasset internally
    (Idle: typo'd "Shiled" → corrected "Shield"); FBX filename and
    GUID identical, runtime resolves by GUID, so no runtime impact.
    No edits needed in PlayerBaseController.controller,
    PlayerOverride_MaleHero.overrideController,
    PlayerCombat.cs, PlayerAnimator.cs, PlayerController.cs,
    PlayerInputReader.cs, or Player_MaleHero.prefab structure.
    Validator state immediately post-swap:
      Step 2 validator: 11 PASS / 1 FAIL (5a stale count == 11,
                        actual 22)
      Step 3 validator: 10 PASS / 0 FAIL ✓
      Step 4 validator: 21 PASS / 1 FAIL (5a stale count == 18,
                        actual 22)
      Step 5 validator: 9 PASS / 0 FAIL ✓
      Step 6 validator: 20 PASS / 0 FAIL ✓
      Step 7 validator: 9 PASS / 0 FAIL ✓
    The two FAILs were pre-existing stale assertions, not swap-related.
    Older validators hardcoded the transition count from the milestone
    that authored them (Step 2 = 11, Step 4 = 18); subsequent
    milestones added more transitions (Step 4 added 7, Step 6 added 4,
    bringing current total to 22). Patched both validators to use
    floor checks (`>= 11`, `>= 18`) so future additions don't
    re-trigger the FAIL. Step 6's strict `== 22` assertion retained
    as the current-truth count.
    Player rig (MaleCharacterPBR child of Player_MaleHero.prefab) is
    in a "Missing Nested Prefab" state post-swap — the Duo's
    MaleCharacterPBR.prefab (GUID 2dfbb63c9cdf7504faf4ff26b0581598)
    was deleted, and the new pack ships 24 modular character prefabs
    (MC01-MC24) instead. M3-02B will pick a replacement and re-wire
    the prefab. In the meantime, Play mode runs but with no visible
    player mesh.
    Documentation: M3_02A_preswap_baseline.md (controller + override
    GUID snapshot), M3_02A_preswap_player_rig.md (prefab + Animator
    settings), M3_02A_postswap_verification.md (auto-relink table +
    validator results).
    Smoke tests deferred to post-M3-02B (visual checks would fail on
    invisible-mesh state).
  M3-01B (base rig discovery, 2026-04-30): read-only inventory of
    World Bundle's Mesh/ folder. Identified AllBodiesCloaks.fbx
    (GUID 075789f0f3fa9414f90d335ae163f413) as the sole Humanoid-rig
    FBX in the new pack — same Avatar source GUID
    (0308cf4e83cf517488b60af58b290fe0) as Duo's MaleCharacterPBR.
    All 24 MC* prefabs reference it as their body mesh. Of 179 Mesh/
    FBXes, exactly 1 is Humanoid; remaining 115 HeadParts + 62
    Weapons + 1 Stage are Generic (parented props). No Polyart variant
    in the new pack (Duo had both PBR + Polyart). Documentation:
    M3_01B_base_rig_discovery.md.
  M3-03A (Duo diff, 2026-04-30): read-only diff between Duo
    .unitypackage (87 assets, SHA-256 7d0bf88b...155b9cd4, 19.6 MiB)
    and the imported World Bundle. Result: 56 DUPLICATE GUIDs
    (animations, textures, controllers, shared materials), 31 UNIQUE
    GUIDs (character prefabs, body/weapon/shield meshes, demo scenes,
    Polyart variants). Filtered to character-relevant items only:
    M3-03B re-import set is 11 assets (2 char prefabs + 1 body mesh +
    4 equipment prefabs + 4 equipment meshes). Excluded: 12 Polyart
    variants (locked PBR target), 3 demo scenes, 2 example controllers,
    1 mask, 3 demo materials. Dependency tracing confirmed character
    prefabs come pre-equipped with sword + shield as nested
    PrefabInstances — equipment must re-import too or characters load
    Missing-Prefab. Shared PBR_Default.mat ↔ World Bundle's
    DefaultPBR.mat (same GUID f323cced...8b67, just renamed by
    publisher). Documentation: M3_03A_duo_diff.md.
  M3-03B (selective Duo re-import, 2026-04-30): COMPLETE.
    Re-imported 11 character-relevant assets from
    RPG Tiny Hero Duo PBR Polyart.unitypackage to
    Assets/AssetPacks/RPG Tiny Hero Duo/ (preserving Duo's prior pack
    location). Method: bash extraction of .unitypackage to /tmp/, GUID
    filter against the 11-allowed set, filesystem copy of asset+meta
    pairs with assetPath rewrite from "Assets/RPG Tiny Hero Duo/" →
    "Assets/AssetPacks/RPG Tiny Hero Duo/" in .meta files. Editor
    script (Assets/Scripts/Player/Editor/M3_03B_DuoReimportVerifier.cs,
    one-off scaffolding — can be deleted post-completion alongside
    M3_02A_PackSwapExecutor.cs) verified post-import:
      - 11/11 GUIDs resolve to expected target paths
      - Player_MaleHero.prefab MaleCharacterPBR PrefabInstance
        auto-relinked (source GUID matches, not flagged Missing)
      - CameraTarget child preserved at (0, 1.6, 0)
      - All 6 M2-B validators clean: Step 2 12/0, Step 3 10/0,
        Step 4 22/0/0, Step 5 9/0, Step 6 20/0, Step 7 9/0
      - The two FAILs that appeared in M3-02A's validator run (Step 2
        and Step 4 stale count assertions) are now PASS thanks to the
        floor-check patches applied at the end of M3-02A.
    Re-imported assets:
      Prefab/MaleCharacterPBR.prefab     guid 2dfbb63c...0581598
      Prefab/FemaleCharacterPBR.prefab   guid cc91c8ba...60b64
      Mesh/ModularCharacterPBR.fbx       guid 34b0895d...691859
      Prefab/OHS03PBR.prefab + .fbx      Male's sword
      Prefab/OHS06PBR.prefab + .fbx      Female's sword
      Prefab/Shield05PBR.prefab + .fbx   Female's shield
      Prefab/Shield08PBR.prefab + .fbx   Male's shield
    Excluded from re-import: 76 other Duo assets (43 animation FBXes
    DUPLICATE in WB, 12 Polyart variants, 3 demo scenes, 2 controllers,
    1 mask, 3 demo materials, etc.).
    M3-02B (rig swap) folded into M3-03B's verification — separate
    rig-swap work not required because GUID auto-relink restored the
    Duo's MaleCharacterPBR exactly. Player_MaleHero.prefab is now
    visually whole again, same as pre-M3-02A.
    Pack swap milestone (M3) COMPLETE — all 25 manual smoke tests
    passed in Player_M1_Test.unity on 2026-04-30: M2B_03 (10/10
    single attack + buffer), M2B_05 (10/10 jump runtime), M2B_07
    (5/5 combo extension). See Assets/Documentation/M3_closeout.md
    for the milestone-closure record.

## Dummy + CharacterStats foundation (2026-05-01)

Combat data layer foundation shipped:

- CharacterStats ScriptableObject (LevelGen.Combat) — duplicate-and-
  tweak template for character HP/Stamina/displayName/description.
  CreateAssetMenu under "LevelGen/Combat/Character Stats". OnValidate
  clamps maxHP and maxStamina to >=1.
- CharacterStatsRuntime MonoBehaviour — references one stats asset,
  copies max values to currentHP/currentStamina at Awake. Public
  read-only properties. Internal ApplyDamage / Heal methods exist
  but are not called from anywhere yet (scaffolding for future
  damage application).
- Targetable marker component — empty identifier with optional
  AimPoint child. Future hook for enemy AI / target lock /
  damage application.
- CharacterStats_Master.asset (100/100, the template) and
  CharacterStats_Dummy.asset (999/100, sandbox crutch) shipped.
- Dummy.prefab — MaleCharacterPBR model + CharacterStatsRuntime +
  Targetable + Animator referencing PlayerBaseController. NO
  player control scripts. Plays Idle on play. Stationary target.
- DummyPrefabBuilder editor (Build Dummy Prefab + Place Dummy in
  Active Scene menu items).
- DummyAndStatsValidator editor — 12 read-only checks.

Stamina is data-only for now: depletion gameplay and HP/Stamina UI
deferred. PlayerCombat untouched in this milestone — TakeHit() still
trigger-only, no damage routing yet.

Files:
- Assets/Scripts/Combat/CharacterStats.cs
- Assets/Scripts/Combat/CharacterStatsRuntime.cs
- Assets/Scripts/Combat/Targetable.cs
- Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs
- Assets/Scripts/Combat/Editor/DummyAndStatsValidator.cs
- Assets/Data/CharacterStats/CharacterStats_Master.asset
- Assets/Data/CharacterStats/CharacterStats_Dummy.asset
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab

Pending follow-up:
- Damage application from PlayerCombat to Targetable + CharacterStatsRuntime
- Death state / hit reactions on Dummy
- HP/Stamina UI bars
- Stamina gameplay (sprint cost, attack cost, regen)

## Player HP / Stamina HUD (2026-05-01)

UI foundation shipped: bottom-left HP / Stamina bars, snap-on-
damage, lerp-on-heal, numeric labels.

- PlayerHUD component (LevelGen.UI) — passive observer reading
  CharacterStatsRuntime each frame. Tag-based player lookup
  with retry coroutine for deferred-spawn scenarios.
- PlayerHUD.prefab — Canvas root with Screen Space Overlay,
  HP (red) over Stamina (yellow), TMP_Text labels.
- CharacterStats_Player.asset (100/100) shipped.
- Player_MaleHero prefab now carries CharacterStatsRuntime
  pointing at CharacterStats_Player.
- Two debug ContextMenu hooks on CharacterStatsRuntime
  (Apply 10 Damage / Heal 10) for manual HUD verification —
  marked TODO M-DamageRouting for removal once real damage
  routing exists.
- PlayerHUDBuilder editor: Build / Place / Add Stats menu
  items.
- PlayerHUDValidator: 11 read-only checks.

HUD is bound and reactive but stats only change via the debug
ContextMenu hooks for now. Damage routing (next milestone) will
make stats change in real gameplay.

Sprite-fix correction (post-test): the first build produced bars
that didn't visually respond to fillAmount — labels updated but
the red rect stayed full. Root cause: a programmatically-created
Image with no sprite assigned cannot clip when type=Filled, so
fillAmount has no visual effect. Unity's editor Add-Component
flow auto-assigns UI/Skin/UISprite.psd; programmatic creation
does not. Fix: PlayerHUDBuilder.CreateBar now assigns
`AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`
to every bar (background = Sliced, fill = Filled). Lesson: when
authoring UI Images via code, always assign a sprite explicitly —
the default-no-sprite path renders fine for flat color but breaks
9-slice and Filled clipping.

Files:
- Assets/Scripts/UI/PlayerHUD.cs
- Assets/Scripts/UI/Editor/PlayerHUDBuilder.cs
- Assets/Scripts/UI/Editor/PlayerHUDValidator.cs
- Assets/Data/CharacterStats/CharacterStats_Player.asset
- Assets/Prefabs/UI/PlayerHUD.prefab
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (debug hooks added)
- Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
  (CharacterStatsRuntime added)

Pending follow-up:
- Damage routing: PlayerCombat hitbox colliders → ApplyDamage
  on Targetables in range
- Hit reactions on Dummy
- Stamina gameplay (sprint cost, attack cost, regen)
- Death state

## Damage routing — hitbox colliders (2026-05-01)

PlayerCombat now deals damage to Targetables via animation-event-
driven hitbox colliders.

- HitboxRelay component (LevelGen.Combat) — bridge from a child
  trigger collider to PlayerCombat.NotifyHitboxTriggered. Reset()
  auto-resolves the parent reference. No state, no Update.
- AnimationEventForwarder component (LevelGen.Combat) — sits on
  the Animator's GameObject (MaleCharacterPBR child) and forwards
  OnHitboxOpen / OnHitboxClose to PlayerCombat on the parent.
  Required because Unity dispatches AnimationEvents to the
  Animator's own GameObject only — it does NOT walk the
  hierarchy, so a method on PlayerCombat (parent of the Animator)
  never fires from an AnimationEvent. Symptom of the missing
  forwarder: console spam "'<animator-go-name>' AnimationEvent
  'OnHitboxOpen' has no receiver! Are you missing a component?".
- PlayerCombat extended with OnHitboxOpen / OnHitboxClose
  (forwarder endpoints) and NotifyHitboxTriggered (called
  from HitboxRelay.OnTriggerEnter). Per-attack HashSet<Targetable>
  hit list cleared on OnHitboxOpen — prevents double-hits within
  one swing while still allowing multi-target sweeps. Each combo
  step starts with a fresh list, so Attack→Attack02→Attack03 can
  each hit the same target once.
- Hardcoded `attackDamage = 10` SerializeField — WeaponStats SO
  deferred.
- CharacterStatsRuntime.ApplyDamage / .Heal promoted from internal
  to public (now have external callers).
- Player_MaleHero prefab gained a WeaponHitbox child under
  weapon_r — BoxCollider (size 0.15×0.15×0.8, center (0,0,0.4),
  isTrigger=true, enabled=false default) + HitboxRelay with
  combat ref pointed at the prefab root's PlayerCombat + a
  kinematic Rigidbody (isKinematic=true, useGravity=false). The
  PlayerCombat.hitbox SerializeField points back at the same
  BoxCollider — both ends wired by the builder.
  The kinematic Rigidbody is REQUIRED for OnTriggerEnter to fire
  on a hitbox that moves via skeletal animation. CharacterController
  on the prefab root does NOT promote deeply-nested child colliders
  to "moving" status — Unity's physics treats them as static
  colliders that happen to be teleporting, and trigger events
  don't dispatch. Symptom: OnHitboxOpen logs fine (events fire,
  hitbox enabled) but no "[PlayerCombat] Hit X for Y" lines ever
  appear. Adding the kinematic Rigidbody to the WeaponHitbox fixed
  it; verified empirically post-initial-build.
- Dummy.prefab gained a CapsuleCollider on root (radius=0.4,
  height=1.8, center=(0,0.9,0), isTrigger=false).
- AnimationEvents added to Attack01-03 at clip.length * 0.35
  (OnHitboxOpen) and clip.length * 0.65 (OnHitboxClose) via
  ModelImporter.clipAnimations + SaveAndReimport. Survives FBX
  reimport because events live in the .meta, not the FBX.
- PlayerCombatHitboxBuilder editor (Assets/Scripts/Combat/Editor/):
  3 menu items — Add Weapon Hitbox / Add Collider to Dummy /
  Add Animation Events to Attack Clips. All idempotent.
- DamageRoutingValidator: 12 read-only checks.

Debug ContextMenu hooks on CharacterStatsRuntime (Apply 10 Damage,
Heal 10) KEPT for now — the HUD's lerp-on-heal currently has no
real heal source, and a self-contained Inspector path for damage
is convenient when not playing. Comment retagged
`// TODO removeMe-after-stamina-and-heal-sources-exist`.

Trigger-event Rigidbody resolution: per Unity, OnTriggerEnter
requires at least one of the colliding pair to have a non-static
collider (Rigidbody, kinematic Rigidbody, or CharacterController).
The CharacterController on the prefab root does NOT promote
deeply-nested child colliders to non-static — the WeaponHitbox
needs its own kinematic Rigidbody to be classified as "moving".
This was the resolved cause of no-damage-events in the first
playthrough; the builder now adds it automatically.

Files:
- Assets/Scripts/Combat/HitboxRelay.cs
- Assets/Scripts/Combat/AnimationEventForwarder.cs
- Assets/Scripts/Player/PlayerCombat.cs (extended — fields + 3 methods)
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (visibility)
- Assets/Scripts/Combat/Editor/PlayerCombatHitboxBuilder.cs
- Assets/Scripts/Combat/Editor/DamageRoutingValidator.cs
- Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
  (WeaponHitbox child under weapon_r)
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab (capsule)
- Attack01-03 _SwordAndShiled.fbx clips (.meta updated with events)

Pending follow-up:
- Hit reactions on Dummy (Animator on Dummy already references
  PlayerBaseController; just needs Hit-trigger fired from damage
  event and combat-data routing on Targetable).
- Death state when HP <= 0.
- Stamina gameplay (sprint cost, attack cost, regen).
- Damage numbers (floating combat text).
- Player taking damage (Dummy fights back / enemy AI).
- WeaponStats SO with per-weapon damage values.
- PlayerHitbox layer + collision matrix tweaks (currently Default).

V1 retired: BoundsChecker, V1 LevelGenerator (runtime), SeedData,
LevelSequence, RoomDefinition, V1 RoomBuilder (COMP_-based),
PropEntry, PropCatalogue, SpawnPoint, RoomContentGenerator, RoomPreset,
RoomPresetLibrary, LevelGeneratorEditor, RoomWorkshopWindow,
PlaceholderPrefabFactory, LevelGenSetup. All recoverable from git
history pre-cleanup.

## M-CursorLock — cursor lock during Play mode (2026-05-02)

QoL detour from M4 hit-reaction work — no architectural changes to
existing systems.

- MouseLook.cs (LevelGen.Input) at Assets/Scripts/Input/MouseLook.cs.
  Moved from Assets/Scripts/MouseLook.cs preserving the .meta GUID
  (f797b8d394913ea4cb0c366ee3a7b9ea). New folder Assets/Scripts/Input/.
- Behavior: locks + hides cursor on enable; Escape unlocks; left-click
  in Game view re-locks; Application.focusChanged=false (Alt-Tab)
  unlocks; OnDisable/OnDestroy unlock as clean teardown.
  [DefaultExecutionOrder(-100)] runs before sibling scripts on frame 0.
- Reads UnityEngine.InputSystem.Keyboard.current /
  Mouse.current with null-guards for headless / batch mode.
- Public surface: bool IsLocked { get; private set; }. No public
  methods. Single MonoBehaviour, no other types.
- Despite the legacy filename, this does NOT rotate the camera or
  read mouse delta — PlayerInput owns Look input. The class is purely
  a Cursor.lockState / Cursor.visible controller.
- Validator: Assets/Scripts/Input/Editor/MouseLookValidator.cs
  (LevelGen ▶ Input ▶ Validate MouseLook) — 7 checks: file at new
  path, old path gone, type+MonoBehaviour, [DefaultExecutionOrder]
  non-positive, namespace contains "Input", exactly one active
  MouseLook in current scene, Play-mode cursor state (skipped in
  edit mode). Read-only.
- Placer: same file hosts a sibling menu item
  (LevelGen ▶ Input ▶ Place _MouseLock in Active Scene) that creates
  the _MouseLock GameObject + MouseLook component, idempotent
  (no-op if already present), Undo-registered, marks the scene
  dirty for save.

Files:
- Assets/Scripts/Input/MouseLook.cs (filled-in stub)
- Assets/Scripts/Input/Editor/MouseLookValidator.cs (NEW)

Pending follow-up:
- User runs `LevelGen ▶ Input ▶ Place _MouseLock in Active Scene`
  in Player_M1_Test.unity (or whichever scene is currently used
  for Play-mode testing) and saves the scene.
- Validator should hit 6 PASS + 1 SKIP (edit mode) or 7 PASS
  (Play mode).
- DontDestroyOnLoad behavior deferred until scene transitions exist.

## M4-A — enemy hit reaction (Dummy, 2026-05-02)

First reactive enemy behavior. Damage routing was already shipped
in M3 (DamageRoutingValidator 12/12); this milestone makes the
Dummy *visually* react when struck. Death is explicitly deferred.

Architectural decisions (locked):
- Animator: new `EnemyBaseController` (Idle + Hit only). Dummy
  stops referencing `PlayerBaseController`.
- Hit transition: `AnyState → Hit` with
  `canTransitionToSelf = false`. Stagger window lives in C#.
- Event routing: `Targetable` transitioned from pure marker →
  event publisher (`event Action<Vector3> OnHit`,
  `public RaiseHit(Vector3)`). Payload is the world-space hit
  point on the target's collider.
- Stagger: script-side cooldown on `EnemyHitReaction`, default
  `staggerWindow = 0.3f`. Within the window, additional `OnHit`
  calls are visually swallowed (damage is already applied
  upstream — they do not re-fire the Hit trigger).
- Single-writer-to-Animator invariant preserved.
  `EnemyHitReaction` is the sole script writing to the Dummy's
  Animator parameters.

Targetable.cs: extended from pure marker → marker + event
publisher. Existing AimPoint resolution preserved. New event
`OnHit` (`Action<Vector3>`) and public `RaiseHit(Vector3)` method.
The class still holds no hit state — `RaiseHit` is a pure
pass-through to subscribers.

PlayerCombat.cs: one-line addition in `NotifyHitboxTriggered`
inside the stats-found branch, after `ApplyDamage` and the
`_currentAttackHitList.Add` call. Computes hit point as
`other.ClosestPoint(hitbox.bounds.center)` (with
`other.bounds.center` fallback if `hitbox` is somehow null) and
calls `targetable.RaiseHit(hitPoint)`. The misconfiguration
warning branch (Targetable without CharacterStatsRuntime) does
NOT call `RaiseHit` — firing the event there would mislead
subscribers since no damage actually applied. No refactoring of
surrounding code.

EnemyBaseController.controller: built procedurally via
`Assets/Scripts/Combat/Editor/EnemyBaseControllerBuilder.cs`
(menu `LevelGen ▶ Combat ▶ Build EnemyBaseController`,
idempotent — deletes & recreates). Lives at
`Assets/Animators/Enemy/EnemyBaseController.controller`.
- Parameters: `Hit` (Trigger).
- States: `Idle` (default, motion = `Idle_Battle_SwordAndShield`
  sub-asset of `Idle_Battle_SwordAndShiled.fbx` — matches what the
  Dummy displayed pre-swap, preserves continuity), `Hit` (motion
  = `GetHit01_SwordAndShield`).
  Both states: writeDefaultValues=1, speed=1.0.
- Transitions:
  AnyState → Hit: condition `Hit` (If), hasExitTime=false,
    fixedDuration=true, duration=0.05, canTransitionToSelf=false.
  Hit → Idle: no conditions, hasExitTime=true, exitTime=0.95,
    fixedDuration=true, duration=0.10.
The "Loop=false" requirement at the state level is governed by
the exit-time transition — Hit transitions out at normalizedTime
0.95 before any clip-side loop wraps. Same pattern as
PlayerBaseController's Hit/Attack states (clip-side `loopTime=1`
left untouched; behavior governed Animator-side).

FBX-vs-subasset name mismatch (lesson, surfaced during initial
build): the Idle FBX's filename retains the publisher's "Shiled"
typo (`Idle_Battle_SwordAndShiled.fbx`) but the AnimationClip
sub-asset inside was renamed to "Shield" during the M3 pack swap
(Duo → World Bundle). `LoadAllAssetRepresentationsAtPath` returns
clips by their sub-asset name, not the FBX filename. First build
attempt failed with "Could not load Idle clip" because the
constant used the FBX filename. CLAUDE.md M3-02A had flagged this
exact mismatch ("Idle: typo'd 'Shiled' → corrected 'Shield'");
should be the default assumption when loading World Bundle clips
from now on. The Hit clip's filename and sub-asset name match —
no Hit-side fix needed.

DummyPrefabBuilder.cs: three surgical edits. (1) `BaseCtrlPath`
constant updated from `PlayerBaseController.controller` →
`EnemyBaseController.controller`. (2) After the Animator wiring
block, `EnemyHitReaction` is added to the Dummy root with its
`animator` field bound to the `MaleCharacterPBR` child Animator
via `AssignAnimatorField` helper (mirrors existing
`AssignStatsField` SerializedObject pattern). (3) `CapsuleCollider`
folded into the build (root-level, isTrigger=false, radius=0.4,
height=1.8, center=(0,0.9,0), direction=Y). Previously the capsule
was added by a separate `LevelGen ▶ Combat ▶ Add Collider to
Dummy` menu (in `PlayerCombatHitboxBuilder.cs`); the standalone
menu still works, but folding the capsule into the main builder
prevents rebuild from dropping it (regression caught by
DamageRoutingValidator check 10 during M4-A verification).
Re-running `Build Dummy Prefab` is idempotent — a fresh root is
created from scratch each time, so no AddComponent guard is
needed. Build now logs `CapsuleCollider` and `EnemyHitReaction`
alongside the other root-level components.

EnemyHitReaction.cs (NEW): namespace `LevelGen.Combat`.
`[RequireComponent(typeof(Targetable))]`,
`[DisallowMultipleComponent]`. Subscribes to `Targetable.OnHit`
in OnEnable, unsubscribes in OnDisable. `HandleHit(Vector3)`
checks the script-side stagger cooldown — if outside the
window, updates `_lastHitTime` and calls
`animator.SetTrigger(HitTriggerHash)`. The Vector3 hit point is
ignored for now (pass-through to future knockback / VFX
subscribers).

Validator: `Assets/Scripts/Combat/Editor/EnemyHitReactionValidator.cs`
(menu `LevelGen ▶ Combat ▶ Validate EnemyHitReaction`) — 14
read-only checks: Targetable.OnHit event type, RaiseHit signature,
EnemyHitReaction.cs presence, RequireComponent + DisallowMultiple
attributes, EnemyBaseController asset + parameter + state +
transition shape (canTransitionToSelf=false on AnyState→Hit,
hasExitTime=true on Hit→Idle), Dummy.prefab Animator points at
EnemyBaseController (NOT PlayerBaseController), EnemyHitReaction
on Dummy root with `animator` field wired, PlayerCombat.cs
source contains `RaiseHit(` call. Format mirrors
`DamageRoutingValidator`. Uses `RequireComponent.m_Type0/1/2`
public field surface for attribute introspection.

Files:
- Assets/Scripts/Combat/Targetable.cs (event publisher)
- Assets/Scripts/Combat/EnemyHitReaction.cs (NEW)
- Assets/Scripts/Combat/Editor/EnemyBaseControllerBuilder.cs (NEW)
- Assets/Scripts/Combat/Editor/EnemyHitReactionValidator.cs (NEW)
- Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs (controller
  swap + EnemyHitReaction wiring)
- Assets/Scripts/Player/PlayerCombat.cs (RaiseHit call after
  ApplyDamage in NotifyHitboxTriggered)
- Assets/Animators/Enemy/EnemyBaseController.controller (NEW,
  produced by builder)
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab (rebuilt
  by `Build Dummy Prefab` after EnemyBaseController exists)

Verification (complete):
- `LevelGen ▶ Combat ▶ Validate EnemyHitReaction` — 14 PASS / 0 FAIL.
- `LevelGen ▶ Combat ▶ Validate Damage Routing` — 12 PASS / 0 FAIL
  (sanity re-run after rebuilds).
- Play-mode smoke test: confirmed working in test scene.

Death deferred to M4-B (shipped 2026-05-03 — see below). Other
enemy types deferred. Knockback / VFX consumers of the hit-point
payload deferred.

## M4-B — enemy death (Dummy, 2026-05-03)

Closes the death loop on Dummy. HP→0 now plays Die01, disables
the corpse's interaction surface (Targetable + Collider +
EnemyHitReaction), and despawns after a configurable delay.
Player-side death and other enemy types remain out of scope.

Architectural decisions (locked):
- Animator: `EnemyBaseController` gains a Death state. Terminal —
  AnyState→Death (trigger), no outgoing transition. Animator parks
  on the last frame of Die01 until despawn destroys the GameObject.
- Cleanup ownership: new `EnemyDeath` script is the sole owner of
  the death sequence. Subscribes to
  `CharacterStatsRuntime.OnDied`, executes the 5-step cleanup
  (disable Targetable / Collider / EnemyHitReaction → fire Death
  trigger → schedule Destroy).
- Despawn: `Destroy(gameObject, despawnDelay)` with
  `despawnDelay = 5f` default, tunable on EnemyDeath. <=0 keeps
  the corpse forever (test convenience).
- Event signature: `event Action<CharacterStatsRuntime> OnDied`
  on CharacterStatsRuntime. Pass-self payload mirrors
  Targetable.OnHit's pattern.
- Single-writer-per-Animator-parameter invariant preserved.
  Dummy's Animator now has two writers: EnemyHitReaction (Hit
  trigger) and EnemyDeath (Death trigger). Each owns exactly one
  parameter; no overlap. Established convention upgraded from
  single-writer-per-Animator to single-writer-per-parameter.
- Hit-after-Death suppression: `EnemyHitReaction.HandleHit`
  early-returns on `_stats.IsDead`. Belt-and-suspenders against
  same-frame OnHit/OnDied ordering — without it, OnHit subscribers
  registered after EnemyDeath could queue a Hit trigger that
  arrives a frame after Death.

CharacterStatsRuntime.cs: extended with `using System;`, public
event `OnDied` (`Action<CharacterStatsRuntime>`), private
`_hasDied` flag, public `IsDead` property. ApplyDamage now sets
`_hasDied = true` BEFORE invoking OnDied (subscriber-safe — any
handler reading IsDead from inside the event handler sees the
post-death state). Single-fire — subsequent ApplyDamage calls
post-death do not re-raise. Heal does NOT revive: HP can rise
above 0 again via Heal, but `_hasDied` stays true and OnDied
does not re-fire (defensive default; revisit if revival semantics
ever ship). Added `[ContextMenu("Debug: Kill")]` hook that calls
`ApplyDamage(99999)`, tagged
`// TODO removeMe-after-real-damage-sources-kill-things` —
necessary because Dummy is at 999 HP and clicking Apply 10 Damage
100 times to trigger a death is intolerable.

EnemyBaseController.controller: extended via the existing
EnemyBaseControllerBuilder (still idempotent — delete + recreate).
Adds a `Death` Trigger parameter and a `Death` state with motion
= `Die01_SwordAndShield` (FBX filename + sub-asset name match —
no Shiled-typo issue on this clip; verified via .meta
clipAnimations entry). New transition AnyState→Death:
condition `Death` (If), hasExitTime=false, fixedDuration=true,
duration=0.05, canTransitionToSelf=false. Death has NO outgoing
transitions (terminal). Builder log line updated to enumerate
the new param/state/transition.

EnemyDeath.cs (NEW): namespace `LevelGen.Combat`.
`[RequireComponent(CharacterStatsRuntime)]`,
`[RequireComponent(Targetable)]`, `[DisallowMultipleComponent]`.
Three SerializeField references (animator, deathCollider,
hitReaction) auto-resolved on Reset() and re-resolved as
fallbacks in Awake(). Subscribes to `_stats.OnDied` in OnEnable,
unsubscribes in OnDisable. `HandleDied(_)` is `_hasFired`-guarded
(belt-and-suspenders against accidental re-subscription); on
first invoke runs the 5-step cleanup in order:
  1. `_targetable.enabled = false` — corpse is no longer a hit
     resolution target.
  2. `deathCollider.enabled = false` — corpse no longer blocks.
  3. `hitReaction.enabled = false` — releases the OnHit
     subscription cleanly.
  4. `animator.SetTrigger(DeathTriggerHash)` — plays Die01.
  5. `Destroy(gameObject, despawnDelay)` if `despawnDelay > 0f`.

EnemyHitReaction.cs: gained a `_stats` cached reference (resolved
in Awake) and an early-return `if (_stats != null && _stats.IsDead)
return;` at the top of HandleHit. Order matters: this runs BEFORE
the stagger-window gate, so a hit-while-dead never updates
`_lastHitTime` either. The `_stats` reference is null-guarded
because EnemyHitReaction does NOT [RequireComponent] StatsRuntime
(future enemies could plausibly have a hit reaction without HP).

DummyPrefabBuilder.cs: gained `EnemyDeath` AddComponent after the
EnemyHitReaction step, plus an `AssignEnemyDeathRefs` helper that
SerializedObject-wires all three field references in one call
(animator → MaleCharacterPBR child Animator; deathCollider →
the CapsuleCollider on the root added in M4-A; hitReaction →
the sibling EnemyHitReaction added in M4-A). Idempotent — the
clean-rebuild pattern means a fresh root every time, no
AddComponent guard needed.

Validator: `Assets/Scripts/Combat/Editor/EnemyDeathValidator.cs`
(menu `LevelGen ▶ Combat ▶ Validate EnemyDeath`) — 16 read-only
checks: OnDied event type, IsDead property type+getter,
EnemyDeath.cs presence, three RequireComponent + DisallowMultiple
attributes, EnemyBaseController Death param + state + AnyState→Death
canTransitionToSelf=false, Death-has-zero-outgoing-transitions
(terminal check), Dummy.prefab EnemyDeath presence + three
references wired (SerializedObject reads), EnemyHitReaction.cs
source contains `IsDead`, M4-A validator type+Run still resolvable
(belt-and-suspenders sanity — confirms M4-A surface didn't
disappear; user re-runs M4-A validator manually for full output).
Format mirrors EnemyHitReactionValidator.

Files:
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (event +
  IsDead + Kill ContextMenu + Heal-doesn't-revive guard)
- Assets/Scripts/Combat/EnemyDeath.cs (NEW)
- Assets/Scripts/Combat/EnemyHitReaction.cs (IsDead guard)
- Assets/Scripts/Combat/Editor/EnemyBaseControllerBuilder.cs
  (Death state + Death param + AnyState→Death transition)
- Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs (EnemyDeath
  AddComponent + AssignEnemyDeathRefs helper)
- Assets/Scripts/Combat/Editor/EnemyDeathValidator.cs (NEW)
- Assets/Animators/Enemy/EnemyBaseController.controller
  (rebuilt by builder — adds Death param/state/transition)
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab (rebuilt
  by `Build Dummy Prefab` after EnemyBaseController exists —
  gains EnemyDeath component with all three references wired)

Pending follow-up:
- User runs `LevelGen ▶ Combat ▶ Build EnemyBaseController` (adds
  the Death state via the rebuilt asset).
- User runs `LevelGen ▶ Combat ▶ Build Dummy Prefab` (re-binds
  the Animator to the new controller GUID and adds EnemyDeath).
- User runs `LevelGen ▶ Combat ▶ Validate EnemyDeath` — expect
  16 PASS / 0 FAIL.
- User re-runs the previous validators for sanity (Damage Routing
  12/12, EnemyHitReaction 14/14).
- Play-mode smoke test: walk to Dummy, right-click
  CharacterStatsRuntime → `Debug: Kill`. Expect Die01 plays,
  combo no longer flinches the corpse, player walks through
  where the Dummy stood, after 5s the GameObject vanishes.
  Re-place a fresh Dummy via `Place Dummy in Active Scene` —
  fresh one works correctly.

Deferred:
- Respawn-during-Play menu — not yet needed; manual Place re-runs
  cover the testing case.
- Loot drops, kill counters, death VFX.
- Player death (Player has its own HP/HUD; player-side OnDied
  hook is a separate milestone).
- Death-fade-into-floor or ragdoll handoff (Die01 holds last
  frame standing; visually OK for now).

## Next CC task

The procedural level generation pipeline is at a stable
checkpoint:
- V2 generator places rooms + halls in scenes (collision +
  PlayerSpawnPoint included via RoomBuilder).
- Player rig (M1 + M2-A + M2-B + M2-C) is shipped and validated.
- M3 pack swap (Duo → World Bundle) is closed.
- Project structure cleaned up (April 2026 reorg).

Next direction is open. Candidates include:
- M2-D level integration (LevelGenerator-driven runtime spawn).
- Death state / Attack04 / weapon-stance switching (small-scope
  additions to M2-B foundation).
- Enemy character (deferred from cleanup session).
- V2 generator door-geometry placement.
- Whitebox PieceCatalogue end-to-end test.

User picks at session start.

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

LVL_Configurator: complete; do not modify logic. Const-string
updates for folder reorg are the only acceptable touch.

Pending work (priority order):

V2 generator is on a stable checkpoint and not under active development.
Returning to Room Workshop next session — items below in priority order:

  1. Openings/doorway workflow (V1 failure point — primary V2
     Room Workshop focus)
  2. Tier stacking
  3. Room connection logic — door geometry vs. open passages
  4. Player integration — M1 + M2-A + M2-B + M2-C COMPLETE. M3
     pack swap COMPLETE (Duo→World Bundle, 2026-04-30). M2-D
     (level integration — Player_RuntimeRig refactor) remains.
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