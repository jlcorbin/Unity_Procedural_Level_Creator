// EnemyHealthBarProximityDriver.cs — distance-based visibility driver for
// EnemyHealthBar.
//
// Uses InvokeRepeating (not a coroutine) so the per-frame overhead is zero
// between polls. Default 0.2s interval gives responsive show/hide without
// a per-frame distance check on every enemy in the scene.
//
// Player lookup is a one-shot in Start. If the player isn't found, the
// component self-disables and logs a warning — this is a genuine bug
// (enemies should never be active before the player exists) and should
// surface clearly rather than silently retry.

using UnityEngine;

namespace LevelGen
{
    /// <summary>
    /// Polls distance to the player and shows or hides the enemy health bar
    /// based on a configurable radius. Uses InvokeRepeating for performance.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBarProximityDriver : MonoBehaviour
    {
        [Header("Wiring (assigned in Inspector)")]
        [SerializeField]
        [Tooltip("The EnemyHealthBar component to drive. Typically lives on a child " +
                 "GameObject of the same enemy root.")]
        private EnemyHealthBar _healthBar;

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Distance from this transform at which the health bar becomes visible. " +
                 "Bar hides when the player moves beyond this radius.")]
        private float _showRadius = 12f;

        [SerializeField]
        [Tooltip("Seconds between proximity checks. Lower values are more responsive " +
                 "but slightly more expensive. 0.2 is a good balance for most scenes.")]
        private float _pollInterval = 0.2f;

        [SerializeField]
        [Tooltip("Tag used to locate the player. Must match the tag set on the Player " +
                 "root GameObject (default 'Player').")]
        private string _playerTag = "Player";

        // Cached player Transform from Start-time lookup.
        private Transform _player;

        void Start()
        {
            // One-shot player lookup. Enemies are spawned after the player so
            // a missing player at this point is a real config error, not a
            // timing issue. Log clearly and self-disable instead of retrying
            // silently.
            var playerGO = GameObject.FindWithTag(_playerTag);
            if (playerGO == null)
            {
                Debug.LogWarning($"[EnemyHealthBarProximityDriver] Player not found by tag " +
                                 $"'{_playerTag}'. Disabling component on '{name}'.", this);
                enabled = false;
                return;
            }

            if (_healthBar == null)
            {
                Debug.LogWarning($"[EnemyHealthBarProximityDriver] _healthBar is not assigned " +
                                 $"on '{name}'. Disabling component.", this);
                enabled = false;
                return;
            }

            _player = playerGO.transform;

            // Start polling immediately (0f initial delay), then repeat at
            // _pollInterval seconds. CancelInvoke in OnDestroy cleans up.
            InvokeRepeating(nameof(CheckProximity), 0f, _pollInterval);
        }

        void OnDestroy()
        {
            // Stop all repeating invocations so Unity doesn't attempt to call
            // CheckProximity after the GameObject has been destroyed.
            CancelInvoke();
        }

        // Called by InvokeRepeating on a fixed time interval.
        private void CheckProximity()
        {
            // Guard against the player being destroyed mid-session (e.g. scene
            // reload in progress, or a future gameplay system destroys the player GO).
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _healthBar.SetVisible(dist <= _showRadius);
        }
    }
}
