# Menu Audit — V2 RoomWorkshop

Date: 2026-04-25
Scope: Assets/Scripts/ (excluding _Archive/)

---

## LevelEditor

All items live under the `Tests` submenu.
Source files: `Assets/Scripts/LevelEditor/Editor/`

| Path | File:Line | What it does | Status | Rec |
|------|-----------|--------------|--------|-----|
| `LevelEditor/Tests/Doorway: Combined paths on 5x3` | `Doorway_Test.cs:75` | Unions manual East + doorCount N/S = 3 gaps; verifies duplicate stamp is idempotent | DIAGNOSTIC | KEEP |
| `LevelEditor/Tests/Dump EdgeSolver Results` | `EdgeSolver_Test.cs:20` | Runs EdgeSolver on Rectangle(5,3), logs summary + first floor/wall/corner entry | DIAGNOSTIC | KEEP |
| `LevelEditor/Tests/Create Gizmo Preview in Scene` | `EdgeSolver_Test.cs:55` | Creates (or reselects) EdgeSolverGizmoPreview GO at world origin | DIAGNOSTIC | KEEP |
| `LevelEditor/Tests/Create RoomBuilder in Scene` | `EdgeSolver_Test.cs:80` | Creates (or reselects) RoomBuilder GO at world origin for inspector-based workflow | ACTIVE | KEEP |
| `LevelEditor/Tests/RoomPiece: 5x3 with 3 Doorways` | `RoomPiece_Test.cs:14` | Builds 5×3+3 doorways, asserts MOD_Room architecture (RoomPiece on child, ExitPoints on child, bounds, forward vectors) | DIAGNOSTIC | KEEP |
| `LevelEditor/Tests/Save: MOD_Room Save+Clear Roundtrip` | `RoomPiece_Test.cs:89` | Builds 5×3, saves MOD_Room as temp prefab, verifies child count + RoomPiece, then Clears and checks empty | DIAGNOSTIC | KEEP |
| `LevelEditor/Tests/Dump Shape Stamps to Console` | `ShapeStamp_Test.cs:14` | Dumps ASCII art of Rectangle(5,3), Diamond(5), Circle(3) to console | DIAGNOSTIC | KEEP |

**Notes:**
- Doorway tests consolidated to a single item. The Combined test covers manual stamp, doorCount-equivalent, union, and idempotency in one pass.
- No LEGACY items. No hotkeys. No duplicate paths.

---

## LevelGen

Source files: `Assets/Scripts/Editor/`, `Assets/Scripts/LevelGen/V2/Editor/`

| Path | File:Line | What it does | Status | Rec |
|------|-----------|--------------|--------|-----|
| `LevelGen/LVL Configurator` | `LVL_Configurator.cs:46` | Opens LVL Configurator EditorWindow — stamps RoomPiece + ExitPoints onto LVL_ prefabs by parsing their names | ACTIVE | KEEP |
| `LevelGen/V2 Level Generator` | `V2LevelGeneratorWindow.cs:15` | Opens V2 Level Generator EditorWindow (Phase A: params + validation, no generation) | ACTIVE | KEEP |
| `LevelGen/Whitebox [Complete]/Generate (Step 1: mirror meshes)` | `WhiteboxPackFactory.cs:75` | Walks FDP 3d/modular/, extracts mesh sub-assets from FBXs, saves as standalone .asset files | ACTIVE | KEEP |
| `LevelGen/Whitebox [Complete]/Generate (Step 2: wrap meshes in prefabs)` | `WhiteboxPackFactory.cs:220` | Creates one prefab per mesh with MeshFilter + MeshRenderer + tinted URP/Lit material per category | ACTIVE | KEEP |
| `LevelGen/Whitebox [Complete]/Generate (Step 3: mirror comps)` | `WhiteboxPackFactory.cs:517` | Mirrors FDP 02_COMPS/ — replaces each FDP part instance with its whitebox equivalent | ACTIVE | KEEP |
| `LevelGen/Whitebox [Complete]/Generate (Step 3: dry run)` | `WhiteboxPackFactory.cs:524` | Same as Step 3 but read-only — logs every mapping decision without writing assets | DIAGNOSTIC | KEEP |
| `LevelGen/Whitebox [Complete]/Diagnose Step 3 (log only, no output)` | `WhiteboxPackFactory.cs:780` | Loads one FDP comp prefab and logs full PrefabUtility state per child for debugging | DIAGNOSTIC | KEEP |
| `LevelGen/Whitebox [Complete]/Generate (Step 4: mirror LVL modules)` | `WhiteboxPackFactory.cs:1023` | Two-pass mirror of FDP 03_LEVEL_MODULES/ — pass 1 builds with placeholders, pass 2 resolves cross-LVL refs | ACTIVE | KEEP |
| `LevelGen/Whitebox [Complete]/Generate (Step 4: dry run)` | `WhiteboxPackFactory.cs:1030` | Same as Step 4 but read-only — logs mapping breakdown before committing | DIAGNOSTIC | KEEP |

**Notes:**
- LVL_Configurator is marked "complete — do not touch" in CLAUDE.md. KEEP unconditionally.
- Whitebox submenu renamed to `[Complete]` to signal these steps are done. Retained for the event FDP pack updates or whitebox needs a full reset.
- `Diagnose Step 3` is explicitly retained per CLAUDE.md: "Kept in the file for future debugging."
- No hotkeys. No duplicate paths.

---

## Tools

Source files: `Assets/Scripts/LevelEditor/Editor/`

| Path | File:Line | What it does | Status | Rec |
|------|-----------|--------------|--------|-----|
| `Tools/V2 Tests/Theme: Build with Selected Theme` | `V2_SampleThemeBuilder.cs:17` | Finds scene RoomBuilder, logs ResolvePrefab() per piece type, then calls Build() twice (with theme / without) to validate theme + direct-slot fallback | ACTIVE | KEEP |
| `Tools/V2 Tests/Save: Categorized RoomPiece Roundtrip` | `V2_SampleThemeBuilder.cs:75` | Tests ResolveSaveFolder() + EnsureFolderExists() for Room/Starter and Hall/Special — folder structure only, no prefab saved | DIAGNOSTIC | KEEP |

**Notes:**
- `Theme: Build with Selected Theme` mutates the scene (calls Build() twice). Active validation tool for the theme resolution path.
- `Save: Categorized Roundtrip` is intentionally folder-structure-only per its implementation ("No prefab saved — folder structure only").
- No hotkeys. No duplicate paths.

---

## Summary

| Status | Count |
|--------|-------|
| ACTIVE | 8 |
| DIAGNOSTIC | 11 |
| LEGACY | 0 |
| UNCLEAR | 0 |
| **Total** | **19** |

---

## Top recommended removals

None. Every item is live, compiles cleanly, and references types that exist in the current codebase. No stale callers, no commented-out bodies, no references to `_Archive/` types.

---

## Items needing JC input

**1. `Create Gizmo Preview in Scene` — still needed?**

`LevelEditor/Tests/Create Gizmo Preview in Scene` (`EdgeSolver_Test.cs:55`)

Decision: KEEP. JC confirmed this remains useful for solver debugging.
