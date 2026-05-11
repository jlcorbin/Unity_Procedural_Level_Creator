// PlayerCombatHitboxBuilder.cs — damage-routing prefab + animation authoring.
//
// Three menu items:
//   LevelGen ▶ Combat ▶ Add Weapon Hitbox to Player_Hero
//   LevelGen ▶ Combat ▶ Add Collider to Dummy
//   LevelGen ▶ Combat ▶ Add Animation Events to Attack Clips
//
// All three are idempotent. AnimationEvent edits go through the
// ModelImporter.clipAnimations API so changes survive FBX reimport.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Combat.EditorTools
{
    public static class PlayerCombatHitboxBuilder
    {
        // ── Paths ───────────────────────────────────────────────────────────
        private const string PlayerPrefabPath = "Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab";
        private const string DummyPrefabPath  = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string AttackClipsFolder =
            "Assets/AssetPacks/RPG Tiny Hero World Bundle/RPGTinyHeroWavePBR/Animation/SwordAndShield";

        // The Attack01-03 clips kept the publisher's _SwordAndShiled typo
        // (verified post-M3 pack swap; Idle's typo was corrected, Attack/Hit
        // were not). Reference these as canonical filenames.
        private static readonly string[] AttackFbxNames =
        {
            "Attack01_SwordAndShiled.fbx",
            "Attack02_SwordAndShiled.fbx",
            "Attack03_SwordAndShiled.fbx",
        };

        // ── Hitbox defaults ─────────────────────────────────────────────────
        private const string HitboxName    = "WeaponHitbox";
        // Bone names in MaleCharacterPBR's skeleton — weapon_r is the
        // right-hand attach point (SwordAndShield is right-handed).
        private static readonly string[] WeaponAttachCandidates =
            { "weapon_r", "weapon_l", "Weapon_R", "Weapon_L" };
        private static readonly Vector3 HitboxSize   = new Vector3(0.15f, 0.15f, 0.8f);
        private static readonly Vector3 HitboxCenter = new Vector3(0f, 0f, 0.4f);

        // ── Dummy collider defaults ─────────────────────────────────────────
        private const float DummyCapsuleRadius = 0.4f;
        private const float DummyCapsuleHeight = 1.8f;
        private static readonly Vector3 DummyCapsuleCenter = new Vector3(0f, 0.9f, 0f);

        // ── Animation event timing ──────────────────────────────────────────
        private const float HitboxOpenFraction  = 0.35f;
        private const float HitboxCloseFraction = 0.65f;
        private const string EventOpenName  = "OnHitboxOpen";
        private const string EventCloseName = "OnHitboxClose";

        // ════════════════════════════════════════════════════════════════════
        // Menu item: add weapon hitbox to Player_Hero
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Add Weapon Hitbox to Player_Hero")]
        private static void AddWeaponHitboxToPlayer()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[HitboxBuilder] Player prefab not found at {PlayerPrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var combat = contents.GetComponent<PlayerCombat>();
                if (combat == null)
                {
                    Debug.LogError("[HitboxBuilder] Player_Hero has no PlayerCombat. Run " +
                                   "'LevelGen ▶ Player ▶ Add PlayerCombat to Player_Hero Prefab' first.");
                    return;
                }

                var attach = FindWeaponAttach(contents.transform);
                if (attach == null)
                {
                    Debug.LogError("[HitboxBuilder] Could not find a weapon attach Transform in " +
                                   $"Player_Hero hierarchy. Looked for: {string.Join(", ", WeaponAttachCandidates)}.");
                    return;
                }

                // Reuse existing hitbox if already authored.
                Transform existing = attach.Find(HitboxName);
                GameObject hitboxGo;
                if (existing != null)
                {
                    hitboxGo = existing.gameObject;
                    Debug.Log($"[HitboxBuilder] Reusing existing '{HitboxName}' under '{attach.name}'.");
                }
                else
                {
                    hitboxGo = new GameObject(HitboxName);
                    hitboxGo.transform.SetParent(attach, worldPositionStays: false);
                    hitboxGo.transform.localPosition = Vector3.zero;
                    hitboxGo.transform.localRotation = Quaternion.identity;
                    hitboxGo.transform.localScale    = Vector3.one;
                    Debug.Log($"[HitboxBuilder] Created '{HitboxName}' under '{attach.name}'.");
                }

                // BoxCollider — create or update.
                // size/center are tuning values; only seed them on first
                // creation, then leave the user's edits alone on rebuild.
                var box = hitboxGo.GetComponent<BoxCollider>();
                bool freshBox = box == null;
                if (freshBox) box = hitboxGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.enabled   = false;     // default disabled — animation events open it
                if (freshBox)
                {
                    box.size   = HitboxSize;
                    box.center = HitboxCenter;
                }

                // Kinematic Rigidbody — required for OnTriggerEnter to fire.
                // The CharacterController on the prefab root doesn't promote
                // deeply-nested child colliders to "moving" status; the
                // WeaponHitbox sweeps via skeletal animation but Unity treats
                // it as a static collider without this. Kinematic = no physics
                // simulation, just tells the physics broadphase "I move".
                var rb = hitboxGo.GetComponent<Rigidbody>();
                if (rb == null) rb = hitboxGo.AddComponent<Rigidbody>();
                rb.isKinematic              = true;
                rb.useGravity               = false;
                rb.interpolation            = RigidbodyInterpolation.None;
                rb.collisionDetectionMode   = CollisionDetectionMode.Discrete;

                // HitboxRelay — create or update.
                var relay = hitboxGo.GetComponent<HitboxRelay>();
                if (relay == null) relay = hitboxGo.AddComponent<HitboxRelay>();
                var relaySo   = new SerializedObject(relay);
                var combatProp = relaySo.FindProperty("combat");
                if (combatProp != null)
                {
                    combatProp.objectReferenceValue = combat;
                    relaySo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(relay);
                }

                // PlayerCombat.hitbox — point at the BoxCollider on the new child.
                var combatSo = new SerializedObject(combat);
                var hitboxProp = combatSo.FindProperty("hitbox");
                if (hitboxProp != null)
                {
                    hitboxProp.objectReferenceValue = box;
                    combatSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(combat);
                }

                // AnimationEventForwarder on the Animator's GameObject —
                // Unity sends AnimationEvents to the Animator's own GO only,
                // not parents. Without the forwarder the events log
                // "no receiver" and damage never routes.
                var animator = contents.GetComponentInChildren<Animator>(includeInactive: true);
                if (animator == null)
                {
                    Debug.LogError("[HitboxBuilder] No Animator found in Player_Hero — " +
                                   "AnimationEventForwarder cannot be placed. " +
                                   "AnimationEvents will not reach PlayerCombat.");
                }
                else
                {
                    var fwd = animator.GetComponent<AnimationEventForwarder>();
                    if (fwd == null) fwd = animator.gameObject.AddComponent<AnimationEventForwarder>();
                    var fwdSo = new SerializedObject(fwd);
                    var fwdCombat = fwdSo.FindProperty("combat");
                    if (fwdCombat != null)
                    {
                        fwdCombat.objectReferenceValue = combat;
                        fwdSo.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(fwd);
                    }
                    Debug.Log($"[HitboxBuilder] AnimationEventForwarder placed on '{animator.name}' " +
                              "(Animator's GameObject). combat ref → prefab root PlayerCombat.");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                Debug.Log($"[HitboxBuilder] WeaponHitbox wired on Player_Hero. " +
                          $"Parent='{attach.name}', size={HitboxSize}, center={HitboxCenter}, " +
                          "isTrigger=true, enabled=false (default). " +
                          "PlayerCombat.hitbox + HitboxRelay.combat both assigned.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: add CapsuleCollider to Dummy
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Add Collider to Dummy")]
        private static void AddColliderToDummy()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[HitboxBuilder] Dummy prefab not found at {DummyPrefabPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(DummyPrefabPath);
            try
            {
                var existing = contents.GetComponent<CapsuleCollider>();
                if (existing != null)
                {
                    Debug.Log("[HitboxBuilder] CapsuleCollider already on Dummy root — no change.");
                    return;
                }

                var capsule = contents.AddComponent<CapsuleCollider>();
                capsule.isTrigger = false;
                capsule.radius    = DummyCapsuleRadius;
                capsule.height    = DummyCapsuleHeight;
                capsule.center    = DummyCapsuleCenter;
                capsule.direction = 1; // 1 = Y axis

                PrefabUtility.SaveAsPrefabAsset(contents, DummyPrefabPath);
                Debug.Log($"[HitboxBuilder] Added CapsuleCollider to Dummy. " +
                          $"radius={DummyCapsuleRadius}, height={DummyCapsuleHeight}, " +
                          $"center={DummyCapsuleCenter}, isTrigger=false.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Menu item: add AnimationEvents to Attack clips
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("LevelGen/Combat/Add Animation Events to Attack Clips")]
        private static void AddAnimationEventsToAttacks()
        {
            int processed = 0;
            int skipped   = 0;
            var lines = new List<string>();

            foreach (var fbxName in AttackFbxNames)
            {
                string fbxPath = $"{AttackClipsFolder}/{fbxName}";
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    skipped++;
                    lines.Add($"  SKIP {fbxName} — not found at {fbxPath}");
                    continue;
                }

                // Load the imported AnimationClip to get its real duration.
                AnimationClip clip = null;
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                {
                    if (sub is AnimationClip ac && !ac.name.StartsWith("__preview__"))
                    {
                        clip = ac;
                        break;
                    }
                }
                if (clip == null)
                {
                    skipped++;
                    lines.Add($"  SKIP {fbxName} — no AnimationClip sub-asset found");
                    continue;
                }

                float openTime  = clip.length * HitboxOpenFraction;
                float closeTime = clip.length * HitboxCloseFraction;

                var newEvents = new[]
                {
                    new AnimationEvent { time = openTime,  functionName = EventOpenName  },
                    new AnimationEvent { time = closeTime, functionName = EventCloseName },
                };

                // ModelImporter authoring: ensure clipAnimations is populated
                // (defaults are empty when the user hasn't overridden anything),
                // then write events on the entry whose name matches the clip.
                var clipAnims = importer.clipAnimations;
                if (clipAnims == null || clipAnims.Length == 0)
                    clipAnims = importer.defaultClipAnimations;

                bool wrote = false;
                for (int i = 0; i < clipAnims.Length; i++)
                {
                    if (clipAnims[i].name == clip.name || clipAnims[i].takeName == clip.name)
                    {
                        clipAnims[i].events = newEvents;
                        wrote = true;
                        break;
                    }
                }
                // Fallback: if no name matched (single-clip FBX is the common
                // case), write events on the first entry. The defaults array
                // mirrors what's actually in the FBX.
                if (!wrote && clipAnims.Length > 0)
                {
                    clipAnims[0].events = newEvents;
                    wrote = true;
                }

                if (!wrote)
                {
                    skipped++;
                    lines.Add($"  SKIP {fbxName} — no clipAnimations entries to write to");
                    continue;
                }

                importer.clipAnimations = clipAnims;
                importer.SaveAndReimport();

                processed++;
                lines.Add($"  OK   {fbxName} — duration={clip.length:F2}s, " +
                          $"OnHitboxOpen@{openTime:F3}s, OnHitboxClose@{closeTime:F3}s");
            }

            string summary = $"[HitboxBuilder] AnimationEvents: {processed} processed, {skipped} skipped.\n" +
                             string.Join("\n", lines);
            if (skipped > 0) Debug.LogWarning(summary);
            else             Debug.Log(summary);
        }

        // ════════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private static Transform FindWeaponAttach(Transform root)
        {
            // Recursive name search; returns the first match in the candidate
            // list order (weapon_r preferred over weapon_l for SwordAndShield).
            foreach (var candidate in WeaponAttachCandidates)
            {
                var found = FindByNameRecursive(root, candidate);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindByNameRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindByNameRecursive(t.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
#endif
