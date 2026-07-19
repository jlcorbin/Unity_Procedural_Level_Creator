// RangedSetupBuilder.cs — M22 P5 editor builders for the ranged combat prefabs.
//
// Builds the two mechanically-fiddly prefabs so they're correct by construction:
//   • Arrow_Projectile.prefab — Rigidbody (no gravity, continuous) + SphereCollider
//     (r = UnrealUnits.ArrowCollisionRadius) + ArrowProjectile, with the pack's
//     Arrow01Projectile mesh as a visual child (falls back to a thin cube).
//   • RangedReticle.prefab — a Screen-Space center crosshair (4 bars + dot),
//     sprites assigned (PlayerHUD sprite-fix lesson) so it renders.
//
// Placement + wiring stays manual (P10): drop the reticle under a scene Canvas,
// set it inactive, and assign it to StanceController._rangedCrosshair; assign the
// arrow prefab to RangedCombat._arrowPrefab.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Player.Editor
{
    public static class RangedSetupBuilder
    {
        private const string ArrowPrefabPath = "Assets/Prefabs/Weapons/Arrow_Projectile.prefab";
        private const string ReticlePrefabPath = "Assets/Prefabs/UI/RangedReticle.prefab";
        private const string ArrowMeshPath =
            "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Mesh/Weapons/Projectile/Arrow01Projectile.fbx";

        [MenuItem("LevelGen/Player/Build Arrow Prefab")]
        public static void BuildArrowPrefab()
        {
            var root = new GameObject("Arrow_Projectile");

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;                                   // ArrowProjectile applies 0.4× itself
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.mass = 0.1f;

            var col = root.AddComponent<SphereCollider>();
            col.radius = UnrealUnits.ArrowCollisionRadius;           // 0.05 m
            col.isTrigger = false;

            root.AddComponent<ArrowProjectile>();

            // Visual: the pack arrow mesh if present, else a thin cube.
            var meshGO = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowMeshPath);
            if (meshGO != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(meshGO);
                vis.transform.SetParent(root.transform, false);
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = Quaternion.identity;
                vis.name = "Mesh";
            }
            else
            {
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(vis.GetComponent<Collider>());
                vis.transform.SetParent(root.transform, false);
                vis.transform.localScale = new Vector3(0.02f, 0.02f, 0.5f);
                vis.name = "Mesh (placeholder)";
                Debug.LogWarning($"[RangedSetupBuilder] Arrow mesh not found at '{ArrowMeshPath}'. Used a placeholder cube.");
            }

            EnsureFolder("Assets/Prefabs/Weapons");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[RangedSetupBuilder] Built '{ArrowPrefabPath}' " +
                      $"(SphereCollider r={UnrealUnits.ArrowCollisionRadius}, Rigidbody no-gravity continuous, ArrowProjectile). " +
                      "Assign it to RangedCombat._arrowPrefab on Player_Hero.");
            Selection.activeObject = prefab;
        }

        [MenuItem("LevelGen/Player/Build Ranged Reticle Prefab")]
        public static void BuildReticlePrefab()
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var root = new GameObject("RangedReticle",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // above HUD (10), below death overlay (100)

            // Center dot.
            MakeBar(root.transform, sprite, "Dot", new Vector2(4f, 4f), Vector2.zero);
            // Four bars around the center (gap in the middle).
            MakeBar(root.transform, sprite, "Top",    new Vector2(2f, 10f), new Vector2(0f,  12f));
            MakeBar(root.transform, sprite, "Bottom", new Vector2(2f, 10f), new Vector2(0f, -12f));
            MakeBar(root.transform, sprite, "Left",   new Vector2(10f, 2f), new Vector2(-12f, 0f));
            MakeBar(root.transform, sprite, "Right",  new Vector2(10f, 2f), new Vector2( 12f, 0f));

            EnsureFolder("Assets/Prefabs/UI");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ReticlePrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[RangedSetupBuilder] Built '{ReticlePrefabPath}'. " +
                      "Place it in the scene (or under the HUD canvas), set it INACTIVE, " +
                      "and assign it to StanceController._rangedCrosshair — it is toggled on in ranged stances.");
            Selection.activeObject = prefab;
        }

        private static void MakeBar(Transform parent, Sprite sprite, string name, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;                 // assign sprite so it renders (PlayerHUD lesson)
            img.color = Color.white;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
