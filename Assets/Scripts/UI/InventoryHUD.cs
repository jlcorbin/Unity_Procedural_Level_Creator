// InventoryHUD.cs — always-visible HUD strip for equipped items (M18).
//
// Displays the currently-equipped Melee and OffHand items as text labels.
// Subscribes to PlayerInventory.OnWeaponEquipped so it refreshes automatically
// whenever the equipped weapon changes. Reads both slots on each refresh so
// an Unequip event (payload null) still shows the correct empty state.
//
// Lives on the persistent HUD Canvas alongside PlayerHUD.
// Wiring: assign _meleeLabel and _offHandLabel in the Inspector.

using TMPro;
using UnityEngine;
using LevelGen.Items;
using LevelGen.Player;

namespace LevelGen
{
    /// <summary>
    /// Always-visible HUD strip showing Melee and OffHand equipped slots as
    /// text labels. Reads <see cref="PlayerInventory.Instance"/> and refreshes
    /// on <see cref="PlayerInventory.OnWeaponEquipped"/>.
    /// Lives on the persistent HUD Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryHUD : MonoBehaviour
    {
        [Tooltip("Label that displays the currently-equipped Melee item. " +
                 "Shows '[Melee] —' when no weapon is equipped.")]
        [SerializeField] private TextMeshProUGUI _meleeLabel;

        [Tooltip("Label that displays the currently-equipped OffHand item. " +
                 "Shows '[OffHand] —' when no item is equipped.")]
        [SerializeField] private TextMeshProUGUI _offHandLabel;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnWeaponEquipped += HandleWeaponEquipped;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (PlayerInventory.Instance != null)
                PlayerInventory.Instance.OnWeaponEquipped -= HandleWeaponEquipped;
        }

        private void Start()
        {
            // Belt-and-suspenders: re-subscribe in case Instance was null
            // during OnEnable (PlayerInventory spawned after this component).
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.OnWeaponEquipped -= HandleWeaponEquipped;
                PlayerInventory.Instance.OnWeaponEquipped += HandleWeaponEquipped;
            }
            RefreshAll();
        }

        // ── Private handlers ──────────────────────────────────────────────────

        /// <summary>Responds to any weapon-slot change by refreshing all labels.</summary>
        /// <param name="_">The newly equipped item (or null on Unequip). Ignored —
        /// both slots are read from Instance directly so a single handler covers all slots.</param>
        private void HandleWeaponEquipped(ItemData _) => RefreshAll();

        /// <summary>
        /// Reads both Melee and OffHand slots from <see cref="PlayerInventory.Instance"/>
        /// and updates the text labels. Safe to call when Instance is null.
        /// </summary>
        private void RefreshAll()
        {
            var inv = PlayerInventory.Instance;

            if (_meleeLabel != null)
                _meleeLabel.text = FormatSlot(EquipSlot.Melee,
                    inv != null ? inv.GetEquipped(EquipSlot.Melee) : null);

            if (_offHandLabel != null)
                _offHandLabel.text = FormatSlot(EquipSlot.OffHand,
                    inv != null ? inv.GetEquipped(EquipSlot.OffHand) : null);
        }

        // ── Static helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Formats a slot-label string.
        /// Returns <c>"[Melee] Sword Name"</c> when equipped,
        /// <c>"[Melee] —"</c> when empty.
        /// </summary>
        /// <param name="slot">The equipment slot.</param>
        /// <param name="item">The currently equipped item, or null if empty.</param>
        private static string FormatSlot(EquipSlot slot, ItemData item)
        {
            string name = item != null ? item.DisplayName : "—";
            return $"[{slot}] {name}";
        }
    }
}
