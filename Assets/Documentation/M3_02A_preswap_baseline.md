# M3-02A — Pre-Swap Baseline (Animator references)

**Date:** 2026-04-30
**Scope:** Snapshot of the 13 currently-resolved clip references in
`PlayerBaseController.controller` and `PlayerOverride_MaleHero.overrideController`
captured **before** the Duo→World Bundle pack swap. Used by the
post-swap verification step to confirm Unity's GUID-based reference
resolver re-pointed all 13 clips to the new pack at their new paths
without needing manual re-wiring.

**Source files (current state):**
- `Assets/Animators/Player/PlayerBaseController.controller`
- `Assets/Animators/Player/PlayerOverride_MaleHero.overrideController`
- All clips currently resolve to FBXes under
  `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/`
  and `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/`.

---

## 13 clip GUIDs in use

| # | Clip name | GUID | Pre-swap path |
|---|---|---|---|
| 1 | Idle_Battle_SwordAndShiled | `0308cf4e83cf517488b60af58b290fe0` | `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx` |
| 2 | MoveBWD_Battle_InPlace_SwordAndShield | `4897d9e1e93439744a78d1cebdef17ff` | `Assets/AssetPacks/RPG Tiny Hero Duo/Animation/SwordAndShield/InPlace/MoveBWD_Battle_InPlace_SwordAndShield.fbx` |
| 3 | MoveFWD_Battle_InPlace_SwordAndShield | `7d4f9e9da55a3bd4f958a63308a522a1` | `…/InPlace/MoveFWD_Battle_InPlace_SwordAndShield.fbx` |
| 4 | MoveLFT_Battle_InPlace_SwordAndShield | `048a541568c52514c9996fea7b37d6e0` | `…/InPlace/MoveLFT_Battle_InPlace_SwordAndShield.fbx` |
| 5 | SprintFWD_Battle_InPlace_SwordAndShield | `5eee3d6dbfbcef04ab20b548575d7b9d` | `…/InPlace/SprintFWD_Battle_InPlace_SwordAndShield.fbx` |
| 6 | MoveRGT_Battle_InPlace_SwordAndShield | `f531fd2d5a6a8a440b5d450e029c4041` | `…/InPlace/MoveRGT_Battle_InPlace_SwordAndShield.fbx` |
| 7 | Attack01_SwordAndShiled | `db509ad77f9b4f84a8eb1989f589b24c` | `…/Animation/SwordAndShield/Attack01_SwordAndShiled.fbx` |
| 8 | GetHit01_SwordAndShield | `c98546b8d8d3ab046afc8acfe706361f` | `…/Animation/SwordAndShield/GetHit01_SwordAndShield.fbx` |
| 9 | JumpStart_Normal_InPlace_SwordAndShield | `c2b2e4c79d87c3045838cbc5935d8a98` | `…/InPlace/JumpStart_Normal_InPlace_SwordAndShield.fbx` |
| 10 | JumpAir_Normal_InPlace_SwordAndShield | `8be8f9bf3f16f184fb9719bd233874e6` | `…/InPlace/JumpAir_Normal_InPlace_SwordAndShield.fbx` |
| 11 | JumpEnd_Normal_InPlace_SwordAndShield | `8b662f6fbb996ba429182e54857361d3` | `…/InPlace/JumpEnd_Normal_InPlace_SwordAndShield.fbx` |
| 12 | Attack02_SwordAndShiled | `8283fadf2c89507469495f30db8680db` | `…/Animation/SwordAndShield/Attack02_SwordAndShiled.fbx` |
| 13 | Attack03_SwordAndShiled | `9a6c3585df66f2e4782635fc7a23494c` | `…/Animation/SwordAndShield/Attack03_SwordAndShiled.fbx` |

---

## Override controller m_Clips slots (verbatim)

`PlayerOverride_MaleHero.overrideController` has 13 self-mapped slots
(`m_OriginalClip == m_OverrideClip`):

```
m_Clips:
- {0308cf4e83cf517488b60af58b290fe0}   Idle
- {4897d9e1e93439744a78d1cebdef17ff}   MoveBWD
- {7d4f9e9da55a3bd4f958a63308a522a1}   MoveFWD
- {048a541568c52514c9996fea7b37d6e0}   MoveLFT
- {5eee3d6dbfbcef04ab20b548575d7b9d}   SprintFWD
- {f531fd2d5a6a8a440b5d450e029c4041}   MoveRGT (note: fileID -1574218436586762272, distinct from others)
- {db509ad77f9b4f84a8eb1989f589b24c}   Attack01
- {c98546b8d8d3ab046afc8acfe706361f}   GetHit01
- {c2b2e4c79d87c3045838cbc5935d8a98}   JumpStart
- {8be8f9bf3f16f184fb9719bd233874e6}   JumpAir
- {8b662f6fbb996ba429182e54857361d3}   JumpEnd
- {8283fadf2c89507469495f30db8680db}   Attack02
- {9a6c3585df66f2e4782635fc7a23494c}   Attack03
```

Override controller's `m_Controller` field references:
`{guid: 4a9428446a7871a4e96cad3e19143a10, type: 2}` →
`Assets/Animators/Player/PlayerBaseController.controller`. This is OUR
controller (project asset) and is unaffected by the pack swap.

---

## Base controller state motion references

`PlayerBaseController.controller` references the same 13 clips via state
`m_Motion` fields. Plus 1 BlendTree internal reference (Locomotion blend
tree's `m_Motion: {fileID: -8223520063775168120}` — internal, not a clip
GUID, and unaffected by the swap).

State → motion GUID:

| State | Motion GUID | Clip name |
|---|---|---|
| Idle | `0308cf4e83cf517488b60af58b290fe0` | Idle_Battle_SwordAndShiled |
| Sprint | `5eee3d6dbfbcef04ab20b548575d7b9d` | SprintFWD_Battle_InPlace_SwordAndShield |
| Attack | `db509ad77f9b4f84a8eb1989f589b24c` | Attack01_SwordAndShiled |
| Hit | `c98546b8d8d3ab046afc8acfe706361f` | GetHit01_SwordAndShield |
| JumpStart | `c2b2e4c79d87c3045838cbc5935d8a98` | JumpStart_Normal_InPlace_SwordAndShield |
| JumpAir | `8be8f9bf3f16f184fb9719bd233874e6` | JumpAir_Normal_InPlace_SwordAndShield |
| JumpEnd | `8b662f6fbb996ba429182e54857361d3` | JumpEnd_Normal_InPlace_SwordAndShield |
| Attack02 | `8283fadf2c89507469495f30db8680db` | Attack02_SwordAndShiled |
| Attack03 | `9a6c3585df66f2e4782635fc7a23494c` | Attack03_SwordAndShiled |
| Locomotion blend tree | (multiple, internal) | MoveFWD/BWD/LFT/RGT |

---

## Verification target

After the swap, every one of the 13 GUIDs above MUST resolve to a
non-null clip whose path is now under
`Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/`
(or `…/InPlace/`).

If any clip resolves to null, OR resolves to a path still under
`Assets/AssetPacks/RPG Tiny Hero Duo/` (which shouldn't exist
post-swap), OR resolves to an unexpected GUID — that's a swap
failure and prompt 2A must abort.

Per M3-01 Section E, all 13 clip GUIDs are byte-identical between the
two packs, so the auto-relink should be 100% successful.

---

**File path:** `e:\Unity\Unity_Procedural_Level_Creator\Assets\Documentation\M3_02A_preswap_baseline.md`
