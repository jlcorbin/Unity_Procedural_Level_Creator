// PlayerJumpRuntimeValidator.cs — M2-B Step 5 validation pass.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Jump Runtime (M2-B Step 5)
//
// Reflection checks per the M2-B Step 5 prompt's validation requirements:
//   1. PlayerAnimator.SetJumpTrigger() public, void, no params.
//   2. PlayerAnimator.SetGrounded(bool) public, void, single bool param.
//   3. PlayerInputReader has public event JumpPressed.
//   4. PlayerController has private method OnJumpPressed.
//   5. Player_MaleHero.prefab has all four player components on its root.
//
// Read-only — does not modify any asset. Each check prints PASS or FAIL
// with detail; final SUMMARY line.
//
// If this file compiles, Step 5's source-level wiring also compiles
// (validator references the same symbols). A Unity-side compile error
// would fail this script first.

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Player;

namespace LevelGen.Player.EditorTools
{
    public static class PlayerJumpRuntimeValidator
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Validate Jump Runtime (M2-B Step 5)")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: PlayerAnimator.SetJumpTrigger() ──────────────────────────
            var animType = typeof(PlayerAnimator);
            var setJump = animType.GetMethod("SetJumpTrigger",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null, types: System.Type.EmptyTypes, modifiers: null);
            bool setJumpOk = setJump != null
                          && setJump.IsPublic
                          && setJump.ReturnType == typeof(void);
            Check("1 PlayerAnimator.SetJumpTrigger() public void", setJumpOk,
                setJump == null ? "missing or non-public"
                                : $"return={setJump.ReturnType.Name}, params={setJump.GetParameters().Length}");

            // ── 2: PlayerAnimator.SetGrounded(bool) ─────────────────────────
            var setGrounded = animType.GetMethod("SetGrounded",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null, types: new[] { typeof(bool) }, modifiers: null);
            bool setGroundedOk = setGrounded != null
                              && setGrounded.IsPublic
                              && setGrounded.ReturnType == typeof(void)
                              && setGrounded.GetParameters().Length == 1
                              && setGrounded.GetParameters()[0].ParameterType == typeof(bool);
            Check("2 PlayerAnimator.SetGrounded(bool) public void", setGroundedOk,
                setGrounded == null ? "missing or non-public"
                                    : $"return={setGrounded.ReturnType.Name}, params=[{string.Join(", ", System.Linq.Enumerable.Select(setGrounded.GetParameters(), p => p.ParameterType.Name))}]");

            // ── 3: PlayerInputReader.JumpPressed event ──────────────────────
            var inputType = typeof(PlayerInputReader);
            var jumpEvent = inputType.GetEvent("JumpPressed",
                BindingFlags.Public | BindingFlags.Instance);
            Check("3 PlayerInputReader.JumpPressed event public", jumpEvent != null,
                jumpEvent != null ? $"type={jumpEvent.EventHandlerType?.Name}"
                                  : "missing");

            // ── 4: PlayerController.OnJumpPressed (private) ─────────────────
            var ctrlType = typeof(PlayerController);
            var onJumpPressed = ctrlType.GetMethod("OnJumpPressed",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null, types: System.Type.EmptyTypes, modifiers: null);
            Check("4 PlayerController.OnJumpPressed() private method", onJumpPressed != null,
                onJumpPressed != null ? $"return={onJumpPressed.ReturnType.Name}, IsPrivate={onJumpPressed.IsPrivate}"
                                      : "missing");

            // ── 5: Prefab has all four player components on root ────────────
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Validator] FAIL — could not load prefab at {PrefabPath}");
                fail++;
            }
            else
            {
                Check("5a CharacterController on prefab root",
                    prefab.GetComponent<CharacterController>() != null, "present");
                Check("5b PlayerInputReader on prefab root",
                    prefab.GetComponent<PlayerInputReader>() != null, "present");
                Check("5c PlayerAnimator on prefab root",
                    prefab.GetComponent<PlayerAnimator>() != null, "present");
                Check("5d PlayerController on prefab root",
                    prefab.GetComponent<PlayerController>() != null, "present");
                Check("5e PlayerCombat on prefab root",
                    prefab.GetComponent<PlayerCombat>() != null, "present");
            }

            // ── Summary ─────────────────────────────────────────────────────
            string summary = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(summary + " — all M2-B Step 5 wiring checks passed.");
            else           Debug.LogError(summary + " — see FAIL lines above.");
        }
    }
}
#endif
