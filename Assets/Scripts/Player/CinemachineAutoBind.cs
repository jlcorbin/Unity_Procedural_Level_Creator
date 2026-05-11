// CinemachineAutoBind.cs — runtime auto-bind of a CinemachineCamera's
// Follow + LookAt targets to the Player_Hero CameraTarget.
//
// Replaces the manual "drag CameraTarget into vcam.Follow" scene-author
// step. Sits on the CinemachineCamera GameObject (or any object — falls
// back to scene-wide search if the local lookup misses).
//
// Solves three scenarios that the manual approach failed at:
//   1. Prefab rebuild orphans the scene-side ref (CameraTarget got a
//      fresh fileID).
//   2. Player_Hero hasn't spawned yet when the camera Awakes — the
//      retry coroutine polls until the player appears, then binds.
//   3. New scene with a CM rig that hasn't been hand-bound yet.

using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace LevelGen.Player
{
    /// <summary>
    /// Sits on (or near) a <see cref="CinemachineCamera"/> and auto-
    /// binds its <c>Follow</c> + <c>LookAt</c> properties to the
    /// CameraTarget child of the first GameObject tagged
    /// <see cref="_playerTag"/>. Retries every
    /// <see cref="_retryInterval"/> seconds until the player appears
    /// or <see cref="_giveUpAfter"/> elapses. Single-fire — once
    /// bound, the coroutine exits.
    /// </summary>
    [DefaultExecutionOrder(200)] // After PlayerHero's Awake (default 0)
    [DisallowMultipleComponent]
    public class CinemachineAutoBind : MonoBehaviour
    {
        [Tooltip("Tag to search for the player. Must match the tag on " +
                 "Player_Hero.prefab's root (default 'Player').")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("Name of the Transform child on the player that the " +
                 "camera should track. Default 'CameraTarget' — set by " +
                 "PlayerHeroBuilder at local (0, 1.6, 0).")]
        [SerializeField] private string _cameraTargetChildName = "CameraTarget";

        [Tooltip("Seconds between retry attempts while waiting for the " +
                 "player to spawn. 0.25 is a reasonable default — fast " +
                 "enough to catch a deferred-spawn within ~1 frame visual " +
                 "tolerance, slow enough to avoid burning Update budget.")]
        [SerializeField] private float _retryInterval = 0.25f;

        [Tooltip("Maximum wall-clock seconds to wait for the player " +
                 "before logging a warning and giving up. Auto-bind " +
                 "stays silent inside this window — keeps log spam low " +
                 "on legitimate deferred-spawn scenarios.")]
        [SerializeField] private float _giveUpAfter = 10f;

        private CinemachineCamera _vcam;
        private bool _isBound;

        /// <summary>True once Follow + LookAt have been assigned successfully.</summary>
        public bool IsBound => _isBound;

        private void Awake()
        {
            _vcam = GetComponent<CinemachineCamera>();
            if (_vcam == null)
            {
                // Fall back to scene-wide search — supports placing this
                // component on a sibling/parent GameObject if desired.
                _vcam = FindAnyObjectByType<CinemachineCamera>();
            }
        }

        private void Start()
        {
            if (_vcam == null)
            {
                Debug.LogError($"[CinemachineAutoBind] No CinemachineCamera in scene. Auto-bind disabled.", this);
                return;
            }
            StartCoroutine(BindRoutine());
        }

        private IEnumerator BindRoutine()
        {
            float elapsed = 0f;
            while (!_isBound && elapsed < _giveUpAfter)
            {
                if (TryBind()) yield break;
                yield return new WaitForSeconds(_retryInterval);
                elapsed += _retryInterval;
            }
            if (!_isBound)
            {
                Debug.LogWarning(
                    $"[CinemachineAutoBind] Gave up after {_giveUpAfter}s — no GameObject tagged " +
                    $"'{_playerTag}' with a '{_cameraTargetChildName}' child was found in the scene.", this);
            }
        }

        private bool TryBind()
        {
            var player = GameObject.FindGameObjectWithTag(_playerTag);
            if (player == null) return false;
            var target = player.transform.Find(_cameraTargetChildName);
            if (target == null) return false;

            _vcam.Follow = target;
            _vcam.LookAt = target;
            _isBound = true;
            Debug.Log($"[CinemachineAutoBind] Bound '{_vcam.name}' to '{player.name}/{target.name}'.", this);
            return true;
        }
    }
}
