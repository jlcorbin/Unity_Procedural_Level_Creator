// ValidateInteraction.cs — consolidated interaction validator
// (M-MenuCleanup / M-MenuDelete).
//
// Originally replaced 2 per-milestone validators (M6 InteractSystem,
// M7 OpenInteractable). Updated in M-MenuDelete to retarget the
// Dummy-prefab checks to Enemy_Grunt.prefab after Dummy was retired.
//
// 16 read-only checks covering Interactable base + concrete subclasses
// + PlayerInteractor + wiring on Player_Hero + Enemy_Grunt prefabs.
// No scene modification.
//
// Menu: LevelGen ▶ Interaction ▶ Validate Interaction
//
// Spec notes — deviations from M-MenuCleanup spec:
// 1. Spec said `Execute()` and `CanExecute()` are abstract. Actual API
//    is `Execute(GameObject)` and `IsEligible(GameObject)`. Validator
//    checks the real names.
// 2. Spec check 12 said `PlayerInteractor._interactRadius > 0`. Per
//    architecture, `PlayerInteractor` has no radius field — radii live
//    on each Interactable subclass's SphereCollider. Replaced with a
//    check that PlayerInteractor exposes the singleton `Instance`
//    property (the actual stable surface).

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Interaction;
using LevelGen.Player;

namespace LevelGen.Interaction.EditorTools
{
    public static class ValidateInteraction
    {
        private const string InteractableSrcPath      = "Assets/Scripts/Interaction/Interactable.cs";
        private const string AssassinateSrcPath       = "Assets/Scripts/Interaction/AssassinateInteractable.cs";
        private const string OpenInteractableSrcPath  = "Assets/Scripts/Interaction/OpenInteractable.cs";
        private const string PlayerInteractorSrcPath  = "Assets/Scripts/Player/PlayerInteractor.cs";
        private const string PlayerInputReaderSrc     = "Assets/Scripts/Player/PlayerInputReader.cs";
        private const string PlayerPrefabPath         = "Assets/Prefabs/Character Prefabs/Player/Player_Hero.prefab";
        // M-MenuDelete: retargeted from Dummy.prefab to Enemy_Grunt.prefab
        // after Dummy retirement. Enemy_Grunt carries the _AssassinateZone
        // child built by EnemyBaseBuilder.BuildAssassinateZone.
        private const string EnemyPrefabPath          = "Assets/Prefabs/Character Prefabs/Enemy/Enemy_Grunt.prefab";
        private const string TestDoorPrefabPath       = "Assets/Prefabs/TestRig/TestDoor.prefab";

        [MenuItem("LevelGen/Interaction/Validate Interaction")]
        public static void Run()
        {
            int pass = 0, fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: Interactable.cs exists ────────────────────────────────────
            bool ok1 = AssetDatabase.LoadAssetAtPath<MonoScript>(InteractableSrcPath) != null;
            Check("1 Interactable.cs exists at expected path",
                ok1,
                ok1 ? InteractableSrcPath : $"missing at {InteractableSrcPath}");

            // ── 2: Interactable is abstract with IsEligible + Execute methods ─
            var interactableType = typeof(Interactable);
            bool isAbstract = interactableType.IsAbstract;
            var executeMethod = interactableType.GetMethod("Execute",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(GameObject) }, null);
            var isEligibleMethod = interactableType.GetMethod("IsEligible",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(GameObject) }, null);
            bool ok2 = isAbstract
                       && executeMethod != null && executeMethod.IsAbstract
                       && isEligibleMethod != null && isEligibleMethod.IsAbstract;
            string detail2 = $"abstract={isAbstract}, " +
                             $"Execute={(executeMethod != null ? (executeMethod.IsAbstract ? "abstract" : "concrete") : "missing")}, " +
                             $"IsEligible={(isEligibleMethod != null ? (isEligibleMethod.IsAbstract ? "abstract" : "concrete") : "missing")}";
            Check("2 Interactable is abstract with IsEligible(GameObject) + Execute(GameObject) abstract methods",
                ok2, detail2);

            // ── 3: RefreshPromptLabel() method exists ────────────────────────
            var refreshLabel = interactableType.GetMethod("RefreshPromptLabel",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            Check("3 Interactable.RefreshPromptLabel() public method",
                refreshLabel != null && refreshLabel.ReturnType == typeof(void),
                refreshLabel != null ? "signature OK" : "method missing");

            // ── 4: AssassinateInteractable.cs exists ─────────────────────────
            bool ok4 = AssetDatabase.LoadAssetAtPath<MonoScript>(AssassinateSrcPath) != null;
            Check("4 AssassinateInteractable.cs exists at expected path",
                ok4,
                ok4 ? AssassinateSrcPath : $"missing at {AssassinateSrcPath}");

            // ── 5: AssassinateInteractable extends Interactable ──────────────
            var assassinateType = typeof(AssassinateInteractable);
            bool ok5 = typeof(Interactable).IsAssignableFrom(assassinateType)
                       && assassinateType != typeof(Interactable);
            Check("5 AssassinateInteractable subclasses Interactable",
                ok5,
                ok5 ? "subclass confirmed" : $"BaseType={assassinateType.BaseType?.Name}");

            // ── 6: _AssassinateZone child on Enemy_Grunt prefab ──────────────
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            bool ok6 = false;
            string detail6 = $"Enemy_Grunt.prefab missing at {EnemyPrefabPath}";
            if (enemy != null)
            {
                var zone = FindChildByName(enemy.transform, "_AssassinateZone");
                ok6 = zone != null;
                detail6 = ok6
                    ? "_AssassinateZone child present"
                    : "missing — run 'LevelGen ▶ Combat ▶ Build Enemy_Grunt Prefab'";
            }
            Check("6 Enemy_Grunt.prefab has _AssassinateZone child", ok6, detail6);

            // ── 7: OpenInteractable.cs exists ────────────────────────────────
            bool ok7 = AssetDatabase.LoadAssetAtPath<MonoScript>(OpenInteractableSrcPath) != null;
            Check("7 OpenInteractable.cs exists at expected path",
                ok7,
                ok7 ? OpenInteractableSrcPath : $"missing at {OpenInteractableSrcPath}");

            // ── 8: OpenInteractable extends Interactable ─────────────────────
            var openType = typeof(OpenInteractable);
            bool ok8 = typeof(Interactable).IsAssignableFrom(openType)
                       && openType != typeof(Interactable);
            Check("8 OpenInteractable subclasses Interactable",
                ok8,
                ok8 ? "subclass confirmed" : $"BaseType={openType.BaseType?.Name}");

            // ── 9: TestDoor.prefab exists at Assets/Prefabs/TestRig/ ─────────
            var testDoor = AssetDatabase.LoadAssetAtPath<GameObject>(TestDoorPrefabPath);
            Check("9 TestDoor.prefab exists at Assets/Prefabs/TestRig/",
                testDoor != null,
                testDoor != null
                    ? TestDoorPrefabPath
                    : "missing — run 'LevelGen ▶ Interaction ▶ Build TestDoor Prefab'");

            // ── 10: PlayerInteractor.cs exists ───────────────────────────────
            bool ok10 = AssetDatabase.LoadAssetAtPath<MonoScript>(PlayerInteractorSrcPath) != null;
            Check("10 PlayerInteractor.cs exists at expected path",
                ok10,
                ok10 ? PlayerInteractorSrcPath : $"missing at {PlayerInteractorSrcPath}");

            // ── 11: PlayerInteractor on Player_Hero prefab ───────────────────
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerInteractor pi = null;
            if (player != null) pi = player.GetComponent<PlayerInteractor>();
            Check("11 Player_Hero.prefab has PlayerInteractor on root",
                pi != null,
                player == null
                    ? $"Player_Hero.prefab missing at {PlayerPrefabPath}"
                    : (pi != null ? "present" : "missing — re-run 'Build Player_Hero Prefab'"));

            // ── 12: PlayerInteractor exposes static Instance property ────────
            // Replaces the spec's "_interactRadius > 0" check — radii live on
            // each Interactable subclass's SphereCollider, not on the central
            // PlayerInteractor singleton. Validate the actual stable surface.
            var interactorType = typeof(PlayerInteractor);
            var instanceProp = interactorType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            Check("12 PlayerInteractor.Instance static property",
                instanceProp != null && instanceProp.PropertyType == typeof(PlayerInteractor),
                instanceProp != null
                    ? $"PropertyType={instanceProp.PropertyType.Name}"
                    : "Instance property missing");

            // ── 13: PlayerInputReader.InteractPressed event ──────────────────
            var readerType = typeof(PlayerInputReader);
            var interactEvt = readerType.GetEvent("InteractPressed",
                BindingFlags.Public | BindingFlags.Instance);
            Check("13 PlayerInputReader.InteractPressed event",
                interactEvt != null && interactEvt.EventHandlerType == typeof(Action),
                interactEvt != null
                    ? $"handlerType={interactEvt.EventHandlerType.Name}"
                    : "event missing");

            // ── 14: PlayerInputReader.OnInteract endpoint ────────────────────
            var onInteract = readerType.GetMethod("OnInteract",
                BindingFlags.Public | BindingFlags.Instance);
            Check("14 PlayerInputReader.OnInteract endpoint",
                onInteract != null,
                onInteract != null ? "signature OK" : "method missing");

            // ── 15: Enemy_Grunt has AssassinateInteractable on _AssassinateZone ─
            bool ok15 = false;
            string detail15 = "Enemy_Grunt.prefab missing";
            if (enemy != null)
            {
                var zone = FindChildByName(enemy.transform, "_AssassinateZone");
                if (zone != null)
                {
                    var ai = zone.GetComponent<AssassinateInteractable>();
                    ok15 = ai != null;
                    detail15 = ok15
                        ? "AssassinateInteractable on _AssassinateZone"
                        : "_AssassinateZone present but no AssassinateInteractable — re-run 'Build Enemy_Grunt Prefab'";
                }
                else
                {
                    detail15 = "no _AssassinateZone child (see check 6)";
                }
            }
            Check("15 Enemy_Grunt.prefab has AssassinateInteractable on _AssassinateZone", ok15, detail15);

            // ── 16: PlayerHero._interactor SerializeField is non-null ────────
            bool ok16 = false;
            string detail16 = "Player_Hero.prefab missing";
            if (player != null)
            {
                var hero = player.GetComponent<PlayerHero>();
                if (hero != null)
                {
                    var so = new SerializedObject(hero);
                    var prop = so.FindProperty("_interactor");
                    if (prop != null)
                    {
                        ok16 = prop.objectReferenceValue != null;
                        detail16 = ok16
                            ? $"wired to '{prop.objectReferenceValue.name}'"
                            : "field is null — re-run 'Build Player_Hero Prefab'";
                    }
                    else
                    {
                        detail16 = "_interactor field not found on PlayerHero";
                    }
                }
                else
                {
                    detail16 = "PlayerHero component missing — re-run 'Build Player_Hero Prefab'";
                }
            }
            Check("16 PlayerHero._interactor SerializeField non-null on Player_Hero.prefab", ok16, detail16);

            Summary(pass, fail);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Transform FindChildByName(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform c in parent)
            {
                var found = FindChildByName(c, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — Interaction wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
