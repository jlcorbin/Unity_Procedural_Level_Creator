# Player Asset Inventory — 2026-04-26

Read-only fact-finding pass for the upcoming Player Animator Controller and
`LevelGen.Player` script work. No assets were modified producing this report.

Character commitment: `Assets/AssetPacks/RPG Tiny Hero Duo/Prefab/MaleCharacterPBR.prefab`.
Stance commitment: SwordAndShield.
Locomotion commitment: `_InPlace` clips, script-driven (CharacterController).

All facts below were extracted from YAML / .meta files on disk. No editor
script was authored or run.

---

## 1. Pack folder structure

Root: `Assets/AssetPacks/RPG Tiny Hero Duo/`

| Folder | fbx | prefab | controller | mask | anim | mat | unity | png | Notes |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| `Animation/` | 38 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | All inside `SwordAndShield/` subtree |
| `Animator/`  | 0  | 0 | 3 | 1 | 0 | 0 | 0 | 0 | Three demo controllers + one mask |
| `Material/`  | 0  | 0 | 0 | 0 | 0 | 4 | 0 | 0 | PBR_Default, Polyart_Default, Skybox_Mat, Stage |
| `Mesh/`      | 11 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | Character + weapon FBX sources |
| `Prefab/`    | 0  | 12 | 0 | 0 | 0 | 0 | 0 | 0 | Char + weapon/shield variants |
| `Scene/`     | 0  | 0 | 0 | 0 | 0 | 0 | 4 | 0 | Demo scenes |
| `Texture/`   | 0  | 0 | 0 | 0 | 0 | 0 | 0 | 3 | Albedo, Emission, MetallicSmoothness |

### `Animation/SwordAndShield/`

- 13 `.fbx` clips at the top level (Idle, Attack01–04, Defend, DefendHit,
  Die, Dizzy, GetHit, GetUp, LevelUp, Victory)
- 1 `.mask` (`AvatarMask.mask`)
- `InPlace/` — 12 `.fbx` (4 directional moves × 1 sprint + 7 jumps)
- `RootMotion/` — 9 `.fbx` (4 directional moves × 1 sprint + 2 jumps + dupes)

No `Animation/` subfolders exist for other stances (TwoHand / Bow / etc.) —
SwordAndShield is the only stance the pack ships.

### `Animator/`

| File | GUID |
|---|---|
| `SwordAndShieldStance.controller` | `2be64f57d7d213648aa9b2e5e8e0a39b` |
| `AnimationLayer.controller`       | `ad202f9dbc4f60c49a5f7d1d0546d1ec` |
| `RootMotion.controller`           | `230b755b75de01e42872522beacd71ec` |
| `AnimLayer.mask`                  | (a layered-anim demo mask) |

### `Prefab/`

12 prefabs total — 4 character variants (Male/Female × PBR/Polyart) and
8 weapon/shield variants (OHS03/06 PBR/Polyart, Shield05/08 PBR/Polyart).

### `Scene/`

`PBRScene.unity`, `PolyartScene.unity`, `AnimationLayer.unity`, `RootMotion.unity`
— demo scenes for asset-store browsing.

---

## 2. MaleCharacterPBR prefab inspection

Source: [Prefab/MaleCharacterPBR.prefab](../Assets/AssetPacks/RPG%20Tiny%20Hero%20Duo/Prefab/MaleCharacterPBR.prefab)

**Prefab root GameObject name:** `MaleCharacterPBR`

**Animator component lives on the prefab root** (file ID 9036980633323257398
under the root GameObject 6086306807194253826 named `MaleCharacterPBR`).

### Animator settings (as shipped)

| Field | Value | Notes |
|---|---|---|
| `runtimeAnimatorController` | `SwordAndShieldStance.controller` | GUID match `2be64f57d7d213648aa9b2e5e8e0a39b` |
| `avatar` | GUID `0308cf4e83cf517488b60af58b290fe0` | Sub-asset of `Idle_Battle_SwordAndShiled.fbx` |
| `cullingMode` | `1` (CullUpdateTransforms) | |
| `updateMode` | `0` (Normal) | |
| `applyRootMotion` | `1` (true) | We will need to flip this off on the runtime prefab |
| `m_HasTransformHierarchy` | `1` | |
| `m_AllowConstantClipSamplingOptimization` | `1` | |
| `m_KeepAnimatorControllerStateOnDisable` | `0` | |

### Hierarchy (top level — children of prefab root)

```
MaleCharacterPBR  (root, has Animator)
├── Hair01            (MeshRenderer)
├── Hair06            (MeshRenderer)
├── Eye01             (MeshRenderer)
├── Eye02             (MeshRenderer)
├── Head01_Male       (MeshRenderer)
├── Head02_Female     (MeshRenderer — extra female head, present but presumably toggled)
├── Mouth01           (MeshRenderer)
├── Mouth02           (MeshRenderer)
├── Body05            (SkinnedMeshRenderer  — main body skinned mesh)
├── Cloak02
├── Cloak03           (SkinnedMeshRenderer  — skinned cloak)
├── Backpack01        (MeshRenderer)
└── root              (skeleton root — bones below)
    └── pelvis
        ├── thigh_l → calf_l → foot_l → ball_l
        ├── thigh_r → calf_r → foot_r → ball_r
        └── spine_01 → spine_02 → spine_03
            ├── neck_01 → head
            ├── clavicle_l → upperarm_l → lowerarm_l → hand_l
            │     ├── index_01_l → index_02_l → index_03_l
            │     ├── thumb_01_l → thumb_02_l → thumb_03_l
            │     └── weapon_l                ← SOCKET (extra bone)
            ├── clavicle_l/shoulderPadJoint_l
            ├── clavicle_r → upperarm_r → lowerarm_r → hand_r
            │     ├── index_01_r → index_02_r → index_03_r
            │     ├── thumb_01_r → thumb_02_r → thumb_03_r
            │     └── weapon_r                ← SOCKET (extra bone)
            ├── clavicle_r/shoulderPadJoint_r
            ├── CloakBone01 → CloakBone02 → CloakBone03
            └── BackpackBone
```

### SkinnedMeshRenderers

| GameObject | Notes |
|---|---|
| `Body05`  | Main body skinned mesh — primary character silhouette |
| `Cloak03` | Cloak skinned mesh — driven by `CloakBone01–03` |

Decorative parts (`Head01_Male`, `Hair01`, `Eye01`, `Mouth01`, `Backpack01`,
`Hair06`, `Eye02`, `Mouth02`, `Head02_Female`) are **MeshRenderers parented
to bones** (head, hand, etc.) — not skinned, just attached.

### Avatar / rig type

**Humanoid.** Confirmed via `Mesh/ModularCharacterPBR.fbx.meta`:
- `animationType: 3` (1=Legacy, 2=Generic, 3=Humanoid)
- `hasExtraRoot: 1`
- `lastHumanDescriptionAvatarSource` GUID = `0308cf4e83cf517488b60af58b290fe0`
  (the same Avatar wired into the prefab's Animator)

The Avatar lives as a sub-asset of `Idle_Battle_SwordAndShiled.fbx`, not on
the character mesh itself — this is the standard Unity humanoid pattern for
animation packs.

---

## 3. Bone names for hand sockets (and other useful targets)

Full transform paths from the prefab root (`MaleCharacterPBR/`):

| Target | Transform path | HumanBodyBones mapping |
|---|---|---|
| Hips / pelvis | `root/pelvis` | `HumanBodyBones.Hips` |
| Spine base    | `root/pelvis/spine_01` | `HumanBodyBones.Spine` |
| Chest         | `root/pelvis/spine_01/spine_02` | `HumanBodyBones.Chest` |
| UpperChest    | `root/pelvis/spine_01/spine_02/spine_03` | `HumanBodyBones.UpperChest` |
| Neck          | `root/pelvis/spine_01/spine_02/spine_03/neck_01` | `HumanBodyBones.Neck` |
| **Head**      | `root/pelvis/spine_01/spine_02/spine_03/neck_01/head` | `HumanBodyBones.Head` |
| LeftShoulder  | `…/spine_03/clavicle_l` | `HumanBodyBones.LeftShoulder` |
| **LeftHand**  | `…/spine_03/clavicle_l/upperarm_l/lowerarm_l/hand_l` | `HumanBodyBones.LeftHand` |
| RightShoulder | `…/spine_03/clavicle_r` | `HumanBodyBones.RightShoulder` |
| **RightHand** | `…/spine_03/clavicle_r/upperarm_r/lowerarm_r/hand_r` | `HumanBodyBones.RightHand` |

### Weapon sockets (extra bones — NOT in HumanBodyBones)

| Socket | Path | Local position vs hand | Local rotation |
|---|---|---|---|
| Left  weapon socket | `.../hand_l/weapon_l` | `(-0.094, 0.007, 0.004)` | `(-0.7071, 0, 0, -0.7071)` ≈ X−90° |
| Right weapon socket | `.../hand_r/weapon_r` | `(0.094, -0.0073, -0.0036)` | `(0.7071, 0, 0, 0.7071)` ≈ X+90° |

Because these are **extra bones outside the humanoid skeleton**, they cannot
be retrieved via `animator.GetBoneTransform(HumanBodyBones.X)`. The
attachment options are:

1. `transform.Find("root/pelvis/.../hand_r/weapon_r")` (path lookup)
2. Recursive name search (`Transform.FindDeepChild` style helper)
3. Cache references in a `MonoBehaviour` populated in OnValidate / inspector

The rig's `humanDescription` block does not flag these bones as
`extraExposedTransformPaths` — they're just regular transforms in the
hierarchy, accessible by name. Neither `weapon_l` nor `weapon_r` appears in
the humanoid `human` mapping list, confirming they are pure socket bones.

---

## 4. Existing controllers — parameters and structure

### `SwordAndShieldStance.controller` (the one wired into the prefab)

| Field | Value |
|---|---|
| Layer count | 1 |
| Layer name  | `Base Layer` |
| Layer mask  | (none — `m_Mask: {fileID: 0}`) |
| **Parameters** | **NONE** (`m_AnimatorParameters: []`) |
| State count    | 24 |
| Default state  | `Idle_Battle_SwordAndShield` |

States in this layer:

| Name | Source clip GUID | Notes |
|---|---|---|
| Idle_Battle_SwordAndShield | `0308cf4e…` | **default state**; misspelled FBX, corrected clip name |
| Idle_Battle_SwordAndShield 0 | `0308cf4e…` | **duplicate** — same clip, name auto-suffixed " 0" |
| Idle_Normal_SwordAndShield | `423aabfe…` | |
| Attack01_SwordAndShiled | `db509ad7…` | misspelled (matches FBX + clip-internal name) |
| Attack02_SwordAndShiled | `8283fadf…` | misspelled |
| Attack03_SwordAndShiled | `9a6c3585…` | misspelled |
| Attack04_SwordAndShiled | `b267a2c2…` | misspelled |
| Defend_SwordAndShield | `61f2c64d…` | |
| DefendHit_SwordAndShield | `655c8542…` | |
| GetHit01_SwordAndShield | `c98546b8…` | |
| Dizzy_SwordAndShield | `065be557…` | |
| Die01_SwordAndShield | `5940bb0b…` | |
| Die01_Stay_SwordAndShield | `0e112f15…` | |
| GetUp_SwordAndShield | `4e1dc6b0…` | |
| MoveFWD_Normal_InPlace_SwordAndShield | `0791e523…` | |
| MoveFWD_Battle_InPlace_SwordAndShield | `7d4f9e9d…` | |
| MoveBWD_Battle_InPlace_SwordAndShield | `4897d9e1…` | |
| MoveLFT_Battle_InPlace_SwordAndShield | `048a5415…` | |
| MoveRGT_Battle_InPlace_SwordAndShield | `f531fd2d…` | |
| SprintFWD_Battle_InPlace_SwordAndShield | `5eee3d6d…` | |
| JumpFull_Normal_RM_SwordAndShield | `dd2792b7…` | RM clip — note _RM_ in name |
| JumpFull_Spin_RM_SwordAndShield | `7d1e5bb9…` | RM clip — note _RM_ in name |
| LevelUp_Battle_SwordAndShield | `7125eebf…` | |
| Victory_Battle_SwordAndShield | `03bef14f…` | terminal — `m_Transitions: []` |

Transition gating (sampled across 10 transitions): every transition has
`m_Conditions: []` and `m_HasExitTime: 1`. **There is not a single
parameter-driven transition in the controller.** It is a pure exit-time
demo loop — Idle → Attack01 → … → Attack04 → Defend → … → Victory, then
back to Idle via long exit times.

### `AnimationLayer.controller`

| Field | Value |
|---|---|
| Layer count | 2 |
| Layer 0 name | `Base Layer` |
| Layer 0 mask | none |
| Layer 0 states | 1 — `MoveFWD_Battle_InPlace_SwordAndShield` (default) |
| Layer 1 name | `New Layer` |
| Layer 1 mask | `acaf52b6…` (`AnimLayer.mask`) |
| Layer 1 states | 1 — `Attack01_SwordAndShiled` (default) |
| **Parameters** | **NONE** |

A trivial demo of layered animation — overlays an attack on top of a walk
loop using a body-part mask. No parameters, no transitions.

### `RootMotion.controller` — out of scope

Per the prompt this controller was not asked about. Mentioned here only to
note that it exists as a demo of the `_RM_` clips driving Stage geometry.

---

## 5. Animation clip inventory

All clips are **inside FBX files as sub-assets** (no standalone `.anim`
files exist anywhere in the pack). The single `.mask` file
(`AvatarMask.mask`) is referenced via the per-clip `maskSource` field.

Clip durations below are **frame counts from the FBX import metadata**
(`firstFrame` / `lastFrame`). The FBX source frame rate is not exposed in
`.meta`; assuming the typical 30 fps source for retargeted humanoid packs,
multiply frames by 1/30 for seconds — but **this is unverified without
running an editor script**. See Findings §3.

Root-motion column reflects **filename convention** (`_InPlace` vs `_RM`),
not curve introspection. All clips have `keepOriginalPositionXZ: 1` set
identically — that flag preserves the source XZ in the imported clip but
does not by itself say whether the source had any.

| Category | Clip name | FBX folder | Frames | Looping (`loopTime`) | Root motion (by name) |
|---|---|---|---:|---|---|
| Idle | Idle_Battle_SwordAndShield | `SwordAndShield/` | 20 | yes | no (in-place) |
| Idle | Idle_Normal_SwordAndShield | `SwordAndShield/` | 140 | yes | no (in-place) |
| Move (InPlace) | MoveFWD_Battle_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | **16** | **yes** | **InPlace** |
| Move (InPlace) | MoveBWD_Battle_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | **16** | **yes** | **InPlace** |
| Move (InPlace) | MoveLFT_Battle_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | **16** | **yes** | **InPlace** |
| Move (InPlace) | MoveRGT_Battle_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | **16** | **yes** | **InPlace** |
| Move (InPlace) | MoveFWD_Normal_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 16 | yes | InPlace — non-Battle FWD only; no BWD/LFT/RGT |
| Move (InPlace) | SprintFWD_Battle_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 12 | yes | InPlace |
| Move (RM) | MoveFWD_Battle_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 16 | yes | RM |
| Move (RM) | MoveBWD_Battle_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 16 | yes | RM |
| Move (RM) | MoveLFT_Battle_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 16 | yes | RM |
| Move (RM) | MoveRGT_Battle_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 16 | yes | RM |
| Move (RM) | MoveFWD_Normal_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 16 | yes | RM |
| Move (RM) | SprintFWD_Battle_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 12 | yes | RM |
| Attack | Attack01_SwordAndShiled | `SwordAndShield/` | 16 | yes (loops while held? — single-shot) | n/a |
| Attack | Attack02_SwordAndShiled | `SwordAndShield/` | 16 | yes | |
| Attack | Attack03_SwordAndShiled | `SwordAndShield/` | 16 | yes | |
| Attack | Attack04_SwordAndShiled | `SwordAndShield/` | 16 | yes | |
| Attack | Attack04_Spinning_SwordAndShield | `SwordAndShield/` | 10 | yes | continuous spin |
| Attack | Attack04_Start_SwordAndShield | `SwordAndShield/` | 6  | **no** | wind-up |
| Defend | Defend_SwordAndShield | `SwordAndShield/` | 20 | yes | (block hold) |
| Defend | DefendHit_SwordAndShield | `SwordAndShield/` | 10 | yes | (impact reaction while blocking) |
| GetHit | GetHit01_SwordAndShield | `SwordAndShield/` | 14 | yes | |
| Stun | Dizzy_SwordAndShield | `SwordAndShield/` | 40 | yes | |
| Death | Die01_SwordAndShield | `SwordAndShield/` | 15 | **no** | |
| Death | Die01_Stay_SwordAndShield | `SwordAndShield/` | 20 | yes | (collapsed-on-floor pose) |
| Death | GetUp_SwordAndShield | `SwordAndShield/` | 25 | **no** | |
| Jump (InPlace) | JumpAir_Double_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 15 | **no** | InPlace |
| Jump (InPlace) | JumpAir_Normal_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 15 | yes | InPlace |
| Jump (InPlace) | JumpAir_Spin_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 15 | **no** | InPlace |
| Jump (InPlace) | JumpEnd_Normal_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 12 | **no** | InPlace |
| Jump (InPlace) | JumpFull_Normal_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 24 | yes | InPlace |
| Jump (InPlace) | JumpFull_Spin_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 24 | yes | InPlace |
| Jump (InPlace) | JumpStart_Normal_InPlace_SwordAndShield | `SwordAndShield/InPlace/` | 8  | **no** | InPlace |
| Jump (RM) | JumpFull_Normal_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 24 | yes | RM |
| Jump (RM) | JumpFull_Spin_RM_SwordAndShield | `SwordAndShield/RootMotion/` | 24 | yes | RM |
| Special | LevelUp_Battle_SwordAndShield | `SwordAndShield/` | 70 | yes | |
| Special | Victory_Battle_SwordAndShield | `SwordAndShield/` | 50 | yes | |

**Total clip count: 38** (matches the 38 `.fbx` count from §1).

### Locomotion blend-tree fitness

The four directional **InPlace Battle** walks needed for a 2D blend tree are
all present and **all exactly 16 frames long**:

- `MoveFWD_Battle_InPlace_SwordAndShield` — 16f
- `MoveBWD_Battle_InPlace_SwordAndShield` — 16f
- `MoveLFT_Battle_InPlace_SwordAndShield` — 16f
- `MoveRGT_Battle_InPlace_SwordAndShield` — 16f

Identical lengths → no per-clip `state.speed` tuning needed inside the blend
tree to keep them in cycle-phase. Sprint (12 frames) is a separate state,
not a blend-tree node — it would need its own state and speed match.

`MoveFWD_Normal_InPlace_SwordAndShield` is a non-Battle stance variant of
forward walk. **No corresponding BWD/LFT/RGT Normal-stance clips exist** —
the non-Battle variant of locomotion is forward-only in this pack.

---

## 6. Existing scripts in pack

Search: `Assets/AssetPacks/RPG Tiny Hero Duo/**/*.cs`

**Result: zero `.cs` files anywhere in the pack.**

The pack ships meshes, prefabs, materials, controllers, masks, textures and
demo scenes only. No demo scripts, no character controllers, no example
camera rigs. Nothing to collide with `LevelGen.Player` and nothing to
delete or ignore.

---

## 7. `LevelGen.Player` greenfield confirmation

| Search | Result |
|---|---|
| Files in `Assets/Scripts/Player/` | none (folder does not exist) |
| Files containing `namespace LevelGen.Player` | none |
| Files containing `namespace LevelGen.Player.Editor` | none |

Player namespace is fully greenfield, matching CLAUDE.md's assertion.

---

## 8. `InputSystem_Actions` confirmation

Source: [InputSystem_Actions.inputactions](../Assets/InputSystem_Actions.inputactions)

### Action maps

| Map name | Actions |
|---|---|
| `Player` | Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, **Sprint** |
| `UI`     | Navigate, Submit, Cancel, Point, Click, RightClick, MiddleClick, ScrollWheel, TrackedDevicePosition, TrackedDeviceOrientation |

### Player action types and bindings (summary)

| Action | Type | Notable bindings |
|---|---|---|
| Move | Value (Vector2) | Gamepad leftStick, WASD/Arrow composite, Joystick stick, XR Primary2DAxis |
| Look | Value (Vector2) | Gamepad rightStick, Pointer/delta, Joystick hatswitch |
| Attack | Button | Mouse leftButton, Gamepad buttonWest, Keyboard enter, Touchscreen tap, XR PrimaryAction, Joystick trigger |
| Interact | Button (**Hold**) | Keyboard E, Gamepad buttonNorth |
| Crouch | Button | Keyboard C, Gamepad buttonEast |
| Jump | Button | Keyboard space, Gamepad buttonSouth, XR secondaryButton |
| Previous | Button | Keyboard 1, Gamepad dpad/left |
| Next | Button | Keyboard 2, Gamepad dpad/right |
| Sprint | Button | **Keyboard leftShift, Gamepad leftStickPress, XR trigger** |

Control schemes: `Keyboard&Mouse`, `Gamepad`, `Touch`, `Joystick`, `XR`.

### Generated C# wrapper class

`InputSystem_Actions.inputactions.meta` reports:

```yaml
ScriptedImporter:
  ...
  generateWrapperCode: 0
  wrapperCodePath:
  wrapperClassName:
  wrapperCodeNamespace:
```

**No C# wrapper class is generated.** No `Assets/InputSystem_Actions.cs`
exists alongside the asset. Wiring will need to choose one of:

1. Flip `generateWrapperCode` to `1` and use the auto-generated
   `InputSystem_Actions` C# class (code-only path)
2. Use the `PlayerInput` MonoBehaviour and wire actions via inspector +
   `SendMessage` / `UnityEvent` callbacks

Choice deferred per prompt scope.

---

## Findings & Open Questions

1. **Naming inconsistency: `_SwordAndShiled` vs `_SwordAndShield`.**
   The misspelled form appears in five of six FBX filenames in the
   `SwordAndShield/` root folder (Idle_Battle, Attack01–04). Inside Unity:
   - `Idle_Battle_SwordAndShiled.fbx` has its **clip name** corrected at
     import to `Idle_Battle_SwordAndShield` (right spelling).
   - `Attack01–04_SwordAndShiled.fbx` keep the **misspelling in the clip
     name as well**.
   The `SwordAndShieldStance.controller` reflects this — its Attack states
   are named `Attack01–04_SwordAndShiled`. Animator Override Controllers
   match by source-clip reference, not by name, so this only matters when
   we look clips up by string. Open question: do we rename the misspelled
   clips (via the `.fbx.meta` `name:` override) before authoring the base
   controller, or live with the asymmetry?

2. **All four directional InPlace Battle walks are uniform 16 frames.**
   Locomotion blend tree drops in clean — no length tuning needed. ✓

3. **Frame-rate-to-seconds conversion is unverifiable from disk alone.**
   The FBX source FPS is binary-only. Assuming Mixamo/Unreal-style 30 fps
   gives 16 frames ≈ 0.533s for a walk cycle, which matches typical
   character-walk feel — but this is an assumption. If the next prompt
   needs exact `AnimationClip.length` values for Animator transition
   tuning, it should run a one-off editor script to introspect each clip.

4. **The shipped `SwordAndShieldStance.controller` is a demo loop, not a
   gameplay controller.** Zero parameters, every transition is exit-time
   only. It tells us nothing about how to drive states from input — the
   base controller will be authored from scratch. The shipped controller
   is still **useful** as a sanity-check Animator (drop it on the prefab
   in the editor to confirm the rig and clips work) and as a one-stop
   reference for all available state names.

5. **Rig is Humanoid (`animationType: 3`).** `animator.GetBoneTransform
   (HumanBodyBones.RightHand)` etc. work for hand/head/hip access. No
   string-path lookup needed for those. ✓

6. **`weapon_l` and `weapon_r` are extra non-humanoid bones** parented to
   `hand_l` and `hand_r` and pre-positioned/rotated for correct sword
   alignment. They are **not** in `HumanBodyBones`. Weapon-attach helper
   must use `transform.Find("...weapon_r")` or recursive name search.
   Local positions are mirrored: `weapon_l` at `(-0.094, 0.007, 0.004)`,
   `weapon_r` at `(0.094, -0.0073, -0.0036)` — the slight asymmetry on the
   right hand suggests the FBX author posed the right hand once and mirrored
   it imperfectly. Cosmetically unimportant.

7. **The shipped Animator has `applyRootMotion: 1`.** Since we're
   committing to script-driven CharacterController locomotion, the runtime
   player prefab will need this set to `0`. Worth flagging in the next
   prompt's design pass.

8. **Avatar source is `Idle_Battle_SwordAndShiled.fbx`, not the character
   mesh FBX.** All other clips retarget onto this Avatar. This is standard
   Mixamo/Unreal-pack practice and not a problem — but it does mean if
   that Avatar's import settings change, every retargeted clip's playback
   shifts. Treat it as load-bearing.

9. **`InputSystem_Actions` includes a `Sprint` action that CLAUDE.md /
   prompt §8 did not list.** The prompt expected eight actions
   (Move/Look/Attack/Interact/Crouch/Jump/Previous/Next); the asset has
   nine (the above plus `Sprint`). Convenient — the pack ships
   `SprintFWD_Battle_InPlace` and we already have an input hook for it.
   No action needed; just noting the asset is one ahead of the brief.

10. **The shipped `SwordAndShieldStance.controller` contains a
    duplicate state.** `Idle_Battle_SwordAndShield` and
    `Idle_Battle_SwordAndShield 0` both reference the same clip — Unity's
    auto-suffix on a copy-paste collision. Confirms the shipped
    controller was hand-authored quickly for demo purposes and not curated
    for production.

11. **`Animation/SwordAndShield/AvatarMask.mask` exists but is not
    referenced by `SwordAndShieldStance.controller`.** It IS used as the
    `maskSource` per imported clip's `transformMask` — so it's a body-part
    weighting mask applied at clip-import time, not a layer mask. The
    layer mask `Animator/AnimLayer.mask` is a separate asset used only by
    `AnimationLayer.controller`'s second layer.

### No blockers

Nothing in this inventory prevents the next prompt from proceeding to
behavior-table + base-controller design. The character is Humanoid, the
locomotion clips are uniform-length and complete, the Player namespace is
empty, and the input asset already exposes everything we need plus Sprint.
The only authoring decisions still pending are:

- Whether to fix the `_SwordAndShiled` clip-name typo (Findings §1)
- How to wire input — generated C# class vs `PlayerInput` MonoBehaviour
  (§8)
- How exactly to access weapon sockets (string-path vs cached refs) (§6)
