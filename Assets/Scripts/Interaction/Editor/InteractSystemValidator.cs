// InteractSystemValidator.cs — read-only checks on M6 wiring.
//
// Single menu item:
//   LevelGen ▶ Interaction ▶ Validate Interact System
//
// 16 checks covering:
//   - Interactable abstract base + InteractPriority enum shape
//   - PlayerInteractor singleton + RequireComponent
//   - PlayerInputReader InteractPressed event + stub log removed
//   - PlayerCombat new public surface (override field, setter,
//     RequestAttack, IsBusy)
//   - AssassinateInteractable subclass shape
//   - Player_MaleHero.prefab tag + PlayerInteractor component
//   - Dummy.prefab _AssassinateZone child wiring

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Combat;
using LevelGen.Interaction;
using LevelGen.Player;

namespace LevelGen.Interaction.EditorTools
{
    public static class InteractSystemValidator
    {
        private const string InteractablePath          = "Assets/Scripts/Interaction/Interactable.cs";
        private const string AssassinateInteractablePath = "Assets/Scripts/Interaction/AssassinateInteractable.cs";
        private const string PlayerInteractorPath      = "Assets/Scripts/Player/PlayerInteractor.cs";
        private const string PlayerInputReaderSrc      = "Assets/Scripts/Player/PlayerInputReader.cs";
        private const string PlayerCombatSrc           = "Assets/Scripts/Player/PlayerCombat.cs";
        private const string PlayerPrefabPath          = "Assets/Prefabs/Character Prefabs/Player/Player_MaleHero.prefab";
        private const string DummyPrefabPath           = "Assets/Prefabs/Character Prefabs/Enemy/Dummy.prefab";

        [MenuItem("LevelGen/Interaction/Validate Interact System")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: Interactable.cs at expected path ─────────────────────────
            bool ok1 = AssetDatabase.LoadAssetAtPath<MonoScript>(InteractablePath) != null;
            Check("1 Interactable.cs exists at expected path", ok1,
                ok1 ? InteractablePath : $"missing at {InteractablePath}");

            // ── 2: Interactable abstract + abstract API ─────────────────────
            var interactableType = typeof(Interactable);
            var isEligible = interactableType.GetMethod("IsEligible",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(GameObject) }, null);
            var execute    = interactableType.GetMethod("Execute",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(GameObject) }, null);
            bool ok2 = interactableType.IsAbstract
                       && isEligible != null && isEligible.IsAbstract
                       && execute    != null && execute.IsAbstract;
            Check("2 Interactable is abstract + IsEligible(GameObject) + Execute(GameObject) abstract", ok2,
                $"isAbstract={interactableType.IsAbstract}, " +
                $"IsEligible={(isEligible == null ? "missing" : isEligible.IsAbstract ? "abstract" : "concrete")}, " +
                $"Execute={(execute == null ? "missing" : execute.IsAbstract ? "abstract" : "concrete")}");

            // ── 3: InteractPriority enum values ─────────────────────────────
            var priorityType = typeof(InteractPriority);
            int pickup      = priorityType.IsEnumDefined("Pickup")      ? (int)Enum.Parse(priorityType, "Pickup")      : -1;
            int open        = priorityType.IsEnumDefined("Open")        ? (int)Enum.Parse(priorityType, "Open")        : -1;
            int assassinate = priorityType.IsEnumDefined("Assassinate") ? (int)Enum.Parse(priorityType, "Assassinate") : -1;
            bool ok3 = pickup == 10 && open == 50 && assassinate == 100;
            Check("3 InteractPriority enum: Pickup=10, Open=50, Assassinate=100", ok3,
                $"Pickup={pickup}, Open={open}, Assassinate={assassinate}");

            // ── 4: PlayerInteractor.cs + Instance static property ───────────
            bool fileOk4 = AssetDatabase.LoadAssetAtPath<MonoScript>(PlayerInteractorPath) != null;
            var interactorType = typeof(PlayerInteractor);
            var instanceProp = interactorType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            bool ok4 = fileOk4
                       && instanceProp != null
                       && instanceProp.PropertyType == typeof(PlayerInteractor);
            Check("4 PlayerInteractor.cs exists + static Instance property", ok4,
                $"file={(fileOk4 ? "OK" : "missing")}, " +
                $"Instance={(instanceProp == null ? "missing" : instanceProp.PropertyType.Name)}");

            // ── 5: PlayerInteractor [RequireComponent(PlayerInputReader)] ──
            var requireAttrs = interactorType.GetCustomAttributes<RequireComponent>();
            bool requireReader = false;
            foreach (var a in requireAttrs)
            {
                if (a.m_Type0 == typeof(PlayerInputReader)
                    || a.m_Type1 == typeof(PlayerInputReader)
                    || a.m_Type2 == typeof(PlayerInputReader))
                { requireReader = true; break; }
            }
            Check("5 PlayerInteractor has [RequireComponent(typeof(PlayerInputReader))]", requireReader,
                requireReader ? "attribute present" : "attribute missing");

            // ── 6: PlayerInputReader source contains InteractPressed event ──
            string readerSrcFull = Path.Combine(Application.dataPath, "..", PlayerInputReaderSrc);
            bool ok6 = false;
            string detail6 = $"source missing at {PlayerInputReaderSrc}";
            if (File.Exists(readerSrcFull))
            {
                string src = File.ReadAllText(readerSrcFull);
                ok6 = src.Contains("event System.Action InteractPressed")
                      || src.Contains("event Action InteractPressed");
                detail6 = ok6 ? "InteractPressed event declared"
                              : "no 'event Action InteractPressed' declaration";
            }
            Check("6 PlayerInputReader.cs declares InteractPressed event", ok6, detail6);

            // ── 7: PlayerInputReader.OnInteract no longer logs ──────────────
            // The M1 stub was a literal Debug.Log("[PlayerInputReader] Interact").
            // Bracket-matching the method body would be more rigorous but
            // a fixed-width slice spilled into OnCrouch (which still has
            // its own M1-stub log) and produced a false positive. Direct
            // string match against the specific stub line is exact.
            bool ok7 = false;
            string detail7 = $"source missing at {PlayerInputReaderSrc}";
            if (File.Exists(readerSrcFull))
            {
                string src = File.ReadAllText(readerSrcFull);
                bool stubPresent = src.Contains("Debug.Log(\"[PlayerInputReader] Interact\"");
                ok7 = !stubPresent;
                detail7 = ok7
                    ? "M1 stub log line absent (Debug.Log(\"[PlayerInputReader] Interact\") removed)"
                    : "M1 stub log line still present";
            }
            Check("7 PlayerInputReader.OnInteract no longer logs (M1 stub removed)", ok7, detail7);

            // ── 8: PlayerCombat surface (override field + setter +
            //     RequestAttack + IsBusy) ──────────────────────────────────
            string combatSrcFull = Path.Combine(Application.dataPath, "..", PlayerCombatSrc);
            bool combatSrcOk = File.Exists(combatSrcFull);
            string combatSrc = combatSrcOk ? File.ReadAllText(combatSrcFull) : "";
            bool hasOverride    = combatSrc.Contains("_nextHitDamageOverride");
            bool hasSetter      = combatSrc.Contains("SetNextHitDamageOverride");
            bool hasRequest     = combatSrc.Contains("RequestAttack");
            bool hasIsBusy      = combatSrc.Contains("IsBusy");
            bool ok8 = combatSrcOk && hasOverride && hasSetter && hasRequest && hasIsBusy;
            Check("8 PlayerCombat surface: _nextHitDamageOverride + SetNextHitDamageOverride + RequestAttack + IsBusy", ok8,
                combatSrcOk
                    ? $"_nextHitDamageOverride={hasOverride}, SetNextHitDamageOverride={hasSetter}, " +
                      $"RequestAttack={hasRequest}, IsBusy={hasIsBusy}"
                    : $"source missing at {PlayerCombatSrc}");

            // ── 9: AssassinateInteractable subclasses Interactable ──────────
            bool fileOk9 = AssetDatabase.LoadAssetAtPath<MonoScript>(AssassinateInteractablePath) != null;
            var assassinateType = typeof(AssassinateInteractable);
            bool ok9 = fileOk9 && typeof(Interactable).IsAssignableFrom(assassinateType);
            Check("9 AssassinateInteractable.cs exists + subclasses Interactable", ok9,
                fileOk9
                    ? (ok9 ? "subclasses Interactable" : "type doesn't subclass Interactable")
                    : $"missing at {AssassinateInteractablePath}");

            // ── 10: AssassinateInteractable [RequireComponent(SphereCollider)] ──
            var assassinateRequires = assassinateType.GetCustomAttributes<RequireComponent>();
            bool requireSphere = false;
            foreach (var a in assassinateRequires)
            {
                if (a.m_Type0 == typeof(SphereCollider)
                    || a.m_Type1 == typeof(SphereCollider)
                    || a.m_Type2 == typeof(SphereCollider))
                { requireSphere = true; break; }
            }
            Check("10 AssassinateInteractable has [RequireComponent(typeof(SphereCollider))]", requireSphere,
                requireSphere ? "attribute present" : "attribute missing");

            // ── 11: Player_MaleHero.prefab has tag 'Player' ─────────────────
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            bool ok11 = playerPrefab != null && playerPrefab.CompareTag("Player");
            Check("11 Player_MaleHero.prefab has tag 'Player'", ok11,
                playerPrefab == null
                    ? $"prefab missing at {PlayerPrefabPath}"
                    : (ok11 ? "tag='Player'" : $"tag='{playerPrefab.tag}' (expected 'Player')"));

            // ── 12: Player_MaleHero.prefab has PlayerInteractor ─────────────
            PlayerInteractor interactor = null;
            if (playerPrefab != null) interactor = playerPrefab.GetComponent<PlayerInteractor>();
            Check("12 Player_MaleHero.prefab has PlayerInteractor on root", interactor != null,
                playerPrefab == null
                    ? "prefab missing — see check 11"
                    : (interactor != null
                        ? "found on prefab root"
                        : "missing — run 'LevelGen ▶ Player ▶ Add PlayerInteractor to Player_MaleHero Prefab'"));

            // ── 13: Dummy.prefab _AssassinateZone child + AssassinateInteractable ──
            var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            Transform zoneT = null;
            AssassinateInteractable zoneScript = null;
            if (dummyPrefab != null)
            {
                zoneT = dummyPrefab.transform.Find("_AssassinateZone");
                if (zoneT != null) zoneScript = zoneT.GetComponent<AssassinateInteractable>();
            }
            bool ok13 = zoneT != null && zoneScript != null;
            Check("13 Dummy.prefab has _AssassinateZone child + AssassinateInteractable", ok13,
                dummyPrefab == null
                    ? $"prefab missing at {DummyPrefabPath}"
                    : (zoneT == null
                        ? "no _AssassinateZone child — re-run 'Build Dummy Prefab'"
                        : zoneScript != null ? "wired correctly"
                                             : "_AssassinateZone present but no AssassinateInteractable on it"));

            // ── 14: _AssassinateZone SphereCollider isTrigger=true + radius>0 ──
            bool ok14 = false;
            string detail14 = "_AssassinateZone missing — see check 13";
            if (zoneT != null)
            {
                var sc = zoneT.GetComponent<SphereCollider>();
                if (sc == null)
                {
                    detail14 = "no SphereCollider on _AssassinateZone";
                }
                else
                {
                    ok14 = sc.isTrigger && sc.radius > 0f;
                    detail14 = $"isTrigger={sc.isTrigger}, radius={sc.radius}";
                }
            }
            Check("14 _AssassinateZone SphereCollider isTrigger=true + radius>0", ok14, detail14);

            // ── 15: AssassinateInteractable._targetStats wired ──────────────
            bool ok15 = false;
            string detail15 = "_AssassinateZone missing — see check 13";
            if (zoneScript != null)
            {
                var so = new SerializedObject(zoneScript);
                var prop = so.FindProperty("_targetStats");
                var assigned = prop != null ? prop.objectReferenceValue as CharacterStatsRuntime : null;
                ok15 = assigned != null;
                detail15 = ok15
                    ? $"wired to CharacterStatsRuntime on '{assigned.gameObject.name}'"
                    : "_targetStats unassigned";
            }
            Check("15 _AssassinateZone _targetStats reference wired", ok15, detail15);

            // ── 16: AssassinateInteractable._promptAnchor non-null ──────────
            bool ok16 = false;
            string detail16 = "_AssassinateZone missing — see check 13";
            if (zoneScript != null)
            {
                var so = new SerializedObject(zoneScript);
                var prop = so.FindProperty("_promptAnchor");
                var assigned = prop != null ? prop.objectReferenceValue as Transform : null;
                ok16 = assigned != null;
                detail16 = ok16
                    ? $"wired to Transform on '{assigned.gameObject.name}'"
                    : "_promptAnchor unassigned";
            }
            Check("16 _AssassinateZone _promptAnchor reference wired", ok16, detail16);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Interact system wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
