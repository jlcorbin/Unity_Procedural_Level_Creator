// WeaponPrefabBuilder.cs — authoring for weapon WorldPrefab assets (M20c).
//
// Builds all 57 Wave PBR weapon prefabs from the publisher's pre-built
// Prefab/Weapons/ assets (textured, correctly-materialised) as the mesh
// source — replacing the earlier FBX-raw approach that produced untextured
// grey meshes.
//
// Menu: LevelGen ▶ Weapons ▶ Build All Weapon Prefabs
//
// Idempotency contract:
//   - Prefab  : SaveAsPrefabAsset overwrites in place (GUID preserved). Do NOT
//               DeleteAsset first — that would reassign the GUID and break
//               ItemData.worldPrefab references across re-runs.
//   - ItemData: create-if-missing. If the asset already exists, ONLY the
//               _worldPrefab field is refreshed (user-tuned damage / id / rarity
//               are NOT overwritten on subsequent runs).
//
// Weapon authoring standard (M20b / M20c):
//   Every weapon WorldPrefab MUST carry on the ROOT:
//     1. BoxCollider  — isTrigger=true, enabled=false (enabled per-frame by PlayerCombat).
//     2. Rigidbody   — isKinematic=true, useGravity=false (required for OnTriggerEnter
//                      to fire on a collider that moves via skeletal animation).
//     3. HitboxRelay — combat field NULL in the prefab; wired at runtime by
//                      PlayerEquipmentVisuals.HandleWeaponEquipped.
//   The publisher's weapon prefab is nested as a CHILD at local identity — this
//   carries the visible mesh with correct materials. The root owns collision/relay.
//
// NOTE: BoxCollider center/size values are first-pass category estimates.
// Open each prefab in Prefab Mode and tune using the collider bounds handle gizmo.
//
// Validator: see WeaponPrefabValidator.cs  (LevelGen ▶ Weapons ▶ Validate Weapon Prefabs)

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Items;

namespace LevelGen.Items.Editor
{
    /// <summary>
    /// Idempotent builder for all 57 Wave PBR weapon prefabs + matching
    /// <see cref="ItemData"/> assets. The weapon table is exposed as
    /// <c>internal static</c> so <see cref="WeaponPrefabValidator"/> can
    /// iterate the same list without duplication.
    /// </summary>
    public static class WeaponPrefabBuilder
    {
        // ── Base paths ────────────────────────────────────────────────────────

        private const string PublisherPrefabBase =
            "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Prefab/Weapons";

        internal const string PrefabFolder   = "Assets/Prefabs/Weapons";
        internal const string ItemDataFolder = "Assets/Data/Items";

        // ── Category colider defaults ─────────────────────────────────────────

        // OHS (One-Handed Sword / Axe / Hammer / Stick / Niddle)
        private static readonly Vector3 OhsCenter = new Vector3(0f,    0f, 0.35f);
        private static readonly Vector3 OhsSize   = new Vector3(0.10f, 0.10f, 0.70f);

        // THS (Two-Handed Sword)
        private static readonly Vector3 ThsCenter = new Vector3(0f,    0f, 0.50f);
        private static readonly Vector3 ThsSize   = new Vector3(0.12f, 0.12f, 1.00f);

        // Spear / Polearm
        private static readonly Vector3 SpearCenter = new Vector3(0f,    0f, 0.60f);
        private static readonly Vector3 SpearSize   = new Vector3(0.08f, 0.08f, 1.20f);

        // Shield (OffHand)
        private static readonly Vector3 ShieldCenter = new Vector3(0f,   0f, 0.05f);
        private static readonly Vector3 ShieldSize   = new Vector3(0.50f, 0.60f, 0.10f);

        // Bow (Ranged)
        private static readonly Vector3 BowCenter = new Vector3(0f,    0f, 0.30f);
        private static readonly Vector3 BowSize   = new Vector3(0.08f, 0.50f, 0.08f);

        // Arrows (Ranged)
        private static readonly Vector3 ArrowsCenter = new Vector3(0f,    0f, 0.20f);
        private static readonly Vector3 ArrowsSize   = new Vector3(0.05f, 0.05f, 0.40f);

        // Wand / Staff (Ranged)
        private static readonly Vector3 WandCenter = new Vector3(0f,    0f, 0.30f);
        private static readonly Vector3 WandSize   = new Vector3(0.06f, 0.06f, 0.60f);

        // ── Weapon category damage defaults ───────────────────────────────────

        private const int DamageOhs    = 15;
        private const int DamageThs    = 25;
        private const int DamageSpear  = 20;
        private const int DamageShield = 5;
        private const int DamageRanged = 18;   // Bow + Wand
        private const int DamageArrows = 0;

        // ── Weapon entry table (single source of truth shared with validator) ─

        /// <summary>
        /// All 57 canonical Wave PBR weapons. Used by both
        /// <see cref="BuildAll"/> and <see cref="WeaponPrefabValidator"/>.
        /// Each entry: (publisherPrefabName, itemDataId, displayName, slot, damage, colliderCenter, colliderSize).
        /// </summary>
        internal static readonly WeaponEntry[] AllWeapons = new WeaponEntry[]
        {
            // ── One-Handed Melee (OHS) — 16 items ──────────────────────────
            new WeaponEntry("OHS01_Stick",   "wooden_stick",       "Wooden Stick",    EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS02_Niddle",  "needle_rapier",      "Needle Rapier",   EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS03_Sword",   "iron_sword",         "Iron Sword",      EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS04_Sword",   "bronze_sword",       "Bronze Sword",    EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS05_Sword",   "steel_sword",        "Steel Sword",     EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS06_Sword",   "knight_sword",       "Knight Sword",    EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS07_Sword",   "noble_sword",        "Noble Sword",     EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS08_Sword",   "ornate_sword",       "Ornate Sword",    EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS09_Sword",   "royal_sword",        "Royal Sword",     EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS10_Axe",     "hand_axe",           "Hand Axe",        EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS11_Hammer",  "war_hammer",         "War Hammer",      EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS12_Sword",   "curved_sword",       "Curved Sword",    EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS13_Axe",     "battle_axe",         "Battle Axe",      EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS14_Hammer",  "heavy_hammer",       "Heavy Hammer",    EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS15_Sword",   "dark_sword",         "Dark Sword",      EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),
            new WeaponEntry("OHS16_Sword",   "light_sword",        "Light Sword",     EquipSlot.Melee,   DamageOhs,    OhsCenter, OhsSize),

            // ── Two-Handed Swords (THS) — 7 items ──────────────────────────
            new WeaponEntry("THS01_Sword",   "claymore",           "Claymore",        EquipSlot.Melee,   DamageThs,    ThsCenter, ThsSize),
            new WeaponEntry("THS02_Sword",   "greatsword",         "Greatsword",      EquipSlot.Melee,   DamageThs,    ThsCenter, ThsSize),
            new WeaponEntry("THS03_Sword",   "zweihander",         "Zweihander",      EquipSlot.Melee,   DamageThs,    ThsCenter, ThsSize),
            new WeaponEntry("THS04_Sword",   "flamberge",          "Flamberge",       EquipSlot.Melee,   DamageThs,    ThsCenter, ThsSize),
            new WeaponEntry("THS05_Sword",   "executioner_sword",  "Executioner Sword", EquipSlot.Melee, DamageThs,    ThsCenter, ThsSize),
            new WeaponEntry("THS06_Sword",   "katana",             "Katana",          EquipSlot.Melee,   DamageThs,    ThsCenter, ThsSize),
            new WeaponEntry("THS07_Sword",   "dark_blade",         "Dark Blade",      EquipSlot.Melee,   DamageThs,    ThsCenter, ThsSize),

            // ── Spears — 5 items ────────────────────────────────────────────
            new WeaponEntry("Spear01",       "wooden_spear",       "Wooden Spear",    EquipSlot.Melee,   DamageSpear,  SpearCenter, SpearSize),
            new WeaponEntry("Spear02",       "iron_spear",         "Iron Spear",      EquipSlot.Melee,   DamageSpear,  SpearCenter, SpearSize),
            new WeaponEntry("Spear03",       "steel_spear",        "Steel Spear",     EquipSlot.Melee,   DamageSpear,  SpearCenter, SpearSize),
            new WeaponEntry("Spear04",       "pike",               "Pike",            EquipSlot.Melee,   DamageSpear,  SpearCenter, SpearSize),
            new WeaponEntry("Spear05",       "royal_halberd",      "Royal Halberd",   EquipSlot.Melee,   DamageSpear,  SpearCenter, SpearSize),

            // ── Shields / Off-Hand — 20 items ───────────────────────────────
            new WeaponEntry("Shield01",      "wooden_buckler",     "Wooden Buckler",  EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield02",      "round_shield",       "Round Shield",    EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield03",      "iron_shield",        "Iron Shield",     EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield04",      "kite_shield",        "Kite Shield",     EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield05",      "heater_shield",      "Heater Shield",   EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield06",      "tower_shield",       "Tower Shield",    EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield07",      "crusader_shield",    "Crusader Shield", EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield08",      "knight_shield",      "Knight Shield",   EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield09",      "noble_shield",       "Noble Shield",    EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield10",      "royal_shield",       "Royal Shield",    EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield11",      "ornate_shield",      "Ornate Shield",   EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield12",      "dragon_shield",      "Dragon Shield",   EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield13",      "lion_shield",        "Lion Shield",     EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield14",      "eagle_shield",       "Eagle Shield",    EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield15",      "wolf_shield",        "Wolf Shield",     EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield16",      "dark_shield",        "Dark Shield",     EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield17",      "light_shield",       "Light Shield",    EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield18",      "arcane_shield",      "Arcane Shield",   EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield19",      "holy_shield",        "Holy Shield",     EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),
            new WeaponEntry("Shield20",      "aegis",              "Aegis",           EquipSlot.OffHand, DamageShield, ShieldCenter, ShieldSize),

            // ── Ranged (Bows, Arrows, Wands) — 9 items ─────────────────────
            new WeaponEntry("Bows",          "bow",                "Bow",             EquipSlot.Ranged,  DamageRanged, BowCenter,    BowSize),
            new WeaponEntry("Arrows",        "arrows",             "Arrows",          EquipSlot.Ranged,  DamageArrows, ArrowsCenter, ArrowsSize),
            new WeaponEntry("Wand01",        "apprentice_wand",    "Apprentice Wand", EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
            new WeaponEntry("Wand02",        "mage_wand",          "Mage Wand",       EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
            new WeaponEntry("Wand03",        "crystal_wand",       "Crystal Wand",    EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
            new WeaponEntry("Wand04",        "fire_wand",          "Fire Wand",       EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
            new WeaponEntry("Wand05",        "ice_wand",           "Ice Wand",        EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
            new WeaponEntry("Wand06",        "shadow_wand",        "Shadow Wand",     EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
            new WeaponEntry("Wand07",        "arcane_staff",       "Arcane Staff",    EquipSlot.Ranged,  DamageRanged, WandCenter,   WandSize),
        };

        // ════════════════════════════════════════════════════════════════════
        // Menu item — Build All Weapon Prefabs
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds all 57 Wave PBR weapon prefabs and their matching
        /// <see cref="ItemData"/> assets. Idempotent — safe to re-run.
        /// </summary>
        [MenuItem("LevelGen/Weapons/Build All Weapon Prefabs")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Prefabs", "Weapons");
            EnsureFolder("Assets/Data",    "Items");

            int built = 0, skipped = 0;
            foreach (var w in AllWeapons)
            {
                bool ok = BuildWeapon(w);
                if (ok) built++;
                else    skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[WeaponPrefabBuilder] Build All complete. " +
                $"{built} weapon(s) built / updated, {skipped} skipped (check errors above).");
        }

        // ════════════════════════════════════════════════════════════════════
        // Core parameterised build method
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds one weapon prefab from the supplied <see cref="WeaponEntry"/>.
        /// Returns <c>true</c> on success, <c>false</c> on any early-return error.
        /// </summary>
        private static bool BuildWeapon(WeaponEntry w)
        {
            string publisherPrefabPath =
                $"{PublisherPrefabBase}/{w.PrefabName}.prefab";

            // ── Step 1: locate publisher prefab (pre-built, textured) ─────────
            var publisherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(publisherPrefabPath);
            if (publisherPrefab == null)
            {
                Debug.LogError(
                    $"[WeaponPrefabBuilder] Publisher prefab not found at '{publisherPrefabPath}'. " +
                    "Verify the path and casing. Skipping this weapon.");
                return false;
            }

            string weaponPrefabName = $"WeaponPrefab_{w.PrefabName}";
            string prefabPath       = $"{PrefabFolder}/{weaponPrefabName}.prefab";
            string itemDataPath     = $"{ItemDataFolder}/ItemData_{w.PrefabName}.asset";

            // ── Step 2: build in-memory hierarchy ────────────────────────────
            var root = new GameObject(weaponPrefabName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Visible mesh: the publisher's textured prefab as a child at local identity.
            // worldPositionStays: false so the model stays at the socket origin when
            // PlayerEquipmentVisuals instantiates this prefab under weapon_r.
            var meshChild = (GameObject)PrefabUtility.InstantiatePrefab(publisherPrefab, root.transform);
            if (meshChild == null)
            {
                Debug.LogError(
                    $"[WeaponPrefabBuilder] PrefabUtility.InstantiatePrefab returned null " +
                    $"for '{publisherPrefabPath}'. Skipping '{w.PrefabName}'.");
                Object.DestroyImmediate(root);
                return false;
            }
            meshChild.transform.localPosition = Vector3.zero;
            meshChild.transform.localRotation = Quaternion.identity;
            meshChild.transform.localScale    = Vector3.one;

            // Trigger BoxCollider on ROOT — disabled until PlayerCombat.OnHitboxOpen
            // enables it for the active damage frames of a swing.
            // NOTE: center/size below are first-pass category estimates.
            // Tune each weapon in Prefab Mode using the collider bounds handle gizmo.
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.enabled   = false;
            box.center    = w.ColliderCenter;
            box.size      = w.ColliderSize;

            // Kinematic Rigidbody on ROOT — required for OnTriggerEnter to fire on
            // a collider that moves via skeletal animation (M3/M11/M20b lesson).
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic            = true;
            rb.useGravity             = false;
            rb.interpolation          = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            // HitboxRelay on ROOT — combat ref intentionally null in the prefab asset.
            // PlayerEquipmentVisuals.HandleWeaponEquipped writes relay.Combat = pc
            // at runtime after instantiation under weapon_r.
            root.AddComponent<HitboxRelay>();

            // ── Step 3: save prefab (idempotent overwrite, GUID-preserving) ──
            // Do NOT DeleteAsset first — that reassigns the GUID and breaks the
            // ItemData._worldPrefab reference we wire in Step 4.
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool prefabSaved);
            Object.DestroyImmediate(root);

            if (!prefabSaved || savedPrefab == null)
            {
                Debug.LogError(
                    $"[WeaponPrefabBuilder] SaveAsPrefabAsset failed for '{prefabPath}'. " +
                    $"Skipping '{w.PrefabName}'.");
                return false;
            }

            // ── Step 4: create or update ItemData asset ───────────────────────
            // Create-if-missing: if the asset already exists, only _worldPrefab is
            // refreshed — user-tuned values (damage, rarity, id, etc.) are preserved.
            bool itemDataCreated = false;
            var existingData = AssetDatabase.LoadAssetAtPath<ItemData>(itemDataPath);

            if (existingData == null)
            {
                // First run: create fresh with all default values.
                var newData = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(newData, itemDataPath);

                existingData = AssetDatabase.LoadAssetAtPath<ItemData>(itemDataPath);
                if (existingData == null)
                {
                    Debug.LogError(
                        $"[WeaponPrefabBuilder] Failed to create ItemData at '{itemDataPath}'. " +
                        $"Skipping '{w.PrefabName}'.");
                    return false;
                }

                // Wire all fields via SerializedObject — programmatic CreateInstance
                // does not apply Reset/inspector defaults (M6 lesson).
                var soNew = new SerializedObject(existingData);
                SetStringProp(soNew, "_id",          w.ItemId);
                SetStringProp(soNew, "_displayName", w.DisplayName);
                SetEnumProp  (soNew, "_slot",        (int)w.Slot);
                SetIntProp   (soNew, "_damage",      w.Damage);
                SetEnumProp  (soNew, "_rarity",      (int)ItemRarity.Common);
                SetObjectProp(soNew, "_worldPrefab", savedPrefab);
                soNew.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(existingData);

                itemDataCreated = true;
            }
            else
            {
                // Subsequent run: only refresh _worldPrefab — leave all other fields alone.
                var soExisting = new SerializedObject(existingData);
                SetObjectProp(soExisting, "_worldPrefab", savedPrefab);
                soExisting.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(existingData);
            }

            // ── Step 5: per-weapon log ────────────────────────────────────────
            Debug.Log(
                $"[WeaponPrefabBuilder] {w.PrefabName} — " +
                $"Prefab: {prefabPath} | " +
                $"ItemData: {itemDataPath} ({(itemDataCreated ? "CREATED" : "UPDATED worldPrefab only")}) | " +
                $"slot={w.Slot}, damage={w.Damage}");

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // SerializedObject helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Writes a string value to a named serialized property.
        /// Logs a descriptive error on field-not-found so type-drift is caught early.
        /// </summary>
        private static void SetStringProp(SerializedObject so, string fieldName, string value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[WeaponPrefabBuilder] {so.targetObject.GetType().Name} " +
                               $"has no serialized field '{fieldName}'. API drift?");
                return;
            }
            prop.stringValue = value;
        }

        /// <summary>
        /// Writes an int value to a named serialized property.
        /// Logs a descriptive error on field-not-found.
        /// </summary>
        private static void SetIntProp(SerializedObject so, string fieldName, int value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[WeaponPrefabBuilder] {so.targetObject.GetType().Name} " +
                               $"has no serialized field '{fieldName}'. API drift?");
                return;
            }
            prop.intValue = value;
        }

        /// <summary>
        /// Writes an enum value (as its underlying int) to a named serialized property.
        /// Logs a descriptive error on field-not-found.
        /// </summary>
        private static void SetEnumProp(SerializedObject so, string fieldName, int enumValue)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[WeaponPrefabBuilder] {so.targetObject.GetType().Name} " +
                               $"has no serialized field '{fieldName}'. API drift?");
                return;
            }
            prop.enumValueIndex = enumValue;
        }

        /// <summary>
        /// Writes a <see cref="UnityEngine.Object"/> reference to a named serialized property.
        /// Logs a descriptive error on field-not-found.
        /// </summary>
        private static void SetObjectProp(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[WeaponPrefabBuilder] {so.targetObject.GetType().Name} " +
                               $"has no serialized field '{fieldName}'. API drift?");
                return;
            }
            prop.objectReferenceValue = value;
        }

        // ════════════════════════════════════════════════════════════════════
        // Folder helper
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates <paramref name="name"/> under <paramref name="parent"/> if it
        /// does not already exist. Idempotent.
        /// </summary>
        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path)) return;
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrEmpty(guid))
                Debug.LogError($"[WeaponPrefabBuilder] Failed to create folder: {path}");
            else
                Debug.Log($"[WeaponPrefabBuilder] Created folder: {path}");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // WeaponEntry — data record (internal, shared with WeaponPrefabValidator)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Immutable data record for a single weapon. Shared between
    /// <see cref="WeaponPrefabBuilder"/> and <see cref="WeaponPrefabValidator"/>
    /// as the single source of truth for the 57-weapon library.
    /// </summary>
    internal struct WeaponEntry
    {
        /// <summary>Publisher prefab name (e.g. <c>OHS04_Sword</c>).</summary>
        public readonly string    PrefabName;

        /// <summary>Stable snake_case key for <c>ItemData._id</c>.</summary>
        public readonly string    ItemId;

        /// <summary>Human-readable name shown in inventory UI.</summary>
        public readonly string    DisplayName;

        /// <summary>Equipment slot.</summary>
        public readonly EquipSlot Slot;

        /// <summary>Default damage value written on first-create only.</summary>
        public readonly int       Damage;

        /// <summary>Local-space BoxCollider center estimate.</summary>
        public readonly Vector3   ColliderCenter;

        /// <summary>Local-space BoxCollider size estimate.</summary>
        public readonly Vector3   ColliderSize;

        public WeaponEntry(
            string    prefabName,
            string    itemId,
            string    displayName,
            EquipSlot slot,
            int       damage,
            Vector3   colliderCenter,
            Vector3   colliderSize)
        {
            PrefabName     = prefabName;
            ItemId         = itemId;
            DisplayName    = displayName;
            Slot           = slot;
            Damage         = damage;
            ColliderCenter = colliderCenter;
            ColliderSize   = colliderSize;
        }
    }
}
#endif
