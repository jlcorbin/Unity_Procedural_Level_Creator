// InventoryPanel.cs — toggled inventory panel (M18).
//
// Architecture:
//   This script's GameObject stays ALWAYS ACTIVE so input subscriptions work
//   even while the visual panel is closed. Toggle shows / hides _panelRoot
//   (the visual child) rather than this GameObject itself.
//
//   Open  → _panelRoot.SetActive(true) + Time.timeScale = 0
//   Close → _panelRoot.SetActive(false) + Time.timeScale = 1
//
//   The bag list is repopulated fresh on every Open call from
//   PlayerInventory.Instance.Items. Each row instantiates _itemRowPrefab
//   and calls InventoryItemRow.Init. After any equip/unequip action the
//   list and equipped labels are both refreshed in place.
//
// Wiring (all in the Inspector):
//   _panelRoot           — child GameObject that is the visual panel
//   _bagContainer        — Transform parent for spawned item rows
//   _equippedMeleeLabel  — TMP label for Melee slot display
//   _equippedOffHandLabel— TMP label for OffHand slot display
//   _itemRowPrefab       — prefab carrying InventoryItemRow component
//   _playerInputReader   — PlayerInputReader on Player_Hero
//   _closeButton         — optional close button on the panel

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LevelGen.Items;
using LevelGen.Player;

namespace LevelGen
{
    /// <summary>
    /// Toggled inventory panel. The host GameObject stays always-active
    /// so input subscriptions survive panel-close. Pauses time while open
    /// via <see cref="Time.timeScale"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryPanel : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────────────

        [Tooltip("The visual panel GameObject to show/hide. This script's " +
                 "GameObject stays active so input subscriptions work.")]
        [SerializeField] private GameObject _panelRoot;

        [Tooltip("Parent Transform under which item rows are spawned. " +
                 "Should be a VerticalLayoutGroup or similar.")]
        [SerializeField] private Transform _bagContainer;

        [Tooltip("TMP label showing the currently-equipped Melee item inside the panel.")]
        [SerializeField] private TextMeshProUGUI _equippedMeleeLabel;

        [Tooltip("TMP label showing the currently-equipped OffHand item inside the panel.")]
        [SerializeField] private TextMeshProUGUI _equippedOffHandLabel;

        [Tooltip("Prefab instantiated once per inventory item. Must carry an InventoryItemRow component.")]
        [SerializeField] private GameObject _itemRowPrefab;

        [Tooltip("PlayerInputReader on Player_Hero. Wired in Inspector. " +
                 "Subscribes to OnToggleInventoryPerformed to open/close the panel.")]
        [SerializeField] private PlayerInputReader _playerInputReader;

        [Tooltip("Optional close button. If assigned, clicking it calls Close(). " +
                 "Wired programmatically on every Open call.")]
        [SerializeField] private Button _closeButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_playerInputReader != null)
                _playerInputReader.OnToggleInventoryPerformed += HandleToggle;
        }

        private void OnDisable()
        {
            if (_playerInputReader != null)
                _playerInputReader.OnToggleInventoryPerformed -= HandleToggle;
        }

        private void OnDestroy()
        {
            // Belt-and-suspenders: restore timescale if destroyed while open.
            Time.timeScale = 1f;
        }

        // ── Toggle logic ──────────────────────────────────────────────────────

        /// <summary>Called by the OnToggleInventoryPerformed event from PlayerInputReader.</summary>
        private void HandleToggle()
        {
            if (_panelRoot != null && _panelRoot.activeSelf)
                Close();
            else
                Open();
        }

        /// <summary>Opens the inventory panel and pauses the game.</summary>
        public void Open()
        {
            if (_panelRoot == null) return;

            _panelRoot.SetActive(true);
            Time.timeScale = 0f;

            RefreshEquippedLabels();
            RepopulateBagList();

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(Close);
            }
        }

        /// <summary>Closes the inventory panel and resumes the game.</summary>
        public void Close()
        {
            if (_panelRoot == null) return;

            _panelRoot.SetActive(false);
            Time.timeScale = 1f;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Destroys all existing bag rows and re-instantiates one row per
        /// item in <see cref="PlayerInventory.Instance"/>. Safe if Instance is null.
        /// </summary>
        private void RepopulateBagList()
        {
            if (_bagContainer == null || _itemRowPrefab == null) return;

            // Destroy existing children.
            for (int i = _bagContainer.childCount - 1; i >= 0; i--)
                Destroy(_bagContainer.GetChild(i).gameObject);

            var inv = PlayerInventory.Instance;
            if (inv == null) return;

            // Use the Items IReadOnlyList property (already available from M16).
            IReadOnlyList<ItemData> items = inv.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                GameObject rowGO = Instantiate(_itemRowPrefab, _bagContainer);
                var row = rowGO.GetComponent<InventoryItemRow>();
                if (row != null)
                    row.Init(item, OnEquipClicked);
            }
        }

        /// <summary>
        /// Called when the player clicks Equip or Unequip on a bag row.
        /// Equips the item if it is not the current slot occupant;
        /// unequips if it already is. Then refreshes labels and bag list.
        /// </summary>
        /// <param name="item">The item whose button was clicked.</param>
        private void OnEquipClicked(ItemData item)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || item == null) return;

            bool isCurrentlyEquipped = inv.IsSlotEquipped(item.Slot)
                && inv.GetEquipped(item.Slot) == item;

            if (isCurrentlyEquipped)
                inv.Unequip(item.Slot);
            else
                inv.Equip(item);

            // Refresh both the equipped-slot summary and the bag list
            // so button labels update to the new equip state.
            RefreshEquippedLabels();
            RepopulateBagList();
        }

        /// <summary>
        /// Updates the two equipped-slot labels inside the panel using the
        /// same "[Slot] Name" / "[Slot] —" format as InventoryHUD.
        /// </summary>
        private void RefreshEquippedLabels()
        {
            var inv = PlayerInventory.Instance;

            if (_equippedMeleeLabel != null)
                _equippedMeleeLabel.text = FormatSlot(EquipSlot.Melee,
                    inv != null ? inv.GetEquipped(EquipSlot.Melee) : null);

            if (_equippedOffHandLabel != null)
                _equippedOffHandLabel.text = FormatSlot(EquipSlot.OffHand,
                    inv != null ? inv.GetEquipped(EquipSlot.OffHand) : null);
        }

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
