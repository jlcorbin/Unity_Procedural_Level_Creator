// EnemyHealthBar.cs — world-space health bar that billboards to the main
// camera and tracks an enemy's CharacterStatsRuntime.
//
// Intended setup: add this component to a child GameObject under the enemy
// root, positioned at head height. Wire _fillImage to a Filled (Horizontal,
// Left) UnityEngine.UI.Image inside a World Space Canvas child of this
// same GameObject. _barYOffset shifts the local Y from the parent's origin
// so the bar floats above the enemy mesh.
//
// Visibility is driven externally — EnemyHealthBarProximityDriver calls
// SetVisible(bool). This script never polls distance itself.

using UnityEngine;
using UnityEngine.UI;
using LevelGen.Combat;

namespace LevelGen
{
    /// <summary>
    /// World-space health bar billboard. Tracks <see cref="CharacterStatsRuntime"/>
    /// HP and billboards to the main camera each frame.
    /// Call <see cref="SetVisible"/> from any external system to force show/hide.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("Wiring (assigned in Inspector)")]
        [SerializeField]
        [Tooltip("The Filled Image used as the HP bar fill. Must be Image Type=Filled, " +
                 "Fill Method=Horizontal, Fill Origin=Left.")]
        private Image _fillImage;

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Local Y offset from this transform's parent. Positions the bar above the enemy mesh.")]
        private float _barYOffset = 2.2f;

        [SerializeField]
        [Tooltip("CharacterStatsRuntime to track. If left null, resolved via " +
                 "GetComponentInParent at Awake.")]
        private CharacterStatsRuntime _stats;

        // Cached camera reference — lazily populated in Update because
        // Camera.main may not exist at Awake (e.g. the CM rig is added at
        // runtime via CinemachineAutoBind). Cleared and retried each frame
        // when null so a late-spawning camera is picked up automatically.
        private Camera _cam;

        void Awake()
        {
            // Resolve stats if not wired in the Inspector.
            if (_stats == null)
                _stats = GetComponentInParent<CharacterStatsRuntime>();

            if (_stats == null)
            {
                Debug.LogWarning($"[EnemyHealthBar] '{name}' could not find a " +
                                 "CharacterStatsRuntime in self or parents. " +
                                 "Disabling component.", this);
                enabled = false;
                return;
            }

            // Position the bar above the enemy's origin.
            transform.localPosition = new Vector3(0f, _barYOffset, 0f);
        }

        void OnEnable()
        {
            if (_stats != null)
                _stats.OnDied += OnStatsDied;
        }

        void OnDisable()
        {
            // Unsubscribe to prevent leaking into a destroyed runtime.
            // CLAUDE.md static-event lifetime rule: always unsubscribe in
            // OnDisable / OnDestroy for events that outlive this component.
            if (_stats != null)
                _stats.OnDied -= OnStatsDied;
        }

        void Update()
        {
            // Lazy camera cache: try Camera.main each frame it's null so
            // a late-spawning Cinemachine brain is picked up automatically.
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            // Billboard: align forward to face away from the camera so the
            // bar faces the viewer. Using -cam.transform.forward avoids the
            // LookAt mirror-flip that can occur on some orientations, and
            // is cheaper than a full Quaternion.LookRotation call.
            transform.forward = -_cam.transform.forward;

            // Update fill amount.
            if (_fillImage != null && _stats.MaxHP > 0)
            {
                _fillImage.fillAmount =
                    Mathf.Clamp01((float)_stats.CurrentHP / _stats.MaxHP);
            }
        }

        /// <summary>
        /// Shows or hides this health bar GameObject. External systems
        /// (proximity driver, target lock) call this — do not add logic here.
        /// </summary>
        /// <param name="value">True to show, false to hide.</param>
        public void SetVisible(bool value)
        {
            gameObject.SetActive(value);
        }

        // Called when the tracked character dies. Hides the bar so a dead
        // enemy's corpse (which stays in the scene until EnemyDeath despawns
        // it after _despawnDelay seconds) does not show a 0 / MaxHP bar.
        private void OnStatsDied(CharacterStatsRuntime stats)
        {
            SetVisible(false);
        }
    }
}
