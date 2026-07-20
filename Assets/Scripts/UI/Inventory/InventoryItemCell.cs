// InventoryItemCell.cs — one cell in the Items Bag grid (Candy Cloud).
//
// Structure (built by InventoryUIBuilder):
//   Root (Image = rarity `main`, rounded)      ← 2px frame
//    └ Fill (Image = rarity `soft`, inset 2)   ← soft background
//    ├ Icon (Image)      — item sprite when ItemData.Icon is set
//    ├ IconText (TMP)    — placeholder initials when no sprite yet
//    ├ Count (Image+TMP) — "×N", materials only
//    └ SelectRing (Image)— dark selection frame, toggled
//
// Icons are placeholders until real chibi item art exists (design: emoji in the
// mocks are placeholders — swap for sprite atlases).

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using LevelGen.Items;

namespace LevelGen.UI
{
    /// <summary>
    /// A single bag cell. Call <see cref="Bind"/> to show an item, or
    /// <see cref="BindEmpty"/> for a filler slot.
    /// </summary>
    public class InventoryItemCell : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image frame;        // rarity main (border)
        [SerializeField] private Image fill;         // rarity soft (background)
        [SerializeField] private Image icon;         // item sprite
        [SerializeField] private TMP_Text iconText;  // fallback initials
        [SerializeField] private GameObject countRoot;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private GameObject selectRing;

        /// <summary>The item shown in this cell (null when empty).</summary>
        public ItemData Item { get; private set; }

        /// <summary>Raised when the cell is clicked; payload is this cell.</summary>
        public event System.Action<InventoryItemCell> Clicked;

        /// <summary>Shows an item with rarity framing and an optional stack count.</summary>
        /// <param name="item">Item to display.</param>
        /// <param name="count">Stack size; pass 0 or 1 to hide the badge (gear).</param>
        /// <param name="palette">Rarity colour source.</param>
        public void Bind(ItemData item, int count, RarityPalette palette)
        {
            Item = item;
            if (item == null) { BindEmpty(palette); return; }

            var c = palette != null ? palette.Get(item.Rarity) : new RarityColorSet();
            if (frame != null) frame.color = c.main;
            if (fill != null) fill.color = c.soft;

            bool hasSprite = item.Icon != null;
            if (icon != null)
            {
                icon.enabled = hasSprite;
                if (hasSprite) icon.sprite = item.Icon;
            }
            if (iconText != null)
            {
                iconText.enabled = !hasSprite;
                if (!hasSprite) iconText.text = Initials(item.DisplayName);
            }

            bool showCount = item.IsMaterial && count > 1;
            if (countRoot != null) countRoot.SetActive(showCount);
            // ASCII 'x' (not ×) — the default TMP font lacks U+00D7. Swap to '×'
            // once Fredoka/Nunito TMP font assets are imported.
            if (showCount && countText != null) countText.text = $"x{count}";

            SetSelected(false);
            gameObject.SetActive(true);
        }

        /// <summary>Shows an empty filler cell (no item, muted frame).</summary>
        public void BindEmpty(RarityPalette palette)
        {
            Item = null;
            if (frame != null) frame.color = palette != null ? palette.emptyMain : Color.grey;
            if (fill != null) fill.color = palette != null ? palette.emptySoft : Color.white;
            if (icon != null) icon.enabled = false;
            if (iconText != null) iconText.enabled = false;
            if (countRoot != null) countRoot.SetActive(false);
            SetSelected(false);
            gameObject.SetActive(true);
        }

        /// <summary>Toggles the dark selection ring.</summary>
        public void SetSelected(bool selected)
        {
            if (selectRing != null) selectRing.SetActive(selected);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Item != null) Clicked?.Invoke(this);
        }

        private static string Initials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Split(' ');
            if (parts.Length >= 2 && parts[1].Length > 0)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }
    }
}
