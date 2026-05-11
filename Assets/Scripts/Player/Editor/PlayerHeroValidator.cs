// PlayerHeroValidator.cs — M12-R consolidated player-side validator.
//
// Replaces (consolidates the responsibilities of) the deleted:
//   PlayerStaminaValidator.cs
//   PlayerDeathValidator.cs
//   PlayerDodgeValidator.cs
//
// Combat-side validators that test cross-cutting concerns (animator
// state shape, combo wiring, jump wiring, weapon hitbox, damage routing)
// remain as separate scripts — they cover concerns broader than the
// Player prefab manifest. This validator focuses on:
//   1. Prefab structural integrity (components present, no duplicates)
//   2. PlayerHero ref wiring (every SerializeField non-null)
//   3. Per-script API surface (events / methods / properties)
//   4. Animator parameter / state regression (Dodge + Death + Attack/Hit)
//
// Read-only. No scene modification, no asset writes.
//
// Menu: LevelGen ▶ Player ▶ Validate Player_Hero

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Input;
using LevelGen.Player;

namespace LevelGen.Player.Editor
{
    public static class PlayerHeroValidator
    {
        private const string PrefabPath          = "Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab";
        private const string OldPrefabPath       = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const string ControllerPath      = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string PlayerHeroSrcPath   = "Assets/Scripts/Player/PlayerHero.cs";

        [MenuItem("LevelGen/Player/Validate Player_Hero")]
        public static void Run()
        {
            int pass = 0, fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: Player_Hero.prefab exists; old name gone ──────────────────
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check("1 Player_Hero.prefab exists at canonical path",
                prefab != null,
                prefab != null ? PrefabPath : $"missing at {PrefabPath} — run 'Build Player_Hero Prefab'");

            bool oldStillPresent = AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefabPath) != null;
            Check("2 Old Player_MaleHero.prefab is gone (rename complete)",
                !oldStillPresent,
                oldStillPresent
                    ? "old prefab still at " + OldPrefabPath + " — rename did not run / collided"
                    : "renamed/removed");

            if (prefab == null) { Summary(pass, fail); return; }

            // ── 3: PlayerHero component on root ──────────────────────────────
            var hero = prefab.GetComponent<PlayerHero>();
            Check("3 PlayerHero component present on root",
                hero != null,
                hero != null ? "found" : "MISSING — re-run builder");

            // ── 4-17: All 14 required components present on root ─────────────
            CheckComponent<CharacterController>(prefab, "4", ref pass, ref fail);
            CheckComponent<UnityEngine.InputSystem.PlayerInput>(prefab, "5", ref pass, ref fail);
            CheckComponent<CharacterStatsRuntime>(prefab, "6", ref pass, ref fail);
            CheckComponent<Targetable>(prefab, "7", ref pass, ref fail);
            CheckComponent<PlayerInputReader>(prefab, "8", ref pass, ref fail);
            CheckComponent<PlayerAnimator>(prefab, "9", ref pass, ref fail);
            CheckComponent<PlayerController>(prefab, "10", ref pass, ref fail);
            CheckComponent<PlayerCombat>(prefab, "11", ref pass, ref fail);
            CheckComponent<PlayerStamina>(prefab, "12", ref pass, ref fail);
            CheckComponent<PlayerDodge>(prefab, "13", ref pass, ref fail);
            CheckComponent<PlayerHitReaction>(prefab, "14", ref pass, ref fail);
            CheckComponent<PlayerDeath>(prefab, "15", ref pass, ref fail);
            CheckComponent<PlayerInteractor>(prefab, "16", ref pass, ref fail);
            CheckComponent<MouseLook>(prefab, "17", ref pass, ref fail);

            // ── 18: No duplicate components on root ──────────────────────────
            bool noDupes =
                prefab.GetComponents<PlayerHero>().Length == 1 &&
                prefab.GetComponents<PlayerInputReader>().Length == 1 &&
                prefab.GetComponents<PlayerAnimator>().Length == 1 &&
                prefab.GetComponents<PlayerController>().Length == 1 &&
                prefab.GetComponents<PlayerCombat>().Length == 1 &&
                prefab.GetComponents<PlayerStamina>().Length == 1 &&
                prefab.GetComponents<PlayerDodge>().Length == 1 &&
                prefab.GetComponents<PlayerHitReaction>().Length == 1 &&
                prefab.GetComponents<PlayerDeath>().Length == 1 &&
                prefab.GetComponents<PlayerInteractor>().Length == 1 &&
                prefab.GetComponents<MouseLook>().Length == 1;
            Check("18 No duplicate Player components on root", noDupes,
                noDupes ? "all single-instance" : "duplicates found — DisallowMultipleComponent violated");

            // ── 19-32: PlayerHero SerializeField refs all wired ──────────────
            if (hero != null)
            {
                var so = new SerializedObject(hero);
                CheckRef(so, "_characterController", "19", ref pass, ref fail);
                CheckRef(so, "_stats",               "20", ref pass, ref fail);
                CheckRef(so, "_targetable",          "21", ref pass, ref fail);
                CheckRef(so, "_playerInput",         "22", ref pass, ref fail);
                CheckRef(so, "_input",               "23", ref pass, ref fail);
                CheckRef(so, "_mouseLook",           "24", ref pass, ref fail);
                CheckRef(so, "_controller",          "25", ref pass, ref fail);
                CheckRef(so, "_animator",            "26", ref pass, ref fail);
                CheckRef(so, "_stamina",             "27", ref pass, ref fail);
                CheckRef(so, "_dodge",               "28", ref pass, ref fail);
                CheckRef(so, "_combat",              "29", ref pass, ref fail);
                CheckRef(so, "_hitReaction",         "30", ref pass, ref fail);
                CheckRef(so, "_death",               "31", ref pass, ref fail);
                CheckRef(so, "_interactor",          "32", ref pass, ref fail);
            }
            else
            {
                for (int i = 19; i <= 32; i++)
                    Check($"{i} PlayerHero SerializeField ref", false, "PlayerHero missing — see check 3");
            }

            // ── 33-35: CharacterStatsRuntime surface ─────────────────────────
            var statsType = typeof(CharacterStatsRuntime);
            Check("33 CharacterStatsRuntime.IsInvulnerable property",
                statsType.GetProperty("IsInvulnerable", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");
            Check("34 CharacterStatsRuntime.SetInvulnerable(bool) method",
                statsType.GetMethod("SetInvulnerable", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(bool) }, null) != null,
                "reflection");
            Check("35 CharacterStatsRuntime.ApplyDamage(int) method",
                statsType.GetMethod("ApplyDamage", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null) != null,
                "reflection");

            // ── 36-39: PlayerInputReader events ──────────────────────────────
            var readerType = typeof(PlayerInputReader);
            Check("36 PlayerInputReader.AttackPressed event",
                readerType.GetEvent("AttackPressed", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");
            Check("37 PlayerInputReader.JumpPressed event",
                readerType.GetEvent("JumpPressed", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");
            Check("38 PlayerInputReader.InteractPressed event",
                readerType.GetEvent("InteractPressed", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");
            Check("39 PlayerInputReader.DodgePressed event",
                readerType.GetEvent("DodgePressed", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");

            // ── 40-41: PlayerCombat surface ──────────────────────────────────
            var combatType = typeof(PlayerCombat);
            Check("40 PlayerCombat.CancelAttack() method",
                combatType.GetMethod("CancelAttack", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null) != null,
                "reflection");
            // WeaponHitbox child presence (owned by PlayerCombatHitboxBuilder)
            bool hasHitbox = false;
            string hitboxDetail = "weapon_r child not found";
            var weaponR = FindChildRecursive(prefab.transform, "weapon_r");
            if (weaponR == null) weaponR = FindChildRecursive(prefab.transform, "Weapon_R");
            if (weaponR != null)
            {
                var wh = FindChildByName(weaponR, "WeaponHitbox");
                hasHitbox = wh != null;
                hitboxDetail = hasHitbox
                    ? $"WeaponHitbox under '{weaponR.name}'"
                    : $"weapon bone '{weaponR.name}' found but no WeaponHitbox child — run 'Add Weapon Hitbox to Player_Hero'";
            }
            Check("41 WeaponHitbox child under weapon_r", hasHitbox, hitboxDetail);

            // ── 42: PlayerDodge.IsDodging property ───────────────────────────
            Check("42 PlayerDodge.IsDodging property",
                typeof(PlayerDodge).GetProperty("IsDodging", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");

            // ── 43-48: Animator controller shape ─────────────────────────────
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                for (int i = 43; i <= 48; i++)
                    Check($"{i} PlayerBaseController.controller", false, $"missing at {ControllerPath}");
            }
            else
            {
                bool hasDodgeTrigger = false, hasDodgeDir = false;
                bool hasAttack = false, hasHit = false, hasDeath = false;
                foreach (var p in controller.parameters)
                {
                    if (p.name == "DodgeTrigger"   && p.type == AnimatorControllerParameterType.Trigger) hasDodgeTrigger = true;
                    if (p.name == "DodgeDirection" && p.type == AnimatorControllerParameterType.Int)     hasDodgeDir     = true;
                    if (p.name == "Attack"         && p.type == AnimatorControllerParameterType.Trigger) hasAttack       = true;
                    if (p.name == "Hit"            && p.type == AnimatorControllerParameterType.Trigger) hasHit          = true;
                    if (p.name == "Death"          && p.type == AnimatorControllerParameterType.Trigger) hasDeath        = true;
                }
                Check("43 Animator parameter DodgeTrigger (Trigger)",   hasDodgeTrigger, hasDodgeTrigger ? "OK" : "missing — run 'Extend PlayerBaseController (M12 Dodge)'");
                Check("44 Animator parameter DodgeDirection (Int)",     hasDodgeDir,     hasDodgeDir     ? "OK" : "missing");
                Check("45 Animator parameter Attack/Hit/Death present", hasAttack && hasHit && hasDeath,
                    $"Attack={hasAttack}, Hit={hasHit}, Death={hasDeath}");

                AnimatorState rollFWD = null, rollBWD = null, rollLFT = null, rollRGT = null;
                if (controller.layers.Length > 0)
                {
                    var rootSm = controller.layers[0].stateMachine;
                    FindStateRecursive(rootSm, "RollFWD", ref rollFWD);
                    FindStateRecursive(rootSm, "RollBWD", ref rollBWD);
                    FindStateRecursive(rootSm, "RollLFT", ref rollLFT);
                    FindStateRecursive(rootSm, "RollRGT", ref rollRGT);
                }
                Check("46 Animator Roll{FWD,BWD,LFT,RGT} states present",
                    rollFWD != null && rollBWD != null && rollLFT != null && rollRGT != null,
                    $"FWD={(rollFWD != null)}, BWD={(rollBWD != null)}, LFT={(rollLFT != null)}, RGT={(rollRGT != null)}");

                // Death state (regression — M5)
                AnimatorState deathState = null;
                if (controller.layers.Length > 0)
                    FindStateRecursive(controller.layers[0].stateMachine, "Death", ref deathState);
                Check("47 Animator Death state present (M5 regression)",
                    deathState != null,
                    deathState != null ? "OK" : "missing — was M5 extender run?");

                // Attack / Hit states (regression — M2-B / M4-A)
                AnimatorState atkState = null, hitState = null;
                if (controller.layers.Length > 0)
                {
                    FindStateRecursive(controller.layers[0].stateMachine, "Attack", ref atkState);
                    FindStateRecursive(controller.layers[0].stateMachine, "Hit",    ref hitState);
                }
                Check("48 Animator Attack + Hit states present (M2-B/M4 regression)",
                    atkState != null && hitState != null,
                    $"Attack={(atkState != null)}, Hit={(hitState != null)}");
            }

            // ── 49: PlayerDeath.OnPlayerDied event ───────────────────────────
            Check("49 PlayerDeath.OnPlayerDied event",
                typeof(PlayerDeath).GetEvent("OnPlayerDied", BindingFlags.Public | BindingFlags.Instance) != null,
                "reflection");

            // ── 50: PlayerInteractor radius via SerializeField on prefab ─────
            // Interactable subclasses carry their own radius via SphereCollider;
            // PlayerInteractor has no radius field of its own. Check that
            // PlayerInteractor.cs script file exists as a sanity proxy.
            Check("50 PlayerInteractor.cs source exists",
                AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Player/PlayerInteractor.cs") != null,
                "source file");

            Summary(pass, fail);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void CheckComponent<T>(GameObject root, string idx,
                                              ref int pass, ref int fail) where T : Component
        {
            bool ok = root.GetComponent<T>() != null;
            if (ok) { pass++; Debug.Log($"[Validator] PASS — {idx} Component {typeof(T).Name} present on root"); }
            else    { fail++; Debug.LogError($"[Validator] FAIL — {idx} Component {typeof(T).Name} missing from root"); }
        }

        private static void CheckRef(SerializedObject so, string fieldName, string idx,
                                     ref int pass, ref int fail)
        {
            var prop = so.FindProperty(fieldName);
            bool ok = prop != null && prop.objectReferenceValue != null;
            string msg = prop == null
                ? "field not found in SerializedObject"
                : (ok ? prop.objectReferenceValue.name : "field is null — re-run builder");
            if (ok) { pass++; Debug.Log($"[Validator] PASS — {idx} PlayerHero.{fieldName}: {msg}"); }
            else    { fail++; Debug.LogError($"[Validator] FAIL — {idx} PlayerHero.{fieldName}: {msg}"); }
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform c in parent)
                if (c.name == name) return c;
            return null;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var found = FindChildRecursive(c, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void FindStateRecursive(AnimatorStateMachine sm, string name, ref AnimatorState found)
        {
            if (found != null || sm == null) return;
            foreach (var sc in sm.states)
                if (sc.state != null && sc.state.name == name) { found = sc.state; return; }
            foreach (var cm in sm.stateMachines)
            {
                FindStateRecursive(cm.stateMachine, name, ref found);
                if (found != null) return;
            }
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Player_Hero wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
