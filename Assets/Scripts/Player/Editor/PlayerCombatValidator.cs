// PlayerCombatValidator.cs — M2-B Step 3 validation pass.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate PlayerCombat Wiring (M2-B Step 3)
//
// Read-only checks:
//   1. PlayerAnimator.SetAttackTrigger() exists & public.
//   2. PlayerAnimator.SetHitTrigger() exists & public.
//   3. PlayerInputReader.AttackPressed event exists & public.
//   4. PlayerCombat type exists; TakeHit() public; ContextMenu attribute present.
//   5. Player_MaleHero.prefab has PlayerCombat on root.
//   6. Prefab root also has PlayerInputReader, PlayerAnimator, PlayerController,
//      CharacterController (sanity).
//
// If this script compiles AND the type-resolution checks pass, the runtime
// scripts compile cleanly — the script itself depends on the symbols it's
// validating, so a Unity-side compile error would fail this script first.

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Player;

namespace LevelGen.Player.EditorTools
{
    public static class PlayerCombatValidator
    {
        private const string PrefabPath = "Assets/Prefabs/Player/Player_MaleHero.prefab";

        [MenuItem("LevelGen/Player/Validate PlayerCombat Wiring (M2-B Step 3)")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1/2: PlayerAnimator API surface ─────────────────────────────
            var animType = typeof(PlayerAnimator);
            var setAttack = animType.GetMethod("SetAttackTrigger", BindingFlags.Public | BindingFlags.Instance);
            var setHit    = animType.GetMethod("SetHitTrigger",    BindingFlags.Public | BindingFlags.Instance);
            Check("1 PlayerAnimator.SetAttackTrigger() public", setAttack != null,
                setAttack != null ? "found" : "missing or non-public");
            Check("2 PlayerAnimator.SetHitTrigger() public", setHit != null,
                setHit != null ? "found" : "missing or non-public");

            // ── 3: PlayerInputReader.AttackPressed event ────────────────────
            var inputType = typeof(PlayerInputReader);
            var evt = inputType.GetEvent("AttackPressed", BindingFlags.Public | BindingFlags.Instance);
            Check("3 PlayerInputReader.AttackPressed event public", evt != null,
                evt != null ? $"type={evt.EventHandlerType?.Name}" : "missing");

            // ── 4: PlayerCombat type + TakeHit() + ContextMenu ──────────────
            var combatType = typeof(PlayerCombat);
            var takeHit = combatType.GetMethod("TakeHit", BindingFlags.Public | BindingFlags.Instance);
            bool hasCtx = false;
            if (takeHit != null)
            {
                var attrs = takeHit.GetCustomAttributes(typeof(ContextMenu), false);
                hasCtx = attrs.Length > 0;
            }
            Check("4a PlayerCombat.TakeHit() public", takeHit != null,
                takeHit != null ? "found" : "missing or non-public");
            Check("4b PlayerCombat.TakeHit() has [ContextMenu]", hasCtx,
                hasCtx ? "attribute present" : "missing — Inspector right-click won't expose Take Hit");

            // ── 5: Prefab has PlayerCombat ──────────────────────────────────
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Validator] FAIL — could not load prefab at {PrefabPath}");
                fail++;
            }
            else
            {
                var combat = prefab.GetComponent<PlayerCombat>();
                Check("5 PlayerCombat on Player_MaleHero prefab root", combat != null,
                    combat != null ? "present" : "missing — run 'Add PlayerCombat to Player_MaleHero Prefab' menu item");

                // ── 6: sibling components for sanity ────────────────────────
                Check("6a PlayerInputReader on prefab root",
                    prefab.GetComponent<PlayerInputReader>() != null, "present-or-missing");
                Check("6b PlayerAnimator on prefab root",
                    prefab.GetComponent<PlayerAnimator>() != null, "present-or-missing");
                Check("6c PlayerController on prefab root",
                    prefab.GetComponent<PlayerController>() != null, "present-or-missing");
                Check("6d CharacterController on prefab root",
                    prefab.GetComponent<CharacterController>() != null, "present-or-missing");
            }

            // ── Summary ─────────────────────────────────────────────────────
            string summary = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(summary + " — all M2-B Step 3 checks passed.");
            else           Debug.LogError(summary + " — see FAIL lines above.");
        }
    }
}
#endif
