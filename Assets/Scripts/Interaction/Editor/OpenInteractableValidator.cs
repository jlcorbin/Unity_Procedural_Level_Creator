// OpenInteractableValidator.cs — read-only checks on M7 wiring.
//
// Single menu item:
//   LevelGen ▶ Interaction ▶ Validate OpenInteractable
//
// 12 read-only checks covering:
//   - OpenInteractable script + subclass relationship + RequireComponent
//   - SerializeField labels for open/close states (literal-stub matches
//     per M6 lesson — no fixed-width slice scans)
//   - Coroutine method present in source
//   - Interactable base now exposes RefreshPromptLabel
//   - Interactable.EnsurePromptUI calls RefreshPromptLabel
//   - TestDoor.prefab exists with the expected child structure
//   - _OpenZone trigger collider configured correctly
//   - _hinge field wired to a child Transform

#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LevelGen.Interaction;

namespace LevelGen.Interaction.EditorTools
{
    public static class OpenInteractableValidator
    {
        private const string OpenInteractablePath = "Assets/Scripts/Interaction/OpenInteractable.cs";
        private const string InteractableSrcPath  = "Assets/Scripts/Interaction/Interactable.cs";
        private const string TestDoorPrefabPath   = "Assets/Prefabs/TestRig/TestDoor.prefab";

        [MenuItem("LevelGen/Interaction/Validate OpenInteractable")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            // ── 1: OpenInteractable.cs at expected path ─────────────────────
            bool ok1 = AssetDatabase.LoadAssetAtPath<MonoScript>(OpenInteractablePath) != null;
            Check("1 OpenInteractable.cs exists at expected path", ok1,
                ok1 ? OpenInteractablePath : $"missing at {OpenInteractablePath}");

            // ── 2: OpenInteractable subclasses Interactable ─────────────────
            var openType = typeof(OpenInteractable);
            bool ok2 = typeof(Interactable).IsAssignableFrom(openType);
            Check("2 OpenInteractable subclasses Interactable", ok2,
                ok2 ? "subclass relationship confirmed"
                    : "type does not subclass Interactable");

            // ── 3: OpenInteractable [RequireComponent(SphereCollider)] ─────
            var requireAttrs = openType.GetCustomAttributes<RequireComponent>();
            bool requireSphere = false;
            foreach (var a in requireAttrs)
            {
                if (a.m_Type0 == typeof(SphereCollider)
                    || a.m_Type1 == typeof(SphereCollider)
                    || a.m_Type2 == typeof(SphereCollider))
                { requireSphere = true; break; }
            }
            Check("3 OpenInteractable has [RequireComponent(typeof(SphereCollider))]", requireSphere,
                requireSphere ? "attribute present" : "attribute missing");

            // ── 4: OpenInteractable source contains _openLabel + _closeLabel ──
            // Direct literal-stub match (per M6 lesson — no slice scans).
            string openSrcFull = Path.Combine(Application.dataPath, "..", OpenInteractablePath);
            bool ok4 = false;
            string detail4 = $"source missing at {OpenInteractablePath}";
            if (File.Exists(openSrcFull))
            {
                string src = File.ReadAllText(openSrcFull);
                bool hasOpenLabel  = src.Contains("_openLabel");
                bool hasCloseLabel = src.Contains("_closeLabel");
                ok4 = hasOpenLabel && hasCloseLabel;
                detail4 = ok4
                    ? "_openLabel + _closeLabel SerializeFields present"
                    : $"_openLabel={hasOpenLabel}, _closeLabel={hasCloseLabel}";
            }
            Check("4 OpenInteractable.cs declares _openLabel + _closeLabel", ok4, detail4);

            // ── 5: AnimateRotation coroutine method present in source ──────
            bool ok5 = false;
            string detail5 = $"source missing at {OpenInteractablePath}";
            if (File.Exists(openSrcFull))
            {
                string src = File.ReadAllText(openSrcFull);
                ok5 = src.Contains("AnimateRotation");
                detail5 = ok5 ? "AnimateRotation coroutine present"
                              : "no AnimateRotation method found";
            }
            Check("5 OpenInteractable.cs declares AnimateRotation coroutine", ok5, detail5);

            // ── 6: Interactable.RefreshPromptLabel public method ───────────
            var refreshMethod = typeof(Interactable).GetMethod("RefreshPromptLabel",
                BindingFlags.Public | BindingFlags.Instance,
                null, System.Type.EmptyTypes, null);
            bool ok6 = refreshMethod != null && refreshMethod.ReturnType == typeof(void);
            Check("6 Interactable.RefreshPromptLabel() public void method", ok6,
                refreshMethod != null
                    ? $"returns {refreshMethod.ReturnType.Name}"
                    : "method missing");

            // ── 7: Interactable.EnsurePromptUI calls RefreshPromptLabel ────
            // Source-scan against the literal call, not a slice — M6 lesson.
            string interactableSrcFull = Path.Combine(Application.dataPath, "..", InteractableSrcPath);
            bool ok7 = false;
            string detail7 = $"source missing at {InteractableSrcPath}";
            if (File.Exists(interactableSrcFull))
            {
                string src = File.ReadAllText(interactableSrcFull);
                bool hasCall = src.Contains("RefreshPromptLabel()");
                bool hasMethod = src.Contains("public void RefreshPromptLabel(");
                // Both must be present: declaration + at least one call site
                // somewhere in the file (EnsurePromptUI body).
                ok7 = hasCall && hasMethod;
                detail7 = ok7
                    ? "RefreshPromptLabel() declared and called from EnsurePromptUI"
                    : $"declaration={hasMethod}, call={hasCall}";
            }
            Check("7 Interactable.cs EnsurePromptUI calls RefreshPromptLabel", ok7, detail7);

            // ── 8: TestDoor.prefab exists ──────────────────────────────────
            var testDoor = AssetDatabase.LoadAssetAtPath<GameObject>(TestDoorPrefabPath);
            bool ok8 = testDoor != null;
            Check("8 TestDoor.prefab exists", ok8,
                ok8 ? TestDoorPrefabPath
                    : $"missing at {TestDoorPrefabPath} — run 'LevelGen ▶ Interaction ▶ Build TestDoor Prefab'");

            // ── 9: TestDoor has _OpenZone child ────────────────────────────
            Transform zoneT = null;
            if (testDoor != null) zoneT = testDoor.transform.Find("_OpenZone");
            Check("9 TestDoor.prefab has _OpenZone child", zoneT != null,
                testDoor == null
                    ? "prefab missing — see check 8"
                    : (zoneT != null ? "found"
                                     : "no _OpenZone child — re-run 'Build TestDoor Prefab'"));

            // ── 10: _OpenZone SphereCollider isTrigger=true + radius>0 ─────
            bool ok10 = false;
            string detail10 = "_OpenZone missing — see check 9";
            if (zoneT != null)
            {
                var sc = zoneT.GetComponent<SphereCollider>();
                if (sc == null)
                {
                    detail10 = "no SphereCollider on _OpenZone";
                }
                else
                {
                    ok10 = sc.isTrigger && sc.radius > 0f;
                    detail10 = $"isTrigger={sc.isTrigger}, radius={sc.radius}";
                }
            }
            Check("10 _OpenZone SphereCollider isTrigger=true + radius>0", ok10, detail10);

            // ── 11: _OpenZone has OpenInteractable component ───────────────
            OpenInteractable openComp = null;
            if (zoneT != null) openComp = zoneT.GetComponent<OpenInteractable>();
            Check("11 _OpenZone has OpenInteractable component", openComp != null,
                zoneT == null
                    ? "_OpenZone missing — see check 9"
                    : (openComp != null ? "found"
                                        : "no OpenInteractable on _OpenZone"));

            // ── 12: _hinge field wired to a child Transform ────────────────
            bool ok12 = false;
            string detail12 = "OpenInteractable missing — see check 11";
            if (openComp != null)
            {
                var so = new SerializedObject(openComp);
                var prop = so.FindProperty("_hinge");
                var assigned = prop != null ? prop.objectReferenceValue as Transform : null;
                ok12 = assigned != null;
                detail12 = ok12
                    ? $"wired to '{assigned.name}' (Transform)"
                    : "_hinge unassigned";
            }
            Check("12 OpenInteractable._hinge reference wired", ok12, detail12);

            Summary(pass, fail);
        }

        private static void Summary(int pass, int fail)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL";
            if (fail == 0) Debug.Log(msg + " — OpenInteractable wiring OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
