# M3-02A Step 3 — Post-Swap Verification

**Date:** 2026-04-30 19:55
**Target pack root:** `Assets/AssetPacks/RPG Tiny Hero World Bundle/`

PASS criteria: GUID resolves to a non-null AnimationClip whose path is inside the new pack root.
Clip-name mismatches are INFORMATIONAL only (publisher renamed clip-subasset internally without changing FBX filename or GUID — runtime resolves by GUID, not by name).

## Auto-relink results — 13 currently-wired clips

| GUID | Expected name | Resolved name | Resolved path | Verdict |
|---|---|---|---|---|
| `0308cf4e83cf517488b60af58b290fe0` | Idle_Battle_SwordAndShiled | Idle_Battle_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx | **PASS** (clip name differs — cosmetic) |
| `4897d9e1e93439744a78d1cebdef17ff` | MoveBWD_Battle_InPlace_SwordAndShield | MoveBWD_Battle_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveBWD_Battle_InPlace_SwordAndShield.fbx | **PASS** |
| `7d4f9e9da55a3bd4f958a63308a522a1` | MoveFWD_Battle_InPlace_SwordAndShield | MoveFWD_Battle_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveFWD_Battle_InPlace_SwordAndShield.fbx | **PASS** |
| `048a541568c52514c9996fea7b37d6e0` | MoveLFT_Battle_InPlace_SwordAndShield | MoveLFT_Battle_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveLFT_Battle_InPlace_SwordAndShield.fbx | **PASS** |
| `5eee3d6dbfbcef04ab20b548575d7b9d` | SprintFWD_Battle_InPlace_SwordAndShield | SprintFWD_Battle_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/SprintFWD_Battle_InPlace_SwordAndShield.fbx | **PASS** |
| `f531fd2d5a6a8a440b5d450e029c4041` | MoveRGT_Battle_InPlace_SwordAndShield | MoveRGT_Battle_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/MoveRGT_Battle_InPlace_SwordAndShield.fbx | **PASS** |
| `db509ad77f9b4f84a8eb1989f589b24c` | Attack01_SwordAndShiled | Attack01_SwordAndShiled | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Attack01_SwordAndShiled.fbx | **PASS** |
| `c98546b8d8d3ab046afc8acfe706361f` | GetHit01_SwordAndShield | GetHit01_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/GetHit01_SwordAndShield.fbx | **PASS** |
| `c2b2e4c79d87c3045838cbc5935d8a98` | JumpStart_Normal_InPlace_SwordAndShield | JumpStart_Normal_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/JumpStart_Normal_InPlace_SwordAndShield.fbx | **PASS** |
| `8be8f9bf3f16f184fb9719bd233874e6` | JumpAir_Normal_InPlace_SwordAndShield | JumpAir_Normal_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/JumpAir_Normal_InPlace_SwordAndShield.fbx | **PASS** |
| `8b662f6fbb996ba429182e54857361d3` | JumpEnd_Normal_InPlace_SwordAndShield | JumpEnd_Normal_InPlace_SwordAndShield | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/InPlace/JumpEnd_Normal_InPlace_SwordAndShield.fbx | **PASS** |
| `8283fadf2c89507469495f30db8680db` | Attack02_SwordAndShiled | Attack02_SwordAndShiled | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Attack02_SwordAndShiled.fbx | **PASS** |
| `9a6c3585df66f2e4782635fc7a23494c` | Attack03_SwordAndShiled | Attack03_SwordAndShiled | Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield/Attack03_SwordAndShiled.fbx | **PASS** |

**Auto-relink summary: 13 PASS / 0 FAIL / 1 cosmetic name-mismatch warnings** (13 expected total).

## M2-B Validator results

Each menu item below was invoked via EditorApplication.ExecuteMenuItem.
Validator output is in the Console; this report only captures invocation success.

- ✓ Invoked: `LevelGen/Player/Validate Combat Animator (M2-B Step 2)`
- ✓ Invoked: `LevelGen/Player/Validate PlayerCombat Wiring (M2-B Step 3)`
- ✓ Invoked: `LevelGen/Player/Validate Jump Animator (M2-B Step 4)`
- ✓ Invoked: `LevelGen/Player/Validate Jump Runtime (M2-B Step 5)`
- ✓ Invoked: `LevelGen/Player/Validate Combo Animator (M2-B Step 6)`
- ✓ Invoked: `LevelGen/Player/Validate Combo Runtime (M2-B Step 7)`

**Validators invoked: 6 / 6.** Validator-internal PASS/FAIL counts are in the Console — review individually.

---

**Next step:** M3-02B — replace the broken `MaleCharacterPBR` PrefabInstance under `Player_MaleHero.prefab` with a chosen MC* prefab from the new pack.
