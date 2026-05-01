# M3-03A — Duo vs World Bundle GUID Diff

**Date:** 2026-04-30
**Scope:** Read-only diff between the Duo `.unitypackage`
(`RPG Tiny Hero Duo PBR Polyart.unitypackage`, on disk in publisher
cache) and the already-imported World Bundle. Identifies which Duo
assets share GUIDs with the World Bundle (DUPLICATE — skip on
re-import) vs. which are Duo-exclusive (UNIQUE — candidates for
selective re-import). Drives M3-03B's actual re-import.

**Status:** Read-only research. Scratch directory under `/tmp/` used
and cleaned up. No project, asset, or git changes.

**Headline outcome:** Of 87 Duo assets, **31 are UNIQUE** (Duo-only
GUID). Filtered to character-relevant items only, **the recommended
M3-03B re-import set is 11 assets** — 2 character prefabs (Male +
Female PBR) + 1 body mesh + 4 equipped weapon/shield prefabs + 4
equipped weapon/shield meshes. All Polyart variants, scenes,
controllers, and orphan materials are excluded. Estimated re-import
size: < 5 MB.

---

## Section A — Duo package metadata

| Field | Value |
|---|---|
| Path | `C:\Users\Jason\AppData\Roaming\Unity\Asset Store-5.x\Dungeon Mason\3D ModelsCharactersHumanoids\RPG Tiny Hero Duo PBR Polyart.unitypackage` |
| Category folder | `3D ModelsCharactersHumanoids/` (publisher categorized as Humanoids — different from World Bundle's `3D ModelsEnvironmentsFantasy/`) |
| Size | 20,519,757 bytes (≈ 19.6 MiB) |
| SHA-256 | `7d0bf88bbe659fe1df6fefb495474f384fa29e512d4e39bf1dca7182155b9cd4` |
| Total assets in manifest | 87 |

---

## Section B — Duo pack folder structure

```
Assets/RPG Tiny Hero Duo/
├── Animation/    (43 assets — all DUPLICATE; same GUIDs as World Bundle)
├── Animator/     (5 assets — controllers + masks; mostly UNIQUE but not character-relevant)
├── Material/     (5 assets — PBR_Default DUP, others UNIQUE / orphan)
├── Mesh/         (12 assets — body + weapon + shield FBXes, all UNIQUE)
├── Prefab/       (13 assets — char + OHS + shield, all UNIQUE)
├── Scene/        (5 assets — demo scenes, all UNIQUE)
└── Texture/      (4 assets — all DUPLICATE)
```

Note publisher path is `Assets/RPG Tiny Hero Duo/` (root-of-Assets,
not under `AssetPacks/`). The previous pack import was relocated to
`Assets/AssetPacks/RPG Tiny Hero Duo/` per the project's convention.

---

## Section C — World Bundle GUID inventory

| Source | Count |
|---|---|
| `.meta` files in `Assets/AssetPacks/RPG Tiny Hero World Bundle/` | 1619 |
| Unique GUIDs extracted | 1619 |

Used as the diff baseline. Anything in this set is "already in
World Bundle" — re-importing those GUIDs from the Duo would
collide.

---

## Section D — GUID diff summary

| Bucket | Count | Note |
|---|---|---|
| Total Duo assets | **87** | |
| **DUPLICATE** (same GUID in WB) | **56** | Skip on re-import |
| **UNIQUE** (Duo-only GUID) | **31** | Candidate for re-import |

Of the 56 DUPLICATES, the bulk are the 43 SwordAndShield animation
FBXes (already verified GUID-byte-identical in M3-01 Section E),
plus shared materials (PBR_Default.mat ↔ World Bundle's
`DefaultPBR.mat` — same GUID `f323cced...8b67`, just renamed) and
the 4 character textures.

---

## Section E — UNIQUE assets grouped by type (all 31)

| Category | Count | Files |
|---|---|---|
| Prefabs | 12 | 4 character (Male/Female × PBR/Polyart) + 4 OHS swords + 4 shields |
| Meshes (FBX) | 10 | 2 ModularCharacter (PBR/Polyart) + 4 OHS + 4 Shield |
| Scenes | 3 | AnimationLayer, PolyartScene, RootMotion |
| Materials | 3 | Polyart_Default, Skybox_Mat, Stage |
| Controllers | 2 | AnimationLayer.controller, RootMotion.controller |
| Mask | 1 | AnimLayer.mask |

### All 31 UNIQUE asset paths

```
Prefabs (12):
  Assets/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab
  Assets/RPG Tiny Hero Duo/Prefab/MaleCharacterPolyart.prefab
  Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPBR.prefab
  Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPolyart.prefab
  Assets/RPG Tiny Hero Duo/Prefab/OHS03PBR.prefab
  Assets/RPG Tiny Hero Duo/Prefab/OHS03Polyart.prefab
  Assets/RPG Tiny Hero Duo/Prefab/OHS06PBR.prefab
  Assets/RPG Tiny Hero Duo/Prefab/OHS06Polyart.prefab
  Assets/RPG Tiny Hero Duo/Prefab/Shield05PBR.prefab
  Assets/RPG Tiny Hero Duo/Prefab/Shield05Polyart.prefab
  Assets/RPG Tiny Hero Duo/Prefab/Shield08PBR.prefab
  Assets/RPG Tiny Hero Duo/Prefab/Shield08Polyart.prefab

Meshes (10):
  Assets/RPG Tiny Hero Duo/Mesh/ModularCharacterPBR.fbx
  Assets/RPG Tiny Hero Duo/Mesh/ModularCharacterPolyart.fbx
  Assets/RPG Tiny Hero Duo/Mesh/OHS03PBR.fbx
  Assets/RPG Tiny Hero Duo/Mesh/OHS03Polyart.fbx
  Assets/RPG Tiny Hero Duo/Mesh/OHS06PBR.fbx
  Assets/RPG Tiny Hero Duo/Mesh/OHS06Polyart.fbx
  Assets/RPG Tiny Hero Duo/Mesh/Shield05PBR.fbx
  Assets/RPG Tiny Hero Duo/Mesh/Shield05Polyart.fbx
  Assets/RPG Tiny Hero Duo/Mesh/Shield08PBR.fbx
  Assets/RPG Tiny Hero Duo/Mesh/Shield08Polyart.fbx

Scenes (3):
  Assets/RPG Tiny Hero Duo/Scene/AnimationLayer.unity
  Assets/RPG Tiny Hero Duo/Scene/PolyartScene.unity
  Assets/RPG Tiny Hero Duo/Scene/RootMotion.unity

Materials (3):
  Assets/RPG Tiny Hero Duo/Material/Polyart_Default.mat   (Polyart shading variant)
  Assets/RPG Tiny Hero Duo/Material/Skybox_Mat.mat        (demo scene)
  Assets/RPG Tiny Hero Duo/Material/Stage.mat             (demo scene)

Controllers + mask (3):
  Assets/RPG Tiny Hero Duo/Animator/AnimationLayer.controller
  Assets/RPG Tiny Hero Duo/Animator/RootMotion.controller
  Assets/RPG Tiny Hero Duo/Animator/AnimLayer.mask
```

---

## Section F — Character-relevant UNIQUE assets (dependency tree)

Both character prefabs are **pre-equipped with weapons + shields**.
Their GameObject hierarchy includes nested PrefabInstance refs to
the OHS sword + Shield. Excluding the equipment would import the
character prefabs in a "Missing Nested Prefab" state, which is
exactly the M3-02A condition we're trying to fix elsewhere.

Conclusion: re-importing the character prefabs requires
re-importing their equipped weapons + the meshes those weapons
reference.

### Dependency tree

```
MaleCharacterPBR.prefab  (UNIQUE, GUID 2dfbb63c...581598)
  ├─ Avatar source       → Idle_Battle_SwordAndShiled.fbx        (DUPLICATE — already in WB)
  ├─ Animator controller → SwordAndShieldStance.controller       (DUPLICATE — example, we override anyway)
  ├─ Body mesh           → ModularCharacterPBR.fbx               (UNIQUE — must re-import)
  ├─ Body material       → PBR_Default.mat / WB DefaultPBR.mat   (DUPLICATE — already in WB)
  ├─ Equipped sword      → OHS03PBR.prefab                       (UNIQUE — must re-import)
  │    └─ mesh           → OHS03PBR.fbx                          (UNIQUE — must re-import)
  └─ Equipped shield     → Shield08PBR.prefab                    (UNIQUE — must re-import)
       └─ mesh           → Shield08PBR.fbx                       (UNIQUE — must re-import)

FemaleCharacterPBR.prefab  (UNIQUE, GUID cc91c8ba...0b64)
  ├─ Avatar source       → Idle_Battle_SwordAndShiled.fbx        (DUPLICATE)
  ├─ Animator controller → SwordAndShieldStance.controller       (DUPLICATE)
  ├─ Body mesh           → ModularCharacterPBR.fbx               (UNIQUE — same as Male)
  ├─ Body material       → PBR_Default.mat                       (DUPLICATE)
  ├─ Equipped sword      → OHS06PBR.prefab                       (UNIQUE — different from Male's)
  │    └─ mesh           → OHS06PBR.fbx                          (UNIQUE)
  └─ Equipped shield     → Shield05PBR.prefab                    (UNIQUE — different from Male's)
       └─ mesh           → Shield05PBR.fbx                       (UNIQUE)
```

### Polyart variants intentionally excluded

The Duo ships PBR + Polyart variants of each character / mesh /
weapon. Per the locked PBR target, we exclude all `*Polyart*`
items. The 12 Polyart UNIQUEs (4 prefabs + 4 OHS prefabs would
be Polyart, etc.) are skipped.

If we ever want the Polyart shading look later, those are still
recoverable from the Duo `.unitypackage` cache at any time.

---

## Section G — Recommended re-import set (M3-03B input)

**Total: 11 assets** — 2 character prefabs + 5 supporting prefabs +
5 supporting meshes (counted as ModularCharacterPBR is 1 mesh shared
between Male and Female).

```
Character prefabs (2):
  Assets/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab
    GUID: 2dfbb63c9cdf7504faf4ff26b0581598
  Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPBR.prefab
    GUID: cc91c8ba8b9a34f4d99e70d721f60b64

Body mesh (1, shared between Male and Female):
  Assets/RPG Tiny Hero Duo/Mesh/ModularCharacterPBR.fbx
    GUID: 34b0895dfc863f742aa5075a4e691859

Equipment prefabs (4):
  Assets/RPG Tiny Hero Duo/Prefab/OHS03PBR.prefab     (Male's sword)
    GUID: 9fbe61d1f4bc091439f25f985dd189a5
  Assets/RPG Tiny Hero Duo/Prefab/OHS06PBR.prefab     (Female's sword)
    GUID: 3617e4093ca8d5145986d37c89cfd692
  Assets/RPG Tiny Hero Duo/Prefab/Shield05PBR.prefab  (Female's shield)
    GUID: ac86f8ddca59acb4184d3eb42067bafa
  Assets/RPG Tiny Hero Duo/Prefab/Shield08PBR.prefab  (Male's shield)
    GUID: 573036acf9f1e0c42845dddc25cea245

Equipment meshes (4):
  Assets/RPG Tiny Hero Duo/Mesh/OHS03PBR.fbx
    GUID: de392f919a34e4e4c94a6e77ef08a22d
  Assets/RPG Tiny Hero Duo/Mesh/OHS06PBR.fbx
    GUID: 8e583e9185af1be4e9dd6f501e6fe7d4
  Assets/RPG Tiny Hero Duo/Mesh/Shield05PBR.fbx
    GUID: 99054bc45d8ad7442b1e413b0e260ca7
  Assets/RPG Tiny Hero Duo/Mesh/Shield08PBR.fbx
    GUID: 4f74d725d371c394ba3753952409c5ee
```

All 11 assets have GUIDs that do **not** exist in the World Bundle —
so re-importing causes no GUID collisions.

### What M3-02B can use this for

After M3-03B re-imports these 11 assets, `Player_MaleHero.prefab`
will see its missing PrefabInstance reference (GUID
`2dfbb63c9cdf7504faf4ff26b0581598`) **resolve automatically** — the
same way the 13 animation-clip references resolved in M3-02A. The
player rig will visually return to the Duo's MaleCharacterPBR look
without any prefab edits.

This means M3-02B is essentially a no-op once M3-03B runs:
deleting the broken-PrefabInstance warning is automatic.

---

## Section H — Sanity-check results

Per Step ⑥ of the prompt:

| File | Expected | Actual | Status |
|---|---|---|---|
| `Prefab/MaleCharacterPBR.prefab` | UNIQUE | UNIQUE (`2dfbb63c...`) | ✓ |
| `Prefab/FemaleCharacterPBR.prefab` | UNIQUE if exists | UNIQUE (`cc91c8ba...`) — exists | ✓ |
| `Mesh/ModularCharacterPBR.fbx` | UNIQUE | UNIQUE (`34b0895d...`) | ✓ |

All three sanity checks passed. Approach is valid.

---

## Section I — Open questions for the user

1. **Confirm the 11-asset re-import set** in Section G? Cleanest
   path forward is "yes" — anything less leaves missing-prefab
   warnings on character load.

2. **Polyart variants — confirmed exclude?** Skipping them by
   default per the locked PBR target. If you want them as a
   visual-style backup, M3-03B can include all 22 PBR+Polyart
   character/equipment items instead of just the 11 PBR-only items.
   Recovery cost is low (just re-running the import filter).

3. **Demo scenes (3)** — `AnimationLayer.unity`,
   `PolyartScene.unity`, `RootMotion.unity` — these are pack-author
   showcase scenes, not gameplay scenes. Recommend exclude. If you
   want them as reference material later, recoverable from cache.

4. **Animator controllers (3) + mask (1)** — `AnimationLayer.controller`,
   `RootMotion.controller`, `AnimLayer.mask`. These are pack
   examples; we use our own `PlayerBaseController.controller`.
   Recommend exclude.

5. **Demo materials (3)** — `Polyart_Default.mat`, `Skybox_Mat.mat`,
   `Stage.mat`. Two are demo-scene-only, one is the Polyart variant.
   None used by the PBR character. Recommend exclude.

---

## Cleanup

`/tmp/duo-inventory-diff/` scratch directory deleted at end.
Duo `.unitypackage` left untouched in publisher cache.

---

**Report path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M3_03A_duo_diff.md`
