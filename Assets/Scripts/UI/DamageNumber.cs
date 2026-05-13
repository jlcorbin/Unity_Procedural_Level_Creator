// DamageNumber.cs — single floating damage value (M8, polished M15b).
//
// World-space TMP_Text that drifts upward and fades out over a
// short lifetime, then self-destroys. Spawned by
// DamageNumberSpawner; never instantiated directly.
//
// M15b additions: camera billboard in Update, lateral spawn jitter,
// increased rise distance, and TMP alpha fade via _tmp.alpha.

using System.Collections;
using TMPro;
using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// One floating damage number. <see cref="Initialize"/> sets the
    /// world position and damage value, then starts the rise + fade
    /// coroutine. The GameObject self-destroys at the end of its
    /// lifetime — no pooling.
    /// Billboards to the main camera every frame so numbers remain
    /// readable as the camera orbits.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class DamageNumber : MonoBehaviour
    {
        [Tooltip("Total seconds the number is visible before self-destroy.")]
        [SerializeField] private float _lifetime = 1.0f;

        [Tooltip("World-space distance the number drifts upward over its lifetime. " +
                 "Increased from 1.5 to 2.0 (M15b) so numbers clear the health bar.")]
        [SerializeField] private float _riseDistance = 2.0f;

        [Tooltip("Color of the damage text. Future: drive from damage type / source. " +
                 "For now, white.")]
        [SerializeField] private Color _color = Color.white;

        [Tooltip("Max random lateral offset on spawn to prevent number stacking.")]
        [SerializeField] private float _lateralJitter = 0.3f;

        private TMP_Text _tmp;
        private Vector3  _startPosition;

        // Cached camera reference — lazy-populated each Update frame while
        // null, matching the EnemyHealthBar / LockIndicator pattern so a
        // late-spawning CinemachineAutoBind rig is picked up automatically.
        private Camera _cam;

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            _tmp.color = _color;
        }

        private void Update()
        {
            // Lazy camera cache — retry each frame while null so a
            // Cinemachine-driven Camera.main that spawns after this
            // component is still picked up.
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            // Billboard: align rotation to face the viewer. Uses
            // Quaternion.LookRotation(-cam.forward, cam.up) to match the
            // LockIndicator pattern (avoids LookAt mirror-flip).
            transform.rotation = Quaternion.LookRotation(
                _cam.transform.forward, _cam.transform.up);
        }

        /// <summary>
        /// Place the number near <paramref name="worldPosition"/> (with a
        /// small random lateral jitter applied to prevent stacking when
        /// multiple hits land on the same point), set the text to the
        /// integer-rounded damage value, and start the rise + fade animation.
        /// The DamageNumberSpawner calls this immediately after Instantiate.
        /// Note: <c>transform.position</c> will differ slightly from
        /// <paramref name="worldPosition"/> due to the jitter offset.
        /// </summary>
        /// <param name="worldPosition">Approximate world-space spawn point.</param>
        /// <param name="damage">Damage value to display (rounded to nearest integer).</param>
        public void Initialize(Vector3 worldPosition, float damage)
        {
            transform.position = worldPosition;

            // Random lateral offset prevents numbers from stacking on the
            // same XZ point when multiple hits land in quick succession.
            Vector3 jitter = new Vector3(
                UnityEngine.Random.Range(-_lateralJitter, _lateralJitter),
                0f,
                UnityEngine.Random.Range(-_lateralJitter, _lateralJitter));
            transform.position += jitter;

            _startPosition = transform.position;
            _tmp.text = damage.ToString("0");
            StartCoroutine(AnimateLifetime());
        }

        private IEnumerator AnimateLifetime()
        {
            float elapsed = 0f;
            // Ensure the number starts fully opaque for this play (the
            // prefab may have been pooled in future; explicit reset here).
            _tmp.alpha = 1f;

            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _lifetime);
                // Smoothstep ease — same curve OpenInteractable uses for
                // its door swing, applied to both the rise and the fade.
                float smoothT = t * t * (3f - 2f * t);

                transform.position = _startPosition + Vector3.up * (_riseDistance * smoothT);

                // Fade alpha 1 → 0 using the same smoothstep curve so the
                // number is opaque when spawned and invisible at destroy.
                _tmp.alpha = 1f - smoothT;

                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
