// AssassinateInteractable.cs — first concrete Interactable subclass.
//
// Eligibility: target alive AND player is within the back-arc
// behind the target (dot-product check against target's forward).
//
// Execute: snap-rotate the player to face the target, set a one-
// shot damage override on PlayerCombat, then fire RequestAttack —
// runs through the existing combo + hitbox path. The override is
// consumed by PlayerCombat.NotifyHitboxTriggered on the first
// hitbox-target intersection.

using UnityEngine;
using LevelGen.Combat;
using LevelGen.Player;

namespace LevelGen.Interaction
{
    /// <summary>
    /// Press-E-to-assassinate Interactable. Wires onto a child of any
    /// enemy with <see cref="CharacterStatsRuntime"/>. Eligibility is
    /// alive + behind-arc; Execute fires the player's normal attack
    /// with a one-shot damage override.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class AssassinateInteractable : Interactable
    {
        [Header("Assassinate")]
        [Tooltip("Target's stats — auto-resolves to GetComponentInParent on Reset.")]
        [SerializeField] private CharacterStatsRuntime _targetStats;

        [Tooltip("Target's transform for facing checks. Auto-resolves to " +
                 "_targetStats.transform on Reset.")]
        [SerializeField] private Transform _targetTransform;

        [Tooltip("Player must be in the back-arc relative to target's forward. " +
                 "Compare against -dot(target.forward, toPlayer): " +
                 "0.5 ≈ 60° back arc, 0.0 ≈ behind half-circle, 1.0 = directly behind only.")]
        [Range(-1f, 1f)]
        [SerializeField] private float _backArcDot = 0.5f;

        [Tooltip("Damage applied for the assassinate swing. Single-shot " +
                 "override on PlayerCombat — consumed once, then cleared.")]
        [SerializeField] private int _assassinateDamage = 99999;

        protected override void Reset()
        {
            base.Reset();

            // Defaults specific to assassinate.
            _priority    = InteractPriority.Assassinate;
            _promptLabel = "Assassinate";

            // Try to resolve target refs from a parent stats component.
            _targetStats = GetComponentInParent<CharacterStatsRuntime>();
            if (_targetStats != null)
            {
                _targetTransform = _targetStats.transform;
                // Anchor the prompt above the target's head by default.
                _promptAnchor = _targetStats.transform;
            }

            // Configure the trigger collider.
            var sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                if (sc.radius < 0.01f) sc.radius = 1.5f;
            }
        }

        public override bool IsEligible(GameObject player)
        {
            if (player == null) return false;
            if (_targetStats == null || _targetStats.IsDead) return false;
            if (_targetTransform == null) return false;

            // Vector from target to player, projected onto target.forward.
            // fwdDot=+1 means player is directly in front; -1 means directly
            // behind. Eligible when fwdDot < -_backArcDot (behind by enough).
            Vector3 toPlayer = player.transform.position - _targetTransform.position;
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude < 0.0001f) return false;
            toPlayer.Normalize();

            Vector3 fwd = _targetTransform.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.0001f) return false;
            fwd.Normalize();

            float fwdDot = Vector3.Dot(fwd, toPlayer);
            return fwdDot < -_backArcDot;
        }

        public override void Execute(GameObject player)
        {
            if (player == null) return;

            var combat = player.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogWarning("[AssassinateInteractable] Player has no PlayerCombat — " +
                                 "can't deliver assassinate. No-op.", this);
                return;
            }

            // No-op if mid-Attack/Hit. Drops the input deliberately —
            // assassinate must run from a stable state.
            if (combat.IsBusy) return;

            // Snap-rotate player to face the target before the swing so the
            // existing weapon hitbox path connects.
            if (_targetTransform != null)
            {
                Vector3 toTarget = _targetTransform.position - player.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    player.transform.rotation = Quaternion.LookRotation(toTarget.normalized);
            }

            combat.SetNextHitDamageOverride(_assassinateDamage);
            combat.RequestAttack();

            // Eligibility flips false next frame when target dies (OnDied
            // → IsDead==true → base Update detects → Deregister).
            // No explicit prompt-hide here.
        }
    }
}
