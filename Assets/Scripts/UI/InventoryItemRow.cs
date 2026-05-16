// InventoryItemRow.cs — single row in the inventory panel bag list (M18).
//
// Displays an item's name and slot, and provides an Equip / Unequip button
// whose label reflects the current equip state. Initialised by InventoryPanel
// via Init(item, onEquipClicked) each time the bag list is repopulated.
//
// Wiring (done by the developer in the Inspector on the row prefab):
//   _nameLabel  — TMP label showing the item's DisplayName
//   _slotLabel  — TMP label showing the item's Slot enum name
//   _equipButton — Button whose child TMP label shows "Equip" or "Unequip"

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LevelGen.Items;
using LevelGen.Player;

namespace LevelGen
{
    /// <summary>
    /// Represents a single item row in the inventory bag list.
    /// Call <see cref="Init"/> after instantiation to bind the row to an item.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryItemRow : MonoBehaviour
    {
        [Tooltip("Label that displays the item's display name.")]
        [SerializeField] private TextMeshProUGUI _nameLabel;

        [Tooltip("Label that displays the item's equipment slot (Melee, OffHand, etc.).")]
        [SerializeField] private TextMeshProUGUI _slotLabel;

        [Tooltip("Button to equip or unequip this item. " +
                 "Its child TMP_Text is updated to 'Equip' or 'Unequip' depending on state.")]
        [SerializeField] private Button _equipButton;

        // ── Internal state ────────────────────────────────────────────────────

        private ItemData _item;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Binds this row to an item and registers the equip callback.
        /// Must be called once after <c>Instantiate</c>.
        /// </summary>
        /// <param name="item">The item this row represents. Must not be null.</param>
        /// <param name="onEquipClicked">
        /// Callback invoked when the user clicks the Equip / Unequip button.
        /// Receives the bound <paramref name="item"/> as the argument.
        /// </param>
        public void Init(ItemData item, System.Action<ItemData> onEquipClicked)
        {
            _item = item;

            if (_nameLabel != null)
                _nameLabel.text = item.DisplayName;

            if (_slotLabel != null)
                _slotLabel.text = item.Slot.ToString();

            if (_equipButton != null)
            {
                _equipButton.onClick.RemoveAllListeners();
                _equipButton.onClick.AddListener(() => onEquipClicked(_item));

                // Set the button label text: "Unequip" if this item is the
                // currently equipped item for its slot, "Equip" otherwise.
                var buttonLabel = _equipButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonLabel != null)
                {
                    bool isEquipped = PlayerInventory.Instance != null
                        && PlayerInventory.Instance.IsSlotEquipped(item.Slot)
                        && PlayerInventory.Instance.GetEquipped(item.Slot) == item;

                    buttonLabel.text = isEquipped ? "Unequip" : "Equip";
                }
            }
        }
    }
}
