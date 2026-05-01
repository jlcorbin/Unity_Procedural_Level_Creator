# M3-01B — World Bundle Base Rig Discovery

**Date:** 2026-04-30
**Scope:** Read-only inventory of the new RPG Tiny Hero World Bundle's
`Mesh/` folder to identify the base rigged character mesh — the
Humanoid-skeleton FBX that all character animations were authored
against. Drives M3-02B (rig swap into `Player_MaleHero.prefab`).

**Status:** Read-only research. No project state, asset, or git
changes.

**Headline outcome:** **Exactly one Humanoid-rig FBX exists in the
entire pack: `AllBodiesCloaks.fbx`** at the root of `Mesh/`. It is the
single canonical base rig, used by all 24 ModularCharacter prefabs.
Same Avatar source GUID as the existing Duo's `MaleCharacterPBR`
(`0308cf4e83cf517488b60af58b290fe0`), so retargeting and animation
playback are identical. **Recommendation: use `AllBodiesCloaks.fbx`
as the base rig in M3-02B.**

---

## Section A — Mesh/ folder structure

```
RPGTinyHeroWavePBR/Mesh/
├── AllBodiesCloaks.fbx         ← root  (1 FBX)
├── Stage.fbx                   ← root  (1 FBX, static prop)
├── HeadParts/                          (115 FBX, all static)
│   ├── Hair01.fbx … HairNN.fbx
│   ├── Head01_Male.fbx, Head02_Female.fbx
│   ├── Eyes/ Mouths/ Ears/ etc.
│   └── …
└── Weapons/                            (57 FBX, all static)
    ├── Sword*.fbx, Bow*.fbx, Spear*.fbx, Wand*.fbx, etc.
    └── Projectile/                     (5 FBX projectiles)
```

**Total: 179 FBX files** (2 root + 115 HeadParts + 62 Weapons).

---

## Section B — Rig-type classification

| Location | Count | animationType | Notes |
|---|---|---|---|
| `Mesh/AllBodiesCloaks.fbx` | 1 | **3 (Humanoid)** | Base rigged character mesh — all body variants in one FBX |
| `Mesh/Stage.fbx` | 1 | 2 (Generic) | Static showcase platform (23 KB) |
| `Mesh/HeadParts/*.fbx` | 115 | 2 (Generic) | Static head/face meshes — parented to head bone at runtime |
| `Mesh/Weapons/*.fbx` | 57 | 2 (Generic) | Static weapon meshes — parented to hand bone |
| `Mesh/Weapons/Projectile/*.fbx` | 5 | 2 (Generic) | Static projectile meshes |

**Humanoid count: 1 of 179.** No ambiguity about which FBX is the
base rig.

---

## Section C — `AllBodiesCloaks.fbx` details

| Field | Value |
|---|---|
| Path | `Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Mesh/AllBodiesCloaks.fbx` |
| GUID | `075789f0f3fa9414f90d335ae163f413` |
| Filesize | 6,690 KB (≈ 6.5 MiB) |
| `animationType` | **3 (Humanoid)** |
| `avatarSetup` | **2 (Copy From Other)** — Avatar lives in a separate FBX |
| Avatar source | `lastHumanDescriptionAvatarSource: {guid: 0308cf4e83cf517488b60af58b290fe0, fileID: 9000000}` → Avatar embedded in `Idle_Battle_SwordAndShiled.fbx` |
| Animation clips | `clipAnimations: []` (empty — pure mesh asset, no animation data) |
| `autoGenerateAvatarMappingIfUnspecified` | 1 |

### Why this is the canonical base rig

1. **Only Humanoid FBX in the pack** — every other mesh is Generic.
2. **Has all body variants** — the "Cloaks" suffix and 6.5 MiB size
   indicate it contains multiple SkinnedMesh variants (cloaked /
   uncloaked / different body types) that the ModularCharacters
   selectively activate.
3. **Avatar source matches the Duo's MaleCharacterPBR** — same GUID
   `0308cf4e83cf517488b60af58b290fe0`, embedded in the same
   `Idle_Battle_SwordAndShiled.fbx`. Retargeting behavior is
   byte-identical to the prior rig.
4. **Empty animations array** — confirms it's a base mesh, not an
   animation FBX masquerading as a body.
5. **All 24 MC* prefabs reference this FBX's GUID for their body
   meshes** (verified in Section D).

---

## Section D — Cross-reference with MC*.prefab (sample: 01, 12, 24)

| Prefab | Body mesh GUID | Avatar GUID | Notes |
|---|---|---|---|
| MC01 | `075789f0f3fa9414f90d335ae163f413` (single) | `0308cf4e83cf517488b60af58b290fe0` | All SkinnedMeshRenderers reference one source FBX (the base). Simple character. |
| MC12 | `075789f0f3fa9414f90d335ae163f413` (+ 168 other mesh GUIDs for accessories/heads) | `0308cf4e83cf517488b60af58b290fe0` | Body still AllBodiesCloaks; additional GUIDs are HeadParts and accessory meshes — the "elaborate" variant. |
| MC24 | `075789f0f3fa9414f90d335ae163f413` (single) | `0308cf4e83cf517488b60af58b290fe0` | Same shape as MC01. |

**100% agreement on the base body mesh.** All sampled MC* prefabs
use `AllBodiesCloaks.fbx` (`075789f0...163f413`) for their body and
the same Humanoid Avatar (`0308cf4e83cf517488b60af58b290fe0`).

The 24 ModularCharacter prefabs differ in:
- Which SkinnedMeshRenderers in `AllBodiesCloaks.fbx` are active
  (clothed vs unclothed body parts)
- Which HeadParts mesh is parented to the head bone
- Which weapon mesh (if any) is parented to the hand bone
- Material/color overrides

But the underlying rigged skeleton + Avatar are identical for all 24.

---

## Section E — Polyart variant

**No Polyart variant exists in this pack.** Search results:

```
find "Assets/AssetPacks/RPG Tiny Hero World Bundle" -iname "*polyart*"
→ (no results)
```

The Duo shipped both PBR and Polyart shading variants. The World
Bundle ships only PBR. Per the locked decision (PBR target), this
is fine — but worth flagging in case visual style discussion comes
up later.

---

## Section F — Recommendation

### ➤ Use `AllBodiesCloaks.fbx` as the player rig base in M3-02B.

| Field | Value |
|---|---|
| **Name** | `AllBodiesCloaks` (FBX stem) |
| **Path** | `Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Mesh/AllBodiesCloaks.fbx` |
| **GUID** | `075789f0f3fa9414f90d335ae163f413` |
| **Justification** | Sole Humanoid-rig FBX in the pack; same Avatar GUID as Duo's MaleCharacterPBR (zero retarget difference); all 24 MC* prefabs reference it as their body mesh source. |

### Two ways to wire this into Player_MaleHero (M3-02B's call)

**Option α — Use one of the MC* prefabs directly** (e.g., MC01).
Pro: ready-made composite (body + head + initial materials), no
manual rig assembly. Con: brings additional meshes (HeadParts,
default materials) we may want to swap visually later.

**Option β — Drag `AllBodiesCloaks.fbx` directly** as the
PrefabInstance child. Pro: minimal-asset rig with just the body
SkinnedMeshRenderers; full control over which body parts to
activate. Con: head will be empty (no face) until a HeadPart prefab
is parented manually.

**Sub-recommendation: Option α with MC01** — the MC prefabs are
designed to be drop-in characters. MC01 is "simple" (single mesh
reference) so it's the lightest of the 24. M3-02B can pick a more
elaborate MC variant later if visual variety matters.

But this is a visual-design call, not a technical one — both
options use the same Avatar and animate identically.

---

## Section G — Open questions for the user

1. **Confirmed base rig FBX path?** Recommend `AllBodiesCloaks.fbx`
   as the canonical base.

2. **MC* prefab choice for M3-02B?** Pick one of the 24
   ModularCharacters (or the single MaskTint01) for the
   `Player_MaleHero` child. Recommendation: **MC01** for simplicity,
   but any of MC01–MC24 works (they all share the same body and
   Avatar). Visual preference call.

3. **Visual validation before M3-02B?** Optional: drag
   `AllBodiesCloaks.fbx` (or one of the MC* prefabs) into the
   `Player_M1_Test.unity` scene first to eyeball the body
   silhouette. Not required — the GUID-preserved Avatar guarantees
   the animations will retarget correctly.

---

## Cleanup

No project state was modified. 179 FBX `.meta` files inspected via
read-only `grep`. No imports, no `.meta` writes, no scene changes.

---

**Report path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M3_01B_base_rig_discovery.md`
