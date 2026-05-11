// PlayerDodge.cs — M12 directional 4-way dodge.
//
// V press → roll in the current movement direction (or forward if no
// input), 25 stamina cost, 0.5s i-frames, 0.8s cooldown. Cancels any
// in-flight attack via PlayerCombat.CancelAttack(); scripted impulse
// on CharacterController drives displacement (NOT root motion).
//
// Single-direction dependency: subscribes to PlayerInputReader.DodgePressed,
// writes via PlayerAnimator (sole writer-per-parameter) for DodgeTrigger +
// DodgeDirection, mutates CharacterStatsRuntime via SetInvulnerable +
// SpendStamina. PlayerCombat is consulted via the new public
// CancelAttack(). Never reaches into the Animator directly.
//
// Roll-motion vs PlayerController interplay: PlayerController.Update
// suppresses its own horizontal motion when IsDodging is true (step 4.6
// in PlayerController) so only gravity-Y is applied by the controller.
// PlayerDodge then calls cc.Move(horizontal * dt) inside its coroutine,
// producing two cc.Move calls per frame — well-defined behavior in
// Unity (each Move updates position before the next reads it).

using System.Collections;
using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.Player
{
    /// <summary>
    /// Owns the M12 dodge sequence: re-entry gating, stamina cost,
    /// i-frame window, attack cancel, animator parameter writes,
    /// and scripted horizontal displacement through the
    /// <see cref="CharacterController"/>. The Animator graph's
    /// AnyState→Roll{FWD,BWD,LFT,RGT} transitions read the
    /// <c>DodgeDirection</c> int + <c>DodgeTrigger</c> trigger we
    /// write through <see cref="PlayerAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterStatsRuntime))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(PlayerAnimator))]
    [DisallowMultipleComponent]
    public class PlayerDodge : MonoBehaviour
    {
        // ── Tunables ────────────────────────────────────────────────────────

        [Header("Roll Motion")]
        [Tooltip("Horizontal speed during the roll in m/s. Default 8 — " +
                 "tuned for ~2.8m total displacement at 0.35s duration.")]
        [SerializeField] private float _rollSpeed     = 8f;

        [Tooltip("Duration of the displacement window in seconds. The " +
                 "Animator clip plays at its native length and exits to " +
                 "Locomotion via Exit Time = 1.0 — this value gates only " +
                 "the cc.Move loop, not the visual clip.")]
        [SerializeField] private float _rollDuration  = 0.35f;

        [Header("I-Frames")]
        [Tooltip("Seconds for which CharacterStatsRuntime.IsInvulnerable " +
                 "is held true. Independent of roll duration so future " +
                 "weight classes / armor mods can tune one without the " +
                 "other.")]
        [SerializeField] private float _iFrameDuration = 0.5f;

        [Header("Cost / Cooldown")]
        [Tooltip("Cooldown in seconds between successful dodge activations. " +
                 "Gates re-entry on _lastDodgeTime; presses during cooldown " +
                 "are dropped silently.")]
        [SerializeField] private float _cooldown      = 0.8f;

        [Tooltip("Stamina deducted per successful dodge activation. " +
                 "Dropped if CurrentStamina < this at press time.")]
        [SerializeField] private float _staminaCost   = 25f;

        // ── Cached refs / state ─────────────────────────────────────────────

        private CharacterController   _cc;
        private CharacterStatsRuntime _stats;
        private PlayerInputReader     _input;
        private PlayerCombat          _combat;        // optional — null tolerated
        private PlayerAnimator        _playerAnim;    // optional — null tolerated
        private PlayerDeath           _death;         // optional — null tolerated
        private Transform             _cameraTransform;

        private bool  _isDodging;
        private float _lastDodgeTime = -999f; // ensures first press passes the cooldown gate

        // Direction enum maps to the animator's DodgeDirection int:
        //   0 = FWD, 1 = BWD, 2 = LFT, 3 = RGT
        private const int DirFWD = 0;
        private const int DirBWD = 1;
        private const int DirLFT = 2;
        private const int DirRGT = 3;

        /// <summary>
        /// True while a roll coroutine is running. Read by
        /// <see cref="PlayerController"/> to suppress its own
        /// horizontal motion during the roll (gravity is still
        /// applied by the controller; horizontal comes from us).
        /// Goes false the frame the coroutine exits.
        /// </summary>
        public bool IsDodging => _isDodging;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _cc        = GetComponent<CharacterController>();
            _stats     = GetComponent<CharacterStatsRuntime>();
            _input     = GetComponent<PlayerInputReader>();
            // Optional refs — null-tolerant.
            _combat     = GetComponent<PlayerCombat>();
            _playerAnim = GetComponent<PlayerAnimator>();
            _death      = GetComponent<PlayerDeath>();

            var main = Camera.main;
            _cameraTransform = main != null ? main.transform : null;
        }

        private void OnEnable()
        {
            if (_input != null) _input.DodgePressed += OnDodgePressed;
            if (_death != null) _death.OnPlayerDied += HandlePlayerDied;
        }

        private void OnDisable()
        {
            if (_input != null) _input.DodgePressed -= OnDodgePressed;
            if (_death != null) _death.OnPlayerDied -= HandlePlayerDied;
        }

        // ── Input handler ───────────────────────────────────────────────────

        private void OnDodgePressed()
        {
            // Re-entry guards. All three are independent — any single
            // failure drops the press silently.
            if (_isDodging) return;                                  // 1. already rolling
            if (_stats == null || _stats.CurrentStamina < _staminaCost) return; // 2. stamina
            if (Time.time - _lastDodgeTime < _cooldown) return;       // 3. cooldown

            // Spend FIRST so a same-frame second press would fail the
            // stamina gate even before _isDodging flips true.
            _stats.SpendStamina(_staminaCost);
            _lastDodgeTime = Time.time;

            // Cancel any in-flight attack BEFORE we set Animator params.
            // PlayerCombat clears its buffered combo + per-swing hitlist +
            // hitbox; the visual interruption comes from AnyState→Roll{X}
            // overriding the Attack state.
            if (_combat != null) _combat.CancelAttack();

            // Compute roll direction (world-space) and which clip to play
            // (body-local int 0/1/2/3).
            Vector3 worldDir = GetRollWorldDirection(out int directionInt);

            if (_playerAnim != null)
            {
                _playerAnim.SetDodgeDirection(directionInt);
                _playerAnim.SetDodgeTrigger();
            }

            // Launch both coroutines concurrently. Roll handles
            // displacement; i-frame handles invuln window. Independent
            // durations — i-frames typically outlast the roll motion.
            StartCoroutine(RollCoroutine(worldDir));
            StartCoroutine(IFrameCoroutine());
        }

        // ── Coroutines ──────────────────────────────────────────────────────

        private IEnumerator RollCoroutine(Vector3 worldDir)
        {
            _isDodging = true;
            float elapsed = 0f;
            while (elapsed < _rollDuration)
            {
                // Move only on horizontal axes; PlayerController's
                // Update applies gravity-Y in the same frame.
                _cc.Move(worldDir * _rollSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _isDodging = false;
        }

        private IEnumerator IFrameCoroutine()
        {
            if (_stats == null) yield break;
            _stats.SetInvulnerable(true);
            yield return new WaitForSeconds(_iFrameDuration);
            // Defensive — Player may have died during the dodge (e.g.
            // a future damage source bypasses the invuln gate).
            // Either way, clear so subsequent damage applies normally.
            _stats.SetInvulnerable(false);
        }

        // ── Direction selection ─────────────────────────────────────────────

        /// <summary>
        /// Samples <see cref="PlayerInputReader.MoveInput"/> at the
        /// moment V is pressed, transforms it from camera-relative
        /// XZ to a world-space direction, and buckets it into one of
        /// four cardinal directions (forward ±45°, backward ±135°,
        /// left/right). If the input vector is below a small dead-
        /// zone, defaults to character forward.
        /// </summary>
        private Vector3 GetRollWorldDirection(out int directionInt)
        {
            Vector2 input = _input != null ? _input.MoveInput : Vector2.zero;

            // Dead-zone default: no input → forward roll relative to body.
            if (input.sqrMagnitude < 0.01f)
            {
                directionInt = DirFWD;
                return transform.forward;
            }

            // Bucket into one of four cardinals based on which axis
            // dominates. Equivalent to "forward ±45°" angle-bucket but
            // uses absolute-value comparisons to avoid an atan2 call.
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                directionInt = input.y >= 0f ? DirFWD : DirBWD;
            else
                directionInt = input.x >= 0f ? DirRGT : DirLFT;

            // World-space direction:
            //   - Body yaw is locked to camera yaw every frame
            //     (SnapBodyToCameraYaw in PlayerController), so
            //     transform.forward / right are camera-relative.
            //   - Use them directly to avoid pulling camera-relative
            //     math into this script.
            Vector3 fwd   = transform.forward;
            Vector3 right = transform.right;
            // Defensive: if Camera.main exists and SnapBodyToCameraYaw
            // hasn't yet run on frame 0, prefer the camera's forward.
            // No-op in normal flow.
            if (_cameraTransform != null)
            {
                Vector3 cf = _cameraTransform.forward; cf.y = 0f; cf.Normalize();
                if (cf.sqrMagnitude > 0.001f)
                {
                    fwd   = cf;
                    right = new Vector3(cf.z, 0f, -cf.x); // 90° CW rotation = right
                }
            }

            switch (directionInt)
            {
                case DirFWD: return fwd;
                case DirBWD: return -fwd;
                case DirRGT: return right;
                case DirLFT: return -right;
            }
            return fwd;
        }

        // ── Death cleanup ───────────────────────────────────────────────────

        private void HandlePlayerDied(PlayerDeath _)
        {
            // Stop any in-flight coroutines so we don't keep moving
            // the corpse and don't hold IsInvulnerable=true past death.
            StopAllCoroutines();
            _isDodging = false;
            if (_stats != null) _stats.SetInvulnerable(false);
            enabled = false;
        }
    }
}
