// PlayerDeathOverlayBuilder.cs — authoring for the M5 "You Died" overlay.
//
// Two menu items:
//   LevelGen ▶ UI ▶ Build PlayerDeathOverlay Prefab
//   LevelGen ▶ UI ▶ Place PlayerDeathOverlay in Active Scene
//
// Build is idempotent — overwrite-confirmed. Mirrors PlayerHUDBuilder's
// structure (Canvas root, ScreenSpaceOverlay) but with sortingOrder=100
// to draw above the HUD.
//
// Sprite-fix lesson from PlayerHUD carried forward: backdrop Image is
// type=Sliced and assigns the built-in UISprite.psd to avoid no-clipping
// issues.

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using LevelGen.UI;

namespace LevelGen.UI.EditorTools
{
    public static class PlayerDeathOverlayBuilder
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string PrefabPath = "Assets/Prefabs/UI/PlayerDeathOverlay.prefab";

        // ── Colors ──────────────────────────────────────────────────────────
        private static readonly Color BackdropColor   = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color YouDiedColor    = new Color(0.85f, 0.10f, 0.10f, 1f);
        private static readonly Color ButtonBgColor   = new Color(0.20f, 0.20f, 0.20f, 1f);
        private static readonly Color ButtonTextColor = Color.white;

        // ── Layout constants ────────────────────────────────────────────────
        private const int   YouDiedFontSize = 96;
        private const int   ButtonFontSize  = 32;
        private const float ButtonWidth     = 240f;
        private const float ButtonHeight    = 64f;
        private const float ButtonYOffset   = -120f;  // below "You Died" label

        // ════════════════════════════════════════════════════════════════════
        // Menu item: build the prefab
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Build PlayerDeathOverlay Prefab")]
        private static void BuildPrefab()
        {
            // Overwrite confirm.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Overwrite PlayerDeathOverlay prefab?",
                    $"A prefab already exists at {PrefabPath}.\n\nOverwrite?",
                    "Overwrite", "Cancel");
                if (!ok)
                {
                    Debug.Log("[PlayerDeathOverlayBuilder] Build canceled — existing prefab preserved.");
                    return;
                }
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "UI");

            // ── Root: holds PlayerDeathOverlay component ────────────────────
            // Root has NO Canvas — the Canvas is a child so we can hide
            // it with SetActive(false) without disabling the component
            // that listens for PlayerDeath.OnPlayerDied.
            var root = new GameObject("PlayerDeathOverlay", typeof(PlayerDeathOverlay));

            // ── Canvas child (hidden by default) ────────────────────────────
            var canvasGO = new GameObject("Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(root.transform, worldPositionStays: false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            // Sort above PlayerHUD (sortingOrder=10).
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            // ── Backdrop (full-screen dim) ──────────────────────────────────
            var backdrop = CreateImage(canvasGO.transform, "Backdrop", BackdropColor, sliced: true);
            Stretch(backdrop.transform);

            // ── "You Died" label ────────────────────────────────────────────
            var label = CreateLabel(canvasGO.transform, "YouDiedLabel", "You Died",
                YouDiedFontSize, YouDiedColor);
            // Centered, slight lift above mid-screen so the button can sit below.
            SetAnchored(label.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f),
                new Vector2(900f, 200f));
            label.alignment = TextAlignmentOptions.Center;

            // ── Restart button ──────────────────────────────────────────────
            var buttonGO = new GameObject("RestartButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonGO.transform.SetParent(canvasGO.transform, worldPositionStays: false);
            SetAnchored(buttonGO.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, ButtonYOffset),
                new Vector2(ButtonWidth, ButtonHeight));

            var btnImg = buttonGO.GetComponent<Image>();
            btnImg.color  = ButtonBgColor;
            btnImg.type   = Image.Type.Sliced;
            btnImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var btnLabel = CreateLabel(buttonGO.transform, "Text", "Restart",
                ButtonFontSize, ButtonTextColor);
            Stretch(btnLabel.transform);
            btnLabel.alignment = TextAlignmentOptions.Center;

            var button = buttonGO.GetComponent<Button>();
            button.targetGraphic = btnImg;

            // ── Wire SerializeField refs on PlayerDeathOverlay ──────────────
            var overlay = root.GetComponent<PlayerDeathOverlay>();
            var so = new SerializedObject(overlay);
            AssignField(so, "_canvas",        canvas);
            AssignField(so, "_restartButton", button);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(overlay);

            // ── Hide the canvas in the prefab so the overlay starts hidden ──
            // PlayerDeathOverlay.Awake also sets this defensively.
            canvasGO.SetActive(false);

            // ── Save prefab ─────────────────────────────────────────────────
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (!success || saved == null)
            {
                Debug.LogError("[PlayerDeathOverlayBuilder] SaveAsPrefabAsset failed.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (reloaded != null)
            {
                Selection.activeObject = reloaded;
                EditorGUIUtility.PingObject(reloaded);
            }

            Debug.Log(
                $"[PlayerDeathOverlayBuilder] Built {PrefabPath}.\n" +
                $"  Canvas: ScreenSpaceOverlay, sortingOrder=100 (above HUD)\n" +
                $"  Backdrop: full-screen 75% black\n" +
                $"  'You Died' label: {YouDiedFontSize}pt red\n" +
                $"  Restart button: {ButtonWidth}x{ButtonHeight}px, reloads active scene.\n" +
                $"  Canvas child starts inactive (overlay hidden until OnPlayerDied)."
            );
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: place into active scene
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/UI/Place PlayerDeathOverlay in Active Scene")]
        private static void PlaceInActiveScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerDeathOverlayBuilder] {PrefabPath} not found. " +
                               "Run 'LevelGen ▶ UI ▶ Build PlayerDeathOverlay Prefab' first.");
                return;
            }

            var existing = Object.FindAnyObjectByType<PlayerDeathOverlay>();
            if (existing != null)
            {
                Debug.Log("[PlayerDeathOverlayBuilder] PlayerDeathOverlay already in scene — selecting existing instance.");
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                // Still ensure EventSystem on re-runs — covers scenes
                // that had the overlay placed before this was added.
                EnsureEventSystem();
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
            {
                Debug.LogError("[PlayerDeathOverlayBuilder] InstantiatePrefab failed.");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place PlayerDeathOverlay");
            EditorSceneManager.MarkSceneDirty(instance.scene);

            // UGUI buttons need an EventSystem in the scene to dispatch
            // clicks. Scenes from EditorSceneManager.NewScene(DefaultGameObjects)
            // don't include one, and creating a Canvas programmatically
            // (vs. via GameObject ▶ UI ▶ Canvas) doesn't auto-add it
            // either. Add one here if missing — idempotent.
            EnsureEventSystem();

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            Debug.Log($"[PlayerDeathOverlayBuilder] Placed '{instance.name}' in active scene.");
        }

        /// <summary>
        /// Ensures a single EventSystem GameObject exists in the active
        /// scene. Uses InputSystemUIInputModule (the project uses the new
        /// Input System; StandaloneInputModule would log warnings here).
        /// Idempotent — silent no-op if one is already present.
        /// </summary>
        private static void EnsureEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>();
            if (existing != null) return;

            var go = new GameObject("EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Add EventSystem");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[PlayerDeathOverlayBuilder] Added EventSystem (with InputSystemUIInputModule) " +
                      "to active scene — required for UGUI button click dispatch.");
        }

        // ════════════════════════════════════════════════════════════════════
        // UI hierarchy helpers
        // ════════════════════════════════════════════════════════════════════

        private static Image CreateImage(Transform parent, string name, Color color, bool sliced)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, worldPositionStays: false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type   = sliced ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text,
                                            int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, worldPositionStays: false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text          = text;
            label.color         = color;
            label.fontSize      = fontSize;
            label.fontStyle     = FontStyles.Bold;
            label.alignment     = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void SetAnchored(Transform t, Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        private static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AssignField(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[PlayerDeathOverlayBuilder] PlayerDeathOverlay has no serialized field '{fieldName}'.");
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
                Debug.LogError($"[PlayerDeathOverlayBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[PlayerDeathOverlayBuilder] Created folder: {path}");
        }
    }
}
#endif
