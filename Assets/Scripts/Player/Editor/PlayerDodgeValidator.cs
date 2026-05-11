// PlayerDodgeValidator.cs — read-only checks on M12 wiring.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Player Dodge
//
// 17 checks covering the script, animator, and prefab surfaces.
// Pattern mirrors PlayerDeathValidator: reflection on types,
// AnimatorController API for the graph, SerializedObject reads
// for the prefab. No scene/play-mode work — runs in edit mode.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Player.Editor
{
    public static class PlayerDodgeValidator
    {
        private const string PlayerDodgePath        = "Assets/Scripts/Player/PlayerDodge.cs";
        private const string PlayerInputReaderPath  = "Assets/Scripts/Player/PlayerInputReader.cs";
        private const string PlayerCombatSrcPath    = "Assets/Scripts/Player/PlayerCombat.cs";
        private const string PlayerControllerPath   = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string PlayerPrefabPath       = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Validate Player Dodge")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: PlayerDodge.cs at expected path ──────────────────────────
            bool ok1 = AssetDatabase.LoadAssetAtPath<MonoScript>(PlayerDodgePath) != null;
            Check("1 PlayerDodge.cs exists at expected path", ok1,
                ok1 ? PlayerDodgePath : $"missing at {PlayerDodgePath}");

            // ── 2: PlayerDodge type & attributes ────────────────────────────
            var dodgeType = typeof(PlayerDodge);
            var requireAttrs = dodgeType.GetCustomAttributes<RequireComponent>();
            bool requireCC = false, requireStats = false, requireInput = false;
            foreach (var a in requireAttrs)
            {
                if (a.m_Type0 == typeof(CharacterController)       || a.m_Type1 == typeof(CharacterController)       || a.m_Type2 == typeof(CharacterController))       requireCC    = true;
                if (a.m_Type0 == typeof(CharacterStatsRuntime)     || a.m_Type1 == typeof(CharacterStatsRuntime)     || a.m_Type2 == typeof(CharacterStatsRuntime))     requireStats = true;
                if (a.m_Type0 == typeof(PlayerInputReader)         || a.m_Type1 == typeof(PlayerInputReader)         || a.m_Type2 == typeof(PlayerInputReader))         requireInput = true;
            }
            Check("2 PlayerDodge [RequireComponent(CharacterController)]", requireCC,
                requireCC ? "attribute present" : "attribute missing");
            Check("3 PlayerDodge [RequireComponent(CharacterStatsRuntime)]", requireStats,
                requireStats ? "attribute present" : "attribute missing");
            Check("4 PlayerDodge [RequireComponent(PlayerInputReader)]", requireInput,
                requireInput ? "attribute present" : "attribute missing");

            // ── 5: CharacterStatsRuntime.IsInvulnerable property ────────────
            var statsType = typeof(CharacterStatsRuntime);
            var invProp = statsType.GetProperty("IsInvulnerable",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok5 = invProp != null
                       && invProp.PropertyType == typeof(bool)
                       && invProp.GetGetMethod() != null;
            Check("5 CharacterStatsRuntime.IsInvulnerable public bool { get; }", ok5,
                invProp == null
                    ? "property missing"
                    : $"type={invProp.PropertyType.Name}, getter={(invProp.GetGetMethod() != null ? "yes" : "no")}");

            // ── 6: CharacterStatsRuntime.SetInvulnerable(bool) method ──────
            var setInvMethod = statsType.GetMethod("SetInvulnerable",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(bool) }, null);
            bool ok6 = setInvMethod != null && setInvMethod.ReturnType == typeof(void);
            Check("6 CharacterStatsRuntime.SetInvulnerable(bool) public void", ok6,
                setInvMethod != null
                    ? $"signature OK, returns {setInvMethod.ReturnType.Name}"
                    : "method missing");

            // ── 7: ApplyDamage(int) signature unchanged ────────────────────
            var applyDmg = statsType.GetMethod("ApplyDamage",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            bool ok7 = applyDmg != null && applyDmg.ReturnType == typeof(void);
            Check("7 CharacterStatsRuntime.ApplyDamage(int) signature unchanged", ok7,
                applyDmg != null
                    ? "signature preserved"
                    : "method missing or signature changed");

            // ── 8: PlayerInputReader.DodgePressed event of Action ──────────
            var readerType = typeof(PlayerInputReader);
            var dodgeEvt = readerType.GetEvent("DodgePressed",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok8 = dodgeEvt != null && dodgeEvt.EventHandlerType == typeof(System.Action);
            Check("8 PlayerInputReader.DodgePressed event of type System.Action", ok8,
                dodgeEvt == null
                    ? "event missing"
                    : $"handlerType='{dodgeEvt.EventHandlerType.Name}'");

            // ── 9: PlayerCombat.CancelAttack() public void ─────────────────
            var combatType = typeof(PlayerCombat);
            var cancelMethod = combatType.GetMethod("CancelAttack",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            bool ok9 = cancelMethod != null && cancelMethod.ReturnType == typeof(void);
            Check("9 PlayerCombat.CancelAttack() public void", ok9,
                cancelMethod != null
                    ? "method present"
                    : "method missing");

            // ── 10-11: Animator parameters ─────────────────────────────────
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null)
            {
                string detail = $"missing at {PlayerControllerPath}";
                Check("10 PlayerBaseController has parameter 'DodgeTrigger' (Trigger)",  false, detail);
                Check("11 PlayerBaseController has parameter 'DodgeDirection' (Int)",    false, detail);
                Check("12 PlayerBaseController has states RollFWD/BWD/LFT/RGT",           false, detail);
                Check("13 AnyState→Roll{FWD,BWD,LFT,RGT} transitions present",            false, detail);
                Check("14 Roll{X} → Locomotion exit transitions (exitTime=1.0)",         false, detail);
            }
            else
            {
                bool hasTrig = false, hasDir = false;
                string trigDetail = "parameter 'DodgeTrigger' not found";
                string dirDetail  = "parameter 'DodgeDirection' not found";
                foreach (var p in controller.parameters)
                {
                    if (p.name == "DodgeTrigger")
                    {
                        hasTrig = p.type == AnimatorControllerParameterType.Trigger;
                        trigDetail = hasTrig ? "type=Trigger" : $"type={p.type} (expected Trigger)";
                    }
                    if (p.name == "DodgeDirection")
                    {
                        hasDir = p.type == AnimatorControllerParameterType.Int;
                        dirDetail = hasDir ? "type=Int" : $"type={p.type} (expected Int)";
                    }
                }
                Check("10 PlayerBaseController has parameter 'DodgeTrigger' (Trigger)", hasTrig, trigDetail);
                Check("11 PlayerBaseController has parameter 'DodgeDirection' (Int)",    hasDir,  dirDetail);

                // ── 12: Roll{X} states present (walking sub-state-machines) ─
                AnimatorState rollFWD = null, rollBWD = null, rollLFT = null, rollRGT = null;
                if (controller.layers.Length > 0)
                {
                    var rootSm = controller.layers[0].stateMachine;
                    FindStateRecursive(rootSm, "RollFWD", ref rollFWD);
                    FindStateRecursive(rootSm, "RollBWD", ref rollBWD);
                    FindStateRecursive(rootSm, "RollLFT", ref rollLFT);
                    FindStateRecursive(rootSm, "RollRGT", ref rollRGT);
                }
                bool ok12 = rollFWD != null && rollBWD != null && rollLFT != null && rollRGT != null;
                Check("12 PlayerBaseController has states RollFWD/BWD/LFT/RGT", ok12,
                    $"FWD={(rollFWD != null ? "OK" : "missing")}, " +
                    $"BWD={(rollBWD != null ? "OK" : "missing")}, " +
                    $"LFT={(rollLFT != null ? "OK" : "missing")}, " +
                    $"RGT={(rollRGT != null ? "OK" : "missing")}");

                // ── 13: AnyState→Roll{X} transitions with DodgeTrigger condition ─
                int anyStateMatches = 0;
                if (controller.layers.Length > 0)
                {
                    var rootSm = controller.layers[0].stateMachine;
                    foreach (var t in rootSm.anyStateTransitions)
                    {
                        if (t.destinationState == null) continue;
                        if (t.destinationState != rollFWD
                            && t.destinationState != rollBWD
                            && t.destinationState != rollLFT
                            && t.destinationState != rollRGT) continue;
                        if (t.canTransitionToSelf) continue; // must be false
                        bool hasTriggerCond = false;
                        foreach (var c in t.conditions)
                            if (c.parameter == "DodgeTrigger") { hasTriggerCond = true; break; }
                        if (hasTriggerCond) anyStateMatches++;
                    }
                }
                bool ok13 = anyStateMatches >= 4;
                Check("13 AnyState→Roll{FWD,BWD,LFT,RGT} transitions present", ok13,
                    $"found {anyStateMatches}/4 with DodgeTrigger condition + canTransitionToSelf=false");

                // ── 14: Roll{X} → Locomotion exits with exitTime=1.0 ───────
                int rollExitCount = 0;
                AnimatorState locomotion = controller.layers[0].stateMachine != null
                    ? FindRootState(controller.layers[0].stateMachine, "Locomotion")
                    : null;
                if (locomotion != null)
                {
                    foreach (var roll in new[] { rollFWD, rollBWD, rollLFT, rollRGT })
                    {
                        if (roll == null) continue;
                        foreach (var t in roll.transitions)
                        {
                            if (t.destinationState != locomotion) continue;
                            if (!t.hasExitTime) continue;
                            if (!Mathf.Approximately(t.exitTime, 1.0f)) continue;
                            rollExitCount++;
                            break;
                        }
                    }
                }
                bool ok14 = rollExitCount == 4;
                Check("14 Roll{X} → Locomotion exit transitions (exitTime=1.0)", ok14,
                    locomotion == null
                        ? "Locomotion state missing"
                        : $"found {rollExitCount}/4 exit transitions");
            }

            // ── 15: Player_MaleHero.prefab has PlayerDodge ──────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerDodge dodgeComp = null;
            if (playerPrefab != null) dodgeComp = playerPrefab.GetComponent<PlayerDodge>();
            Check("15 Player_MaleHero.prefab has PlayerDodge on root", dodgeComp != null,
                playerPrefab == null
                    ? $"prefab missing at {PlayerPrefabPath}"
                    : (dodgeComp != null
                        ? "found on prefab root"
                        : "missing — run 'LevelGen ▶ Player ▶ Add PlayerDodge to Player_MaleHero Prefab'"));

            // ── 16: Player_MaleHero.prefab has CharacterController + StatsRuntime + InputReader ─
            bool ok16 = playerPrefab != null
                        && playerPrefab.GetComponent<CharacterController>()  != null
                        && playerPrefab.GetComponent<CharacterStatsRuntime>() != null
                        && playerPrefab.GetComponent<PlayerInputReader>()     != null;
            Check("16 Player_MaleHero RequireComponent prereqs satisfied", ok16,
                playerPrefab == null
                    ? "prefab missing"
                    : ($"CharacterController={(playerPrefab.GetComponent<CharacterController>()  != null ? "OK" : "MISSING")}, " +
                       $"StatsRuntime={(playerPrefab.GetComponent<CharacterStatsRuntime>()      != null ? "OK" : "MISSING")}, " +
                       $"InputReader={(playerPrefab.GetComponent<PlayerInputReader>()           != null ? "OK" : "MISSING")}"));

            // ── 17: PlayerCombat present (for CancelAttack delegate target) ─
            bool ok17 = playerPrefab != null && playerPrefab.GetComponent<PlayerCombat>() != null;
            Check("17 Player_MaleHero has PlayerCombat (needed for CancelAttack)", ok17,
                playerPrefab == null
                    ? "prefab missing"
                    : (ok17 ? "PlayerCombat present" : "missing — run 'LevelGen ▶ Player ▶ Add PlayerCombat to Player_MaleHero Prefab'"));

            Summary(pass, fail);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static AnimatorState FindRootState(AnimatorStateMachine sm, string name)
        {
            foreach (var sc in sm.states)
            {
                if (sc.state != null && sc.state.name == name) return sc.state;
            }
            return null;
        }

        private static void FindStateRecursive(AnimatorStateMachine sm, string name, ref AnimatorState found)
        {
            if (found != null || sm == null) return;
            foreach (var sc in sm.states)
            {
                if (sc.state != null && sc.state.name == name) { found = sc.state; return; }
            }
            foreach (var cm in sm.stateMachines)
            {
                FindStateRecursive(cm.stateMachine, name, ref found);
                if (found != null) return;
            }
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Player Dodge wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
