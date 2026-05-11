// EnemyCombatValidator.cs — read-only checks on M11 wiring.
//
// Single menu item:
//   LevelGen ▶ Combat ▶ Validate Enemy Combat
//
// 16 read-only checks covering:
//   - EnemyCombat / EnemyHitboxRelay / EnemyAnimationEventForwarder
//     scripts present with the right surface
//   - EnemyCombat IsDead + friendly-fire guards (literal-stub matches
//     per M6 lesson — no fixed-width slice scans)
//   - EnemyAnimationEventAbsorber.cs deleted as part of M11 cleanup
//   - PlayerHitReaction.cs present + RequireComponent + sub/unsub +
//     TakeHit call (literal-stub matches)
//   - Player_Hero.prefab gains Targetable + PlayerHitReaction + tag
//   - Dummy.prefab gains EnemyCombat + Forwarder on child + EnemyWeaponHitbox

#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Player;

namespace LevelGen.Combat.EditorTools
{
    public static class EnemyCombatValidator
    {
        private const string EnemyCombatSrcPath        = "Assets/Scripts/Combat/EnemyCombat.cs";
        private const string EnemyHitboxRelaySrcPath   = "Assets/Scripts/Combat/EnemyHitboxRelay.cs";
        private const string EnemyForwarderSrcPath     = "Assets/Scripts/Combat/EnemyAnimationEventForwarder.cs";
        private const string EnemyAbsorberSrcPath      = "Assets/Scripts/Combat/EnemyAnimationEventAbsorber.cs";
        private const string PlayerHitReactionSrcPath  = "Assets/Scripts/Player/PlayerHitReaction.cs";
        private const string PlayerPrefabPath          = "Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab";
        private const string DummyPrefabPath           = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";
        private const string EnemyHitboxName           = "EnemyWeaponHitbox";

        [MenuItem("LevelGen/Combat/Validate Enemy Combat")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: EnemyCombat.NotifyHitboxTriggered(Collider) public ───────
            var ecType = typeof(EnemyCombat);
            var notifyM = ecType.GetMethod("NotifyHitboxTriggered",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Collider) }, null);
            Check("1 EnemyCombat.NotifyHitboxTriggered(Collider) public", notifyM != null,
                notifyM != null ? "found" : "method missing or wrong signature");

            // ── 2: EnemyCombat OnHitboxOpen + OnHitboxClose public ──────────
            var openM = ecType.GetMethod("OnHitboxOpen",
                BindingFlags.Public | BindingFlags.Instance,
                null, System.Type.EmptyTypes, null);
            var closeM = ecType.GetMethod("OnHitboxClose",
                BindingFlags.Public | BindingFlags.Instance,
                null, System.Type.EmptyTypes, null);
            bool ok2 = openM != null && closeM != null;
            Check("2 EnemyCombat OnHitboxOpen + OnHitboxClose public", ok2,
                ok2 ? "both found"
                    : $"OnHitboxOpen={openM != null}, OnHitboxClose={closeM != null}");

            // ── 3: EnemyCombat.cs source contains stats.IsDead guard ────────
            string ecSrcFull = Path.Combine(Application.dataPath, "..", EnemyCombatSrcPath);
            bool ok3 = false;
            string detail3 = $"source missing at {EnemyCombatSrcPath}";
            string ecSrc = null;
            if (File.Exists(ecSrcFull))
            {
                ecSrc = File.ReadAllText(ecSrcFull);
                ok3 = ecSrc.Contains("if (stats.IsDead) return;");
                detail3 = ok3
                    ? "stats.IsDead early-return guard present"
                    : "stats.IsDead guard not found — dead targets may receive damage events";
            }
            Check("3 EnemyCombat.cs contains 'if (stats.IsDead) return;' guard", ok3, detail3);

            // ── 4: EnemyCombat.cs source contains friendly-fire guard ───────
            bool ok4 = false;
            string detail4 = $"source missing at {EnemyCombatSrcPath}";
            if (ecSrc != null)
            {
                ok4 = ecSrc.Contains("stats.CompareTag(\"Player\")");
                detail4 = ok4
                    ? "friendly-fire guard (CompareTag(\"Player\")) present"
                    : "friendly-fire guard not found — enemies can damage non-Player targets";
            }
            Check("4 EnemyCombat.cs contains friendly-fire guard 'stats.CompareTag(\"Player\")'", ok4, detail4);

            // ── 5: EnemyHitboxRelay.cs has OnTriggerEnter ───────────────────
            bool ok5 = AssetDatabase.LoadAssetAtPath<MonoScript>(EnemyHitboxRelaySrcPath) != null;
            string detail5 = ok5 ? EnemyHitboxRelaySrcPath : $"missing at {EnemyHitboxRelaySrcPath}";
            if (ok5)
            {
                var trigM = typeof(EnemyHitboxRelay).GetMethod("OnTriggerEnter",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(Collider) }, null);
                ok5 = trigM != null;
                detail5 = trigM != null
                    ? "EnemyHitboxRelay.OnTriggerEnter(Collider) present"
                    : "EnemyHitboxRelay.OnTriggerEnter missing or wrong signature";
            }
            Check("5 EnemyHitboxRelay.cs present + OnTriggerEnter declared", ok5, detail5);

            // ── 6: EnemyAnimationEventForwarder.cs present + Open/Close ─────
            bool ok6 = AssetDatabase.LoadAssetAtPath<MonoScript>(EnemyForwarderSrcPath) != null;
            string detail6 = ok6 ? EnemyForwarderSrcPath : $"missing at {EnemyForwarderSrcPath}";
            if (ok6)
            {
                var fwdType = typeof(EnemyAnimationEventForwarder);
                var fOpen   = fwdType.GetMethod("OnHitboxOpen",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, System.Type.EmptyTypes, null);
                var fClose  = fwdType.GetMethod("OnHitboxClose",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, System.Type.EmptyTypes, null);
                ok6 = fOpen != null && fClose != null;
                detail6 = ok6
                    ? "EnemyAnimationEventForwarder.OnHitboxOpen + OnHitboxClose present"
                    : $"OnHitboxOpen={fOpen != null}, OnHitboxClose={fClose != null}";
            }
            Check("6 EnemyAnimationEventForwarder.cs present + OnHitboxOpen/Close public", ok6, detail6);

            // ── 7: EnemyAnimationEventAbsorber.cs DELETED (M11 cleanup) ─────
            bool absorberGone = AssetDatabase.LoadAssetAtPath<MonoScript>(EnemyAbsorberSrcPath) == null;
            // Belt-and-suspenders: also check the file system since
            // AssetDatabase can lag if the user hasn't refreshed yet.
            string absorberFull = Path.Combine(Application.dataPath, "..", EnemyAbsorberSrcPath);
            bool absorberFileGone = !File.Exists(absorberFull);
            bool ok7 = absorberGone && absorberFileGone;
            Check("7 EnemyAnimationEventAbsorber.cs deleted (M10 stub gone)", ok7,
                ok7 ? "absorber file removed"
                    : $"AssetDatabase missing={absorberGone}, file system missing={absorberFileGone} — " +
                      "delete Assets/Scripts/Combat/EnemyAnimationEventAbsorber.cs and its .meta");

            // ── 8: PlayerHitReaction has [RequireComponent(Targetable)] ─────
            bool ok8 = AssetDatabase.LoadAssetAtPath<MonoScript>(PlayerHitReactionSrcPath) != null;
            string detail8 = ok8 ? PlayerHitReactionSrcPath : $"missing at {PlayerHitReactionSrcPath}";
            if (ok8)
            {
                var phrType  = typeof(PlayerHitReaction);
                var requires = phrType.GetCustomAttributes<RequireComponent>();
                bool hasTargetable = false;
                foreach (var a in requires)
                {
                    if (a.m_Type0 == typeof(Targetable)
                        || a.m_Type1 == typeof(Targetable)
                        || a.m_Type2 == typeof(Targetable))
                    { hasTargetable = true; break; }
                }
                ok8 = hasTargetable;
                detail8 = hasTargetable
                    ? "[RequireComponent(typeof(Targetable))] present"
                    : "[RequireComponent(typeof(Targetable))] missing";
            }
            Check("8 PlayerHitReaction.cs present + [RequireComponent(Targetable)]", ok8, detail8);

            // ── 9: PlayerHitReaction subscribes AND unsubscribes ────────────
            bool ok9 = false;
            string detail9 = $"source missing at {PlayerHitReactionSrcPath}";
            string phrSrcFull = Path.Combine(Application.dataPath, "..", PlayerHitReactionSrcPath);
            string phrSrc = null;
            if (File.Exists(phrSrcFull))
            {
                phrSrc = File.ReadAllText(phrSrcFull);
                bool hasSub   = phrSrc.Contains("_targetable.OnHit += HandleHit");
                bool hasUnsub = phrSrc.Contains("_targetable.OnHit -= HandleHit");
                ok9 = hasSub && hasUnsub;
                detail9 = ok9
                    ? "subscribes (+=) and unsubscribes (-=) HandleHit"
                    : $"subscribe={hasSub}, unsubscribe={hasUnsub} — both required to prevent leaks";
            }
            Check("9 PlayerHitReaction.cs subscribes AND unsubscribes _targetable.OnHit", ok9, detail9);

            // ── 10: PlayerHitReaction calls _combat.TakeHit() ───────────────
            bool ok10 = false;
            string detail10 = $"source missing at {PlayerHitReactionSrcPath}";
            if (phrSrc != null)
            {
                ok10 = phrSrc.Contains("_combat.TakeHit()");
                detail10 = ok10
                    ? "_combat.TakeHit() call present"
                    : "_combat.TakeHit() call not found — flinch will not fire";
            }
            Check("10 PlayerHitReaction.cs contains '_combat.TakeHit()' call", ok10, detail10);

            // ── 11: Player_Hero.prefab has Targetable component ─────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            bool ok11 = false;
            string detail11 = $"prefab missing at {PlayerPrefabPath}";
            if (playerPrefab != null)
            {
                ok11 = playerPrefab.GetComponent<Targetable>() != null;
                detail11 = ok11
                    ? "Targetable on Player root"
                    : "Targetable missing — run 'LevelGen ▶ Player ▶ Add Targetable to Player_Hero Prefab'";
            }
            Check("11 Player_Hero.prefab has Targetable on root", ok11, detail11);

            // ── 12: Player_Hero.prefab has PlayerHitReaction ────────────
            bool ok12 = false;
            string detail12 = $"prefab missing at {PlayerPrefabPath}";
            if (playerPrefab != null)
            {
                ok12 = playerPrefab.GetComponent<PlayerHitReaction>() != null;
                detail12 = ok12
                    ? "PlayerHitReaction on Player root"
                    : "PlayerHitReaction missing — run 'LevelGen ▶ Player ▶ Add PlayerHitReaction to Player_Hero Prefab'";
            }
            Check("12 Player_Hero.prefab has PlayerHitReaction on root", ok12, detail12);

            // ── 13: Player_Hero.prefab has tag 'Player' ─────────────────
            bool ok13 = false;
            string detail13 = $"prefab missing at {PlayerPrefabPath}";
            if (playerPrefab != null)
            {
                ok13 = playerPrefab.CompareTag("Player");
                detail13 = ok13
                    ? "tag='Player'"
                    : $"tag='{playerPrefab.tag}' (expected 'Player' — friendly-fire guard would block all hits)";
            }
            Check("13 Player_Hero.prefab tag = 'Player'", ok13, detail13);

            // ── 14: Dummy.prefab has EnemyCombat ────────────────────────────
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            bool ok14 = false;
            string detail14 = $"prefab missing at {DummyPrefabPath}";
            EnemyCombat dummyCombat = null;
            if (dummyPrefab != null)
            {
                dummyCombat = dummyPrefab.GetComponent<EnemyCombat>();
                ok14 = dummyCombat != null;
                detail14 = ok14
                    ? "EnemyCombat on Dummy root"
                    : "EnemyCombat missing — re-run 'LevelGen ▶ Combat ▶ Build Dummy Prefab'";
            }
            Check("14 Dummy.prefab has EnemyCombat on root", ok14, detail14);

            // ── 15: Dummy MaleCharacterPBR child has Forwarder (not Absorber) ──
            bool ok15 = false;
            string detail15 = "Dummy prefab missing — see check 14";
            if (dummyPrefab != null)
            {
                var animOnChild = dummyPrefab.GetComponentInChildren<Animator>(includeInactive: true);
                if (animOnChild == null)
                {
                    detail15 = "no child Animator on Dummy";
                }
                else
                {
                    var fwd = animOnChild.GetComponent<EnemyAnimationEventForwarder>();
                    ok15 = fwd != null;
                    detail15 = ok15
                        ? $"EnemyAnimationEventForwarder on '{animOnChild.gameObject.name}'"
                        : $"EnemyAnimationEventForwarder missing on '{animOnChild.gameObject.name}' — " +
                          "re-run 'Build Dummy Prefab'";
                }
            }
            Check("15 Dummy MaleCharacterPBR child has EnemyAnimationEventForwarder (M11)", ok15, detail15);

            // ── 16: Dummy has EnemyWeaponHitbox child wired correctly ───────
            bool ok16 = false;
            string detail16 = "Dummy prefab missing — see check 14";
            if (dummyPrefab != null)
            {
                Transform hitboxT = FindByNameRecursive(dummyPrefab.transform, EnemyHitboxName);
                if (hitboxT == null)
                {
                    detail16 = $"no '{EnemyHitboxName}' child found in Dummy hierarchy — re-run 'Build Dummy Prefab'";
                }
                else
                {
                    var box   = hitboxT.GetComponent<BoxCollider>();
                    var rb    = hitboxT.GetComponent<Rigidbody>();
                    var relay = hitboxT.GetComponent<EnemyHitboxRelay>();
                    bool boxOk   = box != null && box.isTrigger && !box.enabled;
                    bool rbOk    = rb != null && rb.isKinematic && !rb.useGravity;
                    bool relayOk = false;
                    if (relay != null)
                    {
                        var so = new SerializedObject(relay);
                        var prop = so.FindProperty("_combat");
                        relayOk = prop != null && prop.objectReferenceValue == dummyCombat;
                    }
                    ok16 = boxOk && rbOk && relayOk;
                    detail16 = ok16
                        ? $"BoxCollider trigger+disabled, kinematic Rigidbody, EnemyHitboxRelay._combat wired"
                        : $"BoxCollider(trigger+disabled)={boxOk}, kinematicRigidbody={rbOk}, " +
                          $"EnemyHitboxRelay._combat={relayOk}";
                }
            }
            Check("16 Dummy.prefab EnemyWeaponHitbox child fully wired (BoxCollider + Rigidbody + Relay)", ok16, detail16);

            // ── 17: Player_Hero.prefab CharacterController.radius >= 0.35 ──
            // Q5: bumped 0.3 → 0.4 in M11 for symmetric combat reach with the
            // Dummy's CapsuleCollider (also 0.4). Loose lower bound (0.35)
            // catches "someone reset it to the 0.3 default" without locking
            // to the specific 0.4 — leaves room for tuning.
            bool ok17 = false;
            string detail17 = $"prefab missing at {PlayerPrefabPath}";
            if (playerPrefab != null)
            {
                var cc = playerPrefab.GetComponent<CharacterController>();
                if (cc == null)
                {
                    detail17 = "Player_Hero has no CharacterController";
                }
                else
                {
                    ok17 = cc.radius >= 0.35f;
                    detail17 = ok17
                        ? $"CharacterController.radius={cc.radius:F3} (≥ 0.35)"
                        : $"CharacterController.radius={cc.radius:F3} (< 0.35) — " +
                          "run 'LevelGen ▶ Player ▶ Tune CharacterController for Hit Reception'";
                }
            }
            Check("17 Player_Hero.prefab CharacterController.radius >= 0.35 (M11 Q5)", ok17, detail17);

            Summary(pass, fail);
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

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Enemy combat wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
