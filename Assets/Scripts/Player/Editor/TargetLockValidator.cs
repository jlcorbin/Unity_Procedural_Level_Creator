// TargetLockValidator.cs — M15. Read-only validator for the Target Lock system.
//
// Confirms:
//   1. TargetLock script + attributes are present and correctly shaped.
//   2. LockIndicator script exists.
//   3. PlayerInputReader exposes OnLockOnPerformed (Action).
//   4. InputSystem_Actions.inputactions declares a "LockOn" action.
//   5. PlayerController.cs contains the strafe branch (TargetLock.Instance).
//   6. PlayerHero has [RequireComponent(typeof(TargetLock))].
//   7. Player_Hero.prefab carries a TargetLock component.
//
// Read-only. No scene modification, no asset writes.
//
// Menu: LevelGen ▶ Player ▶ Validate Target Lock

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Player.Editor
{
    public static class TargetLockValidator
    {
        // ── Paths ────────────────────────────────────────────────────────────
        private const string TargetLockSrcPath        = "Assets/Scripts/Player/TargetLock.cs";
        private const string LockIndicatorSrcPath     = "Assets/Scripts/Combat/LockIndicator.cs";
        private const string PlayerControllerSrcPath  = "Assets/Scripts/Player/PlayerController.cs";
        private const string PlayerHeroSrcPath        = "Assets/Scripts/Player/PlayerHero.cs";
        private const string InputActionsPath         = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerHeroPrefabPath     = "Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab";

        // ── Type lookups (deferred via reflection so this file compiles even
        //    if TargetLock is renamed / moved during a future refactor) ───────
        private const string TargetLockTypeName       = "LevelGen.TargetLock, Assembly-CSharp";
        private const string LockIndicatorTypeName    = "LevelGen.LockIndicator, Assembly-CSharp";

        [MenuItem("LevelGen/Player/Validate Target Lock")]
        public static void Run()
        {
            int pass = 0, fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: TargetLock script exists at expected path ─────────────────
            bool targetLockSrcExists = File.Exists(TargetLockSrcPath);
            Check("1 TargetLock script exists",
                targetLockSrcExists,
                targetLockSrcExists ? TargetLockSrcPath : $"missing at {TargetLockSrcPath}");

            // Resolve the TargetLock Type once for downstream reflection checks.
            Type tlType = Type.GetType(TargetLockTypeName);

            // ── 2: TargetLock has [DefaultExecutionOrder(-40)] ───────────────
            if (tlType == null)
            {
                Check("2 TargetLock has [DefaultExecutionOrder(-40)]",
                    false,
                    $"Type '{TargetLockTypeName}' could not be loaded — recompile may be in progress");
            }
            else
            {
                var deoAttr = tlType.GetCustomAttribute<UnityEngine.DefaultExecutionOrder>();
                bool deoOk = deoAttr != null && deoAttr.order == -40;
                Check("2 TargetLock has [DefaultExecutionOrder(-40)]",
                    deoOk,
                    deoAttr == null
                        ? "attribute missing"
                        : $"order = {deoAttr.order} (expected -40)");
            }

            // ── 3: TargetLock.IsLocked is a public bool property ─────────────
            if (tlType != null)
            {
                var prop = tlType.GetProperty("IsLocked",
                    BindingFlags.Public | BindingFlags.Instance);
                bool ok = prop != null && prop.PropertyType == typeof(bool) && prop.CanRead;
                Check("3 TargetLock.IsLocked is a public bool property",
                    ok,
                    prop == null
                        ? "property missing"
                        : $"type = {prop.PropertyType.Name}, canRead = {prop.CanRead}");
            }
            else
            {
                Check("3 TargetLock.IsLocked is a public bool property", false, "type missing");
            }

            // ── 4: TargetLock.LockedTarget is a public Targetable property ───
            if (tlType != null)
            {
                var prop = tlType.GetProperty("LockedTarget",
                    BindingFlags.Public | BindingFlags.Instance);
                bool ok = prop != null && prop.PropertyType == typeof(Targetable) && prop.CanRead;
                Check("4 TargetLock.LockedTarget is a public Targetable property",
                    ok,
                    prop == null
                        ? "property missing"
                        : $"type = {prop.PropertyType.FullName}, canRead = {prop.CanRead}");
            }
            else
            {
                Check("4 TargetLock.LockedTarget is a public Targetable property",
                    false, "type missing");
            }

            // ── 5: TargetLock.Instance static property exists ────────────────
            if (tlType != null)
            {
                var prop = tlType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                bool ok = prop != null && prop.PropertyType == tlType && prop.CanRead;
                Check("5 TargetLock.Instance static property exists",
                    ok,
                    prop == null
                        ? "static property missing"
                        : $"type = {prop.PropertyType.Name}, canRead = {prop.CanRead}");
            }
            else
            {
                Check("5 TargetLock.Instance static property exists", false, "type missing");
            }

            // ── 6: TargetLock.InitFromPlayerHero(PlayerInputReader) method ───
            if (tlType != null)
            {
                var method = tlType.GetMethod("InitFromPlayerHero",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(PlayerInputReader) },
                    null);
                Check("6 TargetLock.InitFromPlayerHero(PlayerInputReader) method",
                    method != null,
                    method != null
                        ? $"return = {method.ReturnType.Name}"
                        : "method missing (or wrong parameter type)");
            }
            else
            {
                Check("6 TargetLock.InitFromPlayerHero(PlayerInputReader) method",
                    false, "type missing");
            }

            // ── 7: LockIndicator script exists at expected path ──────────────
            bool lockIndicatorSrcExists = File.Exists(LockIndicatorSrcPath);
            Check("7 LockIndicator script exists",
                lockIndicatorSrcExists,
                lockIndicatorSrcExists ? LockIndicatorSrcPath : $"missing at {LockIndicatorSrcPath}");

            // Sanity: type loads via reflection.
            Type liType = Type.GetType(LockIndicatorTypeName);
            if (liType == null)
            {
                fail++;
                Debug.LogError($"[Validator] FAIL — 7b LockIndicator type loads: " +
                               $"'{LockIndicatorTypeName}' could not be loaded");
            }
            else
            {
                pass++;
                Debug.Log($"[Validator] PASS — 7b LockIndicator type loads: " +
                          $"{liType.FullName}");
            }

            // ── 8: PlayerInputReader.OnLockOnPerformed event of type Action ──
            var prType = typeof(PlayerInputReader);
            var evt = prType.GetEvent("OnLockOnPerformed",
                BindingFlags.Public | BindingFlags.Instance);
            if (evt == null)
            {
                Check("8 PlayerInputReader.OnLockOnPerformed event", false, "event missing");
            }
            else
            {
                bool ok = evt.EventHandlerType == typeof(Action);
                Check("8 PlayerInputReader.OnLockOnPerformed event",
                    ok,
                    $"handler type = {evt.EventHandlerType.FullName} " +
                    $"(expected System.Action)");
            }

            // ── 9: InputSystem_Actions contains an action named "LockOn" ─────
            if (!File.Exists(InputActionsPath))
            {
                Check("9 InputSystem_Actions.inputactions contains LockOn action",
                    false, $"file missing at {InputActionsPath}");
            }
            else
            {
                string text = File.ReadAllText(InputActionsPath);
                bool ok = text.Contains("\"name\": \"LockOn\"");
                Check("9 InputSystem_Actions.inputactions contains LockOn action",
                    ok,
                    ok ? "action declaration found" : "no action named LockOn in file");
            }

            // ── 10: PlayerController.cs contains TargetLock.Instance ─────────
            //     (confirms the strafe branch landed in the movement script)
            if (!File.Exists(PlayerControllerSrcPath))
            {
                Check("10 PlayerController.cs contains TargetLock.Instance (strafe branch)",
                    false, $"file missing at {PlayerControllerSrcPath}");
            }
            else
            {
                string text = File.ReadAllText(PlayerControllerSrcPath);
                bool ok = text.Contains("TargetLock.Instance");
                Check("10 PlayerController.cs contains TargetLock.Instance (strafe branch)",
                    ok,
                    ok
                        ? "strafe branch detected"
                        : "TargetLock.Instance reference not found — strafe branch missing");
            }

            // ── 11: PlayerHero has [RequireComponent(typeof(TargetLock))] ────
            if (!File.Exists(PlayerHeroSrcPath))
            {
                Check("11 PlayerHero has [RequireComponent(typeof(TargetLock))]",
                    false, $"file missing at {PlayerHeroSrcPath}");
            }
            else
            {
                string text = File.ReadAllText(PlayerHeroSrcPath);
                bool ok = text.Contains("[RequireComponent(typeof(TargetLock))]");
                Check("11 PlayerHero has [RequireComponent(typeof(TargetLock))]",
                    ok,
                    ok
                        ? "attribute declaration found"
                        : "attribute missing — re-run M15 wiring");
            }

            // ── 12: Player_Hero.prefab carries a TargetLock component ────────
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHeroPrefabPath);
            if (prefab == null)
            {
                Check("12 Player_Hero.prefab has TargetLock component",
                    false,
                    $"prefab missing at {PlayerHeroPrefabPath} — run 'Build Player_Hero Prefab'");
            }
            else if (tlType == null)
            {
                Check("12 Player_Hero.prefab has TargetLock component",
                    false, "TargetLock type could not be resolved");
            }
            else
            {
                bool ok = prefab.GetComponent(tlType) != null;
                Check("12 Player_Hero.prefab has TargetLock component",
                    ok,
                    ok
                        ? "component present on root"
                        : "missing — re-run 'Build Player_Hero Prefab' or open the prefab " +
                          "in the editor to trigger [RequireComponent] auto-add");
            }

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string verdict = fail == 0 ? "PASS" : "FAIL";
            Debug.Log($"[Validator] M15 Target Lock — {verdict}: {pass} pass, {fail} fail");
        }
    }
}
#endif
