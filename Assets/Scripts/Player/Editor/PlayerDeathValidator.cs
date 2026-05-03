// PlayerDeathValidator.cs — read-only checks on M5 wiring.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Player Death
//
// 16 checks covering:
//   - PlayerDeath.cs presence + RequireComponent + DisallowMultiple
//   - OnPlayerDied event surface (Action<PlayerDeath>)
//   - PlayerAnimator.SetDeathTrigger() public surface
//   - PlayerAnimator.cs source contains "Death" string
//   - PlayerBaseController shape: Death param, Death state, terminal,
//     AnyState→Death with canTransitionToSelf=false
//   - PlayerCombat.cs source contains IsDead reference inside TakeHit
//   - Player_MaleHero.prefab has PlayerDeath with three refs wired
//   - PlayerDeathOverlay script + prefab present

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;
using LevelGen.UI;

namespace LevelGen.Player.Editor
{
    public static class PlayerDeathValidator
    {
        private const string PlayerDeathPath        = "Assets/Scripts/Player/PlayerDeath.cs";
        private const string PlayerAnimatorSrcPath  = "Assets/Scripts/Player/PlayerAnimator.cs";
        private const string PlayerCombatSrcPath    = "Assets/Scripts/Player/PlayerCombat.cs";
        private const string PlayerControllerPath   = "Assets/Animators/Player/PlayerBaseController.controller";
        private const string PlayerPrefabPath       = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const string OverlayScriptPath      = "Assets/Scripts/UI/PlayerDeathOverlay.cs";
        private const string OverlayPrefabPath      = "Assets/Prefabs/UI/PlayerDeathOverlay.prefab";

        [MenuItem("LevelGen/Player/Validate Player Death")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: PlayerDeath.cs at expected path ──────────────────────────
            bool ok1 = AssetDatabase.LoadAssetAtPath<MonoScript>(PlayerDeathPath) != null;
            Check("1 PlayerDeath.cs exists at expected path", ok1,
                ok1 ? PlayerDeathPath : $"missing at {PlayerDeathPath}");

            // ── 2-3: PlayerDeath attributes ─────────────────────────────────
            var deathType = typeof(PlayerDeath);
            var requireAttrs = deathType.GetCustomAttributes<RequireComponent>();
            bool requireStats = false;
            foreach (var a in requireAttrs)
            {
                if (a.m_Type0 == typeof(CharacterStatsRuntime)
                    || a.m_Type1 == typeof(CharacterStatsRuntime)
                    || a.m_Type2 == typeof(CharacterStatsRuntime))
                { requireStats = true; break; }
            }
            Check("2 PlayerDeath has [RequireComponent(typeof(CharacterStatsRuntime))]", requireStats,
                requireStats ? "attribute present" : "attribute missing");

            bool ok3 = deathType.GetCustomAttribute<DisallowMultipleComponent>() != null;
            Check("3 PlayerDeath has [DisallowMultipleComponent]", ok3,
                ok3 ? "attribute present" : "attribute missing");

            // ── 4: OnPlayerDied event of type Action<PlayerDeath> ───────────
            var onDiedEvent = deathType.GetEvent("OnPlayerDied",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok4 = onDiedEvent != null
                       && onDiedEvent.EventHandlerType == typeof(Action<PlayerDeath>);
            Check("4 PlayerDeath.OnPlayerDied event of type Action<PlayerDeath>", ok4,
                onDiedEvent == null
                    ? "event missing"
                    : $"handlerType='{onDiedEvent.EventHandlerType.Name}'");

            // ── 5: PlayerAnimator.SetDeathTrigger() public ──────────────────
            var animType = typeof(PlayerAnimator);
            var setDeathMethod = animType.GetMethod("SetDeathTrigger",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            bool ok5 = setDeathMethod != null
                       && setDeathMethod.ReturnType == typeof(void);
            Check("5 PlayerAnimator.SetDeathTrigger() public void", ok5,
                setDeathMethod != null
                    ? $"returns {setDeathMethod.ReturnType.Name}"
                    : "method missing");

            // ── 6: PlayerAnimator.cs source contains "Death" hash ───────────
            bool ok6 = false;
            string detail6 = $"source missing at {PlayerAnimatorSrcPath}";
            string animSrcFull = Path.Combine(Application.dataPath, "..", PlayerAnimatorSrcPath);
            if (File.Exists(animSrcFull))
            {
                string src = File.ReadAllText(animSrcFull);
                ok6 = src.Contains("_hashDeath") && src.Contains("\"Death\"");
                detail6 = ok6
                    ? "_hashDeath + \"Death\" const present"
                    : "missing _hashDeath or ParamDeath constant";
            }
            Check("6 PlayerAnimator.cs declares Death hash + parameter name", ok6, detail6);

            // ── 7-10: PlayerBaseController shape ────────────────────────────
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null)
            {
                string detail = $"missing at {PlayerControllerPath}";
                Check("7 PlayerBaseController has parameter 'Death' (Trigger)",      false, detail);
                Check("8 PlayerBaseController has state 'Death'",                    false, detail);
                Check("9 Death state has no outgoing transitions (terminal)",        false, detail);
                Check("10 AnyState → Death transition with canTransitionToSelf=false", false, detail);
            }
            else
            {
                // 7 — Death param Trigger
                bool ok7 = false;
                string detail7 = "parameter 'Death' not found";
                foreach (var p in controller.parameters)
                {
                    if (p.name == "Death")
                    {
                        ok7 = p.type == AnimatorControllerParameterType.Trigger;
                        detail7 = ok7 ? "type=Trigger" : $"type={p.type} (expected Trigger)";
                        break;
                    }
                }
                Check("7 PlayerBaseController has parameter 'Death' (Trigger)", ok7, detail7);

                // 8 — Death state
                AnimatorState deathState = null;
                if (controller.layers.Length > 0)
                {
                    var sm = controller.layers[0].stateMachine;
                    foreach (var sc in sm.states)
                    {
                        if (sc.state.name == "Death") { deathState = sc.state; break; }
                    }
                }
                Check("8 PlayerBaseController has state 'Death'", deathState != null,
                    deathState != null ? "found" : "missing — run 'Extend PlayerBaseController (M5 Death)'");

                // 9 — Death is terminal
                bool ok9 = deathState != null && deathState.transitions.Length == 0;
                Check("9 Death state has no outgoing transitions (terminal)", ok9,
                    deathState == null
                        ? "Death state missing — see check 8"
                        : $"outgoing transitions={deathState.transitions.Length} (expected 0)");

                // 10 — AnyState → Death, canTransitionToSelf=false
                bool ok10 = false;
                string detail10 = "no AnyState transition to Death";
                if (deathState != null && controller.layers.Length > 0)
                {
                    var sm = controller.layers[0].stateMachine;
                    foreach (var t in sm.anyStateTransitions)
                    {
                        if (t.destinationState == deathState)
                        {
                            ok10 = !t.canTransitionToSelf;
                            detail10 = ok10
                                ? "found, canTransitionToSelf=false"
                                : $"found, canTransitionToSelf={t.canTransitionToSelf} (expected false)";
                            break;
                        }
                    }
                }
                Check("10 AnyState → Death transition with canTransitionToSelf=false", ok10, detail10);
            }

            // ── 11: PlayerCombat.TakeHit contains IsDead guard ──────────────
            bool ok11 = false;
            string detail11 = $"source missing at {PlayerCombatSrcPath}";
            string combatSrcFull = Path.Combine(Application.dataPath, "..", PlayerCombatSrcPath);
            if (File.Exists(combatSrcFull))
            {
                string src = File.ReadAllText(combatSrcFull);
                int takeHitIdx = src.IndexOf("public void TakeHit", StringComparison.Ordinal);
                if (takeHitIdx >= 0)
                {
                    // Look at the next ~600 chars for the guard.
                    int sliceEnd = Math.Min(src.Length, takeHitIdx + 600);
                    string slice = src.Substring(takeHitIdx, sliceEnd - takeHitIdx);
                    ok11 = slice.Contains("IsDead");
                    detail11 = ok11
                        ? "IsDead reference found inside TakeHit body"
                        : "TakeHit body has no IsDead guard";
                }
                else
                {
                    detail11 = "TakeHit method not found in PlayerCombat.cs";
                }
            }
            Check("11 PlayerCombat.TakeHit contains IsDead guard", ok11, detail11);

            // ── 12-15: Player_MaleHero.prefab PlayerDeath wiring ────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerDeath deathComp = null;
            if (playerPrefab != null) deathComp = playerPrefab.GetComponent<PlayerDeath>();

            Check("12 Player_MaleHero.prefab has PlayerDeath on root", deathComp != null,
                playerPrefab == null
                    ? $"prefab missing at {PlayerPrefabPath}"
                    : (deathComp != null
                        ? "found on prefab root"
                        : "missing — run 'LevelGen ▶ Player ▶ Add PlayerDeath to Player_MaleHero Prefab'"));

            if (deathComp == null)
            {
                Check("13 PlayerDeath._animator field non-null",   false, "PlayerDeath missing — see check 12");
                Check("14 PlayerDeath._controller field non-null", false, "PlayerDeath missing — see check 12");
                Check("15 PlayerDeath._combat field non-null",     false, "PlayerDeath missing — see check 12");
            }
            else
            {
                var so = new SerializedObject(deathComp);

                var animProp = so.FindProperty("_animator");
                var ctrlProp = so.FindProperty("_controller");
                var combProp = so.FindProperty("_combat");

                var animVal = animProp != null ? animProp.objectReferenceValue as PlayerAnimator    : null;
                var ctrlVal = ctrlProp != null ? ctrlProp.objectReferenceValue as PlayerController  : null;
                var combVal = combProp != null ? combProp.objectReferenceValue as PlayerCombat      : null;

                Check("13 PlayerDeath._animator field non-null", animVal != null,
                    animVal != null
                        ? $"wired to PlayerAnimator on '{animVal.gameObject.name}'"
                        : "field unassigned");
                Check("14 PlayerDeath._controller field non-null", ctrlVal != null,
                    ctrlVal != null
                        ? $"wired to PlayerController on '{ctrlVal.gameObject.name}'"
                        : "field unassigned");
                Check("15 PlayerDeath._combat field non-null", combVal != null,
                    combVal != null
                        ? $"wired to PlayerCombat on '{combVal.gameObject.name}'"
                        : "field unassigned — re-run 'Add PlayerDeath' AFTER 'Add PlayerCombat'");
            }

            // ── 16: Overlay script + prefab present ─────────────────────────
            bool overlayScriptOk = AssetDatabase.LoadAssetAtPath<MonoScript>(OverlayScriptPath) != null;
            bool overlayPrefabOk = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath) != null;
            bool ok16 = overlayScriptOk && overlayPrefabOk;
            Check("16 PlayerDeathOverlay script + prefab exist", ok16,
                $"script={(overlayScriptOk ? "OK" : "missing")}, " +
                $"prefab={(overlayPrefabOk ? "OK" : "missing — run 'LevelGen ▶ UI ▶ Build PlayerDeathOverlay Prefab'")}");

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Player Death wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
