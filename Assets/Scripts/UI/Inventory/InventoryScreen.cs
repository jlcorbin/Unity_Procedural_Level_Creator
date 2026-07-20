// InventoryScreen.cs — Candy Cloud Inventory / Character screen.
//
// Binds the real PlayerInventory (unique gear list + stackable materials) to the
// bag grid, equipment slots, stats readout and selected-item detail strip.
//
// Toggle pattern (M18 lesson): THIS component's GameObject stays active so its
// input subscription lives; only `panelRoot` is shown/hidden. Opening sets
// Time.timeScale = 0 (restored to the captured previous value on close).
//
// Design: Documentation/Asset and inventory UI design/design_handoff_candy_cloud/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LevelGen.Items;
using LevelGen.Player;
using LevelGen.Combat;

namespace LevelGen.UI
{
    /// <summary>Inventory / Character screen. Built by <c>InventoryUIBuilder</c>.</summary>
    [DisallowMultipleComponent]
    public class InventoryScreen : MonoBehaviour
    {
        /// <summary>Bag category filter tabs.</summary>
        public enum Category { All, Weapons, Armor, Materials, Potions }

        [Header("Root / data")]
        [Tooltip("The visual panel toggled on/off. This component's own GameObject stays active.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RarityPalette palette;

        [Header("Bag grid")]
        [SerializeField] private Transform gridContainer;
        [SerializeField] private InventoryItemCell cellPrefab;
        [Tooltip("Grid is padded with empty cells up to this count so the layout stays full.")]
        [SerializeField] private int minCells = 20;

        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private TMP_Text[] tabLabels;

        [Header("Equipment slots")]
        [SerializeField] private EquipmentSlotView[] equipmentSlots;

        [Header("Selected-item detail strip")]
        [SerializeField] private Image detailFrame;
        [SerializeField] private Image detailFill;
        [SerializeField] private Image detailIconFrame;
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailIconText;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailRarity;
        [SerializeField] private TMP_Text detailDamage;

        [Header("Stats / title")]
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text staminaText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button closeButton;

        [Header("Tab colours (design tokens)")]
        [SerializeField] private Color tabActiveBg = RarityPalette.Hex("#2E2A26");
        [SerializeField] private Color tabActiveFg = Color.white;
        [SerializeField] private Color tabIdleBg = new Color(0f, 0f, 0f, 0.05f);
        [SerializeField] private Color tabIdleFg = RarityPalette.Hex("#7A7062");

        // ── Runtime state ───────────────────────────────────────────────────
        private readonly List<InventoryItemCell> _cells = new List<InventoryItemCell>();
        private PlayerInputReader _input;
        private CharacterStatsRuntime _stats;
        private Category _active = Category.All;
        private ItemData _selected;
        private float _prevTimeScale = 1f;

        /// <summary>True while the panel is open.</summary>
        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);

            if (closeButton != null) closeButton.onClick.AddListener(Close);

            for (int i = 0; i < (tabButtons?.Length ?? 0); i++)
            {
                var cat = (Category)i;
                if (tabButtons[i] != null) tabButtons[i].onClick.AddListener(() => SetCategory(cat));
            }
        }

        private void OnEnable() => StartCoroutine(BindToPlayer());

        private void OnDisable()
        {
            if (_input != null) _input.OnToggleInventoryPerformed -= Toggle;
        }

        /// <summary>Waits for the player to exist, then subscribes to the toggle input.</summary>
        private IEnumerator BindToPlayer()
        {
            float deadline = Time.unscaledTime + 10f;
            while (Time.unscaledTime < deadline)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _input = player.GetComponent<PlayerInputReader>();
                    _stats = player.GetComponent<CharacterStatsRuntime>();
                    if (_input != null)
                    {
                        _input.OnToggleInventoryPerformed += Toggle;
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }
            Debug.LogWarning("[InventoryScreen] No tagged Player with PlayerInputReader found — " +
                             "the inventory key will not open the screen.", this);
        }

        // ── Open / close ────────────────────────────────────────────────────

        /// <summary>Toggles the panel open/closed.</summary>
        public void Toggle() { if (IsOpen) Close(); else Open(); }

        /// <summary>Opens the panel, frees the cursor, refreshes, and pauses the game.</summary>
        public void Open()
        {
            if (panelRoot == null || IsOpen) return;   // guard double-open (cursor request must stay balanced)
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            panelRoot.SetActive(true);
            LevelGen.Input.MouseLook.RequestUiCursor();
            Refresh();
        }

        /// <summary>Closes the panel, re-locks the cursor, and restores the time scale.</summary>
        public void Close()
        {
            if (panelRoot == null || !IsOpen) return;
            panelRoot.SetActive(false);
            LevelGen.Input.MouseLook.ReleaseUiCursor();
            Time.timeScale = _prevTimeScale;
        }

        private void OnDestroy()
        {
            // Don't strand a cursor request if the screen is torn down while open.
            if (IsOpen) LevelGen.Input.MouseLook.ReleaseUiCursor();
        }

        // ── Refresh ─────────────────────────────────────────────────────────

        /// <summary>Rebuilds the grid, slots, stats and detail strip from live data.</summary>
        public void Refresh()
        {
            RefreshTabs();
            RefreshGrid();
            RefreshSlots();
            RefreshStats();
            RefreshDetail();
        }

        private void SetCategory(Category category)
        {
            _active = category;
            RefreshTabs();
            RefreshGrid();
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < (tabButtons?.Length ?? 0); i++)
            {
                bool on = (Category)i == _active;
                var img = tabButtons[i] != null ? tabButtons[i].GetComponent<Image>() : null;
                if (img != null) img.color = on ? tabActiveBg : tabIdleBg;
                if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                    tabLabels[i].color = on ? tabActiveFg : tabIdleFg;
            }
        }

        /// <summary>Collects the (item, count) rows visible under the active filter.</summary>
        private List<(ItemData item, int count)> CollectVisible()
        {
            var rows = new List<(ItemData, int)>();
            var inv = PlayerInventory.Instance;
            if (inv == null) return rows;

            bool wantGear = _active == Category.All || _active == Category.Weapons || _active == Category.Armor;
            bool wantMats = _active == Category.All || _active == Category.Materials;

            if (wantGear)
            {
                foreach (var g in inv.Items)
                {
                    if (g == null || g.IsMaterial) continue;
                    bool isArmor = g.Slot == EquipSlot.Armor;
                    if (_active == Category.Weapons && isArmor) continue;
                    if (_active == Category.Armor && !isArmor) continue;
                    rows.Add((g, 1));
                }
            }

            if (wantMats)
            {
                foreach (var kv in inv.Materials)
                    if (kv.Key != null) rows.Add((kv.Key, kv.Value));
            }

            return rows;
        }

        private void RefreshGrid()
        {
            if (gridContainer == null || cellPrefab == null) return;

            var rows = CollectVisible();
            int needed = Mathf.Max(rows.Count, minCells);

            // Grow the pool to the needed size.
            while (_cells.Count < needed)
            {
                var cell = Instantiate(cellPrefab, gridContainer);
                cell.Clicked += OnCellClicked;
                _cells.Add(cell);
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                if (i < rows.Count) _cells[i].Bind(rows[i].item, rows[i].count, palette);
                else if (i < needed) _cells[i].BindEmpty(palette);
                else _cells[i].gameObject.SetActive(false);
            }

            // Keep the selection ring in sync.
            foreach (var c in _cells) c.SetSelected(_selected != null && c.Item == _selected);
        }

        private void OnCellClicked(InventoryItemCell cell)
        {
            _selected = cell.Item;
            foreach (var c in _cells) c.SetSelected(c == cell);
            RefreshDetail();
        }

        private void RefreshSlots()
        {
            var inv = PlayerInventory.Instance;
            foreach (var slot in equipmentSlots)
            {
                if (slot == null) continue;
                ItemData equipped = (!slot.Locked && inv != null) ? inv.GetEquipped(slot.Slot) : null;
                slot.Bind(equipped, palette);
            }
        }

        private void RefreshStats()
        {
            var inv = PlayerInventory.Instance;
            var melee = inv != null ? inv.GetEquipped(EquipSlot.Melee) : null;

            if (damageText != null) damageText.text = (melee != null ? melee.Damage : 0).ToString();
            if (armorText != null) armorText.text = _stats != null ? Mathf.RoundToInt(_stats.Defense).ToString() : "0";
            if (hpText != null) hpText.text = _stats != null ? _stats.MaxHP.ToString() : "0";
            if (staminaText != null) staminaText.text = _stats != null ? _stats.MaxStamina.ToString() : "0";
            if (titleText != null && _stats != null) titleText.text = _stats.DisplayName;
        }

        private void RefreshDetail()
        {
            bool has = _selected != null;
            if (detailName != null) detailName.text = has ? _selected.DisplayName : "-";
            if (detailRarity != null)
            {
                detailRarity.text = has ? _selected.Rarity.ToString().ToUpper() : "";
                if (has && palette != null) detailRarity.color = palette.Get(_selected.Rarity).text;
            }
            if (detailDamage != null)
                detailDamage.text = has && !_selected.IsMaterial ? _selected.Damage.ToString() : "-";

            var c = (has && palette != null) ? palette.Get(_selected.Rarity) : new RarityColorSet();
            if (detailFrame != null) detailFrame.color = has ? c.main : (palette != null ? palette.emptyMain : Color.grey);
            if (detailFill != null) detailFill.color = has ? c.soft : (palette != null ? palette.emptySoft : Color.white);
            if (detailIconFrame != null) detailIconFrame.color = has ? c.main : Color.grey;

            bool hasSprite = has && _selected.Icon != null;
            if (detailIcon != null)
            {
                detailIcon.enabled = hasSprite;
                if (hasSprite) detailIcon.sprite = _selected.Icon;
            }
            if (detailIconText != null)
            {
                detailIconText.enabled = has && !hasSprite;
                if (has && !hasSprite)
                    detailIconText.text = _selected.DisplayName != null && _selected.DisplayName.Length > 0
                        ? _selected.DisplayName.Substring(0, 1).ToUpper() : "?";
            }
        }
    }
}
