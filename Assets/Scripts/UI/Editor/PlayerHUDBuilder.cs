// PlayerHUDBuilder.cs — authoring for the HP/Stamina HUD.
//
// Three menu items:
//   LevelGen ▶ UI ▶ Build PlayerHUD Prefab
//   LevelGen ▶ UI ▶ Place PlayerHUD in Active Scene
//   LevelGen ▶ UI ▶ Add CharacterStatsRuntime to Player_MaleHero
//
// Build is idempotent — overwrite-confirmed. Player stats SO is created
// on first run; Add menu attaches the runtime component to the Player
// prefab if missing.

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using LevelGen.Combat;
using LevelGen.UI;

namespace LevelGen.UI.EditorTools
{
    public static class PlayerHUDBuilder
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string HUDPrefabPath    = "Assets/Prefabs/UI/PlayerHUD.prefab";
        private const string PlayerStatsPath  = "Assets/Data/CharacterStats/CharacterStats_Player.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";

        // ── Colors ──────────────────────────────────────────────────────────
        private static readonly Color BgColor       = new Color(0.10f, 0.10f, 0.10f, 0.85f);
        private static readonly Color HPFillColor   = new Color(0.85f, 0.15f, 0.15f, 1f);
        private static readonly Color StamFillColor = new Color(0.95f, 0.85f, 0.20f, 1f);
        private static readonly Color LabelColor    = Color.white;

        // ── Layout constants ────────────────────────────────────────────────
        private const float BarWidth      = 240f;
        private const float HPHeight      = 30f;
        private const float StaminaHeight = 24f;
        private const float Gap           = 4f;
        private const float CornerOffset  = 20f;
        private const int   HPLabelSize   = 16;
        private const int   StamLabelSize = 14;

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build PlayerHUD prefab
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Build PlayerHUD Prefab")]
        private static void BuildHUDPrefab()
        {
            // Overwrite confirm.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HUDPrefabPath) != null)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Overwrite PlayerHUD prefab?",
                    $"A prefab already exists at {HUDPrefabPath}.\n\nOverwrite?",
                    "Overwrite", "Cancel");
                if (!ok)
                {
                    Debug.Log("[PlayerHUDBuilder] Build canceled — existing prefab preserved.");
                    return;
                }
                AssetDatabase.DeleteAsset(HUDPrefabPath);
            }

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            // ── Build hierarchy in memory ───────────────────────────────────
            var root = new GameObject("PlayerHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlayerHUD));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // HUDRoot — anchored bottom-left, holds both bars.
            float hudRootHeight = HPHeight + Gap + StaminaHeight;
            var hudRoot = CreateChild(root.transform, "HUDRoot");
            SetAnchored(hudRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                        new Vector2(CornerOffset, CornerOffset),
                        new Vector2(BarWidth, hudRootHeight));

            // HPGroup at the top of HUDRoot.
            var hpGroup = CreateChild(hudRoot, "HPGroup");
            SetAnchored(hpGroup, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                        new Vector2(0, StaminaHeight + Gap),
                        new Vector2(BarWidth, HPHeight));

            var hpBg = CreateBar(hpGroup, "HPBackground", BgColor, filled: false);
            var hpFill = CreateBar(hpGroup, "HPFill", HPFillColor, filled: true);
            var hpLabel = CreateLabel(hpGroup, "HPLabel", "HP 100 / 100", HPLabelSize);

            // StaminaGroup at the bottom of HUDRoot.
            var staminaGroup = CreateChild(hudRoot, "StaminaGroup");
            SetAnchored(staminaGroup, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                        new Vector2(0, 0),
                        new Vector2(BarWidth, StaminaHeight));

            var stamBg = CreateBar(staminaGroup, "StaminaBackground", BgColor, filled: false);
            var stamFill = CreateBar(staminaGroup, "StaminaFill", StamFillColor, filled: true);
            var stamLabel = CreateLabel(staminaGroup, "StaminaLabel", "Stamina 100 / 100", StamLabelSize);

            // ── Wire serialized fields on PlayerHUD via SerializedObject ────
            var hud = root.GetComponent<PlayerHUD>();
            var so = new SerializedObject(hud);
            AssignField(so, "hpFill",       hpFill);
            AssignField(so, "staminaFill",  stamFill);
            AssignField(so, "hpLabel",      hpLabel);
            AssignField(so, "staminaLabel", stamLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            // ── Save prefab ─────────────────────────────────────────────────
            var saved = PrefabUtility.SaveAsPrefabAsset(root, HUDPrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[PlayerHUDBuilder] SaveAsPrefabAsset failed.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(HUDPrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[PlayerHUDBuilder] Built {HUDPrefabPath}.\n" +
                $"  Canvas: ScreenSpaceOverlay, sortingOrder=10\n" +
                $"  HP bar:      {BarWidth}x{HPHeight}, fillColor=red\n" +
                $"  Stamina bar: {BarWidth}x{StaminaHeight}, fillColor=yellow\n" +
                $"  Anchored bottom-left at ({CornerOffset}, {CornerOffset}).\n" +
                $"  Place into a scene via 'LevelGen ▶ UI ▶ Place PlayerHUD in Active Scene'."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: place PlayerHUD into the active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Place PlayerHUD in Active Scene")]
        private static void PlaceHUDInActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HUDPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerHUDBuilder] {HUDPrefabPath} not found. " +
                               "Run 'LevelGen ▶ UI ▶ Build PlayerHUD Prefab' first.");
                return;
            }

            // Skip if a PlayerHUD instance already lives in the active scene.
            var existing = Object.FindAnyObjectByType<PlayerHUD>();
            if (existing != null)
            {
                Debug.Log($"[PlayerHUDBuilder] PlayerHUD already in scene — selecting existing instance.");
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Debug.LogError("[PlayerHUDBuilder] InstantiatePrefab failed.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place PlayerHUD");
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            Debug.Log($"[PlayerHUDBuilder] Placed '{instance.name}' in active scene.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: add CharacterStatsRuntime to Player_MaleHero
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Add CharacterStatsRuntime to Player_MaleHero")]
        private static void AddStatsRuntimeToPlayer()
        {
            var playerStats = EnsurePlayerStats();
            if (playerStats == null) return;

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[PlayerHUDBuilder] Player prefab not found at {PlayerPrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var existing = contents.GetComponent<CharacterStatsRuntime>();
                if (existing != null)
                {
                    var so = new SerializedObject(existing);
                    var prop = so.FindProperty("stats");
                    var assigned = prop != null ? prop.objectReferenceValue as CharacterStats : null;

                    if (assigned == playerStats)
                    {
                        Debug.Log("[PlayerHUDBuilder] CharacterStatsRuntime already on Player_MaleHero with " +
                                  "CharacterStats_Player assigned — no change.");
                        return;
                    }

                    if (prop != null)
                    {
                        prop.objectReferenceValue = playerStats;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(existing);
                        PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                        Debug.Log("[PlayerHUDBuilder] CharacterStatsRuntime present but stats was unassigned " +
                                  "or pointed elsewhere — repointed to CharacterStats_Player.");
                    }
                    return;
                }

                var runtime = contents.AddComponent<CharacterStatsRuntime>();
                if (runtime == null)
                {
                    Debug.LogError("[PlayerHUDBuilder] AddComponent<CharacterStatsRuntime>() returned null. Aborting.");
                    return;
                }

                var soNew = new SerializedObject(runtime);
                var propNew = soNew.FindProperty("stats");
                if (propNew != null)
                {
                    propNew.objectReferenceValue = playerStats;
                    soNew.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(runtime);
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                Debug.Log($"[PlayerHUDBuilder] Added CharacterStatsRuntime to Player_MaleHero. " +
                          $"stats=CharacterStats_Player (HP={playerStats.maxHP}, " +
                          $"Stamina={playerStats.maxStamina}).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Player stats asset
        // ════════════════════════════════════════════════════════════════════

        private static CharacterStats EnsurePlayerStats()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "CharacterStats");

            var existing = AssetDatabase.LoadAssetAtPath<CharacterStats>(PlayerStatsPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<CharacterStats>();
            asset.displayName = "Player";
            asset.maxHP       = 100;
            asset.maxStamina  = 100;
            asset.description = "The hero. Default starting values.";

            AssetDatabase.CreateAsset(asset, PlayerStatsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlayerHUDBuilder] Created {PlayerStatsPath}.");
            return asset;
        }

        // ════════════════════════════════════════════════════════════════════
        // UI hierarchy helpers
        // ════════════════════════════════════════════════════════════════════

        private static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.transform;
        }

        private static void SetAnchored(Transform t, Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        // Stretches a child to fill its parent's RectTransform.
        private static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Image CreateBar(Transform parent, string name, Color color, bool filled)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);
            Stretch(go.transform);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            // Image.type=Filled needs a sprite to clip; without one, fillAmount
            // has no visual effect. Use Unity's built-in UISprite (same sprite
            // the editor's Add Component ▶ Image flow assigns by default).
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            if (filled)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)Image.OriginHorizontal.Left;
                img.fillAmount = 1f;
            }
            else
            {
                img.type = Image.Type.Sliced;
            }

            return img;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, worldPositionStays: false);
            Stretch(go.transform);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.color = LabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.raycastTarget = false;

            return label;
        }

        // ════════════════════════════════════════════════════════════════════
        // Misc
        // ════════════════════════════════════════════════════════════════════

        private static void AssignField(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[PlayerHUDBuilder] PlayerHUD has no serialized field '{fieldName}'.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[PlayerHUDBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[PlayerHUDBuilder] Created folder: {path}");
        }
    }
}
#endif
