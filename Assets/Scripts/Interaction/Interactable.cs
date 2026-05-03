// Interactable.cs — abstract base for the M6 interact system.
//
// Contract:
//   - Lives on a GameObject with a trigger collider (self or child).
//   - On player tag enter: cache player ref, evaluate IsEligible.
//     If eligible, register with PlayerInteractor.
//   - Per-frame in Update: if a player is in range, re-evaluate
//     IsEligible and edge-detect register / deregister flips
//     (e.g. player walks around to the front of an enemy).
//   - On player exit: deregister and clear the cached ref.
//   - PlayerInteractor picks the registered Interactable with the
//     highest InteractPriority and calls Execute on E-press.
//
// Subclasses ship behavior by overriding IsEligible(GameObject)
// and Execute(GameObject). They never touch PlayerInteractor
// directly — registration is the base's responsibility.
//
// Prompt UI: each Interactable owns its own World Space Canvas
// child (auto-built via EnsurePromptUI). The prompt is shown by
// the active Interactable only — PlayerInteractor toggles it.

using TMPro;
using UnityEngine;

namespace LevelGen.Interaction
{
    /// <summary>
    /// Static priority ordering for interactable picks. Higher value =
    /// higher priority. PlayerInteractor picks the highest-priority
    /// interactable from its registered set; ties resolve to the
    /// first-registered (documented but not relied on).
    /// </summary>
    public enum InteractPriority
    {
        Pickup      = 10,
        Open        = 50,
        Assassinate = 100,
    }

    /// <summary>
    /// Abstract base for any object the player can interact with via
    /// E-press. Subclass and override <see cref="IsEligible"/> +
    /// <see cref="Execute"/>. The base handles trigger detection,
    /// register/deregister against <see cref="LevelGen.Player.PlayerInteractor"/>,
    /// and prompt-UI build/visibility.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class Interactable : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────

        [Header("Interactable")]
        [Tooltip("Static priority ordering. Higher = more important. " +
                 "Picks the highest-priority registered interactable.")]
        [SerializeField] protected InteractPriority _priority = InteractPriority.Pickup;

        [Tooltip("Short label shown in the prompt. Renders as 'Press [E] {label}'.")]
        [SerializeField] protected string _promptLabel = "Interact";

        [Tooltip("Transform that anchors the world-space prompt UI. " +
                 "Auto-set to this transform on Reset; override for head-height etc.")]
        [SerializeField] protected Transform _promptAnchor;

        [Tooltip("Tag the trigger looks for to identify the player. " +
                 "Default 'Player' — must match Player_MaleHero.prefab tag.")]
        [SerializeField] protected string _playerTag = "Player";

        // ── Runtime state ──────────────────────────────────────────────────

        protected GameObject _playerInRange;
        private   bool       _registered;
        private   Canvas     _promptCanvas;     // child Canvas built by EnsurePromptUI
        private   TMP_Text   _promptLabelText;  // child TMP_Text inside the canvas
        private   GameObject _promptRoot;       // GameObject hosting the canvas

        // ── Public read API ────────────────────────────────────────────────

        public InteractPriority Priority     => _priority;
        public string           PromptLabel  => _promptLabel;
        public Transform        PromptAnchor => _promptAnchor != null ? _promptAnchor : transform;
        public bool             IsRegistered => _registered;

        // ── Abstract API ───────────────────────────────────────────────────

        /// <summary>
        /// Returns true when this interactable is currently usable by the
        /// given player (within back-arc, target alive, etc.). Called on
        /// trigger enter and re-evaluated each frame the player is in
        /// range. Subclasses must override.
        /// </summary>
        public abstract bool IsEligible(GameObject player);

        /// <summary>
        /// Runs the interaction. Called by <see cref="LevelGen.Player.PlayerInteractor"/>
        /// when this is the active interactable and the player presses
        /// the Interact action. Subclasses must override.
        /// </summary>
        public abstract void Execute(GameObject player);

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected virtual void Reset()
        {
            if (_promptAnchor == null) _promptAnchor = transform;
            // Reset only fires from the editor menu (not programmatic
            // AddComponent). Builders that create Interactables via script
            // call EnsurePromptUI explicitly.
            EnsurePromptUI();
        }

        protected virtual void Awake()
        {
            EnsurePromptUI();
            SetPromptVisible(false);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(_playerTag) || !other.CompareTag(_playerTag)) return;
            // The trigger collider may be on a child, but the Player's
            // tag is on the prefab root — walk up to find the tagged GO.
            var player = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;
            if (!player.CompareTag(_playerTag))
            {
                // Climb the hierarchy to find the tagged ancestor.
                var t = other.transform;
                while (t != null && !t.CompareTag(_playerTag)) t = t.parent;
                if (t == null) return;
                player = t.gameObject;
            }
            _playerInRange = player;
            ReevaluateRegistration();
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(_playerTag) || !other.CompareTag(_playerTag))
            {
                // Same hierarchy walk as Enter — exit might be a child
                // collider whose tagged ancestor still matches.
                var t = other.transform;
                while (t != null && !t.CompareTag(_playerTag)) t = t.parent;
                if (t == null || t.gameObject != _playerInRange) return;
            }
            if (_registered) Deregister();
            _playerInRange = null;
        }

        protected virtual void Update()
        {
            // Edge-detect eligibility flips while the player is in range.
            // Cheap — typically one Vector3.Dot per frame in the subclass.
            if (_playerInRange == null) return;
            ReevaluateRegistration();
        }

        protected virtual void OnDisable()
        {
            // Defensive: if disabled (e.g., EnemyDeath disables a sibling
            // collider, or the GO becomes inactive), drop registration so
            // the prompt doesn't leak.
            if (_registered) Deregister();
            _playerInRange = null;
        }

        // ── Registration ───────────────────────────────────────────────────

        /// <summary>
        /// Re-evaluates eligibility for the cached player ref and edge-
        /// detects register / deregister flips.
        /// </summary>
        protected void ReevaluateRegistration()
        {
            if (_playerInRange == null) return;
            bool eligible = IsEligible(_playerInRange);
            if (eligible && !_registered)      Register();
            else if (!eligible && _registered) Deregister();
        }

        protected void Register()
        {
            var interactor = LevelGen.Player.PlayerInteractor.Instance;
            if (interactor == null) return; // no player in scene yet
            interactor.Register(this);
            _registered = true;
        }

        protected void Deregister()
        {
            var interactor = LevelGen.Player.PlayerInteractor.Instance;
            if (interactor != null) interactor.Deregister(this);
            _registered = false;
        }

        // ── Prompt UI (called by PlayerInteractor) ─────────────────────────

        /// <summary>
        /// Toggles the world-space prompt canvas visibility. Called by
        /// <see cref="LevelGen.Player.PlayerInteractor"/> when the active
        /// interactable changes.
        /// </summary>
        public void SetPromptVisible(bool show)
        {
            if (_promptRoot == null) EnsurePromptUI();
            if (_promptRoot == null) return; // build failed; already logged
            _promptRoot.SetActive(show);
            if (show && _promptLabelText != null)
                _promptLabelText.text = $"Press [E] {_promptLabel}";
        }

        /// <summary>
        /// Idempotently builds a child world-space Canvas + TMP_Text under
        /// <see cref="PromptAnchor"/> if one isn't present. Safe to call
        /// repeatedly — finds an existing child named <c>_InteractPrompt</c>
        /// and reuses it.
        /// </summary>
        public virtual void EnsurePromptUI()
        {
            var anchor = PromptAnchor;
            if (anchor == null) anchor = transform;

            // Look for an existing prompt child first (idempotent).
            var existing = anchor.Find("_InteractPrompt");
            if (existing != null)
            {
                _promptRoot      = existing.gameObject;
                _promptCanvas    = existing.GetComponentInChildren<Canvas>(includeInactive: true);
                _promptLabelText = existing.GetComponentInChildren<TMP_Text>(includeInactive: true);
                return;
            }

            // Build root + canvas + label.
            _promptRoot = new GameObject("_InteractPrompt");
            _promptRoot.transform.SetParent(anchor, worldPositionStays: false);
            _promptRoot.transform.localPosition = Vector3.zero;
            _promptRoot.transform.localRotation = Quaternion.identity;
            _promptRoot.transform.localScale    = Vector3.one;

            var canvasGO = new GameObject("Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            canvasGO.transform.SetParent(_promptRoot.transform, worldPositionStays: false);
            canvasGO.transform.localPosition = Vector3.zero;
            canvasGO.transform.localRotation = Quaternion.identity;
            // Tiny scale because World Space Canvas otherwise renders at
            // 1 unit per pixel — gigantic in world units.
            canvasGO.transform.localScale    = new Vector3(0.01f, 0.01f, 0.01f);

            _promptCanvas = canvasGO.GetComponent<Canvas>();
            _promptCanvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)canvasGO.transform;
            rt.sizeDelta = new Vector2(400f, 100f);

            var labelGO = new GameObject("PromptLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            labelGO.transform.SetParent(canvasGO.transform, worldPositionStays: false);
            var labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            _promptLabelText = labelGO.AddComponent<TextMeshProUGUI>();
            _promptLabelText.alignment = TextAlignmentOptions.Center;
            _promptLabelText.fontSize  = 48;
            _promptLabelText.color     = Color.white;
            _promptLabelText.fontStyle = FontStyles.Bold;
            _promptLabelText.text      = $"Press [E] {_promptLabel}";

            // TODO M6-followup: per-frame billboard toward Camera.main.
            // The label faces +Z by default; from the existing camera angle
            // it's still readable, so deferred until camera-relative
            // billboarding is needed by a future Interactable subclass.

            _promptRoot.SetActive(false);
        }
    }
}
