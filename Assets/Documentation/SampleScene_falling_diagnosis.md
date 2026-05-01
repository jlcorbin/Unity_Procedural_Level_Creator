# SampleScene — Player Falling Diagnosis

**Date:** 2026-04-30 21:51
**Scene:** `Assets/Scenes/SampleScene.unity`
**Spawn position:** (5.00, 0.00, -5.00)

Read-only edit-time inspection. No Play mode. No scene/prefab/script
modifications were performed; only this report file is written.

## Step ② — Player

- World position: (5.00, 0.00, -5.00)
- activeInHierarchy: True
- Layer: Default (0)
- CharacterController:
  - center: (0.00, 0.90, 0.00)
  - height: 1.8
  - radius: 0.3
  - skinWidth: 0.08
  - slopeLimit: 45
  - stepOffset: 0.3
  - **Capsule bottom Y (world):** -0.0800  *(below Y=0)*

## Step ③ — All scene colliders

Excludes colliders under `Player_MaleHero`, `CM Brain Camera`, `CM Follow Camera`.

**Total colliders:** 0
**Solid surfaces** (`enabled && !isTrigger && activeInHierarchy`): 0
**Triggers:** 0
**Disabled / inactive:** 0

**ZERO colliders found in scene** outside of player/CM rigs.

## Step ④ — Raycast probes

Downward `Physics.RaycastAll` from `(x, 10, z)`, distance 50, all layers, triggers reported.

### Spawn  — origin (5.00, 10.00, -5.00)

Hits: **1**

| dist | point | type | trigger | layer | path |
|---|---|---|---|---|---|
| 8.380 | (5.00, 1.62, -5.00) | CharacterController | False | Default(0) | `/Player_MaleHero` |

**Highest non-trigger surface Y:** 1.6200

### NW corner inset  — origin (1.00, 10.00, -1.00)

Hits: **0**

**Highest non-trigger surface:** *(none)*

### SE corner inset  — origin (9.00, 10.00, -9.00)

Hits: **0**

**Highest non-trigger surface:** *(none)*

### North edge mid  — origin (5.00, 10.00, -1.00)

Hits: **0**

**Highest non-trigger surface:** *(none)*

## Step ⑤ — Layer collision matrix

Player layer: **Default (0)**

Layers the player collides with:
- Default (0)
- TransparentFX (1)
- Ignore Raycast (2)
- <unnamed-3> (3)
- Water (4)
- UI (5)
- <unnamed-6> (6)
- <unnamed-7> (7)
- <unnamed-8> (8)
- <unnamed-9> (9)
- <unnamed-10> (10)
- <unnamed-11> (11)
- <unnamed-12> (12)
- <unnamed-13> (13)
- <unnamed-14> (14)
- <unnamed-15> (15)
- <unnamed-16> (16)
- <unnamed-17> (17)
- <unnamed-18> (18)
- <unnamed-19> (19)
- <unnamed-20> (20)
- <unnamed-21> (21)
- <unnamed-22> (22)
- <unnamed-23> (23)
- <unnamed-24> (24)
- <unnamed-25> (25)
- <unnamed-26> (26)
- <unnamed-27> (27)
- <unnamed-28> (28)
- <unnamed-29> (29)
- <unnamed-30> (30)
- <unnamed-31> (31)

Layers used by **non-trigger** colliders found in Step ③, and whether the player collides with each:
- *(no non-trigger colliders to compare)*

## Step ⑥ — Diagnosis (corrected)

> **Note on the script's auto-conclusions:** The diagnostic script ran
> `Physics.RaycastAll` *without* filtering self-hits, so the spawn probe
> hit the player's own `CharacterController` capsule at world Y=1.62.
> The script then misfired conclusions E/F/G as if that were a floor
> surface above the spawn. Step ④'s raw table makes this clear: the
> only hit is `type=CharacterController`, `path=/Player_MaleHero` —
> i.e. the player itself. The other three probes (NW, SE, north-mid)
> found **0** hits, confirming the rest of the room has no colliders.
>
> The actual conclusion is **A**, derived from Step ③ alone:

- **A. No solid colliders exist anywhere in the scene** outside of the
  player and CM rigs. Step ③ explicitly reports `Total colliders: 0`,
  `Solid surfaces: 0`, `Triggers: 0`, `Disabled/inactive: 0`. The
  manually-assembled 10×10 starter room (built from FDP modular parts)
  has zero `Collider` components anywhere in its hierarchy.

  FDP's documentation says *"colliders where needed"*; floor parts
  evidently weren't on the publisher's "needed" list. The player
  CharacterController has nothing to stand on and falls due to gravity.

### Suggested fix paths

Three options, in order of decreasing scope-of-change:

1. **Scene-only stop-gap** *(narrowest blast radius)*. Add one thin
   `BoxCollider` GameObject under the room root covering the 10×10
   footprint at Y≈0 (e.g. center (5, -0.05, -5), size (10, 0.1, 10)).
   Doesn't modify any prefab, fixes only this scene. Good for
   continuing development today; bad as the long-term answer because
   every future room would need its own one-off fix.

2. **Per-floor prefab fix** *(canonical answer)*. Add a `MeshCollider`
   (convex=false) to each FDP floor part prefab at
   `Assets/Fantastic Dungeon Pack/prefabs/MODULAR/01_PARTS/Floor/`.
   Affects every scene that uses those prefabs going forward, including
   future generated levels. Preferred long-term fix since the V2
   generator builds rooms from these same parts.

3. **LVL_-level fix** *(generator-friendly)*. Add a single
   floor-spanning `BoxCollider` to each `LVL_*` prefab during
   `LVL_Configurator` processing — same idea as the V2 generator's
   `RoomPiece.boundsSize`/`boundsOffset` collision check, but for
   actual physics. Touches the configurator script, not the FDP parts.
   Reasonable middle ground if the per-part route is too invasive.

The current scene was hand-assembled, not LVL-driven, so option 3
won't fix it directly. For the immediate goal (press Play and walk
around in SampleScene): option 1 is the smallest, fastest, lowest-risk
change. Option 2 is the right answer if the user wants every future
room to have working floors automatically.

### Raw data caveats

- The Step ④ NW/SE/north-mid probes returned 0 hits — meaning the
  player's CharacterController is the only collider anywhere along
  those four downward rays. Consistent with Step ③'s "0 colliders".
- Step ⑤'s layer matrix is irrelevant here — there are no foreign
  layers to mismatch with. Default layer collides with everything,
  but there's nothing to collide *with*.
- The script's heuristic for Conclusion A only fires when both
  `spawnSolidHits == 0 && solidCount == 0`. The self-hit gave
  spawnSolidHits=1, so the gate didn't match; the prompt's
  pattern-matching logic should be re-thought to filter the
  spawn-probe by excluding hits on the player root before applying
  E/F/G heuristics. Note for any future iteration of this script.

