# Player Character — Mechanics & Input Spec (engine-agnostic)

**Purpose:** a complete behavioral spec of the current UE5 Player character so it can be rebuilt in Unity with identical actions, inputs, and feel. This describes **what the character does and the values that drive it** — not UE Blueprint wiring. The Unity project already has the same skeletons, meshes, and animation clips, so asset names are given for direct mapping.

Scope: **Player character only** (`BP_RPG_PlayerCharacter`). All values pulled live from the project on 2026-07-18.

---

## 0. Core design pillars

1. **Camera-relative STRAFE character.** The character never turns to face its movement direction. It always faces where the **camera** faces, and strafes W/A/S/D relative to the camera. (Think Souls-with-lock-on / modern third-person shooter locomotion.)
2. **Over-the-shoulder camera, permanent** for all stances.
3. **8 weapon stances**, cycled with one key. Each stance has its **own** locomotion, attack, and dodge animation sets, and its own equipped weapon mesh(es).
4. **Melee stances (0–5): light/heavy combo.** **Ranged stances (6–7): charge-and-release shot.**
5. **Soft target-lock** and **4-direction dodge roll** layer on top and work in every stance.

---

## 1. Body / collision

| Property | Value |
|---|---|
| Capsule half-height | 88 |
| Capsule radius | 34 |
| Skeletal mesh | `OneMeshCharacter01_SK` (skeleton `OneMeshCharacter01_Skeleton`) |
| Hand attach sockets | `Weapon_R` (right hand), `Weapon_L` (left hand); also `Head`, `BackPack` |

---

## 2. Camera rig

A spring-arm (boom) + follow camera. Over-the-shoulder is a **fixed offset for every stance** (no aim-down-sights toggle).

| Property | Value | Meaning |
|---|---|---|
| Boom length | 350 | distance behind character |
| Boom socket offset | (X 0, **Y 60**, **Z 25**) | 60 units right, 25 up → over right shoulder |
| Camera lag | enabled, speed **15** | smooth follow |
| Boom collision test | on | pulls camera in when blocked by geometry |
| Boom uses control rotation | **yes** | boom rotates with mouse look |
| Camera FOV | 90 | |
| Camera uses control rotation | no | only the boom rotates; camera inherits |

**Unity:** camera pivot that follows the player with position smoothing (lag), a spring-arm raycast for wall collision, rotated by mouse look (yaw + clamped pitch), offset right & up. Tuning knobs: Y = shoulder side, Z = height, boom length = tightness.

---

## 3. Movement model — the key principle

The character orients to the **controller's desired yaw** (camera yaw), **not** to its velocity. So it always faces camera-forward and slides in the input direction.

| Setting | Value | Effect |
|---|---|---|
| Orient rotation to movement | **false** | does NOT face its move direction |
| Use controller **desired** rotation | **true** | smoothly faces camera-forward |
| Rotation rate (yaw) | **500°/s** | how fast it turns to face the camera |
| Pawn "use controller rotation yaw/pitch/roll" | all false | body turn is done by the movement component, not hard-snapped |
| Max walk speed | 600 |
| Max acceleration | 2048 |
| Braking deceleration | 2048 |
| Ground friction | 8 |
| Gravity scale | 1.0 |
| Jump velocity (Z) | 420 |
| Air control | 0.05 |

**Move input** = WASD → 2D vector, rotated by camera yaw → world-space move direction (camera-relative). **Look** = mouse delta → controller yaw/pitch.

**Unity:** `CharacterController` (or Rigidbody). Each frame: build move dir from camera yaw + WASD; rotate the character transform toward camera yaw at 500°/s; move at up to 600 u/s with the accel/braking above. Then feed the animator:
- `Speed` = horizontal velocity magnitude (0–600)
- `Direction` = signed angle between velocity and facing (−180..180)

Because the body faces the camera, walking left yields Direction ≈ −90 → left-strafe clip, walking back ≈ ±180 → backpedal, etc.

---

## 4. Locomotion animation (2D blend tree, per stance)

Each stance has a 2D blendspace: **X = Direction (−180..180), Y = Speed (0..600)**, with 7 samples. All are **in-place** clips (locomotion movement is code-driven, not root motion):

| Sample | Direction | Speed |
|---|---|---|
| Idle | 0 | 0 |
| Move Forward | 0 | 300 |
| Move Backward | ±180 | 300 |
| Move Right | 90 | 300 |
| Move Left | −90 | 300 |
| Sprint Forward | 0 | 600 |

There are **8 blendspaces** (`BS_RPG_Loco_<Stance>`), selected by the `CurrentStance` int (a "blend poses by int" node). **Unity:** one blend tree per stance inside a locomotion sub-state, switched by an int parameter — or a single blend tree whose clips you swap by stance.

**Clip naming quirks to watch (pack-specific):** TwoHandsSword clips use suffix `THS` and its Move clips drop "Battle" (e.g. `MoveFWD_InPlace_THS`); Spear Move clips also drop "Battle"; SwordAndShield idle is a pack typo `Idle_Battle_SwordAndShiled_Anim`.

---

## 5. Input map (complete)

All actions are Enhanced Input actions bound in one mapping context. Bool actions fire on press by default; **Attack also uses release** (see ranged).

| Action | Key | Value | Behavior |
|---|---|---|---|
| Move | **W / A / S / D** | Vector2D | camera-relative movement |
| Look | **Mouse** (Mouse2D) | Vector2D | yaw + pitch |
| Jump | **Space** | bool | jump |
| Dodge | **Left Ctrl** | bool | directional dodge roll |
| Attack | **Left Mouse** | bool | melee: combo tap; ranged: hold=charge, release=fire |
| Switch Stance | **Q** | bool | cycle stance (+1) |
| Target Lock | **Tab** | bool | toggle soft lock |

**Unity:** map 1:1 with the Input System, same keys. Note the Attack action needs **both** a "started/pressed" and a "canceled/released" callback (ranged charge/release).

---

## 6. Stance system (8 stances)

`CurrentStance` is an **int 0–7** (default **1 = SingleSword**). **Q** cycles `(CurrentStance + 1) % 8`. On every switch (and once on spawn) an **ApplyStance** routine runs and does all of:
- set right-hand weapon mesh from `StanceRightMeshes[stance]`, left-hand from `StanceLeftMeshes[stance]`,
- apply per-hand attach **rotation** from `StanceRightRotations` / `StanceLeftRotations`,
- switch the locomotion blendspace (§4),
- show the bow skeletal mesh only in stance 7,
- show the ranged crosshair only in ranged stances (§8).

| # | Stance | Right hand | Left hand | Ranged? |
|---|---|---|---|---|
| 0 | NoWeapon | — | — | no |
| 1 | SingleSword | `OHS03_Sword_SM` | — | no |
| 2 | TwoHandsSword | `THS01_Sword_SM` | — | no |
| 3 | SwordAndShield | `OHS03_Sword_SM` | `Shield04_SM` | no |
| 4 | DoubleSword | `OHS03_Sword_SM` | `OHS03_Sword_SM` | no |
| 5 | Spear | `Spear01_SM` | — | no |
| 6 | MagicWand | `Wand01_SM` | — | **yes** |
| 7 | BowAndArrow | *(bow skeletal mesh, see below)* | — | **yes** |

**Per-hand attach rotations** (only the non-zero ones; these fix each weapon's orientation in the hand socket):
- Right hand: Spear (5) = roll **+10**. All others 0.
- Left hand: SwordAndShield (3) = yaw **−180**; DoubleSword (4) = yaw **−180**, roll **−90**; BowAndArrow (7) = yaw **170**. Others 0.

**Bow (stance 7) is special:** it is a **skeletal** mesh (an 8-bone flexing bow rig `Bow01_SK`), not a static mesh, mounted in the left hand and playing an idle flex loop (`Idle_Bow_Anim`). It also carries a **nocked arrow** mesh (`Arrow01_SK`). The bow itself gets an extra yaw 90 on its own component (stacked with the left-hand yaw 170).

**Unity:** keep an int stance index; on change, enable the correct weapon GameObject(s) parented to the hand bones with the listed local rotations, swap the locomotion blend tree, toggle the bow rig + crosshair. Weapon meshes are simple child objects except the bow, which is its own animated skinned mesh.

---

## 7. Melee combat (stances 0–5)

Attack = **LMB**. A `ComboIndex` int drives a light→heavy chain.

- **Increment:** each swing sets `ComboIndex = (ComboIndex + 1) % 5`.
- **Light vs heavy split:** if `ComboIndex < 3` → **LIGHT** attack (combo steps 0,1,2); else → **HEAVY** attack (steps 3,4).
  - **Light** montages play on an **upper-body slot** (layered blend per bone from the spine) so the character **can keep strafing while light-attacking**.
  - **Heavy** montages play **full-body** (default slot) — these root/commit the character.
- **Per-stance clips:** `StanceComboLight[8]` = `AM_RPG_Combo_<Stance>`; `StanceComboHeavy[8]` = `AM_RPG_ComboHeavy_<Stance>`.
- **Combo window:** after each attack a **retriggerable 1.0 s** timer runs. Attack again within 1 s → `ComboIndex` advances (next step). Let 1 s elapse → `ComboIndex` resets to **0**.

**Hit detection (animation-driven):** each attack clip carries a **"Hit" animation notify** at the strike frame. That notify fires **MeleeHit**, which:
1. does a **sphere overlap, radius 150**, centered in **front** of the character,
2. collects overlapping actors,
3. keeps only those tagged **`CanDamage`**,
4. applies **20 damage** to each (unique-actor overlap = automatic no-double-hit), crediting the player as instigator.

**Unity:** Animator with an **Upper Body layer** (avatar mask spine-and-up) for light attacks and full-body for heavy; a combo counter + 1 s coroutine window; an **animation event** at the strike frame calls `MeleeHit()` → `Physics.OverlapSphere(frontPoint, 150)` filtered by tag/layer → deal 20. Same `(index+1)%5`, `index<3` split.

---

## 8. Ranged combat (stances 6 Wand, 7 Bow) — charge & release

A per-stance flag `StanceIsRanged[8]` is **true for 6 and 7**. In ranged stances LMB does **not** combo — it charges and fires one shot.

- **LMB press:** if ranged → play the **draw/charge** animation (bow flexes; the bow-rig draw clip is picked from `BowDrawAnims` by index) and set `bIsCharging = true`.
- **LMB release:** if `bIsCharging` → play the **shot** animation (one discrete shot; it reuses the stance's combo clip as the fire motion) and set `bIsCharging = false`. The shot clip carries a **"Hit" notify at the release frame** that **spawns the arrow** (frame-accurate release).
- *(Current limitation: there's no distinct held-draw pose and no charge-time→power scaling yet — charge is visual only. Optional to add in Unity.)*

**Arrow spawn & aim (`BP_Arrow` projectile):** spawned at the **bow muzzle** (the `NockArrow` point). Aim direction:
- **If target-locked** → aim straight at the locked target.
- **Else (free aim)** → **raycast from the camera center forward** (up to ~5000 units); use the **hit point** (or the far point if nothing is hit) as the aim target, and point the muzzle at it. This makes the shot converge on the **center crosshair** even though the camera sits off to the shoulder (fixes close-range parallax).

| Arrow property | Value |
|---|---|
| Projectile speed | 6000 (initial = max) |
| Gravity scale | 0.4 (slight arc) |
| Rotation follows velocity | yes |
| Collision sphere radius | 5 |
| Lifespan | 5 s |
| Damage on hit | **30** (arrow's own on-hit → apply damage → destroy) |
| Self-hit | ignored (arrow ignores its instigator/player) |

**Crosshair:** a simple center reticle (`WBP_RangedReticle`: 4 bars + center dot) shown **only in ranged stances**, toggled in ApplyStance on `StanceIsRanged`. *(Deferred cosmetic: while locked-on, the center reticle sits low on the enemy while the arrow homes to the body — a lock-on indicator is a future add.)*

*(Wand (6) currently fires the same arrow projectile as a placeholder — it wants its own cast VFX/projectile later.)*

**Unity:** on Attack-pressed set charging + play draw; on Attack-released play fire and spawn a projectile prefab. Aim = locked-target direction if locked, else a `Camera` ray to find the hit point and aim there. Projectile = Rigidbody, initial velocity `6000 * aimDir`, gravity `0.4×`, `OnCollisionEnter` → 30 damage + destroy, ignore the shooter. Center UI crosshair active while ranged.

---

## 9. Dodge (all stances)

Key = **Left Ctrl**. A guard flag `IsDodging` prevents re-dodging mid-roll. Dodge rolls use **root-motion** clips (the animation drives the displacement).

- **Direction pick:** read the current move-input vector.
  - Input magnitude above threshold → **directional** roll (FWD / RGT / BWD / LFT) relative to facing, chosen by `round(direction/90)`.
  - No input (standing still) → **backward backstep** (BWD).
- **Per-stance clips:** `StanceDodgeFWD/BWD/LFT/RGT[8]` = `AM_RPG_Dodge_<DIR>_<Stance>`.
- On roll complete / blend-out → `IsDodging = false`.

**Unity:** dodge state guarded by a bool; pick the clip by input direction (4-way + standing-back); drive movement with Animator **root motion** (or a scripted displacement matching the clip). No i-frames are implemented (add if desired).

---

## 10. Target lock (soft lock)

Key = **Tab** (toggle).

- **Acquire:** sphere-cast from the **camera forward × 3000** (radius 125), filter by tag **`CanDamage`**, store the hit as `ActorToTargetLock`.
- **While locked (every frame):** aim the camera/character by `LookAt(cameraLocation → targetLocation − 100 on Z)` applied as control rotation. The **−100 Z** intentionally frames the enemy slightly high (screen center lands near the enemy's feet). The strafe movement model still applies, so you circle-strafe around the locked target.
- **Ranged aim** uses `ActorToTargetLock` when set (arrow flies to the target).
- **Clear:** press Tab again, or target becomes invalid/out of range.

**Unity:** on press, `Physics.SphereCast` from camera forward, pick nearest tagged target; each frame rotate the camera rig toward `target − (0,1,0)*100`; clear on re-press / null / distance.

---

## 11. Jump

Two-state (Grounded ↔ Airborne), key **Space**, jump velocity 420, air control 0.05. Enters an air pose while falling and blends back (~0.2 s) on landing. (JumpStart/JumpEnd/anticipation polish not implemented.)

---

## 12. Health / damage flow

The player has a stats component holding health; incoming damage routes through it. Damage the player **deals** flows through:
- melee → tag `CanDamage` + overlap (§7), 20 dmg;
- ranged → `BP_Arrow` projectile on-hit (§8), 30 dmg.
Enemies are tagged `CanDamage`. Kill credit is via the instigator = player.

**Unity:** a Health component on player and enemies; player deals damage through the melee overlap and the projectile hit; use a tag/layer for "damageable."

---

## Animation asset naming reference (for direct Unity mapping)

| System | Asset pattern |
|---|---|
| Locomotion | `BS_RPG_Loco_<Stance>` blendspaces (samples in §4) |
| Light combo | `AM_RPG_Combo_<Stance>` (upper-body) |
| Heavy combo | `AM_RPG_ComboHeavy_<Stance>` (full-body) |
| Dodge | `AM_RPG_Dodge_<FWD\|BWD\|LFT\|RGT>_<Stance>` (root motion) |
| Bow rig idle | `Idle_Bow_Anim` |
| Bow draw (by index 0–4) | `Attack01_Combo0102_Bow_Anim`, `Attack01_Combo0102_Bow_Anim`, `Attack02_Combo03_Bow_Anim`, `Attack04_Combo04_Bow_Anim`, `Attack03_Combo05_Bow_Anim` |

`<Stance>` ∈ NoWeapon, SingleSword, TwoHandsSword, SwordAndShield, DoubleSword, Spear, MagicWand, BowAndArrow.

---

## UE → Unity translation cheat-sheet

| UE concept | Unity equivalent |
|---|---|
| CharacterMovement `use controller desired rotation` (strafe facing) | rotate transform toward camera yaw each frame at 500°/s |
| 2D blendspace (Direction × Speed) | 2D blend tree with the same axes/samples |
| "Blend poses by int" (stance) | int Animator parameter switching sub-states / blend trees |
| Layered blend per bone (upper-body light attacks) | Animator layer + avatar mask (spine-up) |
| Anim Notify "Hit" | Animation Event calling your hit function |
| Root-motion montage (dodge) | Animator `applyRootMotion` (or scripted move) |
| Enhanced Input actions | Input System actions, same keys |
| Spring arm + camera | camera rig with follow-smoothing + collision raycast |
| ApplyDamage + `CanDamage` tag | Health component + tag/layer filter |

---

## Known-deferred / not-yet-built (so Unity can match "as-is" or improve)

- **Lock-on crosshair** doesn't match the locked-arrow aim point (cosmetic; center reticle sits low on the target). Consider a dedicated on-target lock bracket in Unity.
- **True held-draw pose** and **charge-time → power** scaling for ranged (currently charge is visual only; shot fires on release).
- **Wand cast VFX/projectile** (wand reuses the bow arrow as a placeholder).
- **Per-stance get-hit / death** reactions on the player (enemies have them).
- **Jump anticipation/landing** polish (2-state only).

---

*Source of truth: `BP_RPG_PlayerCharacter`, `BP_Arrow`, `IMC_RPG_Default`, `/Game/RPG/` assets — values verified live 2026-07-18. This is a Player-only spec; enemy/AI, UI/HUD beyond the crosshair, and world/level systems are out of scope.*
