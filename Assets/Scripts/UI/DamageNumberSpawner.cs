// DamageNumberSpawner.cs — singleton manager for floating combat text (M8).
//
// Subscribes once to Targetable.AnyTargetableHit (static event) and
// spawns a DamageNumber per hit. New Targetables work automatically —
// they fire the static event from RaiseHit just like everyone else.
//
// Singleton-scoped per-scene. Third cross-cutting singleton in the
// project after MouseLook (_MouseLock) and PlayerInteractor.

using UnityEngine;
using LevelGen.Combat;

namespace LevelGen.UI
{
    /// <summary>
    /// Subscribes to <see cref="Targetable.AnyTargetableHit"/> and
    /// instantiates a <see cref="DamageNumber"/> at each hit point.
    /// One instance per scene; duplicates self-destroy in Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageNumberSpawner : MonoBehaviour
    {
        public static DamageNumberSpawner Instance { get; private set; }

        [Tooltip("DamageNumber prefab to instantiate per hit. Wired by " +
                 "DamageNumberBuilder; must be non-null at runtime or hits " +
                 "are silently dropped.")]
        [SerializeField] private DamageNumber _damageNumberPrefab;

        [Tooltip("Optional parent for spawned numbers (keeps Hierarchy " +
                 "clean). Defaults to this transform if unset.")]
        [SerializeField] private Transform _spawnParent;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DamageNumberSpawner] Duplicate instance detected; " +
                                 "destroying self. Only one DamageNumberSpawner should " +
                                 "exist per scene.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (_spawnParent == null) _spawnParent = transform;
        }

        private void OnEnable()
        {
            // Subscribe in OnEnable / unsubscribe in OnDisable so a temporarily
            // disabled spawner doesn't keep eating events. Matches
            // EnemyHitReaction's subscription pattern.
            Targetable.AnyTargetableHit += HandleAnyHit;
        }

        private void OnDisable()
        {
            // Static event lifetime survives domain reloads — must
            // unsubscribe explicitly or the next domain reload's spawner
            // will fire alongside this destroyed one.
            Targetable.AnyTargetableHit -= HandleAnyHit;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleAnyHit(Vector3 hitPoint, float damage)
        {
            if (_damageNumberPrefab == null) return; // misconfigured; no warn-spam
            var dn = Instantiate(_damageNumberPrefab, _spawnParent);
            dn.Initialize(hitPoint, damage);
        }
    }
}
