// BowArrowMeshSeparator.cs — isolates a single bow + arrow variant.
//
// The pack's Bows.prefab / Arrows.prefab each pack FIVE skinned variants
// (Bow01–Bow05, Arrow01–Arrow05) on a shared skeleton, all rendered at once —
// so the BowAndArrow stance shows a pile. This keeps ONE variant active and
// deactivates the rest, as overrides on the WeaponPrefab_* wrappers, so the
// pack prefabs are never modified.
//
// To show a different variant, change KeptVariant (1–5) and re-run — it's
// idempotent (re-activates all, then hides all but the kept one).
//
// Menu: LevelGen ▶ Weapons ▶ Separate Bow + Arrow Meshes

#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Items.Editor
{
    public static class BowArrowMeshSeparator
    {
        // Which variant to keep visible (1–5): Bow0N / Arrow0N.
        private const int KeptVariant = 1;

        private const string BowsWrapper   = "Assets/Prefabs/Weapons/WeaponPrefab_Bows.prefab";
        private const string ArrowsWrapper = "Assets/Prefabs/Weapons/WeaponPrefab_Arrows.prefab";
        private const string NockedArrowName = "NockedArrow";

        [MenuItem("LevelGen/Weapons/Separate Bow + Arrow Meshes")]
        public static void Separate()
        {
            IsolateVariant(BowsWrapper,   "Bow");
            IsolateVariant(ArrowsWrapper, "Arrow");
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Mounts the (separated) WeaponPrefab_Arrows as a decorative child of the
        /// bow so a single nocked arrow is always shown while the bow is held. As a
        /// child of the bow it rides along in both the dev-cycle and inventory-equip
        /// paths and inherits the bow's off-hand corrective rotation. Physics /
        /// combat components are stripped — it's visual only. Idempotent (replaces
        /// any existing NockedArrow). Run AFTER "Separate Bow + Arrow Meshes".
        /// </summary>
        [MenuItem("LevelGen/Weapons/Mount Nocked Arrow on Bow")]
        public static void MountNockedArrow()
        {
            var arrowAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowsWrapper);
            if (arrowAsset == null)
            {
                Debug.LogError($"[BowArrowSeparator] Arrow prefab not found at '{ArrowsWrapper}'.");
                return;
            }

            var bow = PrefabUtility.LoadPrefabContents(BowsWrapper);
            if (bow == null)
            {
                Debug.LogError($"[BowArrowSeparator] Could not load '{BowsWrapper}'.");
                return;
            }

            // Idempotent: remove a prior nocked arrow.
            var existing = bow.transform.Find(NockedArrowName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var arrow = (GameObject)PrefabUtility.InstantiatePrefab(arrowAsset, bow.transform);
            arrow.name = NockedArrowName;
            arrow.transform.localPosition = Vector3.zero;
            arrow.transform.localRotation = Quaternion.identity;
            arrow.transform.localScale = Vector3.one;

            // Decorative only — strip physics / combat.
            foreach (var hr in arrow.GetComponentsInChildren<HitboxRelay>(true)) Object.DestroyImmediate(hr);
            foreach (var c in arrow.GetComponentsInChildren<Collider>(true))     Object.DestroyImmediate(c);
            foreach (var rb in arrow.GetComponentsInChildren<Rigidbody>(true))   Object.DestroyImmediate(rb);

            PrefabUtility.SaveAsPrefabAsset(bow, BowsWrapper);
            PrefabUtility.UnloadPrefabContents(bow);

            Debug.Log($"[BowArrowSeparator] Mounted '{NockedArrowName}' on the bow at local identity " +
                      "(decorative — physics stripped). If the nock sits off, tune its local transform on " +
                      "WeaponPrefab_Bows and re-save.");
        }

        private static void IsolateVariant(string prefabPath, string namePrefix)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[BowArrowSeparator] Could not load '{prefabPath}'.");
                return;
            }

            // Matches Bow01..Bow05 / Arrow01..Arrow05 exactly — NOT BowJoint01 etc.
            var variantRx = new Regex($"^{namePrefix}0[1-9]$");
            string keepName = $"{namePrefix}0{KeptVariant}";

            int kept = 0, hidden = 0, total = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!variantRx.IsMatch(t.gameObject.name)) continue;
                total++;
                bool keep = t.gameObject.name == keepName;
                t.gameObject.SetActive(keep);   // idempotent: re-activates the kept one, hides the rest
                if (keep) kept++; else hidden++;
            }

            if (total == 0)
                Debug.LogWarning($"[BowArrowSeparator] No '{namePrefix}0N' meshes found under '{prefabPath}'.");
            else
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[BowArrowSeparator] {prefabPath}: kept '{keepName}' ({kept}), hid {hidden} of {total}.");
        }
    }
}
#endif
