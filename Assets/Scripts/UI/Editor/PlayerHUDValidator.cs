// PlayerHUDValidator.cs — read-only checks on the HP/Stamina HUD.
//
// Single menu item:
//   LevelGen ▶ UI ▶ Validate PlayerHUD
//
// 11 checks covering:
//   - PlayerHUD type in LevelGen.UI
//   - CharacterStats_Player.asset values
//   - Player_Hero prefab (CharacterStatsRuntime + stats + tag)
//   - PlayerHUD.prefab (Canvas, component, four refs, Filled-Image setup, TMP labels)
//
// Pattern mirrors DummyAndStatsValidator.cs.

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LevelGen.Combat;
using LevelGen.UI;

namespace LevelGen.UI.EditorTools
{
    public static class PlayerHUDValidator
    {
        private const string HUDPrefabPath    = "Assets/Prefabs/UI/PlayerHUD.prefab";
        private const string PlayerStatsPath  = "Assets/Data/CharacterStats/CharacterStats_Player.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab";

        [MenuItem("LevelGen/UI/Validate PlayerHUD")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: PlayerHUD type in LevelGen.UI ────────────────────────────
            var hudType = typeof(PlayerHUD);
            Check("1 PlayerHUD type in namespace LevelGen.UI",
                hudType.Namespace == "LevelGen.UI",
                $"namespace='{hudType.Namespace}'");

            // ── 2: Player stats SO values ───────────────────────────────────
            var playerStats = AssetDatabase.LoadAssetAtPath<CharacterStats>(PlayerStatsPath);
            if (playerStats == null)
            {
                Check("2 CharacterStats_Player.asset exists with expected values",
                    false, $"missing at {PlayerStatsPath}");
            }
            else
            {
                bool ok = playerStats.displayName == "Player"
                          && playerStats.maxHP      == 100
                          && playerStats.maxStamina == 100;
                Check("2 CharacterStats_Player.asset exists with expected values", ok,
                    $"displayName='{playerStats.displayName}', maxHP={playerStats.maxHP}, " +
                    $"maxStamina={playerStats.maxStamina} (expected 'Player', 100, 100)");
            }

            // ── 3-5: Player_Hero prefab ─────────────────────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            CharacterStatsRuntime playerRuntime = null;
            if (playerPrefab == null)
            {
                Check("3 Player_Hero has CharacterStatsRuntime",
                    false, $"prefab missing at {PlayerPrefabPath}");
                Check("4 Player_Hero CharacterStatsRuntime references CharacterStats_Player",
                    false, "prefab missing — see check 3");
                Check("5 Player_Hero has tag 'Player'",
                    false, "prefab missing — see check 3");
            }
            else
            {
                playerRuntime = playerPrefab.GetComponent<CharacterStatsRuntime>();
                Check("3 Player_Hero has CharacterStatsRuntime",
                    playerRuntime != null,
                    playerRuntime != null
                        ? "present"
                        : "missing — run 'LevelGen ▶ UI ▶ Add CharacterStatsRuntime to Player_Hero'");

                if (playerRuntime == null)
                {
                    Check("4 Player_Hero CharacterStatsRuntime references CharacterStats_Player",
                        false, "component missing — see check 3");
                }
                else
                {
                    var so = new SerializedObject(playerRuntime);
                    var prop = so.FindProperty("stats");
                    var assigned = prop != null ? prop.objectReferenceValue as CharacterStats : null;
                    bool ok = assigned != null && assigned == playerStats;
                    Check("4 Player_Hero CharacterStatsRuntime references CharacterStats_Player",
                        ok,
                        assigned != null
                            ? $"stats='{assigned.name}' (expected 'CharacterStats_Player')"
                            : "stats field unassigned");
                }

                bool tagOk = playerPrefab.CompareTag("Player");
                Check("5 Player_Hero has tag 'Player'", tagOk,
                    tagOk ? "tag='Player'" : $"tag='{playerPrefab.tag}'");
            }

            // ── 6: HUD prefab exists ────────────────────────────────────────
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HUDPrefabPath);
            Check("6 PlayerHUD.prefab exists", hudPrefab != null,
                hudPrefab != null ? HUDPrefabPath : $"missing at {HUDPrefabPath}");

            if (hudPrefab == null)
            {
                Summary(pass, fail);
                return;
            }

            // ── 7: Canvas + Screen Space Overlay ────────────────────────────
            var canvas = hudPrefab.GetComponent<Canvas>();
            if (canvas == null)
            {
                Check("7 PlayerHUD.prefab root has Canvas (Screen Space Overlay)",
                    false, "Canvas component missing on root");
            }
            else
            {
                bool ok = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
                Check("7 PlayerHUD.prefab root has Canvas (Screen Space Overlay)", ok,
                    $"renderMode={canvas.renderMode} (expected ScreenSpaceOverlay)");
            }

            // ── 8: PlayerHUD component on root ──────────────────────────────
            var hud = hudPrefab.GetComponent<PlayerHUD>();
            Check("8 PlayerHUD component on prefab root", hud != null,
                hud != null ? "present" : "missing");

            // ── 9: Four serialized refs non-null ────────────────────────────
            // ── 10: hpFill / staminaFill = Filled / Horizontal / Left ───────
            // ── 11: TMP labels present (sanity — TMP imported) ──────────────
            if (hud == null)
            {
                Check("9 PlayerHUD serialized refs assigned", false,
                    "PlayerHUD component missing — see check 8");
                Check("10 hpFill and staminaFill are Filled/Horizontal/Left", false,
                    "PlayerHUD component missing — see check 8");
                Check("11 TMP labels present (HP + Stamina)", false,
                    "PlayerHUD component missing — see check 8");
            }
            else
            {
                var so = new SerializedObject(hud);
                var pHpFill   = so.FindProperty("hpFill");
                var pStamFill = so.FindProperty("staminaFill");
                var pHpLabel  = so.FindProperty("hpLabel");
                var pStamLab  = so.FindProperty("staminaLabel");

                bool ok9 = pHpFill?.objectReferenceValue   != null
                        && pStamFill?.objectReferenceValue != null
                        && pHpLabel?.objectReferenceValue  != null
                        && pStamLab?.objectReferenceValue  != null;
                Check("9 PlayerHUD serialized refs assigned", ok9,
                    $"hpFill={(pHpFill?.objectReferenceValue != null ? "OK" : "null")}, " +
                    $"staminaFill={(pStamFill?.objectReferenceValue != null ? "OK" : "null")}, " +
                    $"hpLabel={(pHpLabel?.objectReferenceValue != null ? "OK" : "null")}, " +
                    $"staminaLabel={(pStamLab?.objectReferenceValue != null ? "OK" : "null")}");

                var hpImg = pHpFill?.objectReferenceValue as Image;
                var stImg = pStamFill?.objectReferenceValue as Image;
                bool ok10 = hpImg != null && stImg != null
                            && hpImg.type == Image.Type.Filled
                            && hpImg.fillMethod == Image.FillMethod.Horizontal
                            && hpImg.fillOrigin == (int)Image.OriginHorizontal.Left
                            && stImg.type == Image.Type.Filled
                            && stImg.fillMethod == Image.FillMethod.Horizontal
                            && stImg.fillOrigin == (int)Image.OriginHorizontal.Left;
                string ok10Detail = (hpImg == null || stImg == null)
                    ? "fill Image refs missing"
                    : $"hpFill: type={hpImg.type}, fill={hpImg.fillMethod}, origin={hpImg.fillOrigin}; " +
                      $"staminaFill: type={stImg.type}, fill={stImg.fillMethod}, origin={stImg.fillOrigin} " +
                      "(all expected: Filled/Horizontal/Left=0)";
                Check("10 hpFill and staminaFill are Filled/Horizontal/Left", ok10, ok10Detail);

                var hpTmp = pHpLabel?.objectReferenceValue as TMP_Text;
                var stTmp = pStamLab?.objectReferenceValue as TMP_Text;
                bool ok11 = hpTmp != null && stTmp != null;
                Check("11 TMP labels present (HP + Stamina)", ok11,
                    ok11
                        ? $"hpLabel and staminaLabel both TMP_Text"
                        : "missing TMP_Text on one or both label refs — " +
                          "if TMP isn't imported, run Window ▶ TextMeshPro ▶ Import TMP Essentials");
            }

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — PlayerHUD foundation OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
