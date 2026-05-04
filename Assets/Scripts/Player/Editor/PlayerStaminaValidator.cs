// PlayerStaminaValidator.cs — read-only checks on M9 wiring.
//
// Single menu item:
//   LevelGen ▶ Player ▶ Validate Player Stamina
//
// 12 read-only checks covering:
//   - CharacterStats SO source contains the new rate fields + accessors
//     (literal-stub matches per M6 lesson — no fixed-width slice scans)
//   - CharacterStatsRuntime.SpendStamina / RegenStamina public methods
//     (reflection)
//   - PlayerStamina script + RequireComponent attributes + CanSprint
//     property
//   - PlayerController source consults _stamina.CanSprint
//     (literal-stub match)
//   - Player_MaleHero.prefab carries PlayerStamina
//   - CharacterStats_Player.asset has rates > 0 (so stamina actually
//     moves at runtime — catches the "asset wasn't run through the
//     updater" misconfig)

#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player.EditorTools
{
    public static class PlayerStaminaValidator
    {
        private const string CharacterStatsSrcPath        = "Assets/Scripts/Combat/CharacterStats.cs";
        private const string CharacterStatsRuntimeSrcPath = "Assets/Scripts/Combat/CharacterStatsRuntime.cs";
        private const string PlayerStaminaSrcPath         = "Assets/Scripts/Player/PlayerStamina.cs";
        private const string PlayerControllerSrcPath      = "Assets/Scripts/Player/PlayerController.cs";
        private const string PlayerPrefabPath             = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const string PlayerStatsAssetPath         = "Assets/Data/CharacterStats/CharacterStats_Player.asset";

        [MenuItem("LevelGen/Player/Validate Player Stamina")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: CharacterStats.cs source contains _staminaDrainPerSecond field ──
            string statsSrcFull = Path.Combine(Application.dataPath, "..", CharacterStatsSrcPath);
            bool ok1 = false;
            string detail1 = $"source missing at {CharacterStatsSrcPath}";
            string statsSrc = null;
            if (File.Exists(statsSrcFull))
            {
                statsSrc = File.ReadAllText(statsSrcFull);
                ok1 = statsSrc.Contains("_staminaDrainPerSecond");
                detail1 = ok1
                    ? "_staminaDrainPerSecond field present"
                    : "_staminaDrainPerSecond field declaration not found";
            }
            Check("1 CharacterStats.cs declares _staminaDrainPerSecond", ok1, detail1);

            // ── 2: CharacterStats.cs source contains _staminaRegenPerSecond field ──
            bool ok2 = false;
            string detail2 = $"source missing at {CharacterStatsSrcPath}";
            if (statsSrc != null)
            {
                ok2 = statsSrc.Contains("_staminaRegenPerSecond");
                detail2 = ok2
                    ? "_staminaRegenPerSecond field present"
                    : "_staminaRegenPerSecond field declaration not found";
            }
            Check("2 CharacterStats.cs declares _staminaRegenPerSecond", ok2, detail2);

            // ── 3: CharacterStats declares public StaminaDrainPerSecond accessor ──
            var statsType = typeof(CharacterStats);
            var drainProp = statsType.GetProperty("StaminaDrainPerSecond",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok3 = drainProp != null && drainProp.PropertyType == typeof(float);
            Check("3 CharacterStats.StaminaDrainPerSecond public float accessor", ok3,
                drainProp != null
                    ? $"PropertyType={drainProp.PropertyType.Name}"
                    : "accessor missing");

            // ── 4: CharacterStats declares public StaminaRegenPerSecond accessor ──
            var regenProp = statsType.GetProperty("StaminaRegenPerSecond",
                BindingFlags.Public | BindingFlags.Instance);
            bool ok4 = regenProp != null && regenProp.PropertyType == typeof(float);
            Check("4 CharacterStats.StaminaRegenPerSecond public float accessor", ok4,
                regenProp != null
                    ? $"PropertyType={regenProp.PropertyType.Name}"
                    : "accessor missing");

            // ── 5: CharacterStatsRuntime.SpendStamina(float) public ──────────
            var runtimeType = typeof(CharacterStatsRuntime);
            var spendMethod = runtimeType.GetMethod("SpendStamina",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(float) }, null);
            bool ok5 = spendMethod != null && spendMethod.ReturnType == typeof(void);
            Check("5 CharacterStatsRuntime.SpendStamina(float) public void", ok5,
                spendMethod != null
                    ? $"returns {spendMethod.ReturnType.Name}"
                    : "method missing or wrong signature");

            // ── 6: CharacterStatsRuntime.RegenStamina(float) public ──────────
            var regenMethod = runtimeType.GetMethod("RegenStamina",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(float) }, null);
            bool ok6 = regenMethod != null && regenMethod.ReturnType == typeof(void);
            Check("6 CharacterStatsRuntime.RegenStamina(float) public void", ok6,
                regenMethod != null
                    ? $"returns {regenMethod.ReturnType.Name}"
                    : "method missing or wrong signature");

            // ── 7: PlayerStamina.cs at expected path ─────────────────────────
            bool ok7 = AssetDatabase.LoadAssetAtPath<MonoScript>(PlayerStaminaSrcPath) != null;
            Check("7 PlayerStamina.cs exists at expected path", ok7,
                ok7 ? PlayerStaminaSrcPath : $"missing at {PlayerStaminaSrcPath}");

            // ── 8: PlayerStamina has [RequireComponent(CharacterStatsRuntime)] AND [RequireComponent(PlayerController)] AND [DisallowMultipleComponent] ──
            bool ok8 = false;
            string detail8 = "PlayerStamina type missing — see check 7";
            if (ok7)
            {
                var staminaType = typeof(PlayerStamina);
                var requires = staminaType.GetCustomAttributes<RequireComponent>();
                bool hasRuntime    = false;
                bool hasController = false;
                foreach (var a in requires)
                {
                    if (a.m_Type0 == typeof(CharacterStatsRuntime)
                        || a.m_Type1 == typeof(CharacterStatsRuntime)
                        || a.m_Type2 == typeof(CharacterStatsRuntime)) hasRuntime = true;
                    if (a.m_Type0 == typeof(PlayerController)
                        || a.m_Type1 == typeof(PlayerController)
                        || a.m_Type2 == typeof(PlayerController)) hasController = true;
                }
                bool hasDisallow = staminaType.GetCustomAttribute<DisallowMultipleComponent>() != null;
                ok8 = hasRuntime && hasController && hasDisallow;
                detail8 = ok8
                    ? "all three attributes present"
                    : $"RequireComponent(CharacterStatsRuntime)={hasRuntime}, " +
                      $"RequireComponent(PlayerController)={hasController}, " +
                      $"DisallowMultipleComponent={hasDisallow}";
            }
            Check("8 PlayerStamina has RequireComponent + DisallowMultiple attributes", ok8, detail8);

            // ── 9: PlayerStamina.CanSprint public bool property ──────────────
            bool ok9 = false;
            string detail9 = "PlayerStamina type missing — see check 7";
            if (ok7)
            {
                var canSprintProp = typeof(PlayerStamina).GetProperty("CanSprint",
                    BindingFlags.Public | BindingFlags.Instance);
                ok9 = canSprintProp != null && canSprintProp.PropertyType == typeof(bool);
                detail9 = canSprintProp != null
                    ? $"PropertyType={canSprintProp.PropertyType.Name}"
                    : "property missing";
            }
            Check("9 PlayerStamina.CanSprint public bool property", ok9, detail9);

            // ── 10: PlayerController.cs source consults _stamina.CanSprint ───
            // Direct literal-stub match (M6 lesson — no slice scans).
            bool ok10 = false;
            string detail10 = $"source missing at {PlayerControllerSrcPath}";
            string ctrlSrcFull = Path.Combine(Application.dataPath, "..", PlayerControllerSrcPath);
            if (File.Exists(ctrlSrcFull))
            {
                string src = File.ReadAllText(ctrlSrcFull);
                ok10 = src.Contains("_stamina.CanSprint");
                detail10 = ok10
                    ? "_stamina.CanSprint reference found in PlayerController.cs"
                    : "no _stamina.CanSprint reference — sprint engagement is not gated on stamina";
            }
            Check("10 PlayerController.cs gates sprint on _stamina.CanSprint", ok10, detail10);

            // ── 11: Player_MaleHero.prefab has PlayerStamina component ───────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            bool ok11 = false;
            string detail11 = $"prefab missing at {PlayerPrefabPath}";
            if (playerPrefab != null)
            {
                var stamina = playerPrefab.GetComponent<PlayerStamina>();
                ok11 = stamina != null;
                detail11 = ok11
                    ? "PlayerStamina present on prefab root"
                    : "PlayerStamina missing — run 'LevelGen ▶ Player ▶ Add PlayerStamina to Player_MaleHero Prefab' " +
                      "(or rebuild the prefab via Build Player_MaleHero Prefab)";
            }
            Check("11 Player_MaleHero.prefab has PlayerStamina component", ok11, detail11);

            // ── 12: CharacterStats_Player.asset has both rates > 0 ───────────
            var playerStats = AssetDatabase.LoadAssetAtPath<CharacterStats>(PlayerStatsAssetPath);
            bool ok12 = false;
            string detail12 = $"asset missing at {PlayerStatsAssetPath}";
            if (playerStats != null)
            {
                float drain = playerStats.StaminaDrainPerSecond;
                float regen = playerStats.StaminaRegenPerSecond;
                ok12 = drain > 0f && regen > 0f;
                detail12 = ok12
                    ? $"drain={drain}/s, regen={regen}/s"
                    : $"drain={drain}, regen={regen} — at least one is 0; stamina won't move. " +
                      "Run 'LevelGen ▶ Combat ▶ Set Stamina Rates on CharacterStats Assets'";
            }
            Check("12 CharacterStats_Player.asset has staminaDrain + staminaRegen both > 0", ok12, detail12);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Player stamina wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
