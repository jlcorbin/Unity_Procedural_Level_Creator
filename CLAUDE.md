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
- Player death — shipped as M5 (2026-05-03 — see below).
- Death-fade-into-floor or ragdoll handoff (Die01 holds last
  frame standing; visually OK for now).

## M5 — player death (2026-05-03)

Mirrors M4-B for the Player. HP→0 plays Die01, disables
PlayerController + PlayerCombat, raises PlayerDeath.OnPlayerDied
for UI to subscribe to. PlayerDeathOverlay shows a "You Died"
overlay with a Restart button that reloads the active scene.
Player corpse stays in the scene until Restart (no Destroy —
deliberate divergence from EnemyDeath's despawn).

Architectural decisions (locked):
- Animator: `PlayerBaseController` gains a terminal `Death` state
  via PlayerBaseControllerExtender (idempotent additive editor
  script — does NOT rebuild the existing controller; preserves
  all prior M2-B/M2-C state and transitions). AnyState→Death
  trigger transition, canTransitionToSelf=false. Death has NO
  outgoing transitions; Animator parks on the last frame.
- Animator-writer invariant: `PlayerAnimator` is still the SOLE
  writer to Player Animator parameters. New `SetDeathTrigger()`
  public method follows the existing SetHitTrigger /
  SetAttackTrigger / SetJumpTrigger / SetComboNext pattern
  (`_ready`-gated, hash-cached in Awake).
- Cleanup ownership: `PlayerDeath` MonoBehaviour subscribes to
  `CharacterStatsRuntime.OnDied`; on first fire disables
  `PlayerController` + `PlayerCombat`, calls
  `_animator.SetDeathTrigger()`, raises its own
  `OnPlayerDied(this)` event for UI subscribers.
- Input handling: `PlayerInputReader` keeps raising events post-
  death; the disabled `PlayerController` / `PlayerCombat`
  components silently ignore them. Restart button is a Unity
  UGUI button (not InputSystem-driven), so disabling
  PlayerController / PlayerCombat does NOT block clicks.
- Restart: `SceneManager.LoadScene(activeScene.buildIndex)`.
  TODO comment in `PlayerDeathOverlay.OnRestartClicked` flags
  in-place respawn as the future-respawn architecture
  alternative.
- Cursor: `MouseLook` locks at Play start; `PlayerDeathOverlay`
  unlocks explicitly when the overlay shows so the Restart button
  is clickable. Scene reload runs `MouseLook.OnEnable` again,
  re-locking normally.

PlayerBaseControllerExtender.cs (NEW, editor):
  - Loads existing PlayerBaseController (does NOT recreate, so
    M2-B work survives).
  - Adds `Death` Trigger param if missing.
  - Adds `Death` state with motion = Die01_SwordAndShield if
    missing (FBX filename + sub-asset name match — no Shiled
    typo on this clip).
  - Adds AnyState→Death transition if missing
    (canTransitionToSelf=false, hasExitTime=false, dur 0.05).
  - Idempotent — re-running with everything present is the
    "all skipped" green state.

PlayerAnimator.cs: extended with `ParamDeath` const, `_hashDeath`
field, hash assignment in Awake, public `SetDeathTrigger()`
method. Pattern matches existing trigger-set methods identically.

PlayerDeath.cs (NEW): namespace `LevelGen.Player`.
`[RequireComponent(CharacterStatsRuntime)]`,
`[DisallowMultipleComponent]`. Three SerializeField references
(_animator, _controller, _combat) with both Reset() auto-resolve
(editor-only) and Awake fallbacks (runtime). Subscribes to
`_stats.OnDied` in OnEnable, unsubscribes in OnDisable.
`HandleDied(_)` is `_hasFired`-guarded; on first invoke runs:
  1. `_controller.enabled = false` — movement input ignored.
  2. `_combat.enabled = false` — attack input ignored.
  3. `_animator.SetDeathTrigger()` — queues Death.
  4. `OnPlayerDied?.Invoke(this)` — UI subscribers see post-
     cleanup state with trigger queued.
Public `event Action<PlayerDeath> OnPlayerDied` and bool
`HasFired` accessor.

PlayerCombat.cs: gained `_stats` cached reference (resolved in
Awake, null-tolerant — no [RequireComponent] for
CharacterStatsRuntime, mirrors EnemyHitReaction's pattern). New
guard at top of `TakeHit()`:
  `if (_stats != null && _stats.IsDead) return;`
Belt-and-suspenders against same-frame OnHit/OnDied subscriber
ordering — without it, a hit landing in the same frame as HP→0
could queue a flinch right before Death plays.

PlayerDeathOverlay.cs (NEW, `LevelGen.UI`): passive observer.
`[DisallowMultipleComponent]`. Lives on the prefab root with the
Canvas as a child (so SetActive(false) on the canvas doesn't
disable the overlay's OnPlayerDied subscription). Tag-based
player lookup with retry coroutine (copies `PlayerHUD`'s
TryBindToPlayer + PollForPlayer pattern verbatim). `HandlePlayerDied`
shows the canvas, unlocks cursor. `OnRestartClicked` reloads
active scene via `SceneManager.LoadScene(activeScene.buildIndex)`.

Restart input handling — three layers, in priority order:
1. Keyboard fallback in `Update`: R / Enter / Numpad-Enter while
   the canvas is active call `OnRestartClicked` directly. Works
   regardless of EventSystem state.
2. Manual mouse-over-button check in `Update`: when the left
   mouse button is pressed and the cursor is inside the
   RestartButton's RectTransform (resolved via
   `RectTransformUtility.RectangleContainsScreenPoint` with
   null camera, since the canvas is ScreenSpaceOverlay), call
   `OnRestartClicked`. Bypasses the EventSystem +
   InputSystemUIInputModule dispatch chain entirely.
3. Standard UGUI Button.onClick — works only when the scene has
   an EventSystem with `InputSystemUIInputModule` AND the module
   has UI actions bound (the editor's
   `GameObject ▶ UI ▶ Event System` flow auto-assigns
   `DefaultInputActions.inputactions`; programmatic
   `AddComponent<InputSystemUIInputModule>()` leaves the actions
   asset null, so the module is alive but inert).

The triple-redundant input was necessary: during M5 verification
the user's first-Play attempt couldn't click the button. The
diagnosis chain went: missing EventSystem → wrong input module →
no actions bound. The mouse-over-RectTransform fallback is the
bulletproof layer; the keyboard fallback covers gamepad / quick-
test cycles. EventSystem auto-add in the Place menu and a
runtime `EnsureRuntimeEventSystem` in `Awake` are still present
as best-effort layers but no longer load-bearing.

PlayerDeathOverlay.prefab (NEW): Canvas root, ScreenSpaceOverlay,
sortingOrder=100 (above PlayerHUD's sortingOrder=10). Backdrop
(75% black full-screen Sliced Image), "You Died" TMP_Text
(96pt bold red), Restart button (240×64 px). Built via
`PlayerDeathOverlayBuilder.cs` editor (BUILD + Place menus,
mirrors PlayerHUDBuilder). Sprite-fix lesson from PlayerHUD
carried forward — backdrop assigns `UI/Skin/UISprite.psd`.

Player_MaleHero.prefab: gains `PlayerDeath` component on root,
all three field references wired via SerializedObject. Two
authoring paths:
  - `LevelGen ▶ Player ▶ Build Player_MaleHero Prefab` (folded
    into the rebuild flow per M4-A "fold authoring into the
    main builder" lesson — internal helper
    `PlayerPrefabBuilder.AssignPlayerDeathRefs`).
  - `LevelGen ▶ Player ▶ Add PlayerDeath to Player_MaleHero
    Prefab` (NEW, `PlayerDeathPrefabAdder.cs`) — one-shot
    LoadPrefabContents path so the user can ship M5 without
    re-running the full prefab build. Idempotent. Warns clearly
    if PlayerCombat is missing (user must run
    PlayerCombatPrefabAdder first, then re-run this menu to
    wire `_combat`).

Validator: `Assets/Scripts/Player/Editor/PlayerDeathValidator.cs`
(menu `LevelGen ▶ Player ▶ Validate Player Death`) — 16 read-only
checks: PlayerDeath.cs presence, RequireComponent + DisallowMultiple,
OnPlayerDied event signature, PlayerAnimator.SetDeathTrigger
public surface, PlayerAnimator source contains "Death" hash,
PlayerBaseController Death param + state + terminal (zero
outgoing transitions) + AnyState→Death canTransitionToSelf=false,
PlayerCombat.TakeHit body contains IsDead reference (slice-search),
Player_MaleHero.prefab PlayerDeath presence + three refs wired
(SerializedObject reads), PlayerDeathOverlay script + prefab exist.
Format mirrors EnemyDeathValidator.

Files:
- Assets/Scripts/Player/PlayerAnimator.cs (Death hash + param +
  SetDeathTrigger)
- Assets/Scripts/Player/PlayerCombat.cs (cached _stats + IsDead
  guard at top of TakeHit)
- Assets/Scripts/Player/PlayerDeath.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerBaseControllerExtender.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerDeathPrefabAdder.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerDeathValidator.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs
  (PlayerDeath fold-in + AssignPlayerDeathRefs helper)
- Assets/Scripts/UI/PlayerDeathOverlay.cs (NEW)
- Assets/Scripts/UI/Editor/PlayerDeathOverlayBuilder.cs (NEW)
- Assets/Animators/Player/PlayerBaseController.controller
  (extended via PlayerBaseControllerExtender — Death param +
  state + AnyState→Death transition added; M2-B/M2-C state
  preserved)
- Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
  (gains PlayerDeath via either build path or standalone adder)
- Assets/Prefabs/UI/PlayerDeathOverlay.prefab (NEW, produced by
  PlayerDeathOverlayBuilder)

Pending follow-up:
- User runs `LevelGen ▶ Player ▶ Extend PlayerBaseController
  (M5 Death)` — adds Death param/state/transition to the
  controller in place.
- User runs `LevelGen ▶ UI ▶ Build PlayerDeathOverlay Prefab`.
- User runs `LevelGen ▶ Player ▶ Add PlayerDeath to
  Player_MaleHero Prefab` (or rebuilds the prefab via
  Build Player_MaleHero Prefab — both paths work).
- User runs `LevelGen ▶ UI ▶ Place PlayerDeathOverlay in Active
  Scene` (test scene).
- User runs `LevelGen ▶ Player ▶ Validate Player Death` —
  expect 16 PASS / 0 FAIL.
- Sanity re-runs all prior validators (DamageRouting 12/12,
  PlayerHUD 11/11, Combat Foundation 12/12, EnemyHitReaction
  14/14, EnemyDeath 16/16, MouseLook 7/7).
- Play-mode smoke test: enter Play, walk near Dummy, right-click
  CharacterStatsRuntime on Player_MaleHero in Hierarchy →
  `Debug: Kill`. Expect Die01 plays + parks on last frame, WASD
  / mouse / left-click silent, "You Died" overlay appears,
  cursor unlocks, click Restart → scene reloads → fresh run with
  HP 100/100.

Deferred:
- In-place respawn (TODO comment in
  PlayerDeathOverlay.OnRestartClicked — needs spawn-point
  architecture / respawn semantics).
- Death VFX / SFX / camera effects.
- Game over UI polish (death cause, kill counts, etc.).
- Sources of damage to the Player (no enemy AI yet — verify via
  the existing `[ContextMenu("Debug: Kill")]` hook).

## M6 — player interact system + AssassinateInteractable (2026-05-03)

Generic prefab-friendly Interact system. Press E to trigger
contextual actions on nearby Interactables. Ships one concrete
subclass — AssassinateInteractable — wired to the Dummy as the
proof-of-concept. Architecture is ready for Open / Pickup /
Read subclasses to be added later with no edits to the player
or system core.

Note on milestone numbering: this is the SECOND milestone shipped
on 2026-05-03 — the first was Player Death (logged as M5 above).
The user's prompt called this "M5 — Player Interact System"; the
header was bumped to M6 here to avoid a duplicate `## M5` heading.
The milestone log uses M6 going forward.

Architectural decisions (locked):
- Self-registration: each Interactable carries its own trigger
  collider. OnTriggerEnter / Exit cache the player ref;
  per-frame Update re-evaluates IsEligible and edge-detects
  register / deregister flips. PlayerInteractor never polls the
  world.
- World-space prompt UI: each Interactable owns a child Canvas
  (built idempotently by `EnsurePromptUI`) at its
  `_promptAnchor` Transform. PlayerInteractor toggles only the
  active interactable's prompt visibility.
- Damage routing for Assassinate: NOT a sideways write. The
  override flows through PlayerCombat's existing
  Animator-trigger-and-hitbox path. AssassinateInteractable sets
  a one-shot `_nextHitDamageOverride`, fires `RequestAttack()`
  (delegates to the same OnAttackPressed handler as a manual
  LMB press), and PlayerCombat.NotifyHitboxTriggered consumes
  the override on the first hitbox-target intersection.
  PlayerCombat.attackDamage stays the SerializeField default
  (10) for normal swings.
- InteractPriority enum: `Pickup=10`, `Open=50`,
  `Assassinate=100`. PlayerInteractor picks highest-priority
  registered Interactable; same-priority ties resolve to first-
  registered (documented but not relied on).
- PlayerInteractor singleton: `static Instance` set in Awake,
  cleared in OnDestroy. Justified because Interactables can't
  trust a player ref at trigger-enter time (the trigger collider
  may have been entered by a child of the player; resolving the
  tagged ancestor and routing through a known receiver is the
  cleanest pattern). Project convention upgraded from "no cross-
  cutting singletons" to "two cross-cutting receivers": MouseLook's
  `_MouseLock` GameObject (per-scene) and PlayerInteractor's
  `Instance` (per-prefab on Player_MaleHero).

Single-direction dependency preserved:
  PlayerInputReader → (event InteractPressed) → PlayerInteractor
  → (call) → Interactable.Execute → (call) → PlayerCombat.RequestAttack
  + PlayerCombat.SetNextHitDamageOverride.
PlayerCombat is the only writer to the Animator parameters
(SetAttackTrigger fires through PlayerAnimator.SetAttackTrigger).
AssassinateInteractable does NOT touch the Animator directly.

Interactable.cs (NEW, `LevelGen.Interaction`): abstract base.
`[DisallowMultipleComponent]`. SerializeFields: `_priority`
(InteractPriority), `_promptLabel` (string), `_promptAnchor`
(Transform), `_playerTag` (string, default "Player"). Abstract:
`IsEligible(GameObject)`, `Execute(GameObject)`. Concrete:
`Reset` / `Awake` / `OnTriggerEnter` / `OnTriggerExit` / `Update`
/ `OnDisable` / `Register` / `Deregister` / `ReevaluateRegistration`
/ `SetPromptVisible` / `EnsurePromptUI`. The OnTriggerEnter
walks the hierarchy from the colliding transform to find a
tagged ancestor (the Player's tag is on the prefab root, but
the trigger may collide with a child). EnsurePromptUI builds a
child `_InteractPrompt` GameObject containing a World Space
Canvas (scale 0.01) and a TMP_Text label rendering "Press [E] {label}".

PlayerInteractor.cs (NEW, `LevelGen.Player`):
`[RequireComponent(PlayerInputReader)]`,
`[DisallowMultipleComponent]`, static `Instance` property.
HashSet<Interactable> _registered; Interactable _active. On
register/deregister recompute the active by max priority; if
active changes, hide old prompt + show new. OnInteractPressed
dispatches Execute on _active. Subscribes to PlayerDeath.OnPlayerDied
via Awake-cached ref; on player death clears all registrations
+ hides any active prompt (belt-and-suspenders, since
PlayerDeath disables PlayerController/PlayerCombat by default
but not PlayerInteractor).

PlayerInputReader.cs: extended with `event System.Action InteractPressed`
alongside existing AttackPressed/JumpPressed. OnInteract
endpoint now raises the event on `ctx.performed` and the M1
stub log is removed (mirrors the OnAttack / OnJump pattern from
M2-B Step 3 / M2-B Step 5). M1-stub logs preserved on Crouch /
Previous / Next — those wait for their own consumers.

PlayerCombat.cs: four additive surface changes, no refactor.
  - private int `_nextHitDamageOverride = -1` field.
  - public `IsBusy => IsActionLocked` alias property (lets
    Interactables query without exposing the private state-hash
    constants).
  - public `SetNextHitDamageOverride(int)` setter.
  - public `RequestAttack()` that delegates to the existing
    private `OnAttackPressed` handler. Lets external callers
    fire the player's Attack as if LMB had been pressed.
  - Inside `NotifyHitboxTriggered`, the override is consumed
    AFTER stats / hit-list checks (so the warning + already-hit
    branches don't burn it). Override is single-shot — cleared
    on first successful application. Debug log includes
    `(override)` tag when an override was used.

AssassinateInteractable.cs (NEW, `LevelGen.Interaction`):
`[RequireComponent(SphereCollider)]`. Subclasses Interactable.
SerializeFields: `_targetStats` (CharacterStatsRuntime,
auto-resolved on Reset), `_targetTransform`, `_backArcDot`
(default 0.5 ≈ 60° back arc), `_assassinateDamage` (int,
default 99999). Reset() sets `_priority = Assassinate`,
`_promptLabel = "Assassinate"`, anchors prompt to the target's
transform, configures the SphereCollider as a trigger with
radius 1.5. IsEligible: target alive AND `Vector3.Dot(target.forward,
toPlayer) < -_backArcDot`. Execute: snap-rotate the player to
face the target, set the damage override, call
`combat.RequestAttack()`. Drops silently if `combat.IsBusy` is
true (mid-Attack/Hit).

DummyPrefabBuilder.cs: extended to add a `_AssassinateZone`
child after the EnemyDeath component step. Zone gains a
SphereCollider (trigger, radius 1.5, center (0, 0.9, 0)) +
AssassinateInteractable + a `_PromptAnchor_Head` grandchild at
local (0, 1.9, 0). Three SerializeField refs on
AssassinateInteractable wired explicitly via SerializedObject:
`_targetStats` → root's CharacterStatsRuntime, `_targetTransform`
→ root, `_promptAnchor` → the head anchor. EnsurePromptUI
called at build time so the prompt child is visible in the
prefab inspector (otherwise it's only built on Awake).
Idempotent within the existing clean-rebuild pattern (always
called on a fresh root).

Player_MaleHero.prefab: gains PlayerInteractor on root.
PlayerInteractor has no SerializeField references (resolves
PlayerInputReader + PlayerDeath via GetComponent in Awake), so
no explicit field wiring is needed. Two authoring paths:
  - `LevelGen ▶ Player ▶ Build Player_MaleHero Prefab` (folded
    in alongside the M5 PlayerDeath fold-in).
  - `LevelGen ▶ Player ▶ Add PlayerInteractor to Player_MaleHero
    Prefab` (NEW, `PlayerInteractorPrefabAdder.cs`) — one-shot
    LoadPrefabContents path so the user can ship M6 without
    re-running the full prefab build. Idempotent.

Validator: `Assets/Scripts/Interaction/Editor/InteractSystemValidator.cs`
(menu `LevelGen ▶ Interaction ▶ Validate Interact System`) —
16 read-only checks: Interactable.cs presence, Interactable
abstract + abstract API, InteractPriority values, PlayerInteractor.cs
+ static Instance, RequireComponent(PlayerInputReader),
PlayerInputReader InteractPressed event declared,
PlayerInputReader.OnInteract no longer logs (M1 stub removed),
PlayerCombat surface (override + setter + RequestAttack +
IsBusy via source-scan), AssassinateInteractable subclasses
Interactable, AssassinateInteractable RequireComponent(SphereCollider),
Player_MaleHero.prefab tag = "Player", PlayerInteractor on
Player_MaleHero, _AssassinateZone child + script on Dummy,
SphereCollider isTrigger=true + radius>0, _targetStats wired,
_promptAnchor wired. PASS / FAIL / SKIP format mirrors
EnemyDeathValidator.

Files:
- Assets/Scripts/Interaction/Interactable.cs (NEW)
- Assets/Scripts/Interaction/AssassinateInteractable.cs (NEW)
- Assets/Scripts/Interaction/Editor/InteractSystemValidator.cs (NEW)
- Assets/Scripts/Player/PlayerInteractor.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerInteractorPrefabAdder.cs (NEW)
- Assets/Scripts/Player/PlayerInputReader.cs (InteractPressed
  event + OnInteract stub log removed)
- Assets/Scripts/Player/PlayerCombat.cs (override field +
  setter + RequestAttack + IsBusy + consume in
  NotifyHitboxTriggered)
- Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs
  (PlayerInteractor fold-in alongside M5's PlayerDeath fold-in)
- Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs
  (_AssassinateZone child + BuildAssassinateZone +
  AssignAssassinateRefs helpers)
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab (gains
  _AssassinateZone child via builder rebuild)
- Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
  (gains PlayerInteractor via either build path or standalone
  adder)

Verification (complete):
- `LevelGen ▶ Interaction ▶ Validate Interact System` — 16 PASS / 0 FAIL.
- Play-mode smoke test: confirmed working in test scene
  (assassinate prompt appears behind Dummy, disappears in front,
  E-press snap-rotates and kills via 99999-damage override).

Validator first-run fix (2026-05-03, post-shipping): check 7
("OnInteract no longer logs (M1 stub removed)") used a
fixed-width 400-char source slice from `public void OnInteract`.
Adjacent M1-stub sibling methods (OnCrouch / OnPrevious / OnNext)
sit close enough that the slice spilled past OnInteract's
closing brace and matched OnCrouch's still-present `Debug.Log`,
producing a false positive even though OnInteract was clean.
Patched to direct-string match against the literal stub line
`Debug.Log("[PlayerInputReader] Interact"`. Lesson logged:
fixed-width slice scans on source code spill across method
boundaries when methods are short — for "is this specific stub
gone" checks, prefer direct string match against the literal
stub line over slice scans.

Deferred:
- Concrete subclasses: OpenInteractable (doors),
  PickupInteractable (books, items), ReadInteractable (signs),
  ActivateInteractable (levers).
- World-space prompt billboarding toward camera (TODO comment
  in Interactable.EnsurePromptUI). Currently faces +Z which is
  acceptable from the existing camera angle.
- Hold-to-interact / charge-up mechanics.
- Per-key remapping in HUD ("[E]" is hardcoded in the prompt
  string).
- Distance-based tiebreaker if same-priority interactables
  overlap (currently registration-order via HashSet enumeration —
  documented as "first-registered" but enumeration order isn't
  formally guaranteed for HashSet).
- Stealth detection (alerted enemy → assassinate ineligible).
- Snap-to-position on Execute (currently only snap-rotates;
  player position untouched).

## M7 — OpenInteractable + TestDoor (2026-05-04)

Second concrete `Interactable` subclass. Generic open/close
behavior — designed for doors, gates, chests, drawers, lids —
with a procedurally-built test door (`TestDoor.prefab`) as a
stand-in for verifying the loop without depending on level-gen
work. M16 will eventually wire `OpenInteractable` onto real FDP
`COMP_Door_*` prefabs; until then the test door lives in
`Assets/Prefabs/TestRig/` as a diagnostic.

Architectural decisions (locked):
- TestDoor is a procedural primitive (cube on a hinge), NOT an
  FDP prefab. M7 is player/interaction only — no level-gen
  touch.
- Toggleable: first press opens (+90° lerp over 0.4s), second
  press closes (-90°). `OpenInteractable` carries an `_isOpen`
  bool and updates `_promptLabel` ("Open" ↔ "Close") whenever
  state flips.
- Coroutine lerp on the `OpenInteractable` MonoBehaviour. While
  the coroutine is in flight, `_isAnimating = true` and
  `IsEligible` returns false (so the prompt hides during the
  swing). `Execute` early-returns on `_isAnimating == true` as
  defense-in-depth against same-frame double-press.
- Target rotations cached in Awake (`_closedRotation`,
  `_openRotation` from the hinge's initial localRotation).
  Re-deriving each cycle would compound floating-point drift
  across many toggles — caching guarantees the door always
  returns to exactly the same closed orientation.

Invariants preserved:
- Single-direction dependency holds. PlayerInteractor calls
  `interactable.Execute(player)`; OpenInteractable runs its
  coroutine on itself; no callbacks to player-side code.
- Interactable abstract base remains a marker-plus-subclass
  pattern. M7 does NOT modify the base contract; it only
  generalizes `_promptLabel` from "fixed at inspector time" to
  "mutable + reflected to Canvas via RefreshPromptLabel()."
  Purely additive — the field was already `protected`, so
  subclasses could already mutate it; the new helper just gives
  them an idempotent way to push the change to the visible TMP.
- AssassinateInteractable, PlayerInteractor, PlayerCombat,
  PlayerInputReader, animator controllers, and character
  prefabs all unchanged.

Interactable.cs: three surgical additive changes, no removals.
  - `_promptLabel` doc-comment expanded ("subclasses may mutate
    at runtime; call RefreshPromptLabel() to push the change").
  - New public `RefreshPromptLabel()` method: pushes the current
    `_promptLabel` to the cached TMP_Text. Idempotent. Cheap —
    text update only, does NOT rebuild the Canvas.
  - `EnsurePromptUI()` now calls `RefreshPromptLabel()` at both
    exit paths (the early-return-on-existing-child path and the
    after-build path). Keeps the build path and the re-find
    path symmetric — a subclass that mutates `_promptLabel`
    after the prompt UI was first built will see the new text
    on the next `EnsurePromptUI()` or `SetPromptVisible(true)`
    call.
  - `SetPromptVisible(true)` simplified to call
    `RefreshPromptLabel()` instead of duplicating the text-set
    code inline.

OpenInteractable.cs (NEW, `LevelGen.Interaction`):
`[RequireComponent(SphereCollider)]`. Subclasses Interactable.
SerializeFields:
  - `_hinge` (Transform, auto-resolved on Reset to `transform`)
  - `_rotationAxis` (Vector3, default Vector3.up)
  - `_openAngle` (float, default 90°)
  - `_animationDuration` (float, default 0.4s)
  - `_openLabel` (string, default "Open")
  - `_closeLabel` (string, default "Close")
Reset: `_priority = InteractPriority.Open`, `_promptLabel = _openLabel`,
SphereCollider configured as trigger r=1.5. Awake: caches
`_closedRotation` and `_openRotation` from the hinge's initial
localRotation BEFORE base.Awake builds the prompt UI; sets
`_promptLabel` from `_isOpen` then calls `base.Awake()` which
calls `EnsurePromptUI` which calls `RefreshPromptLabel`.
IsEligible: returns false during animation (so the prompt hides
during the swing). Execute: early-returns on `_isAnimating`,
otherwise starts `AnimateRotation` coroutine. Coroutine:
smoothstep ease-in-out (`t * t * (3 - 2t)`), Quaternion.Slerp
between cached from/to rotations, on completion flips `_isOpen`,
swaps `_promptLabel` to the other side, calls `RefreshPromptLabel`,
clears `_isAnimating`. Update tick re-registers automatically
(IsEligible flips back to true).

TestDoorBuilder.cs (NEW, `LevelGen.Interaction.Editor`): two
menu items.
  - `LevelGen ▶ Interaction ▶ Build TestDoor Prefab` —
    idempotent rebuild. Hierarchy:
    ```
    TestDoor (root)
    ├── HingePivot (empty)
    │   └── DoorLeaf (Cube primitive, scale 1×2×0.1m,
    │                 localPosition (0.5, 1, 0) so HingePivot
    │                 sits at the hinge edge)
    └── _OpenZone
        ├── SphereCollider (trigger, r=1.5, center (0.5, 1, 0))
        └── OpenInteractable (_hinge → HingePivot,
                              _promptAnchor → HingePivot)
    ```
    Material: URP/Lit (project standard per
    WhiteboxPackFactory pattern), brown
    `Color(0.6f, 0.4f, 0.2f)` via `_BaseColor`. Falls back with
    a warning if URP/Lit shader not found. The Cube primitive's
    own BoxCollider stays as a non-trigger physical collider so
    the player can't walk through the closed door.
    Wires `_hinge` + `_promptAnchor` via SerializedObject (Reset
    doesn't fire on programmatic AddComponent — M6 lesson #3).
    EnsurePromptUI called at build time so the prompt child is
    visible in the prefab inspector.
  - `LevelGen ▶ Interaction ▶ Place TestDoor in Active Scene` —
    instantiates at world (0, 0, 3). Skips if a TestDoor is
    already in the scene.
Save path: `Assets/Prefabs/TestRig/TestDoor.prefab`. The
`TestRig` folder is created on first build; its name signals
"diagnostic, not gameplay" so it stays segregated when M16
ships real doors.

Validator: `Assets/Scripts/Interaction/Editor/OpenInteractableValidator.cs`
(menu `LevelGen ▶ Interaction ▶ Validate OpenInteractable`) —
12 read-only checks: OpenInteractable.cs presence, subclass
relationship, RequireComponent(SphereCollider), source contains
`_openLabel` + `_closeLabel` (literal-stub matches per M6
lesson #2 — direct String.Contains, no fixed-width slice),
source contains `AnimateRotation` coroutine, Interactable base
declares `RefreshPromptLabel` public method (reflection),
Interactable.cs source contains both the declaration and a
call site for `RefreshPromptLabel` (covers the EnsurePromptUI
hookup), TestDoor.prefab presence, _OpenZone child presence,
SphereCollider trigger=true + radius>0, OpenInteractable
component on _OpenZone, `_hinge` field non-null
(SerializedObject read).

Files:
- Assets/Scripts/Interaction/Interactable.cs (additive:
  RefreshPromptLabel + EnsurePromptUI hooks)
- Assets/Scripts/Interaction/OpenInteractable.cs (NEW)
- Assets/Scripts/Interaction/Editor/TestDoorBuilder.cs (NEW)
- Assets/Scripts/Interaction/Editor/OpenInteractableValidator.cs (NEW)
- Assets/Prefabs/TestRig/TestDoor.prefab (NEW, produced by
  TestDoorBuilder)

No modifications to: AssassinateInteractable, PlayerInteractor,
PlayerCombat, PlayerInputReader, any animator controller, any
character prefab.

Pending follow-up:
- User runs `LevelGen ▶ Interaction ▶ Build TestDoor Prefab`.
- User runs `LevelGen ▶ Interaction ▶ Place TestDoor in Active Scene`.
- User runs `LevelGen ▶ Interaction ▶ Validate OpenInteractable`
  — expect 12 PASS / 0 FAIL.
- Sanity re-runs of all prior validators (DamageRouting 12/12,
  PlayerHUD 11/11, Combat Foundation 12/12, EnemyHitReaction
  14/14, EnemyDeath 16/16, MouseLook 7/7, PlayerDeath 16/16,
  InteractSystem 16/16) — none should regress.
- Play-mode smoke test: walk near test door → "Press [E] Open"
  appears; E swings the door open over 0.4s with ease-in-out;
  prompt re-appears as "Press [E] Close"; E swings closed; spam
  E mid-swing is silently dropped; walking away mid-swing lets
  the door finish; combo cycles stable. Walk to Dummy + door at
  the same time to verify priority (Assassinate=100 > Open=50)
  picks the right one.

Deferred:
- M16 will replace TestDoor with FDP `COMP_Door_*` prefabs
  wired to OpenInteractable.
- Locked / unlocked door states (key check on IsEligible).
- Door SFX / VFX.
- Per-side eligibility (open from front only).
- Bidirectional doors with separate "Open from front" / "Open
  from back" eligibility.
- Multiple-press chest opening / "hold to open" mechanics.
- Per-frame Canvas billboard toward Camera.main (still TODO in
  Interactable.EnsurePromptUI from M6).

## M8 — Damage numbers / floating combat text (2026-05-04)

First real consumer of the `Targetable.OnHit` event payload
introduced in M4-A as a "future hook for knockback / VFX
subscribers." Cosmetic-only — no game logic changes. World-
space TMP_Text drifts upward and fades out on every hit, scene-
wide, automatically picking up new Targetables spawned at
runtime.

Architectural decisions (locked):
- Event payload extended from `Action<Vector3>` to
  `Action<Vector3, float>` — hit point + damage value. The
  existing subscriber `EnemyHitReaction.HandleHit` updates its
  signature and ignores the new param.
  `PlayerCombat.NotifyHitboxTriggered` passes the actual damage
  applied (post-override consumption) to RaiseHit. The damage
  number reflects what landed — including assassinate's 99999
  override.
- New static event `Targetable.AnyTargetableHit(Vector3, float)`
  fires alongside the instance event from `RaiseHit`.
  `DamageNumberSpawner` subscribes once to the static event;
  every Targetable in the scene (including ones spawned at
  runtime) routes through it automatically. Subscribers MUST
  unsubscribe in `OnDisable` — static event lifetime survives
  domain reloads.
- `DamageNumberSpawner` is a singleton-scoped manager
  (`static Instance`). Third project singleton after MouseLook
  (`_MouseLock`) and PlayerInteractor. Per-scene scope, no
  DontDestroyOnLoad — scene reload via Restart resets it
  cleanly. Duplicates self-destroy in Awake with warning.
- `DamageNumber` prefab is a bare GameObject + `TextMeshPro`
  component (NOT inside a Canvas — that would force ScreenSpace
  + RectTransform sizing). The TMP component on a no-Canvas
  GameObject renders via the world-space mesh-renderer path.
- No object pooling. Each DamageNumber is its own GameObject;
  pool only if profiling shows GC hitches. Optimization without
  measurement is technical debt.
- No camera billboarding. The current near-top-down camera
  makes static-orientation text legible. `transform.LookAt(
  Camera.main)` per frame would couple every DamageNumber to
  Camera.main and add a per-frame Update we don't need.

Invariants preserved:
- Single-direction dependency holds. PlayerCombat is the
  damage-application site; Targetable raises events;
  EnemyHitReaction + DamageNumberSpawner are pure consumers.
- Single-writer-per-Animator-parameter unaffected — M8 doesn't
  touch animators.

Targetable.cs: extended from "marker + event publisher (one
event)" → "marker + event publisher (instance + static fan-out)".
Event signature `Action<Vector3>` → `Action<Vector3, float>`.
RaiseHit signature `(Vector3)` → `(Vector3, float)`. RaiseHit
body now invokes both `OnHit?.Invoke(...)` and
`AnyTargetableHit?.Invoke(...)`. Class XML doc updated to
describe both event surfaces and warn about static-event leak
prevention.

PlayerCombat.cs: one-line surgical change in
`NotifyHitboxTriggered`:
  `targetable.RaiseHit(hitPoint)` → `targetable.RaiseHit(hitPoint, dmg)`
The `dmg` local was already computed at the call site (post-
override consumption), so no surrounding refactor needed. The
damage number reflects the actual damage dealt — assassinate
shows "99999", normal swing shows "10".

EnemyHitReaction.cs: `HandleHit(Vector3 hitPoint)` →
`HandleHit(Vector3 hitPoint, float damage)`. Body unchanged
beyond a comment noting the float param is intentionally
ignored (Hit reaction doesn't care about damage value, just
that a hit happened). The `OnEnable`/`OnDisable` subscriptions
type-checked against the new delegate type automatically — no
change needed there.

DamageNumber.cs (NEW, `LevelGen.UI`): `[RequireComponent(TMP_Text)]`,
`[DisallowMultipleComponent]`. SerializeFields: `_lifetime` (1.0s),
`_riseDistance` (1.5 units), `_color` (white). `Initialize(Vector3
worldPosition, float damage)` sets transform.position, writes
text via `damage.ToString("0")` (integer-rounded display — "10"
not "10.0"), starts the rise+fade coroutine. Coroutine uses
smoothstep ease (`t * t * (3 - 2t)`, same curve as
OpenInteractable's door swing) to drift up `_riseDistance` units
while fading alpha 1→0. Self-destroys at lifetime end.

DamageNumberSpawner.cs (NEW, `LevelGen.UI`):
`[DisallowMultipleComponent]`, static `Instance`. SerializeFields:
`_damageNumberPrefab` (DamageNumber, wired by builder),
`_spawnParent` (Transform, defaults to self). Awake sets Instance
+ duplicate-self-destroy guard. OnEnable/OnDisable pair subscribes
+ unsubscribes `Targetable.AnyTargetableHit += HandleAnyHit`.
HandleAnyHit instantiates one DamageNumber per call, routes
through Initialize. Null-tolerant on `_damageNumberPrefab`
(silent drop, no warn-spam).

Pattern carryforward: subscribe in OnEnable / unsubscribe in
OnDisable matches EnemyHitReaction's pattern. The static-event
unsubscribe is load-bearing — without it, the next domain reload's
spawner would fire alongside this destroyed one, doubling numbers
per hit.

Builder:
`Assets/Scripts/UI/Editor/DamageNumberBuilder.cs` —
three menu items:
  - `LevelGen ▶ UI ▶ Build DamageNumber Prefab` (creates a bare
    GameObject + TextMeshPro with fontSize=6 white-bold-with-
    black-outline text, alignment=Center, NOT wrapped in a
    Canvas — world-space mesh-renderer rendering. Saved to
    `Assets/Prefabs/UI/DamageNumber.prefab`. Idempotent
    delete+recreate.)
  - `LevelGen ▶ UI ▶ Build DamageNumberSpawner Prefab` (creates
    DamageNumberSpawner GameObject, wires `_damageNumberPrefab`
    via SerializedObject — Reset() doesn't fire on programmatic
    AddComponent, M6 lesson #3. Saved to
    `Assets/Prefabs/UI/DamageNumberSpawner.prefab`. Aborts if
    DamageNumber prefab missing or has no DamageNumber component.)
  - `LevelGen ▶ UI ▶ Place DamageNumberSpawner in Active Scene`
    (instantiates at world origin. Idempotent — selects existing
    if a DamageNumberSpawner already exists in scene.)

Validator:
`Assets/Scripts/UI/Editor/DamageNumberValidator.cs`
(menu `LevelGen ▶ UI ▶ Validate Damage Numbers`) — 14 read-only
checks: Targetable.cs source-scan for new event declaration +
static event + RaiseHit body invokes both events (literal-stub
matches per M6 lesson #2 — direct String.Contains, no slice),
RaiseHit reflection check `(Vector3, float)`,
EnemyHitReaction.HandleHit reflection check `(Vector3, float)`,
PlayerCombat.cs source contains `targetable.RaiseHit(hitPoint, dmg)`,
DamageNumber + DamageNumberSpawner script presence + attributes,
Initialize reflection check, static Instance property type check,
spawner subscribe AND unsubscribe pair (leak prevention),
DamageNumber + DamageNumberSpawner prefabs exist with
_damageNumberPrefab wired via SerializedObject, EnemyHitReaction.cs
source contains the new HandleHit declaration. Read-only.

EnemyHitReactionValidator.cs updated: checks 1+2 expect the new
`Action<Vector3, float>` event type and `(Vector3, float)` RaiseHit
signature respectively. Without this update the M4-A validator
would FAIL after M8 lands.

Files:
- Assets/Scripts/Combat/Targetable.cs (event signature change,
  static event added, RaiseHit signature change)
- Assets/Scripts/Combat/EnemyHitReaction.cs (HandleHit signature
  update — float param ignored)
- Assets/Scripts/Player/PlayerCombat.cs (RaiseHit call updated
  to pass dmg)
- Assets/Scripts/Combat/Editor/EnemyHitReactionValidator.cs
  (event-type + RaiseHit-signature checks updated)
- Assets/Scripts/UI/DamageNumber.cs (NEW)
- Assets/Scripts/UI/DamageNumberSpawner.cs (NEW)
- Assets/Scripts/UI/Editor/DamageNumberBuilder.cs (NEW)
- Assets/Scripts/UI/Editor/DamageNumberValidator.cs (NEW)
- Assets/Prefabs/UI/DamageNumber.prefab (NEW, produced by builder)
- Assets/Prefabs/UI/DamageNumberSpawner.prefab (NEW, produced by builder)

No modifications to: any animator controllers, character prefabs
(Player_MaleHero, Dummy), Interactables (Assassinate / Open),
PlayerHUD, PlayerDeathOverlay.

Pending follow-up:
- User runs `LevelGen ▶ UI ▶ Build DamageNumber Prefab`.
- User runs `LevelGen ▶ UI ▶ Build DamageNumberSpawner Prefab`.
- User runs `LevelGen ▶ UI ▶ Place DamageNumberSpawner in Active Scene`.
- User runs `LevelGen ▶ UI ▶ Validate Damage Numbers` —
  expect 14 PASS / 0 FAIL.
- Sanity re-runs of all prior validators — DamageRouting 12/12,
  PlayerHUD 11/11, Combat Foundation 12/12, EnemyHitReaction
  14/14 (with updated event-type + RaiseHit-signature checks),
  EnemyDeath 16/16, MouseLook 7/7, PlayerDeath 16/16,
  InteractSystem 16/16, OpenInteractable 12/12.
- Play-mode smoke test: hit Dummy with Attack01 → "10" floats
  up from hit point and fades. Triple combo → three numbers
  spawn independently. Assassinate → "99999" floats up. Reload
  scene via PlayerDeathOverlay Restart → fresh spawner takes
  over, no double-spawning.

Deferred:
- Color coding by damage type (fire/ice/poison/crit) — needs
  DamageInfo struct introduction in a separate intentional
  milestone.
- Crit indicator (larger font, gold color).
- Healing numbers (green, on Heal events) — needs Heal event
  on CharacterStatsRuntime first.
- Object pooling (only if profiling shows GC hitches).
- Camera-billboarding for non-top-down views.
- Per-actor RaiseHit-Y-offset to avoid numbers spawning at
  ground level when hit point is on a low collider.

## M9 — Stamina gameplay (2026-05-04)

Stamina drains while sprinting, regenerates otherwise. Sprint
becomes unavailable at 0 stamina; the player drops to walk
even if Shift is still held. Re-engages once regen lifts
stamina above 0. Mostly *connecting* the existing data layer
to gameplay rather than building new architecture — the HUD's
yellow bar already polled `currentStamina` since the
Dummy+CharacterStats foundation milestone.

Architectural decisions (locked):
- Sprint-only stamina cost. Attack and Jump remain free. Future
  per-action costs will require their own milestone with
  explicit per-action SerializeFields on either CharacterStats
  or a new StaminaCost SO.
- Simple immediate model. Sprint stops at 0; regen happens any
  time stamina is not actively spent. No exhausted state, no
  delayed regen, no lockout window.
- Per-character drain + regen rates live as SerializeFields on
  the CharacterStats SO. Per-character tunable from day one.
  Defaults 25/s drain, 33/s regen — 4s full→empty, ~3s
  empty→full at maxStamina=100. Asymmetric (regen faster than
  drain) on purpose — modern action-game convention.
- Stored internally as float on CharacterStatsRuntime so per-
  frame deltas (drainRate * Time.deltaTime ≈ 0.4 at 25/s and
  60fps) accumulate cleanly. CurrentStamina is reported as
  `Mathf.CeilToInt` so PlayerHUD's int display shows "1/100"
  while any stamina remains, "0/100" only at true zero —
  preserving the "sprintable while non-zero" semantic without
  changing PlayerHUD.
- `PlayerStamina` MonoBehaviour on Player root owns the per-
  frame drain/regen Update. Mirrors the
  PlayerCombat / PlayerInteractor / PlayerDeath pattern of
  single-responsibility components on the player.
- PullCanSprint pattern: PlayerController PULLS
  `_stamina.CanSprint` from PlayerStamina each frame as part
  of the sprint-engagement check; PlayerStamina PULLS sprint
  state from PlayerController (`IsSprintingNow`). One source
  of truth in each direction. No events — events fire once on
  state change, but CanSprint is queried every frame anyway.

Invariants preserved:
- Single-direction dependency at each call site:
  PlayerController → reads CanSprint → engages sprint;
  PlayerStamina → reads IsSprintingNow → drains/regens.
  Never the reverse at either site.
- Single-writer-to-stamina-value: CharacterStatsRuntime is the
  only writer to `currentStamina` (via SpendStamina /
  RegenStamina). PlayerStamina calls those methods; it does
  not touch the field directly.
- HUD pattern unchanged: PlayerHUD continues to be a passive
  observer reading CurrentStamina each frame. No event
  subscription added; no PlayerHUD modifications at all.

CharacterStats.cs: extended with two SerializeFields
(`_staminaDrainPerSecond`, `_staminaRegenPerSecond`) +
matching read-only public accessors
(`StaminaDrainPerSecond`, `StaminaRegenPerSecond`). OnValidate
extended to clamp both to >= 0 (negative regen would be a bug;
negative drain semantically inverts the system).

CharacterStatsRuntime.cs: `currentStamina` field type changed
from int → float (M9). Public `CurrentStamina` (int) accessor
changed from direct field return → `Mathf.CeilToInt(currentStamina)`.
Two new public methods: `SpendStamina(float amount)` and
`RegenStamina(float amount)`, both no-op on amount<=0, both
mutate the float with Mathf.Max/Min clamps. Awake's debug log
updated to use the int accessor (was the raw int field).

PlayerStamina.cs (NEW, `LevelGen.Player`):
`[RequireComponent(CharacterStatsRuntime)]`,
`[RequireComponent(PlayerController)]`,
`[DisallowMultipleComponent]`. `public bool CanSprint` (default
true). Awake resolves siblings via GetComponent (no SerializeFields
to wire). OnEnable/OnDisable subscribes to PlayerDeath.OnPlayerDied
(belt-and-suspenders cleanup). Update reads
`PlayerController.IsSprintingNow`; if sprinting + stamina>0,
calls SpendStamina(drain*dt) and flips CanSprint=false on
hitting 0. Otherwise calls RegenStamina(regen*dt) and flips
CanSprint=true once stamina rises above 0.

PlayerController.cs: cached `_stamina = GetComponent<PlayerStamina>()`
in Awake (null-tolerant — sprint always allowed if missing).
New public auto-property `IsSprintingNow { get; private set; }`
— set inside Update from the `wantSprint` boolean. Sprint
engagement gained one clause:
`(_stamina == null || _stamina.CanSprint)`. Step 9 (animator
SetSprinting) now passes `IsSprintingNow` instead of raw
`_input.IsSprinting` — otherwise the Animator would play
Sprint clip while physically walking (stamina-empty case).

Player_MaleHero.prefab: gains PlayerStamina via either
build path (`Build Player_MaleHero Prefab` — folded in alongside
PlayerDeath / PlayerInteractor) or standalone adder
(`PlayerStaminaPrefabAdder.cs` — one-shot menu
`LevelGen ▶ Player ▶ Add PlayerStamina to Player_MaleHero Prefab`).
Idempotent. Bails with clear message if CharacterStatsRuntime
or PlayerController missing (RequireComponent prerequisites).

CharacterStats_Player.asset / Master.asset / Dummy.asset:
populated via one-shot menu
`LevelGen ▶ Combat ▶ Set Stamina Rates on CharacterStats Assets`
(`CharacterStatsAssetUpdater.cs`). Player + Master both get
drain=25, regen=33; Dummy gets drain=0, regen=0 (Dummy doesn't
sprint — values harmless but honest). Editor-script preferred
over YAML edits (CLAUDE.md convention — hand-edited .asset
files are fragile).

Validator: `Assets/Scripts/Player/Editor/PlayerStaminaValidator.cs`
(menu `LevelGen ▶ Player ▶ Validate Player Stamina`) — 12
read-only checks: CharacterStats source declares both rate
fields (literal-stub matches per M6 lesson #2); both public
accessors via reflection; CharacterStatsRuntime.SpendStamina +
RegenStamina via reflection; PlayerStamina presence;
RequireComponent + DisallowMultiple attributes; CanSprint
property type check; PlayerController source contains
`_stamina.CanSprint` (literal-stub); Player_MaleHero.prefab
has PlayerStamina; CharacterStats_Player.asset has both rates
> 0 (catches the "asset wasn't run through the updater"
misconfig). PASS / FAIL / SKIP format mirrors prior validators.

Files:
- Assets/Scripts/Combat/CharacterStats.cs (two new SerializeFields
  + accessors + OnValidate clamps)
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (currentStamina
  int→float; CurrentStamina returns CeilToInt; two new public
  mutator methods)
- Assets/Scripts/Player/PlayerController.cs (cached _stamina ref;
  new IsSprintingNow public property; sprint engagement clause;
  animator passthrough swapped to IsSprintingNow)
- Assets/Scripts/Player/PlayerStamina.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerStaminaPrefabAdder.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerStaminaValidator.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs
  (PlayerStamina AddComponent fold-in)
- Assets/Scripts/Combat/Editor/CharacterStatsAssetUpdater.cs (NEW)

No modifications to: PlayerHUD (passive observer pattern
preserved), any animator controllers, any interactables
(Assassinate / Open), PlayerCombat (attacks remain free per
Q1), PlayerAnimator, PlayerInputReader, any FBX, any other
character prefab (Dummy unchanged — no enemy stamina yet).

Pending follow-up:
- User runs `LevelGen ▶ Combat ▶ Set Stamina Rates on
  CharacterStats Assets` (writes 25/33 to Player+Master,
  0/0 to Dummy).
- User runs `LevelGen ▶ Player ▶ Add PlayerStamina to
  Player_MaleHero Prefab` (or rebuilds via Build Player_MaleHero
  Prefab — both paths work).
- User runs `LevelGen ▶ Player ▶ Validate Player Stamina` —
  expect 12 PASS / 0 FAIL.
- Sanity re-runs of all prior validators — none should
  regress: DamageRouting 12/12, PlayerHUD 11/11,
  DummyAndStats 12/12, EnemyHitReaction 14/14, EnemyDeath
  16/16, MouseLook 7/7, PlayerDeath 16/16, InteractSystem
  16/16, OpenInteractable 12/12, DamageNumbers 14/14.
- Play-mode smoke test: HUD shows 100/100; hold W → walk, no
  drain; hold W+Shift → sprint, bar drains over ~4s; hits 0
  → drops to walk; bar refills; spam Shift at low stamina →
  micro-sprints; release Shift mid-sprint → walk + regen;
  combat + jump while sprinting → no stamina change.

Deferred:
- Attack stamina cost (locked Q1 — sprint-only model)
- Jump stamina cost (locked Q1)
- Exhausted state / lockout / delayed regen (locked Q2)
- Stamina depletion VFX / SFX (HUD already shows the change)
- Per-equipment / per-armor stamina modifiers
- Enemy stamina (Dummy and future enemies could use the same
  SO fields; no consumer wired yet)
- Stamina-locked combos / blocking systems
- WeaponStats SO with per-weapon stamina costs

## M10 — Basic Dummy AI (2026-05-04)

First reactive enemy behavior with initiative. Through M9 the
Dummy was stationary — Animator with Idle / Hit / Death only.
M10 wires a NavMeshAgent + per-frame FSM so the Dummy can
detect the player, chase, attack on cooldown, and leash back
to Idle. Damage routing TO the player remains M11's territory;
M10's attack swings fire AnimationEvents that are absorbed by
a no-op stub.

Architectural decisions (locked):
- Full FSM package this milestone: movement (NavMeshAgent),
  Animator locomotion state (1D blend tree on MoveSpeed), Attack
  state, AI FSM (Idle / Chase / Attack / Cooldown). NO damage to
  player.
- NavMeshAgent + baked NavMesh in the test scene. Editor menu
  `LevelGen ▶ Combat ▶ Bake Test Scene NavMesh` creates a
  `_NavMeshSurface` GameObject with the modern AI Navigation
  2.x package (`Unity.AI.Navigation.NavMeshSurface`,
  CollectObjects=All, useGeometry=PhysicsColliders) and bakes.
- FSM with three tunable ranges: detection (Idle→Chase trigger),
  attack (Chase→Attack trigger), leash (Chase→Idle trigger).
  Asymmetric `_stoppingDistance < _attackRange` — agent stops
  sliding before the FSM range fires, so Chase→Attack isn't
  jittery at the boundary.
- Reuse `Attack01_SwordAndShiled` clip (publisher's typo
  preserved on this clip; only Idle was renamed during the M3
  pack swap). Its embedded OnHitboxOpen / OnHitboxClose events
  fire on the Dummy's Animator GameObject and are absorbed by
  `EnemyAnimationEventAbsorber` until M11 ships `EnemyCombat`.

Single-writer-per-Animator-parameter invariant (extended):
  - `Hit`        — EnemyHitReaction (M4-A)
  - `Death`      — EnemyDeath       (M4-B)
  - `MoveSpeed`  — EnemyAI          (M10)
  - `Attack`     — EnemyAI          (M10)
EnemyAI joins as the third writer to disjoint parameters.
Convention from M4-B (single-writer-per-*parameter*, not per-
Animator) holds.

M4-A interruption pattern preserved:
  `AnyState → Hit` (`canTransitionToSelf=false`) continues to
  interrupt the new Locomotion + Attack states. EnemyAI reads
  `_stats.IsDead` and the Animator's current state shortNameHash
  (== Hit) to suspend FSM tick mid-Hit. Once Hit→Idle completes,
  FSM resumes; if MoveSpeed > 0.1 next frame the rig blends
  back into Locomotion automatically.

Single-direction dependency at each call site:
  EnemyAI READS player Transform (tag-found at Awake),
  Animator state (for Hit suspension), agent.velocity. WRITES
  MoveSpeed/Attack to Animator, agent.SetDestination /
  isStopped / velocity, transform.rotation (manual face-during-
  Cooldown / Attack swing). Does NOT subscribe to PlayerDeath —
  chasing a dead player is harmless in M10 (no damage applied);
  M11 / M13 will revisit when player death needs to suppress
  enemy aggression.

EnemyAI.cs (NEW, `LevelGen.Combat`):
`[RequireComponent(NavMeshAgent)]`,
`[RequireComponent(CharacterStatsRuntime)]`,
`[DisallowMultipleComponent]`. Public nested enum `State`
(Idle/Chase/Attack/Cooldown). SerializeFields: `_animator`
(auto-resolved on Reset to a child Animator), `_playerTag`
(default "Player"), three ranges (`_detectionRange=8`,
`_attackRange=1.3`, `_leashRange=15`), three movement values
(`_chaseSpeed=2.5`, `_stoppingDistance=1.0`, `_turnSpeed=540`),
one combat value (`_attackCooldown=1.5`).
**Note**: `_attackRange` + `_stoppingDistance` were tuned down
post-M11 from 1.8 / 1.5 → 1.3 / 1.0 because the original
distance placed the Dummy beyond the EnemyWeaponHitbox arc
reach (~1.2m forward of Dummy pivot at peak swing); swings
landed in air. New defaults give 0.5m capsule-edge overlap
at peak swing for consistent hits. DummyPrefabBuilder seeds
`agent.stoppingDistance = 1.0` to match (EnemyAI.Awake
overrides at runtime; the seed is for inspector consistency).
Awake resolves siblings + does initial agent setup. Start
finds Player by tag (PlayerHUD-style retry coroutine if not
yet spawned). Update: early-returns on `_stats.IsDead` (M4-B
terminal) or `IsInHitState` (M4-A suspension); else dispatches
to TickIdle / TickChase / TickCooldown by current state (Attack
is owned by AttackCoroutine). Always drives MoveSpeed at end of
Update from `agent.velocity.magnitude / _chaseSpeed`. EnterAttack
sets agent.isStopped=true, fires Attack trigger, starts
AttackCoroutine which polls Animator normalizedTime ≥ 0.92
to detect anim completion (matches the controller's exitTime),
manually faces the player each frame during the swing.

EnemyAnimationEventAbsorber.cs (NEW, `LevelGen.Combat`):
`[DisallowMultipleComponent]`. Two empty public methods:
`OnHitboxOpen()` and `OnHitboxClose()`. Sole purpose:
suppress the "AnimationEvent has no receiver" warnings on
the Dummy's Animator when Attack01 plays. M11 replaces
this with a real EnemyCombat that fires an enemy weapon
hitbox. Method names MUST exactly match the player-side
endpoint names — Unity dispatches AnimationEvents by
method name, no parent walk (M4-A lesson).

EnemyBaseControllerBuilder.cs: extended with two new
parameters (MoveSpeed Float, Attack Trigger), two new
states (Locomotion 1D blend tree on MoveSpeed: Idle@0 /
MoveFWD_Battle_InPlace_SwordAndShield@1; Attack with
Attack01_SwordAndShiled clip — typo preserved per M3-02A
pack-swap notes), four new transitions (Idle→Locomotion at
MoveSpeed>0.1 dur 0.15, Locomotion→Idle at MoveSpeed<0.1
dur 0.15, AnyState→Attack on Attack trigger
canTransitionToSelf=false dur 0.10, Attack→Locomotion no-cond
exitTime 0.92 dur 0.10). Existing M4-A AnyState→Hit / Hit→Idle
and M4-B AnyState→Death preserved. BlendTree saved as a
sub-asset of the controller via `CreateBlendTreeInController`.

DummyPrefabBuilder.cs: extended after EnemyDeath wiring with:
NavMeshAgent (radius 0.4, height 1.8 to match CapsuleCollider;
speed 2.5, stoppingDistance 1.5, angularSpeed 540, acceleration
12, autoBraking true, updateRotation true), EnemyAI (with
`_animator` SerializedObject-wired to the MaleCharacterPBR
child Animator — Reset() doesn't fire on programmatic
AddComponent; M6 lesson), and EnemyAnimationEventAbsorber
on the MaleCharacterPBR child (so AnimationEvents from
Attack01 have a receiver). New helper `AssignEnemyAIRefs`
mirrors the existing `AssignEnemyDeathRefs` pattern. Build
log line updated to enumerate the new components.

EnemyAINavMeshBaker.cs (NEW, editor): menu
`LevelGen ▶ Combat ▶ Bake Test Scene NavMesh`. Edit-mode
only (early-returns + logs error if Application.isPlaying).
Walks the active scene, adds NavMeshModifier(ignoreFromBuild=true)
to any GameObject with NavMeshAgent or CharacterController
(excludes Player + Dummy from the bake; without this, the
Player's CharacterController would carve a Player-shaped hole
at its Play-mode start position). Creates or reuses a single
`_NavMeshSurface` GameObject in the active scene; bakes via
NavMeshSurface.BuildNavMesh() (modern AI Navigation 2.x API,
package version 2.0.12). Idempotent. Marks the active scene
dirty so the user remembers to save.

Validator: `Assets/Scripts/Combat/Editor/EnemyAIValidator.cs`
(menu `LevelGen ▶ Combat ▶ Validate Enemy AI`) — 16 read-only
checks: EnemyAI presence + RequireComponent attributes for
NavMeshAgent + CharacterStatsRuntime + State enum shape;
EnemyAnimationEventAbsorber presence + OnHitboxOpen/Close
methods (reflection); EnemyBaseController parameter shape
(MoveSpeed Float + Attack Trigger); Locomotion state with
BlendTree motion; Attack state with Attack01_SwordAndShiled
clip; Idle↔Locomotion transitions with MoveSpeed conditions;
AnyState→Attack with canTransitionToSelf=false; Attack→Locomotion
with exitTime ≥ 0.9; Dummy.prefab NavMeshAgent presence + EnemyAI
with `_animator` SerializedObject-wired + Absorber on child.
Read-only — no scene mutations, no NavMesh bake (separate menu).
Format mirrors EnemyDeathValidator.

Files:
- Assets/Scripts/Combat/EnemyAI.cs (NEW)
- Assets/Scripts/Combat/EnemyAnimationEventAbsorber.cs (NEW)
- Assets/Scripts/Combat/Editor/EnemyAINavMeshBaker.cs (NEW)
- Assets/Scripts/Combat/Editor/EnemyAIValidator.cs (NEW)
- Assets/Scripts/Combat/Editor/EnemyBaseControllerBuilder.cs
  (4 new params/states/transitions added; M4-A + M4-B work
  preserved verbatim)
- Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs
  (NavMeshAgent + EnemyAI + Absorber folded into clean-rebuild
  pattern; AssignEnemyAIRefs SerializedObject helper added)
- Assets/Animators/Enemy/EnemyBaseController.controller
  (rebuilt by builder — gains MoveSpeed, Attack params, Locomotion
  + Attack states, 4 new transitions)
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab
  (rebuilt by builder — gains NavMeshAgent + EnemyAI on root,
  EnemyAnimationEventAbsorber on MaleCharacterPBR child)

No modifications to: Player_MaleHero.prefab, any UI/HUD,
interactables (M6/M7), M4-A/M4-B/M5/M8/M9 work. EnemyAI
and AssassinateInteractable coexist via existing patterns —
the Assassinate prompt only realistically fires when the
Dummy is in Idle or Cooldown (during Chase the agent
auto-rotates to face the player, defeating the back-arc check).

Pending follow-up:
- M11: replace EnemyAnimationEventAbsorber with EnemyCombat
  that fires an enemy weapon hitbox + applies player damage
  on OnHitboxOpen.
- Multi-target / threat tables / aggro lists.
- Knockback beyond the existing M4-A Hit reaction.
- NavMeshObstacle on TestDoor when in open state (M16's
  territory once real doors arrive).
- Enemy-distinct attack animations (M14 — currently shares
  the Player's Attack01 clip).
- Patrol routes / waypoint behavior.
- Performance audit if multiple Dummies are placed (Update
  cost per agent is fine for one but FindGameObjectWithTag
  per-spawn is wasteful for swarms — cache the player Transform
  in a singleton).
- EnemyAI doesn't subscribe to PlayerDeath — chasing a dead
  player is acceptable in M10 since no damage applies. M11 /
  M13 will revisit.

## M11 — Player takes damage (2026-05-04)

Closes the combat loop. Through M10 the Dummy could swing but
the Attack-clip AnimationEvents were absorbed by a no-op stub.
M11 replaces the absorber with real damage routing — the Dummy
can now hurt and kill the Player. Death loop verified
end-to-end: Dummy chases → swings → 10 damage per hit → Player
flinches via M2-B Hit state → HUD red bar drains → 0 HP →
M5 PlayerDeath fires → "You Died" overlay → Restart reloads.

Architectural decisions (locked):
- Q1: New `EnemyCombat` script (per-enemy) mirrors PlayerCombat's
  hitbox+damage path verbatim. Replaces M10's
  `EnemyAnimationEventAbsorber` on the MaleCharacterPBR child.
  Pattern duplication accepted (rule of three: defer
  generalization until ≥3 duplicates exist; today we have one
  Player + one Enemy).
- Q2: Player gains `Targetable` on root. Hits route through
  `targetable.RaiseHit(hitPoint, dmg)`. M8 damage numbers
  spawn over Player automatically via the static
  `AnyTargetableHit` event — no DamageNumberSpawner edit
  required. Bidirectional Targetable seam.
- Q3: New `PlayerHitReaction` symmetric to `EnemyHitReaction`.
  Subscribes to its own `Targetable.OnHit` in OnEnable, calls
  `PlayerCombat.TakeHit()` (which sets the Hit trigger via
  PlayerAnimator). PlayerCombat remains sole writer to the
  Hit Animator parameter — single-writer-per-parameter
  invariant preserved.
- Q4: NO stagger window on PlayerHitReaction. Souls-like
  stunlock. With one Dummy on a 1.5s cooldown stunlock isn't
  a problem; revisit if multi-enemy playtest demands it.
- Q5: Player CharacterController.radius bumped 0.3 → 0.4 for
  symmetric combat reach. The CharacterController is the
  Player's authoritative collision shape — Unity treats it as
  non-static, so OnTriggerEnter from the Dummy's hitbox fires
  correctly against it without a separate CapsuleCollider.
  0.3 was tuned for narrow-gap movement; 0.4 matches the
  Dummy's CapsuleCollider (also 0.4) and gives the
  EnemyWeaponHitbox BoxCollider arc enough overlap window to
  consistently land hits across the Attack01 swing. Tradeoff:
  marginally more catch-on-corner behavior in tight geometry.
  Revisit if level-gen environments make movement feel sticky;
  alternative is to introduce a separate hit-reception
  CapsuleCollider while keeping CC at 0.3 for movement.

Combat ladder symmetry (post-M11):

  Concept                       Player side                  Enemy side
  ─────────────────────────────────────────────────────────────────────────────
  Combat owner                  PlayerCombat                 EnemyCombat
  Hitbox enable/disable         OnHitboxOpen/Close (P)       OnHitboxOpen/Close (E)
  Hitbox child                  WeaponHitbox                 EnemyWeaponHitbox
  Trigger relay                 HitboxRelay                  EnemyHitboxRelay
  AnimationEvent receiver       AnimationEventForwarder      EnemyAnimationEventForwarder
  Hittable identity             Targetable                   Targetable
  Hit reaction script           PlayerHitReaction (M11)      EnemyHitReaction (M4-A)
  Death pipeline                PlayerDeath (M5)             EnemyDeath (M4-B)
  Damage default                10                           10 (mirror)

Friendly-fire guard (`stats.CompareTag("Player")` inside
`EnemyCombat.NotifyHitboxTriggered`): hard-coded "only Player
gets hit by enemy attacks" filter. Prevents two Dummies in
melee range from damaging each other (their CapsuleColliders
carry CharacterStatsRuntime + Targetable too). Future
M-Factions milestone replaces this with team / faction IDs.

IsDead guard inside `EnemyCombat.NotifyHitboxTriggered`:
critical for preserving M5's terminal-Death semantic. Without
it, a dead Player would still receive damage events (no real
HP harm — clamps at 0) but PlayerHitReaction would re-fire
GetHit01, interrupting Die01. Stops corpse-flinch.

Single-direction dependency at each call site:
  EnemyCombat reads animation events + hitbox triggers; writes
  to nothing player-side except `targetable.RaiseHit` and
  `stats.ApplyDamage`. The Player receives the hit via its own
  Targetable.OnHit (M8 event payload) and reacts via
  PlayerHitReaction → PlayerCombat → PlayerAnimator. One
  direction.

Single-writer-per-Animator-parameter invariant preserved:
  PlayerCombat (via PlayerAnimator) writes Attack / Hit / Jump /
  Death / ComboNext. PlayerCombat.TakeHit now has TWO callers
  (the existing public API + the new PlayerHitReaction), but
  the parameter writer is still PlayerCombat.

EnemyCombat.cs (NEW, `LevelGen.Combat`): `[DisallowMultipleComponent]`.
SerializeFields: `_attackDamage` (int, default 10 — mirror of
PlayerCombat.attackDamage), `_hitbox` (Collider, wired by builder).
Public: `OnHitboxOpen()` / `OnHitboxClose()` (animation-event
endpoints, called via the Forwarder), `NotifyHitboxTriggered(Collider)`
(called from EnemyHitboxRelay). Per-swing
`HashSet<Targetable> _currentAttackHitList` cleared on
OnHitboxOpen/Close. Notify body resolves stats + targetable
on the hit collider, applies the IsDead guard, applies the
friendly-fire guard, applies damage, raises RaiseHit with the
ClosestPoint-on-other-collider hit point.

EnemyHitboxRelay.cs (NEW, `LevelGen.Combat`):
`[DisallowMultipleComponent]`. SerializeField `_combat`
(EnemyCombat, auto-resolved on Reset via GetComponentInParent).
OnTriggerEnter forwards the colliding Collider to
`_combat.NotifyHitboxTriggered`. Pure relay, no state.

EnemyAnimationEventForwarder.cs (NEW, `LevelGen.Combat`):
REPLACES the M10 EnemyAnimationEventAbsorber. SerializeField
`_combat` (EnemyCombat, auto-resolved on Reset). Public
`OnHitboxOpen()` / `OnHitboxClose()` forward to the same
methods on `_combat`. Method names exactly match the
PlayerCombat-side names since the Attack01 clip is shared
(World Bundle pack, M3 swap).

PlayerHitReaction.cs (NEW, `LevelGen.Player`):
`[RequireComponent(Targetable)]`, `[DisallowMultipleComponent]`.
Resolves Targetable + PlayerCombat + CharacterStatsRuntime via
GetComponent in Awake. OnEnable/OnDisable subscribes /
unsubscribes `_targetable.OnHit`. HandleHit early-returns on
`_stats.IsDead` (defense-in-depth — PlayerCombat.TakeHit also
guards) then calls `_combat.TakeHit()`. The damage param is
ignored (flinch is binary, doesn't scale). No stagger window
per Q4.

EnemyAnimationEventAbsorber.cs (DELETED, M10 stub):
`Assets/Scripts/Combat/EnemyAnimationEventAbsorber.cs` and its
`.meta` removed. The Forwarder is a strict superset.

DummyPrefabBuilder.cs: extended to add EnemyCombat to root
after EnemyAI, replace the AddComponent of Absorber with the
Forwarder on MaleCharacterPBR (with `_combat` SerializedObject-
wired to the EnemyCombat on root), and build the
EnemyWeaponHitbox child under weapon_r:
  - GameObject `EnemyWeaponHitbox` under the bone-name match
    (weapon_r preferred, falls back to weapon_l / Weapon_R / Weapon_L)
  - BoxCollider: size (0.15, 0.15, 0.8), center (0, 0, 0.4),
    isTrigger=true, enabled=false (mirrors Player WeaponHitbox)
  - Kinematic Rigidbody (isKinematic=true, useGravity=false) —
    REQUIRED for OnTriggerEnter to fire on a child collider that
    moves via skeletal animation. M3 lesson; CapsuleCollider on
    root doesn't promote deeply-nested triggers to "moving" status.
  - EnemyHitboxRelay with `_combat` SerializedObject-wired to
    the EnemyCombat on root.
  - Final step: `EnemyCombat._hitbox` SerializedObject-wired to
    the BoxCollider on the new child.
Build summary log line updated to enumerate the new components.
New helpers: `BuildEnemyWeaponHitbox`, `AssignForwarderCombatRef`,
`FindByNameRecursive` (mirror of PlayerCombatHitboxBuilder's
weapon-attach search).

PlayerTakesDamagePrefabAdder.cs (NEW editor): two menu items
folded into one file (must run in order — PlayerHitReaction
RequireComponents Targetable):
  - `LevelGen ▶ Player ▶ Add Targetable to Player_MaleHero Prefab`
  - `LevelGen ▶ Player ▶ Add PlayerHitReaction to Player_MaleHero Prefab`
Both idempotent. LoadPrefabContents → check-and-add →
SaveAsPrefabAsset. PlayerHitReaction adder bails clearly if
Targetable or PlayerCombat missing.

Validators:
- `EnemyCombatValidator.cs` (NEW): menu
  `LevelGen ▶ Combat ▶ Validate Enemy Combat`. 17 read-only
  checks covering EnemyCombat / EnemyHitboxRelay /
  EnemyAnimationEventForwarder presence + surface; IsDead +
  friendly-fire literal-stub source matches; absorber file
  deleted; PlayerHitReaction presence + RequireComponent +
  sub/unsub + TakeHit-call literal-stub matches; Player prefab
  Targetable + PlayerHitReaction + tag; Dummy prefab EnemyCombat
  + Forwarder on child + EnemyWeaponHitbox child fully wired
  (BoxCollider trigger+disabled, kinematic Rigidbody,
  EnemyHitboxRelay._combat); Player CharacterController.radius
  >= 0.35 (Q5 regression check).
- `EnemyAIValidator.cs` (UPDATED): check 5 + check 16 retargeted
  to expect the Forwarder instead of the deleted Absorber.

Files:
- Assets/Scripts/Combat/EnemyCombat.cs (NEW)
- Assets/Scripts/Combat/EnemyHitboxRelay.cs (NEW)
- Assets/Scripts/Combat/EnemyAnimationEventForwarder.cs (NEW)
- Assets/Scripts/Combat/EnemyAnimationEventAbsorber.cs (DELETED)
- Assets/Scripts/Combat/Editor/EnemyCombatValidator.cs (NEW)
- Assets/Scripts/Combat/Editor/EnemyAIValidator.cs (Forwarder swap)
- Assets/Scripts/Combat/Editor/DummyPrefabBuilder.cs
  (EnemyCombat fold-in, Forwarder swap, EnemyWeaponHitbox
   child build + SerializedObject wiring)
- Assets/Scripts/Player/PlayerHitReaction.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerTakesDamagePrefabAdder.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerCapsuleTuner.cs (NEW; Q5 — bumps
  CharacterController.radius 0.3 → 0.4 via SerializedObject;
  idempotent)
- Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
  (gains Targetable + PlayerHitReaction via the two adder menus;
  CharacterController.radius bumped 0.3 → 0.4 via PlayerCapsuleTuner)
- Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab
  (rebuilt by `Build Dummy Prefab`; gains EnemyCombat,
   Forwarder replaces Absorber, EnemyWeaponHitbox child wired)

No modifications to: Targetable.cs, CharacterStatsRuntime.cs,
PlayerCombat.cs, PlayerAnimator.cs, PlayerBaseController.controller,
EnemyBaseController.controller, any UI/HUD, M5 PlayerDeath,
M8 DamageNumberSpawner, M10 EnemyAI.

Pending follow-up:
- WeaponStats SO with per-weapon damage (deferred from
  campaign list — the "weapon variety" milestone)
- Knockback / impact direction
- Stagger-window on PlayerHitReaction if multi-enemy
  stunlock becomes a problem in playtest
- Block / dodge / parry mechanics
- Damage type / element / resistance
- Enemy-vs-enemy damage: remove the friendly-fire guard,
  introduce factions / team IDs (M-Factions)
- EnemyAI doesn't subscribe to PlayerDeath; chasing a dead
  player still applies (no damage now thanks to the IsDead
  guard, but cosmetically odd) — revisit when player-death
  needs to stop enemy aggression scene-wide.

## M12 — Player dodge (2026-05-11)

Directional 4-way dodge. V key triggers a roll in the current
movement direction (or forward if no input), consuming 25 stamina,
granting 0.5s of i-frames, and enforcing an 0.8s cooldown. Cancels
any in-flight attack via the new `PlayerCombat.CancelAttack()`
public method. Movement is driven by a scripted impulse on the
CharacterController inside a coroutine — no root motion.

Architectural decisions (locked):
- I-frames are a single flag on `CharacterStatsRuntime` — new
  `IsInvulnerable` bool + `SetInvulnerable(bool)` mutator. The
  `ApplyDamage` gate is the SOLE i-frame check point (added at the
  top after the `stats == null` guard). Damage events are discarded
  entirely when invulnerable — no HP delta, no `OnDied` emission.
  Generic design: `SetInvulnerable` is callable by any future
  system (post-hit invuln, cinematic invuln) without changes.
- Scripted impulse pattern: `PlayerDodge.RollCoroutine` calls
  `cc.Move(worldDir * _rollSpeed * Time.deltaTime)` each frame for
  the roll duration. PlayerController suppresses ONLY its horizontal
  motion during the roll (new step 4.6 mirroring step 4.5's
  Attack/Hit gate) so two `cc.Move` calls per frame combine cleanly:
  PlayerController applies gravity-Y, PlayerDodge applies horizontal.
  Both calls update position in order — well-defined in Unity.
- Sub-state-machine `Dodge` containing four states `RollFWD`,
  `RollBWD`, `RollLFT`, `RollRGT`. Each Roll state's motion = the
  matching `Roll{DIR}_Battle_InPlace_SwordAndShield` clip from the
  World Bundle pack. InPlace variants chosen since script drives
  displacement (matches M2-B InPlace convention).
- Animator wiring: `DodgeTrigger` (Trigger) + `DodgeDirection`
  (Int 0/1/2/3 mapping FWD/BWD/LFT/RGT). Four `AnyState→Roll{X}`
  transitions, each conditioned on `DodgeTrigger AND
  DodgeDirection == directionValue` with
  `canTransitionToSelf=false`, fixed dur 0.05. Each Roll → Locomotion
  exit transition with `exitTime=1.0`, no condition, fixed dur 0.10.
  `PlayerDodge` calls `SetDodgeDirection` THEN `SetDodgeTrigger`
  in that order so the AnyState transition evaluates with the
  right Int.
- Direction selection: input-vector axis-bucket (no `Mathf.Atan2`).
  Forward = `input.y >= |input.x|, input.y >= 0`. Backward =
  `input.y < -|input.x|`. Right = `input.x > |input.y|`. Left =
  symmetric. Below dead-zone (`sqrMagnitude < 0.01`) defaults to
  forward relative to body. World-space direction derived from
  `transform.forward / right` (body yaw locked to camera yaw every
  frame via SnapBodyToCameraYaw), with a defensive fallback to
  `Camera.main` forward if available.
- Re-entry guards (three independent, any-failure drops silently):
  `_isDodging` (already rolling), `CurrentStamina < _staminaCost`
  (insufficient), `Time.time - _lastDodgeTime < _cooldown`
  (cooldown). Stamina is spent BEFORE setting `_isDodging`/animator
  params so a same-frame double-press also fails the stamina gate.
  `_lastDodgeTime = -999f` initial value ensures the first press
  always passes the cooldown gate.
- Attack cancel: `PlayerCombat.CancelAttack()` is purely local —
  clears `_attackBuffered`, empties `_currentAttackHitList`,
  disables the hitbox. Does NOT touch the Animator (single-writer-
  per-parameter invariant preserved). The visual interruption comes
  from `AnyState → Roll{X}` overriding the Attack state.
- PlayerDeath integration: `PlayerDodge` subscribes to
  `PlayerDeath.OnPlayerDied`; on death, stops all coroutines,
  clears `_isDodging`, sets `IsInvulnerable = false`, disables
  itself. Belt-and-suspenders cleanup so a death mid-dodge doesn't
  leave the player permanently invulnerable.
- Hit during dodge: `AnyState → Hit` (M2-B) still wins because Hit
  has its own AnyState transition; in practice the i-frame gate
  inside `ApplyDamage` swallows the damage, so `Targetable.OnHit`
  doesn't fire, so `PlayerHitReaction.HandleHit` doesn't run, so
  no Hit trigger gets set. Effectively: i-frame dodge ignores
  incoming hits cleanly.

Single-direction dependencies preserved:
  PlayerInputReader → (event DodgePressed) → PlayerDodge →
    (call) PlayerCombat.CancelAttack  +
    (call) PlayerAnimator.SetDodgeDirection / SetDodgeTrigger +
    (call) CharacterStatsRuntime.SpendStamina / SetInvulnerable +
    (call) CharacterController.Move
  PlayerDodge is the ONLY writer to DodgeTrigger and DodgeDirection
  (via PlayerAnimator). PlayerCombat never reaches into the Animator
  directly. PlayerController reads `_dodge.IsDodging` to gate its
  own horizontal motion (the existing _combat.IsActionLocked sibling
  pattern).

CharacterStatsRuntime.cs: added `IsInvulnerable` bool property
  (auto-prop with private setter) + `SetInvulnerable(bool)` mutator.
  `ApplyDamage` early-returns on `IsInvulnerable` at the top, after
  the `stats == null` guard. Other entry points unchanged
  (`Heal`, `SpendStamina`, `RegenStamina` not i-frame-gated;
  invuln only blocks damage).

PlayerInputReader.cs: added `event System.Action DodgePressed` +
  `public void OnDodge(InputAction.CallbackContext ctx)` endpoint.
  Raises on `ctx.started` (the press-down edge, immune to future
  Hold/Tap interactions). M1-stub log NOT added (Dodge has a real
  consumer from day one). Mirrors AttackPressed / JumpPressed /
  InteractPressed pattern.

PlayerCombat.cs: added `public void CancelAttack()` — clears
  `_attackBuffered`, empties `_currentAttackHitList`, disables
  `hitbox` collider. Idempotent. NO Animator parameter writes.
  Existing methods unchanged.

PlayerAnimator.cs: added two parameter name constants
  (`ParamDodgeTrigger`, `ParamDodgeDirection`), two hash fields
  (`_hashDodgeTrigger`, `_hashDodgeDirection`), Awake hash
  assignment, and two public methods (`SetDodgeTrigger`,
  `SetDodgeDirection(int)`). All `_ready`-gated like the other
  trigger setters.

PlayerController.cs: added optional `_dodge` sibling ref (null-
  tolerant, same pattern as `_combat` / `_stamina`). New step 4.6
  in Update: `if (_dodge != null && _dodge.IsDodging) motion =
  Vector3.zero` — mirrors the existing step 4.5 Attack/Hit gate.
  Gravity still applies via `ApplyGravity` (step 5) so
  PlayerDodge's horizontal-only `cc.Move` plus PlayerController's
  vertical-only `cc.Move` compose into the full motion. No
  refactoring of existing step numbering.

PlayerDodge.cs (NEW, `LevelGen.Player`):
  `[RequireComponent(CharacterController)]`,
  `[RequireComponent(CharacterStatsRuntime)]`,
  `[RequireComponent(PlayerInputReader)]`,
  `[DisallowMultipleComponent]`.
  SerializeFields (header-grouped):
    Roll Motion:     `_rollSpeed = 8f`, `_rollDuration = 0.35s`
    I-Frames:        `_iFrameDuration = 0.5s`
    Cost / Cooldown: `_cooldown = 0.8s`, `_staminaCost = 25f`
  Cached refs: CharacterController, CharacterStatsRuntime,
    PlayerInputReader (RequireComponent — never null at runtime),
    PlayerCombat / PlayerAnimator / PlayerDeath (optional —
    null-tolerant). Camera.main.transform cached at Awake for
    direction fallback.
  Public surface: `bool IsDodging` (read-only). Read by
    PlayerController step 4.6.
  Internal direction constants: `DirFWD=0`, `DirBWD=1`,
    `DirLFT=2`, `DirRGT=3` — match the DodgeDirection int values
    that the Animator's AnyState transitions check.

PlayerBaseControllerDodgeExtender.cs (NEW, editor):
  Idempotent additive extender (mirrors M5's
  PlayerBaseControllerExtender pattern). Loads existing controller,
  adds:
    - DodgeTrigger (Trigger) + DodgeDirection (Int) parameters
    - `Dodge` sub-state-machine
    - Four states inside the sub-SM: RollFWD/BWD/LFT/RGT (each
      with the matching InPlace clip motion, writeDefaultValues=1,
      speed=1)
    - Four AnyState→Roll{X} transitions on the root SM (each
      conditioned on DodgeTrigger + DodgeDirection==value,
      canTransitionToSelf=false, fixedDuration=true, dur 0.05)
    - Four Roll{X}→Locomotion exit transitions (exitTime=1.0,
      no conditions, fixedDuration=true, dur 0.10)
  Each addition presence-checked and skipped if already wired.
  Re-runnable; all-skipped output is the green idempotent state.
  Menu: `LevelGen ▶ Player ▶ Extend PlayerBaseController (M12 Dodge)`.

PlayerDodgePrefabAdder.cs (NEW, editor):
  `LevelGen ▶ Player ▶ Add PlayerDodge to Player_MaleHero Prefab`.
  Adds PlayerDodge if not already present; no SerializeField refs
  to wire (PlayerDodge resolves all siblings via GetComponent in
  Awake). Warns clearly if any RequireComponent prerequisite is
  missing from the prefab.

PlayerPrefabBuilder.cs (modified):
  - s_Bindings array extended with `("Dodge", "OnDodge")` —
    UnityEvents wiring picks it up automatically on rebuild.
  - PlayerDodge AddComponent folded into the BuildPlayerMaleHeroPrefab
    sequence after PlayerStamina. No SerializeObject refs to wire.

InputSystem_Actions.inputactions (edited):
  Added Dodge action (Button) to the Player map's actions array
  (post-Sprint position to match s_Bindings tail order). Added
  V key binding under `<Keyboard>/v` in the Player map's bindings
  array, group `Keyboard&Mouse`. Stable GUIDs (a2c7d4e1... for
  the action, b3d8e5f2... for the binding) so future re-merges
  remain idempotent. The prompt referenced
  `Assets/Input/PlayerInputActions.inputactions` which doesn't
  exist in this project — the canonical InputSystem asset is
  `Assets/InputSystem_Actions.inputactions`. Path-correction noted.

PlayerDodgeValidator.cs (NEW, editor):
  `LevelGen ▶ Player ▶ Validate Player Dodge`. 17 read-only
  checks: PlayerDodge.cs presence, three RequireComponent
  attributes, CharacterStatsRuntime.IsInvulnerable + SetInvulnerable
  + ApplyDamage signature, PlayerInputReader.DodgePressed event,
  PlayerCombat.CancelAttack method, DodgeTrigger + DodgeDirection
  parameters, Roll{FWD,BWD,LFT,RGT} states present (walks sub-SM
  via recursive search), AnyState transitions with DodgeTrigger
  condition + canTransitionToSelf=false, Roll→Locomotion exits
  with exitTime=1.0, prefab has PlayerDodge component, prefab
  has all RequireComponent prereqs, prefab has PlayerCombat (for
  CancelAttack delegate target). Format mirrors PlayerDeathValidator.

Files:
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (IsInvulnerable
  property + SetInvulnerable mutator + ApplyDamage early-return guard)
- Assets/Scripts/Player/PlayerInputReader.cs (DodgePressed event +
  OnDodge endpoint)
- Assets/Scripts/Player/PlayerCombat.cs (CancelAttack public method)
- Assets/Scripts/Player/PlayerAnimator.cs (DodgeTrigger/Direction
  hash + param constants + Awake assignment + SetDodgeTrigger +
  SetDodgeDirection methods)
- Assets/Scripts/Player/PlayerController.cs (cached _dodge sibling
  ref + step 4.6 horizontal-motion gate)
- Assets/Scripts/Player/PlayerDodge.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerBaseControllerDodgeExtender.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerDodgePrefabAdder.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerDodgeValidator.cs (NEW)
- Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs (s_Bindings +
  PlayerDodge AddComponent fold-in)
- Assets/InputSystem_Actions.inputactions (Dodge action + V binding)
- Assets/Animators/Player/PlayerBaseController.controller (will be
  extended in place by PlayerBaseControllerDodgeExtender — DodgeTrigger
  + DodgeDirection params, Dodge sub-SM, four Roll states, eight
  transitions added; existing M2-B/M2-C/M5 work preserved verbatim)
- Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
  (will gain PlayerDodge via either build path or standalone adder)

No modifications to: PlayerDeath, PlayerDeathOverlay, PlayerHUD,
PlayerInteractor, PlayerStamina, PlayerHitReaction, Targetable,
EnemyAI, EnemyCombat, EnemyDeath, EnemyHitReaction, MouseLook,
DamageNumber*, Interactable / AssassinateInteractable /
OpenInteractable.

Pending follow-up (Jason runs after CC completes):
1. `LevelGen ▶ Player ▶ Extend PlayerBaseController (M12 Dodge)`
   — adds DodgeTrigger / DodgeDirection params, Dodge sub-SM,
   four Roll states, eight transitions to the controller in place.
2. `LevelGen ▶ Player ▶ Add PlayerDodge to Player_MaleHero Prefab`
   — OR rebuild the prefab via `Build Player_MaleHero Prefab` (both
   paths work; rebuild is preferred since it also re-runs the
   UnityEvent wiring with the new Dodge → OnDodge binding).
3. `LevelGen ▶ Player ▶ Validate Player Dodge` — expect 17 PASS /
   0 FAIL.
4. Sanity re-runs all prior validators — none should regress:
   DamageRouting 12/12, PlayerHUD 11/11, DummyAndStats 12/12,
   EnemyHitReaction 14/14, EnemyDeath 16/16, MouseLook 7/7,
   PlayerDeath 16/16, InteractSystem 16/16, OpenInteractable
   12/12, DamageNumbers 14/14, PlayerStamina 12/12, EnemyAI 16/16,
   EnemyCombat 17/17.
5. Play-mode smoke test: Play scene → press V while running
   forward → player rolls forward over ~0.35s + plays RollFWD
   clip + visually displaces ~2.8m. Stamina bar drops by 25.
   Pressing V again immediately drops silently (cooldown OR
   stamina); after 0.8s stamina has refilled enough and the
   cooldown has elapsed, V works again. Hold W+D and press V →
   rolls forward (forward bucket wins because |y| >= |x|). Hold
   only D and press V → rolls right. Mid-combo press V → attack
   cancels, roll fires (visually the swing aborts, hitbox stays
   disabled, no double-damage on the swing-frame target).
   Stand still + press V → rolls forward. Have Dummy attack you
   AND press V the same frame → 0 damage taken, GetHit01 does NOT
   play (i-frames swallow the hit cleanly).

Deferred:
- Dodge VFX / SFX / dust particle / motion blur
- Camera shake on dodge / camera dolly tweak
- Animation-event-driven i-frame window (currently coroutine-
  driven, fixed 0.5s; could later trigger on clip frame N for
  per-clip tuning)
- Knockback on hit-during-dodge (currently no-op)
- Enemy dodge
- Dodge during target lock (M13 territory — assumes target-lock
  system exists)
- Dodge attack / dodge-into-roll-attack combo
- Per-armor / per-equipment dodge cost / cooldown modifiers
- WeaponStats SO with per-weapon dodge stats
- Stunlock check: M11 Q4 left PlayerHitReaction without a stagger
  window (Souls-like). Combined with dodge i-frames this should
  feel correct, but multi-enemy playtest might need re-tuning.

Lesson logged for this milestone:
- Project's InputSystem asset path is canonical at
  `Assets/InputSystem_Actions.inputactions` — there is no
  `Assets/Input/PlayerInputActions.inputactions`. Prompts that
  reference the latter should be re-anchored to the former.
- `CharacterStatsRuntime` stamina API is `SpendStamina(float)`
  (not `ApplyStaminaCost`); float internally (M9), so dodge cost
  passes through cleanly without rounding.

## Post-rebuild checklist for Player_MaleHero.prefab

`LevelGen ▶ Player ▶ Build Player_MaleHero Prefab` rebuilds the
prefab FROM SCRATCH — anything added by a separate
`Add X to Player_MaleHero` adder (or by an "extender" that mutates
an external asset) is dropped from the new prefab AND any active
scene's instance becomes a refreshed clone that loses bound scene-
side references. Run these in order after every rebuild:

1. `LevelGen ▶ UI ▶ Add CharacterStatsRuntime to Player_MaleHero`
   — adds the HP/Stamina runtime instance pointing at
   CharacterStats_Player.asset.
2. `LevelGen ▶ Player ▶ Add PlayerCombat to Player_MaleHero Prefab`
   — adds the combat owner.
3. `LevelGen ▶ Combat ▶ Add Weapon Hitbox to Player_MaleHero`
   — adds WeaponHitbox child under `weapon_r` with BoxCollider +
   kinematic Rigidbody + HitboxRelay, then wires PlayerCombat.hitbox.
   Run AFTER step 2 (HitboxRelay needs PlayerCombat present).
4. `LevelGen ▶ Player ▶ Add Targetable to Player_MaleHero Prefab`
   (M11) — adds the publisher of OnHit / AnyTargetableHit.
5. `LevelGen ▶ Player ▶ Add PlayerHitReaction to Player_MaleHero
   Prefab` (M11) — subscribes to its own Targetable.OnHit.
   Requires step 4.
6. `LevelGen ▶ Player ▶ Tune CharacterController for Hit Reception`
   (M11 Q5) — bumps CharacterController.radius 0.3 → 0.4.
7. `LevelGen ▶ Player ▶ Add PlayerDeath to Player_MaleHero Prefab`
   — **re-run even though PlayerDeath was folded into the build**.
   The build added the component but `_combat` is null because
   PlayerCombat wasn't present at AddComponent time. Re-running the
   adder re-wires `_combat` to the now-present PlayerCombat.

Scene-side re-bindings (separate from the prefab):

8. Re-bind `CM Follow Camera` in the test scene. The new prefab's
   `CameraTarget` child has a different fileID, so the scene's
   CinemachineCamera Follow + LookAt references resolve to the
   old (dead) transform. Two fixes:
   - **Manual (preferred)**: select `CM Follow Camera`, drag
     `Player_MaleHero/CameraTarget` from the Hierarchy into both
     the Follow and LookAt slots, save scene. Preserves OrbitalFollow
     axes, Reader Gain ±10, Deoccluder, etc.
   - **Nuke + rebuild**: delete `CM Brain Camera` + `CM Follow Camera`
     GameObjects, run `LevelGen ▶ Player ▶ Add Cinemachine Follow
     Camera to Active Scene`. Restores M2-A defaults.
9. Verify `_MouseLock` GameObject is present in the scene (M-CursorLock).
   If missing: `LevelGen ▶ Input ▶ Place _MouseLock in Active Scene`.

Validators (sanity sweep after the cascade):
- `LevelGen ▶ Combat ▶ Validate Damage Routing` → 12/12
- `LevelGen ▶ Combat ▶ Validate Enemy Combat` → 17/17
- `LevelGen ▶ Player ▶ Validate Player Death` → 16/16
- `LevelGen ▶ Player ▶ Validate Player Dodge` → 17/17
- `LevelGen ▶ Combat ▶ Validate Player HUD` → 11/11
- `LevelGen ▶ UI ▶ Validate Damage Numbers` → 14/14
- `LevelGen ▶ Player ▶ Validate Player Stamina` → 12/12
- `LevelGen ▶ Combat ▶ Validate Combat Foundation` → 12/12
- `LevelGen ▶ Interaction ▶ Validate Interact System` → 16/16
- `LevelGen ▶ Interaction ▶ Validate OpenInteractable` → 12/12
- `LevelGen ▶ Combat ▶ Validate EnemyHitReaction` → 14/14
- `LevelGen ▶ Combat ▶ Validate EnemyDeath` → 16/16
- `LevelGen ▶ Input ▶ Validate MouseLook` → 7/7 (or 6/7 + SKIP in edit mode)

Why isn't this folded into Build? Two reasons. (1) The "Add X"
adders carry external asset dependencies (FBX AnimationEvents,
audio clips, CharacterStats SO refs) that aren't owned by the
Player prefab — folding them into the prefab builder couples
unrelated subsystems. (2) Several adders (PlayerCombat,
PlayerHUD's stats wiring, M11 hit-reception bump) were authored
in different milestones with different ownership boundaries; the
"adder per concern" pattern keeps each milestone's prefab change
auditable and revertable. Rebuild is the heavy hammer; adders
are the surgical follow-up.

> **Superseded by M12-R (2026-05-11).** The 7-step prefab-adder
> cascade above is no longer needed — `PlayerHeroBuilder` consolidates
> every component AddComponent + ref wiring into a single idempotent
> menu. Steps 8–9 (scene-side CM camera rebind + `_MouseLock`)
> remain valid since they're scene-side, not prefab-side. The
> validator list under "Validators (sanity sweep)" is replaced by
> the single `LevelGen ▶ Player ▶ Validate Player_Hero` menu.

## M12-R — Player Hero refactor (2026-05-11)

Refactor milestone. Consolidates ~13 per-milestone "Add X to
Player_MaleHero Prefab" editor scripts and 3 milestone-specific
validators into a single root manifest component
(`PlayerHero`), a single idempotent builder
(`PlayerHeroBuilder`), and a single consolidated validator
(`PlayerHeroValidator`). Renames the prefab GUID-preservingly
from `Player_MaleHero.prefab` to `Player_Hero.prefab`.

Architectural decisions (locked):
- `PlayerHero` is a MANIFEST: declares every required sibling
  via `[RequireComponent]`, holds a SerializeField ref to each,
  exposes a public read-only property per ref. NO Update, NO
  gameplay logic, NO Animator writes. Sole purpose: contract
  + lookup table.
- Every per-milestone "Add X to Player_MaleHero Prefab" adder
  is replaced by the single `LevelGen ▶ Player ▶ Build Player_Hero
  Prefab` menu. Builder is idempotent: re-runnable with no
  side effects, logs ADDED / ALREADY PRESENT per component.
- 3 per-milestone validators (`PlayerStaminaValidator`,
  `PlayerDeathValidator`, `PlayerDodgeValidator`) replaced by
  one `PlayerHeroValidator` with 50 checks covering: prefab
  structure (1-3), every required root component (4-17),
  no duplicates (18), every PlayerHero SerializeField ref
  (19-32), CharacterStatsRuntime/InputReader/Combat/Dodge
  API surface (33-42), Animator parameter + state shape
  (43-48 — Dodge + Death regression + Attack/Hit regression),
  PlayerDeath event (49), and a script-presence sanity check
  (50). Per-milestone Animator/Combat/Combo/Jump/Runtime
  validators NOT in the explicit delete list remain — they
  test cross-cutting concerns broader than the Player prefab
  manifest.
- Prefab rename is GUID-preserving via
  `AssetDatabase.RenameAsset` at the top of `PlayerHeroBuilder`.
  Scene references resolve automatically because Unity tracks
  prefab refs by GUID, not by filename. One-shot rename — once
  `Player_Hero.prefab` exists, subsequent builder runs skip the
  rename step.

Spec deviations / interpretation notes:
- `[RequireComponent(typeof(Animator))]` on PlayerHero was
  intentionally OMITTED. The Animator lives on the
  `MaleCharacterPBR` child by design (humanoid FBX rig);
  forcing one on the root would auto-add a second, controller-
  less Animator that would conflict with the child rig and
  break `PlayerAnimator.GetComponentInChildren<Animator>`.
- `[RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]`
  was ADDED to PlayerHero even though the M12-R locked list
  didn't include it. Without PlayerInput, PlayerInputReader
  receives zero callbacks (UnityEvent dispatch source).
- `PlayerController` ↔ `PlayerStamina` cannot both
  `[RequireComponent]` each other (Unity rejects circular
  attrs). `PlayerStamina → PlayerController` is the canonical
  direction (it already had it); `PlayerController._stamina`
  remains null-tolerant in script. PlayerHero's manifest carries
  both, satisfying the root-level contract.
- `PlayerDodge → PlayerDeath` cannot be `[RequireComponent]`'d
  because `PlayerController` now requires `PlayerDodge`, and
  `PlayerDeath` requires `PlayerController` — adding the
  `PlayerDodge → PlayerDeath` edge closes a cycle through three
  scripts. `PlayerDodge._death` remains null-tolerant.
- `WeaponHitbox` child build (under `weapon_r`) is NOT folded
  into `PlayerHeroBuilder` — kept in `PlayerCombatHitboxBuilder`
  because it requires deep bone-tree surgery (search the
  imported FBX skeleton for a named bone, parent a trigger
  collider under it). Different responsibility than the
  root-component manifest. The M12-R validator still checks
  for the WeaponHitbox child (check 41) but doesn't build it.
- AnimationEvent edits on Attack01-03 FBX clips are likewise
  NOT folded into PlayerHeroBuilder — `PlayerCombatHitboxBuilder
  ▶ Add Animation Events to Attack Clips` retains that
  responsibility (FBX-side .meta edits, not prefab-side).
- CharacterStats rate-setting (`CharacterStatsAssetUpdater`) was
  in the delete list. Stamina drain/regen rates (25/33 on the
  Player asset) are now manually-authored ScriptableObject
  values; they only need to be set once on
  `CharacterStats_Player.asset` and persist via the .asset file.
  If a future milestone needs to bulk-set rates across many
  CharacterStats assets, a replacement utility goes alongside
  the new feature.

RequireComponent chains extended on each script (audit-driven):

  Script              Existing                          Added (M12-R)
  ─────────────────────────────────────────────────────────────────────
  PlayerCombat        InputReader, Animator             CharacterStatsRuntime
  PlayerStamina       CharacterStatsRuntime, Controller PlayerDeath
  PlayerInteractor    InputReader                       PlayerDeath
  PlayerHitReaction   Targetable                        PlayerCombat,
                                                        CharacterStatsRuntime
  PlayerDodge         CC, Stats, InputReader            PlayerCombat,
                                                        PlayerAnimator
  PlayerDeath         CharacterStatsRuntime             PlayerAnimator,
                                                        PlayerController,
                                                        PlayerCombat
  PlayerController    CC, InputReader, Animator         PlayerCombat,
                                                        PlayerDodge
                                                        (NOT PlayerStamina
                                                         — cycle)

PlayerHero declares ALL 14 root components (Animator excluded
as noted; PlayerInput added as noted):

  CharacterController, UnityEngine.InputSystem.PlayerInput,
  CharacterStatsRuntime, Targetable, PlayerInputReader,
  PlayerAnimator, PlayerController, PlayerCombat, PlayerStamina,
  PlayerDodge, PlayerHitReaction, PlayerDeath, PlayerInteractor,
  MouseLook.

`MouseLook` migration: moved from scene-side `_MouseLock`
singleton GameObject to the Player_Hero prefab root. The script
itself is unchanged — Awake/OnEnable locks, OnDisable unlocks.
Per-scene `LevelGen ▶ Input ▶ Place _MouseLock in Active Scene`
menu still exists but is now redundant on scenes that contain a
Player_Hero instance. If both an `_MouseLock` GameObject AND
Player_Hero coexist in a scene, MouseLookValidator's "exactly
one active MouseLook" check fails — delete the scene-side
`_MouseLock` to resolve.

Files DELETED (13, per spec):
- Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs
- Assets/Scripts/Player/Editor/PlayerDodgePrefabAdder.cs
- Assets/Scripts/Player/Editor/PlayerStaminaPrefabAdder.cs
- Assets/Scripts/Player/Editor/PlayerTakesDamagePrefabAdder.cs
- Assets/Scripts/Player/Editor/PlayerDeathPrefabAdder.cs
- Assets/Scripts/Player/Editor/PlayerInteractorPrefabAdder.cs
- Assets/Scripts/Player/Editor/PlayerBaseControllerExtender.cs
- Assets/Scripts/Player/Editor/PlayerBaseControllerDodgeExtender.cs
- Assets/Scripts/Player/Editor/PlayerCapsuleTuner.cs
- Assets/Scripts/Player/Editor/PlayerStaminaValidator.cs
- Assets/Scripts/Player/Editor/PlayerDeathValidator.cs
- Assets/Scripts/Player/Editor/PlayerDodgeValidator.cs
- Assets/Scripts/Combat/Editor/CharacterStatsAssetUpdater.cs

Files NOT deleted (per strict reading of the explicit delete
list, even though the Goal text mentions "all individual adder
scripts"):
- Assets/Scripts/Player/Editor/PlayerCombatPrefabAdder.cs
  (M2-B adder; now redundant with PlayerHeroBuilder but still
  functional with path strings updated to Player_Hero)
- Assets/Scripts/Combat/Editor/PlayerCombatHitboxBuilder.cs
  (WeaponHitbox + AnimationEvents authoring; specialized
  bone-tree work that isn't appropriate for PlayerHeroBuilder)
- Assets/Scripts/Player/Editor/PlayerCombatValidator.cs
- Assets/Scripts/Player/Editor/PlayerCombatAnimatorValidator.cs
- Assets/Scripts/Player/Editor/PlayerComboAnimatorValidator.cs
- Assets/Scripts/Player/Editor/PlayerComboRuntimeValidator.cs
- Assets/Scripts/Player/Editor/PlayerJumpAnimatorValidator.cs
- Assets/Scripts/Player/Editor/PlayerJumpRuntimeValidator.cs
  (Animator-graph / runtime-behavior validators — they test
  cross-cutting concerns beyond the Player prefab manifest;
  worth keeping as regression guards. Strings updated to
  Player_Hero.)

Files updated for the prefab rename (Player_MaleHero →
Player_Hero string replacements in code constants, Debug.Log
messages, and Tooltip docstrings):
- Assets/Scripts/Combat/Editor/DamageRoutingValidator.cs
- Assets/Scripts/Combat/Editor/EnemyCombatValidator.cs
- Assets/Scripts/Combat/Editor/PlayerCombatHitboxBuilder.cs
- Assets/Scripts/Combat/AnimationEventForwarder.cs (comment)
- Assets/Scripts/Combat/HitboxRelay.cs (comment)
- Assets/Scripts/Interaction/Editor/InteractSystemValidator.cs
- Assets/Scripts/Interaction/Interactable.cs (Tooltip)
- Assets/Scripts/Player/Editor/PlayerCombatPrefabAdder.cs
- Assets/Scripts/Player/Editor/PlayerCombatValidator.cs
- Assets/Scripts/Player/Editor/PlayerJumpRuntimeValidator.cs
- Assets/Scripts/Player/Editor/M3_02A_PackSwapExecutor.cs
- Assets/Scripts/Player/Editor/M3_03B_DuoReimportVerifier.cs
- Assets/Scripts/Player/Editor/M4_SampleSceneSetup.cs
- Assets/Scripts/Player/Editor/M5_FallingDiagnosis.cs
- Assets/Scripts/Player/PlayerCombat.cs (Debug.LogError msg)
- Assets/Scripts/Player/PlayerSpawner.cs (Tooltip)
- Assets/Scripts/UI/Editor/PlayerHUDBuilder.cs
- Assets/Scripts/UI/Editor/PlayerHUDValidator.cs
- Assets/Scripts/UI/PlayerHUD.cs (Debug.LogWarning msg)
- Assets/Scripts/UI/PlayerDeathOverlay.cs (header comment +
  Debug.LogWarning msg)

Files created:
- Assets/Scripts/Player/PlayerHero.cs
- Assets/Scripts/Player/Editor/PlayerHeroBuilder.cs
- Assets/Scripts/Player/Editor/PlayerHeroValidator.cs

Player scripts modified (RequireComponent chain extensions):
- Assets/Scripts/Player/PlayerCombat.cs
- Assets/Scripts/Player/PlayerStamina.cs
- Assets/Scripts/Player/PlayerInteractor.cs
- Assets/Scripts/Player/PlayerHitReaction.cs
- Assets/Scripts/Player/PlayerDodge.cs
- Assets/Scripts/Player/PlayerDeath.cs
- Assets/Scripts/Player/PlayerController.cs (with explanatory
  comment about the PlayerStamina cycle exception)

Prefab path:
- OLD: Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab
- NEW: Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab
  (rename happens GUID-preservingly inside
   `PlayerHeroBuilder.RenameOldPrefabIfNeeded` on first run;
   scene references auto-resolve)

Pending follow-up (Jason runs after CC completes):
1. `LevelGen ▶ Player ▶ Build Player_Hero Prefab` — performs
   the rename (if needed), ensures all 14 components are
   present, wires all PlayerHero SerializeField refs, wires
   PlayerInputReader UnityEvent bindings (10 actions).
   Expect log lines: prefab status (CREATED or REWIRED),
   components added/already, 10/10 UnityEvent bindings.
2. `LevelGen ▶ Player ▶ Validate Player_Hero` — expect
   50 PASS / 0 FAIL.
3. If WeaponHitbox child is missing (validator check 41
   FAIL):
   `LevelGen ▶ Combat ▶ Add Weapon Hitbox to Player_Hero`
4. If Attack01-03 AnimationEvents missing (validator
   check is in DamageRoutingValidator):
   `LevelGen ▶ Combat ▶ Add Animation Events to Attack Clips`
5. Open the test scene that previously had Player_MaleHero
   placed — confirm the prefab instance shows as Player_Hero
   in the Hierarchy (GUID auto-resolved).
6. Re-bind `CM Follow Camera`'s Follow + LookAt to the new
   prefab instance's `CameraTarget` child (M2-A scene-side
   step — see post-rebuild checklist above for the manual
   re-bind procedure).
7. If both `_MouseLock` GameObject AND Player_Hero coexist
   in the scene — delete `_MouseLock` (MouseLook now lives
   on Player_Hero root).
8. Sanity re-runs:
   - `LevelGen ▶ Combat ▶ Validate Damage Routing` → 12/12
   - `LevelGen ▶ Combat ▶ Validate Enemy Combat` → 17/17
   - `LevelGen ▶ Combat ▶ Validate EnemyHitReaction` → 14/14
   - `LevelGen ▶ Combat ▶ Validate EnemyDeath` → 16/16
   - `LevelGen ▶ Combat ▶ Validate Enemy AI` → 16/16
   - `LevelGen ▶ Combat ▶ Validate Combat Foundation` → 12/12
   - `LevelGen ▶ UI ▶ Validate Damage Numbers` → 14/14
   - `LevelGen ▶ UI ▶ Validate PlayerHUD` → 11/11
   - `LevelGen ▶ Interaction ▶ Validate Interact System` → 16/16
   - `LevelGen ▶ Interaction ▶ Validate OpenInteractable` → 12/12
   - `LevelGen ▶ Input ▶ Validate MouseLook` → 7/7 (or 6/7 + SKIP
     in edit mode)
9. Play-mode smoke test: movement, sprint, jump, attack combo
   (3-hit), hit reactions, death + overlay + restart, dodge
   (V key, 4 directions), i-frames during dodge, stamina cost,
   cooldown, interactables (Assassinate behind Dummy +
   OpenInteractable on TestDoor).

Deferred / not in M12-R scope:
- Enemy-side `EnemyBase` equivalent manifest (separate
  milestone — enemy domain has different concerns).
- Folding `PlayerCombatPrefabAdder` and
  `PlayerCombatHitboxBuilder` into `PlayerHeroBuilder` (left
  alone per strict reading of explicit delete list).
- Replacement for `CharacterStatsAssetUpdater` (rates now
  manually authored on the .asset; bulk-set utility can return
  if needed later).
- Cleanup of older milestone validators that test Animator-only
  / runtime-only concerns (PlayerComboAnimatorValidator etc.)
  — still useful as regression guards; consolidating them into
  PlayerHeroValidator would require duplicating a lot of
  Animator-graph traversal logic. Left as a follow-up cleanup.

Lessons logged for this milestone:
- Unity rejects circular `[RequireComponent]` attributes
  (verified by reasoning: PlayerController ↔ PlayerStamina,
  PlayerDodge ↔ PlayerDeath via PlayerController would form
  cycles). Resolution: the manifest at the root level
  (PlayerHero) carries all the components; per-script
  RequireComponent chains follow one direction only. Document
  the exception with an inline comment so future readers know
  why a "missing" RequireComponent isn't an oversight.
- `[RequireComponent]` enforces same-GameObject only — a
  component on a child cannot satisfy a root-level
  RequireComponent. The Animator-on-child constraint (humanoid
  FBX rig structure) is a hard architectural reality; the
  manifest must omit it and the per-script
  `GetComponentInChildren` lookup carries that responsibility.
- `AssetDatabase.RenameAsset` preserves GUID. Scene references
  by GUID resolve to the renamed asset automatically — no
  scene edits needed. The internal asset name (used for
  display in the Inspector and Project window) updates to
  match the new filename on next AssetDatabase refresh.
- "Adder per concern" was a deliberate ownership pattern
  through M2-B–M12, traded for "every milestone touches one
  file" auditability. M12-R consolidates because the prefab
  is now mature enough that re-running 10+ adders sequentially
  after every rebuild was burning more time than the
  per-milestone auditability gained. Future milestones should
  decide which side of this tradeoff they fall on: small,
  contained changes can still ship a one-shot adder; large
  architectural shifts should fold into PlayerHeroBuilder.

### M12-R post-ship: camera-rig restoration + runtime auto-bind

Two issues surfaced during M12-R verification that prompted
follow-up work:

**1. Lost camera-to-player binding after prefab rebuild.**
The same pattern that bit M12 (scene's `CinemachineCamera.Follow`
+ `LookAt` resolve to a dead `CameraTarget` transform after a
rebuild) re-occurred in M12-R. Fixed structurally rather than
with another manual rebind by adding a runtime auto-bind component.

`Assets/Scripts/Player/CinemachineAutoBind.cs` (NEW):
  Sits on the `CinemachineCamera` GameObject. On `Start`, runs
  a retry coroutine (poll every 0.25s, give up after 10s) that
  finds the GameObject tagged `Player`, locates its `CameraTarget`
  child by name, and writes both `vcam.Follow` and `vcam.LookAt`.
  `[DefaultExecutionOrder(200)]` ensures it runs after the
  Player's Awake. Single-fire — once bound, the coroutine exits.
  Handles three scenarios that scene-saved-by-hand bindings fail at:
  (a) prefab rebuild orphans the scene-side ref, (b) Player_Hero
  spawns deferred at runtime, (c) new scene with a CM rig but
  no manual hand-binding yet.

`Assets/Scripts/Player/Editor/CinemachineAutoBindAdder.cs` (NEW):
  One-shot menu `LevelGen ▶ Player ▶ Add Cinemachine Auto-Bind to
  Active Scene`. Idempotent — adds `CinemachineAutoBind` to the
  scene's `CinemachineCamera` if not already present. For
  retrofitting auto-bind onto an existing CM rig.

**2. M2-A camera-setup menu was a casualty of M12-R deletes.**
`PlayerPrefabBuilder.cs` (deleted) contained the
`Add Cinemachine Follow Camera to Active Scene` menu —
the canonical way to set up the CM rig in a fresh test scene.
After M12-R, scenes without a CM rig had no way to create one,
and `PlayerController.Awake` logged `Camera.main is null` since
no MainCamera-tagged GameObject existed.

`Assets/Scripts/Player/Editor/CinemachineRigBuilder.cs` (NEW):
  Restores the menu `LevelGen ▶ Player ▶ Add Cinemachine Follow
  Camera to Active Scene` as a standalone editor file. Lifted
  from git history of the deleted `PlayerPrefabBuilder.cs` with
  one fold-in: `CinemachineAutoBind` is now auto-added to the
  new vcam GameObject as part of the standard rig build, so
  no future scene-setup ever needs the manual rebind step.
  All M2-A tuning preserved: OrbitalFollow Sphere R=4,
  HorizontalAxis (-180,180) wrap, VerticalAxis (-10,70) init=15°,
  RotationComposer, Deoccluder MinDistance=1.0, Reader Gain
  ±10 (Y inverted) on Player/Look.

Combined effect: after running `Add Cinemachine Follow Camera to
Active Scene` once per test scene, future prefab rebuilds never
break the camera. Pressing Play binds the camera automatically
via the embedded `CinemachineAutoBind`. The "no display camera"
class of bug should be retired.

**Updated post-M12-R post-rebuild checklist:**
The "Re-bind CM Follow Camera" manual step (step 6 in M12-R
pending follow-up) is now obsolete — `CinemachineAutoBind`
handles it on Play. Step 6 retained in the milestone history
above for context, but new sessions should skip it.

Lesson logged:
- Scene-saved component references to prefab children are fragile
  across prefab rebuilds. The "drag the child into the slot"
  scene-author workflow looks robust until the prefab is rebuilt
  and the child's fileID changes — Unity silently keeps the dead
  reference. Pattern fix: a small runtime auto-bind component on
  the consumer side that resolves the reference by tag + name on
  `Start`. Cheap (one tag lookup + one Transform.Find at startup),
  resilient to all prefab churn. Worth applying to other scene→
  prefab references that have caused friction (PlayerHUD, etc.)
  if they ever break the same way.

## M-MenuCleanup — LevelGen menu consolidation (2026-05-11)

Cleanup milestone. Collapses the LevelGen menu from 8 submenus +
scattered items into a production-oriented set. Removes the
Diagnostics, Pack Swap, Scene Setup, and top-level Validate
submenus. Consolidates 8 per-milestone validators into 2 new
consolidated validators (`ValidateEnemy`, `ValidateInteraction`).
Adds an Input-submenu duplicate of the Cinemachine rig-builder
menu for discoverability.

Architectural decisions (locked):
- Two new consolidated validators replace 8 deleted per-milestone
  validators. The per-milestone "validate this exact step's
  wiring" pattern is retired in favor of domain-level "validate
  this entire subsystem" validators that survive milestone churn.
- Test scripts in `Assets/Scripts/LevelEditor/Editor/` (Doorway,
  EdgeSolver, RoomPiece, ShapeStamp) that produced console-dump
  smoke tests are deleted. They had no ongoing production value
  — pure milestone scaffolding from the V1→V2 cell-map refactor.
- The 5 M3/M4/M5/M6 one-off scaffolding scripts (Pack Swap
  executor + verifier, sample-scene setup, falling diagnosis,
  floor-collider stopgap) that drove the Diagnostics / Pack Swap
  / Scene Setup submenus are deleted. They were marked
  "one-off scaffolding, can be deleted" in their own headers
  back when they were written — M-MenuCleanup is the cleanup
  that always lurked next on the deletion queue.
- `CinemachineRigBuilder.Build` now carries TWO `[MenuItem]`
  attributes (Player + Input submenus). Same method, two
  invocation paths. Lets the user find the menu from either
  the input-system mental model OR the player-camera mental
  model.
- The target menu spec listed fewer items per submenu than
  the actual end state. The "Do not touch" file list in the
  spec is the authority — files like `EnemyBaseControllerBuilder`,
  `PlayerCombatHitboxBuilder`, `TestDoorBuilder`, the M2-B
  Animator/Runtime validators, etc. retain their menus because
  the files themselves are protected. Treat the target menu
  spec as aspirational; "Do not touch" supersedes "everything
  not in list is deleted".

Two new validators:

`Assets/Scripts/Combat/Editor/ValidateEnemy.cs` (NEW):
  Menu: `LevelGen ▶ Combat ▶ Validate Enemy`.
  32 checks. Consolidates the 6 deleted enemy-side validators
  (DummyAndStats, EnemyHitReaction, EnemyDeath, DamageRouting,
  EnemyAI, EnemyCombat). Covers:
    - CharacterStats_Dummy asset existence + CharacterStatsRuntime
      on Dummy root + IsInvulnerable / SetInvulnerable / ApplyDamage
      API (checks 1-5)
    - Targetable on Dummy root + OnHit / AnyTargetableHit / RaiseHit
      surface with correct signatures (checks 6-9)
    - EnemyHitReaction component + RequireComponent(Targetable) +
      DisallowMultipleComponent + HandleHit(Vector3, float)
      (checks 10-13)
    - EnemyDeath component + CharacterStatsRuntime.OnDied event +
      IsDead property (checks 14-16)
    - EnemyBaseController asset + Dummy Animator references it (not
      PlayerBaseController) + Hit / Death / MoveSpeed / Attack
      parameters present + AnyState→Hit canTransitionToSelf=false +
      Death state is terminal (checks 17-24)
    - EnemyCombat component + EnemyWeaponHitbox child under
      weapon_r + EnemyAnimationEventForwarder on MaleCharacterPBR
      child + friendly-fire CompareTag("Player") source scan
      (checks 25-28)
    - EnemyAI component + NavMeshAgent component + _attackRange > 0
      + _detectionRange > 0 (checks 29-32)
  Format mirrors PlayerHeroValidator: `[Validator] PASS / FAIL`
  per check, summary line at end.

`Assets/Scripts/Interaction/Editor/ValidateInteraction.cs` (NEW):
  Menu: `LevelGen ▶ Interaction ▶ Validate Interaction`.
  16 checks. Consolidates InteractSystemValidator (M6) +
  OpenInteractableValidator (M7). Covers:
    - Interactable.cs presence + abstract base shape with
      IsEligible(GameObject) + Execute(GameObject) abstract methods +
      RefreshPromptLabel() (checks 1-3)
    - AssassinateInteractable.cs + subclass relationship +
      _AssassinateZone child on Dummy.prefab (checks 4-6)
    - OpenInteractable.cs + subclass relationship + TestDoor.prefab
      existence (checks 7-9)
    - PlayerInteractor.cs + component on Player_Hero.prefab +
      static Instance property surface (checks 10-12)
    - PlayerInputReader.InteractPressed event + OnInteract
      endpoint method (checks 13-14)
    - Dummy AssassinateInteractable on _AssassinateZone + PlayerHero
      _interactor SerializeField non-null (checks 15-16)

  Two deviations from spec (documented in script header + here):
  - Spec said abstract methods were `Execute()` + `CanExecute()`.
    Real API is `IsEligible(GameObject)` + `Execute(GameObject)`.
    Validator checks the real names.
  - Spec check 12 said `PlayerInteractor._interactRadius > 0`.
    Per architecture, PlayerInteractor has no radius field —
    interaction radii live on each Interactable subclass's
    SphereCollider. Replaced with a check that PlayerInteractor
    exposes its singleton `Instance` static property.

Input-submenu Cinemachine entry:
- `CinemachineRigBuilder.Build()` gained a second `[MenuItem]`
  attribute: `LevelGen/Input/Add Cinemachine Follow Camera to
  Active Scene`. Same method, two menu paths. The Player
  submenu copy remains.

Files DELETED (18):

LevelEditor test scripts (4):
- Assets/Scripts/LevelEditor/Editor/Doorway_Test.cs
- Assets/Scripts/LevelEditor/Editor/EdgeSolver_Test.cs
- Assets/Scripts/LevelEditor/Editor/RoomPiece_Test.cs
- Assets/Scripts/LevelEditor/Editor/ShapeStamp_Test.cs

LevelEditor top-level Validate menu source (1):
- Assets/Scripts/LevelEditor/Editor/RoomBuildValidator.cs

Pack Swap submenu sources (2):
- Assets/Scripts/Player/Editor/M3_02A_PackSwapExecutor.cs
- Assets/Scripts/Player/Editor/M3_03B_DuoReimportVerifier.cs

Scene Setup submenu sources (2):
- Assets/Scripts/Player/Editor/M4_SampleSceneSetup.cs
- Assets/Scripts/Player/Editor/M6_FloorColliderStopgap.cs

Diagnostics submenu source (1):
- Assets/Scripts/Player/Editor/M5_FallingDiagnosis.cs

Per-milestone validators replaced by ValidateEnemy (6):
- Assets/Scripts/Combat/Editor/DummyAndStatsValidator.cs
- Assets/Scripts/Combat/Editor/EnemyHitReactionValidator.cs
- Assets/Scripts/Combat/Editor/EnemyAIValidator.cs
- Assets/Scripts/Combat/Editor/EnemyCombatValidator.cs
- Assets/Scripts/Combat/Editor/EnemyDeathValidator.cs
- Assets/Scripts/Combat/Editor/DamageRoutingValidator.cs

Per-milestone validators replaced by ValidateInteraction (2):
- Assets/Scripts/Interaction/Editor/InteractSystemValidator.cs
- Assets/Scripts/Interaction/Editor/OpenInteractableValidator.cs

Files created (2):
- Assets/Scripts/Combat/Editor/ValidateEnemy.cs (32 checks)
- Assets/Scripts/Interaction/Editor/ValidateInteraction.cs (16 checks)

Files modified (1):
- Assets/Scripts/Player/Editor/CinemachineRigBuilder.cs
  (added second [MenuItem] for Input submenu)

Menus kept beyond the target spec (per "Do not touch" + pragmatic
"don't delete files that aren't in the explicit delete list"):

Combat submenu (target 4 items, actual 8):
- Build Dummy Prefab ✓ in target
- Place Dummy in Active Scene ✓ in target
- Bake Test Scene NavMesh ✓ in target
- Validate Enemy ✓ in target (new this milestone)
- Build EnemyBaseController (EnemyBaseControllerBuilder — "Do not touch")
- Add Weapon Hitbox to Player_Hero (PlayerCombatHitboxBuilder)
- Add Collider to Dummy (PlayerCombatHitboxBuilder)
- Add Animation Events to Attack Clips (PlayerCombatHitboxBuilder)

Player submenu (target 3 items, actual 9):
- Build Player_Hero Prefab ✓ in target
- Add Cinemachine Follow Camera to Active Scene ✓ in target
- Validate Player_Hero ✓ in target
- Add Cinemachine Auto-Bind to Active Scene (CinemachineAutoBindAdder)
- Add PlayerCombat to Player_Hero Prefab (PlayerCombatPrefabAdder)
- Validate Combat Animator (M2-B Step 2)
- Validate PlayerCombat Wiring (M2-B Step 3)
- Validate Jump Animator (M2-B Step 4)
- Validate Jump Runtime (M2-B Step 5)
- Validate Combo Animator (M2-B Step 6)
- Validate Combo Runtime (M2-B Step 7)

UI submenu (target 4 items, actual 8):
- Build DamageNumber Prefab ✓ in target
- Build DamageNumberSpawner Prefab ✓ in target
- Place DamageNumberSpawner in Active Scene ✓ in target
- Validate Damage Numbers ✓ in target
- Build PlayerHUD Prefab + Place PlayerHUD + Add CharacterStatsRuntime
  to Player_Hero + Validate PlayerHUD (PlayerHUDBuilder, PlayerHUDValidator)
- Build PlayerDeathOverlay Prefab + Place PlayerDeathOverlay in Active Scene
  (PlayerDeathOverlayBuilder)

Interaction submenu (target 1 item, actual 3):
- Validate Interaction ✓ in target (new this milestone)
- Build TestDoor Prefab + Place TestDoor in Active Scene (TestDoorBuilder)

Input submenu (target 3 items, actual 3):
- Place _MouseLock in Active Scene ✓ in target
- Add Cinemachine Follow Camera to Active Scene ✓ in target (NEW path
  this milestone — duplicate MenuItem on CinemachineRigBuilder)
- Validate MouseLook ✓ in target

Top-level (target 3 items, actual 3):
- LVL Configurator ✓ unchanged
- V2 Level Generator ✓ unchanged
- Whitebox [Complete] ✓ unchanged (8 sub-menus)

Pending follow-up (Jason runs after CC completes):
1. Reopen Unity — Diagnostics / Pack Swap / Scene Setup / top-level
   Validate submenus should be gone. Other extra menus remain as
   noted above.
2. `LevelGen ▶ Combat ▶ Validate Enemy` — expect 32 PASS / 0 FAIL.
3. `LevelGen ▶ Interaction ▶ Validate Interaction` — expect 16 PASS /
   0 FAIL.
4. `LevelGen ▶ Player ▶ Validate Player_Hero` — sanity re-run,
   confirm no regression (50/0).
5. Play-mode smoke test — all prior behaviors still work (movement,
   combo, dodge, hit reactions, death, interact, NavMesh-driven AI).

Deferred / not in M-MenuCleanup scope:
- Folding the M2-B animator/runtime validators (PlayerCombatAnimatorValidator,
  PlayerCombatValidator, PlayerJumpAnimatorValidator, PlayerJumpRuntimeValidator,
  PlayerComboAnimatorValidator, PlayerComboRuntimeValidator) into PlayerHeroValidator
  — would duplicate a lot of Animator-graph traversal logic and these still
  function as regression guards for the specific M2-B milestones. M12-R deferred
  this too; M-MenuCleanup defers it further.
- Folding PlayerCombatPrefabAdder + PlayerCombatHitboxBuilder + PlayerHUDBuilder +
  PlayerDeathOverlayBuilder into PlayerHeroBuilder — would couple UI builders,
  prefab adders, and bone-tree surgery into one mega-builder. Per
  M12-R's "decide which side of this tradeoff you fall on" guideline,
  these are small-scope additions that work fine as standalone menus.

Lessons logged for this milestone:
- "One [MenuItem] attribute" is the default; "two [MenuItem]
  attributes on the same method" is also valid and creates two
  menu paths. Useful when the same operation belongs to multiple
  mental models (Camera setup is both a Player concern and an
  Input concern).
- Spec-as-prompt vs spec-as-contract: M-MenuCleanup's target menu
  was a mental model, not a strict deletion contract. The
  "Do not touch" list was the actual override. Future cleanup
  milestones should expect this pattern — the "do not touch"
  list is the precise contract; "everything not in list" is
  aspirational and applied with judgment.
- Per-milestone validators have a natural lifecycle: they ship
  with their milestone (high value at the moment of integration),
  remain useful as regression guards for ~2-3 follow-up milestones,
  then become noise. Mass-consolidation into a domain-level
  validator at the ~5-10 milestone mark is the right pattern.
  ValidateEnemy (32 checks) replaced 6 per-milestone validators
  that totaled ~80 checks combined — much of the original check
  surface was duplicate API-surface assertions that the
  consolidated validator only needs once.

## M13-EnemyBase — EnemyBase component + Enemy_Grunt archetype (2026-05-11)

Establishes `EnemyBase` as the self-configuring root component for all
enemies — the enemy equivalent of `PlayerHero`. Promotes the Dummy's
combat scaffolding into the first concrete enemy archetype, `Enemy_Grunt`,
built on top of `EnemyBase`. Introduces `EnemyData` as a per-enemy
ScriptableObject (HP, attack damage, AI ranges, movement speed) replacing
hardcoded SerializeField defaults at runtime.

Architectural decisions (locked):
- `EnemyBase` is a MANIFEST: declares every required sibling via
  `[RequireComponent]`, holds a SerializeField ref to each, exposes a
  public read-only property per ref. Mirrors PlayerHero pattern.
  ONE additive behavior: Awake push-down (see below).
- `EnemyData` ScriptableObject lives in `LevelGen.Combat` namespace
  (data/combat concern). One asset per enemy archetype. CharacterStats
  remains the canonical HP/Stamina template for the PLAYER — enemies
  get the EnemyData path; the player path is unchanged.
- Single push-down site: only `EnemyBase.Awake` reads `_data` and pushes
  values into consumers via `InitFromEnemyData(EnemyData)` on
  `CharacterStatsRuntime` / `EnemyAI` / `EnemyCombat`. Consumers never
  read `EnemyData` directly — preserves single-direction dependency.
- `[DefaultExecutionOrder(-50)]` on EnemyBase guarantees its Awake runs
  BEFORE CharacterStatsRuntime/EnemyAI Awake (both at default 0). Without
  this, consumers' own Awake would init from their SerializeField
  defaults before EnemyBase could push EnemyData values.
- `CharacterStatsRuntime.InitFromEnemyData(EnemyData)` writes to a new
  `_enemyData` field. Awake then checks `_enemyData != null` FIRST; if
  set, inits `currentHP = _enemyData.maxHP` and uses EnemyData as the
  source for `MaxHP` + `DisplayName` getters. Else falls through to the
  existing CharacterStats SO path (player-side).
- `EnemyAI.InitFromEnemyData` pushes detection/attack/leash ranges +
  chaseSpeed + stoppingDistance + attackCooldown into its SerializeField
  fields. `EnemyCombat.InitFromEnemyData` pushes attackDamage (cast
  float → int via `Mathf.RoundToInt`).
- `Dummy.prefab` is left UNTOUCHED. Enemy_Grunt is a fresh production
  prefab built from scratch via `EnemyBaseBuilder`. Both prefabs share
  the SAME `EnemyBaseController` (no graph changes this milestone) and
  the SAME `MaleCharacterPBR` model rig.
- `EnemyBaseValidator` REPLACES `ValidateEnemy.cs` from M-MenuCleanup
  (same menu path `LevelGen ▶ Combat ▶ Validate Enemy`). 41 checks:
  the original 32 (still Dummy-targeted) + 9 new M13-EnemyBase checks
  targeting Enemy_Grunt + EnemyBase + EnemyData.

Spec deviations from the M13-EnemyBase prompt:
1. `Animator` is NOT in EnemyBase's `[RequireComponent]` chain because
   the Animator lives on the `MaleCharacterPBR` child by design (FBX
   humanoid rig). Forcing one on the root would auto-add a duplicate,
   controller-less Animator (same lesson as PlayerHero / M12-R).
   EnemyBase still SerializeField-holds the child Animator ref via
   `_animator`, resolved by `EnemyBaseBuilder` at build time.
2. EnemyData defaults match the spec's `EnemyData_Grunt` values, not
   the field-level defaults (e.g. `detectionRange = 6f` default matches
   Grunt; spec also mentioned `detectionRange = 6f`). Range OnValidate
   uses non-strict ordering (`<=`) with auto-bump-up on violation, so
   misordered values get nudged into validity instead of silently kept.

[RequireComponent] chain audit results:
  EnemyHitReaction:  Targetable                                  (UNCHANGED)
  EnemyDeath:        CharacterStatsRuntime, Targetable,
                     + EnemyHitReaction                          (ADDED)
  EnemyCombat:       + CharacterStatsRuntime                     (ADDED)
  EnemyAI:           NavMeshAgent, CharacterStatsRuntime         (UNCHANGED)

`Enemy_Grunt.prefab` hierarchy:
```
Enemy_Grunt (root, tag = default)
  ├── EnemyBase           — manifest, [DefaultExecutionOrder(-50)]
  ├── NavMeshAgent
  ├── CapsuleCollider     (radius=0.4, height=1.8, center=(0,0.9,0))
  ├── CharacterStatsRuntime  (stats=null; EnemyData drives values)
  ├── Targetable
  ├── EnemyAI
  ├── EnemyCombat
  ├── EnemyHitReaction
  ├── EnemyDeath
  └── MaleCharacterPBR (model child)
        ├── Animator → EnemyBaseController
        ├── EnemyAnimationEventForwarder (._combat → EnemyCombat)
        └── weapon_r
              └── EnemyWeaponHitbox
                    ├── BoxCollider (trigger, disabled)
                    ├── Rigidbody (kinematic)
                    └── EnemyHitboxRelay (._combat → EnemyCombat)
```

`EnemyData_Grunt.asset` defaults (HP higher than Dummy's 50 to support
multi-hit combat without the M4-B Dummy convenience):
```
enemyName        Grunt
maxHP            80
attackDamage     10
defense          2
moveSpeed        3.5
rotationSpeed    10
detectionRange   6
attackRange      1.3
leashRange       10
stoppingDistance 1.0
attackCooldown   1.5
```

Files created:
- Assets/Scripts/Combat/EnemyData.cs (NEW, ScriptableObject)
- Assets/Scripts/Enemy/EnemyBase.cs (NEW, manifest)
- Assets/Scripts/Enemy/Editor/EnemyBaseBuilder.cs (NEW)
- Assets/Scripts/Enemy/Editor/EnemyBaseValidator.cs (NEW, 41 checks)

Files modified:
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (added `_enemyData`
  field + `InitFromEnemyData` method; MaxHP / DisplayName accessors
  prefer EnemyData when set; Awake reads EnemyData path first)
- Assets/Scripts/Combat/EnemyAI.cs (added `InitFromEnemyData` method)
- Assets/Scripts/Combat/EnemyCombat.cs (added `[RequireComponent(
  CharacterStatsRuntime)]` + `InitFromEnemyData` method)
- Assets/Scripts/Combat/EnemyDeath.cs (added `[RequireComponent(
  EnemyHitReaction)]`)

Files deleted:
- Assets/Scripts/Combat/Editor/ValidateEnemy.cs (interim
  M-MenuCleanup validator; replaced by EnemyBaseValidator)

Files NOT modified (per "Do not modify" list):
- Dummy.prefab (sandbox target, untouched)
- CharacterStats_Dummy.asset (untouched)
- EnemyBaseController.controller (untouched — Grunt shares with Dummy)
- PlayerHero.cs (player-side untouched)
- CLAUDE.md beyond adding this milestone entry

New assets produced by builder on first run:
- Assets/Data/EnemyData/EnemyData_Grunt.asset (M13-EnemyBase defaults)
- Assets/Prefabs/Character Prefabs/Enemy/Enemy_Grunt.prefab

Pending follow-up (Jason runs after CC completes):
1. `LevelGen ▶ Combat ▶ Build Enemy_Grunt Prefab` — creates
   `EnemyData_Grunt.asset` (if missing) and `Enemy_Grunt.prefab` from
   scratch with all components wired.
2. `LevelGen ▶ Combat ▶ Validate Enemy` — expect 41 PASS / 0 FAIL.
   (Note: this is the same menu path the old ValidateEnemy used —
   Unity will pick up the new EnemyBaseValidator after compile.)
3. `LevelGen ▶ Combat ▶ Place Enemy_Grunt in Active Scene` — drops
   a Grunt at world (4, 0, 4).
4. Sanity re-run `LevelGen ▶ Player ▶ Validate Player_Hero` — should
   stay at 50 PASS / 0 FAIL (player-side completely untouched).
5. Play-mode smoke test in test scene (must have baked NavMesh):
   - Grunt detects Player at ≤6m → enters Chase
   - Closes to ≤1.3m → swings (Attack01) on 1.5s cooldown
   - Player takes damage on hit (10/hit), HUD HP bar drops
   - Grunt HP=80 → Player needs ~3 full 3-hit combos to kill it
   - Grunt dies → Die01 plays, despawns after 5s
   - Player can be killed by Grunt swings (HP=100 → 10 hits to die)
   - Player death overlay appears; Restart reloads scene
   - Dummy still works independently (place via M4 menu; combat
     loop unchanged from M4-M11)
6. Inspector verification: select `Enemy_Grunt` in Project; the root
   shows `EnemyBase` component with `_data` slot filled by
   `EnemyData_Grunt` and all 9 component refs (stats, targetable,
   ai, combat, hitReaction, death, animator, agent, capsule) wired.

Deferred / out of scope for M13-EnemyBase:
- Second enemy archetype (Brute, Archer, etc.) — each gets its own
  EnemyData SO + Enemy_*.prefab via a new builder menu (or by
  extending EnemyBaseBuilder with a template parameter).
- Target lock system (separate milestone).
- Enemy health bar UI (player HUD doesn't auto-extend to enemies).
- Loot drops on death.
- Enemy audio / VFX.
- WeaponStats SO (deferred from earlier milestones; would apply to
  PlayerCombat too).
- Damage mitigation via EnemyData.defense (field exists, not yet
  consumed by ApplyDamage — future scope).
- EnemyData.rotationSpeed consumption by EnemyAI (field exists,
  not yet consumed — EnemyAI keeps its `_turnSpeed = 540°/s` default
  for face-during-cooldown / face-during-attack).
- Folding Dummy.prefab onto the EnemyBase manifest. Dummy stays as
  the legacy / sandbox prefab — the rule-of-three threshold for
  consolidation hasn't been crossed yet (currently 1 production
  enemy + 1 sandbox).
- Enemy taking damage from other enemies (friendly-fire guard
  remains hard-coded — M-Factions milestone).

Lessons logged for this milestone:
- Push-down via Awake at `[DefaultExecutionOrder(-50)]` is the right
  pattern for "one component pushes config into siblings". Avoids
  consumer-side coupling to the data source (siblings expose generic
  `InitFromEnemyData(EnemyData)` methods, EnemyBase calls them in
  the right order). The execution order is load-bearing — without
  the `-50` attribute, sibling Awakes at order 0 could run first
  and overwrite the push-down values with their own SerializeField
  defaults.
- ScriptableObject getter-overrides on a MonoBehaviour are clean if
  the data source is a separate field. `MaxHP => _enemyData != null
  ? _enemyData.maxHP : (stats != null ? stats.maxHP : 0)` lets one
  runtime instance support two data paths (CharacterStats for
  player, EnemyData for enemy) without forking the script. Adding
  a third path (e.g. WeaponStats overriding attackDamage) would
  follow the same template.
- Consolidated validators are the canonical evolution after 5-10
  per-milestone validators accumulate. M-MenuCleanup's ValidateEnemy
  (32 checks) consolidated 6 enemy validators; M13-EnemyBase's
  EnemyBaseValidator (41 checks) extends it by 9 more. The pattern
  is additive — older check numbers stay stable across consolidations
  so log diffs remain readable.

## M14 — Enemy health bar + Defense wired (2026-05-11)

Two-part combat polish milestone. Wires `EnemyData.Defense` through
the damage pipeline as a flat reduction, and ships two scripts for a
world-space health-bar billboard above each enemy. Scripts only —
prefab wiring (Canvas / Image / component placement on Enemy_Grunt)
is deferred to the editor pass.

### Defense wiring (gameplay)

- `EnemyData.cs` — `defense` converted from public field to
  `[SerializeField] private float defense = 5f` + public read-only
  `Defense` property. Field name unchanged so `EnemyData_Grunt.asset`'s
  serialized value (defense=2 per M13-EnemyBase) is preserved.
  OnValidate clamp `if (defense < 0f) defense = 0f` retained.
- `CharacterStatsRuntime.cs` — new `Defense` auto-property (public
  getter, private setter) and `SetDefense(float)` public mutator,
  placed alongside `IsInvulnerable` / `SetInvulnerable` (same
  write-once-from-outside pattern). `ApplyDamage(int amount)` gains a
  flat-reduction step AFTER the IsInvulnerable guard, BEFORE the HP
  delta:
  ```
  int effectiveDamage = Mathf.Max(0, amount - Mathf.RoundToInt(Defense));
  if (effectiveDamage <= 0) return;
  ```
  HP arithmetic stays int; Defense (float) rounds to int via
  `Mathf.RoundToInt`. Debug.Log appends `(after Defense N.NN)` when
  Defense > 0 so log diffs make the reduction visible.
- `EnemyBase.cs` (Enemy manifest) — `Awake` now calls
  `_stats.SetDefense(_data.Defense)` immediately after the existing
  `_stats.InitFromEnemyData(_data)` push-down. `[DefaultExecutionOrder(
  -50)]` ordering preserved — Defense lands before any sibling Awake
  could read it.

Player keeps Defense=0 (no push-down on the player path). Future
WeaponStats / ArmorStats SOs can call `SetDefense` on the player's
CharacterStatsRuntime when the equipment system ships.

### Enemy health bar (UI scripts)

Two new scripts in `LevelGen` namespace (no sub-namespace, per the
specified template):

- `Assets/Scripts/Combat/EnemyHealthBar.cs` (NEW, ~125 lines):
  World-space bar billboard. Inspector wires `_fillImage` (a Filled
  Image) and `_barYOffset` (default 2.2). Awake resolves `_stats` via
  `GetComponentInParent<CharacterStatsRuntime>` if not wired; disables
  self with a warning if still null. Update lazy-caches `Camera.main`
  (retries each null frame so a late-spawning CinemachineAutoBind rig
  is picked up automatically). Billboard via
  `transform.forward = -cam.transform.forward` (avoids LookAt mirror-
  flip, cheaper than Quaternion.LookRotation). Fill update via
  `Mathf.Clamp01((float)stats.CurrentHP / stats.MaxHP)` with
  `MaxHP > 0` guard. OnEnable/OnDisable subscribe/unsubscribe to
  `CharacterStatsRuntime.OnDied` and hide on death (so corpses that
  linger 5s before despawn don't show 0/MaxHP bars). Public
  `SetVisible(bool)` — external systems (proximity driver, future
  target lock) drive visibility; component never polls distance.
- `Assets/Scripts/Combat/EnemyHealthBarProximityDriver.cs` (NEW, ~97
  lines): InvokeRepeating-based distance check. Inspector fields
  `_healthBar`, `_showRadius=12`, `_pollInterval=0.2`, `_playerTag=
  "Player"`. Start does one-shot `GameObject.FindWithTag` lookup; if
  player or `_healthBar` is null, logs warning and self-disables (no
  silent retry — missing player is a real config bug worth surfacing).
  `CheckProximity` reads `Vector3.Distance(transform.position,
  _player.position)` and calls `_healthBar.SetVisible(dist <=
  _showRadius)`. `OnDestroy` cancels the invoke.

### Validator extension

- `Assets/Scripts/Enemy/Editor/EnemyBaseValidator.cs` — appended
  checks 41-44 (continuing the consolidated 40-check sequence from
  M13-EnemyBase):
    41 `EnemyData_Grunt.Defense >= 0`
    42 `CharacterStatsRuntime.Defense` public getter (float)
    43 `CharacterStatsRuntime.SetDefense(float)` public void method
    44a `EnemyHealthBar` component in Enemy_Grunt hierarchy
        (via `Type.GetType("LevelGen.EnemyHealthBar, Assembly-CSharp")`
        + `GetComponentInChildren`)
    44b `EnemyHealthBarProximityDriver` component in Enemy_Grunt
        hierarchy
  Checks 44a/44b use deferred Type.GetType lookup so the validator
  compiles even before the scripts are added to the prefab. FAIL
  messages distinguish "Type not found" (script missing) from
  "Component missing from prefab — wire it in the Editor".

### Files

- Assets/Scripts/Combat/EnemyData.cs (modified — `defense` field
  converted to private SerializeField + Defense property)
- Assets/Scripts/Combat/CharacterStatsRuntime.cs (modified —
  Defense property + SetDefense + ApplyDamage reduction step)
- Assets/Scripts/Enemy/EnemyBase.cs (modified — SetDefense push-down
  in Awake)
- Assets/Scripts/Combat/EnemyHealthBar.cs (NEW)
- Assets/Scripts/Combat/EnemyHealthBarProximityDriver.cs (NEW)
- Assets/Scripts/Enemy/Editor/EnemyBaseValidator.cs (extended,
  41-44 added)

### Current state

- Defense flat-reduction wired end-to-end. EnemyBase.Awake pushes
  Defense from EnemyData_Grunt (=2) into CharacterStatsRuntime; M11's
  10-damage-per-swing now lands as 8/swing on Grunt (10 − 2). Player
  side untouched (Defense=0).
- EnemyHealthBar + EnemyHealthBarProximityDriver scripts compile-
  ready. Enemy_Grunt.prefab does NOT yet carry these components —
  editor wiring deferred (add a child HealthBar GameObject with a
  World Space Canvas → Image, attach `EnemyHealthBar` and a child
  `EnemyHealthBarProximityDriver`, wire references in Inspector,
  save prefab). Validator checks 44a/44b will report "Component
  missing from prefab — wire it in the Editor" until that happens.
- No prefab, scene, asset, .asmdef, or ProjectSettings changes this
  milestone.

### Pending follow-up (Jason runs after CC completes)

1. Editor wiring of EnemyHealthBar onto Enemy_Grunt.prefab:
   - Add child GameObject `HealthBar` under Enemy_Grunt root
   - Add child `Canvas` (Render Mode = World Space) under HealthBar
   - Add child `Background` Image (dark) + child `Fill` Image
     (Image Type = Filled, Fill Method = Horizontal, Origin = Left,
     red tint) under the Canvas
   - On `HealthBar` GameObject, add `EnemyHealthBar` component;
     wire `_fillImage` → Fill, set `_barYOffset` ≈ 2.2
   - On `HealthBar` GameObject (or root), add
     `EnemyHealthBarProximityDriver`; wire `_healthBar` → the
     EnemyHealthBar component on the same GameObject
   - Save prefab
2. `LevelGen ▶ Combat ▶ Validate Enemy` — expect 44/44 PASS.
3. Sanity re-run `LevelGen ▶ Player ▶ Validate Player_Hero` — should
   stay at 63/63 (no player-side changes).
4. Play-mode smoke test:
   - Walk within 12m of a Grunt → health bar appears
   - Bar billboards to camera as player orbits
   - Hit Grunt with Attack01 → fill drops from 100% to 90% (8/80
     damage = 10%, not 10/80 = 12.5%) — confirms Defense applied
   - Walk away beyond 12m → bar hides
   - Kill Grunt → bar hides on death; corpse persists 5s without
     showing 0/80
5. Defense tuning: open `EnemyData_Grunt.asset`, adjust `defense`
   field as desired. EnemyBase pushes the new value at runtime.

### Deferred

- Target Lock system (still recommended next milestone per
  Session_Handoff)
- WeaponStats SO (replaces hardcoded `attackDamage = 10` on
  PlayerCombat; also unlocks Defense push-down for player armor)
- Damage-type system (currently flat reduction; future:
  fire/ice/poison resistances)
- Crit / status effects layered on top of Defense
- Per-archetype health bar art (size, color, sub-bars for shields
  or stagger meter)
- World-bundle billboard mesh (replace UGUI World Space Canvas with
  a Mesh-based bar for mobile perf if profiling shows Canvas cost)
- M-Factions: enemy-vs-enemy damage (currently blocked by
  EnemyCombat friendly-fire guard)

### Lessons logged

- The `defense` field was already public on EnemyData — converting to
  `[SerializeField] private` with the same name preserves serialized
  asset values. Pattern carries forward whenever an existing public
  field needs encapsulation: keep the field name, add SerializeField,
  add a `PublicName => fieldName` property.
- `Mathf.RoundToInt` is the right bridge when an int pipeline
  consumes a float stat (HP arithmetic is integer; Defense is float
  for future ScriptableObject tuning ergonomics). Rounding-to-nearest
  matches damage-feel expectations better than truncation
  (Mathf.FloorToInt would silently absorb 0.49 damage).
- Health-bar visibility belongs to a separate driver component, not
  the bar itself. Lets future systems (Target Lock force-show,
  scripted cinematic hide) call the same `SetVisible` without
  rewiring the proximity check. Proximity is one driver among many.
- One-shot player lookup in Start with self-disable on miss is the
  right pattern for the enemy health bar (vs PlayerHUD's coroutine
  retry). Enemies spawn AFTER the player; a missing player at enemy-
  Start is a config bug, not a timing issue. PlayerHUD's retry exists
  because PlayerHUD spawns at scene load before the player prefab is
  instantiated in some flows — different lifecycle.
- Validator type-by-name lookup (`Type.GetType("LevelGen.X,
  Assembly-CSharp")`) is the canonical pattern when validating types
  that may not exist yet at compile time. Lets the validator land
  alongside the script-writing work without ordering constraints.

### M14 hotfix (2026-05-11)

Recent changes:
- Fixed EnemyBaseBuilder line 276: `.defense` → `.Defense` (field made
  private in M14).
- EnemyData.Defense made get/set (was get-only); fixes CS0200 in
  EnemyBaseBuilder. Clamp matches OnValidate convention.
- DamageNumberSpawner: added _spawnYOffset (default 1.5f) to lift
  numbers above collider contact height. Resolves deferred per-actor
  Y-offset issue.

The M14 conversion of `defense` to a `[SerializeField] private` field
silently broke `EnemyBaseBuilder.EnsureGruntData`, which set the field
directly via `asset.defense = 2f;` (CS0122). Pivoting to `asset.Defense
= 2f;` then surfaced CS0200 because `Defense` was expression-bodied
get-only. Promoted `Defense` to a full property with a clamped setter:
`public float Defense { get => defense; set => defense = Mathf.Max(0f,
value); }`. The clamp mirrors the OnValidate convention (`if (defense
< 0f) defense = 0f`) so authoring paths can't poison the asset with a
negative value. Consumption surface unchanged — `EnemyBase.Awake`
still reads `_data.Defense` as before.

Lesson: when encapsulating a public field, audit ALL writers (not just
readers) before shipping. A read-only property protects from accidental
runtime mutation but breaks authoring paths that legitimately need to
write — and ScriptableObject builders are exactly that. Default to
`{ get; set; }` for SO stat fields; lean on OnValidate clamps rather
than property-side guards.

## M11.1 — post-hit i-frame window on enemy hit (2026-05-11)

Follow-up to M11 (player takes damage). M11 proper shipped the full
damage-to-player routing: `EnemyAnimationEventAbsorber` was deleted,
`EnemyAnimationEventForwarder` and `EnemyHitboxRelay` were created,
and `EnemyCombat.NotifyHitboxTriggered` applies damage. This follow-up
adds the one missing piece: a brief i-frame window on the player after
each hit, preventing rapid-swing stack-damage.

**Changes:**

- `EnemyCombat.cs`: added `_iFrameDuration = 0.5f` SerializeField
  (tunable per-enemy in Inspector). Added `GrantIFrames(CharacterStatsRuntime)`
  private coroutine that calls `stats.SetInvulnerable(true)`, yields
  `WaitForSeconds(_iFrameDuration)`, then calls `SetInvulnerable(false)`
  (skipped if the target died during the window). Coroutine is started in
  `NotifyHitboxTriggered` immediately after `ApplyDamage`, guarded on
  `!stats.IsDead && _iFrameDuration > 0f`. Added `using System.Collections;`
  import required by the coroutine return type.

- `EnemyBaseValidator.cs`: appended checks 45-47 (M11.1 additions).
  - 45: `EnemyCombat._iFrameDuration` SerializeField present (float,
    NonPublic reflection).
  - 46: `EnemyAnimationEventAbsorber.cs` not found at either known path
    (`Assets/Scripts/Combat/` or `Assets/Scripts/Enemy/`) — confirms
    M11 deletion persists.
  - 47a/47b: `EnemyCombat.OnHitboxOpen()` and `OnHitboxClose()` public
    void methods present (already true from M11; this makes the state
    explicit in the validator).

**Already done in M11 (not re-done here):**

- `EnemyAnimationEventAbsorber.cs` was deleted in M11. Confirmed absent
  at both search paths.
- `EnemyCombat.OnHitboxOpen` / `OnHitboxClose` already implemented in
  M11 (enable/disable hitbox + clear hit list). Not modified.
- `HashSet<Targetable> _currentAttackHitList` already exists in M11.
  The prompt's `List<Collider>` alternative was not introduced — the
  existing typed HashSet is correct and consumed by surrounding code.
- `OnTriggerEnter` was NOT added to `EnemyCombat` — the `EnemyHitboxRelay`
  pattern (relay on the child BoxCollider that routes to
  `NotifyHitboxTriggered` on root) is the correct M11 architecture.

**Current state:** enemy attack damage deals to player via
`EnemyCombat.NotifyHitboxTriggered`; player i-frames wired via
`CharacterStatsRuntime.SetInvulnerable` coroutine on each hit;
`EnemyAnimationEventAbsorber` deleted in M11 and confirmed absent.

**Files modified:**

- `Assets/Scripts/Combat/EnemyCombat.cs` — `_iFrameDuration` field,
  `GrantIFrames` coroutine, coroutine start in `NotifyHitboxTriggered`,
  `using System.Collections` import.
- `Assets/Scripts/Enemy/Editor/EnemyBaseValidator.cs` — checks 45-47
  appended.

**Files NOT modified:** `CharacterStatsRuntime.cs` (SetInvulnerable /
IsInvulnerable already shipped in M12), `EnemyHitboxRelay.cs`,
`EnemyAnimationEventForwarder.cs`, `EnemyAI.cs`, `EnemyBase.cs`,
`EnemyDeath.cs`, `EnemyHitReaction.cs`, any prefab, any .unity scene.

**Pending follow-up:**

- `LevelGen ▶ Combat ▶ Validate Enemy` — expect 49 PASS / 0 FAIL
  (44 prior checks + checks 45, 46, 47a, 47b).
- Play-mode smoke test: have Grunt swing at player twice in quick
  succession (within 0.5s). Only the first hit registers on the HP bar;
  the second is absorbed by i-frames. After 0.5s the bar is vulnerable
  again.

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

## Studio Agents (CCGS)

Installed 2026-05-11 from
https://github.com/Donchitos/Claude-Code-Game-Studios.

Provides **39 specialist subagents** (Unity stack — Godot and Unreal
variants stripped during install), **72 skills**, **12 hooks** (inert
until wired in settings.json), and **11 rule docs**.

### Layout
- `.claude/agents/` — 39 specialist subagents (e.g. `unity-specialist`,
  `gameplay-programmer`, `qa-lead`, `level-designer`).
- `.claude/skills/` — 72 workflow skills (e.g. `code-review`,
  `design-review`, `playtest-report`, `gate-check`).
- `.claude/hooks/` — 12 shell-script hooks. **Not active.** None are
  referenced from `.claude/settings.json` yet.
- `.claude/rules/` — 11 coding/design rule docs (`engine-code.md`,
  `gameplay-code.md`, `ui-code.md`, etc.).
- `Documentation/CCGS/` — CCGS framework documentation:
  `WORKFLOW-GUIDE.md`, `COLLABORATIVE-DESIGN-PRINCIPLE.md`,
  design templates, session examples, `workflow-catalog.yaml`,
  Unity engine reference.

### Collaboration Protocol (CCGS convention)
**User-driven collaboration, not autonomous execution.**
Every task follows: **Question → Options → Decision → Draft → Approval**

- Subagents MUST ask "May I write this to [filepath]?" before using
  Write/Edit tools.
- Subagents MUST show drafts or summaries before requesting approval.
- Multi-file changes require explicit approval for the full changeset.
- No commits without user instruction.

Full protocol and examples: `Documentation/CCGS/COLLABORATIVE-DESIGN-PRINCIPLE.md`.

> **First CCGS session?** Run `/start` for the guided onboarding flow
> (CCGS skill, lives under `.claude/skills/start/`).

### Notes for this project
- Engine context (Unity 6.4 URP, IL2CPP, ARM64) is already declared
  at the top of this file — the CCGS engine-stack placeholders are
  not used.
- `.claude/settings.json` was NOT merged from CCGS — your existing
  settings remain canonical. Hooks are inert until you reference them
  explicitly from settings.
- The Godot and Unreal specialist agents and engine references were
  deleted at install time; only the Unity stack is present.
