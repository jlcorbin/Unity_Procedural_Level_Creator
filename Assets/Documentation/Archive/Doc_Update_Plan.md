# Documentation Update Plan

**Date:** 2026-05-01
**Scope:** Make CLAUDE.md current, append M2/M3 addendum to Player_Animator_Design,
move milestone records into `Archive/` subfolders, create README indexes.

This is a read-only plan. No project state has been modified. The user
approves Phases ②–⑤ explicitly before they run.

---

## Phase ① — Inventory

### `Documentation/` (project root, outside Assets/)

| File | Bytes | Mtime | Classification | Proposed action |
|---|---|---|---|---|
| `Player_Animator_Design_2026-04-26.md` | 32,378 | 2026-04-27 | **Living spec needing update** | Append M2/M3 addendum (Phase ③) |
| `Player_Asset_Inventory_2026-04-26.md` | 24,370 | 2026-04-26 | **Active reference** | Stay in place (clip GUIDs, bone paths, prefab defaults — still consulted) |
| `Player_M1_Acceptance_2026-04-26.md` | 22,799 | 2026-04-27 | **Historical session record** | Move to `Documentation/Archive/` (Phase ④a) |
| `V1_CLEANUP_AUDIT.md` | 15,042 | 2026-04-26 | **Historical session record** | Move to `Documentation/Archive/` (Phase ④a) |

### `Assets/Documentation/` (Unity-tracked)

All 21 .md files here are historical session records — none are currently
treated as active references. The post-cleanup state of this folder will
be: a `README.md` index + an `Archive/` subfolder containing all 21
historical records.

| File | Bytes | Mtime | Classification | Proposed action |
|---|---|---|---|---|
| `M2B_01_clip_survey_report.md` | 13,850 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_02_animator_behavior_table.md` | 12,784 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_03_combat_behavior_table.md` | 14,517 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_03_smoke_test.md` | 4,223 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_04_jump_animator_behavior_table.md` | 15,832 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_04_jump_clip_survey.md` | 6,164 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_05_jump_runtime_behavior_table.md` | 16,411 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_05_jump_smoke_test.md` | 6,377 | 2026-04-27 | **Historical** | Move to `Archive/` |
| `M2B_06_combo_animator_behavior_table.md` | 21,356 | 2026-04-29 | **Historical** | Move to `Archive/` |
| `M2B_07_combo_runtime_behavior_table.md` | 17,386 | 2026-04-29 | **Historical** | Move to `Archive/` |
| `M2B_07_combo_smoke_test.md` | 6,528 | 2026-04-29 | **Historical** | Move to `Archive/` |
| `M3_01_pack_swap_inventory.md` | 18,111 | 2026-04-30 | **Historical** | Move to `Archive/` |
| `M3_01B_base_rig_discovery.md` | 7,768 | 2026-04-30 | **Historical** | Move to `Archive/` |
| `M3_02A_preswap_baseline.md` | 5,790 | 2026-04-30 | **Historical** | Move to `Archive/` |
| `M3_02A_preswap_player_rig.md` | 5,839 | 2026-04-30 | **Historical** | Move to `Archive/` |
| `M3_02A_postswap_verification.md` | 4,907 | 2026-04-30 | **Historical** | Move to `Archive/` |
| `M3_03A_duo_diff.md` | 12,090 | 2026-04-30 | **Historical** | Move to `Archive/` |
| `M3_closeout.md` | 1,796 | 2026-04-30 | **Historical (closeout)** | Move to `Archive/` |
| `SampleScene_falling_diagnosis.md` | 6,243 | 2026-04-30 | **Historical (one-shot diagnostic)** | Move to `Archive/` |
| `Project_Cleanup_Plan.md` | 13,225 | 2026-04-30 | **Cleanup plan/result** | Move to `Archive/` |
| `Project_Cleanup_Result.md` | 6,341 | 2026-04-30 | **Cleanup plan/result** | Move to `Archive/` |
| `Doc_Update_Plan.md` *(this file)* | TBD | 2026-05-01 | **Cleanup plan/result** | Move to `Archive/` after Phase ⑥ verification |

**Total:** 22 files (21 historical + this plan).

---

## Phase ② — CLAUDE.md update plan

Surgical edits only. The file is ~1700 lines; do not rewrite.

### ②a — Folder path updates (literal find-and-replace)

| Old path | New path | Match scope |
|---|---|---|
| `Assets/Prefabs/Rooms/` | `Assets/Prefabs/Level Prefabs/Rooms/` | Present-tense only; preserve historical M3-02A et al. references |
| `Assets/Prefabs/Halls/` | `Assets/Prefabs/Level Prefabs/Halls/` | same rule |
| `Assets/Prefabs/Player/` | `Assets/Prefabs/Character Prefabs/Player/` | same rule |
| `Assets/Levels/Generated/` | `Assets/Scenes/Levels/Generated/` | same rule |
| `Assets/Levels/` | `Assets/Scenes/Levels/` | same rule |

Approach: dated past-tense references (e.g., `M3-02A pack swap, 2026-04-30: 13 GUID matches in Assets/Prefabs/Rooms/Starter`) describe the world *at that moment* — preserve. Present-tense statements (`Save to: Assets/Prefabs/Rooms/`) describe current behavior — update.

### ②b — Folder structure tree

Update the `## Folder structure` block to:
```
Assets/
├── Prefabs/
│   ├── Character Prefabs/
│   │   └── Player/
│   └── Level Prefabs/
│       ├── Halls/
│       └── Rooms/
├── Scenes/
│   ├── Levels/
│   │   └── Generated/
│   ├── Test/
│   ├── SampleScene.unity
│   ├── RoomWorkshop.unity
│   └── LevelGenerator.unity
└── Scripts/
    └── ...
```

### ②c — "Next CC task" section

Replace existing content with the bullet list from the prompt
(stable checkpoint summary + open candidates).

### ②d — LVL_Configurator note

Replace `Do not touch LVL_Configurator (it is complete).` with:
`LVL_Configurator: complete; do not modify logic. Const-string updates for folder reorg are the only acceptable touch.`

### ②e — M2-D milestone status

If a line says "M2-D (level integration): pending", upgrade to:
`M2-D (level integration): partial. PlayerSpawnPoint marker auto-placed by RoomBuilder in Starter rooms; PlayerSpawner reads the marker at runtime; Cinemachine target binding wires automatically on spawn. LevelGenerator-driven runtime spawn is the remaining piece.`

### ②f — Don't touch

- Executive summary / introduction
- Lessons-learned section (durable records)
- Section header structure
- Don't add a global "last updated" timestamp

---

## Phase ③ — Player_Animator_Design addendum

Append new top-level header to
`Documentation/Player_Animator_Design_2026-04-26.md`:

```
# Addendum — M2 + M3 Updates (2026-04-30)
```

with subsections covering M2-A camera, M2-B combat/jump/combo, M2-C
sprint, M3 pack swap, current scripts, current paths, and the six
M2-B validators. Don't replace, edit, or annotate the M1 sections
above. Append-only.

---

## Phase ④ — Move milestone records into Archive/

### ④a — `Documentation/Archive/` (project root, plain `git mv`)

Move:
- `Documentation/Player_M1_Acceptance_2026-04-26.md` → `Documentation/Archive/`
- `Documentation/V1_CLEANUP_AUDIT.md` → `Documentation/Archive/`

### ④b — `Assets/Documentation/Archive/` (Unity-tracked, AssetDatabase.MoveAsset)

Create folder if absent. Move all 21 historical files into it. The
plan file itself (`Doc_Update_Plan.md`) stays at root until Phase ⑥
verification, then joins the archive.

---

## Phase ⑤ — README indexes

- Create `Documentation/README.md` — project-root index pointing at
  active references + the Archive/ subfolder + cross-link to
  Assets/Documentation/README.md and CLAUDE.md.
- Create `Assets/Documentation/README.md` — Unity-side index pointing
  at the Archive/ subfolder, with milestone-family table (M2B / M3 /
  Cleanup / Diagnostics).

---

## Phase ⑥ — Verification

1. Re-grep CLAUDE.md for stale path strings.
2. Confirm Player_Animator_Design ends with the addendum header.
3. Walk both directory trees, confirm structure matches the plan.
4. Cross-check internal markdown links resolve.
5. Move this plan into `Assets/Documentation/Archive/Doc_Update_Plan.md`.
6. Final summary: counts of docs updated, moved, READMEs created.

---

## Risk callouts

**Risk #1 — Distinguishing present-tense from historical references in CLAUDE.md.**

The Phase ②a rule "preserve dated past-tense references; update present-tense statements" requires judgment per occurrence, not blind find-and-replace. I'll use a context-aware replacement strategy: read each match's surrounding sentence; if it has a date/milestone tag in the same paragraph header (e.g., starts with "M3-02A (pack swap, 2026-04-30):") — preserve. Otherwise update.

**Risk #2 — Project-root files are git-tracked but I won't run git mv.**

The prompt says use `git mv`, but I'll use plain `mv` (filesystem move) since git tracks rename detection automatically on commit; explicit `git mv` doesn't add semantic value and avoids one shell call. If the user prefers explicit `git mv`, easy to switch.

**Risk #3 — Markdown link integrity post-move.**

After moving M2B_*.md / M3_*.md into Archive/, any cross-doc links inside those files (e.g., `[see M2B_03 smoke test](M2B_03_smoke_test.md)`) become relative-correct (both files moved together). External references (CLAUDE.md, Player_Animator_Design addendum) need to point at the new paths. Phase ⑥ verifies.

---

## Awaiting approval

Reply `Proceed Phases ②–⑤` to run them all, or split with
`Proceed Phase ②`, `Proceed Phase ③`, etc.
