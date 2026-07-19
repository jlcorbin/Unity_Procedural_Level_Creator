// UE5PortValidator.cs — M22 read-only validator.
//
// Confirms the UE5 player-parity port's script + input-asset surface is present
// and wired. Does NOT validate the Animator graph, blend trees, or prefab
// component placement — those are manual Editor steps tracked in
// Documentation/UE5_Port_Plan.md (P10) and verified by play-test.
//
// Menu: LevelGen ▶ Player ▶ Validate UE5 Port

#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LevelGen.Player.Editor
{
    public static class UE5PortValidator
    {
        private const string Asm = "Assembly-CSharp";
        private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string PlayerCombatPath = "Assets/Scripts/Player/PlayerCombat.cs";

        private static int _pass, _fail;

        [MenuItem("LevelGen/Player/Validate UE5 Port")]
        public static void Validate()
        {
            _pass = _fail = 0;

            // Scaffolding types
            var unrealUnits = GetType("LevelGen.Player.UnrealUnits");
            Check(1, "UnrealUnits type exists", unrealUnits != null);

            var stance = GetType("LevelGen.Player.Stance");
            Check(2, "Stance enum has 8 values",
                stance != null && stance.IsEnum && Enum.GetValues(stance).Length == 8);

            Check(3, "StanceDefinition (ScriptableObject) exists",
                IsSubclassOf("LevelGen.Player.StanceDefinition", typeof(ScriptableObject)));

            var ctrl = GetType("LevelGen.Player.StanceController");
            Check(4, "StanceController exists", ctrl != null);
            Check(5, "StanceController.SetStance(Stance,bool) exists",
                ctrl != null && ctrl.GetMethod("SetStance") != null);
            Check(6, "StanceController.CycleStance() exists",
                ctrl != null && ctrl.GetMethod("CycleStance") != null);
            Check(7, "StanceController.IsRanged property exists",
                ctrl != null && ctrl.GetProperty("IsRanged") != null);

            Check(8, "StanceDevCycler (DEV-only) exists",
                GetType("LevelGen.Player.StanceDevCycler") != null);

            // PlayerAnimator stance write
            var anim = GetType("LevelGen.Player.PlayerAnimator");
            Check(9, "PlayerAnimator.SetStanceIndex(int) exists",
                anim != null && anim.GetMethod("SetStanceIndex") != null);

            // Input reader events
            var reader = GetType("LevelGen.Player.PlayerInputReader");
            Check(10, "PlayerInputReader.SwitchStancePressed event exists",
                reader != null && reader.GetEvent("SwitchStancePressed") != null);
            Check(11, "PlayerInputReader.AttackReleased event exists",
                reader != null && reader.GetEvent("AttackReleased") != null);
            Check(12, "PlayerInputReader.OnSwitchStance endpoint exists",
                reader != null && reader.GetMethod("OnSwitchStance") != null);

            // Movement retune fields
            var pc = GetType("LevelGen.Player.PlayerController");
            Check(13, "PlayerController has acceleration/airControl/turnRate fields",
                pc != null
                && HasField(pc, "acceleration")
                && HasField(pc, "airControl")
                && HasField(pc, "turnRate"));

            // Ranged
            var ranged = GetType("LevelGen.Player.RangedCombat");
            Check(14, "RangedCombat exists", ranged != null);
            var arrow = GetType("LevelGen.Combat.ArrowProjectile");
            Check(15, "ArrowProjectile.Initialize(Vector3,GameObject,int) exists",
                arrow != null && arrow.GetMethod("Initialize") != null);

            // PlayerCombat ranged suppression + tag combo (source scan)
            string combatSrc = ReadAsset(PlayerCombatPath);
            Check(16, "PlayerCombat suppresses melee LMB in ranged stances",
                combatSrc != null && combatSrc.Contains("_stance.IsRanged"));
            Check(17, "PlayerCombat combos by Animator tag (Attack1/2/3)",
                combatSrc != null && combatSrc.Contains("Attack1Tag") && combatSrc.Contains("IsAttack1"));

            // Input asset bindings
            string input = ReadAsset(InputAssetPath);
            Check(18, "Input asset has SwitchStance action", input != null && input.Contains("\"SwitchStance\""));
            Check(19, "Input asset binds SwitchStance to Q", input != null && input.Contains("<Keyboard>/q"));
            Check(20, "Input asset binds Dodge to Left Ctrl", input != null && input.Contains("<Keyboard>/leftCtrl"));

            Debug.Log($"[UE5PortValidator] {_pass} PASS / {_fail} FAIL out of {_pass + _fail} checks. " +
                      "Animator graph, blend trees, and prefab wiring are manual (see UE5_Port_Plan.md P10).");
        }

        // ── Helpers ─────────────────────────────────────────────────────────
        private static Type GetType(string full) => Type.GetType($"{full}, {Asm}");

        private static bool IsSubclassOf(string full, Type baseType)
        {
            var t = GetType(full);
            return t != null && t.IsSubclassOf(baseType);
        }

        private static bool HasField(Type t, string name) =>
            t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) != null;

        private static string ReadAsset(string path)
        {
            try { return System.IO.File.ReadAllText(path); }
            catch { return null; }
        }

        private static void Check(int n, string label, bool ok)
        {
            if (ok) { _pass++; Debug.Log($"[UE5PortValidator] {n} PASS — {label}"); }
            else    { _fail++; Debug.LogError($"[UE5PortValidator] {n} FAIL — {label}"); }
        }
    }
}
#endif
