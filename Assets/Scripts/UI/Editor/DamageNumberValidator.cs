// DamageNumberValidator.cs — read-only checks on M8 wiring.
//
// Single menu item:
//   LevelGen ▶ UI ▶ Validate Damage Numbers
//
// 14 checks covering:
//   - Targetable.OnHit + AnyTargetableHit + RaiseHit signatures (literal-stub
//     matches per M6 lesson — no fixed-width slice scans)
//   - EnemyHitReaction.HandleHit signature reflects new (Vector3, float)
//   - PlayerCombat.cs source contains the two-arg RaiseHit call
//   - DamageNumber + DamageNumberSpawner script presence + attributes
//   - DamageNumberSpawner subscribe + unsubscribe pair (leak prevention)
//   - DamageNumber + DamageNumberSpawner prefabs exist with refs wired

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.UI;

namespace LevelGen.UI.EditorTools
{
    public static class DamageNumberValidator
    {
        private const string TargetableSrcPath        = "Assets/Scripts/Combat/Targetable.cs";
        private const string EnemyHitReactionSrcPath  = "Assets/Scripts/Combat/EnemyHitReaction.cs";
        private const string PlayerCombatSrcPath      = "Assets/Scripts/Player/PlayerCombat.cs";
        private const string DamageNumberSrcPath      = "Assets/Scripts/UI/DamageNumber.cs";
        private const string DamageNumberSpawnerSrcPath = "Assets/Scripts/UI/DamageNumberSpawner.cs";
        private const string DamageNumberPrefabPath   = "Assets/Prefabs/UI/DamageNumber.prefab";
        private const string SpawnerPrefabPath        = "Assets/Prefabs/UI/DamageNumberSpawner.prefab";

        [MenuItem("LevelGen/UI/Validate Damage Numbers")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: Targetable.cs source contains the new event declaration ──
            // Direct literal-stub match (M6 lesson — no slice scans).
            string targetableSrcFull = Path.Combine(Application.dataPath, "..", TargetableSrcPath);
            bool ok1 = false;
            string detail1 = $"source missing at {TargetableSrcPath}";
            string targetableSrc = null;
            if (File.Exists(targetableSrcFull))
            {
                targetableSrc = File.ReadAllText(targetableSrcFull);
                ok1 = targetableSrc.Contains("event Action<Vector3, float> OnHit");
                detail1 = ok1
                    ? "OnHit event declared as Action<Vector3, float>"
                    : "event Action<Vector3, float> OnHit declaration not found";
            }
            Check("1 Targetable.cs declares 'event Action<Vector3, float> OnHit'", ok1, detail1);

            // ── 2: Targetable.cs source contains the static event ──────────
            bool ok2 = false;
            string detail2 = $"source missing at {TargetableSrcPath}";
            if (targetableSrc != null)
            {
                ok2 = targetableSrc.Contains("static event Action<Vector3, float> AnyTargetableHit");
                detail2 = ok2
                    ? "AnyTargetableHit static event declared"
                    : "static event Action<Vector3, float> AnyTargetableHit not found";
            }
            Check("2 Targetable.cs declares 'static event Action<Vector3, float> AnyTargetableHit'", ok2, detail2);

            // ── 3: Targetable.RaiseHit signature is (Vector3, float) ────────
            var targetableType = typeof(Targetable);
            var raiseHitMethod = targetableType.GetMethod("RaiseHit",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            bool ok3 = raiseHitMethod != null && raiseHitMethod.ReturnType == typeof(void);
            Check("3 Targetable.RaiseHit(Vector3, float) public void", ok3,
                raiseHitMethod != null
                    ? $"returns {raiseHitMethod.ReturnType.Name}"
                    : "method missing or wrong signature");

            // ── 4: RaiseHit body invokes both events ────────────────────────
            bool ok4 = false;
            string detail4 = $"source missing at {TargetableSrcPath}";
            if (targetableSrc != null)
            {
                bool hasInstance = targetableSrc.Contains("OnHit?.Invoke(hitPoint, damage)");
                bool hasStatic   = targetableSrc.Contains("AnyTargetableHit?.Invoke(hitPoint, damage)");
                ok4 = hasInstance && hasStatic;
                detail4 = ok4
                    ? "OnHit and AnyTargetableHit both invoked from RaiseHit"
                    : $"OnHit?.Invoke(hitPoint, damage)={hasInstance}, " +
                      $"AnyTargetableHit?.Invoke(hitPoint, damage)={hasStatic}";
            }
            Check("4 Targetable.RaiseHit invokes both OnHit and AnyTargetableHit", ok4, detail4);

            // ── 5: EnemyHitReaction.HandleHit(Vector3, float) ───────────────
            var hitReactionType = typeof(EnemyHitReaction);
            var handleHitMethod = hitReactionType.GetMethod("HandleHit",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            bool ok5 = handleHitMethod != null;
            Check("5 EnemyHitReaction.HandleHit signature is (Vector3, float)", ok5,
                ok5 ? "found" : "method missing or wrong signature — Step 3 wiring not landed");

            // ── 6: PlayerCombat.cs contains two-arg RaiseHit call ───────────
            // Direct literal-stub match (M6 lesson) for the new call form.
            bool ok6 = false;
            string detail6 = $"source missing at {PlayerCombatSrcPath}";
            string playerCombatSrcFull = Path.Combine(Application.dataPath, "..", PlayerCombatSrcPath);
            if (File.Exists(playerCombatSrcFull))
            {
                string src = File.ReadAllText(playerCombatSrcFull);
                ok6 = src.Contains("targetable.RaiseHit(hitPoint, dmg)");
                detail6 = ok6
                    ? "targetable.RaiseHit(hitPoint, dmg) call present"
                    : "two-arg RaiseHit call not found — Step 2 wiring not landed";
            }
            Check("6 PlayerCombat.cs contains 'targetable.RaiseHit(hitPoint, dmg)'", ok6, detail6);

            // ── 7: DamageNumber.cs at expected path + RequireComponent(TMP_Text) ──
            bool ok7 = AssetDatabase.LoadAssetAtPath<MonoScript>(DamageNumberSrcPath) != null;
            string detail7 = ok7 ? DamageNumberSrcPath : $"missing at {DamageNumberSrcPath}";
            if (ok7)
            {
                var dnType = typeof(DamageNumber);
                var requires = dnType.GetCustomAttributes<RequireComponent>();
                bool hasTmpRequire = false;
                foreach (var a in requires)
                {
                    if (a.m_Type0 == typeof(TMP_Text)
                        || a.m_Type1 == typeof(TMP_Text)
                        || a.m_Type2 == typeof(TMP_Text))
                    { hasTmpRequire = true; break; }
                }
                ok7 = hasTmpRequire;
                detail7 = hasTmpRequire
                    ? "DamageNumber.cs present with [RequireComponent(typeof(TMP_Text))]"
                    : "DamageNumber.cs present but missing [RequireComponent(typeof(TMP_Text))]";
            }
            Check("7 DamageNumber.cs at expected path + [RequireComponent(TMP_Text)]", ok7, detail7);

            // ── 8: DamageNumber.Initialize(Vector3, float) public ───────────
            var initMethod = typeof(DamageNumber).GetMethod("Initialize",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(float) }, null);
            Check("8 DamageNumber.Initialize(Vector3, float) public", initMethod != null,
                initMethod != null ? "found" : "method missing or wrong signature");

            // ── 9: DamageNumberSpawner.cs at expected path + Instance ───────
            bool ok9 = AssetDatabase.LoadAssetAtPath<MonoScript>(DamageNumberSpawnerSrcPath) != null;
            string detail9 = ok9 ? DamageNumberSpawnerSrcPath : $"missing at {DamageNumberSpawnerSrcPath}";
            if (ok9)
            {
                var spawnerType = typeof(DamageNumberSpawner);
                var instanceProp = spawnerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                ok9 = instanceProp != null && instanceProp.PropertyType == typeof(DamageNumberSpawner);
                detail9 = ok9
                    ? "DamageNumberSpawner.cs present with static Instance property"
                    : "DamageNumberSpawner.cs present but static Instance property missing";
            }
            Check("9 DamageNumberSpawner.cs at expected path + static Instance property", ok9, detail9);

            // ── 10: Spawner subscribes AND unsubscribes — leak prevention ───
            bool ok10 = false;
            string detail10 = $"source missing at {DamageNumberSpawnerSrcPath}";
            string spawnerSrcFull = Path.Combine(Application.dataPath, "..", DamageNumberSpawnerSrcPath);
            if (File.Exists(spawnerSrcFull))
            {
                string src = File.ReadAllText(spawnerSrcFull);
                bool hasSub   = src.Contains("Targetable.AnyTargetableHit += HandleAnyHit");
                bool hasUnsub = src.Contains("Targetable.AnyTargetableHit -= HandleAnyHit");
                ok10 = hasSub && hasUnsub;
                detail10 = ok10
                    ? "subscribes (+=) and unsubscribes (-=) HandleAnyHit"
                    : $"subscribe={hasSub}, unsubscribe={hasUnsub} (both required to prevent static-event leaks)";
            }
            Check("10 DamageNumberSpawner.cs subscribes and unsubscribes HandleAnyHit", ok10, detail10);

            // ── 11: DamageNumber.prefab exists ──────────────────────────────
            var dnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath);
            bool ok11 = dnPrefab != null;
            Check("11 DamageNumber.prefab exists", ok11,
                ok11 ? DamageNumberPrefabPath
                     : $"missing at {DamageNumberPrefabPath} — run 'LevelGen ▶ UI ▶ Build DamageNumber Prefab'");

            // ── 12: DamageNumberSpawner.prefab exists ───────────────────────
            var spawnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnerPrefabPath);
            bool ok12 = spawnerPrefab != null;
            Check("12 DamageNumberSpawner.prefab exists", ok12,
                ok12 ? SpawnerPrefabPath
                     : $"missing at {SpawnerPrefabPath} — run 'LevelGen ▶ UI ▶ Build DamageNumberSpawner Prefab'");

            // ── 13: Spawner prefab _damageNumberPrefab field wired ──────────
            bool ok13 = false;
            string detail13 = "spawner prefab missing — see check 12";
            if (spawnerPrefab != null)
            {
                var spawner = spawnerPrefab.GetComponent<DamageNumberSpawner>();
                if (spawner == null)
                {
                    detail13 = "spawner prefab has no DamageNumberSpawner component";
                }
                else
                {
                    var so = new SerializedObject(spawner);
                    var prop = so.FindProperty("_damageNumberPrefab");
                    var assigned = prop != null ? prop.objectReferenceValue as DamageNumber : null;
                    ok13 = assigned != null;
                    detail13 = ok13
                        ? $"wired to '{assigned.name}'"
                        : "_damageNumberPrefab unassigned — re-run 'Build DamageNumberSpawner Prefab'";
                }
            }
            Check("13 DamageNumberSpawner._damageNumberPrefab wired", ok13, detail13);

            // ── 14: EnemyHitReaction.cs source uses (Vector3, float) handler ─
            // Belt-and-suspenders source-scan in addition to the reflection
            // check above. M6 lesson — direct literal stub, no slice.
            bool ok14 = false;
            string detail14 = $"source missing at {EnemyHitReactionSrcPath}";
            string ehrSrcFull = Path.Combine(Application.dataPath, "..", EnemyHitReactionSrcPath);
            if (File.Exists(ehrSrcFull))
            {
                string src = File.ReadAllText(ehrSrcFull);
                ok14 = src.Contains("private void HandleHit(Vector3 hitPoint, float damage)");
                detail14 = ok14
                    ? "HandleHit declared as (Vector3 hitPoint, float damage)"
                    : "HandleHit declaration with new (Vector3, float) signature not found";
            }
            Check("14 EnemyHitReaction.cs declares HandleHit(Vector3, float)", ok14, detail14);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Damage numbers wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
