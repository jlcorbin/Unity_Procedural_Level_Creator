// PlayerComboWeaponTypeValidator.cs — M21 per-weapon combo code-layer validator.
//
// Read-only. No scene modification, no asset writes.
//
// Checks that every code-layer piece of M21 landed correctly:
//   1–2.  New script files exist on disk.
//   3.    WeaponType enum has the expected 5 values.
//   4–5.  PlayerAnimator gained the SetWeaponType method + _hashWeaponType field.
//   6–7.  PlayerCombat calls SetWeaponType before SetAttackTrigger.
//   8.    WeaponTypeResolver.Resolve(null, null) returns Unarmed at runtime.
//
// Menu: LevelGen ▶ Player ▶ Validate Combo WeaponType (M21)

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Items;
using LevelGen.Player;

namespace LevelGen.Player.Editor
{
    public static class PlayerComboWeaponTypeValidator
    {
        private const string WeaponTypeSrcPath         = "Assets/Scripts/Items/WeaponType.cs";
        private const string WeaponTypeResolverSrcPath = "Assets/Scripts/Items/WeaponTypeResolver.cs";
        private const string PlayerCombatSrcPath       = "Assets/Scripts/Player/PlayerCombat.cs";

        [MenuItem("LevelGen/Player/Validate Combo WeaponType (M21)")]
        public static void Run()
        {
            int pass = 0, fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: WeaponType.cs exists ──────────────────────────────────────
            bool weaponTypeExists = File.Exists(WeaponTypeSrcPath);
            Check("1 WeaponType.cs exists",
                weaponTypeExists,
                weaponTypeExists
                    ? WeaponTypeSrcPath
                    : $"missing at {WeaponTypeSrcPath}");

            // ── 2: WeaponTypeResolver.cs exists ─────────────────────────────
            bool resolverExists = File.Exists(WeaponTypeResolverSrcPath);
            Check("2 WeaponTypeResolver.cs exists",
                resolverExists,
                resolverExists
                    ? WeaponTypeResolverSrcPath
                    : $"missing at {WeaponTypeResolverSrcPath}");

            // ── 3: WeaponType enum has 5 values ─────────────────────────────
            int enumCount = 0;
            bool enumLoaded = false;
            try
            {
                enumCount  = Enum.GetValues(typeof(WeaponType)).Length;
                enumLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerComboWeaponTypeValidator] Could not load WeaponType enum: {ex.Message}");
            }
            Check("3 WeaponType enum has 5 values",
                enumLoaded && enumCount == 5,
                enumLoaded
                    ? $"found {enumCount} values (expected 5)"
                    : "enum type not loaded — script may not have compiled");

            // ── 4: PlayerAnimator has public SetWeaponType method ────────────
            var paType         = typeof(PlayerAnimator);
            var setWeaponMethod = paType.GetMethod("SetWeaponType",
                BindingFlags.Public | BindingFlags.Instance);
            Check("4 PlayerAnimator has public SetWeaponType method",
                setWeaponMethod != null,
                setWeaponMethod != null
                    ? $"found: {setWeaponMethod.Name}({string.Join(", ", Array.ConvertAll(setWeaponMethod.GetParameters(), p => p.ParameterType.Name))})"
                    : "SetWeaponType not found on PlayerAnimator — check PlayerAnimator.cs");

            // ── 5: PlayerAnimator has private _hashWeaponType field ──────────
            var hashField = paType.GetField("_hashWeaponType",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Check("5 PlayerAnimator has _hashWeaponType private instance field",
                hashField != null,
                hashField != null
                    ? $"found: {hashField.FieldType.Name} {hashField.Name}"
                    : "_hashWeaponType field not found on PlayerAnimator — check PlayerAnimator.cs");

            // ── 6 & 7: PlayerCombat source scans ────────────────────────────
            bool combatSrcLoaded = false;
            string[] combatLines = Array.Empty<string>();
            if (File.Exists(PlayerCombatSrcPath))
            {
                combatLines    = File.ReadAllLines(PlayerCombatSrcPath);
                combatSrcLoaded = true;
            }
            else
            {
                Debug.LogWarning($"[PlayerComboWeaponTypeValidator] PlayerCombat.cs not found at {PlayerCombatSrcPath}");
            }

            // 6: Source contains SetWeaponType
            bool hasSetWeaponCall = false;
            int  setWeaponLine    = -1;
            int  setAttackLine    = -1;

            if (combatSrcLoaded)
            {
                for (int i = 0; i < combatLines.Length; i++)
                {
                    string l = combatLines[i];
                    if (setWeaponLine < 0 && l.Contains("SetWeaponType"))
                    {
                        setWeaponLine    = i;
                        hasSetWeaponCall = true;
                    }
                    if (setAttackLine < 0 && l.Contains("SetAttackTrigger"))
                        setAttackLine = i;
                }
            }

            Check("6 PlayerCombat.cs contains SetWeaponType call",
                hasSetWeaponCall,
                hasSetWeaponCall
                    ? $"first occurrence at line {setWeaponLine + 1}"
                    : $"SetWeaponType not found in {PlayerCombatSrcPath}");

            // 7: SetWeaponType appears before SetAttackTrigger
            bool weaponBeforeAttack = setWeaponLine >= 0 && setAttackLine >= 0
                                      && setWeaponLine < setAttackLine;
            Check("7 SetWeaponType line index < SetAttackTrigger line index",
                weaponBeforeAttack,
                weaponBeforeAttack
                    ? $"SetWeaponType at line {setWeaponLine + 1}, SetAttackTrigger at line {setAttackLine + 1}"
                    : $"SetWeaponType line={setWeaponLine + 1}, SetAttackTrigger line={setAttackLine + 1} — weapon type must be pushed before the trigger fires");

            // ── 8: WeaponTypeResolver.Resolve(null, null) == Unarmed ─────────
            bool resolveCorrect = false;
            try
            {
                WeaponType result = WeaponTypeResolver.Resolve(null, null);
                resolveCorrect    = result == WeaponType.Unarmed;
                Check("8 WeaponTypeResolver.Resolve(null, null) returns Unarmed",
                    resolveCorrect,
                    resolveCorrect
                        ? $"returned WeaponType.Unarmed ({(int)result})"
                        : $"returned {result} instead of Unarmed — check WeaponTypeResolver null guard");
            }
            catch (Exception ex)
            {
                fail++;
                Debug.LogError($"[Validator] FAIL — 8 WeaponTypeResolver.Resolve(null, null) returns Unarmed: threw {ex.GetType().Name}: {ex.Message}");
            }

            // ── Summary ──────────────────────────────────────────────────────
            if (pass + fail > 0)
                Debug.Log($"[PlayerComboWeaponTypeValidator] {pass} PASS / {fail} FAIL out of {pass + fail} checks");
        }
    }
}
#endif
