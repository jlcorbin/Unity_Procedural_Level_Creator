// PlayerInteractor.cs — receives Interactable register/deregister
// calls, picks the highest-priority active Interactable, and
// dispatches Execute when InteractPressed fires.
//
// Singleton-ish access: Interactables can't trust a player ref at
// trigger-enter time (the trigger collider may be a child whose
// attachedRigidbody is the prefab root, etc.), so the cleanest
// pattern is for them to call PlayerInteractor.Instance.Register.
// The project has exactly one Player.
//
// Mirrors the player-side singleton pattern of MouseLook's
// _MouseLock GameObject (one cross-cutting per-scene receiver).

using System.Collections.Generic;
using UnityEngine;
using LevelGen.Interaction;

namespace LevelGen.Player
{
    /// <summary>
    /// Receives <see cref="Interactable"/> registrations, tracks the
    /// active (highest-priority) interactable, toggles its prompt UI,
    /// and dispatches Execute on <see cref="PlayerInputReader.InteractPressed"/>.
    /// Disables itself on player death (PlayerDeath.OnPlayerDied) so
    /// the prompt clears before the death overlay covers it.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        /// <summary>
        /// Singleton accessor. Set in Awake; cleared in OnDestroy.
        /// Project has one Player; multiplayer would need rethinking.
        /// </summary>
        public static PlayerInteractor Instance { get; private set; }

        private PlayerInputReader        _input;
        private PlayerDeath              _death;
        private readonly HashSet<Interactable> _registered = new HashSet<Interactable>();
        private Interactable             _active;

        public Interactable Active => _active;
        public int RegisteredCount => _registered.Count;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[PlayerInteractor] Duplicate instance detected on '{name}'. " +
                                 $"Existing instance lives on '{Instance.name}'. " +
                                 "The newer one will overwrite — project assumes single-Player.", this);
            }
            Instance = this;
            _input   = GetComponent<PlayerInputReader>();
            _death   = GetComponent<PlayerDeath>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (_input != null) _input.InteractPressed += OnInteractPressed;
            if (_death != null) _death.OnPlayerDied    += HandlePlayerDied;
        }

        private void OnDisable()
        {
            if (_input != null) _input.InteractPressed -= OnInteractPressed;
            if (_death != null) _death.OnPlayerDied    -= HandlePlayerDied;

            // Clear active prompt so it doesn't leak across enable/disable.
            if (_active != null) _active.SetPromptVisible(false);
            _active = null;
            _registered.Clear();
        }

        // ── Registration API (called by Interactable) ──────────────────────

        public void Register(Interactable i)
        {
            if (i == null) return;
            if (_registered.Add(i)) RecomputeActive();
        }

        public void Deregister(Interactable i)
        {
            if (i == null) return;
            if (_registered.Remove(i)) RecomputeActive();
        }

        // ── Active selection + prompt toggle ───────────────────────────────

        private void RecomputeActive()
        {
            Interactable best = null;
            int bestPri = int.MinValue;
            foreach (var i in _registered)
            {
                if (i == null) continue;
                int p = (int)i.Priority;
                if (p > bestPri)
                {
                    bestPri = p;
                    best    = i;
                }
            }

            if (best == _active) return;

            if (_active != null) _active.SetPromptVisible(false);
            _active = best;
            if (_active != null) _active.SetPromptVisible(true);
        }

        // ── Input handler ──────────────────────────────────────────────────

        private void OnInteractPressed()
        {
            if (_active == null) return;
            // Execute may cause a state change that flips IsEligible
            // (e.g. assassinate kills the target → next frame's
            // ReevaluateRegistration will deregister). We don't preemptively
            // deregister here — Update on the Interactable handles it.
            _active.Execute(gameObject);
        }

        // ── Player death handler ───────────────────────────────────────────

        private void HandlePlayerDied(PlayerDeath _)
        {
            // Belt-and-suspenders: PlayerDeath may not disable this component
            // (it disables PlayerController + PlayerCombat by default). Drop
            // all registrations and hide any active prompt regardless.
            if (_active != null) _active.SetPromptVisible(false);
            _registered.Clear();
            _active = null;
        }
    }
}
