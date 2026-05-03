// DummyAndStatsValidator.cs — read-only checks on the combat foundation.
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Validate Combat Foundation
//
// 12 checks covering:
//   - Type definitions (CharacterStats, CharacterStatsRuntime, Targetable)
//   - Stats assets at expected paths with expected values
//   - Dummy prefab structure (positive + negative component checks)
//   - Animator on Dummy references PlayerBaseController
//
// Pattern mirrors PlayerCombatValidator.cs.

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Combat.EditorTools
{
    public static class DummyAndStatsValidator
    {
        private const string MasterStatsPath = "Assets/Data/CharacterStats/CharacterStats_Master.asset";
        private const string DummyStatsPath  = "Assets/Data/CharacterStats/CharacterStats_Dummy.asset";
        private const string DummyPrefabPath = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        // M4-A swapped Dummy from PlayerBaseController to EnemyBaseController.
        // Path updated to keep the assertion meaningful as the spec evolved.
        private const string BaseCtrlPath    = "Assets/Animators/Enemy/EnemyBaseController.controller";

        [MenuItem("LevelGen/Combat/Validate Combat Foundation")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: CharacterStats type ──────────────────────────────────────
            var statsType = typeof(CharacterStats);
            Check("1 CharacterStats inherits from ScriptableObject",
                typeof(ScriptableObject).IsAssignableFrom(statsType),
                statsType.BaseType?.FullName ?? "<null>");

            // ── 2: CharacterStatsRuntime type ───────────────────────────────
            var runtimeType = typeof(CharacterStatsRuntime);
            Check("2 CharacterStatsRuntime inherits from MonoBehaviour",
                typeof(MonoBehaviour).IsAssignableFrom(runtimeType),
                runtimeType.BaseType?.FullName ?? "<null>");

            // ── 3: Targetable type ──────────────────────────────────────────
            var targetableType = typeof(Targetable);
            Check("3 Targetable inherits from MonoBehaviour",
                typeof(MonoBehaviour).IsAssignableFrom(targetableType),
                targetableType.BaseType?.FullName ?? "<null>");

            // ── 4: Master stats asset exists ────────────────────────────────
            var masterStats = AssetDatabase.LoadAssetAtPath<CharacterStats>(MasterStatsPath);
            Check("4 CharacterStats_Master.asset exists",
                masterStats != null,
                masterStats != null ? MasterStatsPath : $"missing at {MasterStatsPath}");

            // ── 5: Dummy stats asset exists ─────────────────────────────────
            var dummyStats = AssetDatabase.LoadAssetAtPath<CharacterStats>(DummyStatsPath);
            Check("5 CharacterStats_Dummy.asset exists",
                dummyStats != null,
                dummyStats != null ? DummyStatsPath : $"missing at {DummyStatsPath}");

            // ── 6: Master values match spec ─────────────────────────────────
            if (masterStats != null)
            {
                bool ok = masterStats.displayName == "Master Template"
                          && masterStats.maxHP      == 100
                          && masterStats.maxStamina == 100;
                Check("6 Master values match spec", ok,
                    $"displayName='{masterStats.displayName}', " +
                    $"maxHP={masterStats.maxHP}, maxStamina={masterStats.maxStamina} " +
                    "(expected: 'Master Template', 100, 100)");
            }
            else
            {
                Check("6 Master values match spec", false, "asset missing — see check 4");
            }

            // ── 7: Dummy values match spec ──────────────────────────────────
            // M4-B dropped Dummy HP from 999 → 50 so the death pipeline
            // triggers organically from a 2-combo chain (3-hit combo = 30 dmg).
            if (dummyStats != null)
            {
                bool ok = dummyStats.displayName == "Dummy"
                          && dummyStats.maxHP      == 50
                          && dummyStats.maxStamina == 100;
                Check("7 Dummy values match spec", ok,
                    $"displayName='{dummyStats.displayName}', " +
                    $"maxHP={dummyStats.maxHP}, maxStamina={dummyStats.maxStamina} " +
                    "(expected: 'Dummy', 50, 100)");
            }
            else
            {
                Check("7 Dummy values match spec", false, "asset missing — see check 5");
            }

            // ── 8: Dummy prefab exists ──────────────────────────────────────
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            Check("8 Dummy.prefab exists",
                dummyPrefab != null,
                dummyPrefab != null ? DummyPrefabPath : $"missing at {DummyPrefabPath}");

            if (dummyPrefab == null)
            {
                Summary(pass, fail);
                return;
            }

            // ── 9: Root has CharacterStatsRuntime → CharacterStats_Dummy ────
            var runtime = dummyPrefab.GetComponent<CharacterStatsRuntime>();
            if (runtime == null)
            {
                Check("9 CharacterStatsRuntime on Dummy root with stats=Dummy",
                    false, "CharacterStatsRuntime missing");
            }
            else
            {
                // stats is a private SerializeField — read via SerializedObject.
                var so = new SerializedObject(runtime);
                var prop = so.FindProperty("stats");
                var assigned = prop != null ? prop.objectReferenceValue as CharacterStats : null;
                bool ok = assigned != null && assigned == dummyStats;
                Check("9 CharacterStatsRuntime on Dummy root with stats=Dummy",
                    ok,
                    assigned != null ? $"stats='{assigned.name}'" : "stats field unassigned or wrong asset");
            }

            // ── 10: Root has Targetable ─────────────────────────────────────
            Check("10 Targetable on Dummy root",
                dummyPrefab.GetComponent<Targetable>() != null,
                dummyPrefab.GetComponent<Targetable>() != null ? "present" : "missing");

            // ── 11: Negative checks — no player control scripts ─────────────
            (string label, bool present)[] forbidden =
            {
                ("PlayerInputReader",   dummyPrefab.GetComponent<PlayerInputReader>()    != null),
                ("PlayerController",    dummyPrefab.GetComponent<PlayerController>()     != null),
                ("PlayerCombat",        dummyPrefab.GetComponent<PlayerCombat>()         != null),
                ("PlayerAnimator",      dummyPrefab.GetComponent<PlayerAnimator>()       != null),
                ("PlayerSpawner",       dummyPrefab.GetComponent<PlayerSpawner>()        != null),
                ("CharacterController", dummyPrefab.GetComponent<CharacterController>()  != null),
            };
            var sb = new System.Text.StringBuilder();
            bool anyForbiddenPresent = false;
            foreach (var (label, present) in forbidden)
            {
                sb.Append(label).Append('=').Append(present ? "PRESENT" : "absent").Append("; ");
                if (present) anyForbiddenPresent = true;
            }
            Check("11 No player control scripts on Dummy root",
                !anyForbiddenPresent,
                sb.ToString().TrimEnd(';', ' '));

            // ── 12: Animator references PlayerBaseController ────────────────
            var animator = dummyPrefab.GetComponentInChildren<Animator>();
            var baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseCtrlPath);
            string animDetail;
            bool animOk;
            if (animator == null)
            {
                animOk = false;
                animDetail = "no Animator anywhere in hierarchy";
            }
            else if (animator.runtimeAnimatorController == null)
            {
                animOk = false;
                animDetail = "Animator.runtimeAnimatorController is null";
            }
            else
            {
                // The Animator field type is RuntimeAnimatorController; comparing
                // to AnimatorController works because EnemyBaseController IS an
                // AnimatorController (not an override).
                animOk = baseCtrl != null && animator.runtimeAnimatorController == baseCtrl;
                animDetail = $"controller='{animator.runtimeAnimatorController.name}' " +
                             $"(expected: 'EnemyBaseController' at {BaseCtrlPath})";
            }
            Check("12 Animator references EnemyBaseController", animOk, animDetail);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — combat foundation OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
