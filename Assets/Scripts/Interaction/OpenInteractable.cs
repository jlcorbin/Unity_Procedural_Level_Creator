// OpenInteractable.cs — second concrete Interactable subclass (M7).
//
// Toggleable: first press opens (rotates the hinge by _openAngle
// over _animationDuration), second press closes (rotates back).
// Coroutine lerp on this MonoBehaviour. While the lerp runs,
// _isAnimating=true and IsEligible returns false — the prompt
// hides during the swing and reappears with the new label
// ("Open" ↔ "Close") on completion.
//
// Generic by design: any "openable thing" — doors, gates, chests,
// drawers, lids — should fit. M7 ships only the test door
// (procedurally built; see TestDoorBuilder); M16 will wire this
// onto real FDP COMP_Door_* prefabs.

using System.Collections;
using UnityEngine;

namespace LevelGen.Interaction
{
    /// <summary>
    /// Toggleable open/close interactable. Rotates a configurable
    /// hinge transform by <see cref="_openAngle"/> degrees around
    /// <see cref="_rotationAxis"/> over <see cref="_animationDuration"/>
    /// seconds. State is binary (<c>_isOpen</c>). Press during
    /// animation is silently dropped via IsEligible.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class OpenInteractable : Interactable
    {
        [Header("Open")]
        [Tooltip("Transform that rotates when the door opens. " +
                 "Defaults to this transform on Reset().")]
        [SerializeField] private Transform _hinge;

        [Tooltip("Local-space rotation axis. Default Y (up) — vertical " +
                 "hinge / standard door.")]
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;

        [Tooltip("Degrees to rotate when opening. Negative for counter-clockwise.")]
        [SerializeField] private float _openAngle = 90f;

        [Tooltip("Seconds to complete the open or close lerp.")]
        [SerializeField] private float _animationDuration = 0.4f;

        [Tooltip("Label shown when the door is closed (state: pressing E will open it).")]
        [SerializeField] private string _openLabel = "Open";

        [Tooltip("Label shown when the door is open (state: pressing E will close it).")]
        [SerializeField] private string _closeLabel = "Close";

        // Runtime state — not serialized.
        private bool       _isOpen;
        private bool       _isAnimating;
        private Quaternion _closedRotation;
        private Quaternion _openRotation;

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected override void Reset()
        {
            base.Reset();

            _priority    = InteractPriority.Open;
            _promptLabel = _openLabel;
            if (_hinge == null) _hinge = transform;

            var sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                if (sc.radius < 0.01f) sc.radius = 1.5f;
            }
        }

        protected override void Awake()
        {
            // Cache hinge BEFORE base.Awake builds the prompt UI — defensive
            // against a future base override that might consult _hinge.
            if (_hinge == null) _hinge = transform;

            // Cache the closed and open target rotations from the hinge's
            // initial localRotation. Re-deriving each open/close cycle would
            // accumulate floating-point drift across many toggles; locking
            // both targets up-front guarantees the door always returns to
            // exactly the same closed orientation.
            _closedRotation = _hinge.localRotation;
            _openRotation   = _closedRotation * Quaternion.AngleAxis(_openAngle, _rotationAxis);

            // Sync prompt label to current state (default: closed → "Open").
            _promptLabel = _isOpen ? _closeLabel : _openLabel;

            base.Awake();
            // base.Awake calls EnsurePromptUI which calls RefreshPromptLabel —
            // the label text on the canvas now matches _promptLabel.
        }

        // ── Eligibility / Execute ──────────────────────────────────────────

        public override bool IsEligible(GameObject player)
        {
            if (player == null) return false;
            // Hide the prompt while animating so the visible label can't
            // be a stale "Open" while the door is swinging open.
            if (_isAnimating) return false;
            return true;
        }

        public override void Execute(GameObject player)
        {
            // Defense-in-depth: PlayerInteractor only calls Execute on the
            // active interactable, which IsEligible should have already
            // marked false during animation. Belt-and-suspenders for a
            // same-frame edge case.
            if (_isAnimating) return;

            // OpenInteractable assumes its lifetime >= _animationDuration.
            // Destroying this GameObject mid-coroutine cancels the swing
            // and leaves the door at whatever rotation it was at. Acceptable
            // for the test door; gameplay doors should not be destroyed
            // while open.
            StartCoroutine(AnimateRotation(_isOpen));
        }

        // ── Coroutine ──────────────────────────────────────────────────────

        private IEnumerator AnimateRotation(bool currentlyOpen)
        {
            _isAnimating = true;

            Quaternion from = _hinge.localRotation;
            Quaternion to   = currentlyOpen ? _closedRotation : _openRotation;

            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                // Smoothstep ease-in-out. Gives the swing a tiny bit of
                // polish without the cost of an AnimationCurve field.
                float eased = t * t * (3f - 2f * t);
                _hinge.localRotation = Quaternion.Slerp(from, to, eased);
                yield return null;
            }
            _hinge.localRotation = to;

            _isOpen = !currentlyOpen;
            _promptLabel = _isOpen ? _closeLabel : _openLabel;
            RefreshPromptLabel();

            _isAnimating = false;
            // Update tick will edge-detect IsEligible flipping back to true
            // and re-register, showing the new label automatically.
        }
    }
}
