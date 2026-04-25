# V2 Level Generator — Design Spec

**Status:** Phases A–D complete. Theme-aware prefab selection is the only spec item still deferred.
**Date:** 2026-04-25

**Implementation status:**
- Phase A — EditorWindow shell + validation: COMPLETE
- Phase B — Spine-only generator (Starter → rooms → Boss, with backtracking): COMPLETE
- Phase C — Branches off the spine: COMPLETE (theme-aware selection deferred — manifest logs theme name but generator still pulls from raw folders)
- Phase D — Scene save (.unity) + manifest text output: COMPLETE

---

## Overview

V2 builds **single-scene baked levels** at edit time. A user opens an
EditorWindow, sets parameters, hits Generate, and gets a `.unity` scene
file plus a manifest log. Output is a static dungeon assembled from
saved RoomPiece and Hall prefabs.

Architecture: **build from scratch.** The V1 audit confirmed there is
no V1 generator engine to bridge into. V1's contribution is the
data model only: `RoomPiece`, `ExitPoint`, and `RoomPiece.GetWorldBounds()`.
Everything else (intake, theme-aware selection, collision check,
rotation, backtracking, scene saving) is new V2 code.

---

## User-facing controls (EditorWindow params)

### Output
- **Scene name** (string, e.g. `Dungeon_Tutorial_01`)
- **Output folder** (Assets-relative path, e.g. `Assets/Levels/Generated/`)

### Source
- **Catalogue** (PieceCatalogue reference)
- **Theme name** (dropdown populated from catalogue's themes; "(none)"
  uses raw prefab folders only)

### Room budget (per category)
- Starter count: int (default 1, normally 1)
- Boss count: int (default 1, normally 1)
- Small count: int
- Medium count: int
- Large count: int
- Special count: int

### Hall budget
- **Spine hall size** (dropdown: Small / Medium / Large / Special) —
  applied to every hall connecting two spine rooms
- **Branch hall size** (dropdown: Small / Medium / Large / Special) —
  applied to every hall connecting a spine room to a branch room
- Hall counts are derived from layout (spine length and branch slot
  count), not user-specified.

### Layout
- **Layout style:** Linear / Grid / Organic / Corridor (initial:
  Linear-with-branches; others stubbed for later)
- **Branch slot count:** N reserved branch positions. These are
  drawn from the same Small/Medium/Large/Special pool as the spine.
  Spine length = (S+M+L+Special) − branchSlotCount.
- **Difficulty signals:**
  - Branching factor (0–1 chance multiplier)
  - Dead-end count (rooms with one exit only)
  - Secret-room count (special rooms hidden behind specific connections)

### Reproducibility
- **Seed:** int, blank/0 = random each run

---

## Generation algorithm

### Spine (linear path Starter → Boss)

1. Place **Starter** room at world origin, parented under
   `GeneratedLevel` root GameObject.
2. **Spine length** = total Rooms (Small + Medium + Large) − Branch slot
   count. Boss is appended at the end of the spine, so the spine ends
   at Boss.
3. For each spine position 1..N:
   - Pick a random unused exit on the previous room.
   - Pick a random room category from the remaining unused budget
     across **Small + Medium + Large + Special** (single combined
     pool). The draw is weighted by remaining counts: if remaining is
     `(small=2, medium=1, large=0, special=1)`, the random draw pool
     size is 4 — 2/4 chance Small, 1/4 chance Medium, 1/4 chance
     Special. Decrement that category's counter. No round-robin
     ordering — every placement is a random draw from whatever's left.
   - Pick a random room prefab from that category's folder that has at
     least one ExitPoint matching the required incoming direction
     (after rotation candidates are considered).
   - Try to align an exit on the new room to the chosen previous-room
     exit, allowing **90° rotation** (0/90/180/270) on the new room.
   - Insert the **Spine hall** prefab (chosen size from EditorWindow)
     between the two aligned exits.
   - Run collision check (RoomPiece bounds AABB) against all prior
     placed rooms + halls.
   - If collision: try a different rotation, then a different exit on
     the previous room, then a different room candidate from the same
     category.
   - If all options exhausted: **backtrack** — undo the last spine room
     and re-pick.

### Branches (side rooms off the spine)

4. For each reserved branch slot:
   - Pick a random spine room with at least one unused exit.
     "Spine room" means Starter, any spine room placed in step 3,
     or Boss — anything currently in the placed list that came
     from a non-branch slot.
   - Pick a random unused exit on that spine room.
   - Pick a random category from the **same combined pool** as the
     spine — Small / Medium / Large / Special — weighted by
     remaining counts. (No spine-vs-branch budget distinction.)
   - Place + connect via the **Branch hall** prefab (chosen size
     from EditorWindow) the same way as spine rooms.
   - Same backtracking rules. On dead end, restore the budget
     counter and try a different category, then a different
     attach exit, then a different attach room.

Spine length = (Small + Medium + Large + Special) − branchSlotCount.
Starter and Boss are not counted in the pool.

### Boss room

5. Boss is the final room on the spine. It uses the **Boss** category.
6. Boss placement is otherwise identical to spine — random exit,
   random rotation, hall, collision check.
7. If Boss can't be placed (extreme corner case), generation fails
   completely — no partial scene saved.

### Hall sizing

- Halls are **structural prefabs**. There is no "gap" to measure — the
  hall *is* the space between two rooms.
- The user specifies hall size per context in the EditorWindow:
  - **Spine hall size** for connections between two spine rooms
  - **Branch hall size** for connections between a spine room and a
    branch room
- The chosen size determines which folder the hall comes from
  (`Assets/Prefabs/Halls/{Size}/`).

### Connection geometry

- Each connection is a 3-piece chain:
  `Room1.exit → Hall.entry, Hall.exit → Room2.entry`
- Snap order: place Room1 first (it's already placed), then snap
  Hall's entry ExitPoint to Room1's chosen exit ExitPoint, then snap
  Room2's entry ExitPoint to Hall's exit ExitPoint.
- Snap math: align positions (entry to exit), then rotate so the
  connecting room's incoming ExitPoint forward vector is the
  negation of the previous piece's outgoing ExitPoint forward
  vector. ("They face each other.")
- Rotation is constrained to {0°, 90°, 180°, 270°} on the Y axis.

### Hall sourcing

- Halls are pulled from `Assets/Prefabs/Halls/{Size}/` where `{Size}`
  matches the user's chosen Spine or Branch hall size.
- If the chosen Theme defines a Hall prefab and the folder is empty,
  fall back to the Theme's hall.
- If neither exists for that size, generation fails for that
  connection — backtracking applies.
- Manifest logs the hall prefab name used per connection.

### Piece selection within a category

- Filter the category's prefabs to those with at least one ExitPoint
  matching the required direction (after rotation candidates considered).
- Pick at random from that filtered set.
- If filtered set is empty: backtrack.

### Backtracking

- Maintain a placement stack: each entry = (room/hall, parent exit,
  rotation, candidate-set-index).
- On dead end, pop the top entry and try the next candidate at that
  level. If all candidates at that level exhausted, pop again.
- Cap total backtracks at e.g. 50 — beyond that, fail with manifest
  noting the failure point.

### Output

- Single root GameObject `GeneratedLevel` parents every spawned room
  and hall.
- Scene saved to `{outputFolder}/{sceneName}.unity`.
- Manifest saved to `{outputFolder}/{sceneName}_manifest.txt` —
  contents:
  - Seed used
  - All input params (catalogue, theme, counts, layout settings)
  - Final room/hall list with placement order
  - Backtrack count
  - Slack per hall (gap vs hall length)
  - Total generation time

---

## V2 input layer (EditorWindow)

### Entry point

- MenuItem: `LevelGen/V2 Level Generator` opens the window.
- Window title: "V2 Level Generator"

### Layout

Single scrollable EditorWindow with collapsible sections:
- Output (scene name, folder)
- Source (catalogue, theme picker)
- Room Budget (six int fields)
- Hall Budget (four int fields + auto-fill toggle)
- Layout (style dropdown, branch slots, difficulty fields)
- Reproducibility (seed)
- **Generate** button at the bottom — disabled until required params
  are valid (catalogue assigned, scene name non-empty)

### Validation before generate

- Catalogue assigned
- Scene name non-empty and unique in target folder (or prompt to
  overwrite)
- Total room count ≥ 2 (Starter + Boss minimum)
- Branch slot count ≤ spine length − 1

---

## V1 contributions (data model only)

V1's contribution to the V2 generator is the data model. Everything
else is new V2 code.

| V1 piece | Used by V2 generator |
|----------|---------------------|
| `RoomPiece` component | Carries bounds + lookup root for ExitPoints |
| `RoomPiece.GetWorldBounds()` | Generator collision check uses this directly |
| `ExitPoint` component | Connection points for snapping rooms together |

### One bridge required

`RoomPiece.RefreshExits()` must be called immediately after every
`PrefabUtility.InstantiatePrefab` in the generator. Editor-mode
instantiation never fires `Awake()`, so the cached ExitPoint list on
the RoomPiece would otherwise be stale.

### Rotation / bounds caveat

`GetWorldBounds()` returns
`new Bounds(transform.position + boundsOffset, boundsSize * 2f)`. The
extents come straight from `boundsSize` and **do not account for
rotation**. For square rooms (all `LVL_*` prefabs:
`boundsSize.x == boundsSize.z`) this is fine. For non-square
RoomBuilder rooms (e.g. 5×3), a 90° or 270° rotation requires the
generator's collision function to swap the X and Z extents before the
overlap check.

### Things the generator builds from scratch

- Intake interface (EditorWindow params)
- Theme-aware piece selection (themes are V2-only)
- Collision check (AABB against placed-room list)
- Rotation logic (0/90/180/270 candidates per placement)
- Backtracking (placement stack with retry budget)
- Scene save (`EditorSceneManager.SaveScene`)
- Manifest output (text log)

---

## Out of scope for V1 of the V2 generator

- Grid / Organic / Corridor layout styles (Linear-with-branches first)
- Dead-end and secret-room signals (params expose, generator stubs them)
- Theme-aware prop placement (uses Theme for room/hall prefabs only)
- Tier stacking in generated levels (single tier = floor only for now)
- Player spawn / boss trigger / save-point objects (added later)
- Multi-floor dungeon stacking (one .unity = one floor for now)

---

## Open questions

- **Difficulty signal mix on spine:** does difficulty influence what
  category of room gets picked next on the spine? (e.g. higher
  difficulty = more Large rooms toward the end). Not yet decided.
  Initial implementation uses pure random pick from remaining budget.
- **Secret rooms:** exact mechanism for "hidden behind specific
  conditions" needs design once basic generator is working.

## Resolved questions

- **Rotation handling for collision:** RoomPiece.localBounds does NOT
  rotate with the room. For non-square rooms at 90°/270° the
  generator's collision function must swap the X and Z extents
  before the overlap check. Square rooms (LVL_*) need no swap.
- **Hall sizing:** Halls are structural prefabs picked by user choice,
  not by gap measurement. See "Hall sizing" section above.
- **Budget consumption:** Weighted random pick from remaining
  Small/Medium/Large/Special pool per slot. Both spine and branch
  slots draw from the same combined bucket.
- **Spine vs branch budget split:** Single combined pool of
  Small + Medium + Large + Special. Both spine and branch slots
  draw from the same remaining-counts bucket via weighted random
  pick. Branches no longer have a privileged Special-only budget.