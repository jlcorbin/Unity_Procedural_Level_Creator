# Project Cleanup — Result

**Date:** 2026-04-30
**Plan:** [Project_Cleanup_Plan.md](Project_Cleanup_Plan.md)
**Status:** Complete. 8/8 verifier checks PASS.

---

## What changed

### Folders moved (3 + 1 patch = 4 total `MoveAsset` calls)

| Source | Destination |
|---|---|
| `Assets/Prefabs/Rooms` | `Assets/Prefabs/Level Prefabs/Rooms` |
| `Assets/Prefabs/Halls` | `Assets/Prefabs/Level Prefabs/Halls` |
| `Assets/Prefabs/Player` | `Assets/Prefabs/Character Prefabs/Player` |
| `Assets/Levels` | `Assets/Scenes/Levels` (Phase ⑤b-patch — caught by the
verifier; original Phase ④ was string-only on the false assumption the
folder was already gone) |

Two new umbrella folders created: `Level Prefabs/` and
`Character Prefabs/` under `Assets/Prefabs/`.

### Strings updated (16 literals across 13 files)

| File | Change |
|---|---|
| [LVL_Configurator.cs](Assets/Scripts/Editor/LVL_Configurator.cs) | 2 const-string updates (Rooms / Halls folder roots) |
| [RoomBuilder.cs](Assets/Scripts/LevelEditor/RoomBuilder.cs) | 1 interpolated literal + matching doc comment |
| [V2PrefabSource.cs](Assets/Scripts/LevelGen/V2/Editor/V2PrefabSource.cs) | 2 path literals + 3 doc comments (single bundled edit) |
| [V2LevelGenerator.cs](Assets/Scripts/LevelGen/V2/Editor/V2LevelGenerator.cs) | 8 error-message literals (single bundled edit) |
| [RoomPiece_Test.cs](Assets/Scripts/LevelEditor/Editor/RoomPiece_Test.cs) | 2 const-string updates |
| [V2_SampleThemeBuilder.cs](Assets/Scripts/LevelEditor/Editor/V2_SampleThemeBuilder.cs) | 2 expected-path const updates |
| [PlayerPrefabBuilder.cs](Assets/Scripts/Player/Editor/PlayerPrefabBuilder.cs) | PrefabPath const + EnsureFolder argument |
| [M4_SampleSceneSetup.cs](Assets/Scripts/Player/Editor/M4_SampleSceneSetup.cs) | PrefabPath const |
| [PlayerJumpRuntimeValidator.cs](Assets/Scripts/Player/Editor/PlayerJumpRuntimeValidator.cs) | PrefabPath const |
| [M3_03B_DuoReimportVerifier.cs](Assets/Scripts/Player/Editor/M3_03B_DuoReimportVerifier.cs) | PlayerPrefabPath const |
| [PlayerCombatValidator.cs](Assets/Scripts/Player/Editor/PlayerCombatValidator.cs) | PrefabPath const |
| [PlayerCombatPrefabAdder.cs](Assets/Scripts/Player/Editor/PlayerCombatPrefabAdder.cs) | PrefabPath const |
| [LevelGenSettings.cs](Assets/Scripts/LevelGen/V2/LevelGenSettings.cs) | `outputFolder` default → `Assets/Scenes/Levels/Generated` |
| [V2LevelGeneratorWindow.cs](Assets/Scripts/LevelGen/V2/Editor/V2LevelGeneratorWindow.cs) | `DefaultFolder` const → same |

### Folders deleted (3)

| Path | Reason |
|---|---|
| `Assets/RoomPresets/` | 8 orphaned V1 ScriptableObjects (`RoomPreset` / `RoomPresetLibrary` types retired) |
| `Assets/LevelSequences/` | Empty leftover from V1 cleanup |
| `Assets/Levels/` | Moved to `Assets/Scenes/Levels/` (not deleted; one-line distinction) |

### Scripts deleted

**None.** Phase ① audit confirmed zero V1 class definitions remain in
the live tree. The 2026-04-26 V1 cleanup is still complete.

---

## Verifier results

`LevelGen ▶ Cleanup ⑦ — Verify` reports:

```
PASS — Player_MaleHero loads at new path: loaded 'Player_MaleHero'
PASS — Player_MaleHero has CameraTarget child: local pos (0.00, 1.60, 0.00)
PASS — Starter_10x10 loads at new path: loaded 'Starter_10x10'
PASS — SampleScene opens: path = 'Assets/Scenes/SampleScene.unity'
PASS — Assets/RoomPresets gone: absent
PASS — Assets/LevelSequences gone: absent
PASS — Assets/Levels gone (moved to Assets/Scenes/Levels): absent
PASS — Assets/Scenes/Levels exists: present
SUMMARY — 8 PASS / 0 FAIL
```

Compile cleanliness implicit (the verifier itself only invokes if the
project compiled).

---

## New tree layout

```
Assets/Prefabs/
  Character Prefabs/
    Player/
      Player_MaleHero.prefab
  Level Prefabs/
    Halls/
      Large/
      Medium/
      Small/
    Rooms/
      Boss/
      Curated/
      Large/
      Medium/
      Small/
      Starter/

Assets/Scenes/
  Levels/
    Generated/
      Dungeon_New.unity
      Dungeon_New_manifest.txt
  Test/
    Player_M1_Test.unity
  SampleScene.unity
  RoomWorkshop.unity
  LevelGenerator.unity
```

---

## Diagnostic miss noted for future cleanups

My Phase ① audit reported `Assets/Levels/` and `Assets/LevelSequences/`
as "already gone" because the `Glob` pattern `Assets/Levels/**/*` and
`Assets/LevelSequences/**/*` returned no matches. Both folders **did**
exist as empty folders (with `.meta` files); `Glob` only matches files,
not empty containers. Phase ⑤'s `AssetDatabase.IsValidFolder` correctly
flagged both. Phase ⑤b-patch corrected the miss.

Lesson for similar future audits: pair `Assets/X/**/*` (file glob)
with `Assets/X*` (.meta sidecar glob) to detect empty-but-present
folders.

---

## One-off scaffolding scripts

The following scripts were created for this cleanup and **can be safely
deleted** along with their `.meta` files:

```
Assets/Scripts/Editor/Cleanup_Phase2_FolderReorg.cs
Assets/Scripts/Editor/Cleanup_Phase5_DeleteLegacyFolders.cs
Assets/Scripts/Editor/Cleanup_Phase5b_PatchMoveAndDelete.cs
Assets/Scripts/Editor/Cleanup_Phase7_Verify.cs
```

Their menu items disappear automatically when deleted. None of them
have runtime behavior — they only fire on user-driven menu invocation.

Earlier one-offs from prior milestones can also go (per their own
"can be safely deleted after milestone X" notes in their headers):
`M3_02A_PackSwapExecutor.cs`, `M3_03B_DuoReimportVerifier.cs`,
`M4_SampleSceneSetup.cs`, `M5_FallingDiagnosis.cs`,
`M6_FloorColliderStopgap.cs`. These are the user's call — they're
inert until invoked, no maintenance cost to leaving them.

---

## CLAUDE.md is now stale

CLAUDE.md was explicitly out-of-scope for this prompt, but several
folder paths in it now point at the old layout:
- `Assets/Prefabs/Rooms/` → should be `Assets/Prefabs/Level Prefabs/Rooms/`
- `Assets/Prefabs/Halls/` → should be `Assets/Prefabs/Level Prefabs/Halls/`
- `Assets/Prefabs/Player/` → should be `Assets/Prefabs/Character Prefabs/Player/`
- `Assets/Levels/Generated/` → should be `Assets/Scenes/Levels/Generated/`

A separate doc-pass prompt should update these. The "do not touch
LVL_Configurator" line in CLAUDE.md is now also slightly inconsistent —
the configurator was edited in Phase ③ (two const updates only, no
logic change), explicitly approved.
