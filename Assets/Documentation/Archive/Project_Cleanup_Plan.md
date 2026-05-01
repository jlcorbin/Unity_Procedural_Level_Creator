# Project Cleanup Plan

**Date:** 2026-04-30
**Scope:** Folder reorg + dead-code audit prior to M2-D / enemy work.

This is a read-only diagnostic report. No project state has been
modified. The user approves each subsequent phase explicitly.

---

## Phase ①a — `Assets/Prefabs/` current state

| Subfolder | Prefab count (recursive) | Notable contents |
|---|---|---|
| `Rooms/` | 10 | Starter (1), Boss (1), Small (2), Medium (1), Large (1), Curated (4) |
| `Halls/` | 5 | Small (1), Medium (2), no Large/Special yet |
| `Player/` | 1 | `Player_MaleHero.prefab` |

All three have proper sibling `.meta` files. No loose prefabs at
`Assets/Prefabs/` root.

---

## Phase ①b — String-path references

### `"Assets/Prefabs/Rooms"` and `"Assets/Prefabs/Halls"` (live code)

| File:Line | Form | Notes |
|---|---|---|
| [LVL_Configurator.cs:34-35](Assets/Scripts/Editor/LVL_Configurator.cs#L34-L35) | `private const string RoomsFolder = "Assets/Prefabs/Rooms"` + `HallsFolder` | **⚠ Marked "do not touch" in CLAUDE.md.** Cannot be left stale either — see Risk #1. |
| [RoomBuilder.cs:849-850](Assets/Scripts/LevelEditor/RoomBuilder.cs#L849-L850) | `$"Assets/Prefabs/{typeFolder}/{ResolveCategoryName()}"` | Single interpolated literal builds Rooms/Halls paths |
| [V2PrefabSource.cs:17,21](Assets/Scripts/LevelGen/V2/Editor/V2PrefabSource.cs#L17) | `$"Assets/Prefabs/Rooms/{category}"` + `$"Assets/Prefabs/Halls/{size}"` | V2 generator's prefab discovery |
| [V2PrefabSource.cs:10,15,19](Assets/Scripts/LevelGen/V2/Editor/V2PrefabSource.cs#L10) | doc comments mentioning paths | XML doc comments |
| [V2LevelGenerator.cs:199-213](Assets/Scripts/LevelGen/V2/Editor/V2LevelGenerator.cs#L199-L213) | 8 error-message string literals | All `"No prefabs in Assets/Prefabs/Rooms/X/..."` style |
| [RoomPiece_Test.cs:92-93](Assets/Scripts/LevelEditor/Editor/RoomPiece_Test.cs#L92-L93) | `"Assets/Prefabs/Rooms/Small"` + `__test_save_roundtrip.prefab` | Editor test; hardcoded folder |
| [V2_SampleThemeBuilder.cs:90,103](Assets/Scripts/LevelEditor/Editor/V2_SampleThemeBuilder.cs#L90) | `expectedRoom = "Assets/Prefabs/Rooms/Starter"` + `expectedHall = "Assets/Prefabs/Halls/Special"` | Verifies `RoomBuilder.ResolveSaveFolder()` output |

### `"Assets/Prefabs/Player"` (live code)

| File:Line | Form |
|---|---|
| [PlayerPrefabBuilder.cs:44](Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs#L44) | `private const string PrefabPath = "Assets/Prefabs/Player/Player_MaleHero.prefab"` |
| [PlayerPrefabBuilder.cs:98](Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs#L98) | `EnsureFolder("Assets/Prefabs", "Player")` |
| [M4_SampleSceneSetup.cs:36](Assets/Scripts/Player/Editor/M4_SampleSceneSetup.cs#L36) | `private const string PrefabPath = "Assets/Prefabs/Player/Player_MaleHero.prefab"` |
| [PlayerJumpRuntimeValidator.cs:30](Assets/Scripts/Player/Editor/PlayerJumpRuntimeValidator.cs#L30) | same |
| [M3_03B_DuoReimportVerifier.cs:30](Assets/Scripts/Player/Editor/M3_03B_DuoReimportVerifier.cs#L30) | same |
| [PlayerCombatValidator.cs:29](Assets/Scripts/Player/Editor/PlayerCombatValidator.cs#L29) | same |
| [PlayerCombatPrefabAdder.cs:18](Assets/Scripts/Player/Editor/PlayerCombatPrefabAdder.cs#L18) | same |

### `"Assets/Levels"`

| File:Line | Form |
|---|---|
| [LevelGenSettings.cs:14](Assets/Scripts/LevelGen/V2/LevelGenSettings.cs#L14) | `outputFolder = "Assets/Levels/Generated"` (default value, `[NonSerialized]`) |
| [V2LevelGeneratorWindow.cs:274](Assets/Scripts/LevelGen/V2/Editor/V2LevelGeneratorWindow.cs#L274) | `const string DefaultFolder = "Assets/Levels/Generated"` |

### `"Assets/LevelSequences"` and `"Assets/RoomPresets"` (live code)

**Zero hits.** No live code references either folder.

### V1 symbol references (excluding self-references in archive)

Searched for `RoomPreset`, `RoomPresetLibrary`, `PropCatalogue`, `PropEntry`, `RoomDefinition`, `RoomContentGenerator`, `LevelSequence`, `SeedData`, `BoundsChecker`, `LevelGenSetup`, `PlaceholderPrefabFactory`, `LevelGeneratorEditor`, `RoomWorkshopWindow`, `SpawnPoint`.

**Zero matches** in any live `.cs` outside of:
- [PlayerSpawnPoint.cs:20](Assets/Scripts/LevelGen/PlayerSpawnPoint.cs#L20) — comment only ("Color matches the convention used by SpawnPoint.cs"). The comment is now slightly stale (V1 SpawnPoint is gone), but it's harmless.

---

## Phase ①c — Folder-existence audit

| Path | Exists? | Contents | Status |
|---|---|---|---|
| `Assets/RoomPresets/` | **YES** | 8 `.asset` + 8 `.meta` (Room_New, Room_New_2, Room_New_med, Room_New_Ran, Hall_New, Hall_New_2, Hall_New_Double, DefaultRoomPresetLibrary) | All assets reference V1 `RoomPreset` / `RoomPresetLibrary` types that no longer exist in live code → **orphaned, safe to delete** |
| `Assets/Levels/` | **NO** | — | Already gone. Phase ④ becomes a string-update only. |
| `Assets/LevelSequences/` | **NO** | — | Already gone. Phase ⑤b is a no-op. |
| `Assets/Scripts/_Archive/` | **NO** | — | Already gone. The V1 cleanup audit's "Item D-2" decision was apparently to delete (rather than keep). |

---

## Phase ①d — Live V1 class definitions

Searched for `class RoomPreset`, `class RoomPresetLibrary`, `class PropCatalogue`, `class PropEntry`, `class RoomDefinition`, `class RoomContentGenerator`, `class LevelSequence`, `class SeedData`, `class BoundsChecker`, `class LevelGenSetup`, `class PlaceholderPrefabFactory`, `class LevelGeneratorEditor`, `class RoomWorkshopWindow`, `class SpawnPoint` across all live `.cs`.

**Result: ZERO V1 class definitions in live code.** Per CLAUDE.md V1 cleanup audit (2026-04-26), these were all retired. Re-verification confirms the live tree is still clean.

**Phase ⑥ is therefore a no-op** — there's nothing to delete.

---

## Phase ①e — Plan summary

### Phase ② — Folder moves (3 calls)

| MoveAsset call | Reference impact |
|---|---|
| `Assets/Prefabs/Rooms` → `Assets/Prefabs/Level Prefabs/Rooms` | All Rooms-path strings need Phase ③ remediation |
| `Assets/Prefabs/Halls` → `Assets/Prefabs/Level Prefabs/Halls` | All Halls-path strings need Phase ③ remediation |
| `Assets/Prefabs/Player` → `Assets/Prefabs/Character Prefabs/Player` | All Player-path strings need Phase ③ remediation |

Plus 2 `CreateFolder` calls for the umbrellas.

GUID-based scene/prefab references survive `MoveAsset` automatically. Only string literals in code need updates.

### Phase ③ — String fixes (per file)

**Critical-path files (these must update or runtime breaks):**

1. **[LVL_Configurator.cs:34-35](Assets/Scripts/Editor/LVL_Configurator.cs#L34-L35)**
   - `"Assets/Prefabs/Rooms"` → `"Assets/Prefabs/Level Prefabs/Rooms"`
   - `"Assets/Prefabs/Halls"` → `"Assets/Prefabs/Level Prefabs/Halls"`
   - **⚠ See Risk #1 below — this file is marked "do not touch" in CLAUDE.md.**

2. **[RoomBuilder.cs:849-850](Assets/Scripts/LevelEditor/RoomBuilder.cs#L849-L850)**
   - `$"Assets/Prefabs/{typeFolder}/{ResolveCategoryName()}"` → `$"Assets/Prefabs/Level Prefabs/{typeFolder}/{ResolveCategoryName()}"`
   - Plus the doc comment example at line 845.

3. **[V2PrefabSource.cs:17,21](Assets/Scripts/LevelGen/V2/Editor/V2PrefabSource.cs#L17)**
   - `$"Assets/Prefabs/Rooms/{category}"` → `$"Assets/Prefabs/Level Prefabs/Rooms/{category}"`
   - `$"Assets/Prefabs/Halls/{size}"` → `$"Assets/Prefabs/Level Prefabs/Halls/{size}"`
   - Plus 3 doc comments (lines 10, 15, 19).

4. **[V2LevelGenerator.cs:199-213](Assets/Scripts/LevelGen/V2/Editor/V2LevelGenerator.cs#L199-L213)** — 8 error-message strings
   - All `Assets/Prefabs/Rooms/X` → `Assets/Prefabs/Level Prefabs/Rooms/X`
   - All `Assets/Prefabs/Halls/Y` → `Assets/Prefabs/Level Prefabs/Halls/Y`
   - These are diagnostic strings; if missed, runtime works but errors look stale.

5. **[RoomPiece_Test.cs:92-93](Assets/Scripts/LevelEditor/Editor/RoomPiece_Test.cs#L92-L93)** — test fixture path
   - `"Assets/Prefabs/Rooms/Small"` → `"Assets/Prefabs/Level Prefabs/Rooms/Small"` (×2 lines)

6. **[V2_SampleThemeBuilder.cs:90,103](Assets/Scripts/LevelEditor/Editor/V2_SampleThemeBuilder.cs#L90)** — test expectations
   - `"Assets/Prefabs/Rooms/Starter"` → `"Assets/Prefabs/Level Prefabs/Rooms/Starter"`
   - `"Assets/Prefabs/Halls/Special"` → `"Assets/Prefabs/Level Prefabs/Halls/Special"`

7. **Player prefab path — 6 files:**
   - [PlayerPrefabBuilder.cs:44,98](Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs#L44) — `PrefabPath` const + `EnsureFolder("Assets/Prefabs", "Player")` call (the latter needs full restructuring: → `EnsureFolder("Assets/Prefabs/Character Prefabs", "Player")`)
   - [M4_SampleSceneSetup.cs:36](Assets/Scripts/Player/Editor/M4_SampleSceneSetup.cs#L36)
   - [PlayerJumpRuntimeValidator.cs:30](Assets/Scripts/Player/Editor/PlayerJumpRuntimeValidator.cs#L30)
   - [M3_03B_DuoReimportVerifier.cs:30](Assets/Scripts/Player/Editor/M3_03B_DuoReimportVerifier.cs#L30)
   - [PlayerCombatValidator.cs:29](Assets/Scripts/Player/Editor/PlayerCombatValidator.cs#L29)
   - [PlayerCombatPrefabAdder.cs:18](Assets/Scripts/Player/Editor/PlayerCombatPrefabAdder.cs#L18)
   - All: `"Assets/Prefabs/Player/Player_MaleHero.prefab"` → `"Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab"`

### Phase ④ — Levels move (string-only)

`Assets/Levels/` doesn't exist on disk → **no `MoveAsset` call needed**. Just update the two string defaults so the V2 generator's first run will create the folder under the new location:

- [LevelGenSettings.cs:14](Assets/Scripts/LevelGen/V2/LevelGenSettings.cs#L14): `"Assets/Levels/Generated"` → `"Assets/Scenes/Levels/Generated"`
- [V2LevelGeneratorWindow.cs:274](Assets/Scripts/LevelGen/V2/Editor/V2LevelGeneratorWindow.cs#L274): same

### Phase ⑤ — Folder deletions

| Folder | Action | Reason |
|---|---|---|
| `Assets/RoomPresets/` | **DELETE** (8 .asset + 8 .meta) | Orphaned V1 scriptable objects; their `RoomPreset` / `RoomPresetLibrary` types are gone from live code |
| `Assets/LevelSequences/` | **NO-OP** | Already gone |
| `Assets/Levels/` | **NO-OP** | Already gone |

### Phase ⑥ — Script deletions

**NO-OP.** No live V1 class definitions found. The V1 cleanup audit from 2026-04-26 still holds.

### Phase ⑦ — Verification

Standard end-of-prompt checks: compile clean, all original literals replaced, new tree matches expected layout, SampleScene loads without missing-reference warnings.

---

## Risk callouts

**Risk #1 — `LVL_Configurator.cs` is marked do-not-touch in CLAUDE.md.**

CLAUDE.md says: *"Do not touch LVL_Configurator (it is complete)."*

The cleanup either:
- (a) Updates the two `const string` values inside it (the two-line minimum change). This is functionally trivial and necessary for the configurator to keep working after Phase ②.
- (b) Skips Phase ② entirely (the only Configurator-affecting move is Rooms/Halls → Level Prefabs/).
- (c) Adds a thin compatibility shim somewhere (overkill).

**Recommendation: (a).** The "do not touch" rule is intent ("don't redesign or refactor"), not literal ("never edit at all"). Two const-string updates preserve all logic and merely match the new folder layout. CLAUDE.md was written before the reorg was planned. After approval, this two-line update is the cleanest path.

If the user wants strict adherence to the do-not-touch rule, the alternative is dropping Rooms/Halls from Phase ② entirely (move only Player). Flagging for explicit user decision before Phase ② runs.

**Risk #2 — Folder names with spaces.**

`Level Prefabs` and `Character Prefabs` are valid Unity folder names but require careful quoting. All `.cs` files in this codebase already use string literals for paths (no shell-style escaping needed). Spaces in `AssetDatabase.LoadAssetAtPath()` and `MoveAsset()` work natively. No quoting risk for the C# code.

The only place spaces would matter: shell commands invoked from C# (none in this project that I found). Safe to proceed.

**Risk #3 — Comments and XML docstrings reference old paths.**

V2PrefabSource.cs has 3 doc comments mentioning `Assets/Prefabs/Rooms/...`. RoomBuilder.cs:845 has `Example: "Assets/Prefabs/Rooms/Starter"` in its XML doc. CLAUDE.md itself has many references to these paths.

Recommendation: update inline doc comments in the same files I'm editing for code (Phase ③). Skip CLAUDE.md per the prompt's explicit "Do not touch CLAUDE.md" — that's a separate doc pass.

**Risk #4 — `_Archive/` is gone.**

The V1 cleanup audit's "Item D-2" question (keep `_Archive/` or move it outside `Assets/`) was apparently resolved by deletion. No action needed; just noted.

---

## Awaiting approval

Proceed phase-by-phase. Reply with:
- `Proceed Phase ②` to run folder moves
- `Proceed Phase ③` to run string fixes
- `Proceed Phase ④` to run Levels string update
- `Proceed Phase ⑤` to delete `RoomPresets/`
- `Proceed Phase ⑥` is auto-skip (no-op confirmed)
- `Proceed Phase ⑦` for verification
- Or `Proceed Phase ②, ③, ④, ⑤, ⑦` to run all phases sequentially

Phase ② depends on user disposition for **Risk #1** (LVL_Configurator
edit). If you say `Skip LVL_Configurator` I'll move only Player and
defer Rooms/Halls until that decision is resolved.
