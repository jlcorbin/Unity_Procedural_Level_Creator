// DamageRoutingValidator.cs — read-only checks on damage routing wiring.
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Validate Damage Routing
//
// 12 checks covering:
//   - HitboxRelay type
//   - PlayerCombat new public methods + serialized fields
//   - CharacterStatsRuntime visibility promotion
//   - Player_MaleHero WeaponHitbox (BoxCollider trigger + HitboxRelay + wiring)
//   - PlayerCombat.hitbox field assignment
//   - Dummy collider
//   - Attack01-03 AnimationEvents (best-effort; warn-skip on missing FBX)

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Combat.EditorTools
{
    public static class DamageRoutingValidator
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const string DummyPrefabPath  = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string AttackClipsFolder =
            "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield";

        private static readonly string[] AttackFbxNames =
        {
            "Attack01_SwordAndShiled.fbx",
            "Attack02_SwordAndShiled.fbx",
            "Attack03_SwordAndShiled.fbx",
        };

        [MenuItem("LevelGen/Combat/Validate Damage Routing")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: HitboxRelay type exists ──────────────────────────────────
            var relayType = typeof(HitboxRelay);
            Check("1 HitboxRelay type in LevelGen.Combat",
                relayType.Namespace == "LevelGen.Combat",
                $"namespace='{relayType.Namespace}'");

            // ── 2: PlayerCombat new public methods ──────────────────────────
            var combatType = typeof(PlayerCombat);
            var openM   = combatType.GetMethod("OnHitboxOpen",          BindingFlags.Public | BindingFlags.Instance);
            var closeM  = combatType.GetMethod("OnHitboxClose",         BindingFlags.Public | BindingFlags.Instance);
            var notifyM = combatType.GetMethod("NotifyHitboxTriggered", BindingFlags.Public | BindingFlags.Instance);
            bool methodsOk = openM != null && closeM != null && notifyM != null;
            Check("2 PlayerCombat OnHitboxOpen/OnHitboxClose/NotifyHitboxTriggered public",
                methodsOk,
                $"OnHitboxOpen={(openM != null ? "OK" : "missing")}, " +
                $"OnHitboxClose={(closeM != null ? "OK" : "missing")}, " +
                $"NotifyHitboxTriggered={(notifyM != null ? "OK" : "missing")}");

            // ── 3: PlayerCombat new serialized fields ───────────────────────
            var dmgField = combatType.GetField("attackDamage", BindingFlags.NonPublic | BindingFlags.Instance);
            var hbField  = combatType.GetField("hitbox",       BindingFlags.NonPublic | BindingFlags.Instance);
            bool fieldsOk = dmgField != null && hbField != null;
            Check("3 PlayerCombat attackDamage / hitbox serialized fields", fieldsOk,
                $"attackDamage={(dmgField != null ? dmgField.FieldType.Name : "missing")}, " +
                $"hitbox={(hbField != null ? hbField.FieldType.Name : "missing")}");

            // ── 4: CharacterStatsRuntime.ApplyDamage public ─────────────────
            var statsType = typeof(CharacterStatsRuntime);
            var applyM = statsType.GetMethod("ApplyDamage", BindingFlags.Public | BindingFlags.Instance);
            Check("4 CharacterStatsRuntime.ApplyDamage public",
                applyM != null,
                applyM != null ? "found" : "missing or non-public (still internal?)");

            // ── 5: CharacterStatsRuntime.Heal public ────────────────────────
            var healM = statsType.GetMethod("Heal", BindingFlags.Public | BindingFlags.Instance);
            Check("5 CharacterStatsRuntime.Heal public",
                healM != null,
                healM != null ? "found" : "missing or non-public (still internal?)");

            // ── 6-9: Player_MaleHero WeaponHitbox wiring ────────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Check("6 Player_MaleHero has a WeaponHitbox child", false,
                    $"prefab missing at {PlayerPrefabPath}");
                Check("7 WeaponHitbox BoxCollider trigger=true, enabled=false", false,
                    "prefab missing — see check 6");
                Check("8 WeaponHitbox HitboxRelay.combat → prefab root PlayerCombat", false,
                    "prefab missing — see check 6");
                Check("9 PlayerCombat.hitbox field assigned to WeaponHitbox collider", false,
                    "prefab missing — see check 6");
            }
            else
            {
                var hitboxRelay = playerPrefab.GetComponentInChildren<HitboxRelay>(includeInactive: true);
                Check("6 Player_MaleHero has a WeaponHitbox child", hitboxRelay != null,
                    hitboxRelay != null
                        ? $"found at '{GetPath(playerPrefab.transform, hitboxRelay.transform)}'"
                        : "no HitboxRelay anywhere in hierarchy — run " +
                          "'LevelGen ▶ Combat ▶ Add Weapon Hitbox to Player_MaleHero'");

                if (hitboxRelay == null)
                {
                    Check("7 WeaponHitbox BoxCollider trigger=true, enabled=false", false,
                        "WeaponHitbox missing — see check 6");
                    Check("8 WeaponHitbox HitboxRelay.combat → prefab root PlayerCombat", false,
                        "WeaponHitbox missing — see check 6");
                }
                else
                {
                    var box = hitboxRelay.GetComponent<BoxCollider>();
                    bool ok7 = box != null && box.isTrigger && !box.enabled;
                    Check("7 WeaponHitbox BoxCollider trigger=true, enabled=false", ok7,
                        box == null
                            ? "BoxCollider missing on WeaponHitbox GameObject"
                            : $"isTrigger={box.isTrigger}, enabled={box.enabled} (expected true / false)");

                    var rootCombat = playerPrefab.GetComponent<PlayerCombat>();
                    var so = new SerializedObject(hitboxRelay);
                    var prop = so.FindProperty("combat");
                    var assigned = prop != null ? prop.objectReferenceValue as PlayerCombat : null;
                    bool ok8 = assigned != null && assigned == rootCombat;
                    Check("8 WeaponHitbox HitboxRelay.combat → prefab root PlayerCombat", ok8,
                        assigned == null
                            ? "HitboxRelay.combat unassigned"
                            : (assigned == rootCombat ? "wired correctly"
                                                      : $"wired to '{assigned.name}' (expected prefab root)"));
                }

                // 9 — PlayerCombat.hitbox field assignment.
                var combat = playerPrefab.GetComponent<PlayerCombat>();
                if (combat == null)
                {
                    Check("9 PlayerCombat.hitbox field assigned to WeaponHitbox collider", false,
                        "PlayerCombat missing on prefab root");
                }
                else
                {
                    var combatSo = new SerializedObject(combat);
                    var hbProp = combatSo.FindProperty("hitbox");
                    var assignedCol = hbProp != null ? hbProp.objectReferenceValue as Collider : null;
                    bool ok9 = assignedCol != null
                               && hitboxRelay != null
                               && assignedCol.gameObject == hitboxRelay.gameObject;
                    Check("9 PlayerCombat.hitbox field assigned to WeaponHitbox collider", ok9,
                        assignedCol == null
                            ? "PlayerCombat.hitbox unassigned"
                            : (hitboxRelay != null && assignedCol.gameObject == hitboxRelay.gameObject
                                ? $"assigned to BoxCollider on '{assignedCol.name}'"
                                : "PlayerCombat.hitbox points at a different GameObject than WeaponHitbox"));
                }
            }

            // ── 10: Dummy CapsuleCollider ───────────────────────────────────
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (dummyPrefab == null)
            {
                Check("10 Dummy.prefab has a CapsuleCollider", false,
                    $"prefab missing at {DummyPrefabPath}");
            }
            else
            {
                var caps = dummyPrefab.GetComponent<CapsuleCollider>();
                Check("10 Dummy.prefab has a CapsuleCollider", caps != null,
                    caps != null
                        ? $"radius={caps.radius}, height={caps.height}, center={caps.center}, isTrigger={caps.isTrigger}"
                        : "missing — run 'LevelGen ▶ Combat ▶ Add Collider to Dummy'");
            }

            // ── 11-12: Attack clip AnimationEvents (best-effort) ────────────
            int clipPass = 0;
            int clipFail = 0;
            int clipSkip = 0;
            var perClipDetail = new System.Text.StringBuilder();
            foreach (var fbxName in AttackFbxNames)
            {
                string fbxPath = $"{AttackClipsFolder}/{fbxName}";
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    clipSkip++;
                    perClipDetail.Append($"\n  {fbxName}: SKIP (FBX missing)");
                    continue;
                }

                var clipAnims = importer.clipAnimations;
                if (clipAnims == null || clipAnims.Length == 0)
                    clipAnims = importer.defaultClipAnimations;

                bool foundOpen  = false;
                bool foundClose = false;
                foreach (var ca in clipAnims)
                {
                    if (ca.events == null) continue;
                    foreach (var ev in ca.events)
                    {
                        if (ev.functionName == "OnHitboxOpen")  foundOpen  = true;
                        if (ev.functionName == "OnHitboxClose") foundClose = true;
                    }
                }

                if (foundOpen && foundClose)
                {
                    clipPass++;
                    perClipDetail.Append($"\n  {fbxName}: OK");
                }
                else
                {
                    clipFail++;
                    perClipDetail.Append($"\n  {fbxName}: FAIL " +
                        $"(OnHitboxOpen={(foundOpen ? "✓" : "✗")}, " +
                        $"OnHitboxClose={(foundClose ? "✓" : "✗")})");
                }
            }

            // 11: Attack01 specifically (the always-required one).
            var att01Importer = AssetImporter.GetAtPath($"{AttackClipsFolder}/{AttackFbxNames[0]}") as ModelImporter;
            bool att01Ok = false;
            string att01Detail = "Attack01 FBX missing";
            if (att01Importer != null)
            {
                var clipAnims = att01Importer.clipAnimations;
                if (clipAnims == null || clipAnims.Length == 0)
                    clipAnims = att01Importer.defaultClipAnimations;
                bool fo = false, fc = false;
                foreach (var ca in clipAnims)
                {
                    if (ca.events == null) continue;
                    foreach (var ev in ca.events)
                    {
                        if (ev.functionName == "OnHitboxOpen")  fo = true;
                        if (ev.functionName == "OnHitboxClose") fc = true;
                    }
                }
                att01Ok = fo && fc;
                att01Detail = $"OnHitboxOpen={(fo ? "✓" : "✗")}, OnHitboxClose={(fc ? "✓" : "✗")}";
            }
            Check("11 Attack01 has OnHitboxOpen + OnHitboxClose AnimationEvents", att01Ok, att01Detail);

            // 12: Attack02/Attack03 best-effort — pass if both present, warn if either FBX is missing.
            Check("12 Attack02 + Attack03 AnimationEvents (best-effort)",
                clipFail == 0 && clipSkip < AttackFbxNames.Length,
                $"{clipPass} pass / {clipFail} fail / {clipSkip} skip" + perClipDetail);

            Summary(pass, fail);
        }

        private static string GetPath(Transform root, Transform t)
        {
            if (t == root) return root.name;
            var stack = new System.Collections.Generic.List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Add(root.name);
            stack.Reverse();
            return string.Join("/", stack);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — damage routing OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
