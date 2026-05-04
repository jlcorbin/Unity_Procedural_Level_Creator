// DamageNumber.cs — single floating damage value (M8).
//
// World-space TMP_Text that drifts upward and fades out over a
// short lifetime, then self-destroys. Spawned by
// DamageNumberSpawner; never instantiated directly.

using System.Collections;
using TMPro;
using UnityEngine;

namespace LevelGen.UI
{
    /// <summary>
    /// One floating damage number. <see cref="Initialize"/> sets the
    /// world position and damage value, then starts the rise + fade
    /// coroutine. The GameObject self-destroys at the end of its
    /// lifetime — no pooling.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class DamageNumber : MonoBehaviour
    {
        [Tooltip("Total seconds the number is visible before self-destroy.")]
        [SerializeField] private float _lifetime = 1.0f;

        [Tooltip("World-space distance the number drifts upward over its lifetime.")]
        [SerializeField] private float _riseDistance = 1.5f;

        [Tooltip("Color of the damage text. Future: drive from damage type / source. " +
                 "For now, white.")]
        [SerializeField] private Color _color = Color.white;

        private TMP_Text _tmp;
        private Vector3  _startPosition;

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            _tmp.color = _color;
        }

        /// <summary>
        /// Place the number at <paramref name="worldPosition"/>, set the
        /// text to the integer-rounded damage value, and start the rise +
        /// fade animation. The DamageNumberSpawner calls this immediately
        /// after Instantiate.
        /// </summary>
        public void Initialize(Vector3 worldPosition, float damage)
        {
            transform.position = worldPosition;
            _startPosition = worldPosition;
            _tmp.text = damage.ToString("0");
            StartCoroutine(AnimateLifetime());
        }

        private IEnumerator AnimateLifetime()
        {
            float elapsed = 0f;
            Color baseColor = _tmp.color;
            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _lifetime);
                // Smoothstep ease for both rise and fade — same curve
                // OpenInteractable uses for door swing.
                float eased = t * t * (3f - 2f * t);

                transform.position = _startPosition + Vector3.up * (_riseDistance * eased);
                _tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - eased);

                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
