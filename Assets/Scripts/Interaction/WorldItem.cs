// WorldItem.cs — concrete Interactable subclass for world pickup items (M16).
//
// Inherits the full Interactable lifecycle for free: E-key prompt,
// SphereCollider trigger radius, register/deregister against
// PlayerInteractor. Execute adds the item to PlayerInventory and
// destroys this GameObject.
//
// Access modifier note (Interactable base):
//   _priority, _promptLabel, _promptAnchor, _playerTag are all
//   [SerializeField] protected — accessible from this subclass.
//   Reset() and Awake() are protected virtual — overridden with
//   base.Reset() / base.Awake() calls per the OpenInteractable pattern.

using UnityEngine;
using LevelGen.Items;
using LevelGen.Player;

namespace LevelGen.Interaction
{
    /// <summary>
    /// World-space pickup item. Place on any GameObject that should be
    /// collectable via E-press. Assign an <see cref="ItemData"/> asset to
    /// <c>_itemData</c> in the Inspector. On Execute, adds the item to
    /// <see cref="PlayerInventory"/> and destroys this GameObject.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class WorldItem : Interactable
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("World Item")]
        [Tooltip("The item granted to the player on pickup.")]
        [SerializeField] private ItemData _itemData;

        [Tooltip("Interaction trigger radius in world units.")]
        [SerializeField] private float _pickupRadius = 1.5f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Editor reset — configures sensible defaults for the pickup.
        /// Sets priority to <see cref="InteractPriority.Pickup"/>, prompt
        /// label to "Pick Up", and configures the SphereCollider.
        /// </summary>
        protected override void Reset()
        {
            base.Reset();

            _priority    = InteractPriority.Pickup;
            _promptLabel = "Pick Up";

            var sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                sc.radius    = _pickupRadius;
            }
        }

        /// <summary>
        /// Awake — enriches the prompt label with the item's display name
        /// (e.g. "Pick Up Light Sword") before the base builds the Canvas UI.
        /// </summary>
        protected override void Awake()
        {
            // Mutate _promptLabel before base.Awake() so EnsurePromptUI
            // picks up the display-name label on first build.
            // (_promptLabel is protected on Interactable — accessible here.)
            if (_itemData != null)
                _promptLabel = $"Pick Up {_itemData.DisplayName}";

            base.Awake();
        }

        // ── Interactable contract ──────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> while the item data reference is assigned.
        /// WorldItems without data are never eligible (safety guard against
        /// unconfigured prefab instances in the scene).
        /// </summary>
        public override bool IsEligible(GameObject player)
        {
            return _itemData != null;
        }

        /// <summary>
        /// Adds <see cref="_itemData"/> to <see cref="PlayerInventory"/> and
        /// destroys this GameObject. Logs a warning if PlayerInventory is
        /// absent, or a notice if the inventory is full.
        /// </summary>
        public override void Execute(GameObject player)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[WorldItem] PlayerInventory.Instance is null — " +
                                 "ensure PlayerInventory is on the player prefab.", this);
                return;
            }

            if (!inv.AddItem(_itemData))
            {
                Debug.Log("[WorldItem] Inventory full — could not add item.", this);
                return;
            }

            Destroy(gameObject);
        }
    }
}
