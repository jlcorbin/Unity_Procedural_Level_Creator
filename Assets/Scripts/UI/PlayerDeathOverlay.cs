// PlayerDeathOverlay.cs — passive observer of PlayerDeath.OnPlayerDied.
//
// Lives on the prefab root of PlayerDeathOverlay.prefab. Finds
// Player_Hero once at Start (with a polling retry coroutine
// for deferred-spawn scenarios — same pattern as PlayerHUD), then
// subscribes to its PlayerDeath.OnPlayerDied event.
//
// On death:
//   - Show the Canvas child (full-screen "You Died" + Restart button).
//   - Unlock cursor so the user can click Restart (overrides MouseLook
//     for this one-shot; scene reload re-locks normally).
// On Restart click:
//   - SceneManager.LoadScene(activeScene.buildIndex) reloads the
//     current scene. // TODO M5-followup: in-place respawn alternative
//     once spawn-point architecture / respawn semantics are defined.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LevelGen.Player;

namespace LevelGen.UI
{
    /// <summary>
    /// Subscribes to <see cref="PlayerDeath.OnPlayerDied"/> on the
    /// player tagged with <see cref="playerTag"/>; on fire, shows the
    /// "You Died" Canvas child and unlocks the cursor. The Restart
    /// button reloads the active scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDeathOverlay : MonoBehaviour
    {
        [Header("Wiring (assigned in prefab)")]
        [SerializeField]
        [Tooltip("Canvas child to show on death. Hidden in the prefab.")]
        private Canvas _canvas;

        [SerializeField]
        [Tooltip("Restart button. Click reloads the active scene.")]
        private Button _restartButton;

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Tag used to find the Player. Default 'Player'.")]
        private string playerTag = "Player";

        // Runtime state — not serialized.
        private PlayerDeath _playerDeath;
        private bool        _isBound;
        private Coroutine   _bindLoop;

        public bool IsBound => _isBound;

        private void Awake()
        {
            if (_canvas == null || _restartButton == null)
            {
                Debug.LogError("[PlayerDeathOverlay] One or more serialized refs are unassigned. " +
                               "Disabling component. Run 'LevelGen ▶ UI ▶ Build PlayerDeathOverlay Prefab' " +
                               "to regenerate.", this);
                enabled = false;
                return;
            }

            // Hidden by default. The prefab also ships with the canvas
            // GameObject inactive, but force here too in case an instance
            // was hand-edited.
            _canvas.gameObject.SetActive(false);

            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(OnRestartClicked);

            EnsureRuntimeEventSystem();
        }

        /// <summary>
        /// UGUI button clicks require an EventSystem in the scene with a
        /// working input module. Scenes from
        /// EditorSceneManager.NewScene(DefaultGameObjects) don't ship one,
        /// and a pre-existing EventSystem with the legacy
        /// StandaloneInputModule silently won't dispatch in projects using
        /// InputSystem-only mode. This defensive runtime ensure creates a
        /// new EventSystem (with InputSystemUIInputModule) if either case
        /// holds. Idempotent and safe — never modifies an EventSystem the
        /// user has already wired correctly.
        /// </summary>
        private static void EnsureRuntimeEventSystem()
        {
            var existing = FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                // Already has a working InputSystemUIInputModule? Great.
                if (existing.GetComponent<InputSystemUIInputModule>() != null) return;

                // Pre-existing EventSystem but its module is the legacy one
                // (or none). In InputSystem-only projects the legacy module
                // silently does nothing — swap it out so the button works.
                var legacy = existing.GetComponent<BaseInputModule>();
                if (legacy != null) Destroy(legacy);
                existing.gameObject.AddComponent<InputSystemUIInputModule>();
                Debug.LogWarning("[PlayerDeathOverlay] Pre-existing EventSystem had no " +
                                 "InputSystemUIInputModule — added one at runtime so the " +
                                 "Restart button click can dispatch. To silence this, run " +
                                 "'LevelGen ▶ UI ▶ Place PlayerDeathOverlay in Active Scene' " +
                                 "(idempotent — won't add duplicates).");
                return;
            }

            // No EventSystem at all — create one.
            var go = new GameObject("EventSystem (runtime)");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Debug.LogWarning("[PlayerDeathOverlay] No EventSystem in scene — created one at " +
                             "runtime so the Restart button click can dispatch. To silence " +
                             "this, run 'LevelGen ▶ UI ▶ Place PlayerDeathOverlay in Active Scene'.");
        }

        private void Start()
        {
            if (!TryBindToPlayer())
                _bindLoop = StartCoroutine(PollForPlayer());
        }

        private void OnDisable()
        {
            if (_playerDeath != null) _playerDeath.OnPlayerDied -= HandlePlayerDied;
            if (_bindLoop != null)
            {
                StopCoroutine(_bindLoop);
                _bindLoop = null;
            }
        }

        private void HandlePlayerDied(PlayerDeath _)
        {
            _canvas.gameObject.SetActive(true);
            // MouseLook locks the cursor on Play start; unlock so the
            // overlay's Restart button is clickable. Scene reload runs
            // MouseLook.OnEnable again, which re-locks.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        private void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            // Keyboard fallback. Works regardless of EventSystem state.
            var kb = Keyboard.current;
            if (kb != null &&
                (kb.rKey.wasPressedThisFrame
                 || kb.enterKey.wasPressedThisFrame
                 || kb.numpadEnterKey.wasPressedThisFrame))
            {
                OnRestartClicked();
                return;
            }

            // Manual mouse-over-button check — bypasses the EventSystem +
            // InputSystemUIInputModule dispatch chain entirely. Necessary
            // when the module is present but has no UI actions bound
            // (which happens when it's added programmatically rather than
            // via GameObject ▶ UI ▶ Event System, since the editor flow
            // assigns default actions and AddComponent does not).
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (_restartButton == null || !_restartButton.interactable) return;
            var rt = _restartButton.GetComponent<RectTransform>();
            if (rt == null) return;
            // ScreenSpaceOverlay canvas uses screen pixel coords; pass
            // null camera per RectTransformUtility convention.
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    rt, mouse.position.ReadValue(), null))
            {
                OnRestartClicked();
            }
        }

        private void OnRestartClicked()
        {
            // TODO M5-followup: in-place respawn alternative once
            // spawn-point architecture / respawn semantics are defined.
            // Reload covers the test case for now.
            var active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }

        private bool TryBindToPlayer()
        {
            var all = GameObject.FindGameObjectsWithTag(playerTag);
            if (all.Length > 1)
                Debug.LogWarning($"[PlayerDeathOverlay] {all.Length} GameObjects tagged '{playerTag}' " +
                                 "in scene. Binding to the first.", this);

            var player = all.Length > 0 ? all[0] : null;
            if (player == null) return false;

            var death = player.GetComponent<PlayerDeath>();
            if (death == null)
            {
                Debug.LogWarning($"[PlayerDeathOverlay] '{player.name}' has no PlayerDeath. " +
                                 "Run 'LevelGen ▶ Player ▶ Build Player_Hero Prefab' " +
                                 "to wire it.", this);
                return false;
            }

            _playerDeath = death;
            _playerDeath.OnPlayerDied += HandlePlayerDied;
            _isBound = true;
            return true;
        }

        private IEnumerator PollForPlayer()
        {
            var wait = new WaitForSeconds(0.5f);
            while (!_isBound)
            {
                yield return wait;
                if (TryBindToPlayer()) break;
            }
            _bindLoop = null;
        }
    }
}
