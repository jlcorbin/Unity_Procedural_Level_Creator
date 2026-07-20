// EquipmentSlotView.cs — one equipment slot flanking the hero preview.
//
// Three visual states (design handoff §"Equipment slots"):
//   • Filled  — 2px rarity border + rarity soft bg + item icon
//   • Empty   — muted frame, no icon (slot wired but nothing equipped)
//   • Locked  — future slot: muted frame, icon at 30% opacity, 🔒 badge
// Only Melee / Off-hand / Ranged / Armor are wired today; Head/Chest/Feet/Charm
// are shown locked.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using LevelGen.Items;

namespace LevelGen.UI
{
    /// <summary>A single equipment slot with a label chip below it.</summary>
    public class EquipmentSlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image fill;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconText;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject lockBadge;

        [Tooltip("Which slot this represents. Ignored when the slot is locked/future.")]
        [SerializeField] private EquipSlot slot = EquipSlot.Melee;

        [Tooltip("Locked/future slot — not wired to gameplay yet (Head/Chest/Feet/Charm).")]
        [SerializeField] private bool locked;

        /// <summary>Equipment slot this view represents.</summary>
        public EquipSlot Slot => slot;

        /// <summary>True when this is a future/locked slot.</summary>
        public bool Locked => locked;

        /// <summary>The equipped item shown (null when empty/locked).</summary>
        public ItemData Item { get; private set; }

        /// <summary>Raised when an unlocked slot is clicked.</summary>
        public event System.Action<EquipmentSlotView> Clicked;

        /// <summary>Configures the static parts (called by the builder).</summary>
        public void Configure(EquipSlot equipSlot, string labelText, bool isLocked)
        {
            slot = equipSlot;
            locked = isLocked;
            if (label != null) label.text = labelText;
        }

        /// <summary>Shows the currently equipped item (or empty when null).</summary>
        public void Bind(ItemData item, RarityPalette palette)
        {
            Item = locked ? null : item;

            if (lockBadge != null) lockBadge.SetActive(locked);

            if (locked)
            {
                if (frame != null) frame.color = palette != null ? palette.lockedMain : Color.grey;
                if (fill != null) fill.color = palette != null ? palette.lockedSoft : Color.white;
                SetIcon(null, 0.30f);
                return;
            }

            if (Item == null)
            {
                if (frame != null) frame.color = palette != null ? palette.emptyMain : Color.grey;
                if (fill != null) fill.color = palette != null ? palette.emptySoft : Color.white;
                SetIcon(null, 1f);
                return;
            }

            var c = palette != null ? palette.Get(Item.Rarity) : new RarityColorSet();
            if (frame != null) frame.color = c.main;
            if (fill != null) fill.color = c.soft;
            SetIcon(Item, 1f);
        }

        private void SetIcon(ItemData item, float alpha)
        {
            bool hasSprite = item != null && item.Icon != null;
            if (icon != null)
            {
                icon.enabled = hasSprite;
                if (hasSprite)
                {
                    icon.sprite = item.Icon;
                    var col = icon.color; col.a = alpha; icon.color = col;
                }
            }
            if (iconText != null)
            {
                bool showText = item != null && !hasSprite;
                iconText.enabled = showText;
                if (showText)
                {
                    iconText.text = item.DisplayName != null && item.DisplayName.Length > 0
                        ? item.DisplayName.Substring(0, 1).ToUpper() : "?";
                    var col = iconText.color; col.a = alpha; iconText.color = col;
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!locked) Clicked?.Invoke(this);
        }
    }
}
