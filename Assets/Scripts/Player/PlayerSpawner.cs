// PlayerSpawner.cs
// Instantiates the player prefab into the active scene at a designated
// spawn point. M1 stub — does NOT wire camera follow (Q-3 says M1 ships
// with a hand-positioned static camera). Camera scripting is its own
// later prompt.

using UnityEngine;

namespace LevelGen.Player
{
    /// <summary>
    /// Drop into a test scene with <see cref="playerPrefab"/> assigned.
    /// On <c>Start</c> instantiates the prefab at <see cref="spawnPoint"/>
    /// (or this transform if none is set).
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Spawn")]
        [Tooltip("Player prefab to instantiate at Start. Assign Player_MaleHero.prefab.")]
        [SerializeField] private GameObject playerPrefab;

        [Tooltip("Spawn point. Defaults to this transform if null.")]
        [SerializeField] private Transform spawnPoint;

        private void Start()
        {
            if (playerPrefab == null)
            {
                Debug.LogError($"[PlayerSpawner] No playerPrefab assigned on '{name}'.", this);
                return;
            }

            Transform sp = spawnPoint != null ? spawnPoint : transform;
            GameObject spawned = Instantiate(playerPrefab, sp.position, sp.rotation);
            spawned.name = playerPrefab.name + " (Spawned)";
            Debug.Log($"[PlayerSpawner] Spawned '{spawned.name}' at {sp.position}. Camera follow not implemented in M1 — main camera stays where it is.", spawned);
        }
    }
}
