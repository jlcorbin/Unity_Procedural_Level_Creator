// MouseLookValidator.cs — read-only checks on cursor-lock wiring.
//
// Two menu items:
//   LevelGen ▶ Input ▶ Validate MouseLook
//   LevelGen ▶ Input ▶ Place _MouseLock in Active Scene
//
// 7 checks covering:
//   - File moved to Assets/Scripts/Input/
//   - Old path no longer exists
//   - Type resolves and is MonoBehaviour
//   - DefaultExecutionOrder attribute set non-positive
//   - Namespace contains "Input" substring
//   - Active scene contains exactly one MouseLook
//   - Cursor state correct in Play mode (skipped in edit mode)

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LevelGen.Input;

namespace LevelGen.Input.EditorTools
{
    public static class MouseLookValidator
    {
        private const string NewPath = "Assets/Scripts/Input/MouseLook.cs";
        private const string OldPath = "Assets/Scripts/MouseLook.cs";
        private const string LockObjectName = "_MouseLock";

        [MenuItem("LevelGen/Input/Validate MouseLook")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;
            int skip = 0;

            void Check(string label, bool ok, string detail)
            {
                if (ok) { pass++; Debug.Log($"[Validator] PASS — {label}: {detail}"); }
                else    { fail++; Debug.LogError($"[Validator] FAIL — {label}: {detail}"); }
            }

            void Skip(string label, string detail)
            {
                skip++;
                Debug.Log($"[Validator] SKIP — {label}: {detail}");
            }

            // ── 1: File at new path ─────────────────────────────────────────
            bool newExists = AssetDatabase.LoadAssetAtPath<MonoScript>(NewPath) != null;
            Check("1 MouseLook.cs exists at Assets/Scripts/Input/", newExists,
                newExists ? NewPath : $"missing at {NewPath}");

            // ── 2: Old path is gone ─────────────────────────────────────────
            bool oldExists = AssetDatabase.LoadAssetAtPath<MonoScript>(OldPath) != null;
            Check("2 Old Assets/Scripts/MouseLook.cs no longer exists", !oldExists,
                oldExists ? "still present at old path — move was incomplete" : "clean");

            // ── 3: Type resolves + is MonoBehaviour ─────────────────────────
            var type = typeof(MouseLook);
            bool isMb = typeof(MonoBehaviour).IsAssignableFrom(type);
            Check("3 MouseLook type resolves and is a MonoBehaviour", isMb,
                $"type='{type.FullName}', MonoBehaviour={isMb}");

            // ── 4: [DefaultExecutionOrder] with value <= 0 ──────────────────
            var attr = type.GetCustomAttribute<DefaultExecutionOrder>();
            bool ok4 = attr != null && attr.order <= 0;
            Check("4 [DefaultExecutionOrder] present with non-positive order", ok4,
                attr == null
                    ? "attribute missing"
                    : $"order={attr.order} (expected <= 0)");

            // ── 5: Namespace contains "Input" ───────────────────────────────
            bool ok5 = type.Namespace != null && type.Namespace.Contains("Input");
            Check("5 MouseLook namespace contains 'Input'", ok5,
                $"namespace='{type.Namespace}'");

            // ── 6: Exactly one active MouseLook in current scene ────────────
            var active = SceneManager.GetActiveScene();
            int found = 0;
            string foundDetail = "";
            if (active.IsValid() && active.isLoaded)
            {
                var instances = Object.FindObjectsByType<MouseLook>(FindObjectsInactive.Exclude);
                found = instances.Length;
                if (found > 0)
                {
                    var names = new string[instances.Length];
                    for (int i = 0; i < instances.Length; i++) names[i] = instances[i].name;
                    foundDetail = $"on '{string.Join("', '", names)}' in scene '{active.name}'";
                }
                else
                {
                    foundDetail = $"none in scene '{active.name}' — " +
                        $"run 'LevelGen ▶ Input ▶ Place {LockObjectName} in Active Scene'";
                }
            }
            else
            {
                foundDetail = "no active scene loaded";
            }
            Check("6 Active scene contains exactly one active MouseLook instance",
                found == 1, $"found={found} {foundDetail}");

            // ── 7: Play-mode cursor state ───────────────────────────────────
            if (Application.isPlaying)
            {
                bool ok7 = Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;
                Check("7 Cursor.lockState == Locked AND Cursor.visible == false (Play mode)", ok7,
                    $"lockState={Cursor.lockState}, visible={Cursor.visible} (expected Locked / false)");
            }
            else
            {
                Skip("7 Cursor state in Play mode", "skipped — not in Play mode");
            }

            Summary(pass, fail, skip);
        }

        [MenuItem("LevelGen/Input/Place _MouseLock in Active Scene")]
        public static void PlaceInActiveScene()
        {
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded)
            {
                Debug.LogError("[MouseLookPlacer] No active scene loaded.");
                return;
            }

            var existing = Object.FindObjectsByType<MouseLook>(FindObjectsInactive.Include);
            if (existing.Length > 0)
            {
                Debug.Log($"[MouseLookPlacer] _MouseLock already present " +
                    $"({existing.Length} instance(s)). No changes.");
                Selection.activeObject = existing[0].gameObject;
                return;
            }

            var go = new GameObject(LockObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create _MouseLock");
            Undo.AddComponent<MouseLook>(go);
            EditorSceneManager.MarkSceneDirty(active);
            Selection.activeObject = go;

            Debug.Log($"[MouseLookPlacer] Placed '{LockObjectName}' with MouseLook " +
                $"in scene '{active.name}'. Save the scene to persist.");
        }

        private static void Summary(int pass, int fail, int skip)
        {
            string msg = $"[Validator] SUMMARY — {pass} PASS / {fail} FAIL / {skip} SKIP";
            if (fail == 0) Debug.Log(msg + " — MouseLook OK.");
            else           Debug.LogError(msg + " — see FAIL lines above.");
        }
    }
}
#endif
